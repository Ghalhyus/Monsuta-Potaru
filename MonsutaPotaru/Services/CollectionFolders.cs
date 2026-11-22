namespace MonsutaPotaru.Services;

/// <summary>Clé d'un dossier du mode compact de « Ma collection » : une espèce (toutes formes réunies), ou tous les œufs.</summary>
public readonly record struct CollectionGroupKey(ushort Species, bool Eggs = false)
{
    /// <summary>Le dossier « Œufs » : leur espèce reste cachée, ils vont tous ensemble.</summary>
    public static readonly CollectionGroupKey AllEggs = new(0, Eggs: true);
}

/// <summary>
/// Un dossier du mode compact : les Pokémon d'une espèce (ou les œufs) qui correspondent à la recherche, à l'origine et aux
/// pastilles. Les dates, positions et identifiants extrêmes servent à le placer comme son premier Pokémon dans le tri.
/// </summary>
/// <param name="FirstId">Plus petit identifiant : celui du Pokémon quand le dossier n'en contient qu'un.</param>
/// <param name="SpriteSpecies">Espèce dont on montre l'image (pour les œufs : celle du premier œuf, comme dans la grille).</param>
public sealed record CollectionGroup(CollectionGroupKey Key, int Count, string SpeciesName, string SortName,
    DateTime Newest, DateTime Oldest, int FirstPosition, int LastPosition, int FirstId, int LastId, ushort SpriteSpecies)
{
    /// <summary>Un seul Pokémon : une case normale plutôt qu'un dossier.</summary>
    public bool IsSingle => Count == 1;

    /// <summary>Image du dossier : forme de base, non chromatique (l'image d'un œuf pour le dossier « Œufs »).</summary>
    public string SpriteUrl => PokemonSprites.Url(SpriteSpecies, 0, PKHeX.Core.EntityContext.Gen9, false, 0, false, Key.Eggs);

    /// <summary>Nom affiché : l'espèce, ou « Œufs ».</summary>
    public string Label => Key.Eggs ? "Œufs" : SpeciesName;

    /// <summary>
    /// Range les dossiers comme leur premier Pokémon dans ce tri (voir <see cref="CollectionSort"/>) : le dossier « Œufs »
    /// à la fin ; par arrivée, le plus récent du dossier (ou le plus ancien, à l'envers) ; par nom ; par numéro ; par quantité
    /// (les plus nombreux d'abord, à égalité par numéro croissant dans les deux sens).
    /// </summary>
    public static List<CollectionGroup> Order(IEnumerable<CollectionGroup> groups, CollectionSort sort)
    {
        var eggsLast = groups.OrderBy(g => g.Key.Eggs);
        var sorted = (sort.By, sort.Reversed) switch
        {
            (CollectionSortBy.Name, false) => eggsLast.ThenBy(g => g.SortName, StringComparer.Ordinal).ThenBy(g => g.Key.Species),
            (CollectionSortBy.Name, true) => eggsLast.ThenByDescending(g => g.SortName, StringComparer.Ordinal).ThenBy(g => g.Key.Species),
            (CollectionSortBy.Dex, false) => eggsLast.ThenBy(g => g.Key.Species),
            (CollectionSortBy.Dex, true) => eggsLast.ThenByDescending(g => g.Key.Species),
            (CollectionSortBy.Quantity, false) => eggsLast.ThenByDescending(g => g.Count).ThenBy(g => g.Key.Species),
            (CollectionSortBy.Quantity, true) => eggsLast.ThenBy(g => g.Count).ThenBy(g => g.Key.Species),
            (CollectionSortBy.Arrival, true) => eggsLast.ThenBy(g => g.Oldest).ThenByDescending(g => g.LastPosition).ThenBy(g => g.FirstId),
            _ => eggsLast.ThenByDescending(g => g.Newest).ThenBy(g => g.FirstPosition).ThenByDescending(g => g.LastId),
        };
        return sorted.ToList();
    }
}

/// <summary>
/// Cases de la grille en mode compact : chaque dossier prend une case (une espèce seule : la case de son Pokémon) ; un
/// dossier ouvert est suivi de ses Pokémon, qui décalent la suite. Sert à retrouver ce qu'il faut lire en base pour les
/// lignes que <c>Virtualize</c> demande.
/// </summary>
public sealed class CollectionFolderLayout
{
    private readonly List<CollectionGroup> groups;
    private readonly int[] starts; // première case de chaque dossier

    public CollectionFolderLayout(List<CollectionGroup> groups, IReadOnlySet<CollectionGroupKey> open)
    {
        this.groups = groups;
        starts = new int[groups.Count];
        var cell = 0;
        for (var i = 0; i < groups.Count; i++)
        {
            starts[i] = cell;
            cell += 1 + (IsOpen(groups[i], open) ? groups[i].Count : 0);
        }
        CellCount = cell;
        OpenKeys = open;
    }

    public static readonly CollectionFolderLayout Empty = new([], new HashSet<CollectionGroupKey>());

    private IReadOnlySet<CollectionGroupKey> OpenKeys { get; }

    /// <summary>Nombre de cases (dossiers, Pokémon seuls, Pokémon des dossiers ouverts).</summary>
    public int CellCount { get; }

    /// <summary>Nombre de Pokémon (le compte affiché en tête de page).</summary>
    public int PokemonCount => groups.Sum(g => g.Count);

    public IReadOnlyList<CollectionGroup> Groups => groups;

    private static bool IsOpen(CollectionGroup group, IReadOnlySet<CollectionGroupKey> open) => !group.IsSingle && open.Contains(group.Key);

    public bool IsOpen(CollectionGroup group) => IsOpen(group, OpenKeys);

    /// <summary>
    /// Ce qu'occupent les cases [<paramref name="first"/>, <paramref name="end"/>[ : dans l'ordre, la case d'un dossier
    /// (<see cref="CollectionLayoutPart.MemberCount"/> nul) ou une tranche des Pokémon d'un dossier ouvert.
    /// </summary>
    public List<CollectionLayoutPart> Slice(int first, int end)
    {
        var parts = new List<CollectionLayoutPart>();
        end = Math.Min(end, CellCount);
        if (first >= end)
            return parts;
        var index = Array.BinarySearch(starts, first);
        if (index < 0)
            index = ~index - 1; // la case tombe dans le contenu du dossier précédent
        for (var i = Math.Max(index, 0); i < groups.Count && starts[i] < end; i++)
        {
            var group = groups[i];
            if (starts[i] >= first)
                parts.Add(new(group, 0, 0));
            if (!IsOpen(group))
                continue;
            var from = Math.Max(0, first - starts[i] - 1);
            var to = Math.Min(group.Count, end - starts[i] - 1);
            if (to > from)
                parts.Add(new(group, from, to - from));
        }
        return parts;
    }
}

/// <summary>
/// Morceau de la grille compacte : la case du dossier <paramref name="Group"/> (<paramref name="MemberCount"/> nul), ou
/// ses Pokémon n° <paramref name="MemberSkip"/> à <paramref name="MemberSkip"/> + <paramref name="MemberCount"/> - 1.
/// </summary>
public readonly record struct CollectionLayoutPart(CollectionGroup Group, int MemberSkip, int MemberCount)
{
    public bool IsHead => MemberCount == 0;
}
