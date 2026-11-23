namespace MonsutaPotaru.Services;

/// <summary>Noms des mini-jeux (rubrique « Mini-jeux » du menu), tels qu'affichés dans les statistiques.</summary>
public static class MiniGames
{
    public const string Chess = "Échecs";
    public const string HideSeek = "Cache-cache";
}

/// <summary>Résultat d'une partie de mini-jeu pour un joueur.</summary>
public enum MiniGameOutcome { Won, Lost, Draw }

/// <summary>
/// Une partie de mini-jeu vue par un joueur : le jeu, les adversaires qui ont un compte (pas les invités sans compte, qu'on
/// ne sait pas reconnaître d'une partie à l'autre : un seul aux échecs, jusqu'à sept au cache-cache), la date, le résultat.
/// </summary>
public sealed record MiniGamePlay(string Game, IReadOnlyList<string> Opponents, DateTime At, MiniGameOutcome Outcome)
{
    /// <summary>L'adversaire s'il a un compte ; null pour un invité des parties publiques (<see cref="ChessHub.GuestIdPrefix"/>).</summary>
    public static string? AccountOrNull(string id) =>
        id.Length == 0 || id.StartsWith(ChessHub.GuestIdPrefix, StringComparison.Ordinal) ? null : id;
}

/// <summary>
/// Parties de mini-jeux d'un joueur (bloc « Générale » de l'onglet Mini-jeux, et le haut de chaque mini-jeu) : jouées,
/// gagnées, perdues, nulles, joueurs différents (comptes seulement : les invités sans compte n'y entrent pas), et le
/// mini-jeu le plus joué (à égalité : le plus récemment joué ; null sans partie).
/// </summary>
public sealed record MiniGameStats(int Won, int Lost, int Draws, int Players, string? Favorite, int FavoriteGames)
{
    public int Total => Won + Lost + Draws;

    public static MiniGameStats From(IReadOnlyList<MiniGamePlay> plays)
    {
        var favorite = MostPlayed(plays, p => p.Game, p => p.At);
        return new MiniGameStats(
            plays.Count(p => p.Outcome == MiniGameOutcome.Won),
            plays.Count(p => p.Outcome == MiniGameOutcome.Lost),
            plays.Count(p => p.Outcome == MiniGameOutcome.Draw),
            plays.SelectMany(p => p.Opponents).Distinct().Count(),
            favorite?.Key, favorite?.Count ?? 0);
    }

    /// <summary>La clé la plus fréquente et son nombre ; à égalité, celle jouée le plus récemment ; null sans élément.</summary>
    public static (string Key, int Count)? MostPlayed<T>(IEnumerable<T> items, Func<T, string> key, Func<T, DateTime> at) =>
        items.GroupBy(key)
            .Select(g => (Key: g.Key, Count: g.Count(), Last: g.Max(at)))
            .OrderByDescending(g => g.Count).ThenByDescending(g => g.Last)
            .Select(g => ((string Key, int Count)?)(g.Key, g.Count))
            .FirstOrDefault();
}

/// <summary>Onglet Mini-jeux des statistiques : le bloc « Générale » (tous les mini-jeux) et celui de chaque mini-jeu.</summary>
public sealed record MiniGameOverview(MiniGameStats General, ChessStats Chess, HideSeekStats HideSeek);

/// <summary>Statistiques des mini-jeux d'un joueur ; un nouveau mini-jeu ajoute ses parties à <see cref="MiniGameStats.From"/>.</summary>
public sealed class MiniGameService(ChessService chess, HideSeekService hideSeek)
{
    public async Task<MiniGameOverview> StatsAsync(string userId)
    {
        var chessPlays = await chess.PlaysAsync(userId);
        var hideSeekPlays = await hideSeek.PlaysAsync(userId);
        var all = chessPlays.Select(p => p.Play).Concat(hideSeekPlays.Select(p => p.Play)).ToList();
        return new MiniGameOverview(MiniGameStats.From(all), ChessStats.From(chessPlays), HideSeekStats.From(hideSeekPlays));
    }
}
