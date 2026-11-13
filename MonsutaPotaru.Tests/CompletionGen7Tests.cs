using Microsoft.Extensions.Logging.Abstractions;
using PKHeX.Core;
using MonsutaPotaru.Data;
using MonsutaPotaru.Services;

namespace MonsutaPotaru.Tests;

/// <summary>
/// Complétion de la 7e génération : tables de Soleil / Lune, d'Ultra-Soleil / Ultra-Lune et de Let's Go (générées
/// d'après les listes de drapeaux de FlagsEditorEX et les check-lists de l'utilisateur), Pokédex national (Let's Go :
/// Kanto, Meltan et Melmetal) ; lecture de la vraie sauvegarde d'Ultra-Soleil ; Soleil et Let's Go en mémoire. Pas encore
/// de dresseurs.
/// </summary>
public sealed class CompletionGen7Tests : IDisposable
{
    private readonly TestDatabase _db = new("sacha", "ondine", "pierre");
    private readonly StoredSaveService _saves;
    private readonly CompletionService _completions;

    public CompletionGen7Tests()
    {
        _saves = _db.StoredSaves();
        _completions = new CompletionService(_db.Factory, NullLogger<CompletionService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    private static IEnumerable<int> Events(string condition) =>
        condition.Split('|', '+').Where(a => a.StartsWith("E:", StringComparison.Ordinal)).Select(a => int.Parse(a[2..]));

    private static byte[] Real(string file) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Sauvegardes", file));

    private static CompletionRow[] Table(string game) => (CompletionRow[])typeof(Gen7CompletionData).GetField(game)!.GetValue(null)!;

    [Theory]
    [InlineData("Sun", 28, 408, 27, 134, 100, 4000)]
    [InlineData("Moon", 28, 408, 27, 134, 100, 4000)]
    [InlineData("UltraSun", 30, 482, 62, 140, 100, 4960)]
    [InlineData("UltraMoon", 30, 482, 62, 140, 100, 4960)]
    [InlineData("LetsGoPikachu", 17, 331, 30, 158, 60, 4096)]
    [InlineData("LetsGoEevee", 17, 331, 30, 158, 60, 4096)]
    public void Tables_FiveSections_AndConditionsInsideTheSave(string game, int story, int items, int encounters, int quests,
        int machines, int flags)
    {
        var rows = Table(game);
        Assert.DoesNotContain(rows, r => r.Section is CompletionSection.Pokedex or CompletionSection.Trainers);
        Assert.Equal([story, items, encounters, quests],
            new[] { CompletionSection.Story, CompletionSection.Items, CompletionSection.Encounters, CompletionSection.Quests }
                .Select(s => rows.Count(r => r.Section == s)));
        foreach (var atom in rows.Select(r => r.Done).SelectMany(c => c.Split('|', '+')))
        {
            var parts = atom.Split(':');
            switch (parts[0])
            {
                case "E": Assert.InRange(int.Parse(parts[1]), 0x60, flags - 1); break;
                case "D": Assert.InRange(int.Parse(parts[1]), 1, 809); break;
                case "K": Assert.InRange(int.Parse(parts[1]), 1, 1057); break;
                case "PS": Assert.InRange(int.Parse(parts[1]), 0, 14); break;
                case "BT": Assert.InRange(int.Parse(parts[1]), 0, 2); break;
                case "ZC": Assert.InRange(int.Parse(parts[1]), 0, 99); break;
                case "TF": Assert.InRange(int.Parse(parts[1]), 1, 155); break;
                default: Assert.Fail($"Condition inattendue en 7e génération : {atom}"); break;
            }
        }
        Assert.All(rows.Where(r => r.Species != 0), r => Assert.InRange(r.Species, 1, 809));
        // Objets : chaque lieu en un seul bloc ; un drapeau = un objet ; chaque CT une fois ; jamais un cristal Z « tenu »
        var zones = rows.Where(r => r.Section == CompletionSection.Items).Select(r => r.Zone).ToList();
        var blocks = zones.Where((z, i) => i == 0 || zones[i - 1] != z).ToList();
        Assert.Equal(blocks.Count, blocks.Distinct().Count());
        var done = rows.Where(r => r.Section == CompletionSection.Items).Select(r => r.Done).ToList();
        Assert.Equal(done.Count, done.Distinct().Count());
        Assert.Equal(Enumerable.Range(1, machines), rows.Where(r => r.Tm != 0).Select(r => r.Tm).Order());
        Assert.DoesNotContain(rows, r => r.Item is >= 776 and <= 806 or 836);
        // Quêtes : chaque activité en un seul bloc
        var activities = rows.Where(r => r.Section == CompletionSection.Quests).Select(r => r.Zone).ToList();
        var groups = activities.Where((z, i) => i == 0 || activities[i - 1] != z).ToList();
        Assert.Equal(groups.Count, groups.Distinct().Count());
    }

    [Fact]
    public void Labels_MachinesCrystalsVersionsAndQuests()
    {
        Assert.Equal("CT13 Laser Glace", Gen7Completion.AlolaLabel(new(CompletionSection.Items, "Mont Lanakila", null, "K:1", Tm: 13)));
        Assert.Equal("CT100 Confidence", Gen7Completion.AlolaLabel(new(CompletionSection.Items, "Route 1", null, "K:1", Tm: 100)));
        Assert.Equal("CT01 Coup d’Boule", Gen7Completion.LetsGoLabel(new(CompletionSection.Items, "Argenta", null, "K:1", Tm: 1)));
        Assert.Equal("Normazélite", Gen7Completion.ItemName(807)); // PKHeX : « Normazélite [Z] »
        Assert.Equal("Lougarozélite", Gen7Completion.ItemName(931));
        Assert.Equal("Florizarrite", Gen7Completion.LetsGoItemName(659));
        // Échanges de Let's Go : la forme d'Alola
        var trade = Gen7CompletionData.LetsGoPikachu.First(r => r.Section == CompletionSection.Encounters && r.Form == 1);
        Assert.Equal("Rattata d'Alola (contre Rattata)", Gen7Completion.LetsGoLabel(trade));
        // Versions : Ultra-Chimères, fossiles, légendaires de l'Ultra-Brèche
        Assert.Contains(Gen7CompletionData.Sun, r => r.Species == 794 && r.Section == CompletionSection.Encounters);
        Assert.DoesNotContain(Gen7CompletionData.Moon, r => r.Species == 794);
        Assert.Contains(Gen7CompletionData.Moon, r => r.Species == 410);
        Assert.DoesNotContain(Gen7CompletionData.Sun, r => r.Species == 410);
        Assert.Contains(Gen7CompletionData.UltraSun, r => r.Species == 250 && r.Section == CompletionSection.Encounters);
        Assert.DoesNotContain(Gen7CompletionData.UltraMoon, r => r.Species == 250);
        // Cristaux Z des épreuves dans l'histoire, pas dans les objets
        Assert.Single(Gen7CompletionData.UltraSun, r => r.Done.Split('|').Contains("K:809"));
        Assert.Equal(8, Gen7CompletionData.LetsGoEevee.Count(r => r.Label?.StartsWith("Badge ", StringComparison.Ordinal) == true));
        // Quêtes : Arbre de Combat (6), Zygarde (5 Cœurs et 95 / 100 Cellules), Scanner des Îles (28), Experts (153)
        Assert.Equal(6, Gen7CompletionData.Moon.Count(r => r.Zone == "Arbre de Combat" && r.Section == CompletionSection.Quests));
        Assert.Equal(95, Gen7CompletionData.Sun.Count(r => r.Done.StartsWith("ZC:", StringComparison.Ordinal)));
        Assert.Equal(100, Gen7CompletionData.UltraMoon.Count(r => r.Done.StartsWith("ZC:", StringComparison.Ordinal)));
        Assert.Equal(28, Gen7CompletionData.UltraSun.Count(r => r.Zone == "Scanner des Îles"));
        Assert.Equal(153, Gen7CompletionData.LetsGoPikachu.Count(r => r.Done.StartsWith("TF:", StringComparison.Ordinal)
            && int.Parse(r.Done[3..]) <= 153));
        // Nectars des jardins (quotidiens) : jamais
        Assert.DoesNotContain(Gen7CompletionData.Sun, r => r.Item is >= 853 and <= 856);
    }

    [Fact]
    public void Sun_InMemory_CrystalsStampsFlagsBattleTreeAndZygarde()
    {
        var sav = new SAV7SM { Version = GameVersion.SN };
        Assert.Equal([LivingDexGame.Sun], CompletionGames.For(sav));
        var empty = Gen7Completion.Steps(sav, LivingDexGame.Sun);
        var progress = CompletionProgress.Of(empty);
        Assert.Equal((5, 0), (progress.Sections.Count, progress.Tenths));
        Assert.Equal(802, empty.Count(s => s.Section == CompletionSection.Pokedex));
        Assert.Equal("#802 Marshadow", empty.Last(s => s.Section == CompletionSection.Pokedex).Label);

        var rows = Gen7CompletionData.Sun;
        SaveSessionTests.GiveItems(sav, (807, 1), (816, 1), (328, 1)); // Normazélite, Aérozélite, CT01
        sav.Misc.Stamps = 1 << 2; // tampon de l'île d'Akala
        foreach (var flag in Events(rows.Single(r => r.Label == "Devenir Maître de la Ligue Pokémon d'Alola").Done))
            sav.EventWork.SetEventFlag(flag, true);
        sav.BattleTree.SetTreeStreak(20, 1, super: false, max: true);
        sav.EventWork.SetZygardeCell(2, 2); // cellule n° 3 ramassée
        sav.EventWork.SetZygardeCell(3, 1); // n° 4 seulement visible

        var steps = Gen7Completion.Steps(sav, LivingDexGame.Sun);
        Assert.True(steps.Single(s => s.Label == "Épreuve d'Althéo").Done);
        Assert.False(steps.Single(s => s.Label == "Épreuve de Néphie").Done);
        Assert.True(steps.Single(s => s.Label == "Grande Épreuve d'Akala (Alyxia)").Done); // par le tampon
        Assert.False(steps.Single(s => s.Label == "Grande Épreuve de Mele-Mele (Pectorius)").Done);
        Assert.True(steps.Single(s => s.Label == "Devenir Maître de la Ligue Pokémon d'Alola").Done);
        Assert.True(steps.Single(s => s.Label == "Aérozélite").Done);
        Assert.True(steps.Single(s => s.Label == "CT01 Rengorgement").Done);
        Assert.False(steps.Single(s => s.Label == "CT02 Draco-Griffe").Done);
        Assert.True(steps.Single(s => s.Label == "Combat Duo : 20 victoires (Red ou Blue au 20e combat)").Done);
        Assert.False(steps.Single(s => s.Label == "Combat Solo : 20 victoires (Red ou Blue au 20e combat)").Done);
        Assert.True(steps.Single(s => s.Label == "Cellule Zygarde n° 3").Done);
        Assert.False(steps.Single(s => s.Label == "Cellule Zygarde n° 4").Done);
        Assert.InRange(CompletionProgress.Of(steps).Tenths, 1, 999);
    }

    [Fact]
    public void LetsGo_InMemory_BadgesTitlesAndKantoPokedex()
    {
        var sav = new SAV7b();
        Assert.Equal([LivingDexGame.LetsGoPikachu, LivingDexGame.LetsGoEevee], CompletionGames.For(sav));
        var empty = Gen7Completion.Steps(sav, LivingDexGame.LetsGoEevee);
        Assert.Equal(153, empty.Count(s => s.Section == CompletionSection.Pokedex));
        Assert.Equal("#809 Melmetal", empty.Last(s => s.Section == CompletionSection.Pokedex).Label);
        Assert.Equal(0, CompletionProgress.Of(empty).Tenths);

        var rows = Gen7CompletionData.LetsGoEevee;
        foreach (var flag in Events(rows.Single(r => r.Label == "Badge Roche (Pierre)").Done))
            sav.EventWork.SetFlag(flag, true);
        sav.EventWork.SetTitleFlag(133); // Expert Évoli
        sav.EventWork.SetTitleFlag(155); // Maître de Combat
        SaveSessionTests.GiveItems(sav, (659, 1)); // Florizarrite

        var steps = Gen7Completion.Steps(sav, LivingDexGame.LetsGoEevee);
        Assert.True(steps.Single(s => s.Label == "Badge Roche (Pierre)").Done);
        Assert.False(steps.Single(s => s.Label == "Badge Cascade (Ondine)").Done);
        Assert.True(steps.Single(s => s.Label == "Battre l'Expert Évoli").Done);
        Assert.False(steps.Single(s => s.Label == "Battre l'Expert Pikachu").Done);
        Assert.True(steps.Single(s => s.Label == "Battre Red (titre de Maître de Combat)").Done);
        Assert.True(steps.Single(s => s.Label == "Florizarrite").Done);
        Assert.InRange(CompletionProgress.Of(steps).Tenths, 1, 999);
    }

    [Fact]
    public async Task RealUltraSun_ProgressReadInItsFlags()
    {
        Assert.True((await _saves.AddAsync("sacha", "ultra-soleil.sav", Real("ultra-soleil.sav"))).Ok);
        var candidate = Assert.Single(await _completions.ListCandidatesAsync("sacha"));
        Assert.Equal([LivingDexGame.UltraSun], candidate.Games);
        var view = (await _completions.GetAsync("sacha",
            (await _completions.CreateAsync("sacha", candidate.SaveId, LivingDexGame.UltraSun)).Id!.Value))!;

        // Début d'Akala : épreuve d'Althéo et Grande Épreuve de Pectorius faites, Tauros et Mastouffe ; pas encore Néphie
        Assert.True(view.Steps.Single(s => s.Label == "Épreuve d'Althéo").Done);
        Assert.True(view.Steps.Single(s => s.Label == "Grande Épreuve de Mele-Mele (Pectorius)").Done);
        Assert.True(view.Steps.Single(s => s.Label == "Recevoir l'Appel-Monture (Tauros)").Done);
        Assert.True(view.Steps.Single(s => s.Label == "Poké Monture Mastouffe").Done);
        Assert.False(view.Steps.Single(s => s.Label == "Épreuve de Néphie").Done);
        Assert.True(view.Steps.Single(s => s.Label == "Aérozélite").Done);
        // 20 cellules Zygarde ramassées ; aucun objet de Poni trouvé
        Assert.Equal(20, view.Steps.Count(s => s.Done && s.Label.StartsWith("Cellule Zygarde", StringComparison.Ordinal)));
        Assert.DoesNotContain(view.Steps, s => s.Done && s.Section == CompletionSection.Items
            && (s.Zone.Contains("Poni", StringComparison.Ordinal) || s.Zone == "Village Flottant"));
        Assert.Equal(68, view.Steps.Count(s => s.Done && s.Section == CompletionSection.Pokedex));
        Assert.DoesNotContain(view.Steps, s => s.State == CompletionState.Missed);
        Assert.InRange(view.Progress.Tenths, 1, 999);
    }
}
