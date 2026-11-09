using System.Numerics;
using Microsoft.EntityFrameworkCore;
using MonsutaPotaru.Data;

namespace MonsutaPotaru.Services;

/// <summary>
/// Argent du joueur (onglet « Générale » des statistiques). <paramref name="Spent"/> : tout ce qui est sorti du
/// porte-monnaie (emplacements, agrandissements du parc, achats de parcs) ; <paramref name="Deposited"/> : le solde plus ce
/// qui a été dépensé, pour que retirer de l'argent d'une sauvegarde puis l'y remettre ne compte pas deux fois.
/// </summary>
public sealed record MoneyStats(long Deposited, long Spent, long Balance);

/// <summary>
/// Moyenne d'un mode sur plusieurs living dex : combien l'ont (<paramref name="Count"/>), combien y sont complets, et la
/// moyenne de leurs pourcentages en dixièmes de pour cent, arrondie vers le bas (1 000 seulement quand tous sont complets).
/// </summary>
public sealed record LivingDexAverage(int Count, int Completed, int Tenths)
{
    /// <summary>« 57,3 % », comme les barres des living dex.</summary>
    public string Percent => LivingDexGames.Percent(Tenths, 1000);
}

/// <summary>
/// Statistiques d'un groupe de living dex (les normaux, ou les chromatiques) : le général compte chacun dans son plus grand
/// mode (Complet, Ultime pour les globaux, son seul mode pour un chromatique d'un jeu) ; les autres, seulement ceux qui ont
/// ce mode (un living dex de 1re génération, « National seul », n'entre ni dans le régional ni dans le complet).
/// </summary>
public sealed record LivingDexStats(LivingDexAverage General, LivingDexAverage Regional, LivingDexAverage National, LivingDexAverage Complete)
{
    public int Count => General.Count;
}

/// <summary>
/// Statistiques d'un joueur (page « Statistiques », la sienne ou celle d'un autre joueur selon sa visibilité) : chaque
/// onglet calcule les siennes quand on l'ouvre, sans cache. Échanges et combats : <see cref="TradeService.StatsAsync"/>,
/// <see cref="BattleService.StatsAsync"/>.
/// </summary>
public sealed class StatisticsService(IDbContextFactory<ApplicationDbContext> dbFactory, LivingDexService livingDexes)
{
    /// <summary>Solde, argent dépensé (chaque sortie du porte-monnaie) et argent déposé (solde + dépensé).</summary>
    public async Task<MoneyStats> MoneyAsync(string userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var balance = await db.Wallets.AsNoTracking().Where(w => w.OwnerId == userId).Select(w => w.Money).FirstOrDefaultAsync();
        var spent = -await db.WalletOperations.AsNoTracking().Where(o => o.OwnerId == userId && o.Amount < 0).SumAsync(o => o.Amount);
        return new(balance + spent, spent, balance);
    }

    /// <summary>Living dex normaux (globaux compris) et chromatiques (des jeux et global), à part.</summary>
    public async Task<(LivingDexStats Normal, LivingDexStats Shiny)> LivingDexAsync(string userId)
    {
        var progress = await livingDexes.ProgressAsync(userId);
        return (Summarize(progress.Where(p => !p.Game.Shiny).ToList()), Summarize(progress.Where(p => p.Game.Shiny).ToList()));
    }

    /// <summary>Moyennes d'un groupe de living dex, chacun dans les modes qu'il a.</summary>
    public static LivingDexStats Summarize(IReadOnlyList<LivingDexCompletion> dexes) => new(
        Average(dexes.Select(d => d.Modes[^1]).ToList()),
        Average(InMode(dexes, LivingDexMode.Regional)),
        Average(InMode(dexes, LivingDexMode.National)),
        Average(InMode(dexes, LivingDexMode.Complete)));

    private static List<LivingDexModeProgress> InMode(IReadOnlyList<LivingDexCompletion> dexes, LivingDexMode mode) =>
        [.. dexes.SelectMany(d => d.Modes).Where(m => m.Mode == mode)];

    /// <summary>
    /// Moyenne des pourcentages (chaque living dex pèse autant), calculée en fractions exactes pour qu'un arrondi ne
    /// donne jamais 100 % à des living dex incomplets, ni 49,9 % à une moyenne de 50 % tout rond.
    /// </summary>
    public static LivingDexAverage Average(IReadOnlyList<LivingDexModeProgress> modes)
    {
        if (modes.Count == 0)
            return new(0, 0, 0);
        BigInteger sum = 0, denominator = 1;
        foreach (var m in modes.Where(m => m.Total > 0))
        {
            sum = sum * m.Total + m.Owned * denominator;
            denominator *= m.Total;
            var gcd = BigInteger.GreatestCommonDivisor(sum, denominator);
            if (gcd > 1)
            {
                sum /= gcd;
                denominator /= gcd;
            }
        }
        return new(modes.Count, modes.Count(m => m.Complete), (int)(sum * 1000 / (denominator * modes.Count)));
    }
}
