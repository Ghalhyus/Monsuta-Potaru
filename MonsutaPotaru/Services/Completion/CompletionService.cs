using Microsoft.EntityFrameworkCore;
using MonsutaPotaru.Data;
using PKHeX.Core;

namespace MonsutaPotaru.Services;

/// <summary>Complétion dans la vue générale : jeu, sauvegarde, progression.</summary>
/// <param name="SaveLabel">Dresseur et nom du fichier (pour soi).</param>
/// <param name="PublicSaveLabel">Dresseur seul, sans nom de fichier (pour les autres joueurs).</param>
public sealed record CompletionSummary(int Id, LivingDexGameInfo Game, string SaveLabel, string PublicSaveLabel, CompletionProgress Progress);

/// <summary>Une complétion et toutes ses étapes, lues dans la sauvegarde.</summary>
public sealed record CompletionView(int Id, LivingDexGameInfo Game, string SaveLabel, string PublicSaveLabel,
    IReadOnlyList<CompletionStep> Steps, CompletionProgress Progress);

/// <summary>Sauvegarde stockée qui peut recevoir une complétion, et ses jeux possibles (Rouge ou Bleu : au choix).</summary>
public sealed record CompletionCandidate(int SaveId, string Label, IReadOnlyList<LivingDexGame> Games);

public sealed record CompletionResult(bool Ok, string Message, int? Id = null);

/// <summary>
/// Complétions d'un compte (« Défis » → « Complétion (100 %) ») : une partie stockée suivie vers le 100 %, lue à chaque
/// affichage dans la sauvegarde liée (elle suit donc chaque mise à jour, site ou 3DS). Une par jeu et par compte ; une
/// sauvegarde pour une seule complétion (elle peut aussi avoir son living dex). Pour l'instant : de la 1re à la 3e génération
/// (<see cref="CompletionGames"/>).
/// </summary>
public sealed class CompletionService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ILogger<CompletionService> logger)
{
    /// <summary>Complétions du compte, dans l'ordre des jeux.</summary>
    public async Task<List<CompletionSummary>> ListAsync(string ownerId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var rows = await db.Completions.AsNoTracking().Where(c => c.OwnerId == ownerId)
            .Select(c => new { c.Id, c.Game, c.StoredSave!.TrainerName, c.StoredSave.FileName, c.StoredSave.Data })
            .ToListAsync();
        return [.. rows.OrderBy(c => c.Game).Select(c =>
        {
            var steps = Steps(c.Game, c.Data, c.FileName);
            return new CompletionSummary(c.Id, LivingDexGames.Get(c.Game), SaveLabel(c.TrainerName, c.FileName), c.TrainerName,
                CompletionProgress.Of(steps));
        })];
    }

    /// <summary>Une complétion et ses étapes ; null si elle n'existe pas (ou pas sur ce compte).</summary>
    public async Task<CompletionView?> GetAsync(string ownerId, int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var c = await db.Completions.AsNoTracking().Where(c => c.Id == id && c.OwnerId == ownerId)
            .Select(c => new { c.Id, c.Game, c.StoredSave!.TrainerName, c.StoredSave.FileName, c.StoredSave.Data })
            .FirstOrDefaultAsync();
        if (c is null)
            return null;
        var steps = Steps(c.Game, c.Data, c.FileName);
        return new(c.Id, LivingDexGames.Get(c.Game), SaveLabel(c.TrainerName, c.FileName), c.TrainerName, steps,
            CompletionProgress.Of(steps));
    }

    /// <summary>Étapes lues dans la sauvegarde ; aucune si elle ne se lit plus (elle ne compte alors pour rien).</summary>
    private static List<CompletionStep> Steps(LivingDexGame game, byte[] data, string fileName) =>
        StoredSaveService.TryRead(data, fileName, out var sav) ? CompletionGames.Steps(sav, game) ?? [] : [];

    /// <summary>Jeux qui ont déjà leur complétion sur ce compte (grisés à la création).</summary>
    public async Task<HashSet<LivingDexGame>> TakenGamesAsync(string ownerId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return [.. await db.Completions.Where(c => c.OwnerId == ownerId).Select(c => c.Game).ToListAsync()];
    }

