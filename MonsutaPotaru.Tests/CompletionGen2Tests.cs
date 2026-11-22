using Microsoft.Extensions.Logging.Abstractions;
using PKHeX.Core;
using MonsutaPotaru.Data;
using MonsutaPotaru.Services;

namespace MonsutaPotaru.Tests;

/// <summary>
/// Complétion de la 2e génération : tables d'Or / Argent et de Cristal (générées d'après pokegold, pokecrystal et les
/// check-lists de l'utilisateur), six sections, lecture de la vraie sauvegarde d'Argent ; Cristal et les cas limites en
/// mémoire (une sauvegarde de 2e génération créée vide n'est pas relue par PKHeX une fois écrite).
/// </summary>
public sealed class CompletionGen2Tests : IDisposable
{
    private readonly TestDatabase _db = new("sacha", "ondine", "pierre");
    private readonly StoredSaveService _saves;
    private readonly CompletionService _completions;

    public CompletionGen2Tests()
    {
        _saves = _db.StoredSaves();
        _completions = new CompletionService(_db.Factory, NullLogger<CompletionService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    private static int EventOf(string condition)
    {
        Assert.StartsWith("E:", condition);
        return int.Parse(condition[2..].Split('+', '|')[0]);
    }

    [Fact]
    public void Tables_HaveSixSections_AndConditionsInsideTheSave()
    {
        foreach (var (rows, crystal) in new[] { (Gen2CompletionData.GoldSilver, false), (Gen2CompletionData.Crystal, true) })
        {
            Assert.DoesNotContain(rows, r => r.Section == CompletionSection.Pokedex);
            Assert.Equal(crystal ? 35 : 34, rows.Count(r => r.Section == CompletionSection.Story));
            Assert.Equal(crystal ? 333 : 321, rows.Count(r => r.Section == CompletionSection.Trainers));
            Assert.Equal(crystal ? 350 : 300, rows.Count(r => r.Section == CompletionSection.Items));
            Assert.Equal(crystal ? 26 : 23, rows.Count(r => r.Section == CompletionSection.Encounters));
            Assert.Equal(50, rows.Count(r => r.Section == CompletionSection.Quests));
            // 16 badges, les 26 Zarbi (une forme chacun), les 7 Noigrumes de Fargas, la Fratrie des sept jours
            Assert.Equal(16, rows.Count(r => r.Label?.StartsWith("Badge ", StringComparison.Ordinal) == true));
            Assert.Equal(Enumerable.Range(0, 26), rows.Where(r => r.Done.StartsWith("U:", StringComparison.Ordinal)).Select(r => (int)r.Form));
            Assert.Equal(7, rows.Count(r => r.Detail == "Fargas"));
            Assert.Equal(7, rows.Count(r => r.Detail == "Fratrie des sept jours"));
            // Cristal seulement : Suicune à la Tour Ferraille, Minidraco du sanctuaire, Eusine
            Assert.Equal(crystal, rows.Any(r => r.Species == 147));
            Assert.Equal(crystal, rows.Any(r => r.Label == "Voir Suicune s'enfuir et battre Eusine"));
            foreach (var atom in rows.SelectMany(r => new[] { r.Done, r.Missed }).OfType<string>().SelectMany(c => c.Split('|', '+')))
            {
                var parts = atom.Split(':');
                switch (parts[0])
                {
                    case "E": Assert.InRange(int.Parse(parts[1]), 0, 1999); break;
                    case "R" or "S": Assert.InRange(int.Parse(parts[1]), 0x23E4, 0x2600); break;
                    case "U": Assert.Contains(int.Parse(parts[1]), new[] { 0x2A8C, 0x2A67 }); break;
                    case "D": Assert.InRange(int.Parse(parts[1]), 1, 251); break;
                    default: Assert.Fail($"Condition inattendue en 2e génération : {atom}"); break;
                }
            }
            // GS Ball (distribution, comptée : choix de l'utilisateur) et son Celebi : Cristal seulement, une fois chacun ;
            // l'Œuf Mystère, rendu au Prof. Orme, jamais
            Assert.Equal(crystal ? 1 : 0, rows.Count(r => r.Section == CompletionSection.Items && Gen2Completion.Label(r) == "GS Ball"));
            Assert.Equal(crystal ? 1 : 0, rows.Count(r => r.Section == CompletionSection.Encounters && r.Species == 251));
            Assert.DoesNotContain(rows, r => r.Section == CompletionSection.Items && Gen2Completion.Label(r) == "Œuf Mystère");
        }
    }

    [Fact]
    public void Labels_TmsCountsAndUnown()
    {
        var rows = Gen2CompletionData.Crystal;
        Assert.Equal("CT31 Coud’Boue", Gen2Completion.Label(rows.First(r => r.Tm == 31)));
        Assert.Equal("CS01 Coupe", Gen2Completion.Label(rows.First(r => r.Tm == 51)));
        Assert.Equal("Super Bonbon ×2", Gen2Completion.Label(new(CompletionSection.Items, "Mauville", null, "E:1", Item: 32, Count: 2)));
        Assert.Equal("Capturer Zarbi Q", Gen2Completion.Label(rows.Single(r => r.Done.StartsWith("U:", StringComparison.Ordinal) && r.Form == 16)));
    }

    [Fact]
    public void Crystal_InMemory_BadgesUnownMissedRockets()
    {
        var sav = new SAV2(LanguageID.French, GameVersion.C);
        var empty = Gen2Completion.Steps(sav, LivingDexGame.Crystal);
        Assert.Equal(35 + 251 + 333 + 350 + 26 + 50, empty.Count);
        var progress = CompletionProgress.Of(empty);
        Assert.Equal((6, 0, "un sixième"), (progress.Sections.Count, progress.Tenths, progress.Share));
        Assert.Equal("#251 Celebi", empty[35 + 250].Label);

        sav.Data[0x23E5] = 0b0000_0001; // Badge Zéphyr
        sav.Data[0x23E6] = 0b1000_0000; // Badge Terre
        sav.Data[0x2A67] = 1 + 16; // Zarbi Q capturé le premier
        var rows = Gen2CompletionData.Crystal;
        var rockets = rows.Where(r => r.Missed?.Contains('+') == true).ToList(); // Tour Radio : partis avec la Team Rocket
        Assert.NotEmpty(rockets);
        foreach (var flag in rockets[0].Missed!.Split('+'))
            sav.SetEventFlag(EventOf(flag), true);
        var steps = Gen2Completion.Steps(sav, LivingDexGame.Crystal);
        // La Tour Radio libérée (condition des Rockets partis) compte aussi dans l'histoire
        Assert.Equal(["Badge Zéphyr (Albert)", "Libérer la Tour Radio de la Team Rocket", "Badge Terre (Blue)"], steps.Where(s => s.Done && s.Section == CompletionSection.Story).Select(s => s.Label));
        Assert.Equal(["Capturer Zarbi Q"], steps.Where(s => s.Done && s.Section == CompletionSection.Quests).Select(s => s.Label));
        Assert.Equal((ushort)201, steps.Single(s => s.Label == "Capturer Zarbi Q").Species);
        Assert.Equal(rockets.Count, steps.Count(s => s.State == CompletionState.Missed));

        // Red : parti du Mont Argenté (drapeau levé) après l'accord du Prof. Chen
        var red = rows.Single(r => r.Label == "Battre Red");
        foreach (var flag in red.Done.Split('+'))
            sav.SetEventFlag(EventOf(flag), true);
        Assert.True(Gen2Completion.Steps(sav, LivingDexGame.Crystal).Single(s => s.Label == "Battre Red").Done);
    }

    [Fact]
    public async Task RealSilver_ProgressReadInItsFlags()
    {
        var result = await _saves.AddAsync("sacha", "argent.sav", File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Sauvegardes", "argent.sav")));
        Assert.True(result.Ok, result.Message);
        var candidate = Assert.Single(await _completions.ListCandidatesAsync("sacha"));
        Assert.Equal([LivingDexGame.Gold, LivingDexGame.Silver], candidate.Games); // PKHeX ne distingue pas Or d'Argent
        var created = await _completions.CreateAsync("sacha", candidate.SaveId, LivingDexGame.Silver);
        Assert.True(created.Ok, created.Message);
        var view = (await _completions.GetAsync("sacha", created.Id!.Value))!;

        // Partie aux 8 badges de Johto, Ligue battue, Kanto pas commencé
        Assert.Equal(["Histoire", "Pokédex", "Dresseurs", "Objets", "Rencontres uniques", "Quêtes annexes"], view.Progress.Sections.Select(s => s.Name));
        Assert.Equal([(22, 34), (34, 251), (180, 321), (99, 300), (9, 23), (1, 50)], view.Progress.Sections.Select(s => (s.Done, s.Total)));
        Assert.Equal("34,7 %", view.Progress.Percent);
        bool Done(string label) => view.Steps.First(s => s.Label == label).Done;
        Assert.True(Done("Badge Lever (Sandra)"));
        Assert.True(Done("Battre le Maître Peter et entrer au Panthéon"));
        Assert.False(Done("Badge Roche (Pierre)"));
        Assert.True(Done("Lugia"));
        Assert.True(Done("CT31 Coud’Boue"));
        Assert.True(Done("Rencontrer Vanessa (vendredi)"));
        Assert.DoesNotContain(view.Steps, s => s.State == CompletionState.Missed);
        // Rival de la Tour Cendrée et de la Route Victoire : lus dans la scène de la carte
        Assert.Equal(5, view.Steps.Count(s => s.Label == "Rival" && s.Done));
    }
}
