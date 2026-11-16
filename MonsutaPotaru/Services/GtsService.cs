using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MonsutaPotaru.Data;
using PKHeX.Core;

namespace MonsutaPotaru.Services;

/// <summary>
/// Pokémon demandé en échange d'une offre de la GTS, ou cherché parmi les offres : une espèce (null : toutes, pour une
/// recherche), des niveaux (compris), un sexe (0 mâle, 1 femelle), chromatique ou non, et le refus des Pokémon illégaux.
/// </summary>
public sealed record GtsWanted(ushort? Species, byte? MinLevel = null, byte? MaxLevel = null, byte? Gender = null, bool? Shiny = null,
    bool LegalOnly = false)
{
    /// <summary>Bornes ramenées à 1-100 (0 ou vide : pas de borne), inversées si le minimum dépasse le maximum.</summary>
    public static GtsWanted Create(ushort? species, int? minLevel, int? maxLevel, byte? gender, bool? shiny, bool legalOnly)
    {
        static byte? Clamp(int? level) => level is > 0 ? (byte)Math.Min(level.Value, 100) : null;
        var (low, high) = (Clamp(minLevel), Clamp(maxLevel));
        if (low > high)
            (low, high) = (high, low);
        return new(species, low, high, gender is 0 or 1 ? gender : null, shiny, legalOnly);
    }

    /// <summary>Ce Pokémon répond à la demande (jamais un œuf).</summary>
    public bool Matches(ushort species, byte level, byte gender, bool shiny, bool legal, bool egg) =>
        !egg && (Species is null || Species == species) && (MinLevel is null || level >= MinLevel) && (MaxLevel is null || level <= MaxLevel)
        && (Gender is null || Gender == gender) && (Shiny is null || Shiny == shiny) && (!LegalOnly || legal);
}

/// <summary>Un Pokémon tel que la GTS l'affiche.</summary>
/// <param name="Gender">« ♂ », « ♀ », ou vide.</param>
public sealed record GtsPokemonView(ushort Species, string Name, string SpeciesName, byte Level, string Gender, bool IsShiny, bool IsLegal,
    string SpriteUrl)
{
    /// <summary>« Bob (Kadabra) » pour un Pokémon surnommé, sinon « Kadabra ».</summary>
    public string FullName => Name == SpeciesName ? SpeciesName : $"{Name} ({SpeciesName})";
}

/// <summary>
/// Une offre de la GTS : le Pokémon déposé, ce qui est demandé, qui l'a déposé (null : son profil est réglé sur
/// « Personne »), les jeux qui peuvent l'accepter, et si celui qui regarde a un Pokémon qui convient (<paramref name="Owned"/>).
/// </summary>
public sealed record GtsOfferView(int Id, GtsPokemonView Pokemon, string Wanted, string WantedSpriteUrl, string? Pseudo, string Games,
    DateTime CreatedAt, DateTime ExpiresAt, bool Owned, string Origin);

/// <summary>Une case de la boîte de réception, et les sauvegardes où elle peut aller (le stockage interne : toujours).</summary>
public sealed record GtsInboxItem(int Id, GtsPokemonView Pokemon, GtsStatus Status, string Note, DateTime ArrivedAt,
    IReadOnlyList<GtsDestination> Saves);

/// <summary>Sauvegarde où un Pokémon de la boîte de réception peut aller.</summary>
public sealed record GtsDestination(int SaveId, string Label);

/// <summary>
/// Pokémon que le joueur peut donner pour une offre : son identifiant dans la collection, d'où il vient, et pourquoi il
/// ne peut pas être choisi (<paramref name="Problem"/>, null s'il le peut).
/// </summary>
public sealed record GtsCandidate(int BankId, GtsPokemonView Pokemon, string Origin, string? Problem);

/// <summary>
/// Pokémon choisi pour un dépôt : sa vue, son origine, pourquoi il ne peut pas aller sur la GTS (null s'il le peut), et
/// les espèces que son jeu peut demander en échange (numéro, nom), dans l'ordre alphabétique.
/// </summary>
public sealed record GtsDepositChoice(GtsPokemonView Pokemon, string Origin, bool FromSave, string? Problem, string Games,
    IReadOnlyList<GtsSpecies> Species);

public sealed record GtsSpecies(ushort Id, string Name);

/// <summary>Résultat d'une opération de la GTS, prêt à afficher.</summary>
public sealed record GtsResult(bool Ok, string Message, TradeShow? Show = null);

/// <summary>
/// Ce que donnera l'échange choisi : <paramref name="Problem"/> s'il ne peut plus se faire, sinon les évolutions annoncées
/// (le Pokémon reçu, puis celui qui part ; null s'il n'y en a pas).
/// </summary>
public sealed record GtsPreview(string? Problem, string? ReceivedEvolution, string? GivenEvolution, string Arrival);

/// <summary>Signale les changements de la boîte de réception d'un compte (pastille du menu), d'un circuit à l'autre.</summary>
public sealed class GtsEvents
{
    public event Action<string>? InboxChanged;

    public void Notify(string userId) => InboxChanged?.Invoke(userId);
}

