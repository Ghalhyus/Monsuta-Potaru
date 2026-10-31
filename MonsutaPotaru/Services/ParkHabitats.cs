using System.Text.Json;
using PKHeX.Core;

namespace MonsutaPotaru.Services;

/// <summary>
/// Type d'une case du parc (feuille « Habitat » de l'utilisateur), dans l'ordre des chiffres du champ « habitats » des
/// cartes (écrit par outils/carte-parc.py d'après les planches de chaque case).
/// </summary>
public enum Habitat : byte
{
    None = 0,
    Sand = 1,
    Cave = 2,
    Cliff = 3,
    Vegetation = 4,
    Water = 5,
    Dungeon = 6,
    Volcano = 7,
}

/// <summary>Avis d'un type de Pokémon sur un type de case (« o » et « x » de la feuille « Habitat »).</summary>
public enum HabitatOpinion { Neutral, Likes, Dislikes }

/// <summary>Types de cases d'une carte du parc, et leur nombre dans sa zone « Parc » (là où vivent les Pokémon).</summary>
public sealed class ParkHabitatMap(int width, int height, Habitat[] cells, (int X, int Y, int W, int H) area)
{
    public int Width => width;
    public int Height => height;
    /// <summary>Zone « Parc », en cases.</summary>
    public (int X, int Y, int W, int H) Area => area;

    /// <summary>Type de la case (x, y) ; hors de la carte : aucun.</summary>
    public Habitat At(int x, int y) =>
        x < 0 || y < 0 || x >= width || y >= height ? Habitat.None : cells[y * width + x];

    /// <summary>
    /// Nombre de cases de chaque type dans la zone « Parc », bloquées comprises (l'eau, les arbres : on n'y marche pas,
    /// mais ils font partie du parc). Calculé une fois.
    /// </summary>
    public IReadOnlyDictionary<Habitat, int> Counts { get; } = Count(width, cells, area);

    private static Dictionary<Habitat, int> Count(int width, Habitat[] cells, (int X, int Y, int W, int H) area)
    {
        var counts = Enum.GetValues<Habitat>().ToDictionary(h => h, _ => 0);
        for (var y = area.Y; y < area.Y + area.H; y++)
            for (var x = area.X; x < area.X + area.W; x++)
                counts[cells[y * width + x]]++;
        return counts;
    }
}

/// <summary>
/// Types de cases du parc : ce que chaque type de Pokémon aime ou non (feuille « Habitat », pour son bonheur), et ceux de
/// chaque carte (wwwroot/parc/parc-1-{taille}.json), lus au démarrage par <see cref="Load"/>.
/// </summary>
public static class ParkHabitats
{
    /// <summary>Les types de cases, sans « aucun », dans l'ordre de la feuille.</summary>
    public static IReadOnlyList<Habitat> All { get; } = [.. Enum.GetValues<Habitat>().Where(h => h != Habitat.None)];

    public static string Name(Habitat habitat) => habitat switch
    {
        Habitat.Sand => "Sable",
        Habitat.Cave => "Cave",
        Habitat.Cliff => "Falaise",
        Habitat.Vegetation => "Végétation",
        Habitat.Water => "Eau",
        Habitat.Dungeon => "Donjon",
        Habitat.Volcano => "Volcan",
        _ => "Aucun",
    };

    /// <summary>Couleur de la case dans l'affichage des types (mode debug du parc).</summary>
    public static string Color(Habitat habitat) => habitat switch
    {
        Habitat.Sand => "#f2d16b",
        Habitat.Cave => "#8b5a2b",
        Habitat.Cliff => "#9aa3ad",
        Habitat.Vegetation => "#2ecc40",
        Habitat.Water => "#1e7bff",
        Habitat.Dungeon => "#9b4dff",
        Habitat.Volcano => "#ff4b2b",
        _ => "transparent",
    };

    // Feuille « Habitat », recopiée telle quelle : une colonne par type de case (Sable, Cave, Falaise, Végétation, Eau,
    // Donjon, Volcan), « o » aime, « x » n'aime pas, « . » rien.
    private static readonly Dictionary<MoveType, string> Sheet = new()
    {
        //                     SCFVEDV
        [MoveType.Normal]   = "...o.xx",
        [MoveType.Flying]   = ".xoo.xx",
        [MoveType.Ground]   = "oo..x..",
        [MoveType.Bug]      = "...o..x",
        [MoveType.Steel]    = "x..oxox",
        [MoveType.Water]    = "....o.x",
        [MoveType.Electric] = "...ox.x",
        [MoveType.Ice]      = "......x",
        [MoveType.Dark]     = ".o...o.",
        [MoveType.Fighting] = "..oo...",
        [MoveType.Poison]   = "x..o.o.",
        [MoveType.Rock]     = "xo.xx.o",
        [MoveType.Ghost]    = ".o...o.",
        [MoveType.Fire]     = "xx..x.o",
        [MoveType.Grass]    = "x..o.xx",
        [MoveType.Psychic]  = "...o.o.",
        [MoveType.Dragon]   = ".oo..oo",
        [MoveType.Fairy]    = "xx.o.xx",
    };

