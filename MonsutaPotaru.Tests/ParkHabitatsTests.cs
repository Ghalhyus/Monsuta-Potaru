using System.Text.Json;
using MonsutaPotaru.Services;
using PKHeX.Core;

namespace MonsutaPotaru.Tests;

/// <summary>
/// Types de cases du parc : la feuille « Habitat » recopiée dans ParkHabitats, et le champ « habitats » des cartes
/// (outils/carte-parc.py, d'après les planches de chaque case).
/// </summary>
public sealed class ParkHabitatsTests
{
    public static TheoryData<int> Sizes() => [.. Enumerable.Range(1, ParkService.MaxExpansions + 1)];

    private static string MapJson(int size) =>
        File.ReadAllText(Path.Combine(RealSavesTests.WebRoot(), "parc", $"parc-1-{size}.json"));

    [Fact]
    public void Sheet_CoversEveryType()
    {
        for (byte type = 0; type < 18; type++)
            Assert.True(ParkHabitats.Liked(type).Any() || ParkHabitats.Disliked(type).Any(), $"type {type} absent de la feuille");
        Assert.All(Enumerable.Range(0, 18), t => Assert.Equal(HabitatOpinion.Neutral, ParkHabitats.OpinionOf((byte)t, Habitat.None)));
    }

    [Fact]
    public void Sheet_MatchesTheSpreadsheet()
    {
        // Quelques lignes de la feuille, colonne par colonne
        Assert.Equal([Habitat.Water], ParkHabitats.Liked((byte)MoveType.Water));
        Assert.Equal([Habitat.Sand, Habitat.Cave, Habitat.Cliff, Habitat.Vegetation, Habitat.Dungeon, Habitat.Volcano],
            ParkHabitats.Disliked((byte)MoveType.Water)); // tout sauf l'eau (feuille modifiée le 2026-10-31)
        Assert.Equal([Habitat.Cave, Habitat.Volcano], ParkHabitats.Liked((byte)MoveType.Rock));
        Assert.Equal([Habitat.Sand, Habitat.Vegetation, Habitat.Water], ParkHabitats.Disliked((byte)MoveType.Rock));
        Assert.Equal([Habitat.Cave, Habitat.Cliff, Habitat.Dungeon, Habitat.Volcano], ParkHabitats.Liked((byte)MoveType.Dragon));
        Assert.Empty(ParkHabitats.Disliked((byte)MoveType.Dragon));
        Assert.Equal([Habitat.Sand, Habitat.Cave], ParkHabitats.Liked((byte)MoveType.Ground));
        Assert.Equal([Habitat.Sand, Habitat.Cave, Habitat.Dungeon, Habitat.Volcano], ParkHabitats.Disliked((byte)MoveType.Fairy));
        Assert.Equal(HabitatOpinion.Neutral, ParkHabitats.OpinionOf((byte)MoveType.Ice, Habitat.Water));
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public void Map_HasATypeForEveryTile(int size)
    {
        var json = MapJson(size);
        var map = ParkHabitats.Read(json);
        Assert.NotNull(map);
        var rows = JsonDocument.Parse(json).RootElement.GetProperty("habitats").EnumerateArray().Select(r => r.GetString()!).ToList();
        Assert.Equal(map.Height, rows.Count);
        Assert.All(rows, row => Assert.Matches($"^[0-7]{{{map.Width}}}$", row));
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public void Map_TypesFollowTheTiles(int size)
    {
        var json = MapJson(size);
        var map = ParkHabitats.Read(json)!;
        var root = JsonDocument.Parse(json).RootElement;
        var tilesets = root.GetProperty("tilesets").EnumerateArray()
            .Select(t => (First: t.GetProperty("firstgid").GetInt32(), Image: t.GetProperty("image").GetString()!))
            .OrderBy(t => t.First).ToList();
        int[] Layer(string name) => [.. root.GetProperty("layers").EnumerateArray()
            .Single(l => l.GetProperty("name").GetString() == name).GetProperty("data").EnumerateArray().Select(v => v.GetInt32())];
        var ground = Layer("Sol");
        var decor = Layer("Décors");
        for (var i = 0; i < ground.Length; i++)
        {
            var (first, image) = tilesets.Last(t => t.First <= ground[i]);
            var expected = image.StartsWith("Water_") ? Habitat.Water
                : image == "Path_Decoration.png" || (image == "Grass_Tiles_2.png" && ground[i] - first == 97) ? Habitat.None
                : image == "Grass_2_Middle.png" ? Habitat.Vegetation
                : (Habitat?)null;
            if (expected is { } habitat && decor[i] == 0) // objets posés dessus : ne changent pas le type
                Assert.True(habitat == map.At(i % map.Width, i / map.Width), $"case {i % map.Width}, {i / map.Width} : {map.At(i % map.Width, i / map.Width)} au lieu de {habitat}");
        }
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public void Counts_CoverTheParkZone(int size)
    {
        var map = ParkHabitats.Read(MapJson(size))!;
        Assert.Equal(map.Area.W * map.Area.H, map.Counts.Values.Sum());
        Assert.True(map.Counts[Habitat.Vegetation] > map.Area.W * map.Area.H / 2);
        Assert.True(map.Counts[Habitat.Water] > 0, "un plan d'eau dans chaque taille");
        Assert.Equal(Habitat.None, map.At(-1, 0));
    }

    /// <summary>Carte d'essai d'une ligne de 10 cases, sans zone : tout le parc (« 4444455555 » : 50 % végétation, 50 % eau).</summary>
    private static ParkHabitatMap TestMap(string row) =>
        ParkHabitats.Read($$"""{"width":{{row.Length}},"height":1,"layers":[],"habitats":["{{row}}"]}""")!;

    private static ParkMoodResult Mood(MoveType type1, MoveType type2, string row) =>
        ParkHabitats.MoodOf((byte)type1, (byte)type2, TestMap(row));

    private static ParkMoodResult Mood(MoveType type, string row) => Mood(type, type, row);

    [Fact]
    public void Mood_SadFromHalf_HappyAboveHalf()
    {
        // Eau : aime l'eau, n'aime aucun autre habitat
        Assert.Equal(ParkMood.Sad, Mood(MoveType.Water, "7777700000").Mood); // 50 % de volcan : malheureux
        Assert.Equal(ParkMood.Normal, Mood(MoveType.Water, "7777000000").Mood); // 40 %
        Assert.Equal(ParkMood.Normal, Mood(MoveType.Water, "5555500000").Mood); // 50 % d'eau : pas plus de la moitié
        var happy = Mood(MoveType.Water, "5555554444");
        Assert.Equal((ParkMood.Happy, Habitat.Water, 60), (happy.Mood, happy.Habitat, happy.Percent));
        Assert.Equal("Heureux : aime l'eau (60 % du parc).", happy.Message);
        Assert.Equal("Happy", happy.Portrait);
    }

    [Fact]
    public void Mood_SadWins_WhenBothApply()
    {
        // Sol : aime le sable, n'aime pas l'eau ; 50 % de chaque (pas heureux : pas plus de la moitié de sable, de toute façon)
        Assert.Equal(ParkMood.Sad, Mood(MoveType.Ground, "1111155555").Mood);
        // Acier : n'aime pas le sable, aime la végétation ; 50 % de sable, le reste en végétation
        var steel = Mood(MoveType.Steel, "1111144444");
        Assert.Equal((ParkMood.Sad, Habitat.Sand), (steel.Mood, steel.Habitat));
        Assert.Equal("Malheureux : n'aime pas le sable (50 % du parc).", steel.Message);
    }

    [Fact]
    public void Mood_TwoTypes_LikeWins()
    {
        // Roche n'aime pas la végétation, Plante l'aime : « aime » l'emporte (choix de l'utilisateur)
        Assert.Equal(ParkMood.Sad, Mood(MoveType.Rock, "4444444400").Mood);
        Assert.Equal(ParkMood.Happy, Mood(MoveType.Rock, MoveType.Grass, "4444444400").Mood);
        Assert.Equal(HabitatOpinion.Likes, ParkHabitats.OpinionOf((byte)MoveType.Grass, (byte)MoveType.Rock, Habitat.Vegetation));
        // Un type sans avis ne change rien : Glace + Eau aime l'eau, n'aime pas les autres habitats
        Assert.Equal(ParkMood.Happy, Mood(MoveType.Ice, MoveType.Water, "5555550000").Mood);
        Assert.Equal(ParkMood.Sad, Mood(MoveType.Ice, MoveType.Water, "7777700000").Mood);
    }

    [Fact]
    public void Mood_PathsCount_AndNoMap()
    {
        // Les cases sans type (chemins) comptent dans le parc : 6 de végétation sur 10 cases
        Assert.Equal(ParkMood.Happy, Mood(MoveType.Normal, "4444440000").Mood);
        Assert.Equal(ParkMood.Normal, Mood(MoveType.Normal, "4444400000").Mood);
        var none = ParkHabitats.MoodOf((byte)MoveType.Normal, (byte)MoveType.Normal, null);
        Assert.Equal((ParkMood.Normal, "Normal"), (none.Mood, none.Portrait));
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public void Mood_InTheRealParks(int size)
    {
        // Cartes actuelles : surtout de la végétation, un peu d'eau
        var map = ParkHabitats.Read(MapJson(size));
        Assert.Equal(ParkMood.Happy, ParkHabitats.MoodOf((byte)MoveType.Normal, (byte)MoveType.Normal, map).Mood);
        Assert.Equal(ParkMood.Sad, ParkHabitats.MoodOf((byte)MoveType.Rock, (byte)MoveType.Rock, map).Mood);
        Assert.Equal(ParkMood.Sad, ParkHabitats.MoodOf((byte)MoveType.Water, (byte)MoveType.Water, map).Mood); // n'aime pas la végétation
        Assert.Equal(ParkMood.Happy, ParkHabitats.MoodOf((byte)MoveType.Water, (byte)MoveType.Grass, map).Mood); // la Plante l'aime
        Assert.Equal(ParkMood.Normal, ParkHabitats.MoodOf((byte)MoveType.Ice, (byte)MoveType.Ice, map).Mood);
    }

    [Fact]
    public void Portrait_ClosestFolder_ThenNormal()
    {
        string[] folders = ["0025/0000/0001", "0025"]; // Pikachu chromatique, puis l'espèce
        string? Choose(string expression, params string[] files) => PokemonSprites.ChoosePortrait(folders, expression, files.Contains);
        Assert.Equal("images/donjon/portraits/0025/0000/0001/Happy.png", Choose("Happy", "0025/0000/0001/Happy.png", "0025/Happy.png"));
        // Dossier chromatique sans « Happy » : son « Normal » (les couleurs du Pokémon avant l'expression)
        Assert.Equal("images/donjon/portraits/0025/0000/0001/Normal.png", Choose("Happy", "0025/0000/0001/Normal.png", "0025/Happy.png"));
        // Dossier chromatique sans portrait : ceux de l'espèce
        Assert.Equal("images/donjon/portraits/0025/Sad.png", Choose("Sad", "0025/Normal.png", "0025/Sad.png"));
        Assert.Null(Choose("Sad"));
    }

    [Fact]
    public void Load_ReadsEverySize()
    {
        ParkHabitats.Load(RealSavesTests.WebRoot());
        Assert.All(Enumerable.Range(1, ParkService.MaxExpansions + 1), size => Assert.NotNull(ParkHabitats.Map(size)));
        Assert.Null(ParkHabitats.Map(ParkService.MaxExpansions + 2));
    }
}
