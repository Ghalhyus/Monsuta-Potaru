using Microsoft.EntityFrameworkCore;
using PKHeX.Core;
using MonsutaPotaru.Data;

namespace MonsutaPotaru.Services;

/// <summary>Ce qu'il faut pour afficher un Pokémon de la banque dans une grille.</summary>
/// <param name="StoredSaveId">Renseigné pour un Pokémon d'une sauvegarde stockée (lecture seule).</param>
public sealed record BankListItem(int Id, ushort Species, string SpeciesName, string Nickname, byte Level, bool IsShiny, bool IsEgg, bool IsLegal, int? StoredSaveId,
    string SpriteUrl);

/// <summary>
/// Filtre de « Ma collection » : tout, la banque seule, ou une sauvegarde stockée ; <paramref name="Criteria"/> ajoute les
/// filtres des pastilles (type, nature, IV...) ; <paramref name="Group"/> garde un seul dossier du mode compact (une
/// espèce, ou les œufs).
/// </summary>
public readonly record struct CollectionFilter(bool BankOnly = false, int? StoredSaveId = null, PokemonCriteria? Criteria = null,
    CollectionGroupKey? Group = null)
{
    public static readonly CollectionFilter All = new();
    public static readonly CollectionFilter Bank = new(BankOnly: true);
    public static CollectionFilter Save(int id) => new(StoredSaveId: id);
}

/// <summary>Critère de tri de « Ma collection ».</summary>
public enum CollectionSortBy { Arrival, Name, Dex }

/// <summary>
/// Tri de « Ma collection » : par arrivée (les plus récents d'abord), par nom d'espèce ou par numéro de Pokédex (croissants) ;
/// <paramref name="Reversed"/> inverse ce sens. Les œufs sont toujours à la fin, par arrivée (leur espèce reste cachée) ; à
/// égalité (même espèce), les plus récents d'abord.
/// </summary>
public readonly record struct CollectionSort(CollectionSortBy By = CollectionSortBy.Arrival, bool Reversed = false);

