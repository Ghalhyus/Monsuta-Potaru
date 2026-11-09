using Microsoft.Extensions.Logging.Abstractions;
using MonsutaPotaru.Data;
using MonsutaPotaru.Services;

namespace MonsutaPotaru.Tests;

/// <summary>Statistiques d'un joueur : argent déposé et dépensé, moyennes des living dex.</summary>
public sealed class StatisticsServiceTests : IDisposable
{
    private readonly TestDatabase _db = new("sacha", "ondine");
    private readonly LivingDexService _dex;
    private readonly StatisticsService _stats;

    public StatisticsServiceTests()
    {
        _dex = new LivingDexService(_db.Factory, NullLogger<LivingDexService>.Instance);
        _stats = new StatisticsService(_db.Factory, _dex);
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
        var red = new LivingDexCompletion(LivingDexGames.Get(LivingDexGame.Red), [P(151, 151)]); // National seul
        var silver = new LivingDexCompletion(LivingDexGames.Get(LivingDexGame.Silver),
            [P(50, 251, LivingDexMode.Regional), P(50, 251), P(50, 276, LivingDexMode.Complete)]);
        var global = new LivingDexCompletion(GlobalLivingDex.Game,
            [P(1_025, 1_025), P(1_283, 1_283, LivingDexMode.Complete), P(0, 1_578, LivingDexMode.Ultimate)]);

        var stats = StatisticsService.Summarize([red, silver, global]);

        Assert.Equal(3, stats.Count);
        // Général : le plus grand mode de chacun (National de Rouge, Complet d'Argent, Ultime du global).
        Assert.Equal(StatisticsService.Average([P(151, 151), P(50, 276), P(0, 1_578)]), stats.General);
        Assert.Equal(new LivingDexAverage(1, 0, 199), stats.Regional); // Argent seul : 50 / 251 = 19,9 %
        Assert.Equal((3, 2), (stats.National.Count, stats.National.Completed));
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
        Assert.Equal(1, normal.Regional.Count); // Argent seul
        Assert.Equal(3, normal.National.Count);
        Assert.Equal(2, normal.Complete.Count); // Argent et le global
        Assert.Equal(1, shiny.Count);
        Assert.Equal(0, shiny.Regional.Count); // le global chromatique n'a pas de Régional
        Assert.Equal(new LivingDexAverage(1, 0, 0), shiny.General); // stockage interne vide

        var (none, noShiny) = await _stats.LivingDexAsync("ondine");
        Assert.Equal((0, 0), (none.Count, noShiny.Count));
    }
}
