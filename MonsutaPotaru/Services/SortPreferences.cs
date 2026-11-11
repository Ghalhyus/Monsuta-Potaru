using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MonsutaPotaru.Data;

namespace MonsutaPotaru.Services;

/// <summary>
/// Critère de tri d'un onglet de l'inventaire : « par défaut » (numéro d'objet commun à tous les jeux, qui garde ensemble
/// les Balls, les pierres, les potions... ; nom de la capacité pour les CT), nom ou quantité. À égalité de quantité, par nom.
/// </summary>
public enum ItemSortBy { Default, Name, Quantity }

/// <summary>Tri d'un onglet de l'inventaire : croissant par défaut, <paramref name="Descending"/> l'inverse.</summary>
public readonly record struct ItemSort(ItemSortBy By = ItemSortBy.Default, bool Descending = false);

/// <summary>Tri de la vue générale des living dex : par génération croissante par défaut.</summary>
public readonly record struct LivingDexOrder(LivingDexSort By = LivingDexSort.Generation, bool Descending = false);

/// <summary>Tri de la vue générale des rubans : par ordre d'ajout (le premier suivi d'abord) par défaut.</summary>
public readonly record struct RibbonOrder(RibbonSort By = RibbonSort.Arrival, bool Descending = false);

/// <summary>Valeurs « tri » et « ordre » d'une adresse (« ?tri=nom&amp;ordre=desc ») ; null : absente.</summary>
public readonly record struct SortQuery(string? Tri, string? Ordre)
{
    /// <summary>Ni « tri » ni « ordre » : c'est le tri par défaut du compte qui s'applique.</summary>
    public bool IsEmpty => Tri is null && Ordre is null;
}

/// <summary>
/// Les tris de l'inventaire, de « Ma collection » et des vues générales des living dex et des rubans : leurs noms dans l'adresse
/// (« ?tri= », « ?ordre= »), leurs libellés, et la lecture d'une adresse. Une valeur inconnue (adresse ou formulaire
/// trafiqués) donne le premier critère et le sens par défaut, jamais une erreur.
/// </summary>
public static class SiteSorts
{
    /// <summary>Nom de chaque onglet de l'inventaire dans l'adresse (« ?onglet=baies ») et dans les préférences.</summary>
    public static readonly IReadOnlyDictionary<ItemCategory, string> InventoryTabs = new Dictionary<ItemCategory, string>
    {
        [ItemCategory.Items] = "objets",
        [ItemCategory.Balls] = "pokeballs",
        [ItemCategory.Medicine] = "medicaments",
        [ItemCategory.Berries] = "baies",
        [ItemCategory.Machines] = "ct",
    };

    public static readonly IReadOnlyList<(ItemSortBy By, string Slug, string Label)> ItemChoices =
    [
        (ItemSortBy.Default, "defaut", "Par défaut"),
        (ItemSortBy.Name, "nom", "Nom"),
        (ItemSortBy.Quantity, "quantite", "Quantité"),
    ];

    public static readonly IReadOnlyList<(CollectionSortBy By, string Slug, string Label)> CollectionChoices =
    [
        (CollectionSortBy.Arrival, "arrivee", "Arrivée"),
        (CollectionSortBy.Name, "nom", "Nom"),
        (CollectionSortBy.Dex, "pokedex", "N° Pokédex"),
    ];

    public static readonly IReadOnlyList<(LivingDexSort By, string Slug, string Label)> LivingDexChoices =
    [
        (LivingDexSort.Generation, "generation", "Génération"),
        (LivingDexSort.Completion, "completion", "Complétion"),
    ];

    public static readonly IReadOnlyList<(RibbonSort By, string Slug, string Label)> RibbonChoices =
    [
        (RibbonSort.Arrival, "ajout", "Ajout"),
        (RibbonSort.Name, "nom", "Nom"),
        (RibbonSort.Completion, "completion", "Complétion"),
    ];

    public static string Slug(ItemSortBy by) => ItemChoices.First(c => c.By == by).Slug;
    public static string Slug(CollectionSortBy by) => CollectionChoices.First(c => c.By == by).Slug;
    public static string Slug(LivingDexSort by) => LivingDexChoices.First(c => c.By == by).Slug;
    public static string Slug(RibbonSort by) => RibbonChoices.First(c => c.By == by).Slug;

    // ---------- Inventaire : « ordre=desc » pour décroissant ----------

