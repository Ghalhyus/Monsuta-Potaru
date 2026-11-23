using Microsoft.EntityFrameworkCore;
using MonsutaPotaru.Data;

namespace MonsutaPotaru.Services;

/// <summary>
/// Enregistre chaque partie d'échecs terminée (<see cref="ChessHub.Finished"/>) pour les statistiques
/// (<see cref="ChessRecord"/>). L'événement vient du fil d'un joueur ou du minuteur : l'écriture se fait à côté.
/// </summary>
public sealed class ChessRecorder(ChessHub hub, IDbContextFactory<ApplicationDbContext> dbFactory, ILogger<ChessRecorder> logger)
    : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        hub.Finished += OnFinished;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        hub.Finished -= OnFinished;
        return Task.CompletedTask;
    }

    private void OnFinished(ChessSession session) => _ = Task.Run(async () =>
    {
        try
        {
            await RecordAsync(dbFactory, session, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Partie d'échecs {Id} : enregistrement du résultat impossible.", session.Id);
        }
    });

    /// <summary>Résultat de cette partie (rien si elle n'est pas finie).</summary>
    public static async Task RecordAsync(IDbContextFactory<ApplicationDbContext> dbFactory, ChessSession session, DateTime at)
    {
        ChessRecord record;
        lock (session.Gate)
        {
            if (session.Game is not { IsOver: true } game)
                return;
            record = new ChessRecord
            {
                At = at,
                WhiteId = session.Host.UserId,
                BlackId = session.Guest.UserId,
                WhiteTeam = game.White.Id,
                BlackTeam = game.Black.Id,
                Moves = game.Steps.Count,
                WhiteCaptures = game.Steps.Count(s => s.CapturedId is not null && s.Color == ChessColor.White),
                BlackCaptures = game.Steps.Count(s => s.CapturedId is not null && s.Color == ChessColor.Black),
                End = game.End.ToString(),
                WinnerId = game.Winner is { } winner ? session.Player(winner).UserId : null,
            };
        }
        await using var db = await dbFactory.CreateDbContextAsync();
        db.ChessRecords.Add(record);
        await db.SaveChangesAsync();
    }
}

/// <summary>Résultats des parties d'échecs d'un joueur (page « Statistiques », onglet Mini-jeux).</summary>
public sealed class ChessService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    /// <summary>Ses parties, vues de son côté (adversaire, résultat, équipe, pièces prises et perdues).</summary>
    public async Task<List<ChessPlay>> PlaysAsync(string userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var records = await db.ChessRecords.AsNoTracking()
            .Where(c => c.WhiteId == userId || c.BlackId == userId)
            .ToListAsync();
        return records.Select(c =>
        {
            var white = c.WhiteId == userId;
            var opponent = white ? c.BlackId : c.WhiteId;
            return new ChessPlay(
                new MiniGamePlay(MiniGames.Chess, MiniGamePlay.AccountOrNull(opponent) is { } account ? [account] : [], c.At,
                    c.WinnerId switch
                    {
                        null => MiniGameOutcome.Draw,
                        var winner when winner == userId => MiniGameOutcome.Won,
                        _ => MiniGameOutcome.Lost,
                    }),
                white ? c.WhiteTeam : c.BlackTeam, white ? c.WhiteCaptures : c.BlackCaptures, white ? c.BlackCaptures : c.WhiteCaptures);
        }).ToList();
    }

    /// <summary>Ses statistiques aux échecs.</summary>
    public async Task<ChessStats> StatsAsync(string userId) => ChessStats.From(await PlaysAsync(userId));
}

/// <summary>Une partie d'échecs vue par un joueur : la partie (résultat, adversaire), son équipe, ses prises et ses pertes.</summary>
public sealed record ChessPlay(MiniGamePlay Play, string Team, int Taken, int Lost);

/// <summary>
/// Échecs, onglet Mini-jeux des statistiques : parties (comme le bloc « Générale »), pièces prises et perdues, équipe la
/// plus jouée (à égalité : la plus récemment jouée ; null sans partie).
/// </summary>
public sealed record ChessStats(MiniGameStats Games, int PiecesTaken, int PiecesLost, ChessTeam? FavoriteTeam, int FavoriteTeamGames)
{
    public static ChessStats From(IReadOnlyList<ChessPlay> plays)
    {
        var favorite = MiniGameStats.MostPlayed(plays, p => p.Team, p => p.Play.At);
        return new ChessStats(MiniGameStats.From(plays.Select(p => p.Play).ToList()), plays.Sum(p => p.Taken), plays.Sum(p => p.Lost),
            favorite is { } f ? ChessTeams.Find(f.Key) : null, favorite?.Count ?? 0);
    }
}
