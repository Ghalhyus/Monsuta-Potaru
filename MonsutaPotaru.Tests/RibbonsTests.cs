using System.Text.RegularExpressions;
using PKHeX.Core;
using MonsutaPotaru.Services;

namespace MonsutaPotaru.Tests;

/// <summary>Rubans comptés par le suivi (« Défis » → « Rubans ») et leur lecture dans chaque format de PKHeX.</summary>
public sealed class RibbonsTests
{
    private static RibbonState State(List<RibbonState> states, string key) => states.Single(s => s.Ribbon.Key == key);

    [Fact]
    public void Catalog_IsTheRibbonsAnyPokemonCanEarn()
    {
        Assert.Equal(42, Ribbons.All.Count);
        Assert.Equal(Ribbons.All.Count, Ribbons.All.Select(r => r.Key).Distinct().Count());
        // Ni distributions, ni anciens tournois, ni Marques, ni Exceptionnel (hasard de l'Échange Miracle), ni rubans de 3e / 4e génération comptés un par un
        string[] excluded = ["Classic", "Premier", "Event", "Birthday", "Special", "Souvenir", "Wishing", "ChampionWorld", "ChampionBattle",
            "ChampionRegional", "ChampionNational", "Country", "World", "Record", "Partner", "OnceInALifetime", "Winning", "Victory", "Ability", "G3Cool", "G4Cool"];
        Assert.DoesNotContain(Ribbons.All, r => excluded.Contains(r.Key) || r.Key.StartsWith("Mark", StringComparison.Ordinal));
        // Colosseum / XD comptent
        Assert.Contains(Ribbons.All, r => r.Key == "National" && r.Category == RibbonCategory.Colosseum);
        Assert.Contains(Ribbons.All, r => r.Key == "Earth" && r.Category == RibbonCategory.Colosseum);
        // Chaque ruban a un nom, une façon de l'obtenir, une famille titrée, et PKHeX le connaît (le lire ne lève pas)
        Assert.All(Ribbons.All, r =>
        {
            Assert.False(string.IsNullOrWhiteSpace(r.Name));
            Assert.EndsWith(".", r.HowTo);
            Assert.False(string.IsNullOrWhiteSpace(Ribbons.CategoryName(r.Category)));
            Assert.True(Enum.TryParse<RibbonIndex>(r.Key, out _), r.Key);
        });
        Assert.Equal(42, Ribbons.Read(new PK9()).Count);
        Assert.Equal(88, Ribbons.Total); // 40 rubans simples, et ceux des souvenirs un par un (40 + 8)
        // Les souvenirs ont leur détail : 40 rubans de concours, 8 de combat, chacun connu de PKHeX et avec une façon de l'obtenir
        Assert.Equal(40, Ribbons.ContestParts.Count);
        Assert.Equal(8, Ribbons.BattleParts.Count);
        Assert.All(Ribbons.ContestParts.Concat(Ribbons.BattleParts), part =>
        {
            Assert.NotNull(typeof(PK4).GetProperty("Ribbon" + part.Key));
            Assert.EndsWith(".", part.HowTo);
        });
        Assert.Equal(48, Ribbons.ContestParts.Concat(Ribbons.BattleParts).Select(part => part.Key).Distinct().Count());
        Assert.Null(Ribbons.All.Single(r => r.Key == "Effort").Parts);
    }

    [Fact]
    public void Gen1And2_HaveNoRibbon()
    {
        Assert.Equal(0, Ribbons.CountOwned(new PK1 { Species = 25 }));
        Assert.Equal(0, Ribbons.CountOwned(new PK2 { Species = 25 }));
        Assert.Equal(0, Ribbons.CountOwned(null));
        Assert.All(Ribbons.Read(null), r => Assert.False(r.Owned));
    }

    [Fact]
    public void Gen3_ContestRanksAndBattleTower_BecomeTheMemories()
    {
        var pk = new PK3
        {
            Species = 25, RibbonCountG3Cool = 4, RibbonCountG3Beauty = 2, RibbonCountG3Tough = 1,
            RibbonWinning = true, RibbonChampionG3 = true, RibbonEffort = true, RibbonArtist = true,
        };
        var states = Ribbons.Read(pk);
        Assert.Equal(7, State(states, Ribbons.ContestMemory).Count);
        Assert.Equal(1, State(states, Ribbons.BattleMemory).Count);
        Assert.True(State(states, Ribbons.ContestMemory).Owned);
        Assert.False(State(states, Ribbons.ContestMemory).Complete);
        Assert.True(State(states, "ChampionG3").Owned);
        Assert.Equal(11, Ribbons.CountOwned(pk)); // 7 de concours, Gagnant, Maître, Effort, Artiste
        Assert.True(State(states, Ribbons.ContestMemory).Missing); // souvenir entamé
        Assert.False(State(states, "ChampionG3").Missing);

        // Détail : le rang atteint dans chaque catégorie (Beauté 2 : Normal et Super), Gagnant
        var contest = State(states, Ribbons.ContestMemory);
        Assert.False(contest.PartsUnknown);
        Assert.Equal(["G3Cool", "G3CoolSuper", "G3CoolHyper", "G3CoolMaster", "G3Beauty", "G3BeautySuper", "G3Tough"],
            contest.Parts!.Where(p => p.Owned == true).Select(p => p.Part.Key));
        Assert.Equal(["Winning"], State(states, Ribbons.BattleMemory).Parts!.Where(p => p.Owned == true).Select(p => p.Part.Key));
        Assert.Equal("Sang-Froid Super", contest.Parts![1].Part.Name);
        Assert.Equal("images/rubans/ribbong3coolsuper.png", contest.Parts[1].Part.Image);
    }

