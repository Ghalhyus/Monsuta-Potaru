using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PKHeX.Core;
using MonsutaPotaru.Data;
using MonsutaPotaru.Services;

namespace MonsutaPotaru.Tests;

/// <summary>
/// Living dex chromatique d'un jeu : à côté du normal, sur sa propre sauvegarde, il ne compte que les chromatiques, en mode
/// Régional seulement (Let's Go : son Pokédex, en National), sans les espèces jamais chromatiques ; pas en 1re génération.
/// </summary>
public sealed class ShinyLivingDexTests : IDisposable
{
    private readonly TestDatabase _db = new("sacha", "ondine");
    private readonly StoredSaveService _saves;
    private readonly LivingDexService _dex;

    public ShinyLivingDexTests()
    {
        _saves = _db.StoredSaves();
        _dex = new LivingDexService(_db.Factory, NullLogger<LivingDexService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    private static byte[] RealSave(string file) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Sauvegardes", file));

    private async Task<int> Store(string owner, string file, byte[]? data = null)
    {
        var result = await _saves.AddAsync(owner, file, data ?? RealSave(file));
        Assert.True(result.Ok, result.Message);
        return (await _saves.ListAsync(owner)).Single(s => s.FileName == file).Id;
    }

    /// <summary>SoulSilver de l'utilisateur, avec un Germignon chromatique et un Héricendre normal en boîte.</summary>
    private static byte[] SoulSilverWithShiny()
    {
        Assert.True(SaveUtil.TryGetSaveFile(RealSave("soulsilver.sav"), out var sav, "soulsilver.sav"));
        var free = Enumerable.Range(0, sav!.SlotCount).Where(i => sav.GetBoxSlotAtIndex(i).Species == 0).ToList();
        foreach (var (species, shiny, slot) in new[] { ((ushort)152, true, 0), ((ushort)155, false, 1) })
        {
            var pk = new PK4
            {
                Species = species, Version = GameVersion.SS, Language = (int)LanguageID.French, OriginalTrainerName = "Ghal",
                TID16 = sav.TID16, SID16 = sav.SID16, PID = 0x15200000u + (uint)slot, MetLevel = 5, Move1 = 33,
            };
            pk.CurrentLevel = 5;
            pk.ClearNickname();
            if (shiny)
                pk.SetShiny();
            else if (pk.IsShiny)
                pk.PID ^= 0x10000;
            pk.RefreshChecksum();
            sav.SetBoxSlotAtIndex(pk, free[slot]);
        }
        return sav.Write().ToArray();
    }

    /// <summary>Espèces chromatiques de la sauvegarde (équipe et boîtes, sans les œufs), d'après la base.</summary>
    private List<ushort> ShinySpecies(int saveId)
    {
        using var db = _db.NewContext();
        return db.BankPokemon.Where(p => p.StoredSaveId == saveId && p.IsShiny && !p.IsEgg).Select(p => p.Species).Distinct().ToList();
    }

    [Fact]
    public void EachGame_FromTheSecondGeneration_HasAShinyLivingDex_RegionalOnly()
    {
        foreach (var game in LivingDexGames.All)
        {
            Assert.Equal(game.Generation >= 2, LivingDexGames.HasShiny(game));
            if (game.Generation == 1)
                continue;
            var shiny = LivingDexGames.Get(game.Game, shiny: true);
            Assert.True(shiny.Shiny);
            Assert.Equal($"{game.Name} chromatique", shiny.Name);
            Assert.Equal(game.Cover, shiny.Cover);
            // Le Régional ; Let's Go n'a que son Pokédex, affiché en National.
            Assert.Equal([LivingDexGames.IsLetsGo(game) ? LivingDexMode.National : LivingDexMode.Regional], LivingDexGames.Modes(shiny));
            var slot = LivingDexGames.Slots(shiny, LivingDexGames.Modes(shiny)[0])[0];
            Assert.Contains("chromatique/", LivingDexGames.Sprite(shiny, slot)); // sprites du jeu ou de la collection
            Assert.Contains("chromatique/", LivingDexGames.Fallback(shiny, slot));
            Assert.DoesNotContain("chromatique/", LivingDexGames.Sprite(game, slot));
        }
        Assert.False(LivingDexGames.HasShiny(GlobalLivingDex.ShinyGame)); // un jeu à part
        Assert.True(GlobalLivingDex.ShinyGame.Shiny);
        Assert.Equal("images/livingdex/platine/chromatique/387.png",
            LivingDexGames.Sprite(LivingDexGames.Get(LivingDexGame.Platinum, true), new LivingDexSlot(387, 0)));
        Assert.Throws<KeyNotFoundException>(() => LivingDexGames.Get(LivingDexGame.Red, shiny: true));
    }

    [Fact]
    public void ShinyLocked_OnlyInAShinyLivingDexOfAGame()
    {
        var victini = new LivingDexSlot(494, 0);
        Assert.True(LivingDexGames.ShinyLocked(LivingDexGames.Get(LivingDexGame.Black, true), victini));
        Assert.False(LivingDexGames.ShinyLocked(LivingDexGames.Get(LivingDexGame.Black), victini));
        Assert.False(LivingDexGames.ShinyLocked(GlobalLivingDex.ShinyGame, victini)); // le global a sa propre liste
        Assert.False(LivingDexGames.ShinyLocked(LivingDexGames.Get(LivingDexGame.Black, true), new LivingDexSlot(495, 0)));
    }

    [Fact]
    public async Task Create_NextToTheNormalOne_OnItsOwnSave()
    {
        var red = await Store("sacha", "rouge.sav");
        var black = await Store("sacha", "noir.sav");
        var soulSilver = await Store("sacha", "soulsilver.sav");

        // Pas de chromatiques en 1re génération : Rouge n'est pas proposé.
        Assert.Equal([black, soulSilver], (await _dex.ListCandidatesAsync("sacha", shiny: true)).Select(c => c.SaveId));
        Assert.Equal("Pokémon Rouge n'a pas de chromatiques : pas de living dex chromatique.",
            (await _dex.CreateAsync("sacha", red, LivingDexGame.Red, shiny: true)).Message);
        Assert.Equal("Cette sauvegarde n'est pas une partie de Pokémon Noire.",
            (await _dex.CreateAsync("sacha", soulSilver, LivingDexGame.Black, shiny: true)).Message);

        var normal = await _dex.CreateAsync("sacha", black, LivingDexGame.Black);
        Assert.True(normal.Ok, normal.Message);
        // Une sauvegarde chacun : celle du normal ne sert plus.
        Assert.Equal("Cette sauvegarde a déjà un living dex.", (await _dex.CreateAsync("sacha", black, LivingDexGame.Black, shiny: true)).Message);
        Assert.Equal([soulSilver], (await _dex.ListCandidatesAsync("sacha", shiny: true)).Select(c => c.SaveId));

        var shiny = await _dex.CreateAsync("sacha", soulSilver, LivingDexGame.SoulSilver, shiny: true);
        Assert.True(shiny.Ok, shiny.Message);
        Assert.Equal("Living dex Pokémon Argent SoulSilver chromatique créé.", shiny.Message);
        Assert.Equal([LivingDexGame.SoulSilver], await _dex.TakenGamesAsync("sacha", shiny: true));
        Assert.Equal([LivingDexGame.Black], await _dex.TakenGamesAsync("sacha"));

        // Une partie de Platine : proposée en normal et en chromatique ; un chromatique Platine créé entre-temps (autre
        // onglet) : refusé, mais le normal reste possible.
        var platinum = await Store("sacha", "platine.sav", SaveSessionTests.CreatePlatinumSave());
        Assert.Equal([LivingDexGame.Platinum], Assert.Single(await _dex.ListCandidatesAsync("sacha", shiny: true)).Games);
        using (var db = _db.NewContext())
        {
            db.LivingDexes.Add(new LivingDex { OwnerId = "sacha", Game = LivingDexGame.Platinum, Shiny = true, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        Assert.Empty(await _dex.ListCandidatesAsync("sacha", shiny: true));
        Assert.Equal([LivingDexGame.Platinum], (await _dex.ListCandidatesAsync("sacha")).Single(c => c.SaveId == platinum).Games);
        Assert.Equal("Tu as déjà un living dex Pokémon Platine chromatique : supprime-le pour en créer un autre.",
            (await _dex.CreateAsync("sacha", platinum, LivingDexGame.Platinum, shiny: true)).Message);
        Assert.True((await _dex.CreateAsync("sacha", platinum, LivingDexGame.Platinum)).Ok);

        Assert.Equal(["Pokémon Platine", "Pokémon Platine chromatique", "Pokémon Argent SoulSilver chromatique", "Pokémon Noire"],
            (await _dex.ListAsync("sacha")).Select(d => d.Game.Name));
        Assert.Contains("Pokémon Argent SoulSilver chromatique", (await _dex.NamesBySaveAsync("sacha")).Values);
        Assert.False(await _dex.DeleteAsync("ondine", shiny.Id!.Value));
        Assert.True(await _dex.DeleteAsync("sacha", shiny.Id!.Value));
        Assert.Equal([LivingDexGame.Platinum], await _dex.TakenGamesAsync("sacha", shiny: true));
    }

    [Fact]
    public async Task Counts_OnlyTheShinies_OfItsSave()
    {
        var save = await Store("sacha", "soulsilver.sav", SoulSilverWithShiny());
        var id = (await _dex.CreateAsync("sacha", save, LivingDexGame.SoulSilver, shiny: true)).Id!.Value;

        var view = (await _dex.GetAsync("sacha", id, LivingDexMode.Complete))!; // seul le Régional : Johto
        Assert.Equal([LivingDexMode.Regional], view.Modes);
        Assert.Equal(LivingDexMode.Regional, view.Mode);
        Assert.Equal(256, view.Entries.Count); // aucune espèce jamais chromatique à Johto
        var shinies = ShinySpecies(save);
        Assert.Contains((ushort)152, shinies);
        Assert.Equal(shinies.Count(s => LivingDexGames.Get(LivingDexGame.SoulSilver).Regional.Contains(s)), view.Owned);
        var chikorita = view.Entries[0];
        Assert.Equal((1, (ushort)152, true), (chikorita.Number, chikorita.Species, chikorita.Owned));
        Assert.Equal("images/livingdex/heartgold-soulsilver/chromatique/152.png", chikorita.Sprite);
        Assert.Equal(shinies.Contains(155), view.Entries.Single(e => e.Species == 155).Owned); // le Héricendre normal ne compte pas

        // Le chromatique supprimé, la sauvegarde reçoit le normal : il compte tout le monde, chromatiques compris.
        Assert.True(await _dex.DeleteAsync("sacha", id));
        var normal = (await _dex.GetAsync("sacha", (await _dex.CreateAsync("sacha", save, LivingDexGame.SoulSilver)).Id!.Value, LivingDexMode.Regional))!;
        Assert.True(normal.Entries[0].Owned);
        Assert.True(normal.Owned > view.Owned);
    }

    [Fact]
    public async Task Unova_WithoutVictini_KeepsTheRegionalNumbers()
    {
        var save = await Store("sacha", "noir.sav");
        var id = (await _dex.CreateAsync("sacha", save, LivingDexGame.Black, shiny: true)).Id!.Value;
        var view = (await _dex.GetAsync("sacha", id))!;
        Assert.Equal(155, view.Entries.Count); // 156 sans Victini, jamais chromatique
        Assert.DoesNotContain(view.Entries, e => e.Species == 494);
        Assert.Equal((1, (ushort)495), (view.Entries[0].Number, view.Entries[0].Species)); // Vipélierre garde son n° 001
        Assert.Equal("images/livingdex/noire-blanche/chromatique/495.png", view.Entries[0].Sprite);
        Assert.Equal(ShinySpecies(save).Count(s => LivingDexGames.Get(LivingDexGame.Black).Regional.Contains(s)), view.Owned);
    }

    [Fact]
    public async Task List_CountsTheShinyOneInItsOnlyMode()
    {
        var black = await Store("sacha", "noir.sav");
        var soulSilver = await Store("sacha", "soulsilver.sav");
        await _dex.CreateGlobalAsync("sacha", shiny: true);
        await _dex.CreateAsync("sacha", black, LivingDexGame.Black);
        await _dex.CreateAsync("sacha", soulSilver, LivingDexGame.SoulSilver, shiny: true);

        foreach (var mode in new[] { LivingDexMode.Regional, LivingDexMode.National, LivingDexMode.Complete })
        {
            var cards = await _dex.ListAsync("sacha", mode);
            var shiny = cards.Single(c => c.Game.Game == LivingDexGame.SoulSilver);
            Assert.Equal((LivingDexMode.Regional, 256), (shiny.Mode, shiny.Total));
            Assert.True(shiny.Game.Shiny);
        }
        // Ultime : seulement le global chromatique.
        Assert.Equal([LivingDexGame.GlobalShiny], (await _dex.ListAsync("sacha", LivingDexMode.Ultimate)).Select(c => c.Game.Game));
    }

    [Fact]
    public void Filter_AndSort_ShinyAfterItsNormal()
    {
        LivingDexSummary Card(LivingDexGame game, bool shiny, int owned) =>
            new(0, LivingDexGames.Get(game, shiny), "", owned, 100);
        var cards = new[]
        {
            Card(LivingDexGame.Platinum, true, 50), Card(LivingDexGame.Black, false, 10), Card(LivingDexGame.Platinum, false, 20),
            new LivingDexSummary(0, GlobalLivingDex.ShinyGame, "", 0, 100), new LivingDexSummary(0, GlobalLivingDex.Game, "", 0, 100),
        };
        string Names(IEnumerable<LivingDexSummary> list) => string.Join(", ", list.Select(c => c.Game.Name));

        Assert.Equal("Living dex global, Living dex global chromatique, Pokémon Platine, Pokémon Platine chromatique, Pokémon Noire",
            Names(LivingDexGames.Sort(cards, LivingDexSort.Generation, descending: false)));
        Assert.Equal("Living dex global, Living dex global chromatique, Pokémon Noire, Pokémon Platine, Pokémon Platine chromatique",
            Names(LivingDexGames.Sort(cards, LivingDexSort.Generation, descending: true)));
        Assert.Equal("Living dex global, Living dex global chromatique, Pokémon Platine chromatique, Pokémon Platine, Pokémon Noire",
            Names(LivingDexGames.Sort(cards, LivingDexSort.Completion, descending: true)));

        Assert.Equal(["Living dex global chromatique", "Pokémon Platine chromatique"],
            cards.Where(c => LivingDexGames.Matches(c.Game, LivingDexKind.Shiny)).Select(c => c.Game.Name).Order());
        Assert.Equal(3, cards.Count(c => LivingDexGames.Matches(c.Game, LivingDexKind.Normal)));
        Assert.Equal(5, cards.Count(c => LivingDexGames.Matches(c.Game, LivingDexKind.All)));
        foreach (var kind in Enum.GetValues<LivingDexKind>())
            Assert.Equal(kind, LivingDexGames.KindFromSlug(LivingDexGames.KindSlug(kind)));
        Assert.Equal(LivingDexKind.All, LivingDexGames.KindFromSlug("n'importe quoi"));
        Assert.Equal("chromatiques", LivingDexGames.KindSlug(LivingDexKind.Shiny));
    }

    [Fact]
    public async Task UniqueIndex_AllowsANormalAndAShinyPerGame()
    {
        using var db = _db.NewContext();
        db.LivingDexes.Add(new LivingDex { OwnerId = "sacha", Game = LivingDexGame.Platinum, CreatedAt = DateTime.UtcNow });
        db.LivingDexes.Add(new LivingDex { OwnerId = "sacha", Game = LivingDexGame.Platinum, Shiny = true, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        db.LivingDexes.Add(new LivingDex { OwnerId = "sacha", Game = LivingDexGame.Platinum, Shiny = true, CreatedAt = DateTime.UtcNow });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
