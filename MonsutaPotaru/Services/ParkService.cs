using Microsoft.EntityFrameworkCore;
using PKHeX.Core;
using MonsutaPotaru.Data;

namespace MonsutaPotaru.Services;

/// <summary>Pokémon du parc, avec ce qu'il faut pour l'animer (sprites Donjon Mystère, famille, caractère).</summary>
/// <param name="SpriteFolder">Dossier de ses sprites Donjon Mystère (« images/donjon/0025/0000/0001/ » : chromatique).</param>
/// <param name="Family">Espèce de base de sa famille d'évolution (Pikachu et Raichu : Pichu), pour les affinités.</param>
/// <param name="Nature">Nature (index PKHeX), qui donne son caractère : plus actif, plus dormeur...</param>
/// <param name="Mood">Humeur selon les habitats du parc (<see cref="ParkHabitats.MoodOf"/>) : « happy », « normal » ou
/// « sad » (un malheureux attaque les autres de temps en temps, parc.js).</param>
/// <param name="Moves">Façon de se déplacer dans ce paysage (<see cref="ParkHabitats.MovementOf(byte, byte, ParkLandscape)"/>) :
/// « swim » (nage), « lava » (va dans la lave), « swim-lava » (les deux), « fly » (survole l'eau, la lave, les rebords et
/// les flancs de la montagne, se pose à terre ou sur un sommet) ou null (marche seulement).</param>
public sealed record ParkPokemon(int Id, ushort Species, string SpeciesName, string Nickname, byte Level, bool IsShiny,
    string SpriteFolder, int Family, byte Nature, string Mood = "normal", string? Moves = null);

/// <summary>Pokémon proposé dans la fenêtre « Choisir les Pokémon » (sans les œufs).</summary>
/// <param name="InPark">Il vit dans le parc affiché.</param>
/// <param name="HasSprites">SpriteCollab a ses sprites Donjon Mystère : sinon, grisé (64 espèces récentes n'en ont pas encore).</param>
/// <param name="OtherPark">Paysage de l'autre parc où il vit (le choisir l'en fait partir) ; null s'il n'est dans aucun autre.</param>
/// <param name="Refusal">Ce qui l'empêche de vivre dans ce paysage (« Ne nage pas » pour l'océan,
/// <see cref="ParkLandscapes.Refusal"/>) : grisé ; null s'il peut y aller.</param>
public sealed record ParkCandidate(int Id, ushort Species, string SpeciesName, string Nickname, byte Level, bool IsShiny,
    string SpriteUrl, int? StoredSaveId, bool InPark, bool HasSprites, ParkLandscape? OtherPark = null, string? Refusal = null);

/// <summary>Parc d'un joueur, pour la liste déroulante de la page.</summary>
/// <param name="Count">Pokémon qui y vivent.</param>
public sealed record ParkInfo(int Id, ParkLandscape Landscape, int Expansions, int Count)
{
    public string Name => ParkLandscapes.Name(Landscape);
    public int Capacity => ParkService.CapacityOf(Expansions);
}

public sealed record ParkResult(bool Ok, string Message);

/// <param name="Expansions">Agrandissements achetés, après l'achat (0 à <see cref="ParkService.MaxExpansions"/>).</param>
public sealed record ParkExpansionResult(bool Ok, string Message, int Expansions = 0, long Balance = 0);

/// <param name="ParkId">Le parc acheté (0 si l'achat est refusé).</param>
public sealed record ParkPurchaseResult(bool Ok, string Message, int ParkId = 0, long Balance = 0);

/// <summary>
/// « Parc » : des Pokémon de la collection (stockage interne ou sauvegardes stockées), de toutes les générations, qui s'y
/// promènent avec les sprites Donjon Mystère de leur forme (animation dans le navigateur : wwwroot/js/parc.js). Les espèces
/// dont SpriteCollab n'a pas encore les sprites ne peuvent pas y aller. Chaque joueur a la Forêt (créée à sa première
/// visite) et achète les autres paysages (<see cref="BuyAsync"/>, un parc par paysage) ; un Pokémon vit dans un seul parc.
/// Chaque parc accueille jusqu'à <see cref="CapacityOf"/> Pokémon et s'agrandit en payant avec le porte-monnaie
/// (<see cref="ExpandAsync"/>) : chaque taille a sa carte (wwwroot/parc/{carte}-{taille}.json, <see cref="ParkLandscapes.MapName"/>).
/// Le numéro d'un parc vient du navigateur (liste déroulante) : chaque méthode revérifie qu'il est à ce joueur.
/// </summary>
public sealed class ParkService(IDbContextFactory<ApplicationDbContext> dbFactory, ILogger<ParkService> logger)
{
    /// <summary>Pokémon accueillis sans agrandissement.</summary>
    public const int BaseCapacity = 20;
    /// <summary>Pokémon de plus à chaque agrandissement.</summary>
    public const int ExpansionCapacity = 20;
    /// <summary>Agrandissements possibles pour l'instant (80 Pokémon) : une carte par taille, de 1 à MaxExpansions + 1.</summary>
    public const int MaxExpansions = 3;
    /// <summary>Prix d'un agrandissement (40 000 ₽ jusqu'au 2026-11-07, puis 25 000 : choix de l'utilisateur).</summary>
    public const long ExpansionPrice = 25_000;
    /// <summary>Prix d'un nouveau parc : celui d'un agrandissement (choix de l'utilisateur).</summary>
    public const long ParkPrice = ExpansionPrice;

