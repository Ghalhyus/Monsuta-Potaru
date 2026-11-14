using Microsoft.Extensions.Logging.Abstractions;
using PKHeX.Core;
using MonsutaPotaru.Data;
using MonsutaPotaru.Services;

namespace MonsutaPotaru.Tests;

/// <summary>
/// Complétion de la 8e génération : tables d'Épée / Bouclier, de Diamant Étincelant / Perle Scintillante et de Légendes
/// Arceus (générées d'après les listes de drapeaux de FlagsEditorEX et les check-lists de l'utilisateur) ; lecture de la
/// vraie sauvegarde d'Épée ; DÉ/PS et Arceus en mémoire. Pas de dresseurs.
/// </summary>
public sealed class CompletionGen8Tests : IDisposable
{
    private readonly TestDatabase _db = new("sacha", "ondine", "pierre");
    private readonly StoredSaveService _saves;
    private readonly CompletionService _completions;

    public CompletionGen8Tests()
    {
        _saves = _db.StoredSaves();
        _completions = new CompletionService(_db.Factory, NullLogger<CompletionService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    private static byte[] Real(string file) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Sauvegardes", file));

    private static CompletionRow[] Table(string game) => (CompletionRow[])typeof(Gen8CompletionData).GetField(game)!.GetValue(null)!;

    private static IEnumerable<string> Atoms(string condition) => condition.Split('|', '+');

    /// <summary>Lève les blocs booléens (« X:clé ») d'une condition d'Épée / Bouclier ou d'Arceus.</summary>
    private static void SetBlocks(SCBlockAccessor accessor, string condition)
    {
        foreach (var atom in Atoms(condition).Where(a => a.StartsWith("X:", StringComparison.Ordinal)))
            accessor.GetBlock(Convert.ToUInt32(atom[2..], 16)).ChangeBooleanType(SCTypeCode.Bool2);
    }

    [Theory]
    [InlineData("Sword", 30, 525, 71, 149, 200)]
    [InlineData("Shield", 30, 525, 71, 149, 200)]
    [InlineData("BrilliantDiamond", 20, 562, 34, 469, 100)]
    [InlineData("ShiningPearl", 20, 562, 34, 469, 100)]
    [InlineData("LegendsArceus", 16, 0, 17, 145, 0)]
    public void Tables_ConditionsKnownAndEachMachineOnce(string game, int story, int items, int encounters, int quests, int machines)
    {
        var rows = Table(game);
        Assert.DoesNotContain(rows, r => r.Section is CompletionSection.Pokedex or CompletionSection.Trainers);
        Assert.Equal([story, items, encounters, quests],
            new[] { CompletionSection.Story, CompletionSection.Items, CompletionSection.Encounters, CompletionSection.Quests }
                .Select(s => rows.Count(r => r.Section == s)));
        foreach (var atom in rows.SelectMany(r => Atoms(r.Done)))
        {
            var parts = atom.Split(':');
            switch (parts[0])
            {
                case "X": Assert.Equal(8, parts[1].Length); break;
                case "XV": Assert.InRange(int.Parse(parts[2]), 1, 20); break;
                case "HI": Assert.InRange(int.Parse(parts[1]), 0, 1535); break;
                case "E": Assert.InRange(int.Parse(parts[1]), 0, 3999); break;
                case "SF": Assert.InRange(int.Parse(parts[1]), 0, 999); break;
                case "B": Assert.InRange(int.Parse(parts[1]), 0, 7); break;
                case "D": Assert.InRange(int.Parse(parts[1]), 1, 905); break;
                case "K": Assert.InRange(int.Parse(parts[1]), 1, 1828); break;
                case "N" or "BTM" or "UF" or "UG" or "LF": break;
                default: Assert.Fail($"Condition inattendue en 8e génération : {atom}"); break;
            }
        }
        // Objets : chaque lieu en un seul bloc ; un drapeau = un objet ; chaque CT (et DT) une fois
        var zones = rows.Where(r => r.Section == CompletionSection.Items).Select(r => r.Zone).ToList();
        var blocks = zones.Where((z, i) => i == 0 || zones[i - 1] != z).ToList();
        Assert.Equal(blocks.Count, blocks.Distinct().Count());
        var done = rows.Where(r => r.Section == CompletionSection.Items).Select(r => r.Done).ToList();
        Assert.Equal(done.Count, done.Distinct().Count());
        Assert.Equal(Enumerable.Range(1, machines), rows.Where(r => r.Tm != 0).Select(r => r.Tm).Distinct().Order());
        // DÉ/PS : une même CT parfois au sol à deux endroits (Route et Grands Souterrains) ; ailleurs, une fois
        if (!game.EndsWith("Diamond", StringComparison.Ordinal) && game != "ShiningPearl")
            Assert.Equal(machines, rows.Count(r => r.Tm != 0));
        // Quêtes : chaque activité en un seul bloc
        var activities = rows.Where(r => r.Section == CompletionSection.Quests).Select(r => r.Zone).ToList();
        var groups = activities.Where((z, i) => i == 0 || activities[i - 1] != z).ToList();
        Assert.Equal(groups.Count, groups.Distinct().Count());
    }

    [Fact]
    public void Labels_MachinesRecordsVersionsAndQuests()
    {
        Assert.Equal("CT00 Ultimapoing", Gen8Completion.SwordShieldLabel(new(CompletionSection.Items, "Kickenham", null, "K:1", Tm: 1)));
        Assert.Equal("DT00 Danse Lames", Gen8Completion.SwordShieldLabel(new(CompletionSection.Items, "Terres Sauvages", null, "K:1", Tm: 101)));
        Assert.Equal("CT01 Mitra-Poing", Gen8Completion.SinnohLabel(new(CompletionSection.Items, "Route 215", null, "K:1", Tm: 1)));
        Assert.Equal("CT93 Coupe", Gen8Completion.SinnohLabel(new(CompletionSection.Items, "Vestigion", null, "K:1", Tm: 93)));
        Assert.Equal("Poignet Dynamax", Gen8Completion.SwordShieldItemName(1077));
        // Pokédex : les trois Pokédex régionaux d'Épée / Bouclier, Hisui
        Assert.Equal(584, Gen8Completion.SwordShieldPokedex.Count);
        Assert.Contains(898, Gen8Completion.SwordShieldPokedex); // Sylveroy (Couronneige)
        Assert.DoesNotContain(151, Gen8Completion.SwordShieldPokedex); // Mew : par Pokémon HOME seulement
        Assert.Equal(242, Gen8Completion.HisuiPokedex.Count);
        // Versions : Ho-Oh d'Épée, Lugia de Bouclier (Grand Antre Dynamax) ; Zacian / Zamazenta ; Dialga / Palkia
        Assert.Contains(Gen8CompletionData.Sword, r => r.Species == 250 && r.Section == CompletionSection.Encounters);
        Assert.DoesNotContain(Gen8CompletionData.Shield, r => r.Species == 250);
        Assert.Contains(Gen8CompletionData.Shield, r => r.Label == "Capturer Zamazenta");
        Assert.Contains(Gen8CompletionData.ShiningPearl, r => r.Label == "Capturer Palkia");
        // Cadeaux mystère de DÉ/PS (Mew, Darkrai, Shaymin, Arceus) : pas dans les rencontres ; Jirachi (sauvegarde d'Épée / Bouclier) reste
        Assert.DoesNotContain(Gen8CompletionData.BrilliantDiamond, r => r.Section == CompletionSection.Encounters && r.Species is 151 or 491 or 492 or 493);
        Assert.Contains(Gen8CompletionData.BrilliantDiamond, r => r.Section == CompletionSection.Encounters && r.Species == 385);
        // Quêtes : 149 Taupiqueur d'Alola, concours (5), Zarbi (28), Flammes Étranges (107), rangs (10)
        Assert.Equal(149, Gen8CompletionData.Sword.Count(r => r.Zone == "Taupiqueur d'Alola"));
        Assert.Equal(5, Gen8CompletionData.ShiningPearl.Count(r => r.Zone == "Concours"));
        Assert.Equal(28, Gen8CompletionData.BrilliantDiamond.Count(r => r.Zone == "Zarbi"));
        Assert.Equal(107, Gen8CompletionData.LegendsArceus.Count(r => r.Zone == "Flammes Étranges"));
        Assert.Equal(10, Gen8CompletionData.LegendsArceus.Count(r => r.Zone == "Rangs du Groupe Galaxie"));
    }

    [Fact]
    public void Sword_FromRealSave_BadgesBlocksHiddenItemsAndMachines()
    {
        // Une sauvegarde vierge de PKHeX n'a pas les blocs des drapeaux : partie de la vraie sauvegarde, puis modifiée
        var sav = (SAV8SWSH)SaveUtil.GetSaveFile(Real("epee.sav"))!;
        var rows = Gen8CompletionData.Sword;
        Assert.Equal(0b111, sav.Badges);
        sav.Badges |= 1 << 4; // Sally
        SetBlocks(sav.Accessor, rows.Single(r => r.Label == "Mission d'Arène : le quiz").Done);
        var before = Gen8Completion.Steps(sav, LivingDexGame.Sword);
        var hidden = rows.First(r => r.Done.StartsWith("HI:", StringComparison.Ordinal)
            && !before.Single(s => s.Zone == r.Zone && s.Detail == r.Detail && s.Label == Gen8Completion.SwordShieldLabel(r)).Done
            && int.Parse(r.Done[3..]) < 512);
        sav.Accessor.GetBlock(0x6148F6AC).Data[int.Parse(hidden.Done[3..]) * 4] = 3; // ramassé pour de bon
        SaveSessionTests.GiveItems(sav, (1230, 1), (1130, 1)); // CT00, DT00

        var steps = Gen8Completion.Steps(sav, LivingDexGame.Sword);
        Assert.True(steps.Single(s => s.Label == "Badge Fée (Sally)").Done);
        Assert.False(steps.Single(s => s.Label == "Badge Combat (Faïza)").Done);
        Assert.True(steps.Single(s => s.Label == "Mission d'Arène : le quiz").Done);
        Assert.Equal(before.Count(s => s.Done && s.Section == CompletionSection.Items) + 3,
            steps.Count(s => s.Done && s.Section == CompletionSection.Items));
        Assert.True(steps.Single(s => s.Label == "CT00 Ultimapoing").Done);
        Assert.True(steps.Single(s => s.Label == "DT00 Danse Lames").Done);
        // Sauvegarde sans DLC : blocs des objets cachés de l'Isolarmure absents, jamais faits
        Assert.Contains(rows, r => r.Done.StartsWith("HI:", StringComparison.Ordinal) && int.Parse(r.Done[3..]) >= 512);
        Assert.DoesNotContain(steps, s => s.Done && s.Zone == "Plaine Salutation");
    }

    [Fact]
    public void BrilliantDiamond_InMemory_BadgesContestsTowerUnownAndNationalDex()
    {
        var sav = new SAV8BS();
        var empty = Gen8Completion.Steps(sav, LivingDexGame.BrilliantDiamond);
        Assert.Equal(0, CompletionProgress.Of(empty).Tenths);
        Assert.Equal(493, empty.Count(s => s.Section == CompletionSection.Pokedex));

        sav.FlagWork.SetSystemFlag(124, true); // badge de Pierrick
        sav.FlagWork.SetSystemFlag(10, true); // Concours Beauté Master
        sav.BattleTower.MasterRankMax = 10;
        sav.Zukan.SetHasFormFlag(201, 2, false, true); // Zarbi C
        var flag = int.Parse(Gen8CompletionData.BrilliantDiamond.Single(r => r.Label == "Recevoir la Bicyclette").Done[2..]);
        sav.FlagWork.SetFlag(flag, true);

        var steps = Gen8Completion.Steps(sav, LivingDexGame.BrilliantDiamond);
        Assert.True(steps.Single(s => s.Label == "Badge Charbon (Pierrick)").Done);
        Assert.False(steps.Single(s => s.Label == "Badge Forêt (Flo)").Done);
        Assert.True(steps.Single(s => s.Label == "Recevoir la Bicyclette").Done);
        Assert.True(steps.Single(s => s.Label == "Super Show Concours Beauté : rang Master").Done);
        Assert.False(steps.Single(s => s.Label == "Super Show Concours Grâce : rang Master").Done);
        Assert.True(steps.Single(s => s.Label == "Atteindre le rang 10 de la classe Expert").Done);
        Assert.True(steps.Single(s => s.Label == "Zarbi C").Done);
        Assert.False(steps.Single(s => s.Label == "Zarbi A").Done);
        Assert.False(steps.Single(s => s.Label == "Obtenir le Pokédex National").Done);
        Assert.InRange(CompletionProgress.Of(steps).Tenths, 1, 999);
    }

    [Fact]
    public void LegendsArceus_InMemory_RanksWispsUnownAndThreeStepsPerSpecies()
    {
        var sav = new SAV8LA();
        Assert.Equal([LivingDexGame.LegendsArceus], CompletionGames.For(sav));
        var empty = Gen8Completion.Steps(sav, LivingDexGame.LegendsArceus);
        Assert.Equal(0, CompletionProgress.Of(empty).Tenths);
        Assert.Equal(726, empty.Count(s => s.Section == CompletionSection.Pokedex));
        Assert.Equal(["#722 Brindibou : capturé", "#722 Brindibou : recherche au niveau 10", "#722 Brindibou : Parfait"],
            empty.Where(s => s.Section == CompletionSection.Pokedex && s.Species == 722).Select(s => s.Label));
        Assert.DoesNotContain(empty, s => s.Section == CompletionSection.Items);

        // Sauvegarde vierge : blocs numériques sans type (et pas de drapeaux) ; on leur donne le leur
        foreach (var (key, value) in new[] { (0x50FE632Au, 3u), (0x8B18AC32u, 2u) }) // rang 3★, 2 Flammes des Plaines Obsidiennes
        {
            var block = sav.Accessor.GetBlock(key);
            block.ChangeStoredType(SCTypeCode.UInt32);
            block.SetValue(value);
        }
        sav.PokedexSave.SetPokeObtainFlags(201, 2, 1); // Zarbi C

        var steps = Gen8Completion.Steps(sav, LivingDexGame.LegendsArceus);
        Assert.False(steps.Single(s => s.Label == "Apaiser le Monarque Hachécateur").Done); // bloc absent
        Assert.True(steps.Single(s => s.Label == "Zarbi C").Done);
        Assert.False(steps.Single(s => s.Label == "Zarbi A").Done);
        Assert.True(steps.Single(s => s.Label == "Rang 3★").Done);
        Assert.False(steps.Single(s => s.Label == "Rang 4★").Done);
        Assert.True(steps.Single(s => s.Label == "Flamme Étrange n° 2 (Plaines Obsidiennes)").Done);
        Assert.False(steps.Single(s => s.Label == "Flamme Étrange n° 3 (Plaines Obsidiennes)").Done);
        Assert.False(steps.Single(s => s.Label == "Flamme Étrange n° 1 (Marais Carmin)").Done);
        Assert.InRange(CompletionProgress.Of(steps).Tenths, 1, 999);
    }

    [Fact]
    public async Task RealSword_ProgressReadInItsBlocks()
    {
        Assert.True((await _saves.AddAsync("sacha", "epee.sav", Real("epee.sav"))).Ok);
        var candidate = Assert.Single(await _completions.ListCandidatesAsync("sacha"));
        Assert.Equal([LivingDexGame.Sword], candidate.Games);
        var view = (await _completions.GetAsync("sacha",
            (await _completions.CreateAsync("sacha", candidate.SaveId, LivingDexGame.Sword)).Id!.Value))!;

        // Trois badges (Percy, Donna, Kabu) et leurs Missions d'Arène ; pas encore Old Chister ; pas de DLC
        Assert.True(view.Steps.Single(s => s.Label == "Badge Feu (Kabu)").Done);
        Assert.True(view.Steps.Single(s => s.Label == "Mission d'Arène : 5 points (captures et K.O.)").Done);
        Assert.True(view.Steps.Single(s => s.Label == "Recevoir la Moticyclette").Done);
        Assert.False(view.Steps.Single(s => s.Label == "Badge Combat (Faïza)").Done);
        Assert.False(view.Steps.Single(s => s.Label == "Arriver sur l'Isolarmure").Done);
        Assert.Equal(10, view.Steps.Count(s => s.Done && s.Section == CompletionSection.Story));
        Assert.Equal(202, view.Steps.Count(s => s.Done && s.Section == CompletionSection.Pokedex));
        Assert.Equal(117, view.Steps.Count(s => s.Done && s.Section == CompletionSection.Items));
        Assert.True(view.Steps.Single(s => s.Label == "CT40 Météores").Done);
        Assert.DoesNotContain(view.Steps, s => s.Done && s.Section == CompletionSection.Items && s.Zone == "Plaine Salutation");
        Assert.DoesNotContain(view.Steps, s => s.State == CompletionState.Missed);
        // Cadeaux d'après la Ligue (Salamèche de Tarak) : pas encore, malgré le drapeau d'objet de la carte levé
        Assert.False(view.Steps.Single(s => s.Section == CompletionSection.Encounters && s.Species == 4).Done);
        Assert.Equal(2, view.Steps.Count(s => s.Done && s.Section == CompletionSection.Encounters)); // Toxizap, fossile
        Assert.Equal(186, view.Progress.Tenths);
    }
}
