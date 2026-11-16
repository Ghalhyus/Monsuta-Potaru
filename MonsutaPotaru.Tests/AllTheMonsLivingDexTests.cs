using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PKHeX.Core;
using MonsutaPotaru.Data;
using MonsutaPotaru.Services;

namespace MonsutaPotaru.Tests;

/// <summary>
/// Living dex All the Mons : un monde Minecraft stocké, compté dans son équipe et son PC ; les modes et les cases du
/// global, adaptés au modpack (Méga de Légendes Z-A, formes et espèces propres au modpack).
/// </summary>
public sealed class AllTheMonsLivingDexTests : IDisposable
{
    private readonly TestDatabase _db = new("sacha", "ondine");
    private readonly LivingDexService _dex;

    public AllTheMonsLivingDexTests() => _dex = new LivingDexService(_db.Factory, NullLogger<LivingDexService>.Instance);

    public void Dispose() => _db.Dispose();

    private static IReadOnlyList<GlobalSlot> Slots(LivingDexMode mode) => AllTheMonsLivingDex.Slots(mode);

    private static List<string> Names(LivingDexMode mode, ushort species) => Slots(mode).Where(s => s.Species == species).Select(s => s.Name).ToList();

    /// <summary>Le vrai monde (un Feunnec dans l'équipe) avec ces Pokémon dans la première boîte ; son numéro.</summary>
    private async Task<int> StoreWorld(string owner, params CobblemonPokemon[] pc)
    {
        var result = await _db.StoredSaves().AddAsync(owner, "Kantalia.zip", CobblemonTests.World(pc));
        Assert.True(result.Ok, result.Message);
        await using var db = _db.NewContext();
        return await db.StoredSaves.Where(s => s.OwnerId == owner).Select(s => s.Id).SingleAsync();
    }

    private async Task<int> CreateOn(params CobblemonPokemon[] pc)
    {
        var saveId = await StoreWorld("sacha", pc);
        var result = await _dex.CreateAsync("sacha", saveId, LivingDexGame.AllTheMons);
        Assert.True(result.Ok, result.Message);
        return result.Id!.Value;
    }

    /// <summary>Change la forme, et ajoute des traits (« Features ») : <c>("core_color", "blue")</c>.</summary>
    private static Action<NbtCompound> Form(string formId, params (string Feature, string Value)[] features) => nbt =>
    {
        nbt["FormId"] = CobblemonTests.Text(formId);
        var list = nbt.List("Features")!;
        foreach (var (feature, value) in features)
        {
            var compound = new NbtCompound();
            compound["cobblemon:feature_id"] = CobblemonTests.Text(feature);
            compound[feature] = CobblemonTests.Text(value);
            list.Items.Add(compound);
        }
    };

    private static Action<NbtCompound> Holding(string item) => nbt =>
    {
        var held = new NbtCompound();
        held["id"] = CobblemonTests.Text(item);
        held["count"] = new NbtValue(NbtType.Int, 1);
        nbt["HeldItem"] = held;
    };

    private static Action<NbtCompound> Female => nbt => nbt["Gender"] = CobblemonTests.Text("FEMALE");

    [Fact]
    public void Modes_AndTheirSlots()
    {
        var game = LivingDexGames.Get(LivingDexGame.AllTheMons);
        Assert.Same(AllTheMonsLivingDex.Game, game);
        Assert.Equal([LivingDexMode.National, LivingDexMode.Complete, LivingDexMode.Ultimate], LivingDexGames.Modes(game));
        Assert.False(LivingDexGames.HasShiny(game)); // pas encore de chromatique
        Assert.False(LivingDexGames.RegionalOnly(game));
        Assert.Equal(1027, Slots(LivingDexMode.National).Count); // 1 025 et les deux espèces du modpack
        Assert.Equal(1335, Slots(LivingDexMode.Complete).Count);
        Assert.Equal(1593, Slots(LivingDexMode.Ultimate).Count);
        foreach (var mode in GlobalLivingDex.Modes)
            Assert.Equal(Slots(mode).Count, Slots(mode).Select(s => s.Name).Distinct().Count()); // chaque case a son nom
        Assert.Equal(["Piglich", "Creepyon"], Slots(LivingDexMode.National).TakeLast(2).Select(s => s.Name));
    }