    /// <summary>Tri d'un onglet lu dans l'adresse ; null si elle n'en dit rien.</summary>
    public static ItemSort? ItemFromQuery(SortQuery query) => query.IsEmpty
        ? null
        : new ItemSort(ItemChoices.FirstOrDefault(c => c.Slug == query.Tri, ItemChoices[0]).By, query.Ordre == "desc");

    public static SortQuery ItemQuery(ItemSort sort) => new(Slug(sort.By), sort.Descending ? "desc" : null);

    // ---------- Collection : « ordre=asc|desc », le sens réel ----------

    /// <summary>L'arrivée montre les plus récents d'abord ; le nom et le numéro sont croissants.</summary>
    public static bool DescendingByDefault(CollectionSortBy by) => by == CollectionSortBy.Arrival;

    /// <summary>Le tri montre-t-il le plus grand (ou le plus récent) d'abord ?</summary>
    public static bool IsDescending(CollectionSort sort) => DescendingByDefault(sort.By) != sort.Reversed;

    /// <summary>Tri de la collection lu dans l'adresse (sens par défaut du critère, sauf « ordre » contraire) ; null si elle n'en dit rien.</summary>
    public static CollectionSort? CollectionFromQuery(SortQuery query)
    {
        if (query.IsEmpty)
            return null;
        var by = CollectionChoices.FirstOrDefault(c => c.Slug == query.Tri, CollectionChoices[0]).By;
        var descending = query.Ordre is "asc" or "desc" ? query.Ordre == "desc" : DescendingByDefault(by);
        return new CollectionSort(by, descending != DescendingByDefault(by));
    }

    public static SortQuery CollectionQuery(CollectionSort sort) =>
        new(Slug(sort.By), sort.Reversed ? (IsDescending(sort) ? "desc" : "asc") : null);

    // ---------- Living dex : « ordre=desc » pour décroissant ----------

    /// <summary>Tri de la vue générale lu dans l'adresse ; null si elle n'en dit rien.</summary>
    public static LivingDexOrder? LivingDexFromQuery(SortQuery query) => query.IsEmpty
        ? null
        : new LivingDexOrder(LivingDexChoices.FirstOrDefault(c => c.Slug == query.Tri, LivingDexChoices[0]).By, query.Ordre == "desc");

    public static SortQuery LivingDexQuery(LivingDexOrder order) => new(Slug(order.By), order.Descending ? "desc" : null);

    // ---------- Rubans : « ordre=desc » pour décroissant ----------

    /// <summary>Tri de la vue générale des rubans lu dans l'adresse ; null si elle n'en dit rien.</summary>
    public static RibbonOrder? RibbonFromQuery(SortQuery query) => query.IsEmpty
        ? null
        : new RibbonOrder(RibbonChoices.FirstOrDefault(c => c.Slug == query.Tri, RibbonChoices[0]).By, query.Ordre == "desc");

    public static SortQuery RibbonQuery(RibbonOrder order) => new(Slug(order.By), order.Descending ? "desc" : null);

    /// <summary>
    /// Ce qu'il faut écrire dans l'adresse pour un tri choisi sur la page : rien quand c'est le tri par défaut du compte
    /// (l'adresse reste courte, et suit un changement de préférence), sinon le critère et le sens, toujours écrits en entier
    /// (« ?tri=arrivee » quand le défaut du compte est le nom).
    /// </summary>
    public static Dictionary<string, object?> AddressFor(SortQuery chosen, SortQuery accountDefault) => chosen == accountDefault
        ? new() { ["tri"] = null, ["ordre"] = null }
        : new() { ["tri"] = chosen.Tri, ["ordre"] = chosen.Ordre };
}

/// <summary>
/// Tris par défaut d'un compte (« Mon compte » → « Préférences ») : un par onglet de l'inventaire, celui de « Ma
/// collection » et ceux des vues générales des living dex et des rubans. Ils s'appliquent quand l'adresse ne précise pas de tri, sur les
/// pages du joueur comme sur celles des autres joueurs qu'il consulte. Rangés en JSON dans <see cref="ApplicationUser.SortPreferences"/>,
/// avec les noms de l'adresse : <c>{"inventaire":{"ct":{"tri":"nom"}},"collection":{"tri":"nom","ordre":"desc"},"rubans":{"tri":"completion"}}</c>.
/// </summary>
public sealed class SortPreferences
{
    /// <summary>Les tris d'origine du site : par défaut dans l'inventaire, par arrivée, par génération, par ajout.</summary>
    public static readonly SortPreferences Default = new();

