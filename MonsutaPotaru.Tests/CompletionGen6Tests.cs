using Microsoft.Extensions.Logging.Abstractions;
using PKHeX.Core;
using MonsutaPotaru.Data;
using MonsutaPotaru.Services;

namespace MonsutaPotaru.Tests;

/// <summary>
/// Complétion de la 6e génération : tables de X / Y et de Rubis Oméga / Saphir Alpha (générées d'après les listes de
/// drapeaux de FlagsEditorEX, les check-lists de l'utilisateur et les ROM pour les dresseurs), Pokédex national ;
/// lecture des vraies sauvegardes de Rubis Oméga et de Y ; Y aussi en mémoire.
/// </summary>
public sealed class CompletionGen6Tests : IDisposable
{
    private readonly TestDatabase _db = new("sacha", "ondine", "pierre");
    private readonly StoredSaveService _saves;
    private readonly CompletionService _completions;

    public CompletionGen6Tests()
    {
        _saves = _db.StoredSaves();
        _completions = new CompletionService(_db.Factory, NullLogger<CompletionService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    private static IEnumerable<int> Events(string condition) =>
        condition.Split('|', '+').Where(a => a.StartsWith("E:", StringComparison.Ordinal)).Select(a => int.Parse(a[2..]));

    private static byte[] Real(string file) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Sauvegardes", file));

    private static CompletionRow[] Table(string game) => (CompletionRow[])typeof(Gen6CompletionData).GetField(game)!.GetValue(null)!;

    [Theory]
    [InlineData("X", 40, 261, 476, 15, 115, 105)]
    [InlineData("Y", 40, 261, 476, 15, 115, 105)]
    [InlineData("OmegaRuby", 37, 375, 515, 49, 48, 107)]
    [InlineData("AlphaSapphire", 37, 374, 515, 49, 48, 107)]
    public void Tables_SixSections_AndConditionsInsideTheSave(string game, int story, int trainers, int items, int encounters,
        int quests, int machines)
    {
        var rows = Table(game);
        Assert.DoesNotContain(rows, r => r.Section == CompletionSection.Pokedex);
        Assert.Equal([story, trainers, items, encounters, quests],
            new[] { CompletionSection.Story, CompletionSection.Trainers, CompletionSection.Items, CompletionSection.Encounters,
                CompletionSection.Quests }.Select(s => rows.Count(r => r.Section == s)));
        Assert.Equal(8, rows.Count(r => r.Label?.StartsWith("Badge ", StringComparison.Ordinal) == true));
        foreach (var atom in rows.Select(r => r.Done).SelectMany(c => c.Split('|', '+')))
        {
            var parts = atom.Split(':');
            switch (parts[0])
            {
                case "E": Assert.InRange(int.Parse(parts[1]), 0x60, 0xCFF); break;
                case "W": Assert.InRange(int.Parse(parts[1]), 0x30, 0x1FF); break;
                case "D": Assert.InRange(int.Parse(parts[1]), 1, 721); break;
                case "B": Assert.InRange(int.Parse(parts[1]), 0, 7); break;
                case "K" or "H": Assert.InRange(int.Parse(parts[1]), 1, 775); break;
                case "ST": Assert.InRange(int.Parse(parts[1]), 0, 29); break;
                default: Assert.Fail($"Condition inattendue en 6e génération : {atom}"); break;
            }
        }
        Assert.All(rows.Where(r => r.Species != 0), r => Assert.InRange(r.Species, 1, 721));
        // Objets et dresseurs : chaque lieu en un seul bloc ; un drapeau = un objet, un dresseur ; chaque CT et CS une fois
        foreach (var section in new[] { CompletionSection.Items, CompletionSection.Trainers })
        {
            var zones = rows.Where(r => r.Section == section).Select(r => r.Zone).ToList();
            var blocks = zones.Where((z, i) => i == 0 || zones[i - 1] != z).ToList();
            Assert.Equal(blocks.Count, blocks.Distinct().Count());
            var done = rows.Where(r => r.Section == section).Select(r => r.Done).ToList();
            Assert.Equal(done.Count, done.Distinct().Count());
        }
        // Dresseurs : drapeau 0x6CC + numéro, une équipe lisible (espèces nationales, niveaux)
        foreach (var trainer in rows.Where(r => r.Section == CompletionSection.Trainers))
        {
            Assert.InRange(int.Parse(trainer.Done[2..]), 0x6CD, 0x6CC + 949);
            Assert.All(trainer.Team!.Split(',').Select(m => m.Split(':').Select(int.Parse).ToArray()), m =>
            {
                Assert.InRange(m[0], 1, 721);
                Assert.InRange(m[1], 1, 100);
            });
        }
        Assert.Equal(Enumerable.Range(1, machines), rows.Where(r => r.Tm != 0).Select(r => r.Tm).Order());
    }

    [Fact]
    public void Labels_MachinesItemsVersionsAndQuests()
    {
        var xy = LearnSource6XY.MachineMoves.ToArray();
        var oras = LearnSource6AO.MachineMoves.ToArray();
        Assert.Equal("CT27 Retour", Gen6Completion.Label(new(CompletionSection.Items, "Route 4", null, "K:354", Tm: 27), xy));
        Assert.Equal("CT100 Confidence", Gen6Completion.Label(new(CompletionSection.Items, "Route 4", null, "K:1", Tm: 100), xy));
        Assert.Equal("CS01 Coupe", Gen6Completion.Label(new(CompletionSection.Items, "Route 4", null, "K:1", Tm: 101), xy));
        Assert.Equal("CS06 Éclate-Roc", Gen6Completion.Label(new(CompletionSection.Items, "Lavandia", null, "K:1", Tm: 106), oras));
        Assert.Equal("CS07 Plongée", Gen6Completion.Label(new(CompletionSection.Items, "Algatia", null, "K:1", Tm: 107), oras));
        Assert.Equal("Potion", Gen6Completion.ItemName(17));
        Assert.Equal("Altarite", Gen6Completion.ItemName(755));
        // Planques : celle de la version ; Méga-Gemmes de la version (Dracaufite X dans X seulement)
        Assert.Contains(Gen6CompletionData.OmegaRuby, r => r.Zone == "Planque Magma");
        Assert.DoesNotContain(Gen6CompletionData.OmegaRuby, r => r.Zone == "Planque Aqua");
        Assert.DoesNotContain(Gen6CompletionData.AlphaSapphire, r => r.Zone == "Planque Magma");
        Assert.Contains(Gen6CompletionData.X, r => r.Item == 660 && r.Done == "H:660");
        Assert.DoesNotContain(Gen6CompletionData.Y, r => r.Item == 660);
        // Latiasite et Latiosite : pas dans X / Y (échange depuis Rubis Oméga / Saphir Alpha)
        Assert.DoesNotContain(Gen6CompletionData.X, r => r.Item is 684 or 685);
        // Quêtes : Maison de Combat (10), Super Training (30), concours (8, Rubis Oméga / Saphir Alpha)
        Assert.Equal(10, Gen6CompletionData.X.Count(r => r.Zone == "Maison de Combat" && r.Section == CompletionSection.Quests));
        Assert.Equal(30, Gen6CompletionData.Y.Count(r => r.Done.StartsWith("ST:", StringComparison.Ordinal)));
        // Château de Combat (X / Y) : chaque noble une fois, les doublons (Marquise Violette) en « l'un ou l'autre »
        Assert.Equal(75, Gen6CompletionData.X.Count(r => r.Zone == "Château de Combat"));
        Assert.Equal($"E:{0x6CC + 254}|E:{0x6CC + 262}", Gen6CompletionData.Y.Single(r => r.Label == "Marquise Violette").Done);
        Assert.DoesNotContain(Gen6CompletionData.OmegaRuby, r => r.Zone == "Château de Combat");
        // Pokémon reçus par cadeau mystère : pas dans les rencontres, toujours dans le Pokédex (Volcanion, Jirachi)
        Assert.DoesNotContain(Gen6CompletionData.X, r => r.Section == CompletionSection.Encounters && r.Species == 721);
        Assert.DoesNotContain(Gen6CompletionData.OmegaRuby, r => r.Section == CompletionSection.Encounters && r.Species == 385);
        Assert.Equal(8, Gen6CompletionData.AlphaSapphire.Count(r => r.Zone == "Salles Concours Live"));
        // Objets cachés qui reviennent (Miel de la Route 12, 0x474) : jamais
        Assert.DoesNotContain(Gen6CompletionData.X, r => r.Done == "E:1140");
        // Dresseurs : ceux d'une arène avec son badge (Norman : 7 salles), les sbires de la version, pas les champions
        // (Roxanne : 561), ni le rival de X / Y (combat de Port Tempères : 184)
        Assert.Equal(7, Gen6CompletionData.OmegaRuby.Count(r => r.Zone == "Arène de Clémenti-Ville" && r.Section == CompletionSection.Trainers));
        Assert.Equal(3, Gen6CompletionData.X.Count(r => r.Zone == "Arène de Neuvartault"));
        Assert.Contains(Gen6CompletionData.OmegaRuby, r => r.Zone == "Planque Magma" && r.Section == CompletionSection.Trainers);
        Assert.Contains(Gen6CompletionData.AlphaSapphire, r => r.Zone == "Planque Aqua" && r.Section == CompletionSection.Trainers);
        Assert.DoesNotContain(Gen6CompletionData.OmegaRuby, r => r.Done == $"E:{0x6CC + 561}");
        Assert.DoesNotContain(Gen6CompletionData.X, r => r.Done == $"E:{0x6CC + 184}");
        Assert.Equal("Racaillou (niv. 10)", Gen6Completion.Label(Gen6CompletionData.OmegaRuby.First(r => r.Zone == "Arène de Mérouville"), oras));
    }

    [Fact]
    public void Y_InMemory_BadgesItemsWorksMegaStonesSuperTrainingAndMaison()
    {
        var sav = new SAV6XY { Version = GameVersion.Y };
        Assert.Equal([LivingDexGame.Y], CompletionGames.For(sav));
        var empty = Gen6Completion.Steps(sav, LivingDexGame.Y);
        var progress = CompletionProgress.Of(empty);
        Assert.Equal((6, 0), (progress.Sections.Count, progress.Tenths));
        Assert.Equal(721, empty.Count(s => s.Section == CompletionSection.Pokedex));
        Assert.Equal("#721 Volcanion", empty.Last(s => s.Section == CompletionSection.Pokedex).Label);

        sav.Badges = 0b1; // Badge Coléoptère
        var rows = Gen6CompletionData.Y;
        foreach (var flag in Events(rows.Single(r => r.Label == "Battre la Maîtresse Dianthéa et entrer au Panthéon").Done))
            sav.EventWork.SetEventFlag(flag, true);
        sav.EventWork.SetWork(0x078, 2); // ABC Aventure (WK_SCENE_R03)
        SaveSessionTests.GiveItems(sav, (643, 1), (339, 1)); // Rollers, CT12
        sav.SuperTrain.SetTime1(0, 42.5f);
        foreach (var flag in Events(rows.Single(r => r.Label == "Combat Solo : battre la Châtelaine (20e victoire)").Done))
            sav.EventWork.SetEventFlag(flag, true);
        // Gardevoirite tenue par un Pokémon d'une boîte : comptée
        var pk = new PK6 { Species = (ushort)Species.Gardevoir, HeldItem = 657 };
        sav.SetBoxSlotAtIndex(pk, 0, 0);

        var steps = Gen6Completion.Steps(sav, LivingDexGame.Y);
        Assert.True(steps.Single(s => s.Label == "Badge Coléoptère (Violette)").Done);
        Assert.False(steps.Single(s => s.Label == "Badge Mur (Lino)").Done);
        Assert.True(steps.Single(s => s.Label == "Battre la Maîtresse Dianthéa et entrer au Panthéon").Done);
        Assert.True(steps.Single(s => s.Label == "Battre Malva (Conseil 4)").Done); // Ligue déjà gagnée
        Assert.False(steps.First(s => s.Section == CompletionSection.Trainers).Done);
        Assert.True(steps.Single(s => s.Label == "Recevoir l'ABC Aventure").Done);
        Assert.True(steps.Single(s => s.Label == "Recevoir les Rollers").Done);
        Assert.True(steps.Single(s => s.Label == "CT12 Provoc").Done);
        Assert.False(steps.Single(s => s.Label == "CT13 Laser Glace").Done);
        Assert.True(steps.Single(s => s.Label == "Gardevoirite").Done);
        Assert.False(steps.Single(s => s.Label == "Absolite").Done);
        Assert.Equal(1, steps.Count(s => s.Done && s.Zone == "Super Training"));
        Assert.True(steps.Single(s => s.Label == "Combat Solo : battre la Châtelaine (20e victoire)").Done);
        Assert.False(steps.Single(s => s.Label == "Combat Super Solo : battre la Châtelaine (50e victoire)").Done);
        Assert.InRange(CompletionProgress.Of(steps).Tenths, 1, 999);
    }

    [Fact]
    public async Task RealOmegaRuby_ProgressReadInItsFlags()
    {
        Assert.True((await _saves.AddAsync("sacha", "rubis-omega.sav", Real("rubis-omega.sav"))).Ok);
        var candidate = Assert.Single(await _completions.ListCandidatesAsync("sacha"));
        Assert.Equal([LivingDexGame.OmegaRuby], candidate.Games);
        var view = (await _completions.GetAsync("sacha",
            (await _completions.CreateAsync("sacha", candidate.SaveId, LivingDexGame.OmegaRuby)).Id!.Value))!;

        // Partie finie : 8 badges, Ligue, Groudon, Épisode Delta, Deoxys ; Latios reçu à l'Île du Sud
        Assert.Equal(8, view.Steps.Count(s => s.Done && s.Label.StartsWith("Badge ", StringComparison.Ordinal)));
        Assert.True(view.Steps.Single(s => s.Label == "Battre le Maître Pierre Rochard et entrer au Panthéon").Done);
        Assert.True(view.Steps.Single(s => s.Label == "Capturer Primo-Groudon").Done);
        Assert.True(view.Steps.Single(s => s.Label == "Commencer l'Épisode Delta").Done);
        Assert.True(view.Steps.Single(s => s.Label == "Capturer Deoxys").Done);
        // Dresseurs : 291 sur 375, toutes les arènes ; pas encore les passagers du Marina ni les chenaux du sud
        var trainers = view.Steps.Where(s => s.Section == CompletionSection.Trainers).ToList();
        Assert.Equal((291, 375), (trainers.Count(s => s.Done), trainers.Count));
        Assert.All(trainers.Where(s => s.Zone.StartsWith("Arène ", StringComparison.Ordinal)), s => Assert.True(s.Done));
        Assert.DoesNotContain(trainers, s => s.Zone == "Le Marina" && s.Done);
        Assert.True(view.Steps.Single(s => s.Species == 381 && s.Section == CompletionSection.Encounters).Done);
        Assert.False(view.Steps.Single(s => s.Species == 380 && s.Section == CompletionSection.Encounters).Done);
        // Concours : premier rang Master gagné, pas encore les 4 catégories ; Super Training : 10 épreuves réussies
        Assert.True(view.Steps.Single(s => s.Label == "Remporter un premier concours de rang Master").Done);
        Assert.False(view.Steps.Single(s => s.Label == "Remporter le rang Master dans 4 catégories").Done);
        Assert.Equal(10, view.Steps.Count(s => s.Done && s.Zone == "Super Training"));
        Assert.DoesNotContain(view.Steps, s => s.State == CompletionState.Missed);
        Assert.InRange(view.Progress.Tenths, 1, 999);
    }

    [Fact]
    public async Task RealY_ProgressReadInItsFlags()
    {
        Assert.True((await _saves.AddAsync("sacha", "y.sav", Real("y.sav"))).Ok);
        var candidate = Assert.Single(await _completions.ListCandidatesAsync("sacha"));
        Assert.Equal([LivingDexGame.Y], candidate.Games);
        var view = (await _completions.GetAsync("sacha",
            (await _completions.CreateAsync("sacha", candidate.SaveId, LivingDexGame.Y)).Id!.Value))!;

        // Début de partie : badge de Violette, ses dresseurs (levés par le jeu), pas le Conseil 4
        Assert.Equal(1, view.Steps.Count(s => s.Done && s.Label.StartsWith("Badge ", StringComparison.Ordinal)));
        Assert.True(view.Steps.Single(s => s.Label == "Badge Coléoptère (Violette)").Done);
        Assert.False(view.Steps.Single(s => s.Label == "Battre Malva (Conseil 4)").Done);
        var trainers = view.Steps.Where(s => s.Section == CompletionSection.Trainers).ToList();
        Assert.Equal((50, 261), (trainers.Count(s => s.Done), trainers.Count));
        Assert.All(trainers.Where(s => s.Zone == "Arène de Neuvartault"), s => Assert.True(s.Done));
        // Château de Combat : 20 nobles battus
        Assert.Equal(20, view.Steps.Count(s => s.Done && s.Zone == "Château de Combat"));
        Assert.DoesNotContain(view.Steps, s => s.State == CompletionState.Missed);
        Assert.InRange(view.Progress.Tenths, 1, 999);
    }
}
