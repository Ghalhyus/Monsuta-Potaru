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
        string[] badges = ["Badge Charbon (Pierrick)", "Badge Forêt (Flo)", "Badge Pavé (Mélina)", "Badge Palustre (Lovis)"];
        Assert.Equal([true, true, true, false], badges.Select(l => view.Steps.Single(s => s.Label == l).Done));
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

    [Fact]
    public void Ribbons_SinnohContestsBelowMaster_TowerAndEffort()
    {
        static List<CompletionRow> Ribbons(LivingDexGame game) =>
            [.. Gen4Completion.Rows(game).Where(r => r.Done.StartsWith("RB:", StringComparison.Ordinal))];
        // Normal, Méga, Ultra (le rang Master a déjà son drapeau), six Aptitude, Effort ; pas de concours en HG / SS
        Assert.Equal(22, Ribbons(LivingDexGame.Diamond).Count);
        Assert.Equal(22, Ribbons(LivingDexGame.Platinum).Count);
        Assert.Equal(7, Ribbons(LivingDexGame.SoulSilver).Count);
        Assert.DoesNotContain(Ribbons(LivingDexGame.Platinum), r => r.Label!.EndsWith("Master", StringComparison.Ordinal));
        Assert.Equal("Ébènelle", Ribbons(LivingDexGame.HeartGold).Single(r => r.Done == "RB:Effort").Zone);

        var sav = new SAV4Pt();
        sav.SetBoxSlotAtIndex(new PK4 { Species = 387, RibbonG4Cool = true, RibbonG4CoolGreat = true, RibbonAbility = true }, 0, 0);
        bool Done(string label) => Gen4Completion.Steps(sav, LivingDexGame.Platinum).Single(s => s.Label == label).Done;
        Assert.True(Done("Obtenir le Ruban Sang-Froid Méga"));
        Assert.False(Done("Obtenir le Ruban Sang-Froid Ultra"));
        Assert.True(Done("Obtenir le Ruban Aptitude"));
        Assert.False(Done("Obtenir le Ruban Aptitude Méga"));
        Assert.False(Done("Obtenir le Ruban Effort (510 EV)"));
    }

    private static List<CompletionRow> Pokeathlon(LivingDexGame game) =>
        [.. Gen4Completion.Rows(game).Where(r => r.Done.StartsWith("PA:", StringComparison.Ordinal))];

    [Fact]
    public void Pokeathlon_77StepsInHeartGoldSoulSilverOnly()
    {
        // 5 parcours, 10 + 13 + 12 + 10 trophées et objets des quatre salles, 27 Cartes Mémo
        var rows = Pokeathlon(LivingDexGame.SoulSilver);
        Assert.Equal(77, rows.Count);
        string[] details = ["parcours", "Salle de la Coopération", "Salle de la Confiance", "Salle du Potentiel", "Salle de l'Amitié",
            "Cartes Mémo"];
        Assert.Equal([5, 10, 13, 12, 10, 27], details.Select(d => rows.Count(r => r.Detail == d)));
        Assert.All(rows, r => Assert.Equal((CompletionSection.Quests, "Dôme Pokéathlon"), (r.Section, r.Zone)));
        Assert.Equal(rows.Count, rows.Select(r => r.Label).Distinct().Count());
        Assert.Equal(rows.Count, rows.Select(r => r.Done).Distinct().Count());
        Assert.Contains(rows, r => r.Label == "Trophée : 4 500 points au total");
        Assert.Contains(rows, r => r.Label == "Obtenir la Carte Mémo 27");
        // Les dernières quêtes d'HG / SS ; ni Diamant, ni Perle, ni Platine
        Assert.Equal(rows, Gen4Completion.Rows(LivingDexGame.HeartGold).TakeLast(77));
        Assert.Empty(Pokeathlon(LivingDexGame.Diamond));
        Assert.Empty(Pokeathlon(LivingDexGame.Platinum));
    }

    [Fact]
    public void Pokeathlon_InMemory_MedalsScoresCountersRecordsAndCards()
    {
        var sav = new SAV4HGSS();
        bool Done(string label) => Gen4Completion.Steps(sav, LivingDexGame.HeartGold).Single(s => s.Label == label).Done;
        var labels = Pokeathlon(LivingDexGame.HeartGold).Select(r => r.Label).ToHashSet();
        int DoneCount() => Gen4Completion.Steps(sav, LivingDexGame.HeartGold).Count(s => s.Done && labels.Contains(s.Label));
        // Une sauvegarde vierge (zéros partout, records compris) : rien de fait, pas même le Cours'Obstacles « en 0 s »
        Assert.Equal(0, DoneCount());

        var athlon = sav.Pokeathlon;
        athlon.Medals.AwardMedal(25, 0b00001); // Pikachu : Vitesse
        athlon.Medals.AwardMedal(152, PokeathlonMedalManager4.MaxMedalBits); // Germignon : les 5
        Assert.True(Done("Gagner le parcours Vitesse"));
        Assert.True(Done("Gagner le parcours Saut"));
        Assert.True(Done("Trophée : un Pokémon aux 5 médailles"));
        Assert.False(Done("Trophée : 2 Pokémon aux 5 médailles"));

        var speed = athlon.GetCourseRecord(PokeathlonStat4.Speed);
        speed.ScoreMax = 430;
        Assert.True(Done("Trophée : parcours Vitesse à 420 points"));
        Assert.False(Done("Trophée : parcours Vitesse à 450 points"));
        Assert.False(Done("Trophée : parcours Force à 420 points"));

        var counters = athlon.GlobalCounters;
        counters.Dashed = 5000;
        counters.Switched = 199;
        for (var e = PokeathlonEvent4.HurdleDash; e < PokeathlonEvent4.Count; e++)
            counters[e] = e == PokeathlonEvent4.GoalRoll ? 49u : 50u;
        Assert.True(Done("Chaussures : 5 000 sprints"));
        Assert.False(Done("Drapeau : 200 changements de Pokémon"));
        Assert.False(Done("Poké Ball : 50 premières places dans chaque épreuve")); // Balle au But : 49
        counters[PokeathlonEvent4.GoalRoll] = 50;
        Assert.True(Done("Poké Ball : 50 premières places dans chaque épreuve"));

        // Records : Cours'Obstacles en images (2 400 = 80 s), plus bas c'est mieux ; les autres, plus haut
        static void SetRecord(Pokeathlon4 athlon, PokeathlonEvent4 e, ushort value)
        {
            var data = athlon.GetEventSelf(e);
            data.Attempts = 1;
            var best = data.GetRecord(0);
            best.Record = value;
            for (var i = 1; i < PokeathlonEventData4.MaxRecord; i++)
            {
                var other = data.GetRecord(i);
                other.Record = ushort.MaxValue;
            }
        }
        SetRecord(athlon, PokeathlonEvent4.HurdleDash, 2500);
        SetRecord(athlon, PokeathlonEvent4.BlockSmash, 130);
        Assert.False(Done("Trophée : Cours'Obstacles en 80 s ou moins"));
        Assert.True(Done("Trophée : Casse Roc 130 rocs"));
        SetRecord(athlon, PokeathlonEvent4.HurdleDash, 2400);
        Assert.True(Done("Trophée : Cours'Obstacles en 80 s ou moins"));

        athlon.FlagsDataCard = 0b101; // Cartes Mémo 01 et 03
        Assert.True(Done("Obtenir la Carte Mémo 01"));
        Assert.False(Done("Obtenir la Carte Mémo 02"));
        Assert.True(Done("Obtenir la Carte Mémo 03"));

        // Amitié : le total (meilleurs scores des parcours et des épreuves, médailles) passe 3 000
        Assert.False(Done("Trophée : 3 000 points au total"));
        for (var c = PokeathlonStat4.Speed; c < PokeathlonStat4.Count; c++)
        {
            var course = athlon.GetCourseRecord(c);
            course.ScoreMax = 600;
        }
        Assert.True(Done("Trophée : 3 000 points au total"));
        Assert.False(Done("Trophée : 3 100 points au total"));
    }

    [Fact]
    public async Task RealHeartGold_PokeathlonSpeedCourseWon()
    {
        Assert.True((await _saves.AddAsync("sacha", "heartgold.sav", Real("heartgold.sav"))).Ok);
        var candidate = Assert.Single(await _completions.ListCandidatesAsync("sacha"));
        Assert.Equal([LivingDexGame.HeartGold], candidate.Games);
        var view = (await _completions.GetAsync("sacha",
            (await _completions.CreateAsync("sacha", candidate.SaveId, LivingDexGame.HeartGold)).Id!.Value))!;
        var labels = Pokeathlon(LivingDexGame.HeartGold).Select(r => r.Label).ToHashSet();
        var athlon = view.Steps.Where(s => s.Section == CompletionSection.Quests && labels.Contains(s.Label)).ToList();
        Assert.Equal(77, athlon.Count);
        // Une partie gagnée au parcours Vitesse (386 points ; Cours'Obstacles en 100,6 s, Course de Relais à 14,2 tours)
        Assert.Equal(["Gagner le parcours Vitesse"], athlon.Where(s => s.Done).Select(s => s.Label));
    }
}
