using Microsoft.Extensions.Logging.Abstractions;
using MonsutaPotaru.Data;
using MonsutaPotaru.Services;
using PKHeX.Core;

namespace MonsutaPotaru.Tests;

/// <summary>Statistiques d'un joueur : argent déposé et dépensé, moyennes des living dex, rubans et complétions.</summary>
public sealed class StatisticsServiceTests : IDisposable
{
    private readonly TestDatabase _db = new("sacha", "ondine");
    private readonly LivingDexService _dex;
    private readonly StatisticsService _stats;

    public StatisticsServiceTests()
    {
        _dex = new LivingDexService(_db.Factory, NullLogger<LivingDexService>.Instance);
        _stats = new StatisticsService(_db.Factory, _dex, new RibbonService(_db.Factory, NullLogger<RibbonService>.Instance),
            new CompletionService(_db.Factory, NullLogger<CompletionService>.Instance));
    }

    public void Dispose() => _db.Dispose();

    private static LivingDexModeProgress P(int owned, int total, LivingDexMode mode = LivingDexMode.National) => new(mode, owned, total);

    [Fact]
    public async Task Money_SpentIsEveryDebit_DepositedIsTheBalancePlusWhatWasSpent()
    {
        Assert.Equal(new MoneyStats(0, 0, 0), await _stats.MoneyAsync("sacha")); // ni porte-monnaie ni journal

        using (var db = _db.NewContext())
        {
            db.Wallets.Add(new Wallet { OwnerId = "sacha", Money = 1_500 });
            db.Wallets.Add(new Wallet { OwnerId = "ondine", Money = 99 });
            db.WalletOperations.AddRange(
                new WalletOperation { OwnerId = "sacha", Kind = WalletOperationKind.WithdrawFromSave, Amount = 3_000 },
                new WalletOperation { OwnerId = "sacha", Kind = WalletOperationKind.BuyCollectionSlots, Amount = -1_000 },
                new WalletOperation { OwnerId = "sacha", Kind = WalletOperationKind.ExpandPark, Amount = -400 },
                new WalletOperation { OwnerId = "sacha", Kind = WalletOperationKind.BuyPark, Amount = -100 },
                new WalletOperation { OwnerId = "sacha", Kind = WalletOperationKind.WithdrawItemsFromSave, Amount = 0 },
                new WalletOperation { OwnerId = "ondine", Kind = WalletOperationKind.BuyPark, Amount = -5_000 }); // un autre joueur
            await db.SaveChangesAsync();
        }

        // Dépensé : 1 000 + 400 + 100 ; déposé : le solde (1 500) et ce qui a été dépensé, pas les retraits eux-mêmes.
        Assert.Equal(new MoneyStats(3_000, 1_500, 1_500), await _stats.MoneyAsync("sacha"));
    }

    [Fact]
    public void Average_IsTheMeanOfThePercentages_RoundedDown()
    {
        Assert.Equal(new LivingDexAverage(0, 0, 0), StatisticsService.Average([]));
        // Chaque living dex pèse autant : 151 / 151 et 0 / 1 000 font 50 %, pas 151 / 1 151.
        Assert.Equal(new LivingDexAverage(2, 1, 500), StatisticsService.Average([P(151, 151), P(0, 1_000)]));
        // Fractions exactes : un tiers et deux tiers font 50,0 %, pas 49,9 %.
        Assert.Equal(LivingDexGames.Percent(500, 1_000), StatisticsService.Average([P(1, 3), P(2, 3)]).Percent);
        // Arrondi vers le bas : 100 % seulement quand tous sont complets.
        Assert.Equal(LivingDexGames.Percent(999, 1_000), StatisticsService.Average([P(999, 1_000), P(1_000, 1_000)]).Percent);
        Assert.Equal(new LivingDexAverage(2, 2, 1_000), StatisticsService.Average([P(151, 151), P(251, 251)]));
        Assert.Equal(LivingDexGames.Percent(1, 1), StatisticsService.Average([P(151, 151), P(251, 251)]).Percent);
        // Beaucoup de living dex aux tailles premières entre elles : pas de débordement.
        var many = Enumerable.Range(0, 40).Select(i => P(i, 1_000 + i * 7)).ToList();
        Assert.InRange(StatisticsService.Average(many).Tenths, 0, 1_000);
    }