public sealed class BankService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    PokemonInfo info,
    IConfiguration config,
    IWebHostEnvironment env,
    ILogger<BankService> logger)
{
    /// <summary>Pokémon de la banque (hors sauvegardes stockées) : ceux que « Transfert » peut retirer.</summary>
    public async Task<List<BankPokemon>> ListAsync(string ownerId, string? search = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await Query(db, ownerId, search, CollectionFilter.Bank).ToListAsync();
    }

    public async Task<int> CountAsync(string ownerId, string? search = null, CollectionFilter filter = default, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await Query(db, ownerId, search, filter).CountAsync(ct);
    }

    /// <summary>
    /// Une tranche de la collection, sans les octets du Pokémon (affichage seulement) :
    /// la page « Ma collection » charge ainsi au fur et à mesure du défilement.
    /// </summary>
    public async Task<List<BankListItem>> GetRangeAsync(string ownerId, string? search, int skip, int take, CollectionFilter filter = default,
        CollectionSort sort = default, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await Sorted(Filtered(db, ownerId, search, filter), sort)
            .Skip(skip)
            .Take(take)
            .Select(ListItem)
            .ToListAsync(ct);
    }

    /// <summary>Ces Pokémon du compte (cases des espèces seules du mode compact), dans le désordre.</summary>
    public async Task<List<BankListItem>> GetItemsAsync(string ownerId, IReadOnlyCollection<int> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0)
            return [];
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.BankPokemon.AsNoTracking().Where(p => p.OwnerId == ownerId && ids.Contains(p.Id)).Select(ListItem).ToListAsync(ct);
    }

    /// <summary>
    /// Dossiers du mode compact : les Pokémon qui correspondent à la recherche et au filtre, regroupés par espèce (toutes
    /// formes réunies ; les œufs ensemble), avec leur nombre, rangés selon le tri. Une seule requête : au plus un dossier
    /// par espèce.
    /// </summary>
    public async Task<List<CollectionGroup>> GetGroupsAsync(string ownerId, string? search, CollectionFilter filter = default,
        CollectionSort sort = default, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var rows = await Filtered(db, ownerId, search, filter)
            .GroupBy(p => new { Species = p.IsEgg ? 0 : (int)p.Species, p.IsEgg })
            .Select(g => new
            {
                g.Key.Species,
                g.Key.IsEgg,
                Count = g.Count(),
                SpeciesName = g.Max(p => p.SpeciesName),
                SortName = g.Max(p => p.SortName),
                Newest = g.Max(p => p.DepositedAt),
                Oldest = g.Min(p => p.DepositedAt),
                FirstPosition = g.Min(p => p.Position),
                LastPosition = g.Max(p => p.Position),
                FirstId = g.Min(p => p.Id),
                LastId = g.Max(p => p.Id),
                SpriteSpecies = g.Min(p => p.Species),
            })
            .ToListAsync(ct);
        return CollectionGroup.Order(rows.Select(r => new CollectionGroup(new((ushort)r.Species, r.IsEgg), r.Count, r.SpeciesName ?? "",
            r.SortName ?? "", r.Newest, r.Oldest, r.FirstPosition, r.LastPosition, r.FirstId, r.LastId, r.SpriteSpecies)), sort);
    }

    /// <summary>Case d'un Pokémon dans la grille (sans ses octets).</summary>
    private static readonly System.Linq.Expressions.Expression<Func<BankPokemon, BankListItem>> ListItem = p =>
        new BankListItem(p.Id, p.Species, p.SpeciesName, p.Nickname, p.Level, p.IsShiny, p.IsEgg, p.IsLegal, p.StoredSaveId,
            PokemonSprites.Url(p.Species, p.Form, (EntityContext)p.Context, p.Gender == 1, p.HeldItem ?? 0, p.IsShiny, p.IsEgg));

    /// <summary>Talents présents dans la collection (hors œufs), pour la liste du filtre « Talent ».</summary>
    public async Task<List<int>> ListAbilitiesAsync(string ownerId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.BankPokemon.Where(p => p.OwnerId == ownerId && !p.IsEgg && p.Ability > 0)
            .Select(p => p.Ability).Distinct().ToListAsync();
    }

    /// <summary>Occupation de la collection (banque + sauvegardes stockées) et sa limite.</summary>
    public async Task<CollectionUsage> GetUsageAsync(string ownerId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await CollectionCapacity.GetUsageAsync(db, ownerId);
    }

    public async Task<BankPokemon?> GetAsync(string ownerId, int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.BankPokemon.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id && p.OwnerId == ownerId);
    }

    /// <summary>
    /// Pokémon d'un utilisateur, filtrés par origine et par espèce / surnom, du plus récent au plus ancien
    /// (et, dans une même sauvegarde stockée, dans l'ordre équipe → boîtes → reste).
    /// </summary>
    private static IQueryable<BankPokemon> Query(ApplicationDbContext db, string ownerId, string? search, CollectionFilter filter) =>
        Filtered(db, ownerId, search, filter).OrderByDescending(p => p.DepositedAt).ThenBy(p => p.Position).ThenByDescending(p => p.Id);

    // Clés de tri (traduites en SQL) : les œufs, déjà rangés à la fin, n'ont ni nom, ni numéro, ni forme.
    private static readonly System.Linq.Expressions.Expression<Func<BankPokemon, string>> NameKey = p => p.IsEgg ? "" : p.SortName;
    private static readonly System.Linq.Expressions.Expression<Func<BankPokemon, int>> DexKey = p => p.IsEgg ? 0 : p.Species;
    private static readonly System.Linq.Expressions.Expression<Func<BankPokemon, int>> FormKey = p => p.IsEgg ? 0 : p.Form;

    /// <summary>Tri choisi dans « Ma collection » (voir <see cref="CollectionSort"/>).</summary>
    private static IQueryable<BankPokemon> Sorted(IQueryable<BankPokemon> query, CollectionSort sort)
    {
        var eggsLast = query.OrderBy(p => p.IsEgg);
        var sorted = (sort.By, sort.Reversed) switch
        {
            (CollectionSortBy.Name, false) => eggsLast.ThenBy(NameKey).ThenBy(FormKey),
            (CollectionSortBy.Name, true) => eggsLast.ThenByDescending(NameKey).ThenBy(FormKey),
            (CollectionSortBy.Dex, false) => eggsLast.ThenBy(DexKey).ThenBy(FormKey),
            (CollectionSortBy.Dex, true) => eggsLast.ThenByDescending(DexKey).ThenBy(FormKey),
            (CollectionSortBy.Arrival, true) => eggsLast.ThenBy(p => p.DepositedAt).ThenByDescending(p => p.Position).ThenBy(p => p.Id),
            _ => eggsLast,
        };
        return sorted.ThenByDescending(p => p.DepositedAt).ThenBy(p => p.Position).ThenByDescending(p => p.Id);
    }

    /// <summary>
    /// Remplit <see cref="BankPokemon.SortName"/> des Pokémon enregistrés avant son ajout (migration <c>SortName</c>) ; appelé
    /// au démarrage, ne fait rien une fois tout rempli.
    /// </summary>
    public static async Task FillSortNamesAsync(ApplicationDbContext db)
    {
        var names = await db.BankPokemon.Where(p => p.SortName == "" && p.SpeciesName != "").Select(p => p.SpeciesName).Distinct().ToListAsync();
        foreach (var name in names)
        {
            var key = BankPokemon.SortKey(name);
            await db.BankPokemon.Where(p => p.SortName == "" && p.SpeciesName == name)
                .ExecuteUpdateAsync(p => p.SetProperty(x => x.SortName, key));
        }
    }

    /// <summary>
    /// Remplit <see cref="BankPokemon.Gender"/> et <see cref="BankPokemon.HeldItem"/> des Pokémon enregistrés avant leur
    /// ajout (migration <c>SpriteDetails</c>), en relisant leurs octets ; appelé au démarrage, ne fait rien une fois tout rempli.
    /// </summary>
    public static async Task FillSpriteDetailsAsync(ApplicationDbContext db)
    {
        while (true)
        {
            var batch = await db.BankPokemon.Where(p => p.Gender == null).OrderBy(p => p.Id).Take(500).ToListAsync();
            if (batch.Count == 0)
                return;
            foreach (var entry in batch)
            {
                var pk = ToPkm(entry);
                entry.Gender = pk?.Gender ?? 2;
                entry.HeldItem = pk?.HeldItem ?? 0;
            }
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
        }
    }

    /// <summary>
    /// Remplit les caractéristiques des filtres (<see cref="PokemonTraits"/> : types, nature, talent, IV, forme, Méga) des
    /// Pokémon enregistrés avant leur ajout (migration <c>CollectionFilters</c>), en relisant leurs octets ; appelé au
    /// démarrage, ne fait rien une fois tout rempli (repère : nature null).
    /// </summary>
    public static async Task FillFilterDetailsAsync(ApplicationDbContext db)
    {
        while (true)
        {
            var batch = await db.BankPokemon.Where(p => p.Nature == null).OrderBy(p => p.Id).Take(500).ToListAsync();
            if (batch.Count == 0)
                return;
            foreach (var entry in batch)
            {
                if (ToPkm(entry) is { } pk)
                    PokemonTraits.Fill(entry, pk);
                else
                    entry.Nature = 0; // octets illisibles : rien à filtrer, mais ne pas les relire à chaque démarrage
            }
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
        }
    }

    /// <summary>Pokémon d'un utilisateur, filtrés par origine et par espèce / surnom (sans tri).</summary>
    private static IQueryable<BankPokemon> Filtered(ApplicationDbContext db, string ownerId, string? search, CollectionFilter filter)
    {
        var query = db.BankPokemon.AsNoTracking().Where(p => p.OwnerId == ownerId);
        if (filter.BankOnly)
            query = query.Where(p => p.StoredSaveId == null);
        else if (filter.StoredSaveId is { } saveId)
            query = query.Where(p => p.StoredSaveId == saveId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = $"%{search.Trim()}%";
            query = query.Where(p => EF.Functions.Like(p.SpeciesName, s) || EF.Functions.Like(p.Nickname, s));
        }
        if (filter.Criteria is { } criteria)
            query = criteria.Apply(query);
        if (filter.Group is { } group)
            query = group.Eggs ? query.Where(p => p.IsEgg) : query.Where(p => !p.IsEgg && p.Species == group.Species);
        return query.OrderByDescending(p => p.DepositedAt).ThenBy(p => p.Position).ThenByDescending(p => p.Id);
    }

    /// <summary>
    /// Pokémon de cette sauvegarde déjà ailleurs dans le compte (stockage interne, sauvegardes stockées) : son chargement
    /// dans « Transfert » est refusé.
    /// </summary>
    public async Task<List<DuplicateCopy>> FindDuplicatesAsync(string ownerId, IEnumerable<PKM> pokemon)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await PokemonFingerprint.FindAsync(db, ownerId, pokemon.Select(PokemonFingerprint.Of));
    }

    /// <summary>Refus d'un dépôt : ce Pokémon est déjà dans le stockage interne (vieille copie de la sauvegarde ?).</summary>
    public static string AlreadyInStorageMessage(string name) =>
        $"{name} est déjà dans ton stockage interne (le même Pokémon, d'une vieille copie de la sauvegarde ?) : dépôt refusé.";

    public static PKM? ToPkm(BankPokemon entry) =>
        EntityFormat.GetFromBytes(entry.Data.ToArray(), (EntityContext)entry.Context);

    /// <summary>Octets du Pokémon au format des boîtes, déchiffrés (ce que garde <see cref="BankPokemon.Data"/>).</summary>
    public static byte[] StoredData(PKM pk)
    {
        var data = new byte[pk.SIZE_STORED];
        pk.WriteDecryptedDataStored(data);
        return data;
    }

    /// <summary>Ligne de base pour un Pokémon : octets bruts + métadonnées d'affichage et de recherche.</summary>
    public static BankPokemon CreateEntry(PokemonInfo info, PKM pk, string ownerId, DateTime now)
    {
        var view = info.Describe(pk);
        var entry = new BankPokemon
        {
            OwnerId = ownerId,
            Data = StoredData(pk),
            Context = (byte)pk.Context,
            Extension = pk.Extension,
            Species = pk.Species,
            Form = pk.Form,
            SpeciesName = view.SpeciesName,
            SortName = BankPokemon.SortKey(view.SpeciesName),
            Nickname = view.Nickname,
            Level = pk.CurrentLevel,
            Gender = pk.Gender,
            HeldItem = pk.HeldItem,
            IsShiny = pk.IsShiny,
            IsEgg = pk.IsEgg,
            IsLegal = view.IsLegal,
            OriginalTrainer = pk.OriginalTrainerName,
            OriginGame = view.OriginGame,
            Fingerprint = PokemonFingerprint.Of(pk),
            DepositedAt = now,
        };
        PokemonTraits.Fill(entry, pk); // types, nature, IV... pour les filtres de « Ma collection »
        return entry;
    }

    /// <summary>
    /// Enregistre en base les dépôts et retraits en attente, garde une copie de la sauvegarde
    /// avant/après, puis renvoie le fichier modifié à télécharger.
    /// </summary>
    public async Task<byte[]> CommitAsync(string ownerId, SaveSession session)
    {
        var sav = session.Save ?? throw new InvalidOperationException("Aucune sauvegarde chargée.");

        await using var db = await dbFactory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();

        // La sauvegarde a pu être stockée (depuis un autre onglet) après son chargement dans « Transfert ».
        var key = SaveIdentity.Of(sav).Key;
        if (await db.StoredSaves.AnyAsync(s => s.IdentityKey == key))
            throw new InvalidOperationException("Cette sauvegarde est maintenant stockée sur le serveur : elle ne peut plus être utilisée dans « Transfert ».");

        // Un Pokémon déjà au stockage interne ne peut pas y entrer une seconde fois (vieille copie de la sauvegarde) : la
        // page refuse déjà une telle sauvegarde au chargement, ceci couvre un dépôt fait entre-temps depuis un autre onglet.
        // Vérifié avant les retraits, sinon retirer l'original et déposer sa copie le dupliquerait.
        var deposited = session.Deposits.Select(d => PokemonFingerprint.Of(d.Pokemon)).ToList();
        var inStorage = await PokemonFingerprint.FindAsync(db, ownerId, deposited, internalOnly: true);
        if (inStorage.Count > 0)
            throw new InvalidOperationException(AlreadyInStorageMessage(inStorage[0].IsEgg ? "Un Œuf" : inStorage[0].SpeciesName)
                + " Annule son dépôt, puis valide de nouveau.");

        foreach (var w in session.Withdrawals)
        {
            // Jamais un Pokémon de sauvegarde stockée : ils sont en lecture seule.
            var deleted = await db.BankPokemon.Where(p => p.Id == w.BankId && p.OwnerId == ownerId && p.StoredSaveId == null).ExecuteDeleteAsync();
            if (deleted == 0)
                throw new InvalidOperationException("Un des Pokémon retirés n'est plus dans le stockage interne (retiré depuis un autre onglet ?). Annule et recommence.");
        }

        // Taille de la collection, vérifiée dans la transaction : les retraits viennent de libérer leur place.
        var afterWithdrawals = await CollectionCapacity.GetUsageAsync(db, ownerId);
        var before = afterWithdrawals with { Used = afterWithdrawals.Used + session.Withdrawals.Count };
        if (CollectionCapacity.Exceeds(before, afterWithdrawals.Used + session.Deposits.Count))
            throw new InvalidOperationException(CollectionCapacity.BankFullMessage(before, session.Deposits.Count - session.Withdrawals.Count));

        var now = DateTime.UtcNow;
        foreach (var d in session.Deposits)
            db.BankPokemon.Add(CreateEntry(info, d.Pokemon, ownerId, now));

        await db.SaveChangesAsync();
        var newData = sav.Write().ToArray();
        await WriteBackupsAsync(ownerId, session, newData);
        await tx.CommitAsync();

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Utilisateur {User} : {Deposits} dépôt(s), {Withdrawals} retrait(s).",
                ownerId, session.Deposits.Count, session.Withdrawals.Count);

        session.MarkCommitted(newData);
        return newData;
    }

    /// <summary>Dossier des copies de sécurité d'un utilisateur (créé au besoin).</summary>
    public static string BackupDirectory(IConfiguration config, IWebHostEnvironment env, string ownerId)
    {
        var root = Path.Combine(env.ContentRootPath, config["Bank:BackupPath"] ?? "Data/backups"); // Combine ignore ContentRootPath si le chemin configuré est absolu
        var dir = Path.Combine(root, ownerId);
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>Copie de sécurité de la sauvegarde avant et après chaque validation.</summary>
    private async Task WriteBackupsAsync(string ownerId, SaveSession session, byte[] newData)
    {
        var dir = BackupDirectory(config, env, ownerId);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var name = Path.GetFileNameWithoutExtension(session.FileName);
        var ext = Path.GetExtension(session.FileName);
        if (session.OriginalData is { } original)
            await File.WriteAllBytesAsync(Path.Combine(dir, $"{stamp}_{name}_avant{ext}"), original);
        await File.WriteAllBytesAsync(Path.Combine(dir, $"{stamp}_{name}_apres{ext}"), newData);
    }
}