    private const string UnknownPark = "Ce parc n'existe pas.";

    public static int CapacityOf(int expansions) => BaseCapacity + ExpansionCapacity * expansions;

    /// <summary>Parcs du joueur, dans l'ordre de leur achat : la Forêt d'abord (créée ici à sa première visite).</summary>
    public async Task<List<ParkInfo>> ListParksAsync(string ownerId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await EnsureForestAsync(db, ownerId);
        return await db.Parks.AsNoTracking()
            .Where(p => p.OwnerId == ownerId)
            .OrderBy(p => p.Id)
            .Select(p => new ParkInfo(p.Id, p.Landscape, p.Expansions, db.ParkMembers.Count(m => m.ParkId == p.Id)))
            .ToListAsync();
    }

    /// <summary>
    /// La Forêt, premier parc de chacun, créée à la première visite (la migration ParkLandscapes l'a créée pour les comptes
    /// d'avant). « OR IGNORE » : deux pages ouvertes en même temps n'en créent pas deux (index unique).
    /// </summary>
    private static async Task EnsureForestAsync(ApplicationDbContext db, string ownerId)
    {
        if (await db.Parks.AnyAsync(p => p.OwnerId == ownerId && p.Landscape == ParkLandscape.Forest))
            return;
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT OR IGNORE INTO Parks (OwnerId, Landscape, Expansions) SELECT Id, {(int)ParkLandscape.Forest}, 0 FROM AspNetUsers WHERE Id = {ownerId}");
    }

    private static Task<Park?> FindAsync(ApplicationDbContext db, string ownerId, int parkId) =>
        db.Parks.AsNoTracking().FirstOrDefaultAsync(p => p.Id == parkId && p.OwnerId == ownerId);

    /// <summary>
    /// Achète un parc de ce paysage pour <see cref="ParkPrice"/> : parc, débit et ligne du journal de l'inventaire en une
    /// seule opération. Le paysage vient du navigateur : refusé s'il n'existe pas ou si le joueur l'a déjà (l'index unique
    /// le vérifie aussi pour deux achats simultanés).
    /// </summary>
    public async Task<ParkPurchaseResult> BuyAsync(string ownerId, ParkLandscape landscape)
    {
        if (!Enum.IsDefined(landscape))
            return new(false, "Ce paysage n'existe pas.");
        await using var db = await dbFactory.CreateDbContextAsync();
        await EnsureForestAsync(db, ownerId);
        await using var tx = await db.Database.BeginTransactionAsync();

        var added = await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT OR IGNORE INTO Parks (OwnerId, Landscape, Expansions) VALUES ({ownerId}, {(int)landscape}, 0)");
        if (added == 0)
            return new(false, $"Tu as déjà un parc « {ParkLandscapes.Name(landscape)} ».");

        // Débit conditionnel et atomique ; sans assez d'argent, la transaction (parc compris) est annulée
        var debited = await db.Wallets.Where(w => w.OwnerId == ownerId && w.Money >= ParkPrice)
            .ExecuteUpdateAsync(w => w.SetProperty(x => x.Money, x => x.Money - ParkPrice));
        if (debited == 0)
        {
            await tx.RollbackAsync();
            var available = await db.Wallets.Where(w => w.OwnerId == ownerId).Select(w => w.Money).FirstOrDefaultAsync();
            return new(false, $"Un nouveau parc coûte {InventoryService.FormatMoney(ParkPrice)}, ton porte-monnaie contient " +
                              $"{InventoryService.FormatMoney(available)} : il manque {InventoryService.FormatMoney(ParkPrice - available)}. " +
                              "Retire de l'argent d'une sauvegarde depuis « Inventaire ».", Balance: available);
        }

        var parkId = await db.Parks.Where(p => p.OwnerId == ownerId && p.Landscape == landscape).Select(p => p.Id).FirstAsync();
        var balance = await db.Wallets.Where(w => w.OwnerId == ownerId).Select(w => w.Money).FirstAsync();
        db.WalletOperations.Add(new WalletOperation
        {
            OwnerId = ownerId,
            At = DateTime.UtcNow,
            Kind = WalletOperationKind.BuyPark,
            Amount = -ParkPrice,
            BalanceAfter = balance,
            Details = $"Nouveau parc : {ParkLandscapes.Name(landscape)}",
        });
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Utilisateur {User} : parc {Landscape} acheté.", ownerId, landscape);
        return new(true, $"Nouveau parc : {ParkLandscapes.Name(landscape)}. Il accueille {CapacityOf(0)} Pokémon, choisis ceux " +
                         $"qui s'y installent. Solde : {InventoryService.FormatMoney(balance)}.", parkId, balance);
    }