    [Fact]
    public void Complete_AddsTheModpackMegasAndForms()
    {
        var complete = Slots(LivingDexMode.Complete);
        Assert.Equal(["Raichu", "Raichu d'Alola", "Méga-Raichu X", "Méga-Raichu Y"], Names(LivingDexMode.Complete, 26));
        Assert.Equal(["Mistigrix", "Méga-Mistigrix ♂", "Méga-Mistigrix ♀"], Names(LivingDexMode.Complete, 678));
        Assert.Equal(["Méga-Nigirigon (Courbée)", "Méga-Nigirigon (Affalée)", "Méga-Nigirigon (Raide)"], Names(LivingDexMode.Complete, 978)
            .Where(n => n.StartsWith("Méga")).ToList());
        Assert.Contains("Méga-Magearna (Couleur du Passé)", Names(LivingDexMode.Complete, 801));
        Assert.Contains("Stari (Patrickyu)", Names(LivingDexMode.Complete, 120));
        Assert.Contains(complete, s => s.Species == 670 && s.FormKey == "Eternal"); // Floette Éternelle
        Assert.Equal("Eternal", complete.Single(s => s.Name == "Méga-Floette").BaseFormKey);
        Assert.Equal("F", complete.Single(s => s.Name == "Méga-Mistigrix ♀").BaseFormKey);
        // Ce que Cobblemon n'a pas : Pichu Troizépi
        Assert.DoesNotContain(complete, s => s.Species == 172 && s.FormKey == "Spiky");
        // Les Méga du global gardent leur case, une seule fois
        Assert.Equal(["Dracaufeu", "Méga-Dracaufeu X", "Méga-Dracaufeu Y", "Dracaufeu Gigamax"], Names(LivingDexMode.Complete, 6));
    }

    [Fact]
    public void Ultimate_AddsCobblemonTrimsAndPatterns_WithoutWhatCobblemonLacks()
    {
        var ultimate = Slots(LivingDexMode.Ultimate);
        Assert.Equal(16, Names(LivingDexMode.Ultimate, 676).Count); // Couafarel : sans coupe, 9 coupes des jeux, 6 de Cobblemon
        Assert.Contains("Couafarel (coupe Matcha)", Names(LivingDexMode.Ultimate, 676));
        Assert.Contains("Prismillon (motif Inferno)", Names(LivingDexMode.Ultimate, 666));
        Assert.DoesNotContain(ultimate, s => s.FormKey is "Starter" or "Spiky" or "Legend");
        Assert.DoesNotContain(ultimate, s => s.Name.EndsWith("Dominant"));
        Assert.Equal(["Papilord"], Names(LivingDexMode.Ultimate, 414)); // une seule forme dans Cobblemon
        Assert.Equal(["Lépidonille"], Names(LivingDexMode.Ultimate, 664));
        Assert.Contains(ultimate, s => s.Species == 710 && s.FormKey == "Large"); // la grande taille de Pitrouille reste
    }

    [Fact]
    public void Images_OfTheModpack_AreInTheRepository()
    {
        var webRoot = RealSavesTests.WebRoot(); // images versionnées, contrairement à wwwroot/images
        Assert.True(File.Exists(Path.Combine(webRoot, AllTheMonsLivingDex.Game.Cover)));
        var images = Slots(LivingDexMode.Ultimate).Concat(Slots(LivingDexMode.Complete)).Select(s => s.Image).OfType<string>().Distinct().ToList();
        Assert.Equal(12, images.Count);
        Assert.All(images, image => Assert.True(File.Exists(Path.Combine(webRoot, image)), image));
    }

