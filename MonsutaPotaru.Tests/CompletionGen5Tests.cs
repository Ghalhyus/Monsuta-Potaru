using Microsoft.Extensions.Logging.Abstractions;
using PKHeX.Core;
using MonsutaPotaru.Data;
using MonsutaPotaru.Services;

namespace MonsutaPotaru.Tests;

/// <summary>
/// Complétion de la 5e génération : tables de Noir / Blanc (lues dans la ROM de Pokémon Version Noire) et de Noir 2 /
/// Blanc 2 (générées d'après pokebw2), drapeaux nommés de FlagsEditorEX, check-lists de l'utilisateur ; Pokédex national ;
/// lecture des vraies sauvegardes de Noir et de Noir 2 ; Blanc 2 en mémoire.
/// </summary>
public sealed class CompletionGen5Tests : IDisposable
{
    private readonly TestDatabase _db = new("sacha", "ondine", "pierre");
    private readonly StoredSaveService _saves;
    private readonly CompletionService _completions;

    public CompletionGen5Tests()
    {
        _saves = _db.StoredSaves();
        _completions = new CompletionService(_db.Factory, NullLogger<CompletionService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    private static IEnumerable<int> Events(string condition) =>
        condition.Split('|', '+').Where(a => a.StartsWith("E:", StringComparison.Ordinal)).Select(a => int.Parse(a[2..]));

    private static byte[] Real(string file) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Sauvegardes", file));

    private static CompletionRow[] Table(string game) => (CompletionRow[])typeof(Gen5CompletionData).GetField(game)!.GetValue(null)!;

    [Theory]
    [InlineData("Black", 30, 327, 483, 24, 106)]
    [InlineData("White", 30, 327, 513, 24, 106)]
    [InlineData("Black2", 28, 416, 631, 41, 389)]
    [InlineData("White2", 28, 417, 631, 41, 389)]
    public void Tables_SixSections_AndConditionsInsideTheSave(string game, int story, int trainers, int items, int encounters,
        int quests)
    {
        var rows = Table(game);
        Assert.DoesNotContain(rows, r => r.Section == CompletionSection.Pokedex);
        Assert.Equal([story, trainers, items, encounters, quests],
            new[] { CompletionSection.Story, CompletionSection.Trainers, CompletionSection.Items, CompletionSection.Encounters, CompletionSection.Quests }
                .Select(s => rows.Count(r => r.Section == s)));
        Assert.Equal(8, rows.Count(r => r.Label?.StartsWith("Badge ", StringComparison.Ordinal) == true));
        var flags = game.EndsWith('2') ? 0xBF8 : 0xB60; // drapeaux de PKHeX (EventWork5B2W2, EventWork5BW)
        foreach (var atom in rows.SelectMany(r => new[] { r.Done, r.Missed }).OfType<string>().SelectMany(c => c.Split('|', '+')))
        {
            var parts = atom.Split(':');
            switch (parts[0])
            {
                case "E": Assert.InRange(int.Parse(parts[1]), 1, flags - 1); break;
                case "D": Assert.InRange(int.Parse(parts[1]), 1, 649); break;
                case "B": Assert.InRange(int.Parse(parts[1]), 0, 7); break;
                case "K": Assert.InRange(int.Parse(parts[1]), 1, 638); break;
                case "PW": Assert.InRange(int.Parse(parts[1]), 0, 28); break;
                case "MD": Assert.InRange(int.Parse(parts[1]), 0, 253); break;
                case "PR": Assert.InRange(int.Parse(parts[1]), 0, 99); break;
                case "SB" or "N": break;
                default: Assert.Fail($"Condition inattendue en 5e génération : {atom}"); break;
            }
        }
        // Chaque dresseur a son équipe ; chaque Pokémon est un vrai numéro
        Assert.All(rows.Where(r => r.Section == CompletionSection.Trainers), r => Assert.NotNull(r.Team));
        Assert.All(rows.Where(r => r.Species != 0), r => Assert.InRange(r.Species, 1, 649));
        // Dresseurs et objets : chaque lieu en un seul bloc
        foreach (var section in new[] { CompletionSection.Trainers, CompletionSection.Items })
        {
            var zones = rows.Where(r => r.Section == section).Select(r => r.Zone).ToList();
            var blocks = zones.Where((z, i) => i == 0 || zones[i - 1] != z).ToList();
            Assert.Equal(blocks.Count, blocks.Distinct().Count());
        }
        // Un drapeau = un objet, un dresseur = une étape
        foreach (var section in new[] { CompletionSection.Trainers, CompletionSection.Items })
        {
            var done = rows.Where(r => r.Section == section).Select(r => r.Done).ToList();
            Assert.Equal(done.Count, done.Distinct().Count());
        }
    }

    [Fact]
    public void Labels_MachinesItemsVersionsAndMedals()
    {
        Assert.Equal("CT01 Aiguisage", Gen5Completion.Label(new(CompletionSection.Items, "Route 1", null, "E:1", Tm: 1)));
        Assert.Equal("CT95 Aboiement", Gen5Completion.Label(new(CompletionSection.Items, "Route 1", null, "E:1", Tm: 95)));
        Assert.Equal("CS06 Plongée", Gen5Completion.Label(new(CompletionSection.Items, "Route 1", null, "E:1", Tm: 101)));
        Assert.Equal("Potion", Gen5Completion.ItemName(17));
        // La Forêt Blanche dans Blanc seulement, la Ville Noire dans Noir seulement
        Assert.Contains(Gen5CompletionData.White, r => r.Zone == "Forêt Blanche" && r.Section == CompletionSection.Items);
        Assert.DoesNotContain(Gen5CompletionData.Black, r => r.Zone == "Forêt Blanche" && r.Section == CompletionSection.Items);
        Assert.DoesNotContain(Gen5CompletionData.White, r => r.Zone == "Ville Noire" && r.Section == CompletionSection.Items);
        // Distribution réelle : Victini (Pass Liberté) en Noir / Blanc ; dragon de la version
        Assert.Contains(Gen5CompletionData.Black, r => r.Species == 494 && r.Detail == "Pass Liberté");
        Assert.Contains(Gen5CompletionData.Black, r => r.Species == 643 && r.Section == CompletionSection.Encounters);
        Assert.Contains(Gen5CompletionData.White, r => r.Species == 644 && r.Section == CompletionSection.Encounters);
        // Choix exclusif : une seule case pour le fossile
        Assert.Single(Gen5CompletionData.Black, r => r.Label == "Fossile Plaque ou Fossile Plume");
        // 100 parures dans les quatre jeux ; Noire 2 / Blanche 2 : 254 médailles, 29 tournois du PWT, 6 lignes du Métro
        Assert.All(new[] { Gen5CompletionData.Black, Gen5CompletionData.White2 },
            rows => Assert.Equal(100, rows.Count(r => r.Done.StartsWith("PR:", StringComparison.Ordinal))));
        Assert.Equal("Nœud Rose", Gen5CompletionData.Black.Single(r => r.Done == "PR:0").Label);
        Assert.Equal("Premiers Pas", Gen5CompletionData.Black2.Single(r => r.Done == "MD:0").Label);
        Assert.Equal(254, Gen5CompletionData.Black2.Count(r => r.Done.StartsWith("MD:", StringComparison.Ordinal)));
        Assert.Equal(29, Gen5CompletionData.Black2.Count(r => r.Done.StartsWith("PW:", StringComparison.Ordinal)));
        Assert.Equal(6, Gen5CompletionData.Black.Count(r => r.Done.StartsWith("SB:", StringComparison.Ordinal)));
        // Dresseurs d'une seule version (Route Victoire) : jamais « ratés »
        Assert.DoesNotContain(Gen5CompletionData.Black2, r => r.Missed is not null);
    }

    [Fact]
    public void White2_InMemory_BadgesItemsMedalsPwtSubwayAndNationalDex()
    {
        var sav = new SAV5B2W2 { Version = GameVersion.W2 };
        Assert.Equal([LivingDexGame.White2], CompletionGames.For(sav));
        var empty = Gen5Completion.Steps(sav, LivingDexGame.White2);
        var progress = CompletionProgress.Of(empty);
        Assert.Equal((6, 0), (progress.Sections.Count, progress.Tenths));
        Assert.Equal(649, empty.Count(s => s.Section == CompletionSection.Pokedex));
        Assert.Equal("#649 Genesect", empty.Last(s => s.Section == CompletionSection.Pokedex).Label);

        sav.Misc.Badges = 0b1; // Badge Basique
        sav.Zukan.IsNationalDexUnlocked = true;
        var rows = Gen5CompletionData.White2;
        foreach (var flag in Events(rows.Single(r => r.Label == "Recevoir le Pokédex").Done))
            sav.EventWork.SetEventFlag(flag, true);
        SaveSessionTests.GiveItems(sav, (450, 1)); // Bicyclette
        sav.Medals[0].Obtain(new DateOnly(2012, 6, 23));
        sav.Musical.SetHasProp(1); // Nœud Bleu
        sav.PWT.SetPWTRecord(PWTRecordID.Driftveil, 1);
        sav.BattleSubway.SuperSingle = true;
        sav.BattleSubway.SuperDoubleRecord = 49;

        var steps = Gen5Completion.Steps(sav, LivingDexGame.White2);
        Assert.True(steps.Single(s => s.Label == "Badge Basique (Tcheren)").Done);
        Assert.False(steps.Single(s => s.Label == "Badge Toxique (Strykna)").Done);
        Assert.True(steps.Single(s => s.Label == "Recevoir le Pokédex").Done);
        Assert.True(steps.Single(s => s.Label == "Recevoir le Pokédex National").Done);
        Assert.True(steps.Single(s => s.Label == "Recevoir la Bicyclette").Done);
        Assert.True(steps.Single(s => s.Label == "Premiers Pas" && s.Zone == "Médailles").Done);
        Assert.True(steps.Single(s => s.Label == "Nœud Bleu" && s.Detail == "parure").Done);
        Assert.False(steps.Single(s => s.Label == "Nœud Rose" && s.Detail == "parure").Done);
        Assert.True(steps.Single(s => s.Label == "Remporter le Tournoi de Port Yoneuve").Done);
        Assert.True(steps.Single(s => s.Label == "Battre les Chefs du Métro (Ligne Simple)").Done);
        Assert.False(steps.Single(s => s.Label == "Battre les Chefs du Métro (Ligne Super Simple)").Done);
        Assert.True(steps.Single(s => s.Label == "Battre les Chefs du Métro (Ligne Super Double)").Done);

        // Un dresseur battu : son drapeau (0x5F0 + son numéro)
        var trainer = rows.First(r => r.Section == CompletionSection.Trainers);
        sav.EventWork.SetEventFlag(Events(trainer.Done).First(), true);
        Assert.Equal(1, Gen5Completion.Steps(sav, LivingDexGame.White2).Count(s => s.Done && s.Section == CompletionSection.Trainers));
    }

    [Fact]
    public async Task RealBlack_AndBlack2_ProgressReadInTheirFlags()
    {
        foreach (var file in new[] { "noir.sav", "noire-2.sav" })
            Assert.True((await _saves.AddAsync("sacha", file, Real(file))).Ok);
        var candidates = await _completions.ListCandidatesAsync("sacha");
        Assert.Equal(2, candidates.Count);

        // Noir : partie finie (Ghetis battu, Conseil 4 battu)
        var black = candidates.Single(c => c.Label.Contains("noir.sav"));
        Assert.Equal([LivingDexGame.Black], black.Games);
        var view = (await _completions.GetAsync("sacha", (await _completions.CreateAsync("sacha", black.SaveId, LivingDexGame.Black)).Id!.Value))!;
        Assert.Equal(8, view.Steps.Count(s => s.Done && s.Label.StartsWith("Badge ", StringComparison.Ordinal)));
        Assert.True(view.Steps.Single(s => s.Label == "Battre N et Ghetis").Done);
        Assert.True(view.Steps.Single(s => s.Label == "Battre Anis (Conseil 4)").Done);
        Assert.Equal(20, view.Steps.Count(s => s.Done && s.Detail == "parure")); // 20 parures dans la Boîte Parure
        Assert.InRange(view.Progress.Tenths, 1, 999);

        // Noir 2 : début de partie
        var black2 = candidates.Single(c => c.Label.Contains("noire-2.sav"));
        Assert.Equal([LivingDexGame.Black2], black2.Games);
        var b2 = (await _completions.GetAsync("sacha", (await _completions.CreateAsync("sacha", black2.SaveId, LivingDexGame.Black2)).Id!.Value))!;
        Assert.True(b2.Steps.Single(s => s.Label == "Recevoir le Pokédex").Done);
        Assert.False(b2.Steps.Single(s => s.Label == "Battre la Maîtresse Iris et entrer au Panthéon").Done);
        Assert.DoesNotContain(b2.Steps, s => s.State == CompletionState.Missed);
        Assert.InRange(b2.Progress.Tenths, 1, 999);
    }
}