    /// <summary>
    /// Agrandit le parc de <see cref="ExpansionCapacity"/> Pokémon pour <see cref="ExpansionPrice"/> : taille, débit et ligne
    /// du journal de l'inventaire en une seule opération. La taille et le solde sont vérifiés par les mises à jour
    /// elles-mêmes : deux achats simultanés ne dépassent ni la plus grande taille ni le solde.
    /// </summary>
    public async Task<ParkExpansionResult> ExpandAsync(string ownerId, int parkId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();

        var expanded = await db.Parks.Where(p => p.Id == parkId && p.OwnerId == ownerId && p.Expansions < MaxExpansions)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.Expansions, x => x.Expansions + 1));
        if (expanded == 0)
            return await FindAsync(db, ownerId, parkId) is null
                ? new(false, UnknownPark)
                : new(false, $"Ce parc a déjà sa plus grande taille ({CapacityOf(MaxExpansions)} Pokémon).", MaxExpansions);

        // Débit conditionnel et atomique ; sans assez d'argent, la transaction (taille comprise) est annulée
        var debited = await db.Wallets.Where(w => w.OwnerId == ownerId && w.Money >= ExpansionPrice)
            .ExecuteUpdateAsync(w => w.SetProperty(x => x.Money, x => x.Money - ExpansionPrice));
        if (debited == 0)
        {
            await tx.RollbackAsync();
            var available = await db.Wallets.Where(w => w.OwnerId == ownerId).Select(w => w.Money).FirstOrDefaultAsync();
            return new(false, $"L'agrandissement coûte {InventoryService.FormatMoney(ExpansionPrice)}, ton porte-monnaie contient " +
                              $"{InventoryService.FormatMoney(available)} : il manque {InventoryService.FormatMoney(ExpansionPrice - available)}. " +
                              "Retire de l'argent d'une sauvegarde depuis « Inventaire ».", Balance: available);
        }

        var park = await db.Parks.Where(p => p.Id == parkId).Select(p => new { p.Expansions, p.Landscape }).FirstAsync();
        var balance = await db.Wallets.Where(w => w.OwnerId == ownerId).Select(w => w.Money).FirstAsync();
        db.WalletOperations.Add(new WalletOperation
        {
            OwnerId = ownerId,
            At = DateTime.UtcNow,
            Kind = WalletOperationKind.ExpandPark,
            Amount = -ExpansionPrice,
            BalanceAfter = balance,
            Details = $"{ParkLandscapes.Name(park.Landscape)} : {CapacityOf(park.Expansions - 1)} → {CapacityOf(park.Expansions)} Pokémon",
        });
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Utilisateur {User} : parc {Landscape} agrandi ({Expansions} agrandissements).", ownerId, park.Landscape, park.Expansions);
        return new(true, $"Parc agrandi : il accueille maintenant {CapacityOf(park.Expansions)} Pokémon. " +
                         $"Solde : {InventoryService.FormatMoney(balance)}.", park.Expansions, balance);
    }

    /// <summary>Pokémon du parc, dans l'ordre de leur arrivée (aucun si le parc n'est pas à ce joueur).</summary>
    public async Task<List<ParkPokemon>> ListAsync(string ownerId, int parkId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        if (await FindAsync(db, ownerId, parkId) is not { } park)
            return [];
        var members = await db.ParkMembers.AsNoTracking()
            .Where(m => m.ParkId == parkId)
            .OrderBy(m => m.AddedAt).ThenBy(m => m.BankPokemonId)
            .Select(m => m.BankPokemon!)
            .Select(p => new { p.Id, p.Species, p.Form, p.Context, p.SpeciesName, p.Nickname, p.Level, p.IsShiny, p.Gender, p.HeldItem, p.Nature, p.Type1, p.Type2 })
            .ToListAsync();
        // Humeur selon les habitats de la carte du parc (son paysage, sa taille)
        var map = ParkHabitats.Map(park.Landscape, park.Expansions + 1);
        return members.Select(p => new ParkPokemon(p.Id, p.Species, p.SpeciesName, p.Nickname, p.Level, p.IsShiny,
            PokemonSprites.DonjonFolder(p.Species, p.Form, (EntityContext)p.Context, p.IsShiny, p.Gender == 1, p.HeldItem ?? 0),
            Family(p.Species), p.Nature ?? 0, ParkHabitats.MoodOf(p.Type1, p.Type2, map).Mood.ToString().ToLowerInvariant(),
            ParkHabitats.MovementOf(p.Type1, p.Type2, park.Landscape))).ToList();
    }

    /// <summary>
    /// Fiche d'un Pokémon de ce parc du joueur (clic sur le Pokémon), ou null s'il n'y est pas. Les numéros viennent du
    /// navigateur : un visiteur ne doit pas pouvoir lire ainsi le reste de la collection du joueur.
    /// </summary>
    public async Task<BankPokemon?> GetMemberAsync(string ownerId, int parkId, int bankPokemonId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.ParkMembers.AsNoTracking()
            .Where(m => m.OwnerId == ownerId && m.ParkId == parkId && m.BankPokemonId == bankPokemonId)
            .Select(m => m.BankPokemon!)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Pokémon qui peuvent aller dans ce parc (sans les œufs), filtrés comme dans « Ma collection » (pastilles), par numéro
    /// de Pokédex ; ceux d'un autre parc du joueur portent son paysage, ceux que ce paysage refuse (un Pokémon qui ne nage
    /// pas, pour l'océan) la raison.
    /// </summary>
    public async Task<List<ParkCandidate>> ListCandidatesAsync(string ownerId, int parkId, PokemonCriteria? criteria = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var landscape = (await FindAsync(db, ownerId, parkId))?.Landscape ?? ParkLandscape.Forest;
        var placed = await db.ParkMembers.AsNoTracking().Where(m => m.OwnerId == ownerId)
            .Select(m => new { m.BankPokemonId, m.ParkId, m.Park!.Landscape })
            .ToDictionaryAsync(m => m.BankPokemonId);
        var query = Eligible(db, ownerId);
        if (criteria is not null)
            query = criteria.Apply(query);
        var candidates = await query
            .OrderBy(p => p.Species).ThenByDescending(p => p.Level).ThenBy(p => p.Id)
            .Select(p => new { p.Id, p.Species, p.Form, p.Context, p.SpeciesName, p.Nickname, p.Level, p.IsShiny, p.Gender, p.HeldItem, p.StoredSaveId, p.Type1, p.Type2 })
            .ToListAsync();
        return candidates.Select(p =>
        {
            var home = placed.GetValueOrDefault(p.Id);
            return new ParkCandidate(p.Id, p.Species, p.SpeciesName, p.Nickname, p.Level, p.IsShiny,
                PokemonSprites.Url(p.Species, p.Form, (EntityContext)p.Context, p.Gender == 1, p.HeldItem ?? 0, p.IsShiny),
                p.StoredSaveId, home?.ParkId == parkId, PokemonSprites.HasDonjonSprites(p.Species),
                home is not null && home.ParkId != parkId ? home.Landscape : null, ParkLandscapes.Refusal(landscape, p.Type1, p.Type2));
        }).ToList();
    }

    /// <summary>
    /// Remplace les Pokémon du parc par <paramref name="ids"/> (choix de la fenêtre « Choisir les Pokémon ») : ceux qui y
    /// restent gardent leur place, les nouveaux arrivent après ; ceux d'un autre parc du joueur le quittent pour venir ici.
    /// Un parc qui a plus de Pokémon que sa capacité (30 avant les agrandissements, 25 ensuite, 20 depuis) les garde : on
    /// peut en retirer, pas en ajouter tant qu'il dépasse. Un Pokémon que le paysage refuse (<see cref="ParkLandscapes.Refusal"/>)
    /// est refusé ici aussi : la liste vient du navigateur.
    /// </summary>
    public async Task<ParkResult> SetAsync(string ownerId, int parkId, IReadOnlyCollection<int> ids)
    {
        var wanted = ids.ToHashSet();
        await using var db = await dbFactory.CreateDbContextAsync();
        if (await FindAsync(db, ownerId, parkId) is not { } park)
            return new(false, UnknownPark);
        var capacity = CapacityOf(park.Expansions);
        if (wanted.Count > capacity)
        {
            var current = await db.ParkMembers.Where(m => m.ParkId == parkId).Select(m => m.BankPokemonId).ToListAsync();
            if (!wanted.IsSubsetOf(current))
                return new(false, $"Le parc accueille {capacity} Pokémon au plus : retires-en {wanted.Count - capacity}.");
        }

        var eligible = await Eligible(db, ownerId).Where(p => wanted.Contains(p.Id)).Select(p => new { p.Id, p.Species, p.SpeciesName, p.Type1, p.Type2 }).ToListAsync();
        if (eligible.Count != wanted.Count)
            return new(false, "Seuls tes Pokémon (pas les œufs) peuvent aller au parc.");
        if (eligible.FirstOrDefault(p => !PokemonSprites.HasDonjonSprites(p.Species)) is { } withoutSprites)
            return new(false, $"{withoutSprites.SpeciesName} n'a pas encore de sprites Donjon Mystère : il ne peut pas aller au parc.");
        if (eligible.FirstOrDefault(p => ParkLandscapes.Refusal(park.Landscape, p.Type1, p.Type2) is not null) is { } refused)
            return new(false, RefusalMessage(refused.SpeciesName, park.Landscape));

        await using var tx = await db.Database.BeginTransactionAsync();
        var now = DateTime.UtcNow;
        await db.ParkMembers.Where(m => m.ParkId == parkId && !wanted.Contains(m.BankPokemonId)).ExecuteDeleteAsync();
        // Venus d'un autre parc : ils arrivent après ceux qui restent, comme les nouveaux
        var moved = await db.ParkMembers.Where(m => m.OwnerId == ownerId && m.ParkId != parkId && wanted.Contains(m.BankPokemonId))
            .ExecuteUpdateAsync(m => m.SetProperty(x => x.ParkId, parkId).SetProperty(x => x.AddedAt, now));
        var present = await db.ParkMembers.Where(m => m.ParkId == parkId).Select(m => m.BankPokemonId).ToListAsync();
        db.ParkMembers.AddRange(wanted.Except(present).Select(id => new ParkMember { BankPokemonId = id, OwnerId = ownerId, ParkId = parkId, AddedAt = now }));
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        logger.LogInformation("Utilisateur {User} : parc {Park} de {Count} Pokémon ({Moved} venus d'un autre parc).", ownerId, parkId, wanted.Count, moved);
        if (wanted.Count == 0)
            return new(true, "Le parc est vide.");
        return new(true, $"Parc mis à jour : {wanted.Count} Pokémon" + moved switch
        {
            0 => ".",
            1 => ", dont 1 venu d'un autre parc.",
            _ => $", dont {moved} venus d'un autre parc.",
        });
    }

    /// <summary>Retire un Pokémon de son parc (il reste dans la collection).</summary>
    public async Task<bool> RemoveAsync(string ownerId, int bankPokemonId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.ParkMembers.Where(m => m.OwnerId == ownerId && m.BankPokemonId == bankPokemonId).ExecuteDeleteAsync() > 0;
    }

    /// <summary>Vide le parc (bouton « Vider le parc ») : ses Pokémon restent dans la collection, les autres parcs ne bougent pas.</summary>
    public async Task<ParkResult> ClearAsync(string ownerId, int parkId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var count = await db.ParkMembers.Where(m => m.OwnerId == ownerId && m.ParkId == parkId).ExecuteDeleteAsync();
        if (count == 0)
            return new(false, "Le parc est déjà vide.");
        logger.LogInformation("Utilisateur {User} : parc {Park} vidé ({Count} Pokémon).", ownerId, parkId, count);
        return new(true, count == 1
            ? "Le parc est vide : son Pokémon reste dans ta collection."
            : $"Le parc est vide : ses {count} Pokémon restent dans ta collection.");
    }

    /// <summary>Message d'un Pokémon que ce paysage refuse (fenêtre de choix et <see cref="SetAsync"/>).</summary>
    public static string RefusalMessage(string speciesName, ParkLandscape landscape) =>
        $"{speciesName} ne nage pas : seuls les Pokémon qui nagent (type Eau) peuvent vivre {ParkLandscapes.Where(landscape)}.";

    private static IQueryable<BankPokemon> Eligible(ApplicationDbContext db, string ownerId) =>
        db.BankPokemon.AsNoTracking().Where(p => p.OwnerId == ownerId && !p.IsEgg && p.Species >= 1);

    /// <summary>Espèce de base de la famille d'évolution (celle qui sort de l'œuf) : Pikachu, Raichu → Pichu.</summary>
    public static int Family(ushort species) => PersonalTable.SV.GetFormEntry(species, 0).HatchSpecies;
}