    [Fact]
    public void Summarize_CountsEachLivingDexInTheModesItHas()
    {
        var red = new LivingDexCompletion(LivingDexGames.Get(LivingDexGame.Red), [P(151, 151, LivingDexMode.Regional)]); // Régional seul
        var silver = new LivingDexCompletion(LivingDexGames.Get(LivingDexGame.Silver),
            [P(50, 251, LivingDexMode.Regional), P(50, 251), P(50, 276, LivingDexMode.Complete)]);
        var global = new LivingDexCompletion(GlobalLivingDex.Game,
            [P(1_025, 1_025), P(1_283, 1_283, LivingDexMode.Complete), P(0, 1_578, LivingDexMode.Ultimate)]);

        var stats = StatisticsService.Summarize([red, silver, global]);

        Assert.Equal(3, stats.Count);
        // Général : le plus grand mode de chacun (Régional de Rouge, Complet d'Argent, Ultime du global).
        Assert.Equal(StatisticsService.Average([P(151, 151), P(50, 276), P(0, 1_578)]), stats.General);
        Assert.Equal(new LivingDexAverage(2, 1, 599), stats.Regional); // Rouge 100 % et Argent 50 / 251 = 19,9 %
        Assert.Equal((2, 1), (stats.National.Count, stats.National.Completed)); // Rouge n'y est pas
        Assert.Equal((2, 1), (stats.Complete.Count, stats.Complete.Completed));
    }

    [Fact]
    public async Task LivingDex_SameCountsAsTheOverview_ShinyApart()
    {
        var saves = _db.StoredSaves();
        async Task<int> Store(string file)
        {
            var data = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Sauvegardes", file));
            Assert.True((await saves.AddAsync("sacha", file, data)).Ok);
            return (await saves.ListAsync("sacha")).Single(s => s.FileName == file).Id;
        }
        Assert.True((await _dex.CreateAsync("sacha", await Store("rouge.sav"), LivingDexGame.Red)).Ok);
        Assert.True((await _dex.CreateAsync("sacha", await Store("argent.sav"), LivingDexGame.Silver)).Ok);
        Assert.True((await _dex.CreateGlobalAsync("sacha")).Ok);
        Assert.True((await _dex.CreateGlobalAsync("sacha", shiny: true)).Ok);

        // Même comptage que la vue générale, mode par mode.
        var progress = await _dex.ProgressAsync("sacha");
        foreach (var mode in new[] { LivingDexMode.Regional, LivingDexMode.National, LivingDexMode.Complete, LivingDexMode.Ultimate })
            foreach (var card in await _dex.ListAsync("sacha", mode))
            {
                var counted = progress.Single(p => p.Game == card.Game).Modes.Single(m => m.Mode == card.Mode);
                Assert.Equal((card.Owned, card.Total), (counted.Owned, counted.Total));
            }

        var (normal, shiny) = await _stats.LivingDexAsync("sacha");
        Assert.Equal(3, normal.Count);
        Assert.Equal(2, normal.Regional.Count); // Rouge (son seul mode) et Argent
        Assert.Equal(2, normal.National.Count); // Argent et le global, pas Rouge
        Assert.Equal(2, normal.Complete.Count); // Argent et le global
        Assert.Equal(1, shiny.Count);
        Assert.Equal(0, shiny.Regional.Count); // le global chromatique n'a pas de Régional
        Assert.Equal(new LivingDexAverage(1, 0, 0), shiny.General); // stockage interne vide

        var (none, noShiny) = await _stats.LivingDexAsync("ondine");
        Assert.Equal((0, 0), (none.Count, noShiny.Count));
    }

    private static RibbonSummary Tracker(int owned) => new(0, "Pyrane", "Pyrane", "", 50, false, owned, Ribbons.Total, null, DateTime.UtcNow);

    private static CompletionProgress Progress(params (CompletionSection Section, int Done, int Total)[] sections) =>
        new([.. sections.Select(s => new CompletionSectionProgress(s.Section, s.Done, 0, s.Total))]);

    [Fact]
    public void Ribbons_TotalAveragePerPokemonAndCompletion()
    {
        Assert.Equal(new RibbonStats(0, new LivingDexAverage(0, 0, 0)), StatisticsService.Summarize(Array.Empty<RibbonSummary>()));

        var stats = StatisticsService.Summarize([Tracker(4), Tracker(Ribbons.Total), Tracker(0)]);

        Assert.Equal(92, stats.Earned); // 4 + 88 + 0, les rubans des souvenirs un par un
        Assert.Equal(3, stats.Count);
        Assert.Equal("30,6", stats.PerPokemon); // 92 / 3, arrondi vers le bas
        Assert.Equal(new LivingDexAverage(3, 1, 348), stats.Completion); // 92 / 264 = 34,8 %, un Pokémon complet
        Assert.Equal("3", StatisticsService.Summarize([Tracker(2), Tracker(4)]).PerPokemon); // compte rond : sans virgule
    }

