using Microsoft.EntityFrameworkCore;
using PKHeX.Core;
using MonsutaPotaru.Data;

namespace MonsutaPotaru.Services;

/// <summary>Critère de tri de la vue générale des rubans : ordre d'ajout au suivi, nom, ou complétion.</summary>
public enum RibbonSort { Arrival, Name, Completion }

/// <summary>
/// Carte d'un Pokémon suivi : son dernier état connu, ses rubans obtenus sur le total (ceux des souvenirs un par un), et où
/// il est (null : plus dans le compte).
/// </summary>
public sealed record RibbonSummary(int Id, string Nickname, string SpeciesName, string SpriteUrl, byte Level, bool IsShiny,
    int Owned, int Total, string? Location, DateTime CreatedAt)
{
    /// <summary>Encore dans le compte (stockage interne ou sauvegarde stockée).</summary>
    public bool Present => Location is not null;

    /// <summary>Nom affiché : le surnom, et l'espèce quand il en a un (« PIKACHU » de 3e génération n'en est pas un).</summary>
    public bool HasNickname => RibbonService.IsNickname(Nickname, SpeciesName);
}

/// <summary>Page d'un suivi : le Pokémon, où il est (null : parti), et l'état de chaque ruban compté.</summary>
public sealed record RibbonView(int Id, string Nickname, string SpeciesName, string SpriteUrl, byte Level, bool IsShiny,
    string? Location, int? BankId, IReadOnlyList<RibbonState> Ribbons)
{
    public bool Present => Location is not null;
    public bool HasNickname => RibbonService.IsNickname(Nickname, SpeciesName);
    /// <summary>Rubans obtenus sur <see cref="Total"/>, ceux des souvenirs un par un (<see cref="RibbonState.Earned"/>).</summary>
    public int Earned => Ribbons.Sum(r => r.Earned);

    public int Total => Ribbons.Sum(r => r.Worth);
}

/// <summary>Pokémon de la collection qu'on peut ajouter au suivi.</summary>
public sealed record RibbonCandidate(int BankId, string Nickname, string SpeciesName, byte Level, bool IsShiny, string SpriteUrl, string Location);

public sealed record RibbonResult(bool Ok, string Message, int Id = 0);

/// <summary>
/// Suivi des rubans (« Défis » → « Rubans ») : n'importe quel Pokémon de la collection (stockage interne ou sauvegarde
/// stockée, pas les œufs), retrouvé par son empreinte, et ses rubans comptés (<see cref="Ribbons"/>). Chaque lecture
/// reprend l'état du Pokémon dans le compte ; parti, il garde son dernier état connu.
/// </summary>
public sealed class RibbonService(IDbContextFactory<ApplicationDbContext> dbFactory, ILogger<RibbonService> logger)
{
    /// <summary>Nombre de Pokémon proposés à la fois dans l'ajout (la recherche affine).</summary>
    public const int CandidatePage = 60;

    /// <summary>Pokémon suivis du compte, mis à jour d'après la collection.</summary>
    public async Task<List<RibbonSummary>> ListAsync(string ownerId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var trackers = await db.RibbonTrackers.Where(r => r.OwnerId == ownerId).ToListAsync();
        var current = await RefreshAsync(db, ownerId, trackers);
        return [.. trackers.Select(t => new RibbonSummary(t.Id, t.Nickname, t.SpeciesName, SpriteOf(t), t.Level, t.IsShiny,
            Ribbons.CountOwned(PkmOf(t)), Ribbons.Total, current.GetValueOrDefault(t.Fingerprint)?.Location, t.CreatedAt))];
    }

    /// <summary>Un suivi du compte, mis à jour d'après la collection ; null s'il n'existe pas (ou plus).</summary>
    public async Task<RibbonView?> GetAsync(string ownerId, int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var tracker = await db.RibbonTrackers.FirstOrDefaultAsync(r => r.Id == id && r.OwnerId == ownerId);
        if (tracker is null)
            return null;
        var current = (await RefreshAsync(db, ownerId, [tracker])).GetValueOrDefault(tracker.Fingerprint);
        return new RibbonView(tracker.Id, tracker.Nickname, tracker.SpeciesName, SpriteOf(tracker), tracker.Level, tracker.IsShiny,
            current?.Location, current?.BankId, Ribbons.Read(PkmOf(tracker)));
    }

