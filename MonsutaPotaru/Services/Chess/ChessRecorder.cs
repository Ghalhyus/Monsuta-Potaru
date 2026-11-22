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
                End = game.End.ToString(),
                WinnerId = game.Winner is { } winner ? session.Player(winner).UserId : null,
            };
        }
        await using var db = await dbFactory.CreateDbContextAsync();
        db.ChessRecords.Add(record);
        await db.SaveChangesAsync();
    }
}

/// <summary>Résultats des parties d'échecs d'un joueur (page « Statistiques »).</summary>
public sealed class ChessService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    /// <summary>Victoires, défaites, nulles et adversaires différents de ce joueur aux échecs.</summary>
    public async Task<BattleRecordStats> StatsAsync(string userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var games = await db.ChessRecords.AsNoTracking()
            .Where(c => c.WhiteId == userId || c.BlackId == userId)
            .Select(c => new { Opponent = c.WhiteId == userId ? c.BlackId : c.WhiteId, c.WinnerId })
            .ToListAsync();
        return new(games.Count(g => g.WinnerId == userId), games.Count(g => g.WinnerId != null && g.WinnerId != userId),
            games.Count(g => g.WinnerId == null), games.Select(g => g.Opponent).Distinct().Count());
    }
}
