using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using MonsutaPotaru.Data;
using MonsutaPotaru.Services;

namespace MonsutaPotaru.Tests;

/// <summary>
/// Complétion All the Mons : tables du modpack (générées par outils/completion_allthemons.py), lecteur SNBT, fichiers
/// gardés d'un monde (succès, quêtes de l'équipe FTB, groupe), étapes lues dans le vrai monde Kantalia
/// (<c>Sauvegardes/all-the-mons.zip</c> : 62 quêtes et 34 succès faits), poids par étape, création et liste du service.
/// Pages : <see cref="CompletionPagesTests"/>.
/// </summary>
public sealed class AllTheMonsCompletionTests : IDisposable
{
    private const string Player = CobblemonTests.Player;
    private const string Party = "11111111-2222-3333-4444-555555555555";

    private readonly TestDatabase _db = new("sacha", "ondine");
    private readonly StoredSaveService _saves;
    private readonly CompletionService _completions;

    public AllTheMonsCompletionTests()
    {
        _saves = _db.StoredSaves();
        _completions = new CompletionService(_db.Factory, NullLogger<CompletionService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    private async Task<int> StoreWorld(string owner = "sacha", byte[]? zip = null)
    {
        var result = await _saves.AddAsync(owner, "Kantalia.zip", zip ?? CobblemonTests.World());
        Assert.True(result.Ok, result.Message);
        return (await _saves.ListAsync(owner)).Single(s => s.FileName == "Kantalia.zip").Id;
    }

    /// <summary>Le vrai monde, avec ces fichiers remplacés (null : retiré).</summary>
    private static byte[] WorldWith(params (string Name, byte[]? Data)[] changes)
    {
        var files = CobblemonTests.Unzip(CobblemonTests.RealZip());
        foreach (var (name, data) in changes)
        {
            if (data is null)
                files.Remove(name);
            else
                files[name] = data;
        }
        return CobblemonTests.Zip([.. files.Select(f => (f.Key, f.Value))]);
    }

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    private static CobblemonSave Read(byte[] zip) => CobblemonSave.TryRead(zip, out var error) ?? throw new InvalidOperationException(error);

    // ---------- Tables ----------

    [Fact]
    public void Tables_AreConsistent()
    {
        var quests = AllTheMonsCompletionData.Quests;
        var advancements = AllTheMonsCompletionData.Advancements;
        Assert.Equal(4656, quests.Length); // les 4 716 du livre, sans les 60 notes de licence (mode édition seulement)
        Assert.Equal(1735, advancements.Length);
        Assert.Equal(71, AllTheMonsCompletionData.Chapters.Length);
        Assert.Equal(50, AllTheMonsCompletionData.Tabs.Length);
        // Identifiants uniques, chapitres et onglets existants, un titre à chaque étape
        Assert.Equal(quests.Length, quests.Select(q => q.Id).Distinct().Count());
        Assert.Equal(advancements.Length, advancements.Select(a => a.Id).Distinct().Count());
        Assert.All(quests, q => Assert.Matches("^[0-9A-F]{16}$", q.Id));
        Assert.All(quests, q => Assert.InRange(q.Chapter, 0, AllTheMonsCompletionData.Chapters.Length - 1));
        Assert.All(advancements, a => Assert.InRange(a.Tab, 0, AllTheMonsCompletionData.Tabs.Length - 1));
        Assert.All(quests, q => Assert.False(string.IsNullOrWhiteSpace(q.Title), q.Id));
        Assert.All(advancements, a => Assert.False(string.IsNullOrWhiteSpace(a.Title), a.Id));
        // Les recettes (succès sans écran) ne comptent pas ; les codes de couleur de Minecraft sont retirés
        Assert.DoesNotContain(advancements, a => a.Id.Contains(":recipes/"));
        Assert.DoesNotContain(quests, q => q.Title.Contains('&') && q.Title.Length > 1 && "0123456789abcdefklmnor".Contains(q.Title[q.Title.IndexOf('&') + 1]));
        // Ordre : chapitres sans groupe, puis par groupe ; Minecraft d'abord dans les succès
        Assert.Equal("Bienvenue", AllTheMonsCompletionData.Chapters[0].Zone);
        Assert.Equal("Cobblemon — Evolution", AllTheMonsCompletionData.Chapters[10].Zone);
        Assert.Equal(["Minecraft", "Nether", "L'End", "Aventure", "Agriculture"], AllTheMonsCompletionData.Tabs.Take(5));
        Assert.Equal("minecraft:story/root", advancements[0].Id);
        Assert.Contains(advancements, a => a.Id == "minecraft:nether/all_effects" && a.Hidden);
        // Succès créés par le script kubejs du modpack (Pika Star)
        Assert.Contains(advancements, a => a.Id == "allthemons:kantonian_pika_star");
    }

    // ---------- SNBT ----------

    [Fact]
    public void Snbt_ReadsFtbFiles()
    {
        var value = Snbt.TryParse("""
            {
            	version: 1
            	uuid: "650fa532fb2b46d3b055b68008f9a895"
            	"ftbteams:display_name": "Ghalhyus \"le grand\""
            	completed: {
            		09E7D93E692CB782: 1794850253616L
            		0CA0B32D52395A39: 20L
            	}
            	scale: 0.5d, tiny: 1b, flag: true
            	list: [ ], ints: [I; 1, 2, -3]
            	names: ["a", "b"]
            }
            """) as Dictionary<string, object?>;
        Assert.NotNull(value);
        Assert.Equal(1L, value["version"]);
        Assert.Equal("Ghalhyus \"le grand\"", value["ftbteams:display_name"]);
        Assert.Equal(1794850253616L, ((Dictionary<string, object?>)value["completed"]!)["09E7D93E692CB782"]);
        Assert.Equal(0.5, value["scale"]);
        Assert.Equal(1L, value["tiny"]);
        Assert.Equal(true, value["flag"]);
        Assert.Empty((List<object?>)value["list"]!);
        Assert.Equal([1L, 2L, -3L], (List<object?>)value["ints"]!);
        Assert.Equal(["a", "b"], (List<object?>)value["names"]!);

        // Ce qui n'est pas du SNBT : null, jamais d'exception
        Assert.Null(Snbt.TryParse("{ a: "));
        Assert.Null(Snbt.TryParse("{ a 1 }"));
        Assert.Null(Snbt.TryParse("{ a: \"pas fermé }"));
        Assert.Null(Snbt.TryParse("{ a: 1 } en trop"));
        Assert.Null(Snbt.TryParse(new string('[', 1000) + new string(']', 1000))); // trop imbriqué
        Assert.Null(Snbt.TryParseCompound(Utf8("[1, 2]")));
    }

    // ---------- Fichiers du monde ----------

    [Fact]
    public void World_KeepsAdvancementsAndQuests()
    {
        var world = CobblemonTests.RealWorld();
        Assert.True(world.HasProgress);
        Assert.Null(world.PartyId);
        var kept = CobblemonTests.Unzip(world.Bundle()).Keys;
        Assert.Contains($"advancements/{Player}.json", kept);
        Assert.Contains($"ftbquests/{Player}.snbt", kept);
        // Rendu au joueur : le dossier pokemon seul, jamais ses succès ni ses quêtes
        Assert.All(CobblemonTests.Unzip(world.Write()).Keys, k => Assert.StartsWith("pokemon/", k));

        // Un monde envoyé sans ces dossiers se lit encore
        var old = Read(WorldWith(($"advancements/{Player}.json", null), ($"ftbquests/{Player}.snbt", null)));
        Assert.False(old.HasProgress);
        Assert.All(AllTheMonsCompletion.Steps(old), s => Assert.Equal(CompletionState.Missing, s.State));
    }

    [Fact]
    public void World_UsesThePartyQuests_WhenThePlayerIsAMember()
    {
        var partyQuests = Utf8("{ completed: { 3BC0A50886A3222B: 1L } }");
        string PartyFile(string rank) => $$"""{ id: "{{Party}}", type: "party", ranks: { {{Player}}: "{{rank}}" } }""";

        // Membre d'un groupe : la progression du groupe (les fichiers des autres équipes ne sont pas gardés)
        var member = Read(WorldWith(($"ftbteams/party/{Party}.snbt", Utf8(PartyFile("member"))), ($"ftbquests/{Party}.snbt", partyQuests),
            ("ftbquests/99999999-9999-9999-9999-999999999999.snbt", partyQuests)));
        Assert.Equal(Party, member.PartyId);
        Assert.Equal(["3BC0A50886A3222B"], AllTheMonsCompletion.DoneQuests(member.Quests));
        Assert.DoesNotContain("ftbquests/99999999-9999-9999-9999-999999999999.snbt", CobblemonTests.Unzip(member.Bundle()).Keys);

        // Seulement allié (ou invité) : sa propre progression
        var ally = Read(WorldWith(($"ftbteams/party/{Party}.snbt", Utf8(PartyFile("ally"))), ($"ftbquests/{Party}.snbt", partyQuests)));
        Assert.Null(ally.PartyId);
        Assert.Equal(62, AllTheMonsCompletion.DoneQuests(ally.Quests).Count(id => AllTheMonsCompletionData.Quests.Any(q => q.Id == id)));
        Assert.DoesNotContain($"ftbquests/{Party}.snbt", CobblemonTests.Unzip(ally.Bundle()).Keys);
    }

    [Fact]
    public void Folder_SendsAdvancementsAndQuests()
    {
        var payload = CobblemonTests.RealFolderPayload();
        var zip = CobblemonSave.ZipFromFolder(payload, out var error);
        Assert.Equal("", error);
        Assert.True(Read(zip!).HasProgress);

        var party = Utf8($$"""{ ranks: { {{Player}}: "owner" } }""");
        Assert.NotNull(CobblemonSave.ZipFromFolder(CobblemonTests.FolderPayload(
            ("level.dat", CobblemonTests.Unzip(CobblemonTests.RealZip())["level.dat"]), ($"ftbteams/party/{Party}.snbt", party)), out _));
        // Rien d'autre des dossiers des mods
        Assert.Null(CobblemonSave.ZipFromFolder(CobblemonTests.FolderPayload(("ftbteams/ftbteams.snbt", party)), out _));
        Assert.Null(CobblemonSave.ZipFromFolder(CobblemonTests.FolderPayload(($"ftbquests/{Player}.json", party)), out _));
        Assert.Null(CobblemonSave.ZipFromFolder(CobblemonTests.FolderPayload(($"advancements/{Player}.snbt", party)), out _));
    }

    // ---------- Étapes et pourcentages ----------

    [Fact]
    public void Steps_ReadTheRealWorld()
    {
        var steps = AllTheMonsCompletion.Steps(CobblemonTests.RealWorld());
        var progress = CompletionProgress.Of(steps, byStep: true);
        Assert.Equal([CompletionSection.ModpackQuests, CompletionSection.Advancements], progress.Sections.Select(s => s.Section));
        Assert.Equal([(62, 4656), (34, 1735)], progress.Sections.Select(s => (s.Done, s.Total)));
        // Chaque étape pèse autant : 96 / 6 391 (la moyenne des sections donnerait 1,6 %)
        Assert.Equal(15, progress.Tenths);
        Assert.Equal("1,5 %", progress.Percent);
        Assert.Equal((96, 6391), ((int)progress.AverageFraction().Numerator, (int)progress.AverageFraction().Denominator));
        Assert.Equal("toutes étapes confondues", progress.Basis);
        Assert.Equal("Chaque étape compte autant dans le total, quelle que soit sa section.", progress.Weighting);
        Assert.Equal(16, CompletionProgress.Of(steps).Tenths);

        // Une quête faite, avec son chapitre ; un succès de Minecraft en français
        Assert.Contains(steps, s => s is { Section: CompletionSection.ModpackQuests, Zone: "Bienvenue", Label: "Bienvenue dans All The Mods 10!", Done: true });
        Assert.Contains(steps, s => s is { Section: CompletionSection.Advancements, Zone: "Minecraft", Label: "L'âge de pierre", Done: true });
        // Cachés tant qu'ils ne sont pas faits ; un succès caché obtenu montre son titre
        Assert.Contains(steps, s => s is { Label: AllTheMonsCompletion.HiddenQuest, Done: false });
        Assert.Contains(steps, s => s is { Label: AllTheMonsCompletion.HiddenAdvancement, Done: false });
        Assert.DoesNotContain(steps, s => s.Label == "Comment en est-on arrivé là ?");
        Assert.Contains(steps, s => s is { Label: "YUNG's Better Dungeons", Done: true });
    }

    [Fact]
    public void Steps_IgnoreUnreadableFiles()
    {
        var world = Read(WorldWith(($"advancements/{Player}.json", Utf8("{ pas du json")), ($"ftbquests/{Player}.snbt", Utf8("{ completed: "))));
        Assert.True(world.HasProgress);
        Assert.DoesNotContain(AllTheMonsCompletion.Steps(world), s => s.Done);
        Assert.Empty(AllTheMonsCompletion.DoneAdvancements(Utf8("[1]")));
        Assert.Empty(AllTheMonsCompletion.DoneAdvancements(Utf8("""{"a:b": {"done": "true"}, "DataVersion": 3955}""")));
        Assert.Equal(["A:B"], AllTheMonsCompletion.DoneAdvancements(Utf8("""{"A:B": {"criteria": {}, "done": true}}""")));
        Assert.Empty(AllTheMonsCompletion.DoneQuests(null));
        Assert.Equal(["0CA0B32D52395A39"], AllTheMonsCompletion.DoneQuests(Utf8("{ completed: { 0ca0b32d52395a39: 1L } }"))); // majuscules
    }

    [Fact]
    public void Statistics_ShowTheModpackSections_OnlyWhenACompletionHasThem()
    {
        var game = CompletionProgress.Of([new(CompletionSection.Story, "Bourg Palette", null, "Pokédex", CompletionState.Done)]);
        Assert.DoesNotContain(StatisticsService.Summarize([game]).Sections, s => CompletionSections.IsModpack(s.Section));

        var world = CompletionProgress.Of(AllTheMonsCompletion.Steps(CobblemonTests.RealWorld()), byStep: true);
        var stats = StatisticsService.Summarize([game, world]);
        Assert.Equal((2, 1), (stats.General.Count, stats.General.Completed));
        Assert.Equal(507, stats.General.Tenths); // (100 % + 96 / 6 391) / 2
        Assert.Equal(13, stats.Sections.Single(s => s.Section == CompletionSection.ModpackQuests).Average.Tenths);
        Assert.Equal(19, stats.Sections.Single(s => s.Section == CompletionSection.Advancements).Average.Tenths);
    }

    // ---------- Service ----------

    [Fact]
    public async Task Service_CreatesTheCompletion_OnAStoredWorld()
    {
        var worldId = await StoreWorld();
        var candidates = await _completions.ListCandidatesAsync("sacha");
        Assert.Equal([LivingDexGame.AllTheMons], candidates.Single(c => c.SaveId == worldId).Games);
        Assert.Contains("All the Mons — Kantalia · Kantalia.zip", candidates.Single().Label);

        // Un monde pour All the Mons seulement ; pas un monde d'un autre compte
        Assert.Equal("Ce monde Minecraft n'est pas une partie de Pokémon Rouge.",
            (await _completions.CreateAsync("sacha", worldId, LivingDexGame.Red)).Message);
        Assert.Equal("Cette sauvegarde n'existe plus.", (await _completions.CreateAsync("ondine", worldId, LivingDexGame.AllTheMons)).Message);
        var created = await _completions.CreateAsync("sacha", worldId, LivingDexGame.AllTheMons);
        Assert.True(created.Ok, created.Message);
        Assert.Equal("Complétion de All the Mons créée.", created.Message);
        Assert.Empty(await _completions.ListCandidatesAsync("sacha"));
        Assert.Contains(LivingDexGame.AllTheMons, await _completions.TakenGamesAsync("sacha"));

        var summary = (await _completions.ListAsync("sacha")).Single();
        Assert.True(summary.Progress.ByStep);
        Assert.Equal("1,5 %", summary.Progress.Percent);
        var view = (await _completions.GetAsync("sacha", created.Id!.Value))!;
        Assert.Null(view.Notice);
        Assert.Equal(4656 + 1735, view.Steps.Count);
        Assert.Equal("Kantalia · Kantalia.zip", view.SaveLabel);
    }

    [Fact]
    public async Task Service_RefusesAGameSaveForAllTheMons()
    {
        var result = await _saves.AddAsync("sacha", "rouge.sav", File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Sauvegardes", "rouge.sav")));
        Assert.True(result.Ok, result.Message);
        var saveId = (await _saves.ListAsync("sacha")).Single().Id;
        Assert.Equal("Cette sauvegarde n'est pas un monde All the Mons.",
            (await _completions.CreateAsync("sacha", saveId, LivingDexGame.AllTheMons)).Message);
        Assert.DoesNotContain(LivingDexGame.AllTheMons, (await _completions.ListCandidatesAsync("sacha")).Single().Games);
    }

    [Fact]
    public async Task Service_WorldWithoutProgress_SaysToSendItAgain()
    {
        var worldId = await StoreWorld(zip: WorldWith(($"advancements/{Player}.json", null), ($"ftbquests/{Player}.snbt", null)));
        var created = await _completions.CreateAsync("sacha", worldId, LivingDexGame.AllTheMons);
        var view = (await _completions.GetAsync("sacha", created.Id!.Value))!;
        Assert.Equal(CompletionService.NoProgressNotice, view.Notice);
        Assert.Equal(0, view.Progress.Tenths);
        Assert.Equal(4656 + 1735, view.Steps.Count);

        // Renvoyé avec ses dossiers : la complétion suit
        var updated = await _saves.UpdateAsync("sacha", worldId, "Kantalia.zip", CobblemonTests.World());
        Assert.True(updated.Ok, updated.Message);
        view = (await _completions.GetAsync("sacha", created.Id!.Value))!;
        Assert.Null(view.Notice);
        Assert.Equal(15, view.Progress.Tenths);
    }
}