    /// <summary>
    /// Pokémon de la collection pas encore suivis (ni œufs, ni Pokémon sans empreinte), cherchés par espèce ou surnom comme
    /// dans « Ma collection » ; les <see cref="CandidatePage"/> premiers par nom, puis du plus récent au plus ancien.
    /// </summary>
    public async Task<List<RibbonCandidate>> ListCandidatesAsync(string ownerId, string? search)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var tracked = db.RibbonTrackers.Where(r => r.OwnerId == ownerId).Select(r => r.Fingerprint);
        var query = db.BankPokemon.AsNoTracking()
            .Where(p => p.OwnerId == ownerId && !p.IsEgg && p.Fingerprint != null && p.Fingerprint != "" && !tracked.Contains(p.Fingerprint));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = $"%{search.Trim()}%";
            query = query.Where(p => EF.Functions.Like(p.SpeciesName, s) || EF.Functions.Like(p.Nickname, s));
        }
        var rows = await query.OrderBy(p => p.SortName).ThenBy(p => p.Species).ThenByDescending(p => p.DepositedAt).ThenBy(p => p.Position)
            .Take(CandidatePage)
            .Select(p => new
            {
                p.Id, p.Nickname, p.SpeciesName, p.Level, p.IsShiny, p.Species, p.Form, p.Context, p.Gender, p.HeldItem, p.Location,
                Game = p.StoredSave == null ? null : p.StoredSave.GameName,
                Trainer = p.StoredSave == null ? null : p.StoredSave.TrainerName,
            })
            .ToListAsync();
        return [.. rows.Select(p => new RibbonCandidate(p.Id, p.Nickname, p.SpeciesName, p.Level, p.IsShiny,
            PokemonSprites.Url(p.Species, p.Form, (EntityContext)p.Context, p.Gender == 1, p.HeldItem ?? 0, p.IsShiny, false),
            LocationOf(p.Game, p.Trainer, p.Location)))];
    }

    /// <summary>Ajoute ce Pokémon du compte au suivi (une fois : retrouvé par son empreinte).</summary>
    public async Task<RibbonResult> AddAsync(string ownerId, int bankId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        // Identifiant venu du navigateur : le Pokémon doit être à ce compte.
        var pokemon = await db.BankPokemon.AsNoTracking().FirstOrDefaultAsync(p => p.Id == bankId && p.OwnerId == ownerId);
        if (pokemon is null)
            return new(false, "Ce Pokémon n'est plus dans ta collection.");
        if (pokemon.IsEgg)
            return new(false, "Un œuf ne peut pas avoir de rubans : fais-le éclore d'abord.");
        if (string.IsNullOrEmpty(pokemon.Fingerprint))
            return new(false, "Ce Pokémon ne peut pas être suivi : il n'a pas pu être identifié.");
        var name = pokemon.Nickname;
        if (await db.RibbonTrackers.AnyAsync(r => r.OwnerId == ownerId && r.Fingerprint == pokemon.Fingerprint))
            return new(false, $"{name} est déjà suivi.");

        var tracker = new RibbonTracker { OwnerId = ownerId, Fingerprint = pokemon.Fingerprint, CreatedAt = DateTime.UtcNow };
        CopyFrom(tracker, pokemon);
        db.RibbonTrackers.Add(tracker);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException) // ajouté au même moment depuis un autre onglet (index unique)
        {
            return new(false, $"{name} est déjà suivi.");
        }
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Utilisateur {User} : suivi des rubans {Tracker} ajouté (Pokémon {Pokemon}).", ownerId, tracker.Id, bankId);
        return new(true, $"{name} ajouté au suivi des rubans.", tracker.Id);
    }

    /// <summary>Arrête le suivi (le Pokémon, lui, ne bouge pas).</summary>
    public async Task<bool> DeleteAsync(string ownerId, int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.RibbonTrackers.Where(r => r.Id == id && r.OwnerId == ownerId).ExecuteDeleteAsync() > 0;
    }

    /// <summary>
    /// Cartes triées. Par ajout : du premier ajouté au dernier ; par nom : le surnom affiché, sans accents ni majuscules ;
    /// par complétion : la part exacte de rubans obtenus. À égalité, toujours par ajout. <paramref name="descending"/>
    /// inverse le critère.
    /// </summary>
    public static List<RibbonSummary> Sort(IEnumerable<RibbonSummary> trackers, RibbonSort sort, bool descending)
    {
        var byArrival = trackers.OrderBy(t => t.CreatedAt).ThenBy(t => t.Id).ToList();
        Comparison<RibbonSummary> compare = sort switch
        {
            RibbonSort.Name => (a, b) => string.CompareOrdinal(BankPokemon.SortKey(a.Nickname), BankPokemon.SortKey(b.Nickname)),
            RibbonSort.Completion => (a, b) => ((long)a.Owned * Math.Max(b.Total, 1)).CompareTo((long)b.Owned * Math.Max(a.Total, 1)),
            _ => (_, _) => 0,
        };
        if (sort == RibbonSort.Arrival)
        {
            if (descending)
                byArrival.Reverse();
            return byArrival;
        }
        // OrderBy est stable : à égalité, l'ordre d'ajout reste.
        return [.. byArrival.Order(Comparer<RibbonSummary>.Create((a, b) => descending ? compare(b, a) : compare(a, b)))];
    }

    /// <summary>Le nom du Pokémon diffère-t-il de son espèce (majuscules et accents mis à part) ?</summary>
    public static bool IsNickname(string nickname, string species) =>
        !string.Equals(BankPokemon.SortKey(nickname), BankPokemon.SortKey(species), StringComparison.Ordinal);

    /// <summary>Recherche de la vue générale : le surnom ou l'espèce, sans accents ni majuscules (« evoli » trouve Évoli).</summary>
    public static bool Matches(RibbonSummary tracker, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return true;
        var key = BankPokemon.SortKey(search.Trim());
        return BankPokemon.SortKey(tracker.Nickname).Contains(key, StringComparison.Ordinal)
            || BankPokemon.SortKey(tracker.SpeciesName).Contains(key, StringComparison.Ordinal);
    }

    /// <summary>Où est le Pokémon suivi dans le compte, et sa ligne actuelle.</summary>
    private sealed record Current(int BankId, string Location);

    /// <summary>
    /// Retrouve les Pokémon suivis dans la collection (par empreinte ; plusieurs exemplaires : le plus récent) et reprend
    /// l'état de ceux qui ont changé. Rend leur emplacement ; un Pokémon absent n'y figure pas.
    /// </summary>
    private static async Task<Dictionary<string, Current>> RefreshAsync(ApplicationDbContext db, string ownerId, List<RibbonTracker> trackers)
    {
        if (trackers.Count == 0)
            return [];
        var fingerprints = trackers.Select(t => t.Fingerprint).ToList();
        var rows = await db.BankPokemon.AsNoTracking()
            .Where(p => p.OwnerId == ownerId && !p.IsEgg && p.Fingerprint != null && fingerprints.Contains(p.Fingerprint))
            .OrderByDescending(p => p.DepositedAt).ThenByDescending(p => p.Id)
            .Select(p => new
            {
                Pokemon = p,
                Game = p.StoredSave == null ? null : p.StoredSave.GameName,
                Trainer = p.StoredSave == null ? null : p.StoredSave.TrainerName,
            })
            .ToListAsync();
        var found = rows.GroupBy(r => r.Pokemon.Fingerprint!).ToDictionary(g => g.Key, g => g.First());
        var changed = false;
        foreach (var tracker in trackers)
        {
            if (found.GetValueOrDefault(tracker.Fingerprint) is not { } row || tracker.Data.AsSpan().SequenceEqual(row.Pokemon.Data))
                continue;
            CopyFrom(tracker, row.Pokemon);
            changed = true;
        }
        if (changed)
            await db.SaveChangesAsync();
        return found.ToDictionary(f => f.Key, f => new Current(f.Value.Pokemon.Id, LocationOf(f.Value.Game, f.Value.Trainer, f.Value.Pokemon.Location)));
    }

    /// <summary>« Stockage interne », ou la sauvegarde et la place : « Platine — Ghal · Boîte 2, case 8 ».</summary>
    private static string LocationOf(string? game, string? trainer, string? place)
    {
        if (game is null)
            return "Stockage interne";
        var label = $"{game} — {trainer}";
        return string.IsNullOrEmpty(place) ? label : $"{label} · {place}";
    }

    private static void CopyFrom(RibbonTracker tracker, BankPokemon pokemon)
    {
        tracker.Data = pokemon.Data;
        tracker.Context = pokemon.Context;
        tracker.Species = pokemon.Species;
        tracker.Form = pokemon.Form;
        tracker.SpeciesName = pokemon.SpeciesName;
        tracker.Nickname = pokemon.Nickname;
        tracker.Level = pokemon.Level;
        tracker.IsShiny = pokemon.IsShiny;
        tracker.Gender = pokemon.Gender;
        tracker.HeldItem = pokemon.HeldItem;
    }

    private static PKM? PkmOf(RibbonTracker tracker) =>
        tracker.Data.Length == 0 ? null : EntityFormat.GetFromBytes(tracker.Data.ToArray(), (EntityContext)tracker.Context);

    private static string SpriteOf(RibbonTracker t) =>
        PokemonSprites.Url(t.Species, t.Form, (EntityContext)t.Context, t.Gender == 1, t.HeldItem ?? 0, t.IsShiny, false);
}
