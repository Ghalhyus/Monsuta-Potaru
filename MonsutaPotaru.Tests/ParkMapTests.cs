using System.Text.Json;
using MonsutaPotaru.Services;
using PKHeX.Core;

namespace MonsutaPotaru.Tests;

/// <summary>
/// Cartes de chaque parc, une par taille (wwwroot/parc/{parc}-{taille}.json, écrites par outils/generer-parc.py,
/// generer-plage.py, generer-volcan.py, generer-desert.py, generer-ocean.py, generer-grotte.py, generer-montagne.py ou generer-donjon.py puis carte-parc.py) : ce que parc.js lit, une zone « Parc » où chaque case libre est
/// accessible depuis les autres (pas de Pokémon enfermé ; à l'océan, d'un îlot à l'autre à la nage ; à la montagne, en volant), et chaque agrandissement qui
/// garde les cases libres de la taille d'avant.
/// </summary>
public sealed class ParkMapTests
{
    private const int Tile = 16;
    private static readonly string[] Layers = ["Sol", "Décors", "Devant", "Collisions"];

    /// <summary>Cartes de chaque parc, sans la taille : le parc 1 (prairie), la plage, le volcan, le désert, l'océan, la grotte, la montagne et le donjon.</summary>
    private static readonly string[] Parks = ["parc-1", "plage-1", "volcan-1", "desert-1", "ocean-1", "grotte-1", "montagne-1", "donjon-1"];

    /// <summary>Tailles de chaque parc : une carte pour chacune (sans agrandissement, puis un à trois).</summary>
    public static TheoryData<string, int> Sizes()
    {
        var data = new TheoryData<string, int>();
        foreach (var park in Parks)
            for (var size = 1; size <= ParkService.MaxExpansions + 1; size++)
                data.Add(park, size);
        return data;
    }

    private static string Json(string park, int size) =>
        File.ReadAllText(Path.Combine(RealSavesTests.WebRoot(), "parc", $"{park}-{size}.json"));

    private static JsonElement Map(string park, int size) => JsonDocument.Parse(Json(park, size)).RootElement;

    private static JsonElement Layer(JsonElement map, string name) =>
        map.GetProperty("layers").EnumerateArray().Single(l => l.GetProperty("name").GetString() == name);

    private static (int X, int Y, int W, int H) Zone(JsonElement map, string name)
    {
        var zone = Layer(map, "Zones").GetProperty("objects").EnumerateArray().Single(o => o.GetProperty("name").GetString() == name);
        int Tiles(string property) => (int)Math.Round(zone.GetProperty(property).GetDouble() / Tile);
        return (Tiles("x"), Tiles("y"), Tiles("width"), Tiles("height"));
    }

    /// <summary>Cases où les Pokémon marchent : dans la zone « Parc », sans tuile du calque « Collisions » (comme parc.js).</summary>
    private static HashSet<(int x, int y)> FreeTiles(JsonElement map)
    {
        int width = map.GetProperty("width").GetInt32();
        var (x0, y0, w, h) = Zone(map, "Parc");
        var collisions = Layer(map, "Collisions").GetProperty("data").EnumerateArray().Select(t => t.GetInt64()).ToArray();
        return Enumerable.Range(y0, h).SelectMany(y => Enumerable.Range(x0, w).Select(x => (x, y)))
            .Where(c => collisions[c.y * width + c.x] == 0).ToHashSet();
    }

    /// <summary>
    /// Cases d'un calque de la montagne (« Sommets » : où un volant se pose ; « Survol » : rebords et flancs qu'il survole),
    /// dans la zone « Parc », comme parc.js ; aucune ailleurs.
    /// </summary>
    private static HashSet<(int x, int y)> Marked(JsonElement map, string layer)
    {
        if (!map.GetProperty("layers").EnumerateArray().Any(l => l.GetProperty("name").GetString() == layer))
            return [];
        int width = map.GetProperty("width").GetInt32();
        var (x0, y0, w, h) = Zone(map, "Parc");
        var data = Layer(map, layer).GetProperty("data").EnumerateArray().Select(t => t.GetInt64()).ToArray();
        return Enumerable.Range(y0, h).SelectMany(y => Enumerable.Range(x0, w).Select(x => (x, y)))
            .Where(c => data[c.y * width + c.x] != 0).ToHashSet();
    }