/// <summary>
/// GTS (Multijoueurs → GTS) : un joueur dépose un Pokémon de sa collection (stockage interne, ou équipe et boîtes d'une
/// sauvegarde stockée) et demande une espèce en échange, avec des critères ; un autre joueur qui a ce Pokémon accepte, et
/// l'échange se fait aussitôt, comme sur console (<see cref="TradeFamily"/>, évolution par échange). Le Pokémon reçu par
/// le déposant, comme celui d'une offre expirée (<see cref="Lifetime"/>), annulée ou retirée avec sa sauvegarde, attend
/// dans sa boîte de réception : aucune sauvegarde n'est écrite sans que son joueur soit là. Il le récupère dans le
/// stockage interne ou dans la sauvegarde de son choix.
/// </summary>
public sealed class GtsService(IDbContextFactory<ApplicationDbContext> dbFactory, StoredSaveService storedSaves, PokemonInfo info,
    GtsEvents events, ILogger<GtsService> logger)
{
    /// <summary>Offres en ligne en même temps, par compte.</summary>
    public const int MaxOffers = 5;

    /// <summary>Durée d'une offre : ensuite, son Pokémon revient dans la boîte de réception.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    /// <summary>Offres montrées au plus par une recherche (les plus récentes).</summary>
    public const int MaxResults = 100;

    public const string InternalStorage = "Stockage interne";

    private const string Gone = "Cette offre n'est plus disponible (acceptée par quelqu'un d'autre, annulée ou expirée).";
    private const string DownloadAdvice = "Télécharge cette sauvegarde (« Mes sauvegardes », ou « Récupérer » depuis ta 3DS) et remets-la dans ton jeu.";

    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    // ---------- Espèces ----------

    /// <summary>Toutes les espèces, par nom (recherche d'une offre).</summary>
    public static readonly IReadOnlyList<GtsSpecies> AllSpecies = SpeciesUpTo(1025, _ => true);

    private static List<GtsSpecies> SpeciesUpTo(int max, Func<ushort, bool> present) =>
        [.. Enumerable.Range(1, max).Select(i => (ushort)i).Where(present)
            .Select(i => new GtsSpecies(i, SpeciesName.GetSpeciesName(i, (int)LanguageID.French)))
            .OrderBy(s => BankPokemon.SortKey(s.Name), StringComparer.Ordinal)];

    /// <summary>L'espèce de ce nom (casse et accents ignorés), parmi <paramref name="species"/> ; null si aucune.</summary>
    public static ushort? FindSpecies(string? name, IEnumerable<GtsSpecies>? species = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;
        var key = BankPokemon.SortKey(name.Trim());
        return (species ?? AllSpecies).FirstOrDefault(s => BankPokemon.SortKey(s.Name) == key)?.Id;
    }

    private static string NameOf(ushort species) => SpeciesName.GetSpeciesName(species, (int)LanguageID.French);

    /// <summary>Données de l'espèce (sexe), dans le dernier jeu qui la connaît.</summary>
    private static PersonalInfo PersonalOf(ushort species) => species switch
    {
        <= 807 => PersonalTable.USUM[species],
        <= 898 => PersonalTable.SWSH[species],
        <= 905 => PersonalTable.LA[species],
        _ => PersonalTable.SV[species],
    };

    /// <summary>« Pikachu ♀ N.10 à 20, chromatique, légal » : la demande d'une offre.</summary>
    public static string Describe(GtsWanted wanted)
    {
        var text = wanted.Species is { } s ? NameOf(s) : "N'importe quel Pokémon";
        if (wanted.Gender is { } g)
            text += g == 0 ? " ♂" : " ♀";
        text += (wanted.MinLevel, wanted.MaxLevel) switch
        {
            (null, null) => "",
            ({ } low, null) => $", N.{low} ou plus",
            (null, { } high) => $", N.{high} ou moins",
            ({ } low, { } high) when low == high => $", N.{low}",
            ({ } low, { } high) => $", N.{low} à {high}",
        };
        if (wanted.Shiny is { } shiny)
            text += shiny ? ", chromatique" : ", non chromatique";
        if (wanted.LegalOnly)
            text += ", légal";
        return text;
    }

    /// <summary>Jeux d'une famille, pour « Pour : Noire, Blanche, Noire 2, Blanche 2 ».</summary>
    public static string Games(TradeFamily family)
    {
        var games = family.Context switch
        {
            EntityContext.Gen1 => "Rouge, Bleu, Jaune (et Or, Argent, Cristal)",
            EntityContext.Gen2 => family.Region == GameBoyRegion.Korean ? "Or, Argent" : "Or, Argent, Cristal (et Rouge, Bleu, Jaune)",
            EntityContext.Gen3 => "Rubis, Saphir, Émeraude, Rouge Feu, Vert Feuille",
            EntityContext.Gen4 => "Diamant, Perle, Platine, HeartGold, SoulSilver",
            EntityContext.Gen5 => "Noire, Blanche, Noire 2, Blanche 2",
            EntityContext.Gen6 => "X, Y, Rubis Oméga, Saphir Alpha",
            EntityContext.Gen7 => "Soleil, Lune, Ultra-Soleil, Ultra-Lune",
            EntityContext.Gen7b => "Let's Go, Pikachu et Évoli",
            EntityContext.Gen8 => "Épée, Bouclier",
            EntityContext.Gen8a => "Légendes Pokémon : Arceus",
            EntityContext.Gen8b => "Diamant Étincelant, Perle Scintillante",
            EntityContext.Gen9 => "Écarlate, Violet",
            var other => other.ToString(),
        };
        return family.Region switch
        {
            GameBoyRegion.Japanese => $"{games}, japonais",
            GameBoyRegion.Korean => $"{games}, coréens",
            _ => games,
        };
    }

    private static TradeFamily FamilyOf(GtsPokemon g) => new((EntityContext)g.FamilyContext, (GameBoyRegion)g.FamilyRegion);

    private static GtsWanted WantedOf(GtsPokemon g) =>
        new(g.WantedSpecies, g.WantedMinLevel, g.WantedMaxLevel, g.WantedGender, g.WantedShiny, g.WantedLegalOnly);

    private static string GenderText(byte gender) => gender switch { 0 => "♂", 1 => "♀", _ => "" };

    private static GtsPokemonView View(GtsPokemon g) => new(g.Species, g.Nickname, g.SpeciesName, g.Level, GenderText(g.Gender), g.IsShiny,
        g.IsLegal, PokemonSprites.Url(g.Species, g.Form, (EntityContext)g.Context, g.Gender == 1, g.HeldItem, g.IsShiny, false));

    private GtsPokemonView View(PKM pk, bool? legal = null)
    {
        var view = info.Describe(pk, legal);
        return new(pk.Species, view.Nickname, view.SpeciesName, pk.CurrentLevel, GenderText(pk.Gender), pk.IsShiny, view.IsLegal,
            PokemonSprites.Url(pk));
    }

    private static string Date(DateTime utc) => utc.ToLocalTime().ToString("dd/MM/yyyy", French);

    // ---------- Expiration et boîte de réception ----------

    /// <summary>
    /// Les offres arrivées à leur terme rendent leur Pokémon (boîte de réception) ; appelé toutes les heures
    /// (<see cref="GtsExpiry"/>) et à l'ouverture de la page.
    /// </summary>
    public async Task ExpireAsync()
    {
        var now = DateTime.UtcNow;
        await using var db = await dbFactory.CreateDbContextAsync();
        var owners = await db.GtsPokemon.Where(g => g.Status == GtsStatus.Offered && g.ExpiresAt <= now).Select(g => g.OwnerId).Distinct().ToListAsync();
        if (owners.Count == 0)
            return;
        var prefix = $"Personne n'a accepté ton offre en {Lifetime.Days} jours : ";
        var expired = await db.GtsPokemon.Where(g => g.Status == GtsStatus.Offered && g.ExpiresAt <= now)
            .ExecuteUpdateAsync(u => u
                .SetProperty(g => g.Status, GtsStatus.Expired)
                .SetProperty(g => g.ArrivedAt, now)
                .SetProperty(g => g.StoredSaveId, (int?)null)
                .SetProperty(g => g.Note, g => prefix + g.Nickname + " te revient."));
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("GTS : {Count} offres expirées.", expired);
        foreach (var owner in owners)
            events.Notify(owner);
    }

    /// <summary>
    /// La sauvegarde est supprimée : les offres dont le Pokémon en venait sont retirées, et leur Pokémon va dans la boîte
    /// de réception. Dans la transaction de la suppression (<see cref="StoredSaveService"/>).
    /// </summary>
    internal static Task EndOffersOfSaveAsync(ApplicationDbContext db, StoredSave save, DateTime now)
    {
        var note = $"La sauvegarde « {save.GameName} — {save.TrainerName} » a été supprimée : ton offre est retirée.";
        return db.GtsPokemon.Where(g => g.StoredSaveId == save.Id && g.Status == GtsStatus.Offered)
            .ExecuteUpdateAsync(u => u
                .SetProperty(g => g.Status, GtsStatus.SaveDeleted)
                .SetProperty(g => g.ArrivedAt, now)
                .SetProperty(g => g.StoredSaveId, (int?)null)
                .SetProperty(g => g.Note, note));
    }

    /// <summary>
    /// Nombre d'offres en ligne par sauvegarde d'où vient leur Pokémon : la confirmation de la suppression d'une sauvegarde
    /// annonce celles qu'elle retirerait.
    /// </summary>
    public async Task<Dictionary<int, int>> OffersBySaveAsync(string userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.GtsPokemon.Where(g => g.OwnerId == userId && g.StoredSaveId != null && g.Status == GtsStatus.Offered)
            .GroupBy(g => g.StoredSaveId!.Value).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count);
    }

    /// <summary>Pokémon qui attendent dans la boîte de réception (pastille du menu).</summary>
    public async Task<int> InboxCountAsync(string userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.GtsPokemon.CountAsync(g => g.OwnerId == userId && g.Status != GtsStatus.Offered);
    }

    /// <summary>
    /// La boîte de réception, les plus récents d'abord, chacun avec les sauvegardes où il peut aller (conversion possible,
    /// une case libre dans les boîtes).
    /// </summary>
    public async Task<List<GtsInboxItem>> InboxAsync(string userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var rows = await db.GtsPokemon.AsNoTracking().Where(g => g.OwnerId == userId && g.Status != GtsStatus.Offered)
            .OrderByDescending(g => g.ArrivedAt).ThenByDescending(g => g.Id).ToListAsync();
        if (rows.Count == 0)
            return [];
        var saves = await ReadSavesAsync(db, userId);
        return [.. rows.Select(g =>
        {
            var pk = ToPkm(g);
            var destinations = pk is null ? [] : saves.Where(s => FreeBoxSlot(s.Sav) is not null && SaveSession.PrepareFor(s.Sav, pk.Clone(), out _) is not null)
                .Select(s => new GtsDestination(s.Save.Id, Label(s.Save))).ToList();
            return new GtsInboxItem(g.Id, View(g), g.Status, g.Note ?? "", g.ArrivedAt ?? g.CreatedAt, destinations);
        })];
    }

    /// <summary>
    /// Récupère un Pokémon de la boîte de réception : dans le stockage interne (<paramref name="saveId"/> null), ou dans la
    /// première case libre des boîtes de cette sauvegarde, au format de son jeu (alors modifiée sur le serveur, à télécharger).
    /// </summary>
    public async Task<GtsResult> RetrieveAsync(string userId, int gtsId, int? saveId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var row = await db.GtsPokemon.FirstOrDefaultAsync(g => g.Id == gtsId && g.OwnerId == userId && g.Status != GtsStatus.Offered);
        if (row is null || ToPkm(row) is not { } pk)
            return new(false, "Ce Pokémon n'est plus dans ta boîte de réception.");
        var now = DateTime.UtcNow;
        var name = row.Nickname;
        var show = ShowOf(row);

        if (saveId is null)
        {
            db.GtsPokemon.Remove(row);
            db.BankPokemon.Add(BankService.CreateEntry(info, pk, userId, now));
            if (!await TrySaveAsync(db))
                return new(false, "Ce Pokémon n'est plus dans ta boîte de réception.");
            events.Notify(userId);
            return new(true, $"{name} est dans ton stockage interne.", show);
        }

        var save = await db.StoredSaves.FirstOrDefaultAsync(s => s.Id == saveId && s.OwnerId == userId);
        if (save is null || !StoredSaveService.TryRead(save.Data, save.FileName, out var sav))
            return new(false, "Cette sauvegarde n'existe plus.");
        if (SaveSession.PrepareFor(sav, pk.Clone(), out var error) is not { } converted)
            return new(false, $"{name} ne peut pas aller dans « {Label(save)} » : {error}");
        if (FreeBoxSlot(sav) is not { } slot)
            return new(false, $"Les boîtes de « {Label(save)} » sont pleines.");
        TradeService.Set(sav, slot, converted);
        var data = sav.Write().ToArray();

        await storedSaves.WriteBackupAsync(userId, save, "gts-avant");
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            await storedSaves.ApplyServerVersionAsync(db, save, sav, data, m => m.GtsChanged = true, now);
            db.GtsPokemon.Remove(row);
            if (!await TrySaveAsync(db))
                return new(false, "Cette sauvegarde ou ce Pokémon vient de changer ailleurs (autre onglet ?) : recommence.");
            await tx.CommitAsync();
        }
        await storedSaves.WriteBackupAsync(userId, save, "gts-apres");
        events.Notify(userId);
        return new(true, $"{name} est arrivé dans « {Label(save)} » (boîte {slot.Box + 1}, case {slot.Slot + 1}). {DownloadAdvice}", show);
    }

    /// <summary>
    /// Animation de l'échange d'un Pokémon de la boîte de réception reçu contre une offre ; null pour un Pokémon rendu
    /// (offre expirée, annulée...), ou reçu avant que l'animation existe.
    /// </summary>
    private static TradeShow? ShowOf(GtsPokemon row)
    {
        if (row.Status != GtsStatus.Received || row.TradeShow is not { } json)
            return null;
        try
        {
            return JsonSerializer.Deserialize<TradeShow>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // ---------- Dépôt ----------

    /// <summary>
    /// Pourquoi ce Pokémon de la collection ne peut pas aller sur la GTS, d'après sa case (la page grise ces Pokémon ; le
    /// dépôt revérifie) : un œuf, ou un Pokémon hors de l'équipe et des boîtes (pension...). Null s'il le peut.
    /// </summary>
    public static string? PickProblem(BankListItem item)
    {
        if (item.IsMinecraft)
            return MinecraftRefusal;
        if (item.IsEgg)
            return "Un œuf ne peut pas aller sur la GTS.";
        if (item.StoredSaveId is not null && !InPartyOrBox(item.Location))
            return $"Ce Pokémon n'est ni dans l'équipe ni dans les boîtes ({item.Location}) : il ne peut pas aller sur la GTS.";
        return null;
    }

    /// <summary>Refus d'un Pokémon de Minecraft : il ne va que dans un monde All the Mons.</summary>
    public const string MinecraftRefusal = "Un Pokémon de Minecraft ne peut pas aller sur la GTS : il ne retourne que dans un monde All the Mons.";

    /// <summary>Place de l'équipe (« Équipe 2 ») ou d'une boîte (« Boîte 3, case 12 », nom de boîte du joueur compris).</summary>
    private static bool InPartyOrBox(string? location) =>
        location is not null && (location.StartsWith("Équipe ", StringComparison.Ordinal) || System.Text.RegularExpressions.Regex.IsMatch(location, @", case \d+$"));

    /// <summary>Offres en ligne du joueur, les plus récentes d'abord.</summary>
    public async Task<List<GtsOfferView>> MyOffersAsync(string userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var rows = await db.GtsPokemon.AsNoTracking().Where(g => g.OwnerId == userId && g.Status == GtsStatus.Offered)
            .OrderByDescending(g => g.CreatedAt).ThenByDescending(g => g.Id).ToListAsync();
        return [.. rows.Select(g => OfferView(g, null, owned: false))];
    }

    private static GtsOfferView OfferView(GtsPokemon g, string? pseudo, bool owned)
    {
        var wanted = WantedOf(g);
        return new(g.Id, View(g), Describe(wanted), PokemonSprites.Url(g.WantedSpecies, 0, EntityContext.Gen9, g.WantedGender == 1, 0, g.WantedShiny == true, false),
            pseudo, Games(FamilyOf(g)), g.CreatedAt, g.ExpiresAt, owned, g.Origin);
    }

    /// <summary>Le Pokémon choisi pour un dépôt (étape 2) : null s'il n'est plus dans la collection.</summary>
    public async Task<GtsDepositChoice?> DepositChoiceAsync(string userId, int bankId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var source = await ReadSourceAsync(db, userId, bankId);
        if (source is null)
            return null;
        var present = source.Sav is { } sav ? (Func<ushort, bool>)(s => sav.Personal.IsPresentInGame(s, 0)) : _ => true;
        var species = SpeciesUpTo(source.Sav?.MaxSpeciesID ?? source.Pokemon.MaxSpeciesID, present);
        return new(View(source.Pokemon, source.Entry.IsLegal), source.Origin, source.Save is not null, source.Problem, Games(source.Family), species);
    }

    /// <summary>
    /// Un Pokémon de la collection, lu avec sa sauvegarde s'il en vient (suivie par le contexte), sa place, la famille des
    /// jeux qui pourront l'échanger, et pourquoi il ne peut pas partir (null s'il le peut).
    /// </summary>
    private sealed record Source(BankPokemon Entry, PKM Pokemon, StoredSave? Save, SaveFile? Sav, TradeSlot? Slot, TradeFamily Family,
        string Origin, string? Problem);

    private async Task<Source?> ReadSourceAsync(ApplicationDbContext db, string userId, int bankId)
    {
        var entry = await db.BankPokemon.AsNoTracking().FirstOrDefaultAsync(p => p.Id == bankId && p.OwnerId == userId);
        if (entry is null || BankService.ToPkm(entry) is not { } pk)
            return null;
        if (entry.StoredSaveId is not { } saveId)
            return new(entry, pk, null, null, null, TradeFamily.Of(pk), InternalStorage, pk.IsEgg ? "Un œuf ne peut pas aller sur la GTS." : null);

        var save = await db.StoredSaves.FirstOrDefaultAsync(s => s.Id == saveId && s.OwnerId == userId);
        if (save is null || !StoredSaveService.TryRead(save.Data, save.FileName, out var sav))
            return null;
        var slot = SlotAt(sav, entry.Position);
        var inSave = slot is { } sl ? TradeService.Get(sav, sl) : null;
        string? problem = null;
        if (inSave is not { Species: > 0 } || PokemonFingerprint.Of(inSave) != entry.Fingerprint)
            problem = "Ce Pokémon a changé de place dans sa sauvegarde : recharge la page.";
        else if (inSave.IsEgg)
            problem = "Un œuf ne peut pas aller sur la GTS.";
        else if (slot is { InParty: false } box && sav.IsBoxSlotOverwriteProtected(box.Box, box.Slot))
            problem = "Cette case est verrouillée par le jeu : ce Pokémon ne peut pas en sortir.";
        else if (slot is { InParty: true } party && !KeepsAPokemon(sav, party.Slot))
            problem = "C'est le dernier Pokémon (hors œufs) de l'équipe : il ne peut pas la quitter.";
        if (slot is null && problem is null)
            problem = $"Ce Pokémon n'est ni dans l'équipe ni dans les boîtes ({entry.Location}) : il ne peut pas aller sur la GTS.";
        return new(entry, inSave ?? pk, save, sav, slot, TradeFamily.Of(sav), Label(save), problem);
    }

    /// <summary>L'équipe garde un Pokémon qui n'est pas un œuf sans celui de la case <paramref name="index"/>.</summary>
    private static bool KeepsAPokemon(SaveFile sav, int index)
    {
        for (int i = 0; i < sav.PartyCount; i++)
        {
            if (i != index && sav.GetPartySlotAtIndex(i) is { Species: > 0, IsEgg: false })
                return true;
        }
        return false;
    }

    /// <summary>
    /// Dépose ce Pokémon sur la GTS contre <paramref name="wanted"/> pendant <see cref="Lifetime"/>. Il quitte aussitôt sa
    /// place : le stockage interne, ou sa sauvegarde (alors modifiée sur le serveur, à télécharger). <see cref="MaxOffers"/>
    /// offres au plus.
    /// </summary>
    public async Task<GtsResult> DepositAsync(string userId, int bankId, GtsWanted wanted)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var source = await ReadSourceAsync(db, userId, bankId);
        if (source is null)
            return new(false, "Ce Pokémon n'est plus dans ta collection : recharge la page.");
        if (source.Problem is { } problem)
            return new(false, problem);
        if (CheckWanted(wanted, source) is { } refused)
            return new(false, refused);

        var now = DateTime.UtcNow;
        var pk = source.Pokemon.Clone();
        var view = info.Describe(pk, source.Entry.IsLegal);
        var row = new GtsPokemon
        {
            OwnerId = userId,
            Status = GtsStatus.Offered,
            FamilyContext = (byte)source.Family.Context,
            FamilyRegion = (byte)source.Family.Region,
            Origin = source.Origin,
            StoredSaveId = source.Save?.Id,
            WantedSpecies = wanted.Species!.Value,
            WantedMinLevel = wanted.MinLevel,
            WantedMaxLevel = wanted.MaxLevel,
            WantedGender = wanted.Gender,
            WantedShiny = wanted.Shiny,
            WantedLegalOnly = wanted.LegalOnly,
            CreatedAt = now,
            ExpiresAt = now + Lifetime,
        };
        Fill(row, pk, view.Nickname, view.SpeciesName, view.IsLegal);

        byte[]? data = null;
        if (source.Save is { } save)
        {
            Remove(source.Sav!, source.Slot!.Value);
            data = source.Sav!.Write().ToArray();
            await storedSaves.WriteBackupAsync(userId, save, "gts-avant");
        }
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            if (await db.GtsPokemon.CountAsync(g => g.OwnerId == userId && g.Status == GtsStatus.Offered) >= MaxOffers)
                return new(false, $"Tu as déjà {MaxOffers} offres sur la GTS : annule-en une, ou attends qu'elle se termine.");
            if (source.Save is { } stored)
                await storedSaves.ApplyServerVersionAsync(db, stored, source.Sav!, data!, m => m.GtsChanged = true, now);
            else if (await db.BankPokemon.Where(p => p.Id == bankId && p.OwnerId == userId && p.StoredSaveId == null).ExecuteDeleteAsync() == 0)
                return new(false, "Ce Pokémon n'est plus dans ton stockage interne : recharge la page.");
            db.GtsPokemon.Add(row);
            if (!await TrySaveAsync(db))
                return new(false, "Cette sauvegarde vient d'être modifiée ailleurs (autre onglet ?) : recharge la page et recommence.");
            await tx.CommitAsync();
        }
        if (source.Save is { } after)
            await storedSaves.WriteBackupAsync(userId, after, "gts-apres");
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("GTS : le compte {User} dépose l'espèce {Species} contre l'espèce {Wanted}.", userId, pk.Species, row.WantedSpecies);

        var message = $"{view.Nickname} est sur la GTS jusqu'au {Date(row.ExpiresAt)}, contre {Describe(wanted)}.";
        return new(true, source.Save is null ? message : $"{message} Il a quitté « {source.Origin} ». {DownloadAdvice}");
    }

    /// <summary>La demande est possible pour ce jeu : une espèce qu'il connaît, un sexe que l'espèce peut avoir. Null si oui.</summary>
    private static string? CheckWanted(GtsWanted wanted, Source source)
    {
        if (wanted.Species is not { } species || species == 0)
            return "Choisis le Pokémon que tu demandes en échange.";
        var max = source.Sav?.MaxSpeciesID ?? source.Pokemon.MaxSpeciesID;
        if (species > max || (source.Sav is { } sav && !sav.Personal.IsPresentInGame(species, 0)))
            return $"{NameOf(species)} n'existe pas dans le jeu de ce Pokémon : choisis-en un autre.";
        var personal = PersonalOf(species);
        return wanted.Gender switch
        {
            not null when personal.Genderless => $"{NameOf(species)} n'a pas de sexe : choisis « Peu importe ».",
            1 when personal.OnlyMale => $"{NameOf(species)} est toujours mâle.",
            0 when personal.OnlyFemale => $"{NameOf(species)} est toujours femelle.",
            _ => null,
        };
    }

    /// <summary>Annule une offre : son Pokémon revient dans la boîte de réception.</summary>
    public async Task<GtsResult> CancelAsync(string userId, int offerId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var now = DateTime.UtcNow;
        var done = await db.GtsPokemon.Where(g => g.Id == offerId && g.OwnerId == userId && g.Status == GtsStatus.Offered)
            .ExecuteUpdateAsync(u => u
                .SetProperty(g => g.Status, GtsStatus.Cancelled)
                .SetProperty(g => g.ArrivedAt, now)
                .SetProperty(g => g.StoredSaveId, (int?)null)
                .SetProperty(g => g.Note, g => "Tu as annulé ton offre : " + g.Nickname + " te revient."));
        if (done == 0)
            return new(false, "Cette offre n'est plus en ligne (acceptée ou expirée entre-temps ?).");
        events.Notify(userId);
        return new(true, "Offre annulée : le Pokémon t'attend dans ta boîte de réception.");
    }

    // ---------- Recherche ----------

    /// <summary>
    /// Offres des autres joueurs dont le Pokémon répond à <paramref name="search"/>, les plus récentes d'abord
    /// (<see cref="MaxResults"/> au plus) ; avec <paramref name="onlyOwned"/>, seulement celles pour lesquelles le joueur a
    /// un Pokémon qui convient, dans un jeu qui peut échanger avec l'offre.
    /// </summary>
    public async Task<List<GtsOfferView>> SearchAsync(string userId, GtsWanted search, bool onlyOwned)
    {
        var now = DateTime.UtcNow;
        await using var db = await dbFactory.CreateDbContextAsync();
        var query = db.GtsPokemon.AsNoTracking().Where(g => g.Status == GtsStatus.Offered && g.ExpiresAt > now && g.OwnerId != userId);
        if (search.Species is { } species)
            query = query.Where(g => g.Species == species);
        if (search.MinLevel is { } min)
            query = query.Where(g => g.Level >= min);
        if (search.MaxLevel is { } max)
            query = query.Where(g => g.Level <= max);
        if (search.Gender is { } gender)
            query = query.Where(g => g.Gender == gender);
        if (search.Shiny is { } shiny)
            query = query.Where(g => g.IsShiny == shiny);
        if (search.LegalOnly)
            query = query.Where(g => g.IsLegal);
        var rows = await query.OrderByDescending(g => g.CreatedAt).ThenByDescending(g => g.Id)
            .Select(g => new { Offer = g, Pseudo = g.Owner!.Audience == ProfileAudience.Nobody ? null : g.Owner.UserName })
            .ToListAsync();

        var owned = await OwnedAsync(db, userId, rows.Select(r => r.Offer).ToList());
        return [.. rows.Where(r => !onlyOwned || owned.Contains(r.Offer.Id)).Take(MaxResults)
            .Select(r => OfferView(r.Offer, r.Pseudo, owned.Contains(r.Offer.Id)))];
    }

    /// <summary>Une offre en ligne d'un autre joueur, telle que la recherche la montre ; null si elle n'y est plus.</summary>
    public async Task<GtsOfferView?> OfferAsync(string userId, int offerId)
    {
        var now = DateTime.UtcNow;
        await using var db = await dbFactory.CreateDbContextAsync();
        var row = await db.GtsPokemon.AsNoTracking()
            .Where(g => g.Id == offerId && g.Status == GtsStatus.Offered && g.ExpiresAt > now && g.OwnerId != userId)
            .Select(g => new { Offer = g, Pseudo = g.Owner!.Audience == ProfileAudience.Nobody ? null : g.Owner.UserName })
            .FirstOrDefaultAsync();
        if (row is null)
            return null;
        var owned = await OwnedAsync(db, userId, [row.Offer]);
        return OfferView(row.Offer, row.Pseudo, owned.Contains(row.Offer.Id));
    }

    /// <summary>Une ligne de la collection qui pourrait répondre à une demande (sans ses octets, sauf en Game Boy).</summary>
    private sealed record OwnedRow(int Id, ushort Species, byte Level, byte? Gender, bool IsShiny, bool IsLegal, bool IsEgg, int? StoredSaveId,
        string? Location, byte Context, byte[]? Data);

    /// <summary>Offres (parmi <paramref name="offers"/>) pour lesquelles le joueur a un Pokémon qui convient, d'un jeu compatible.</summary>
    private async Task<HashSet<int>> OwnedAsync(ApplicationDbContext db, string userId, List<GtsPokemon> offers)
    {
        if (offers.Count == 0)
            return [];
        var species = offers.Select(o => o.WantedSpecies).Distinct().ToList();
        var rows = await OwnedRowsAsync(db, userId, species);
        if (rows.Count == 0)
            return [];
        var families = await SaveFamiliesAsync(db, userId);
        var result = new HashSet<int>();
        foreach (var offer in offers)
        {
            var wanted = WantedOf(offer);
            var family = FamilyOf(offer);
            if (rows.Any(r => Fits(r, wanted) && FamilyOf(r, families) is { } f && f.CanTradeWith(family)))
                result.Add(offer.Id);
        }
        return result;
    }

    private static Task<List<OwnedRow>> OwnedRowsAsync(ApplicationDbContext db, string userId, List<ushort> species) =>
        db.BankPokemon.AsNoTracking().Where(p => p.OwnerId == userId && !p.IsEgg && species.Contains(p.Species) && p.Extension != CobblemonPokemon.Extension)
            .Select(p => new OwnedRow(p.Id, p.Species, p.Level, p.Gender, p.IsShiny, p.IsLegal, p.IsEgg, p.StoredSaveId, p.Location, p.Context,
                p.Context == (byte)EntityContext.Gen1 || p.Context == (byte)EntityContext.Gen2 ? p.Data : null))
            .ToListAsync();

    private static bool Fits(OwnedRow r, GtsWanted wanted) =>
        wanted.Matches(r.Species, r.Level, r.Gender ?? 2, r.IsShiny, r.IsLegal, r.IsEgg)
        && (r.StoredSaveId is null || InPartyOrBox(r.Location));

    /// <summary>Famille d'un Pokémon de la collection : celle de sa sauvegarde, ou de son format au stockage interne.</summary>
    private static TradeFamily? FamilyOf(OwnedRow r, Dictionary<int, TradeFamily> saves)
    {
        if (r.StoredSaveId is { } id)
            return saves.TryGetValue(id, out var f) ? f : null;
        if (r.Data is { } data && EntityFormat.GetFromBytes(data.ToArray(), (EntityContext)r.Context) is { } pk)
            return TradeFamily.Of(pk);
        return new TradeFamily((EntityContext)r.Context, GameBoyRegion.International);
    }

    private static async Task<Dictionary<int, TradeFamily>> SaveFamiliesAsync(ApplicationDbContext db, string userId) =>
        (await ReadSavesAsync(db, userId)).ToDictionary(s => s.Save.Id, s => TradeFamily.Of(s.Sav));

    private sealed record ReadSave(StoredSave Save, SaveFile Sav);

    /// <summary>Les sauvegardes stockées du joueur, lues (ordre de « Mes sauvegardes »).</summary>
    private static async Task<List<ReadSave>> ReadSavesAsync(ApplicationDbContext db, string userId)
    {
        var saves = await db.StoredSaves.AsNoTracking().Where(s => s.OwnerId == userId).OrderBy(s => s.UploadedAt).ToListAsync();
        var read = new List<ReadSave>();
        foreach (var save in saves)
        {
            if (StoredSaveService.TryRead(save.Data, save.FileName, out var sav))
                read.Add(new(save, sav));
        }
        return read;
    }

    // ---------- Échange ----------

    /// <summary>
    /// Pokémon du joueur qui répondent à la demande de l'offre (équipe et boîtes de ses sauvegardes, stockage interne),
    /// ceux d'un jeu qui ne peut pas échanger avec l'offre ou qui ne peut pas recevoir son Pokémon restant dans la liste,
    /// grisés avec la raison. Vide si l'offre n'est plus en ligne.
    /// </summary>
    public async Task<List<GtsCandidate>> CandidatesAsync(string userId, int offerId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var offer = await OnlineOfferAsync(db, userId, offerId);
        if (offer is null || ToPkm(offer) is not { } offered)
            return [];
        var wanted = WantedOf(offer);
        var rows = (await OwnedRowsAsync(db, userId, [offer.WantedSpecies])).Where(r => Fits(r, wanted)).Select(r => r.Id).ToList();
        var entries = await db.BankPokemon.AsNoTracking().Where(p => rows.Contains(p.Id))
            .OrderBy(p => p.StoredSaveId == null).ThenBy(p => p.StoredSaveId).ThenBy(p => p.Position).ThenBy(p => p.Id).ToListAsync();
        var saves = (await ReadSavesAsync(db, userId)).ToDictionary(s => s.Save.Id);
        var family = FamilyOf(offer);
        var candidates = new List<GtsCandidate>();
        foreach (var entry in entries)
        {
            if (BankService.ToPkm(entry) is not { } pk)
                continue;
            var stored = entry.StoredSaveId is { } id && saves.TryGetValue(id, out var s) ? s : null;
            if (entry.StoredSaveId is not null && stored is null)
                continue;
            var origin = stored is null ? InternalStorage : $"{Label(stored.Save)} · {entry.Location}";
            candidates.Add(new(entry.Id, View(pk, entry.IsLegal), origin, GiveProblem(entry, pk, stored, family, offered)));
        }
        return candidates;
    }

    /// <summary>Pourquoi ce Pokémon ne peut pas être donné pour l'offre (jeu, place, réception) ; null s'il le peut.</summary>
    private static string? GiveProblem(BankPokemon entry, PKM pk, ReadSave? stored, TradeFamily family, PKM offered)
    {
        if (stored is null)
            return TradeFamily.Of(pk).CanTradeWith(family) ? null : $"Ce Pokémon ne peut pas échanger avec les jeux de l'offre ({Games(family)}).";
        var sav = stored.Sav;
        if (!TradeFamily.Of(sav).CanTradeWith(family))
            return $"« {Label(stored.Save)} » ne peut pas échanger avec les jeux de l'offre ({Games(family)}).";
        if (SlotAt(sav, entry.Position) is not { } slot || TradeService.Get(sav, slot) is not { Species: > 0 } inSave
            || PokemonFingerprint.Of(inSave) != entry.Fingerprint)
            return "Ce Pokémon a changé de place dans sa sauvegarde : recharge la page.";
        if (!slot.InParty && sav.IsBoxSlotOverwriteProtected(slot.Box, slot.Slot))
            return "Cette case est verrouillée par le jeu : ce Pokémon ne peut pas en sortir.";
        if (TradeService.ConvertFor(offered, sav) is null)
            return $"« {Label(stored.Save)} » ne peut pas recevoir le Pokémon de l'offre (espèce, attaque ou objet inconnus de ce jeu).";
        return null;
    }

    private static Task<GtsPokemon?> OnlineOfferAsync(ApplicationDbContext db, string userId, int offerId)
    {
        var now = DateTime.UtcNow;
        return db.GtsPokemon.FirstOrDefaultAsync(g => g.Id == offerId && g.Status == GtsStatus.Offered && g.ExpiresAt > now && g.OwnerId != userId);
    }

    /// <summary>
    /// Un échange prêt à écrire : l'offre (suivie), le Pokémon donné et son entrée, sa sauvegarde s'il en vient, ce que
    /// reçoit chacun (converti, évolué) et les noms avant l'évolution.
    /// Pour l'animation d'échange : <c>OfferedShow</c> et <c>GivenShow</c>, les deux Pokémon tels qu'ils partent ;
    /// <c>ReceivedShow</c> et <c>GivenArriving</c>, tels qu'ils arrivent, avec leur évolution.
    /// </summary>
    private sealed record Prepared(GtsPokemon Offer, BankPokemon Entry, StoredSave? Save, SaveFile? Sav, TradeSlot? Slot,
        PKM Received, string ReceivedName, ushort? ReceivedEvolvedTo, PKM Given, string GivenName, ushort? GivenEvolvedTo, string Arrival,
        TradeShowPokemon OfferedShow, TradeShowPokemon ReceivedShow, TradeShowPokemon GivenShow, TradeShowPokemon GivenArriving);

    private async Task<(Prepared? Ready, string? Problem)> PrepareAsync(ApplicationDbContext db, string userId, int offerId, int bankId)
    {
        var offer = await OnlineOfferAsync(db, userId, offerId);
        if (offer is null || ToPkm(offer) is not { } offered)
            return (null, Gone);
        var entry = await db.BankPokemon.AsNoTracking().FirstOrDefaultAsync(p => p.Id == bankId && p.OwnerId == userId);
        if (entry is null || BankService.ToPkm(entry) is not { } pk)
            return (null, "Ce Pokémon n'est plus dans ta collection : choisis-en un autre.");
        ReadSave? stored = null;
        if (entry.StoredSaveId is { } saveId)
        {
            var save = await db.StoredSaves.FirstOrDefaultAsync(s => s.Id == saveId && s.OwnerId == userId);
            if (save is null || !StoredSaveService.TryRead(save.Data, save.FileName, out var read))
                return (null, "Ce Pokémon n'est plus dans ta collection : choisis-en un autre.");
            stored = new(save, read);
            var slot = SlotAt(read, entry.Position);
            pk = slot is { } sl && TradeService.Get(read, sl) is { Species: > 0 } inSave ? inSave : pk;
        }
        // La demande, revérifiée sur le Pokémon lui-même (légalité recalculée) : la liste vient du navigateur.
        if (!WantedOf(offer).Matches(pk.Species, pk.CurrentLevel, pk.Gender, pk.IsShiny, PokemonInfo.IsLegal(pk), pk.IsEgg))
            return (null, $"Ce Pokémon ne répond pas à la demande de l'offre ({Describe(WantedOf(offer))}).");
        if (GiveProblem(entry, pk, stored, FamilyOf(offer), offered) is { } giveProblem)
            return (null, giveProblem);

        // Stockage interne : le Pokémon reçu y reste à son format.
        var received = stored is null ? offered.Clone() : TradeService.ConvertFor(offered, stored.Sav)!;
        var given = pk.Clone();
        var receivedName = FullName(received);
        var givenName = FullName(given);
        var receivedShow = TradeShowPokemon.Of(info, received);
        var givenShow = TradeShowPokemon.Of(info, given);
        var receivedEvolvedTo = TradeService.EvolveByTrade(received, given.Species);
        var givenEvolvedTo = TradeService.EvolveByTrade(given, offered.Species);
        var arrival = stored is null ? "ton stockage interne" : $"« {Label(stored.Save)} »";
        return (new(offer, entry, stored?.Save, stored?.Sav, stored is null ? null : SlotAt(stored.Sav, entry.Position),
            received, receivedName, receivedEvolvedTo, given, givenName, givenEvolvedTo, arrival,
            TradeShowPokemon.Of(info, offered), receivedShow.Evolving(info, received, receivedEvolvedTo),
            givenShow, givenShow.Evolving(info, given, givenEvolvedTo)), null);
    }

    /// <summary>Ce que donnera l'échange (évolutions annoncées), ou pourquoi il ne peut pas se faire. Rien n'est écrit.</summary>
    public async Task<GtsPreview> PreviewAsync(string userId, int offerId, int bankId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var (p, problem) = await PrepareAsync(db, userId, offerId, bankId);
        if (p is null)
            return new(problem, null, null, "");
        return new(null,
            p.ReceivedEvolvedTo is null ? null : $"{p.ReceivedName} évoluera en {info.Describe(p.Received).SpeciesName} en arrivant dans {p.Arrival}.",
            p.GivenEvolvedTo is null ? null : $"{p.GivenName} évoluera en {info.Describe(p.Given).SpeciesName} en arrivant chez l'autre joueur.",
            p.Arrival);
    }

    /// <summary>
    /// Accepte l'offre en donnant ce Pokémon de la collection : le Pokémon de l'offre prend sa place (dans sa sauvegarde,
    /// alors modifiée sur le serveur, ou au stockage interne), et le Pokémon donné va dans la boîte de réception du
    /// déposant. Évolutions par échange des deux côtés ; compté dans les statistiques d'échanges.
    /// </summary>
    public async Task<GtsResult> AcceptAsync(string userId, int offerId, int bankId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var (p, problem) = await PrepareAsync(db, userId, offerId, bankId);
        if (p is null)
            return new(false, problem!);
        var now = DateTime.UtcNow;
        var pseudo = await PublicPseudoAsync(db, userId);
        var depositorPseudo = await PublicPseudoAsync(db, p.Offer.OwnerId);
        var offeredName = p.Offer.Nickname;
        var offeredSpecies = p.Offer.Species;
        var depositor = p.Offer.OwnerId;

        byte[]? data = null;
        if (p.Save is { } save)
        {
            TradeService.Set(p.Sav!, p.Slot!.Value, p.Received);
            data = p.Sav!.Write().ToArray();
            await storedSaves.WriteBackupAsync(userId, save, "echange-avant");
        }
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            // L'offre est prise d'abord : deux joueurs qui acceptent en même temps, un seul l'obtient.
            var taken = await db.GtsPokemon.Where(g => g.Id == offerId && g.Status == GtsStatus.Offered && g.ExpiresAt > now)
                .ExecuteUpdateAsync(u => u.SetProperty(g => g.Status, GtsStatus.Received));
            if (taken == 0)
                return new(false, Gone);
            if (p.Save is { } stored)
                await storedSaves.ApplyServerVersionAsync(db, stored, p.Sav!, data!, m => m.PokemonTraded = true, now);
            else
            {
                if (await db.BankPokemon.Where(b => b.Id == bankId && b.OwnerId == userId && b.StoredSaveId == null).ExecuteDeleteAsync() == 0)
                    return new(false, "Ce Pokémon n'est plus dans ton stockage interne : choisis-en un autre.");
                db.BankPokemon.Add(BankService.CreateEntry(info, p.Received, userId, now));
            }

            var givenView = info.Describe(p.Given);
            var evolved = p.GivenEvolvedTo is null ? "" : $", qui a évolué en {givenView.SpeciesName}";
            Fill(p.Offer, p.Given, givenView.Nickname, givenView.SpeciesName, givenView.IsLegal);
            p.Offer.Status = GtsStatus.Received;
            p.Offer.ArrivedAt = now;
            p.Offer.StoredSaveId = null;
            p.Offer.Note = $"Échange avec {pseudo ?? "un dresseur"} : tu as reçu {p.GivenName}{evolved} contre ton {offeredName}.";
            // Le déposant verra l'échange en récupérant le Pokémon : son offre part, le Pokémon donné arrive (et évolue).
            p.Offer.TradeShow = JsonSerializer.Serialize(new TradeShow(pseudo ?? "un dresseur", p.OfferedShow, p.GivenArriving));
            db.TradeRecords.Add(new TradeRecord
            {
                At = now,
                FirstId = depositor,
                SecondId = userId,
                FirstGave = offeredSpecies,
                FirstGaveEvolvedTo = p.ReceivedEvolvedTo,
                SecondGave = p.Entry.Species,
                SecondGaveEvolvedTo = p.GivenEvolvedTo,
            });
            if (!await TrySaveAsync(db))
                return new(false, "Ta sauvegarde vient d'être modifiée ailleurs (autre onglet ?) : recommence.");
            await tx.CommitAsync();
        }
        if (p.Save is { } written)
            await storedSaves.WriteBackupAsync(userId, written, "echange-apres");
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("GTS : le compte {User} accepte l'offre {Offer} du compte {Owner}.", userId, offerId, depositor);
        events.Notify(depositor);

        var receivedEvolved = p.ReceivedEvolvedTo is null ? "" : $", qui a évolué en {info.Describe(p.Received).SpeciesName}";
        var message = $"Échange réussi : tu as reçu {p.ReceivedName}{receivedEvolved} dans {p.Arrival}.";
        return new(true, p.Save is null ? message : $"{message} {DownloadAdvice}",
            new TradeShow(depositorPseudo ?? "un dresseur", p.GivenShow, p.ReceivedShow));
    }

    /// <summary>Pseudo d'un joueur tel que le voient les autres : null s'il n'en a pas ou si son profil est caché (« Personne »).</summary>
    private static Task<string?> PublicPseudoAsync(ApplicationDbContext db, string userId) =>
        db.Users.Where(u => u.Id == userId).Select(u => u.Audience == ProfileAudience.Nobody ? null : u.UserName).FirstOrDefaultAsync();

    // ---------- Outils ----------

    /// <summary>« Kadabra », « Bob (Kadabra) » pour un Pokémon surnommé.</summary>
    private string FullName(PKM pk)
    {
        var view = info.Describe(pk);
        return view.Nickname == view.SpeciesName ? view.SpeciesName : $"{view.Nickname} ({view.SpeciesName})";
    }

    private static string Label(StoredSave save) => $"{save.GameName} — {save.TrainerName}";

    private static PKM? ToPkm(GtsPokemon g) => EntityFormat.GetFromBytes(g.Data.ToArray(), (EntityContext)g.Context);

    /// <summary>Le Pokémon et ce qu'il faut pour l'afficher et le chercher.</summary>
    private static void Fill(GtsPokemon row, PKM pk, string nickname, string speciesName, bool legal)
    {
        row.Data = BankService.StoredData(pk);
        row.Context = (byte)pk.Context;
        row.Extension = pk.Extension;
        row.Species = pk.Species;
        row.Form = pk.Form;
        row.SpeciesName = speciesName;
        row.Nickname = nickname;
        row.Level = pk.CurrentLevel;
        row.Gender = pk.Gender;
        row.HeldItem = pk.HeldItem;
        row.IsShiny = pk.IsShiny;
        row.IsLegal = legal;
        row.Fingerprint = PokemonFingerprint.Of(pk);
    }

    /// <summary>
    /// La case d'un Pokémon de la sauvegarde d'après sa place dans « Ma collection » (<see cref="BankPokemon.Position"/> :
    /// l'ordre de <see cref="StoredSaveService.ReadAllPokemon"/>, équipe puis boîtes puis le reste) ; null pour le reste
    /// (pension, Boîte Combat...).
    /// </summary>
    internal static TradeSlot? SlotAt(SaveFile sav, int position)
    {
        var n = 0;
        if (sav.HasParty)
        {
            for (int i = 0; i < sav.PartyCount; i++)
            {
                if (sav.GetPartySlotAtIndex(i) is { Species: > 0 } && n++ == position)
                    return TradeSlot.Party(i);
            }
        }
        if (sav.HasBox)
        {
            for (int b = 0; b < sav.BoxCount; b++)
            {
                for (int s = 0; s < sav.BoxSlotCount; s++)
                {
                    if (sav.GetBoxSlotAtIndex(b, s) is { Species: > 0 } && n++ == position)
                        return sav.IsBoxSlotLocked(b, s) ? null : new TradeSlot(b, s);
                }
            }
        }
        return null;
    }

    /// <summary>Retire le Pokémon de sa case : l'équipe se resserre, la case d'une boîte reste vide.</summary>
    private static void Remove(SaveFile sav, TradeSlot slot)
    {
        if (slot.InParty)
            sav.DeletePartySlot(slot.Slot);
        else
            sav.SetBoxSlotAtIndex(sav.BlankPKM, slot.Box, slot.Slot);
    }

    /// <summary>Première case libre des boîtes (ni verrouillée, ni protégée) ; null si tout est plein.</summary>
    private static TradeSlot? FreeBoxSlot(SaveFile sav)
    {
        if (!sav.HasBox)
            return null;
        for (int b = 0; b < sav.BoxCount; b++)
        {
            for (int s = 0; s < sav.BoxSlotCount; s++)
            {
                if (sav.GetBoxSlotAtIndex(b, s).Species == 0 && !sav.IsBoxSlotLocked(b, s) && !sav.IsBoxSlotOverwriteProtected(b, s))
                    return new TradeSlot(b, s);
            }
        }
        return null;
    }

    /// <summary>Enregistre ; faux si une ligne a changé entre-temps (jeton de concurrence, ligne supprimée).</summary>
    private static async Task<bool> TrySaveAsync(ApplicationDbContext db)
    {
        try
        {
            await db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }
}

/// <summary>Toutes les heures, les offres arrivées à leur terme rendent leur Pokémon (<see cref="GtsService.ExpireAsync"/>).</summary>
public sealed class GtsExpiry(IServiceScopeFactory scopes, ILogger<GtsExpiry> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<GtsService>().ExpireAsync();
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogError(e, "GTS : échec de l'expiration des offres.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
