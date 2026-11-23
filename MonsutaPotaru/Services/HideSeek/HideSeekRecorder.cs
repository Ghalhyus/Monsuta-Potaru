using Microsoft.EntityFrameworkCore;
using MonsutaPotaru.Data;
using PKHeX.Core;

namespace MonsutaPotaru.Services;

/// <summary>
/// Enregistre chaque partie de cache-cache terminée (<see cref="HideSeekHub.Finished"/>) pour les statistiques
/// (<see cref="HideSeekRecord"/>, une ligne par joueur). L'événement vient du fil d'un joueur ou du minuteur : l'écriture se
/// fait à côté.
/// </summary>
public sealed class HideSeekRecorder(HideSeekHub hub, IDbContextFactory<ApplicationDbContext> dbFactory, ILogger<HideSeekRecorder> logger)
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

    private void OnFinished(HideSeekSession session) => _ = Task.Run(async () =>
    {
        try
        {
            await RecordAsync(dbFactory, session, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Partie de cache-cache {Id} : enregistrement du résultat impossible.", session.Id);
        }
    });

    /// <summary>Résultat de chaque joueur de cette partie (rien si elle n'est pas finie).</summary>
    public static async Task RecordAsync(IDbContextFactory<ApplicationDbContext> dbFactory, HideSeekSession session, DateTime at)
    {
        List<HideSeekRecord> records;
        lock (session.Gate)
        {
            if (session.Stage != HideSeekStage.Done)
                return;
            records = session.Players.Select(p => new HideSeekRecord
            {
                GameId = session.Id,
                At = at,
                UserId = p.UserId,
                Found = session.Found.GetValueOrDefault(p.UserId),
                Rounds = session.RoundIndex + 1,
                Outcome = HideSeekHub.OutcomeOf(session, p.UserId).ToString(),
            }).ToList();
        }
        await using var db = await dbFactory.CreateDbContextAsync();
        db.HideSeekRecords.AddRange(records);
        await db.SaveChangesAsync();
    }
}

/// <summary>
/// Cache-cache, côté base : les Pokémon des collections que la partie peut tirer (<see cref="PoolAsync"/>) et les
/// résultats d'un joueur (page « Statistiques », onglet Mini-jeux).
/// </summary>
public sealed class HideSeekService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    /// <summary>
    /// Les Pokémon d'où viennent les cibles : « Ma collection » de l'hôte ou de tous les joueurs qui ont un compte
    /// (banque, sauvegardes et mondes Minecraft ; pas les œufs), surnommés seulement si l'option est cochée, avec des
    /// sprites Donjon Mystère ; ou les Pokémon aléatoires.
    /// </summary>
    public async Task<IReadOnlyList<HideSeekCandidate>> PoolAsync(HideSeekSettings settings, HideSeekParty host,
        IReadOnlyList<HideSeekParty> players, Func<ushort, bool>? hasSprites = null)
    {
        if (settings.Pool == HideSeekPool.Random)
            return hasSprites is null ? HideSeekDraw.RandomPool() : HideSeekDraw.RandomPool(hasSprites);
        hasSprites ??= PokemonSprites.HasDonjonSprites;
        var owners = (settings.Pool == HideSeekPool.AllPlayers ? players : [host])
            .Where(p => !p.IsGuest).ToDictionary(p => p.UserId, p => p.Pseudo);
        var ids = owners.Keys.ToList();
        await using var db = await dbFactory.CreateDbContextAsync();
        var query = db.BankPokemon.AsNoTracking().Where(p => ids.Contains(p.OwnerId) && !p.IsEgg && p.Species >= 1);
        if (settings.Nicknamed)
            query = query.Where(p => p.Nickname != p.SpeciesName && p.Nickname != "");
        var rows = await query
            .Select(p => new { p.OwnerId, p.Species, p.Form, p.Context, p.IsShiny, p.Gender, p.HeldItem, p.SpeciesName, p.Nickname, p.Type1, p.Type2, p.Nature })
            .ToListAsync();
        return rows.Where(p => hasSprites(p.Species))
            .Select(p => new HideSeekCandidate(owners[p.OwnerId], p.Species, p.Form, (EntityContext)p.Context, p.IsShiny, p.Gender == 1,
                p.HeldItem ?? 0, p.SpeciesName, p.Nickname, p.Type1, p.Type2, p.Nature ?? 0))
            .ToList();
    }

    /// <summary>Ses parties, vues de son côté (adversaires avec un compte, résultat, cibles trouvées).</summary>
    public async Task<List<HideSeekPlay>> PlaysAsync(string userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var mine = await db.HideSeekRecords.AsNoTracking().Where(r => r.UserId == userId).ToListAsync();
        var games = mine.Select(r => r.GameId).ToList();
        var others = (await db.HideSeekRecords.AsNoTracking()
                .Where(r => games.Contains(r.GameId) && r.UserId != userId)
                .Select(r => new { r.GameId, r.UserId })
                .ToListAsync())
            .ToLookup(r => r.GameId, r => r.UserId);
        return mine.Select(r => new HideSeekPlay(
            new MiniGamePlay(MiniGames.HideSeek, others[r.GameId].Select(MiniGamePlay.AccountOrNull).OfType<string>().ToList(), r.At,
                Enum.TryParse<MiniGameOutcome>(r.Outcome, out var outcome) ? outcome : MiniGameOutcome.Lost),
            r.Found)).ToList();
    }
}

/// <summary>Une partie de cache-cache vue par un joueur : la partie (résultat, adversaires) et les cibles qu'il a trouvées.</summary>
public sealed record HideSeekPlay(MiniGamePlay Play, int Found);

/// <summary>Cache-cache, onglet Mini-jeux des statistiques : parties (comme le bloc « Générale ») et Pokémon trouvés.</summary>
public sealed record HideSeekStats(MiniGameStats Games, int Found)
{
    public static HideSeekStats From(IReadOnlyList<HideSeekPlay> plays) =>
        new(MiniGameStats.From(plays.Select(p => p.Play).ToList()), plays.Sum(p => p.Found));
}