    /// <summary>
    /// Eau libre, comme parc.js : dans la zone « Parc », bloquée, de type eau, sans objet posé (« Décors », « Devant ») ;
    /// les nageurs y vont (à l'océan, seulement eux).
    /// </summary>
    private static HashSet<(int x, int y)> OpenWater(string park, int size)
    {
        var map = Map(park, size);
        var habitats = ParkHabitats.Read(Json(park, size))!;
        int width = map.GetProperty("width").GetInt32();
        var (x0, y0, w, h) = Zone(map, "Parc");
        long[] Data(string layer) => Layer(map, layer).GetProperty("data").EnumerateArray().Select(t => t.GetInt64()).ToArray();
        long[] collisions = Data("Collisions"), decors = Data("Décors"), front = Data("Devant");
        return Enumerable.Range(y0, h).SelectMany(y => Enumerable.Range(x0, w).Select(x => (x, y)))
            .Where(c => collisions[c.y * width + c.x] != 0 && decors[c.y * width + c.x] == 0 && front[c.y * width + c.x] == 0
                        && habitats.At(c.x, c.y) == Habitat.Water)
            .ToHashSet();
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public void Map_HasWhatTheParkReads(string park, int size)
    {
        var map = Map(park, size);
        Assert.Equal("orthogonal", map.GetProperty("orientation").GetString());
        Assert.False(map.GetProperty("infinite").GetBoolean());
        Assert.Equal(Tile, map.GetProperty("tilewidth").GetInt32());
        Assert.Equal(Tile, map.GetProperty("tileheight").GetInt32());
        var tiles = map.GetProperty("width").GetInt32() * map.GetProperty("height").GetInt32();
        foreach (var name in Layers)
        {
            var layer = Layer(map, name);
            Assert.Equal("tilelayer", layer.GetProperty("type").GetString());
            Assert.Equal(tiles, layer.GetProperty("data").GetArrayLength()); // CSV, pas de compression
        }
        // Jeux de tuiles incorporés, planche cherchée dans images/parc/tuiles/ sous son seul nom de fichier
        foreach (var tileset in map.GetProperty("tilesets").EnumerateArray())
        {
            Assert.False(tileset.TryGetProperty("source", out _));
            var image = tileset.GetProperty("image").GetString()!;
            Assert.Equal(Path.GetFileName(image), image);
            Assert.EndsWith(".png", image);
        }
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public void ParkZone_InsideTheMap_AndEveryFreeTileReachable(string park, int size)
    {
        var map = Map(park, size);
        int width = map.GetProperty("width").GetInt32(), height = map.GetProperty("height").GetInt32();
        var (sx, sy, sw, sh) = Zone(map, "Départ");
        var (x0, y0, w, h) = Zone(map, "Parc");
        Assert.True(x0 >= 0 && y0 >= 0 && x0 + w <= width && y0 + h <= height, "zone « Parc » hors de la carte");
        Assert.True(x0 <= sx && y0 <= sy && sx + sw <= x0 + w && sy + sh <= y0 + h, "zone « Départ » hors de la zone « Parc »");

        var freeTiles = FreeTiles(map);
        var free = freeTiles.Order().ToList();
        // À l'océan, seuls les nageurs vivent : ils passent d'un îlot à l'autre par l'eau libre, et s'y tiennent aussi (à la
        // montagne, la vallée est reliée à pied : les sommets des volants sont vérifiés par Mountain_FlyersLandOnTheSummits)
        HashSet<(int x, int y)> passable = park == "ocean-1" ? [.. freeTiles, .. OpenWater(park, size)] : freeTiles;
        bool Free(int x, int y) => passable.Contains((x, y));
        // Place pour ses Pokémon : 20 par taille, une quinzaine de cases chacun au moins ; à la plage, la mer est bloquée et
        // les zones choisies par l'utilisateur laissent ~8 cases de sable (les types Eau nagent en plus dans la mer) ; au
        // volcan, la lave (17 à 31 % de la zone) est bloquée, 14 cases de roche dans la vue de départ (les Feu y vont en plus) ;
        // au désert, le flanc du plateau (3 lignes) et l'oasis traversent la vue de départ : 11 cases (les Eau nagent en plus)
        // à l'océan, l'eau libre compte (seuls les nageurs y vivent) ; à la montagne, le plateau de la vue de départ, son flanc
        // et la rivière traversent la vue de départ : 11 cases de vallée (les volants se posent en plus sur les sommets)
        var capacity = ParkService.CapacityOf(size - 1);
        var perPokemon = park switch { "plage-1" => 7, "desert-1" or "montagne-1" => 11, _ => 14 };
        Assert.True(passable.Count >= capacity * perPokemon, $"zone « Parc » trop encombrée : {passable.Count} cases libres pour {capacity} Pokémon");

        // Comme parc.js : en diagonale seulement si les deux côtés sont libres
        // À l'océan, la vue de départ n'a pas de terre : parc.js part de l'eau en son centre (Ground.openLiquid)
        var origin = park == "ocean-1" ? (sx + sw / 2, sy + sh / 2) : free[0];
        Assert.Contains(origin, passable);
        var reached = new HashSet<(int x, int y)> { origin };
        var queue = new Queue<(int x, int y)>(reached);
        while (queue.TryDequeue(out var c))
        {
            for (var dy = -1; dy <= 1; dy++)
                for (var dx = -1; dx <= 1; dx++)
                {
                    if ((dx == 0 && dy == 0) || !Free(c.x + dx, c.y + dy)) continue;
                    if (dx != 0 && dy != 0 && (!Free(c.x + dx, c.y) || !Free(c.x, c.y + dy))) continue;
                    if (reached.Add((c.x + dx, c.y + dy))) queue.Enqueue((c.x + dx, c.y + dy));
                }
        }
        var enclosed = free.Where(c => !reached.Contains(c)).ToList();
        Assert.True(enclosed.Count == 0, $"cases libres inaccessibles (Pokémon enfermé) : {string.Join(", ", enclosed.Take(10))}");
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public void Expansion_KeepsEveryFreeTileOfTheSmallerPark(string park, int size)
    {
        if (size == 1) return;
        var before = FreeTiles(Map(park, size - 1));
        var after = FreeTiles(Map(park, size));
        var lost = before.Where(c => !after.Contains(c)).ToList();
        Assert.True(lost.Count == 0, $"cases libres perdues en passant à la taille {size} : {string.Join(", ", lost.Take(10))}");
        Assert.True(after.Count > before.Count);
        // Montagne : un sommet où l'on se posait le reste
        var summitsAfter = Marked(Map(park, size), "Sommets");
        var summitsLost = Marked(Map(park, size - 1), "Sommets").Where(c => !summitsAfter.Contains(c)).ToList();
        Assert.True(summitsLost.Count == 0, $"sommets perdus en passant à la taille {size} : {string.Join(", ", summitsLost.Take(10))}");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Beach_IsAboutTwoThirdsSea_AtEverySize(int size)
    {
        // Choix de l'utilisateur : environ 65 % de mer et 35 % de sable, les types Eau y sont heureux, Feu malheureux. La
        // taille 1 est la vue de départ, moitié plage moitié mer : un peu plus de la moitié de mer, les Eau heureux quand même.
        var map = ParkHabitats.Read(Json("plage-1", size))!;
        var total = map.Area.W * map.Area.H;
        int sea = map.Counts[Habitat.Water] * 100 / total, sand = map.Counts[Habitat.Sand] * 100 / total;
        Assert.InRange(sea, size == 1 ? 51 : 60, size == 1 ? 55 : 70);
        Assert.InRange(sand, 30, size == 1 ? 48 : 40);
        Assert.Equal(ParkMood.Happy, ParkHabitats.MoodOf((byte)MoveType.Water, (byte)MoveType.Water, map).Mood);
        Assert.Equal(ParkMood.Sad, ParkHabitats.MoodOf((byte)MoveType.Fire, (byte)MoveType.Fire, map).Mood);
        Assert.Equal(ParkMood.Normal, ParkHabitats.MoodOf((byte)MoveType.Normal, (byte)MoveType.Normal, map).Mood);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Ocean_IsAlmostAllWater_WithThreeIsletsToWalkOn(int size)
    {
        // Choix de l'utilisateur : un océan, sans plage, avec trois îlots de sable où les nageurs sortent de l'eau (pas
        // bloqués), aucun dans la vue de départ. Rien d'autre : les Eau y sont heureux, les Feu malheureux (seuls les
        // Feu/Eau y viennent).
        var json = Json("ocean-1", size);
        var habitats = ParkHabitats.Read(json)!;
        var total = habitats.Area.W * habitats.Area.H;
        Assert.True(habitats.Counts[Habitat.Water] * 100 / total >= 95);
        Assert.Equal(total, habitats.Counts[Habitat.Water] + habitats.Counts[Habitat.Sand]);
        Assert.Equal(ParkMood.Happy, ParkHabitats.MoodOf((byte)MoveType.Water, (byte)MoveType.Water, habitats).Mood);
        Assert.Equal(ParkMood.Sad, ParkHabitats.MoodOf((byte)MoveType.Fire, (byte)MoveType.Fire, habitats).Mood);

        // Le sable des îlots est libre, et rien d'autre (leur bord d'écume est de l'eau) : le reste est de l'eau libre, ou
        // porte un rocher (bloqué ; la carte entière compte en plus sa bordure de récif)
        var map = Map("ocean-1", size);
        var free = FreeTiles(map);
        var (x0, y0, w, h) = Zone(map, "Parc");
        var land = Enumerable.Range(y0, h).SelectMany(y => Enumerable.Range(x0, w).Select(x => (x, y)))
            .Where(c => habitats.At(c.x, c.y) == Habitat.Sand).ToHashSet();
        Assert.True(land.SetEquals(free));
        Assert.Equal(size == 1, land.Count == 0);
        Assert.True(OpenWater("ocean-1", size).Count * 10 > total * (size == 4 ? 6 : 7));

        // Rochers animés (animations de Tiled, jouées par parc.js) ; pas de remous d'écume (retirés à la demande de
        // l'utilisateur)
        var animated = map.GetProperty("tilesets").EnumerateArray()
            .Where(t => t.TryGetProperty("tiles", out var tiles) && tiles.EnumerateArray().Any(tile => tile.TryGetProperty("animation", out _)))
            .Select(t => t.GetProperty("image").GetString()).Order().ToList();
        Assert.Equal(6, animated.Count);
        Assert.All(animated, image => Assert.Contains("_Water_Anim", image));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Volcano_IsAllVolcano_WithLavaForTheFireTypes(int size)
    {
        // Choix de l'utilisateur : tout le volcan est de type « Volcan » (Feu, Roche et Dragon heureux, Eau et Normal
        // malheureux), avec 14 à 29 % de lave libre dans la zone selon la taille (dessin de l'utilisateur ; le reste de la
        // carte entière est sa bordure)
        var json = Json("volcan-1", size);
        var habitats = ParkHabitats.Read(json)!;
        var total = habitats.Area.W * habitats.Area.H;
        Assert.True(habitats.Counts[Habitat.Volcano] * 100 / total >= 95);
        Assert.Equal(ParkMood.Happy, ParkHabitats.MoodOf((byte)MoveType.Fire, (byte)MoveType.Fire, habitats).Mood);
        Assert.Equal(ParkMood.Sad, ParkHabitats.MoodOf((byte)MoveType.Water, (byte)MoveType.Water, habitats).Mood);
        Assert.Equal(ParkMood.Sad, ParkHabitats.MoodOf((byte)MoveType.Normal, (byte)MoveType.Normal, habitats).Mood);

        // Lave libre, comme parc.js : bloquée, de type volcan, sans objet posé (« Décors », « Devant ») ; toute autre case
        // bloquée en porte un. Les bulles (calque « Bulles », animées) ne sont pas des objets : seulement sur la lave libre.
        var map = Map("volcan-1", size);
        int width = map.GetProperty("width").GetInt32();
        var (x0, y0, w, h) = Zone(map, "Parc");
        long[] Data(string layer) => Layer(map, layer).GetProperty("data").EnumerateArray().Select(t => t.GetInt64()).ToArray();
        long[] collisions = Data("Collisions"), decors = Data("Décors"), front = Data("Devant"), bubbles = Data("Bulles");
        var lava = Enumerable.Range(y0, h).SelectMany(y => Enumerable.Range(x0, w).Select(x => y * width + x))
            .Where(i => collisions[i] != 0 && decors[i] == 0 && front[i] == 0).ToList();
        Assert.All(lava, i => Assert.Equal(Habitat.Volcano, habitats.At(i % width, i / width)));
        Assert.InRange(lava.Count * 100 / total, 12, 30);
        var onLava = lava.ToHashSet();
        var bubbleTiles = Enumerable.Range(0, bubbles.Length).Where(i => bubbles[i] != 0).ToList();
        Assert.NotEmpty(bubbleTiles);
        Assert.All(bubbleTiles, i => Assert.Contains(i, onLava));

        // Cascades et bulles animées (animations de Tiled, jouées par parc.js)
        var animated = map.GetProperty("tilesets").EnumerateArray()
            .Where(t => t.TryGetProperty("tiles", out var tiles) && tiles.EnumerateArray().Any(tile => tile.TryGetProperty("animation", out _)))
            .Select(t => t.GetProperty("image").GetString()).ToList();
        Assert.Equal(["Volcano_Lavafall.png", "Volcano_lava_buble.png"], animated);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Volcano_EveryCascade_HasALakeAbove_AndLavaBelow(int size)
    {
        // Règle de l'utilisateur : une cascade n'est jamais seule, il lui faut quelque chose en amont (un lac, ici sur deux
        // lignes au moins) et en aval (la coulée). Cascade : 3 × 5 tuiles de Volcano_Lavafall (54 colonnes, première image).
        var map = Map("volcan-1", size);
        int width = map.GetProperty("width").GetInt32();
        var lavafall = map.GetProperty("tilesets").EnumerateArray().Single(t => t.GetProperty("image").GetString() == "Volcano_Lavafall.png");
        long first = lavafall.GetProperty("firstgid").GetInt64(), columns = lavafall.GetProperty("columns").GetInt64();
        long[] Data(string layer) => Layer(map, layer).GetProperty("data").EnumerateArray().Select(t => t.GetInt64()).ToArray();
        long[] collisions = Data("Collisions"), decors = Data("Décors"), front = Data("Devant");
        bool Lava(int x, int y) => collisions[y * width + x] != 0 && decors[y * width + x] == 0 && front[y * width + x] == 0;

        var cascades = 0;
        for (var i = 0; i < decors.Length; i++)
        {
            var id = decors[i] - first;
            if (id < 0 || id >= columns * 5) continue;
            int x = i % width, y = i / width;
            if (id / columns == 0)
            {
                cascades++;
                Assert.True(Lava(x, y - 1) && Lava(x, y - 2), $"cascade en ({x}, {y}) sans lac au-dessus");
            }
            if (id / columns == 4) Assert.True(Lava(x, y + 1), $"cascade en ({x}, {y - 4}) sans coulée en dessous");
        }
        Assert.True(cascades >= 3, $"{cascades / 3} cascade(s) seulement"); // celle de la vue de départ au moins
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Desert_IsMostlySand_ForTheGroundTypes(int size)
    {
        // Choix de l'utilisateur : un désert aride, un seul point d'eau (le lac du plateau, sa cascade et l'oasis), de
        // l'herbe seulement autour de l'eau (carte entière retouchée par lui, 2026-11-03). Plus de la moitié de sable à
        // toutes les tailles : les types Sol heureux, Feu malheureux d'après la feuille « Habitat ». La vue de départ a
        // l'oasis et le flanc du plateau (à peine plus de la moitié) ; la carte entière, sa bordure de cactus sur le sable.
        var json = Json("desert-1", size);
        var habitats = ParkHabitats.Read(json)!;
        var total = habitats.Area.W * habitats.Area.H;
        Assert.InRange(habitats.Counts[Habitat.Sand] * 100 / total, size switch { 1 => 50, 4 => 82, _ => 68 },
            size switch { 1 => 56, 4 => 90, _ => 80 });
        Assert.True(habitats.Counts[Habitat.Sand] * 2 > total);
        Assert.True(habitats.Counts[Habitat.Water] > 0 && habitats.Counts[Habitat.Vegetation] > 0 && habitats.Counts[Habitat.Cliff] > 0);
        Assert.Equal(ParkMood.Happy, ParkHabitats.MoodOf((byte)MoveType.Ground, (byte)MoveType.Ground, habitats).Mood);
        Assert.Equal(ParkMood.Sad, ParkHabitats.MoodOf((byte)MoveType.Fire, (byte)MoveType.Fire, habitats).Mood);
        Assert.Equal(ParkMood.Normal, ParkHabitats.MoodOf((byte)MoveType.Normal, (byte)MoveType.Normal, habitats).Mood);

        // Cascade et touffes d'herbe animées (animations de Tiled, jouées par parc.js)
        var animated = Map("desert-1", size).GetProperty("tilesets").EnumerateArray()
            .Where(t => t.TryGetProperty("tiles", out var tiles) && tiles.EnumerateArray().Any(tile => tile.TryGetProperty("animation", out _)))
            .Select(t => t.GetProperty("image").GetString()).Order().ToList();
        Assert.Equal(["Desert_Cliff_Waterfall_1.png", "Desert_Grass_1_Anim.png", "Desert_Grass_2_Anim.png", "Desert_Grass_3_Anim.png"],
            animated);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Desert_Cascade_HasALakeAbove_AndTheOasisBelow(int size)
    {
        // Règle de l'utilisateur : une cascade n'est jamais seule. Celle du désert sort du lac du plateau (le ruisseau se
        // resserre à une case sur les deux lignes au-dessus d'elle) et tombe dans l'oasis (une case de sortie). Cascade :
        // la chute de Desert_Cliff_Waterfall_1 (18 colonnes, lignes 2 et 3 de la planche, première image) sur les 3 lignes
        // du flanc ; eau : bloquée, de type eau, sans objet posé (là où les Eau nagent, comme parc.js).
        var json = Json("desert-1", size);
        var habitats = ParkHabitats.Read(json)!;
        var map = Map("desert-1", size);
        int width = map.GetProperty("width").GetInt32();
        var waterfall = map.GetProperty("tilesets").EnumerateArray().Single(t => t.GetProperty("image").GetString() == "Desert_Cliff_Waterfall_1.png");
        long first = waterfall.GetProperty("firstgid").GetInt64(), columns = waterfall.GetProperty("columns").GetInt64();
        long[] Data(string layer) => Layer(map, layer).GetProperty("data").EnumerateArray().Select(t => t.GetInt64()).ToArray();
        long[] collisions = Data("Collisions"), decors = Data("Décors"), front = Data("Devant");
        bool Fall(int x, int y) => decors[y * width + x] - first is var id && id >= 0 && id < columns * 6;
        bool Water(int x, int y) => collisions[y * width + x] != 0 && decors[y * width + x] == 0 && front[y * width + x] == 0
                                    && habitats.At(x, y) == Habitat.Water;

        var middles = 0;
        for (var i = 0; i < decors.Length; i++)
        {
            int x = i % width, y = i / width;
            // Haut de la chute, colonne du milieu
            if (!Fall(x, y) || Fall(x, y - 1) || (decors[i] - first) % 3 != 1) continue;
            middles++;
            Assert.True(Water(x, y - 1) && Water(x, y - 2), $"cascade en ({x}, {y}) sans lac au-dessus");
            var bottom = y;
            while (Fall(x, bottom + 1)) bottom++;
            Assert.Equal(3, bottom - y + 1);
            Assert.True(Water(x, bottom + 1), $"cascade en ({x}, {y}) sans eau en dessous");
        }
        Assert.Equal(1, middles); // une seule cascade, dans la vue de départ
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Cave_IsMostlyCave_WithALake_InTheDark(int size)
    {
        // Choix de l'utilisateur (2026-11-03) : une grotte naturelle, un lac souterrain qu'on découvre à la taille 2 et qui
        // grandit ensuite, des cristaux, des stalagmites, des torches, et la pénombre. Surtout de la cave : Sol, Roche,
        // Ténèbres, Spectre et Dragon heureux, Vol et Feu malheureux (feuille « Habitat »).
        var json = Json("grotte-1", size);
        var habitats = ParkHabitats.Read(json)!;
        var total = habitats.Area.W * habitats.Area.H;
        Assert.True(habitats.Counts[Habitat.Cave] * 100 / total >= 85);
        Assert.Equal(total, habitats.Counts[Habitat.Cave] + habitats.Counts[Habitat.Water]);
        Assert.Equal(ParkMood.Happy, ParkHabitats.MoodOf((byte)MoveType.Rock, (byte)MoveType.Rock, habitats).Mood);
        Assert.Equal(ParkMood.Sad, ParkHabitats.MoodOf((byte)MoveType.Flying, (byte)MoveType.Flying, habitats).Mood);
        Assert.Equal(ParkMood.Normal, ParkHabitats.MoodOf((byte)MoveType.Normal, (byte)MoveType.Normal, habitats).Mood);

        // Le lac : de l'eau libre à partir de la taille 2, où les nageurs entrent depuis les pierres du bord (comme
        // parc.js : de proche en proche par les côtés, depuis une case libre)
        var map = Map("grotte-1", size);
        var free = FreeTiles(map);
        var water = OpenWater("grotte-1", size);
        Assert.Equal(size == 1, water.Count == 0);
        var reached = new HashSet<(int x, int y)>(free);
        var queue = new Queue<(int x, int y)>(free);
        while (queue.TryDequeue(out var c))
            foreach (var n in new[] { (c.x + 1, c.y), (c.x - 1, c.y), (c.x, c.y + 1), (c.x, c.y - 1) })
                if (water.Contains(n) && reached.Add(n)) queue.Enqueue(n);
        Assert.True(water.All(reached.Contains), "eau du lac où les nageurs n'entrent pas");

        // La roche vue de dessus : des cases transparentes sur la couleur de fond de la carte ; la pénombre : des lumières
        // dans la carte (torches, cristaux), chacune avec un rayon et une couleur
        Assert.Equal("#391f21", map.GetProperty("backgroundcolor").GetString());
        var lights = map.GetProperty("layers").EnumerateArray()
            .Single(l => l.GetProperty("name").GetString() == "Lumières").GetProperty("objects").EnumerateArray().ToList();
        Assert.True(lights.Count >= 5);
        Assert.All(lights, light =>
        {
            var properties = light.GetProperty("properties").EnumerateArray().Select(p => p.GetProperty("name").GetString()).Order();
            Assert.Equal(["couleur", "rayon"], properties);
            Assert.InRange(light.GetProperty("x").GetDouble(), 0, map.GetProperty("width").GetInt32() * Tile);
            Assert.InRange(light.GetProperty("y").GetDouble(), 0, map.GetProperty("height").GetInt32() * Tile);
        });

        // Torches réparties régulièrement (demande de l'utilisateur, 2026-11-04 : au hasard, elles se collaient et laissaient
        // des coins sans lumière) : jamais à moins de 9 cases l'une de l'autre, aucune case libre à plus de 16 cases d'une torche
        var torches = lights.Where(light => light.GetProperty("properties").EnumerateArray()
                .Single(p => p.GetProperty("name").GetString() == "rayon").GetProperty("value").GetInt32() == 88)
            .Select(light => (X: light.GetProperty("x").GetDouble() / Tile, Y: light.GetProperty("y").GetDouble() / Tile)).ToList();
        Assert.True(torches.Count >= 3);
        double Distance(double ax, double ay, double bx, double by) => Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));
        var closest = torches.SelectMany((a, i) => torches.Take(i).Select(b => Distance(a.X, a.Y, b.X, b.Y))).Min();
        Assert.True(closest >= 9, $"deux torches à {closest:0.0} cases");
        double Nearest(double x, double y) => torches.Min(torch => Distance(x, y, torch.X, torch.Y));
        var farthest = free.Max(c => Nearest(c.x + 0.5, c.y + 0.5));
        Assert.True(farthest <= 16, $"une case libre à {farthest:0.0} cases de la torche la plus proche");

        // Torches animées (animations de Tiled, jouées par parc.js), rien d'autre
        var animated = map.GetProperty("tilesets").EnumerateArray()
            .Where(t => t.TryGetProperty("tiles", out var tiles) && tiles.EnumerateArray().Any(tile => tile.TryGetProperty("animation", out _)))
            .Select(t => t.GetProperty("image").GetString()).ToList();
        Assert.Equal(["Big_Torch_Anim.png"], animated);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Mountain_FlyersLandOnTheSummits(int size)
    {
        // Choix de l'utilisateur (2026-11-07) : tous les Pokémon vont à la montagne, seuls les volants montent sur ses massifs
        // aux flancs de pierre (deux étages, sans escalier) ; une rivière traverse la vallée. Les sommets (dessus des plateaux)
        // et les rebords et flancs sont bloqués pour la marche ; parc.js y laisse passer les volants (calques « Sommets » et
        // « Survol »), qui se posent sur les sommets, tous atteints en volant depuis la vallée.
        var map = Map("montagne-1", size);
        int width = map.GetProperty("width").GetInt32();
        var collisions = Layer(map, "Collisions").GetProperty("data").EnumerateArray().Select(t => t.GetInt64()).ToArray();
        var summits = Marked(map, "Sommets");
        var air = Marked(map, "Survol");
        Assert.NotEmpty(summits);
        Assert.NotEmpty(air);
        Assert.Empty(summits.Intersect(air));
        Assert.All(summits.Concat(air), c => Assert.NotEqual(0, collisions[c.y * width + c.x]));
        // Plus de sommets aux tailles 2 et 3 (la vue de départ a déjà son plateau) ; un massif n'est montré que tout entier,
        // et tous tiennent dans la zone de la taille 3 (l'anneau de la carte entière est trop mince entre la bordure et elle)
        var before = size > 1 ? Marked(Map("montagne-1", size - 1), "Sommets").Count : 0;
        Assert.True(size == 4 ? summits.Count == before : summits.Count > before);
        // Tous atteints en volant depuis la vallée (terre, sommets, rebords et flancs, rivière ; diagonale comme parc.js)
        HashSet<(int x, int y)> flight = [.. FreeTiles(map), .. summits, .. air, .. OpenWater("montagne-1", size)];
        var reached = new HashSet<(int x, int y)> { FreeTiles(map).Order().First() };
        var queue = new Queue<(int x, int y)>(reached);
        while (queue.TryDequeue(out var c))
            for (var dy = -1; dy <= 1; dy++)
                for (var dx = -1; dx <= 1; dx++)
                {
                    var n = (c.x + dx, c.y + dy);
                    if ((dx == 0 && dy == 0) || !flight.Contains(n)) continue;
                    if (dx != 0 && dy != 0 && (!flight.Contains((c.x + dx, c.y)) || !flight.Contains((c.x, c.y + dy)))) continue;
                    if (reached.Add(n)) queue.Enqueue(n);
                }
        Assert.All(summits, c => Assert.Contains(c, reached));

        // Des flancs (falaise) et surtout de l'herbe : les volants (qui aiment les deux) y sont heureux ; une rivière
        var habitats = ParkHabitats.Read(Json("montagne-1", size))!;
        Assert.True(habitats.Counts[Habitat.Cliff] > 0);
        Assert.True(habitats.Counts[Habitat.Vegetation] * 2 > habitats.Area.W * habitats.Area.H);
        Assert.NotEmpty(OpenWater("montagne-1", size));
        Assert.Equal(ParkMood.Happy, ParkHabitats.MoodOf((byte)MoveType.Flying, (byte)MoveType.Flying, habitats).Mood);
        Assert.Equal(ParkMood.Happy, ParkHabitats.MoodOf((byte)MoveType.Fire, (byte)MoveType.Flying, habitats).Mood);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Dungeon_IsAllDungeon_WithSewerBasins_InTheDark(int size)
    {
        // Choix de l'utilisateur (2026-11-07) : un souterrain délaissé, des salles et des couloirs, des bassins d'égout (un dans
        // la vue de départ, deux de plus à la taille 3), la pénombre et des torches. Tout est de type donjon, sauf l'eau des
        // bassins et leurs ponts : Spectre, Ténèbres, Poison, Psy, Acier et Dragon heureux, Normal, Vol, Eau, Plante et Fée
        // malheureux (feuille « Habitat »).
        var json = Json("donjon-1", size);
        var habitats = ParkHabitats.Read(json)!;
        var total = habitats.Area.W * habitats.Area.H;
        Assert.True(habitats.Counts[Habitat.Dungeon] * 100 / total >= 90);
        Assert.Equal(total, habitats.Counts[Habitat.Dungeon] + habitats.Counts[Habitat.Water] + habitats.Counts.GetValueOrDefault(Habitat.None));
        Assert.Equal(ParkMood.Happy, ParkHabitats.MoodOf((byte)MoveType.Ghost, (byte)MoveType.Ghost, habitats).Mood);
        Assert.Equal(ParkMood.Sad, ParkHabitats.MoodOf((byte)MoveType.Grass, (byte)MoveType.Grass, habitats).Mood);
        Assert.Equal(ParkMood.Normal, ParkHabitats.MoodOf((byte)MoveType.Fighting, (byte)MoveType.Fighting, habitats).Mood);

        // Les bassins : de l'eau libre où les nageurs entrent depuis le rebord (comme parc.js : de proche en proche par les
        // côtés, depuis une case libre)
        var map = Map("donjon-1", size);
        var free = FreeTiles(map);
        var water = OpenWater("donjon-1", size);
        Assert.Equal(size >= 3 ? 80 : 20, water.Count);
        var reached = new HashSet<(int x, int y)>(free);
        var queue = new Queue<(int x, int y)>(free);
        while (queue.TryDequeue(out var c))
            foreach (var n in new[] { (c.x + 1, c.y), (c.x - 1, c.y), (c.x, c.y + 1), (c.x, c.y - 1) })
                if (water.Contains(n) && reached.Add(n)) queue.Enqueue(n);
        Assert.True(water.All(reached.Contains), "eau d'un bassin où les nageurs n'entrent pas");

        // La roche : des cases transparentes sur la couleur de fond ; la pénombre : des lumières (torches, candélabres), chacune
        // avec un rayon et une couleur, et plus à chaque taille ; les torches et les pics du sol sont animés
        Assert.Equal("#16141f", map.GetProperty("backgroundcolor").GetString());
        var lights = Layer(map, "Lumières").GetProperty("objects").EnumerateArray().ToList();
        Assert.All(lights, light => Assert.Equal(["couleur", "rayon"],
            light.GetProperty("properties").EnumerateArray().Select(p => p.GetProperty("name").GetString()).Order()));
        if (size > 1) Assert.True(lights.Count > Layer(Map("donjon-1", size - 1), "Lumières").GetProperty("objects").GetArrayLength());
        var animated = map.GetProperty("tilesets").EnumerateArray()
            .Where(t => t.TryGetProperty("tiles", out var tiles) && tiles.EnumerateArray().Any(tile => tile.TryGetProperty("animation", out _)))
            .Select(t => t.GetProperty("image").GetString()).Order().ToList();
        Assert.Equal(["Big_Torch_Anim.png", "Floor_spikes_1.png"], animated);
    }
}
