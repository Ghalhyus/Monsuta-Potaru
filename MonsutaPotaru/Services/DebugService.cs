using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using MonsutaPotaru.Data;

namespace MonsutaPotaru.Services;

/// <summary>
/// Comptes en mode debug (singleton, en mémoire) : activé depuis la page « Administration », il vaut pour tous les onglets
/// du compte jusqu'à sa déconnexion (Account/Logout) ou au redémarrage du serveur. Rien en base.
/// </summary>
public sealed class DebugMode
{
    private readonly ConcurrentDictionary<string, byte> _active = new();

    public bool IsActive(string userId) => _active.ContainsKey(userId);

    public void Set(string userId, bool active)
    {
        if (active)
            _active[userId] = 0;
        else
            _active.TryRemove(userId, out _);
    }
}

/// <summary>Résultat d'une action du mode debug, message prêt à afficher.</summary>
public sealed record DebugResult(bool Ok, string Message);

/// <summary>
/// Mode debug d'un administrateur, pour tester le site avec son compte : de l'argent ajouté au porte-monnaie (montants fixes,
/// écrits dans le journal) et le parc agrandi ou réduit gratuitement. Chaque action revérifie en base que le compte est
/// encore administrateur (le cookie peut garder un rôle retiré) : sinon le mode est coupé.
/// </summary>
public sealed class DebugService(IDbContextFactory<ApplicationDbContext> dbFactory, DebugMode mode, ILogger<DebugService> logger)
{
    public const string NotActiveMessage = "Le mode debug n'est pas activé (page « Administration »).";

    /// <summary>Montants proposés par la page « Inventaire » ; tout autre montant (navigateur modifié) est refusé.</summary>
    public static readonly long[] MoneyAmounts = [10_000, 100_000, 1_000_000];

    /// <summary>Mode debug actif pour ce compte, et compte encore administrateur (sinon le mode est coupé).</summary>
    public async Task<bool> IsActiveAsync(string userId)
    {
        if (!mode.IsActive(userId))
            return false;
        await using var db = await dbFactory.CreateDbContextAsync();
        if (await AdminService.IsAdminAsync(db, userId))
            return true;
        mode.Set(userId, false);
        return false;
    }

    public async Task<DebugResult> SetActiveAsync(string userId, bool active)
    {
        await using (var db = await dbFactory.CreateDbContextAsync())
        {
            if (!await AdminService.IsAdminAsync(db, userId))
            {
                mode.Set(userId, false);
                return new(false, AdminService.NotAdminMessage);
            }
        }
        mode.Set(userId, active);
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Compte {User} : mode debug {State}.", userId, active ? "activé" : "désactivé");
        return new(true, active
            ? "Mode debug activé jusqu'à ta déconnexion : argent dans « Banque d'objets », taille du parc dans « Parc »."
            : "Mode debug désactivé.");
    }

    /// <summary>Ajoute l'un des <see cref="MoneyAmounts"/> au porte-monnaie (crédit atomique, ligne du journal).</summary>
    public async Task<DebugResult> AddMoneyAsync(string userId, long amount)
    {
        if (!MoneyAmounts.Contains(amount))
            return new(false, "Ce montant n'est pas proposé.");
        if (!await IsActiveAsync(userId))
            return new(false, NotActiveMessage);

        await using var db = await dbFactory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var credited = await db.Wallets.Where(w => w.OwnerId == userId)
            .ExecuteUpdateAsync(w => w.SetProperty(x => x.Money, x => x.Money + amount));
        if (credited == 0)
            db.Wallets.Add(new Wallet { OwnerId = userId, Money = amount });
        await db.SaveChangesAsync();
        var balance = await db.Wallets.Where(w => w.OwnerId == userId).Select(w => w.Money).FirstAsync();
        db.WalletOperations.Add(new WalletOperation
        {
            OwnerId = userId,
            At = DateTime.UtcNow,
            Kind = WalletOperationKind.DebugCredit,
            Amount = amount,
            BalanceAfter = balance,
            Details = "Mode debug",
        });
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Compte {User} : {Amount} ₽ ajoutés (mode debug).", userId, amount);
        return new(true, $"{InventoryService.FormatMoney(amount)} ajoutés (mode debug). Solde : {InventoryService.FormatMoney(balance)}.");
    }

    /// <summary>
    /// Agrandit (<paramref name="grow"/>) ou réduit ce parc d'une taille, gratuitement et sans ligne du journal. Réduit, il
    /// garde ses premiers arrivés : les derniers retournent dans la collection (ils y sont toujours).
    /// </summary>
    public async Task<ParkExpansionResult> ResizeParkAsync(string userId, int parkId, bool grow)
    {
        if (!await IsActiveAsync(userId))
            return new(false, NotActiveMessage);
        var delta = grow ? 1 : -1;

        await using var db = await dbFactory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var changed = await db.Parks
            .Where(p => p.Id == parkId && p.OwnerId == userId && p.Expansions + delta >= 0 && p.Expansions + delta <= ParkService.MaxExpansions)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.Expansions, x => x.Expansions + delta));
        if (changed == 0)
            return new(false, await db.Parks.AnyAsync(p => p.Id == parkId && p.OwnerId == userId)
                ? SizeLimitMessage(grow)
                : "Ce parc n'existe pas.");

        var expansions = await db.Parks.Where(p => p.Id == parkId).Select(p => p.Expansions).FirstAsync();
        var capacity = ParkService.CapacityOf(expansions);
        var leaving = await db.ParkMembers.Where(m => m.ParkId == parkId)
            .OrderBy(m => m.AddedAt).ThenBy(m => m.BankPokemonId) // l'ordre de ParkService.ListAsync
            .Skip(capacity).Select(m => m.BankPokemonId).ToListAsync();
        if (leaving.Count > 0)
            await db.ParkMembers.Where(m => m.ParkId == parkId && leaving.Contains(m.BankPokemonId)).ExecuteDeleteAsync();
        await tx.CommitAsync();

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Compte {User} : parc {Park} à {Expansions} agrandissements (mode debug, {Leaving} Pokémon retirés).",
                userId, parkId, expansions, leaving.Count);
        var message = $"Parc {(grow ? "agrandi" : "réduit")} (mode debug) : il accueille maintenant {capacity} Pokémon.";
        return new(true, message + LeavingMessage(leaving.Count), expansions);
    }

    /// <summary>Parc déjà à sa plus grande (<paramref name="grow"/>) ou à sa plus petite taille.</summary>
    private static string SizeLimitMessage(bool grow) => grow
        ? $"Ce parc a déjà sa plus grande taille ({ParkService.CapacityOf(ParkService.MaxExpansions)} Pokémon)."
        : $"Ce parc a déjà sa plus petite taille ({ParkService.CapacityOf(0)} Pokémon).";

    /// <summary>Fin du message d'un parc réduit : les Pokémon retournés dans la collection (rien s'il n'y en a pas).</summary>
    private static string LeavingMessage(int count) => count switch
    {
        0 => "",
        1 => " Le dernier arrivé est retourné dans ta collection.",
        _ => $" Les {count} derniers arrivés sont retournés dans ta collection.",
    };
}