    [Fact]
    public void FormKeys_FollowPKHeXNames()
    {
        Assert.Equal("Blue", CobblemonPokemon.FormKeyOf(550, "bluestriped"));
        Assert.Equal("Rock Star", CobblemonPokemon.FormKeyOf(25, "rockstar")); // Pikachu Cosplayeur : 6e génération
        Assert.Equal("Eternal", CobblemonPokemon.FormKeyOf(670, "eternal"));
        Assert.Equal("Alola", CobblemonPokemon.FormKeyOf(26, "alola"));
        Assert.Equal("10%-C", CobblemonPokemon.FormKeyOf(718, "10c"));
        Assert.Equal("matcha", CobblemonPokemon.FormKeyOf(676, "matcha")); // propre à Cobblemon
        Assert.Equal(GlobalLivingDex.FormKey(104, 0, EntityContext.Gen9), CobblemonPokemon.FormKeyOf(104, "alolabias")); // forme ordinaire
        Assert.True(CobblemonTests.Mon("cubone", 1, Form("alolabias")).IsBaseForm);
        // Zarbi par sa lettre, Météno par son noyau, Charmilly par sa décoration
        Assert.Equal("!", CobblemonTests.Mon("unown", 1, Form("", ("character", "!"))).FormKey);
        Assert.Equal("C-Blue", CobblemonTests.Mon("minior", 1, Form("meteor", ("core_color", "blue"))).FormKey);
        Assert.Equal(0, CobblemonTests.Mon("minior", 1, Form("meteor", ("core_color", "blue"))).Form); // l'image garde la carapace
        Assert.Equal((byte)3, CobblemonTests.Mon("alcremie", 1, Form("normal", ("decoration", "star"))).Sweet);
        Assert.Equal((ushort)9901, CobblemonTests.Mon("piglich", 1).DexNumber);
        Assert.Equal((ushort)0, CobblemonTests.Mon("piglich", 1).Species);
        Assert.Equal((byte)1, CobblemonTests.Mon("raichu", 1, Form("alola")).Form);
    }

