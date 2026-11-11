using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PKHeX.Core;
using MonsutaPotaru.Data;
using MonsutaPotaru.Services;

namespace MonsutaPotaru.Tests;

/// <summary>
/// Complétion (« Défis » → « Complétion (100 %) ») : tables de Rouge / Bleu et de Jaune (générées d'après pokered et
/// pokeyellow), lecture des drapeaux d'une sauvegarde, pourcentage par section et global, création et suppression. Vraies
/// sauvegardes de Rouge et de Bleu (MonsutaPotaru.Tests/Sauvegardes) ; Jaune et les cas limites en mémoire (une sauvegarde
/// de 1re génération créée vide n'est pas relue par PKHeX une fois écrite).
/// </summary>
public sealed class CompletionServiceTests : IDisposable
{
    private readonly TestDatabase _db = new("sacha", "ondine", "pierre");
    private readonly StoredSaveService _saves;
    private readonly CompletionService _completions;

    public CompletionServiceTests()
    {
        _saves = _db.StoredSaves();
        _completions = new CompletionService(_db.Factory, NullLogger<CompletionService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    private static byte[] RealSave(string file) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Sauvegardes", file));

    private async Task<int> Store(string owner, string file, byte[]? data = null)
    {
        var result = await _saves.AddAsync(owner, file, data ?? RealSave(file));
        Assert.True(result.Ok, result.Message);
        return (await _saves.ListAsync(owner)).Single(s => s.FileName == file).Id;
    }

    /// <summary>Drapeau d'une étape à condition simple (« E:n ») : son numéro.</summary>
    private static int EventOf(Gen1Row row)
    {
        Assert.StartsWith("E:", row.Done);
        return int.Parse(row.Done[2..]);
    }

    private static Gen1Row Story(Gen1Row[] rows, string label) => rows.Single(r => r.Label == label);

    [Fact]
    public void Tables_HaveFiveSections_AndConditionsInsideTheSave()
    {
        var sav = new SAV1(LanguageID.French, GameVersion.RB);
        foreach (var (rows, yellow) in new[] { (Gen1CompletionData.RedBlue, false), (Gen1CompletionData.Yellow, true) })
        {
            // Pokédex ajouté à part ; les autres sections viennent des tables
            Assert.DoesNotContain(rows, r => r.Section == CompletionSection.Pokedex);
            Assert.Equal(yellow ? 18 : 17, rows.Count(r => r.Section == CompletionSection.Story));
            Assert.Equal(yellow ? 323 : 324, rows.Count(r => r.Section == CompletionSection.Trainers));
            Assert.Equal(yellow ? 217 : 212, rows.Count(r => r.Section == CompletionSection.Items));
            Assert.Equal(yellow ? 28 : 27, rows.Count(r => r.Section == CompletionSection.Encounters));
            // Chaque condition se lit dans une sauvegarde (drapeaux, objets à interrupteur, octets des données principales)
            foreach (var atom in rows.SelectMany(r => new[] { r.Done, r.Missed }).OfType<string>().SelectMany(c => c.Split('|', '+')))
            {
                var parts = atom.Split(':');
                switch (parts[0])
                {
                    case "E": Assert.InRange(int.Parse(parts[1]), 0, sav.EventFlagCount - 1); break;
                    case "T": Assert.InRange(int.Parse(parts[1]), 0, sav.EventSpawnFlags.Length - 1); break;
                    case "R": Assert.InRange(int.Parse(parts[1]), 0x299C, 0x29F2); Assert.InRange(int.Parse(parts[2]), 0, 7); break;
                    case "B": Assert.InRange(int.Parse(parts[1]), 0, 7); break;
                    default: Assert.Contains(parts[0], new[] { "F", "P" }); break;
                }
            }
            // Jaune seulement : l'amitié de Pikachu, Jessie et James, les trois Pokémon de départ offerts
            Assert.Equal(yellow, rows.Any(r => r.Label == "Amitié de Pikachu au maximum"));
            Assert.Equal(yellow ? 4 : 0, rows.Count(r => r.Label == "Jessie et James"));
            Assert.Equal(yellow, rows.Any(r => r.Species == 1 && r.Section == CompletionSection.Encounters));
            // Les choix : un seul des deux suffit
            Assert.Contains(rows, r => r.Label == "Fossile Dôme ou Nautile" && r.Done.Contains('|'));
            Assert.Contains(rows, r => r.Label == "Kicklee ou Tygnon" && r.Done.Contains('|'));
        }
    }

    [Fact]
    public void Labels_ReadyToShow()
    {
        Assert.Equal("CT01 Ultimapoing", Gen1Completion.ItemName(201));
        Assert.Equal("CT50 Clonage", Gen1Completion.ItemName(250));
        Assert.Equal("CS01 Coupe", Gen1Completion.ItemName(196));
        Assert.Equal("CS05 Flash", Gen1Completion.ItemName(200));
        Assert.Equal("Canne", Gen1Completion.ItemName(76)); // « Acienne Canne » chez PKHeX
        Assert.Equal("Pierre Lune", Gen1Completion.ItemName(10));

        Assert.Equal("Rattata, Abo (niv. 11)", Gen1Completion.Label(new(CompletionSection.Trainers, "Route 3", null, "E:1", Team: "19:11,23:11")));
        Assert.Equal("Rattata niv. 11, Abo niv. 14", Gen1Completion.Label(new(CompletionSection.Trainers, "Route 3", null, "E:1", Team: "19:11,23:14")));
        Assert.Equal("20 jetons", Gen1Completion.Label(new(CompletionSection.Items, "Céladopole", null, "E:1", Coins: 20)));
        Assert.Equal("Canarticho (contre Piafabec)", Gen1Completion.Label(new(CompletionSection.Encounters, "Carmin sur Mer", null, "R:1:0", Species: 83, Give: 21)));
        Assert.Equal("Ronflex", Gen1Completion.Label(new(CompletionSection.Encounters, "Route 12", null, "E:1", Species: 143)));
        Assert.Equal("Rival", Gen1Completion.Label(new(CompletionSection.Trainers, "Azuria", null, "E:1", Label: "Rival")));
    }

    [Fact]
    public void Progress_EachSectionWeighsTheSame()
    {
        static CompletionStep Step(CompletionSection section, bool done) =>
            new(section, "Lieu", null, "Étape", done ? CompletionState.Done : CompletionState.Missing);

        // Le Pokédex rempli (151 étapes) ne pèse qu'un cinquième, autant que l'histoire faite à moitié (2 étapes sur 4)
        List<CompletionStep> steps =
        [
            .. Enumerable.Range(0, 151).Select(_ => Step(CompletionSection.Pokedex, true)),
            Step(CompletionSection.Story, true), Step(CompletionSection.Story, true), Step(CompletionSection.Story, false),
            new(CompletionSection.Story, "Lieu", null, "Ratée", CompletionState.Missed),
            Step(CompletionSection.Trainers, false), Step(CompletionSection.Items, false), Step(CompletionSection.Encounters, false),
        ];
        var progress = CompletionProgress.Of(steps);
        Assert.Equal(300, progress.Tenths); // (1 + 0,5) / 5
        Assert.Equal("30,0 %", progress.Percent);
        Assert.Equal(1, progress.Sections.Single(s => s.Section == CompletionSection.Story).Missed);
        Assert.False(progress.Complete);

        // Tout fait : 100 % pile ; une étape de moins : 99,9 % (arrondi vers le bas)
        var all = CompletionSections.All.SelectMany(s => Enumerable.Range(0, 3).Select(_ => Step(s, true))).ToList();
        Assert.Equal(("100,0 %", true), (CompletionProgress.Of(all).Percent, CompletionProgress.Of(all).Complete));
        all[0] = Step(CompletionSection.Story, false);
        Assert.Equal("93,3 %", CompletionProgress.Of(all).Percent);
        Assert.Equal(0, CompletionProgress.Of([]).Tenths);

        Assert.Equal("Rencontres uniques", CompletionSections.Name(CompletionSection.Encounters));
        Assert.Equal(CompletionSection.Trainers, CompletionSections.FromSlug("DRESSEURS"));
        Assert.Equal(CompletionSection.Story, CompletionSections.FromSlug("inconnue"));
        Assert.Equal(["histoire", "pokedex", "dresseurs", "objets", "rencontres"], CompletionSections.All.Select(CompletionSections.Slug));
    }

    [Fact]
    public void Games_RedBlueYellow_InternationalOnly()
    {
        Assert.Equal([LivingDexGame.Red, LivingDexGame.Blue], Gen1Completion.For(new SAV1(LanguageID.French, GameVersion.RB)));
        Assert.Equal([LivingDexGame.Yellow], Gen1Completion.For(new SAV1(LanguageID.French, GameVersion.YW)));
        Assert.Empty(Gen1Completion.For(new SAV1(LanguageID.Japanese, GameVersion.RB))); // autres adresses
        Assert.Empty(Gen1Completion.For(new SAV2(LanguageID.French, GameVersion.GS)));
    }

    [Fact]
    public void Yellow_InMemory_PikachuLeagueBadgesAndPokedex()
    {
        var sav = new SAV1(LanguageID.French, GameVersion.YW);
        var empty = Gen1Completion.Steps(sav, LivingDexGame.Yellow);
        Assert.Equal(18 + 151 + 323 + 217 + 28, empty.Count);
        Assert.Equal(0, CompletionProgress.Of(empty).Tenths);
        Assert.Equal(CompletionSection.Story, empty[0].Section);
        Assert.Equal("#001 Bulbizarre", empty[18].Label); // le Pokédex juste après l'histoire

        sav.PikaFriendship = 255;
        sav.Badges = 0b1000_0001; // Roche et Terre
        sav.Data[0x284E] = 1; // une équipe au Panthéon
        sav.SetCaught(151, true);
        var row = Story(Gen1CompletionData.Yellow, "Recevoir le Pokédex");
        sav.SetEventFlag(EventOf(row), true);
        var steps = Gen1Completion.Steps(sav, LivingDexGame.Yellow);
        string[] done =
        [
            "Recevoir le Pokédex", "Badge Roche (Pierre)", "Badge Terre (Giovanni)",
            "Vaincre la Ligue Pokémon et entrer au Panthéon", "Amitié de Pikachu au maximum",
        ];
        Assert.Equal(done.Order(), steps.Where(s => s.Done && s.Section == CompletionSection.Story).Select(s => s.Label).Order());
        Assert.Equal(["#151 Mew"], steps.Where(s => s.Done && s.Section == CompletionSection.Pokedex).Select(s => s.Label));
        Assert.Equal(151, steps.Single(s => s.Label == "#151 Mew").Species);
    }

    [Fact]
    public void Missed_SilphRocketsAfterGiovanni_AndTheFirstRoute22Rival()
    {
        var sav = new SAV1(LanguageID.French, GameVersion.RB);
        var rows = Gen1CompletionData.RedBlue;
        var silphRockets = rows.Where(r => r.Section == CompletionSection.Trainers && r.Missed is not null && r.Zone == "Sylphe SARL").ToList();
        Assert.Equal(30, silphRockets.Count);
        Assert.All(Gen1Completion.Steps(sav, LivingDexGame.Red), s => Assert.NotEqual(CompletionState.Missed, s.State));

        // Giovanni vaincu à la Sylphe SARL : les Rockets pas encore battus sont partis ; un Rocket battu reste fait
        sav.SetEventFlag(EventOf(Story(rows, "Libérer la Sylphe SARL (Giovanni vaincu)")), true);
        sav.SetEventFlag(EventOf(silphRockets[0]), true);
        // Badge de Pierre gagné sans le premier combat contre le rival sur la Route 22 : raté aussi
        var route22 = rows.Single(r => r.Detail == "premier combat");
        sav.SetEventFlag(int.Parse(route22.Missed![2..]), true);
        var steps = Gen1Completion.Steps(sav, LivingDexGame.Red);
        var trainers = steps.Where(s => s.Section == CompletionSection.Trainers).ToList();
        Assert.Equal(30, trainers.Count(s => s.State == CompletionState.Missed)); // 29 Rockets et le rival
        Assert.Equal(CompletionState.Missed, trainers.Single(s => s.Detail == "premier combat").State);
        Assert.Equal(1, trainers.Count(s => s.Done && s.Zone == "Sylphe SARL"));
        // Raté : compte dans le total, jamais fait
        var progress = CompletionProgress.Of(steps).Sections.Single(s => s.Section == CompletionSection.Trainers);
        Assert.Equal((1, 30, 324), (progress.Done, progress.Missed, progress.Total));
    }

    [Fact]
    public async Task RealRed_ProgressReadInItsFlags()
    {
        var saveId = await Store("sacha", "rouge.sav");
        var candidate = Assert.Single(await _completions.ListCandidatesAsync("sacha"));
        Assert.Equal([LivingDexGame.Red, LivingDexGame.Blue], candidate.Games); // PKHeX ne distingue pas Rouge de Bleu
        Assert.Contains("rouge.sav", candidate.Label);

        var created = await _completions.CreateAsync("sacha", saveId, LivingDexGame.Red);
        Assert.True(created.Ok, created.Message);
        Assert.Equal("Complétion de Pokémon Rouge créée.", created.Message);
        var view = (await _completions.GetAsync("sacha", created.Id!.Value))!;
        Assert.Equal("Pokémon Rouge", view.Game.Name);
        Assert.EndsWith("· rouge.sav", view.SaveLabel);
        Assert.DoesNotContain("rouge.sav", view.PublicSaveLabel);

        // Partie à 4 badges : l'histoire jusqu'à Érika, 47 Pokémon capturés, 142 dresseurs, 68 objets, 2 rencontres
        Assert.Equal([(11, 17), (47, 151), (142, 324), (68, 212), (2, 27)], view.Progress.Sections.Select(s => (s.Done, s.Total)));
        Assert.Equal("35,8 %", view.Progress.Percent);
        bool Done(string label) => view.Steps.Single(s => s.Label == label && s.Section != CompletionSection.Pokedex).Done;
        Assert.True(Done("Badge Prisme (Érika)"));
        Assert.False(Done("Badge Âme (Koga)"));
        Assert.True(Done("Délivrer M. Fuji"));
        Assert.True(Done("CT34 Patience")); // cadeau de Pierre
        Assert.True(Done("Canarticho (contre Piafabec)")); // échange de Carmin sur Mer
        Assert.False(Done("Lokhlass"));

        var summary = Assert.Single(await _completions.ListAsync("sacha"));
        Assert.Equal(view.Progress.Tenths, summary.Progress.Tenths);
        Assert.Empty(await _completions.ListCandidatesAsync("sacha")); // sauvegarde déjà liée
        Assert.Equal([LivingDexGame.Red], await _completions.TakenGamesAsync("sacha"));
        Assert.Equal("Pokémon Rouge", (await _completions.NamesBySaveAsync("sacha"))[saveId]);
        Assert.Null(await _completions.GetAsync("ondine", created.Id.Value)); // pas sur son compte
    }

    [Fact]
    public async Task Create_Refused_SaysWhy()
    {
        var red = await Store("sacha", "rouge.sav");
        var blue = await Store("sacha", "bleu.sav");
        var silver = await Store("sacha", "argent.sav");

        Assert.Equal("Ce jeu n'a pas encore de complétion.", (await _completions.CreateAsync("sacha", silver, LivingDexGame.Silver)).Message);
        Assert.Equal("Cette sauvegarde n'existe plus.", (await _completions.CreateAsync("ondine", red, LivingDexGame.Red)).Message);
        Assert.Equal("Cette sauvegarde n'est pas une partie de Pokémon Jaune.", (await _completions.CreateAsync("sacha", red, LivingDexGame.Yellow)).Message);
        Assert.True((await _completions.CreateAsync("sacha", red, LivingDexGame.Red)).Ok);
        Assert.Equal("Tu as déjà une complétion de Pokémon Rouge : supprime-la pour en créer une autre.",
            (await _completions.CreateAsync("sacha", blue, LivingDexGame.Red)).Message);
        Assert.Equal("Cette sauvegarde a déjà une complétion.", (await _completions.CreateAsync("sacha", red, LivingDexGame.Blue)).Message);

        // La sauvegarde Bleu reste proposée, pour Bleu seulement (Rouge est pris)
        Assert.Equal([LivingDexGame.Blue], Assert.Single(await _completions.ListCandidatesAsync("sacha")).Games);
        Assert.True((await _completions.CreateAsync("sacha", blue, LivingDexGame.Blue)).Ok);
        Assert.Equal(2, (await _completions.ListAsync("sacha")).Count);
    }

    [Fact]
    public async Task Delete_AndCascadeWithTheSave_LivingDexOnTheSameSave()
    {
        var saveId = await Store("sacha", "rouge.sav");
        var dexes = new LivingDexService(_db.Factory, NullLogger<LivingDexService>.Instance);
        Assert.True((await dexes.CreateAsync("sacha", saveId, LivingDexGame.Red)).Ok); // la même sauvegarde peut avoir les deux
        var id = (await _completions.CreateAsync("sacha", saveId, LivingDexGame.Red)).Id!.Value;

        Assert.False(await _completions.DeleteAsync("ondine", id));
        Assert.True(await _completions.DeleteAsync("sacha", id));
        Assert.Empty(await _completions.ListAsync("sacha"));

        Assert.True((await _completions.CreateAsync("sacha", saveId, LivingDexGame.Blue)).Ok);
        await using (var db = _db.NewContext())
        {
            db.StoredSaves.Remove(await db.StoredSaves.SingleAsync(s => s.Id == saveId));
            await db.SaveChangesAsync();
        }
        await using (var db = _db.NewContext())
            Assert.Empty(await db.Completions.ToListAsync()); // supprimée avec la sauvegarde
    }

    [Fact]
    public async Task UnreadableSave_CountsForNothing()
    {
        var saveId = await Store("sacha", "rouge.sav");
        var id = (await _completions.CreateAsync("sacha", saveId, LivingDexGame.Red)).Id!.Value;
        await using (var db = _db.NewContext())
            await db.StoredSaves.Where(s => s.Id == saveId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Data, new byte[16]));
        var view = (await _completions.GetAsync("sacha", id))!;
        Assert.Empty(view.Steps);
        Assert.Equal(0, view.Progress.Tenths);
    }
}