    /// <summary>
    /// Sauvegardes stockées qui peuvent recevoir une complétion : de la 1re à la 3e génération (sans les versions japonaises
    /// et coréennes des deux premières), pas encore
    /// liées à une complétion, avec un jeu libre.
    /// </summary>
    public async Task<List<CompletionCandidate>> ListCandidatesAsync(string ownerId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var taken = await db.Completions.Where(c => c.OwnerId == ownerId).Select(c => c.Game).ToListAsync();
        var linked = db.Completions.Select(c => c.StoredSaveId);
        var saves = await db.StoredSaves.AsNoTracking()
            .Where(s => s.OwnerId == ownerId && s.Generation <= 3 && !linked.Contains(s.Id))
            .OrderBy(s => s.UploadedAt)
            .Select(s => new { s.Id, s.GameName, s.TrainerName, s.FileName, s.Data })
            .ToListAsync();
        var candidates = new List<CompletionCandidate>();
        foreach (var s in saves)
        {
            if (!StoredSaveService.TryRead(s.Data, s.FileName, out var sav))
                continue;
            var games = CompletionGames.For(sav).Except(taken).ToList();
            if (games.Count > 0)
                candidates.Add(new(s.Id, $"{s.GameName} — {SaveLabel(s.TrainerName, s.FileName)}", games));
        }
        return candidates;
    }

    /// <summary>Complétion d'un jeu sur une sauvegarde stockée du compte.</summary>
    public async Task<CompletionResult> CreateAsync(string ownerId, int saveId, LivingDexGame game)
    {
        if (!CompletionGames.All.Contains(game))
            return new(false, "Ce jeu n'a pas encore de complétion.");
        var gameName = LivingDexGames.Get(game).Name;
        await using var db = await dbFactory.CreateDbContextAsync();
        var save = await db.StoredSaves.AsNoTracking().Where(s => s.Id == saveId && s.OwnerId == ownerId)
            .Select(s => new { s.FileName, s.Data }).FirstOrDefaultAsync();
        if (save is null)
            return new(false, "Cette sauvegarde n'existe plus.");
        if (!StoredSaveService.TryRead(save.Data, save.FileName, out var sav) || !CompletionGames.For(sav).Contains(game))
            return new(false, sav is SAV1 { Japanese: true } or SAV2 { Japanese: true } or SAV2 { Korean: true }
                ? "Les sauvegardes japonaises et coréennes n'ont pas encore de complétion."
                : $"Cette sauvegarde n'est pas une partie de {gameName}.");
        if (await db.Completions.AnyAsync(c => c.OwnerId == ownerId && c.Game == game))
            return new(false, $"Tu as déjà une complétion de {gameName} : supprime-la pour en créer une autre.");
        if (await db.Completions.AnyAsync(c => c.StoredSaveId == saveId))
            return new(false, "Cette sauvegarde a déjà une complétion.");

        var completion = new Completion { OwnerId = ownerId, Game = game, StoredSaveId = saveId, CreatedAt = DateTime.UtcNow };
        db.Completions.Add(completion);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException) // créée au même moment depuis un autre onglet (index uniques)
        {
            return new(false, $"Tu as déjà une complétion de {gameName}, ou cette sauvegarde en a déjà une.");
        }
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Utilisateur {User} : complétion {Game} créée sur la sauvegarde {Save}.", ownerId, game, saveId);
        return new(true, $"Complétion de {gameName} créée.", completion.Id);
    }

    public async Task<bool> DeleteAsync(string ownerId, int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var deleted = await db.Completions.Where(c => c.Id == id && c.OwnerId == ownerId).ExecuteDeleteAsync();
        if (deleted > 0 && logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Utilisateur {User} : complétion {Id} supprimée.", ownerId, id);
        return deleted > 0;
    }

    /// <summary>Complétion de chaque sauvegarde (nom du jeu), pour prévenir qu'elle partira avec elle (« Supprimer »).</summary>
    public async Task<Dictionary<int, string>> NamesBySaveAsync(string ownerId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return (await db.Completions.Where(c => c.OwnerId == ownerId).Select(c => new { c.StoredSaveId, c.Game }).ToListAsync())
            .ToDictionary(c => c.StoredSaveId, c => LivingDexGames.Get(c.Game).Name);
    }

    private static string SaveLabel(string trainer, string fileName) => $"{trainer} · {fileName}";
}
