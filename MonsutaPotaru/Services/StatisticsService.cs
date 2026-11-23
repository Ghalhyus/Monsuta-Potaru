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
/// ce mode (un living dex de 1re génération, Régional seulement, n'entre ni dans le national ni dans le complet).
/// </summary>
public sealed record LivingDexStats(LivingDexAverage General, LivingDexAverage Regional, LivingDexAverage National, LivingDexAverage Complete)
{
    public int Count => General.Count;
}

/// <summary>
/// Rubans des Pokémon suivis (« Défis » → « Rubans », les partis compris avec leurs derniers rubans connus) : rubans gagnés
/// en tout, ceux des souvenirs un par un (<see cref="RibbonState.Earned"/>), et la moyenne des complétions sur
/// <see cref="Ribbons.Total"/> (<see cref="Completion"/> : nombre de Pokémon suivis, complets, moyenne en dixièmes).
/// </summary>
public sealed record RibbonStats(int Earned, LivingDexAverage Completion)
{
    public int Count => Completion.Count;

    /// <summary>Rubans par Pokémon suivi en dixièmes, arrondis vers le bas.</summary>
    public int PerPokemonTenths => Count == 0 ? 0 : (int)((long)Earned * 10 / Count);

    /// <summary>« 12,5 » rubans par Pokémon (« 3 » quand le compte est rond).</summary>
    public string PerPokemon => PerPokemonTenths % 10 == 0 ? $"{PerPokemonTenths / 10}" : $"{PerPokemonTenths / 10},{PerPokemonTenths % 10}";
}

/// <summary>
/// Complétions (100 %) du joueur : la moyenne de leurs pourcentages globaux (<see cref="General"/>), puis section par
/// section, chacune sur les seules complétions qui l'ont (Rouge, Bleu et Jaune n'ont pas de quêtes annexes).
/// </summary>
public sealed record CompletionStats(LivingDexAverage General, IReadOnlyList<(CompletionSection Section, LivingDexAverage Average)> Sections)
{
    public int Count => General.Count;
}

/// <summary>
/// Statistiques d'un joueur (page « Statistiques », la sienne ou celle d'un autre joueur selon sa visibilité) : chaque
/// onglet calcule les siennes quand on l'ouvre, sans cache. « Défis » : living dex, rubans des Pokémon suivis
/// (<see cref="RibbonService.ListAsync"/>), complétions (<see cref="CompletionService.ListAsync"/>). Échanges et combats : <see cref="TradeService.StatsAsync"/>,
/// <see cref="BattleService.StatsAsync"/>.
/// </summary>
public sealed class StatisticsService(IDbContextFactory<ApplicationDbContext> dbFactory, LivingDexService livingDexes,
    RibbonService ribbons, CompletionService completions)
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

    /// <summary>Rubans des Pokémon suivis : gagnés en tout, et la moyenne de leurs complétions.</summary>
    public async Task<RibbonStats> RibbonsAsync(string userId)
    {
        var trackers = await ribbons.ListAsync(userId);
        return Summarize(trackers);
    }

    /// <summary>Complétions du joueur : moyenne générale et moyenne de chaque section.</summary>
    public async Task<CompletionStats> CompletionsAsync(string userId)
    {
        var list = await completions.ListAsync(userId);
        return Summarize([.. list.Select(c => c.Progress)]);
    }

    /// <summary>Moyennes d'un groupe de living dex, chacun dans les modes qu'il a.</summary>
    public static LivingDexStats Summarize(IReadOnlyList<LivingDexCompletion> dexes) => new(
        Average(dexes.Select(d => d.Modes[^1]).ToList()),
        Average(InMode(dexes, LivingDexMode.Regional)),
        Average(InMode(dexes, LivingDexMode.National)),
        Average(InMode(dexes, LivingDexMode.Complete)));

    /// <summary>Total et moyennes d'une liste de Pokémon suivis.</summary>
    public static RibbonStats Summarize(IReadOnlyList<RibbonSummary> trackers) => new(
        trackers.Sum(t => t.Owned),
        Mean([.. trackers.Select(t => (Fraction(t.Owned, t.Total), t.Owned >= t.Total && t.Total > 0))]));

    /// <summary>
    /// Moyennes d'une liste de complétions : le pourcentage global de chacune (la moyenne exacte de ses sections), puis
    /// chaque section sur les complétions qui l'ont. Une sauvegarde illisible (aucune section) compte pour 0 % dans le
    /// général, jamais comme complète.
    /// </summary>
    public static CompletionStats Summarize(IReadOnlyList<CompletionProgress> progress) => new(
        Mean([.. progress.Select(p => (p.AverageFraction(), p.Sections.Count > 0 && p.Complete))]),
        [.. CompletionSections.All
            // Quêtes et succès d'All the Mons : seulement si une complétion les a (sinon une ligne vide pour chaque joueur).
            .Where(section => !CompletionSections.IsModpack(section) || progress.Any(p => p.Sections.Any(s => s.Section == section)))
            .Select(section =>
            {
                var inSection = progress.SelectMany(p => p.Sections).Where(s => s.Section == section).ToList();
                return (section, Mean([.. inSection.Select(s => (Fraction(s.Done, s.Total), s.Done == s.Total))]));
            })]);

    private static List<LivingDexModeProgress> InMode(IReadOnlyList<LivingDexCompletion> dexes, LivingDexMode mode) =>
        [.. dexes.SelectMany(d => d.Modes).Where(m => m.Mode == mode)];

    /// <summary>
    /// Moyenne des pourcentages (chaque living dex pèse autant), calculée en fractions exactes pour qu'un arrondi ne
    /// donne jamais 100 % à des living dex incomplets, ni 49,9 % à une moyenne de 50 % tout rond.
    /// </summary>
    public static LivingDexAverage Average(IReadOnlyList<LivingDexModeProgress> modes) =>
        Mean([.. modes.Select(m => (Fraction(m.Owned, m.Total), m.Complete))]);

    private static (BigInteger Numerator, BigInteger Denominator) Fraction(int done, int total) =>
        total > 0 ? (done, total) : (0, 1);

    /// <summary>
    /// Moyenne de fractions (chacune pèse autant), calculée exactement pour qu'un arrondi ne donne jamais 100 % à des
    /// éléments incomplets, ni 49,9 % à une moyenne de 50 % tout rond ; en dixièmes, arrondie vers le bas.
    /// </summary>
    private static LivingDexAverage Mean(IReadOnlyList<((BigInteger Numerator, BigInteger Denominator) Value, bool Complete)> values)
    {
        if (values.Count == 0)
            return new(0, 0, 0);
        BigInteger sum = 0, denominator = 1;
        foreach (var ((n, d), _) in values)
        {
            // sum / denominator + n / d = (sum × d + n × denominator) / (denominator × d)
            sum = sum * d + n * denominator;
            denominator *= d;
            var gcd = BigInteger.GreatestCommonDivisor(sum, denominator);
            if (gcd > 1)
            {
                sum /= gcd;
                denominator /= gcd;
            }
        }
        return new(values.Count, values.Count(v => v.Complete), (int)(sum * 1000 / (denominator * values.Count)));
    }
}