    /// <summary>Avis du type de Pokémon (index PKHeX, comme <c>BankPokemon.Type1</c>) sur le type de case.</summary>
    public static HabitatOpinion OpinionOf(byte type, Habitat habitat) =>
        habitat == Habitat.None || !Sheet.TryGetValue((MoveType)type, out var row) ? HabitatOpinion.Neutral
        : row[(int)habitat - 1] switch { 'o' => HabitatOpinion.Likes, 'x' => HabitatOpinion.Dislikes, _ => HabitatOpinion.Neutral };

    /// <summary>Types de cases qu'aime le type de Pokémon.</summary>
    public static IEnumerable<Habitat> Liked(byte type) => All.Where(h => OpinionOf(type, h) == HabitatOpinion.Likes);

    /// <summary>Types de cases que n'aime pas le type de Pokémon.</summary>
    public static IEnumerable<Habitat> Disliked(byte type) => All.Where(h => OpinionOf(type, h) == HabitatOpinion.Dislikes);

    private static Dictionary<int, ParkHabitatMap> _maps = [];

    /// <summary>
    /// Lit les types de cases des cartes du parc (une par taille) ; une carte absente ou sans « habitats » n'en a pas
    /// (<see cref="Map"/> rend null).
    /// </summary>
    public static void Load(string webRoot)
    {
        var maps = new Dictionary<int, ParkHabitatMap>();
        for (var size = 1; size <= ParkService.MaxExpansions + 1; size++)
        {
            var path = Path.Combine(webRoot, "parc", $"parc-1-{size}.json");
            if (File.Exists(path) && Read(File.ReadAllText(path)) is { } map)
                maps[size] = map;
        }
        _maps = maps;
    }

    /// <summary>Types de cases de la carte du parc de cette taille (1 : sans agrandissement) ; null si elle n'en a pas.</summary>
    public static ParkHabitatMap? Map(int size) => _maps.GetValueOrDefault(size);

    /// <summary>Lit une carte du parc (JSON de Tiled passé par outils/carte-parc.py) ; null sans « habitats ».</summary>
    public static ParkHabitatMap? Read(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("habitats", out var rows))
            return null;
        int width = root.GetProperty("width").GetInt32(), height = root.GetProperty("height").GetInt32();
        var cells = new Habitat[width * height];
        var y = 0;
        foreach (var row in rows.EnumerateArray())
        {
            var text = row.GetString() ?? "";
            for (var x = 0; x < width && x < text.Length && y < height; x++)
                cells[y * width + x] = text[x] is >= '0' and <= '7' ? (Habitat)(text[x] - '0') : Habitat.None;
            y++;
        }
        return new ParkHabitatMap(width, height, cells, ParkArea(root, width, height));
    }

    /// <summary>Zone « Parc » (sinon « Départ », sinon toute la carte), en cases, ramenée à la carte (comme parc.js).</summary>
    private static (int X, int Y, int W, int H) ParkArea(JsonElement root, int width, int height)
    {
        var zones = root.GetProperty("layers").EnumerateArray()
            .Where(l => l.GetProperty("name").GetString() == "Zones" && l.TryGetProperty("objects", out _))
            .SelectMany(l => l.GetProperty("objects").EnumerateArray()).ToList();
        var zone = zones.FirstOrDefault(o => o.GetProperty("name").GetString() == "Parc");
        if (zone.ValueKind == JsonValueKind.Undefined)
            zone = zones.FirstOrDefault(o => o.GetProperty("name").GetString() == "Départ");
        if (zone.ValueKind == JsonValueKind.Undefined)
            return (0, 0, width, height);
        int Tiles(string property) => (int)Math.Round(zone.GetProperty(property).GetDouble() / 16);
        int x = Math.Clamp(Tiles("x"), 0, width), y = Math.Clamp(Tiles("y"), 0, height);
        return (x, y, Math.Clamp(Tiles("width"), 0, width - x), Math.Clamp(Tiles("height"), 0, height - y));
    }
}