    [Fact]
    public async Task Creation_OnlyOnAWorld_OncePerAccount()
    {
        var worldId = await StoreWorld("sacha");
        var gameId = (await _db.StoredSaves().AddAsync("sacha", "noir.sav", File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Sauvegardes", "noir.sav")))).Ok
            ? await GameSaveId() : 0;

        var candidates = await _dex.ListCandidatesAsync("sacha");
        Assert.Equal([LivingDexGame.AllTheMons], candidates.Single(c => c.SaveId == worldId).Games);
        Assert.DoesNotContain(candidates, c => c.SaveId == gameId && c.Games.Contains(LivingDexGame.AllTheMons));
        Assert.Empty(await _dex.ListCandidatesAsync("sacha", shiny: true) is var shinyCandidates
            ? shinyCandidates.Where(c => c.SaveId == worldId) : []);

        Assert.Equal("Cette sauvegarde n'est pas un monde All the Mons.", (await _dex.CreateAsync("sacha", gameId, LivingDexGame.AllTheMons)).Message);
        Assert.Equal("Cette sauvegarde n'est pas une partie de Pokémon Noire.", (await _dex.CreateAsync("sacha", worldId, LivingDexGame.Black)).Message);
        Assert.Equal("Pas encore de living dex chromatique All the Mons.", (await _dex.CreateAsync("sacha", worldId, LivingDexGame.AllTheMons, shiny: true)).Message);
        Assert.False((await _dex.CreateAsync("sacha", worldId, LivingDexGame.Global)).Ok);
        var created = await _dex.CreateAsync("sacha", worldId, LivingDexGame.AllTheMons);
        Assert.True(created.Ok, created.Message);
        Assert.Equal("Living dex All the Mons créé.", created.Message);
        Assert.Equal("Tu as déjà un living dex All the Mons : supprime-le pour en créer un autre.",
            (await _dex.CreateAsync("sacha", worldId, LivingDexGame.AllTheMons)).Message);
        Assert.DoesNotContain(await _dex.ListCandidatesAsync("sacha"), c => c.SaveId == worldId);
        Assert.Contains(LivingDexGame.AllTheMons, await _dex.TakenGamesAsync("sacha"));
        // Un autre joueur ne crée rien sur ce monde
        Assert.Equal("Cette sauvegarde n'existe plus.", (await _dex.CreateAsync("ondine", worldId, LivingDexGame.AllTheMons)).Message);

        async Task<int> GameSaveId()
        {
            await using var db = _db.NewContext();
            return await db.StoredSaves.Where(s => s.Generation != 0).Select(s => s.Id).SingleAsync();
        }
    }

    [Fact]
    public async Task Counts_TheTeamAndThePc_FormsMegasAndModpackSpecies()
    {
        var id = await CreateOn(
            CobblemonTests.Mon("raichu", 1, Form("alola")),
            CobblemonTests.Mon("raichu", 2, Holding("mega_showdown:raichunite_x")),
            CobblemonTests.Mon("meowstic", 3, nbt => { Female(nbt); Form("f")(nbt); Holding("mega_showdown:meowsticite")(nbt); }),
            CobblemonTests.Mon("staryu", 4, Form("patrickyu")),
            CobblemonTests.Mon("piglich", 5),
            CobblemonTests.Mon("cubone", 6, Form("alolabias")),
            CobblemonTests.Mon("minior", 7, Form("meteor", ("core_color", "blue"))),
            CobblemonTests.Mon("unown", 8, Form("", ("character", "?"))),
            CobblemonTests.Mon("vivillon", 9, Form("inferno")),
            CobblemonTests.Mon("eevee", 10, nbt => { nbt["GmaxFactor"] = new NbtValue(NbtType.Byte, (sbyte)1); nbt["Shiny"] = new NbtValue(NbtType.Byte, (sbyte)1); }));

        var national = (await _dex.GetAsync("sacha", id))!;
        Assert.Equal("Kantalia · Kantalia.zip", national.SaveLabel);
        // Feunnec (équipe), Raichu, Mistigrix, Stari, Piglich, Osselait, Météno, Zarbi, Prismillon, Évoli (chromatique compris)
        Assert.Equal(10, national.Owned);
        var piglich = national.Entries.Single(e => e.Name == "Piglich");
        Assert.True(piglich.Owned);
        Assert.Equal("minecraft/pokemon/piglich.png", piglich.Sprite);
        Assert.Equal("Équipe 1", national.Entries.Single(e => e.Name == "Feunnec").Location);
        Assert.Equal("Boîte 1, case 1", national.Entries.Single(e => e.Name == "Raichu").Location);

        var complete = (await _dex.GetAsync("sacha", id, LivingDexMode.Complete))!;
        Assert.Equal(LivingDexMode.Complete, complete.Mode);
        string[] owned = ["Prismillon", "Raichu d'Alola", "Méga-Raichu X", "Méga-Mistigrix ♀", "Stari (Patrickyu)", "Piglich", "Osselait",
            "Météno (noyau Bleu)", "Zarbi ?", "Évoli Gigamax", "Feunnec"];
        Assert.Equal(owned.Order(), complete.Entries.Where(e => e.Owned).Select(e => e.Name).Order());
        // Le Raichu à la gemme remplit la Méga, pas la case de Raichu ; Mistigrix ♀ est sa seule Méga possible
        Assert.False(complete.Entries.Single(e => e.Name == "Raichu").Owned);
        Assert.Equal("il faut un Mistigrix mâle de plus avec Mistigrixite", complete.Entries.Single(e => e.Name == "Méga-Mistigrix ♂").Note);
        Assert.Equal("avec Mistigrixite", complete.Entries.Single(e => e.Name == "Méga-Mistigrix ♀").Note);
        Assert.StartsWith("il faut un Floette (", complete.Entries.Single(e => e.Name == "Méga-Floette").Note); // une Floette Éternelle

        // Ultime : le motif Inferno de Prismillon a sa case, avec son image
        var ultimate = (await _dex.GetAsync("sacha", id, LivingDexMode.Ultimate))!;
        var inferno = ultimate.Entries.Single(e => e.Name == "Prismillon (motif Inferno)");
        Assert.True(inferno.Owned);
        Assert.Equal("minecraft/pokemon/666-inferno.png", inferno.Sprite);

        // Vue générale (tous les modes) et statistiques : le même comptage
        var summary = Assert.Single(await _dex.ListAsync("sacha", LivingDexMode.Ultimate));
        Assert.Equal((ultimate.Owned, 1593), (summary.Owned, summary.Total));
        Assert.Equal((national.Owned, 1027), ((await _dex.ListAsync("sacha", LivingDexMode.Regional)).Single().Owned, (await _dex.ListAsync("sacha", LivingDexMode.Regional)).Single().Total));
        var progress = Assert.Single(await _dex.ProgressAsync("sacha"));
        Assert.Equal([national.Owned, complete.Owned, ultimate.Owned], progress.Modes.Select(m => m.Owned));
    }

    [Fact]
    public async Task GlobalLivingDex_StillIgnoresMinecraft()
    {
        await CreateOn(CobblemonTests.Mon("raichu", 1));
        await _dex.CreateGlobalAsync("sacha");
        var global = (await _dex.ListAsync("sacha")).Single(d => GlobalLivingDex.IsGlobal(d.Game.Game));
        Assert.Equal(0, global.Owned);
    }
}
