using MonsutaPotaru.Services;

namespace MonsutaPotaru.Tests;

/// <summary>
/// Statistiques des mini-jeux (<see cref="MiniGameStats"/>) : résultats, joueurs différents sans les invités, mini-jeu le
/// plus joué (à égalité : le plus récemment joué).
/// </summary>
public sealed class MiniGameStatsTests
{
    private static readonly DateTime At = new(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Empty_HasNoFavorite()
    {
        var stats = MiniGameStats.From([]);
        Assert.Equal((0, 0, 0, 0, 0, null, 0), (stats.Total, stats.Won, stats.Lost, stats.Draws, stats.Players, stats.Favorite, stats.FavoriteGames));
    }

    [Fact]
    public void Counts_OutcomesAndAccounts_NotGuests()
    {
        var stats = MiniGameStats.From(
        [
            new("Échecs", ["ondine"], At, MiniGameOutcome.Won),
            new("Échecs", ["ondine"], At, MiniGameOutcome.Lost),
            new("Échecs", [], At, MiniGameOutcome.Draw), // invité
            new("Échecs", [], At, MiniGameOutcome.Won),
            new("Échecs", ["pierre"], At, MiniGameOutcome.Won),
            // Cache-cache : plusieurs adversaires à la fois
            new("Cache-cache", ["ondine", "regis"], At, MiniGameOutcome.Draw),
        ]);
        Assert.Equal((6, 3, 1, 2, 3, "Échecs", 5), (stats.Total, stats.Won, stats.Lost, stats.Draws, stats.Players, stats.Favorite, stats.FavoriteGames));
    }

    [Fact]
    public void MostPlayed_TieGoesToTheMostRecent()
    {
        (string Key, DateTime At)[] plays = [("a", At.AddDays(-3)), ("b", At.AddDays(-2)), ("a", At.AddDays(-5)), ("b", At.AddDays(-9))];
        Assert.Equal(("b", 2), MiniGameStats.MostPlayed(plays, p => p.Key, p => p.At));
        (string Key, DateTime At)[] more = [.. plays, ("a", At.AddDays(-30))];
        Assert.Equal(("a", 3), MiniGameStats.MostPlayed(more, p => p.Key, p => p.At));
        Assert.Null(MiniGameStats.MostPlayed(Array.Empty<(string Key, DateTime At)>(), p => p.Key, p => p.At));
    }

    [Theory]
    [InlineData("sacha", "sacha")]
    [InlineData(ChessHub.GuestIdPrefix + "123", null)]
    [InlineData("", null)]
    public void Guests_HaveNoAccount(string id, string? expected) => Assert.Equal(expected, MiniGamePlay.AccountOrNull(id));
}