    [Fact]
    public void Colosseum_AndXD_NationalAndEarth()
    {
        Assert.Equal(["National", "Earth"], Ribbons.Read(new CK3 { Species = 197, RibbonNational = true, RibbonEarth = true })
            .Where(r => r.Owned).Select(r => r.Ribbon.Key));
        Assert.Equal(["National"], Ribbons.Read(new XK3 { Species = 249, RibbonNational = true }).Where(r => r.Owned).Select(r => r.Ribbon.Key));
    }

    [Fact]
    public void Gen4_EachHoennAndSinnohRibbon_AndTheAbilityOnes()
    {
        var pk = new PK4
        {
            Species = 448, RibbonG3Cool = true, RibbonG3CoolSuper = true, RibbonG4CuteMaster = true,
            RibbonAbility = true, RibbonAbilityWorld = true, RibbonVictory = true,
            RibbonChampionSinnoh = true, RibbonLegend = true, RibbonAlert = true, RibbonGorgeousRoyal = true,
            RibbonClassic = true, // distribution : pas comptée
        };
        var states = Ribbons.Read(pk);
        Assert.Equal(3, State(states, Ribbons.ContestMemory).Count);
        Assert.Equal(3, State(states, Ribbons.BattleMemory).Count);
        Assert.Equal(10, Ribbons.CountOwned(pk)); // 3 de concours, 3 de combat, 4 rubans simples
        Assert.Equal(["G3Cool", "G3CoolSuper", "G4CuteMaster"],
            State(states, Ribbons.ContestMemory).Parts!.Where(p => p.Owned == true).Select(p => p.Part.Key));
        Assert.Equal(["Victory", "Ability", "AbilityWorld"],
            State(states, Ribbons.BattleMemory).Parts!.Where(p => p.Owned == true).Select(p => p.Part.Key));
    }

    [Fact]
    public void Gen6AndLater_ReadTheMemoryCounters_AndEveryNewRibbon()
    {
        var pk = new PK9
        {
            Species = 25, RibbonCountMemoryContest = 40, RibbonCountMemoryBattle = 3,
            RibbonChampionKalos = true, RibbonChampionG6Hoenn = true, RibbonBestFriends = true, RibbonTraining = true,
            RibbonMasterBeauty = true, RibbonContestStar = true, RibbonChampionAlola = true, RibbonBattleTreeMaster = true,
            RibbonChampionGalar = true, RibbonTowerMaster = true, RibbonMasterRank = true, RibbonTwinklingStar = true, RibbonHisui = true,
            RibbonChampionPaldea = true, RibbonNational = true,
            RibbonMarkLunchtime = true, RibbonPartner = true, RibbonOnceInALifetime = true, // Marque, distribution, Exceptionnel : pas comptés
        };
        var states = Ribbons.Read(pk);
        var contest = State(states, Ribbons.ContestMemory);
        Assert.True(contest.Complete);
        Assert.Equal("images/rubans/ribboncountmemorycontest2.png", contest.Image); // image dorée
        Assert.Equal("images/rubans/ribboncountmemorybattle.png", State(states, Ribbons.BattleMemory).Image);
        Assert.Equal(58, Ribbons.CountOwned(pk)); // 40 de concours, 3 de combat, 15 rubans simples
        Assert.Equal((40, 40, false), (contest.Earned, contest.Worth, contest.Missing));
        // Le jeu n'a gardé que les compteurs : au complet, tous les rubans ; 3 sur 8, on ne sait pas lesquels
        Assert.All(contest.Parts!, p => Assert.True(p.Owned));
        var battle = State(states, Ribbons.BattleMemory);
        Assert.True(battle.PartsUnknown);
        Assert.All(battle.Parts!, p => Assert.Null(p.Owned));
        // Aucun : tous manquants
        Assert.All(State(Ribbons.Read(new PK9()), Ribbons.BattleMemory).Parts!, p => Assert.False(p.Owned));
        Assert.Equal("images/rubans/ribbonchampiong3.png", Ribbons.Image(Ribbons.All[0]));
    }

    /// <summary>
    /// outils/images.py télécharge l'image de chaque ruban compté (liste RIBBONS) et de chaque ruban des souvenirs
    /// (RIBBON_PARTS), et rien de plus.
    /// </summary>
    [Fact]
    public void ImagesScript_DownloadsEveryRibbon()
    {
        var script = File.ReadAllText(Path.Combine(RealSavesTests.WebRoot(), "..", "..", "outils", "images.py"));
        List<string> Keys(string name) => [.. Regex.Matches(Regex.Match(script, name + @" = \[(.*?)\]", RegexOptions.Singleline).Groups[1].Value,
            "\"([a-z0-9]+)\"").Select(m => m.Groups[1].Value)];
        Assert.Equal(Ribbons.All.Select(r => r.Key.ToLowerInvariant()), Keys("RIBBONS"));
        Assert.Equal(Ribbons.ContestParts.Concat(Ribbons.BattleParts).Select(p => p.Key.ToLowerInvariant()), Keys("RIBBON_PARTS"));
    }
}
