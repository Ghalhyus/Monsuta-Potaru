using Microsoft.Extensions.Logging.Abstractions;
using PKHeX.Core;
using MonsutaPotaru.Data;
using MonsutaPotaru.Services;

namespace MonsutaPotaru.Tests;

/// <summary>
/// Complétion de la 3e génération : tables de Rubis / Saphir, d'Émeraude et de Rouge Feu / Vert Feuille (générées d'après
/// pokeruby, pokeemerald, pokefirered et les check-lists de l'utilisateur), Pokédex national, lecture des vraies sauvegardes
/// de Rouge Feu et de Vert Feuille ; Émeraude et Rubis / Saphir en mémoire.
/// </summary>
public sealed class CompletionGen3Tests : IDisposable
{
    private readonly TestDatabase _db = new("sacha", "ondine", "pierre");
    private readonly StoredSaveService _saves;
    private readonly CompletionService _completions;

    public CompletionGen3Tests()
    {
        _saves = _db.StoredSaves();
        _completions = new CompletionService(_db.Factory, NullLogger<CompletionService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    private static IEnumerable<int> Events(string condition) =>
        condition.Split('|', '+').Where(a => a.StartsWith("E:", StringComparison.Ordinal)).Select(a => int.Parse(a[2..]));

    [Theory]
    [InlineData("RubySapphire", 27, 321, 311, 19, 10)]
    [InlineData("Emerald", 30, 495, 310, 26, 34)]
    [InlineData("FireRedLeafGreen", 20, 438, 318, 28, 16)]
    public void Tables_SixSections_AndConditionsInsideTheSave(string game, int story, int trainers, int items, int encounters, int quests)
    {
        var rows = (CompletionRow[])typeof(Gen3CompletionData).GetField(game)!.GetValue(null)!;
        Assert.DoesNotContain(rows, r => r.Section == CompletionSection.Pokedex);
        Assert.Equal([story, trainers, items, encounters, quests],
            new[] { CompletionSection.Story, CompletionSection.Trainers, CompletionSection.Items, CompletionSection.Encounters, CompletionSection.Quests }
                .Select(s => rows.Count(r => r.Section == s)));
        Assert.Equal(8, rows.Count(r => r.Label?.StartsWith("Badge ", StringComparison.Ordinal) == true));
        foreach (var atom in rows.SelectMany(r => new[] { r.Done, r.Missed }).OfType<string>().SelectMany(c => c.Split('|', '+')))
        {
            var parts = atom.Split(':');
            switch (parts[0])
            {
                case "E": Assert.InRange(int.Parse(parts[1]), 0, game == "Emerald" ? 2399 : 2303); break; // nombre de drapeaux de PKHeX
                case "D": Assert.InRange(int.Parse(parts[1]), 1, 386); break;
                default: Assert.Fail($"Condition inattendue en 3e génération : {atom}"); break;
            }
        }
        // Chaque dresseur a son équipe (pas « Dresseur ») ; seul le rival, sous plusieurs identifiants, n'en montre pas
        Assert.All(rows.Where(r => r.Section == CompletionSection.Trainers),
            r => Assert.True(r.Team is not null || r.Label == "Rival", $"{r.Zone} : {r.Done}"));
        // Dresseurs et objets : chaque lieu en un seul bloc (« Route 1 » ne se range pas avec « Route 10 », deux lieux au
        // même rang de la check-list ne s'entremêlent pas)
        foreach (var section in new[] { CompletionSection.Trainers, CompletionSection.Items })
        {
            var zones = rows.Where(r => r.Section == section).Select(r => r.Zone).ToList();
            var blocks = zones.Where((z, i) => i == 0 || zones[i - 1] != z).ToList();
            Assert.Equal(blocks.Count, blocks.Distinct().Count());
        }
        // Numéros nationaux (pas ceux du jeu) : aucun au-delà de 386
        Assert.All(rows.Where(r => r.Species != 0), r => Assert.InRange(r.Species, 1, 386));
        Assert.All(rows.Where(r => r.Team is not null).SelectMany(r => r.Team!.Split(',')), m => Assert.InRange(int.Parse(m.Split(':')[0]), 1, 386));
    }

    [Fact]
    public void Labels_MachinesNamesAndDistributions()
    {
        Assert.Equal("CT01 Mitra-Poing", Gen3Completion.Label(new(CompletionSection.Items, "Route 115", null, "E:1", Tm: 1)));
        Assert.Equal("CS08 Plongée", Gen3Completion.Label(new(CompletionSection.Items, "Algatia", null, "E:1", Tm: 58)));
        Assert.Equal("Potion", Gen3Completion.ItemName(13));
        var emerald = Gen3CompletionData.Emerald;
        // Distributions comptées (choix de l'utilisateur) ; symboles de la Zone de Combat ; tuteurs
        Assert.Contains(emerald, r => r.Species == 386 && r.Detail == "Ticket Aurora");
        Assert.Contains(emerald, r => r.Species == 151 && r.Detail == "Vieille Carte");
        Assert.Equal(14, emerald.Count(r => r.Label?.StartsWith("Symbole ", StringComparison.Ordinal) == true));
        Assert.Equal(10, emerald.Count(r => r.Detail == "Maître des capacités"));
        Assert.Equal(16, Gen3CompletionData.FireRedLeafGreen.Count(r => r.Detail == "Maître des capacités"));
        Assert.DoesNotContain(Gen3CompletionData.RubySapphire, r => r.Label?.StartsWith("Symbole ", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void Emerald_InMemory_BadgesSymbolsAndNationalDex()
    {
        var sav = new SAV3E();
        var empty = Gen3Completion.Steps(sav, LivingDexGame.Emerald);
        var progress = CompletionProgress.Of(empty);
        Assert.Equal((6, 0), (progress.Sections.Count, progress.Tenths));
        Assert.Equal(386, empty.Count(s => s.Section == CompletionSection.Pokedex));
        Assert.Equal("#386 Deoxys", empty.Last(s => s.Section == CompletionSection.Pokedex).Label);

        var rows = Gen3CompletionData.Emerald;
        foreach (var label in new[] { "Badge Roche (Roxanne)", "Symbole Capacité d'Or" })
            foreach (var flag in Events(rows.Single(r => r.Label == label).Done))
                sav.SetEventFlag(flag, true);
        sav.SetCaught(252, true); // Arcko, numéro national
        var steps = Gen3Completion.Steps(sav, LivingDexGame.Emerald);
        Assert.True(steps.Single(s => s.Label == "Badge Roche (Roxanne)").Done);
        Assert.True(steps.Single(s => s.Label == "Symbole Capacité d'Or").Done);
        Assert.False(steps.Single(s => s.Label == "Symbole Capacité d'Argent").Done);
        Assert.True(steps.Single(s => s.Label == "#252 Arcko").Done);

        // Un dresseur battu : son drapeau (début des drapeaux de dresseurs + son numéro)
        var trainer = rows.First(r => r.Section == CompletionSection.Trainers && r.Team is not null);
        sav.SetEventFlag(Events(trainer.Done).First(), true);
        Assert.Equal(1, Gen3Completion.Steps(sav, LivingDexGame.Emerald).Count(s => s.Done && s.Section == CompletionSection.Trainers));
    }

    [Fact]
    public void RubySapphire_InMemory_LegendariesReadByTheirHideFlag()
    {
        var sav = new SAV3RS();
        Assert.Equal([LivingDexGame.Ruby, LivingDexGame.Sapphire], CompletionGames.For(sav));
        var regirock = Gen3CompletionData.RubySapphire.Single(r => r.Species == 377);
        sav.SetEventFlag(Events(regirock.Done).Single(), true);
        Assert.True(Gen3Completion.Steps(sav, LivingDexGame.Ruby).Single(s => s.Species == 377 && s.Section == CompletionSection.Encounters).Done);
    }

    [Fact]
    public async Task RealLeafGreen_AndFireRed_ProgressReadInTheirFlags()
    {
        foreach (var file in new[] { "vert-feuille.sav", "rouge-feu.sav" })
            Assert.True((await _saves.AddAsync("sacha", file, File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Sauvegardes", file)))).Ok);
        var candidates = await _completions.ListCandidatesAsync("sacha");
        Assert.Equal(2, candidates.Count);
        Assert.All(candidates, c => Assert.Equal([LivingDexGame.FireRed, LivingDexGame.LeafGreen], c.Games));

        var leafGreen = candidates.Single(c => c.Label.Contains("vert-feuille.sav"));
        var id = (await _completions.CreateAsync("sacha", leafGreen.SaveId, LivingDexGame.LeafGreen)).Id!.Value;
        var view = (await _completions.GetAsync("sacha", id))!;
        Assert.Equal([(6, 20), (29, 386), (84, 438), (39, 318), (2, 28), (0, 16)], view.Progress.Sections.Select(s => (s.Done, s.Total)));
        Assert.Equal("12,6 %", view.Progress.Percent);
        Assert.True(view.Steps.Single(s => s.Label == "Recevoir le Pokédex").Done);
        Assert.DoesNotContain(view.Steps, s => s.State == CompletionState.Missed);

        // Rouge Feu, tout au début : les objets cachés qui réapparaissent au hasard (drapeaux levés d'office) ne comptent pas
        var fireRed = candidates.Single(c => c.Label.Contains("rouge-feu.sav"));
        var red = (await _completions.GetAsync("sacha", (await _completions.CreateAsync("sacha", fireRed.SaveId, LivingDexGame.FireRed)).Id!.Value))!;
        Assert.Equal(5, red.Progress.Sections.Single(s => s.Section == CompletionSection.Items).Done);
    }
}
