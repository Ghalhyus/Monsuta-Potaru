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
        Assert.Equal([Habitat.Volcano], ParkHabitats.Disliked((byte)MoveType.Water));
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

    [Fact]
    public void Load_ReadsEverySize()
    {
        ParkHabitats.Load(RealSavesTests.WebRoot());
        Assert.All(Enumerable.Range(1, ParkService.MaxExpansions + 1), size => Assert.NotNull(ParkHabitats.Map(size)));
        Assert.Null(ParkHabitats.Map(ParkService.MaxExpansions + 2));
    }
}
