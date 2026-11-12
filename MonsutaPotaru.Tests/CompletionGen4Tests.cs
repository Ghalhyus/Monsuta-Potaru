using Microsoft.Extensions.Logging.Abstractions;
using PKHeX.Core;
using MonsutaPotaru.Data;
using MonsutaPotaru.Services;

namespace MonsutaPotaru.Tests;

/// <summary>
/// Complétion de la 4e génération : tables de Diamant / Perle (lues dans les fichiers binaires de pokediamond), de Platine et
/// d'HeartGold / SoulSilver (générées d'après pokeplatinum, pokeheartgold et les check-lists de l'utilisateur), Pokédex
/// national, lecture des vraies sauvegardes de Perle et de SoulSilver ; Platine en mémoire.
/// </summary>
public sealed class CompletionGen4Tests : IDisposable
{
    private readonly TestDatabase _db = new("sacha", "ondine", "pierre");
    private readonly StoredSaveService _saves;
    private readonly CompletionService _completions;

    public CompletionGen4Tests()
    {
        _saves = _db.StoredSaves();
        _completions = new CompletionService(_db.Factory, NullLogger<CompletionService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    private static IEnumerable<int> Events(string condition) =>
        condition.Split('|', '+').Where(a => a.StartsWith("E:", StringComparison.Ordinal)).Select(a => int.Parse(a[2..]));

    private static byte[] Real(string file) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Sauvegardes", file));

    [Theory]
    [InlineData("DiamondPearl", 25, 421, 563, 18, 33, 8)]
    [InlineData("Platinum", 29, 424, 640, 28, 40, 8)]
    [InlineData("HeartGoldSoulSilver", 32, 394, 545, 34, 38, 16)]
    public void Tables_SixSections_AndConditionsInsideTheSave(string game, int story, int trainers, int items, int encounters,
        int quests, int badges)
    {
        var rows = (CompletionRow[])typeof(Gen4CompletionData).GetField(game)!.GetValue(null)!;
        Assert.DoesNotContain(rows, r => r.Section == CompletionSection.Pokedex);
        Assert.Equal([story, trainers, items, encounters, quests],
            new[] { CompletionSection.Story, CompletionSection.Trainers, CompletionSection.Items, CompletionSection.Encounters, CompletionSection.Quests }
                .Select(s => rows.Count(r => r.Section == s)));
        Assert.Equal(badges, rows.Count(r => r.Label?.StartsWith("Badge ", StringComparison.Ordinal) == true));
        foreach (var atom in rows.SelectMany(r => new[] { r.Done, r.Missed }).OfType<string>().SelectMany(c => c.Split('|', '+')))
        {
            var parts = atom.Split(':');
            switch (parts[0])
            {
                case "E": Assert.InRange(int.Parse(parts[1]), 1, 2911); break; // 2 912 drapeaux (PKHeX)
                case "D": Assert.InRange(int.Parse(parts[1]), 1, 493); break;
                case "B": Assert.InRange(int.Parse(parts[1]), 0, badges - 1); break;
                case "Z": Assert.InRange(int.Parse(parts[1]), 0, 27); break;
                case "W": Assert.InRange(int.Parse(parts[1]), 0, 287); break;
                case "M" or "N": break;
                default: Assert.Fail($"Condition inattendue en 4e génération : {atom}"); break;
            }
        }
        // Chaque dresseur a son équipe (pas « Dresseur ») ; seul le rival, sous plusieurs identifiants, n'en montre pas
        Assert.All(rows.Where(r => r.Section == CompletionSection.Trainers),
            r => Assert.True(r.Team is not null || r.Label == "Rival", $"{r.Zone} : {r.Done}"));
        Assert.All(rows.Where(r => r.Species != 0), r => Assert.InRange(r.Species, 1, 493));
        // Un lieu lisible partout (pas de nom abrégé de PKHeX)
        Assert.DoesNotContain(rows, r => r.Zone.EndsWith('.'));
        // Dresseurs et objets : chaque lieu en un seul bloc (deux lieux au même rang de la check-list ne s'entremêlent pas)
        foreach (var section in new[] { CompletionSection.Trainers, CompletionSection.Items })
        {
            var zones = rows.Where(r => r.Section == section).Select(r => r.Zone).ToList();
            var blocks = zones.Where((z, i) => i == 0 || zones[i - 1] != z).ToList();
            Assert.Equal(blocks.Count, blocks.Distinct().Count());
        }
        // Une même condition n'apparaît qu'une fois parmi les objets (un drapeau = un objet)
        var items4 = rows.Where(r => r.Section == CompletionSection.Items).Select(r => r.Done).ToList();
        Assert.Equal(items4.Count, items4.Distinct().Count());
    }

    [Fact]
    public void Labels_MachinesItemsAndDistributions()
    {
        Assert.Equal("CT01 Mitra-Poing", Gen4Completion.Label(new(CompletionSection.Items, "Route 215", null, "E:1", Tm: 1)));
        Assert.Equal("CT92 Distorsion", Gen4Completion.Label(new(CompletionSection.Items, "Route 213", null, "E:1", Tm: 92)));
        Assert.Equal("CS01 Coupe", Gen4Completion.Label(new(CompletionSection.Items, "Vestigion", null, "E:1", Tm: 93)));
        Assert.Equal("CS08 Escalade", Gen4Completion.Label(new(CompletionSection.Items, "Route 217", null, "E:1", Tm: 100)));
        Assert.Equal("Potion", Gen4Completion.ItemName(17));
        // Distributions réelles seulement : Darkrai et Shaymin en Platine (Carte Membre, Lettre), pas en Diamant / Perle
        Assert.Contains(Gen4CompletionData.Platinum, r => r.Species == 491 && r.Detail == "Carte Membre");
        Assert.DoesNotContain(Gen4CompletionData.DiamondPearl, r => r.Species is 491 or 492);
        Assert.DoesNotContain(Gen4CompletionData.Platinum, r => r.Species == 493); // Arceus : Flûte Azur jamais distribuée
        Assert.Contains(Gen4CompletionData.HeartGoldSoulSilver, r => r.Species == 251 && r.Detail == "GS Ball");
        // 28 Zarbi dans chaque jeu ; formes de Motisma en Platine seulement
        Assert.All(new[] { Gen4CompletionData.DiamondPearl, Gen4CompletionData.Platinum, Gen4CompletionData.HeartGoldSoulSilver },
            rows => Assert.Equal(28, rows.Count(r => r.Done.StartsWith("Z:", StringComparison.Ordinal))));
        Assert.Equal(5, Gen4CompletionData.Platinum.Count(r => r.Done.StartsWith("M:479:", StringComparison.Ordinal)));
        Assert.Equal("Zarbi ? au Pokédex", Gen4CompletionData.Platinum.Single(r => r.Done == "Z:27").Label);
    }

    [Fact]
    public void Platinum_InMemory_BadgesVariablesFormsAndNationalDex()
    {
        var sav = new SAV4Pt();
        sav.Dex.ClearUnownForms(); // une sauvegarde vierge de PKHeX est à zéro (Zarbi A « vu ») ; le jeu, lui, la vide
        Assert.Equal([LivingDexGame.Platinum], CompletionGames.For(sav));
        var empty = Gen4Completion.Steps(sav, LivingDexGame.Platinum);
        var progress = CompletionProgress.Of(empty);
        Assert.Equal((6, 0), (progress.Sections.Count, progress.Tenths));
        Assert.Equal(493, empty.Count(s => s.Section == CompletionSection.Pokedex));
        Assert.Equal("#493 Arceus", empty.Last(s => s.Section == CompletionSection.Pokedex).Label);

        sav.Badges = 0b1; // Badge Charbon
        sav.Dex.SetCaught(387, true); // Tortipouss (SAV4.SetCaught ne fait rien sur une sauvegarde vierge de PKHeX)
        var rows = Gen4CompletionData.Platinum;
        foreach (var flag in Events(rows.Single(r => r.Label == "Recevoir le Pokédex").Done))
            sav.SetEventFlag(flag, true);
        var distortion = rows.Single(r => r.Label == "Battre Hélio dans le Monde Distorsion").Done.Split(':');
        sav.SetWork(int.Parse(distortion[1]), ushort.Parse(distortion[2]));
        sav.NationalDex = true;
        sav.Dex.AddUnownForm(26); // Zarbi !
        sav.Dex.SetForms(479, [0, 2]); // Motisma normal et Lavage

        var steps = Gen4Completion.Steps(sav, LivingDexGame.Platinum);
        Assert.True(steps.Single(s => s.Label == "Badge Charbon (Pierrick)").Done);
        Assert.False(steps.Single(s => s.Label == "Badge Forêt (Flo)").Done);
        Assert.True(steps.Single(s => s.Label == "Recevoir le Pokédex").Done);
        Assert.True(steps.Single(s => s.Label == "Battre Hélio dans le Monde Distorsion").Done);
        Assert.True(steps.Single(s => s.Label == "Recevoir le Pokédex National").Done);
        Assert.True(steps.Single(s => s.Label == "#387 Tortipouss").Done);
        Assert.True(steps.Single(s => s.Label == "Zarbi ! au Pokédex").Done);
        Assert.False(steps.Single(s => s.Label == "Zarbi A au Pokédex").Done);
        Assert.True(steps.Single(s => s.Label == "Motisma forme Lavage au Pokédex").Done);
        Assert.False(steps.Single(s => s.Label == "Motisma forme Chaleur au Pokédex").Done);

        // Un dresseur battu : son drapeau (0x550 + son numéro)
        var trainer = rows.First(r => r.Section == CompletionSection.Trainers && r.Team is not null);
        sav.SetEventFlag(Events(trainer.Done).First(), true);
        Assert.Equal(1, Gen4Completion.Steps(sav, LivingDexGame.Platinum).Count(s => s.Done && s.Section == CompletionSection.Trainers));
    }

    [Fact]
    public void HeartGold_InMemory_KantoBadgesAfterJohto()
    {
        var sav = new SAV4HGSS();
        Assert.Contains(LivingDexGame.HeartGold, CompletionGames.For(sav));
        sav.Badges16 = 1; // Badge Roche seul : PKHeX range les badges de Kanto à part (Badges16, 8 bits)
        var steps = Gen4Completion.Steps(sav, LivingDexGame.HeartGold);
        Assert.True(steps.Single(s => s.Label == "Badge Roche (Pierre)").Done);
        Assert.False(steps.Single(s => s.Label == "Badge Zéphyr (Albert)").Done);
    }

    [Fact]
    public async Task RealPearl_AndSoulSilver_ProgressReadInTheirFlags()
    {
        foreach (var file in new[] { "perle.sav", "soulsilver.sav" })
            Assert.True((await _saves.AddAsync("sacha", file, Real(file))).Ok);
        var candidates = await _completions.ListCandidatesAsync("sacha");
        Assert.Equal(2, candidates.Count);
        var pearl = candidates.Single(c => c.Label.Contains("perle.sav"));
        Assert.Equal([LivingDexGame.Diamond, LivingDexGame.Pearl], pearl.Games);

        // Perle : 3 badges (Charbon, Forêt, Pavé), Pokédex et Pokémontre reçus, Mars battu aux Éoliennes, bâtiment Galaxie
        // de Vestigion libéré ; pas encore la Clé Stockage
        var view = (await _completions.GetAsync("sacha", (await _completions.CreateAsync("sacha", pearl.SaveId, LivingDexGame.Pearl)).Id!.Value))!;
        Assert.Equal([true, true, true, false], new[] { "Badge Charbon (Pierrick)", "Badge Forêt (Flo)", "Badge Pavé (Mélina)", "Badge Palustre (Lovis)" }
            .Select(l => view.Steps.Single(s => s.Label == l).Done));
        Assert.True(view.Steps.Single(s => s.Label == "Battre Mars aux Éoliennes").Done);
        Assert.True(view.Steps.Single(s => s.Label == "Chasser la Team Galaxie du bâtiment de Vestigion").Done);
        Assert.False(view.Steps.Single(s => s.Label == "Ouvrir l'entrepôt Galaxie (Clé Stockage)").Done);
        Assert.Equal(30, view.Progress.Sections.Single(s => s.Section == CompletionSection.Pokedex).Done);
        Assert.Equal(126, view.Progress.Sections.Single(s => s.Section == CompletionSection.Trainers).Done);
        // Les dresseurs qui ne viennent qu'à certaines heures (policiers de la Route 212…) ne sont pas ratés
        Assert.DoesNotContain(view.Steps, s => s.State == CompletionState.Missed);
        Assert.InRange(view.Progress.Tenths, 1, 999);

        // SoulSilver : 2 badges de Johto, la Team Rocket chassée du Puits Ramoloss, l'Œuf de Togepi reçu
        var soulSilver = candidates.Single(c => c.Label.Contains("soulsilver.sav"));
        Assert.Equal([LivingDexGame.SoulSilver], soulSilver.Games);
        var ss = (await _completions.GetAsync("sacha", (await _completions.CreateAsync("sacha", soulSilver.SaveId, LivingDexGame.SoulSilver)).Id!.Value))!;
        Assert.Equal(2, ss.Steps.Count(s => s.Done && s.Label.StartsWith("Badge ", StringComparison.Ordinal)));
        Assert.True(ss.Steps.Single(s => s.Label == "Chasser la Team Rocket du Puits Ramoloss").Done);
        Assert.True(ss.Steps.Single(s => s.Species == 175 && s.Section == CompletionSection.Encounters).Done);
        Assert.Equal(32, ss.Progress.Sections.Single(s => s.Section == CompletionSection.Trainers).Done);
    }
}