    public IReadOnlyDictionary<ItemCategory, ItemSort> Inventory { get; init; } = new Dictionary<ItemCategory, ItemSort>();
    public CollectionSort Collection { get; init; }
    public LivingDexOrder LivingDex { get; init; }
    public RibbonOrder Ribbons { get; init; }

    public ItemSort InventoryFor(ItemCategory category) => Inventory.GetValueOrDefault(category);

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private sealed class Stored
    {
        public Dictionary<string, StoredSort>? Inventaire { get; set; }
        public StoredSort? Collection { get; set; }
        public StoredSort? Livingdex { get; set; }
        public StoredSort? Rubans { get; set; }
    }

    private sealed class StoredSort
    {
        public string? Tri { get; set; }
        public string? Ordre { get; set; }
        public SortQuery ToQuery() => new(Tri, Ordre);
        public static StoredSort From(SortQuery query) => new() { Tri = query.Tri, Ordre = query.Ordre };
    }

    /// <summary>JSON à enregistrer ; null quand tout est au tri d'origine (la colonne reste vide).</summary>
    public string? ToJson()
    {
        var inventory = Inventory.Where(s => s.Value != default)
            .ToDictionary(s => SiteSorts.InventoryTabs[s.Key], s => StoredSort.From(SiteSorts.ItemQuery(s.Value)));
        var stored = new Stored
        {
            Inventaire = inventory.Count == 0 ? null : inventory,
            Collection = Collection == default ? null : StoredSort.From(SiteSorts.CollectionQuery(Collection)),
            Livingdex = LivingDex == default ? null : StoredSort.From(SiteSorts.LivingDexQuery(LivingDex)),
            Rubans = Ribbons == default ? null : StoredSort.From(SiteSorts.RibbonQuery(Ribbons)),
        };
        return stored.Inventaire is null && stored.Collection is null && stored.Livingdex is null && stored.Rubans is null
            ? null
            : JsonSerializer.Serialize(stored, Json);
    }

    /// <summary>Préférences lues en base ; absentes ou illisibles : les tris d'origine (une partie illisible seulement : elle seule).</summary>
    public static SortPreferences FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Default;
        Stored? stored;
        try
        {
            stored = JsonSerializer.Deserialize<Stored>(json, Json);
        }
        catch (JsonException)
        {
            return Default;
        }
        if (stored is null)
            return Default;
        var inventory = new Dictionary<ItemCategory, ItemSort>();
        foreach (var (tab, sort) in stored.Inventaire ?? [])
            if (sort is not null && SiteSorts.InventoryTabs.FirstOrDefault(t => t.Value == tab) is { Value: not null } category
                && SiteSorts.ItemFromQuery(sort.ToQuery()) is { } itemSort)
                inventory[category.Key] = itemSort;
        return new SortPreferences
        {
            Inventory = inventory,
            Collection = stored.Collection is { } c ? SiteSorts.CollectionFromQuery(c.ToQuery()) ?? default : default,
            LivingDex = stored.Livingdex is { } l ? SiteSorts.LivingDexFromQuery(l.ToQuery()) ?? default : default,
            Ribbons = stored.Rubans is { } r ? SiteSorts.RibbonFromQuery(r.ToQuery()) ?? default : default,
        };
    }
}

/// <summary>Lecture et enregistrement des préférences d'un compte (aujourd'hui : les tris par défaut).</summary>
public sealed class PreferencesService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    /// <summary>Tris par défaut du compte ; les tris d'origine sans compte (ou compte supprimé).</summary>
    public async Task<SortPreferences> SortsAsync(string? userId)
    {
        if (string.IsNullOrEmpty(userId))
            return SortPreferences.Default;
        await using var db = await dbFactory.CreateDbContextAsync();
        return SortPreferences.FromJson(await db.Users.Where(u => u.Id == userId).Select(u => u.SortPreferences).FirstOrDefaultAsync());
    }

    public async Task<AccountResult> SetSortsAsync(string userId, SortPreferences sorts)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var json = sorts.ToJson();
        if (await db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(u => u.SetProperty(x => x.SortPreferences, json)) == 0)
            return new(false, "Ce compte n'existe plus.");
        return new(true, "Tris par défaut enregistrés.");
    }
}