    [Fact]
    public void Completions_GeneralThenEachSection_OnlyTheGamesThatHaveIt()
    {
        Assert.Equal(0, StatisticsService.Summarize(Array.Empty<CompletionProgress>()).Count);

        // Rouge (pas de quêtes annexes), Argent, et une sauvegarde illisible (aucune section).
        var red = Progress((CompletionSection.Story, 1, 2), (CompletionSection.Pokedex, 151, 151), (CompletionSection.Trainers, 0, 3),
            (CompletionSection.Items, 1, 3), (CompletionSection.Encounters, 2, 3));
        var silver = Progress((CompletionSection.Story, 1, 1), (CompletionSection.Pokedex, 251, 251), (CompletionSection.Trainers, 3, 3),
            (CompletionSection.Items, 3, 3), (CompletionSection.Encounters, 3, 3), (CompletionSection.Quests, 1, 3));
        var unreadable = Progress();

        var stats = StatisticsService.Summarize([red, silver, unreadable]);

        Assert.Equal(3, stats.Count);
        // Rouge : (1/2 + 1 + 0 + 1/3 + 2/3) / 5 = 50 % ; Argent : (5 + 1/3) / 6 = 88,8… % ; l'illisible : 0 %, pas complète.
        Assert.Equal(new LivingDexAverage(3, 0, 462), stats.General); // (1/2 + 8/9 + 0) / 3 = 46,2 %
        // Les sections d'All the Mons n'apparaissent pas tant qu'aucune complétion ne les a
        Assert.Equal(CompletionSections.All.Where(s => !CompletionSections.IsModpack(s)), stats.Sections.Select(s => s.Section));
        var sections = stats.Sections.ToDictionary(s => s.Section, s => s.Average);
        Assert.Equal(new LivingDexAverage(2, 1, 750), sections[CompletionSection.Story]); // 1/2 et 1
        Assert.Equal(new LivingDexAverage(2, 2, 1_000), sections[CompletionSection.Pokedex]);
        Assert.Equal(new LivingDexAverage(2, 1, 500), sections[CompletionSection.Trainers]); // 0 et 1
        Assert.Equal(new LivingDexAverage(2, 1, 666), sections[CompletionSection.Items]); // (1/3 + 1) / 2, arrondi vers le bas
        Assert.Equal(new LivingDexAverage(1, 0, 333), sections[CompletionSection.Quests]); // Argent seul

        Assert.Equal(1, StatisticsService.Summarize([silver with { Sections = [.. silver.Sections.Select(s => s with { Done = s.Total })] }]).General.Completed);
    }

    [Fact]
    public async Task RibbonsAndCompletions_FromTheTrackersAndTheStoredSaves()
    {
        // Un Pokémon suivi avec deux rubans (Maître de Sinnoh, Sang-Froid de Hoenn), et un sans ruban.
        var info = new PokemonInfo();
        var ribbons = new RibbonService(_db.Factory, NullLogger<RibbonService>.Instance);
        foreach (var (species, index) in new (ushort, int)[] { (25, 1), (133, 2) })
        {
            var pk = SaveSessionTests.CreatePk5(species, 20, "sacha", index);
            if (species == 25)
            {
                pk.RibbonChampionSinnoh = true;
                pk.RibbonG3Cool = true;
            }
            pk.RefreshChecksum();
            var entry = BankService.CreateEntry(info, pk, "sacha", DateTime.UtcNow);
            using (var db = _db.NewContext())
            {
                db.BankPokemon.Add(entry);
                await db.SaveChangesAsync();
            }
            Assert.True((await ribbons.AddAsync("sacha", entry.Id)).Ok);
        }

        var ribbonStats = await _stats.RibbonsAsync("sacha");
        Assert.Equal((2, 2, "1"), (ribbonStats.Earned, ribbonStats.Count, ribbonStats.PerPokemon));
        Assert.Equal(StatisticsService.Summarize(await ribbons.ListAsync("sacha")), ribbonStats);
        Assert.Equal(0, (await _stats.RibbonsAsync("ondine")).Count);

        // Complétion de Rouge sur la vraie sauvegarde : le même pourcentage que la vue générale, pas de quêtes annexes.
        var data = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Sauvegardes", "rouge.sav"));
        var saves = _db.StoredSaves();
        Assert.True((await saves.AddAsync("sacha", "rouge.sav", data)).Ok);
        var completions = new CompletionService(_db.Factory, NullLogger<CompletionService>.Instance);
        Assert.True((await completions.CreateAsync("sacha", (await saves.ListAsync("sacha")).Single().Id, LivingDexGame.Red)).Ok);

        var completionStats = await _stats.CompletionsAsync("sacha");
        var red = Assert.Single(await completions.ListAsync("sacha")).Progress;
        Assert.Equal(new LivingDexAverage(1, 0, red.Tenths), completionStats.General);
        Assert.Equal(0, completionStats.Sections.Single(s => s.Section == CompletionSection.Quests).Average.Count);
        Assert.Equal(0, (await _stats.CompletionsAsync("ondine")).Count);
    }
}
