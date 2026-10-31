using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MonsutaPotaru.Data;
using MonsutaPotaru.Services;

namespace MonsutaPotaru.Tests;

/// <summary>
/// Mode debug : réservé aux administrateurs (revérifié à chaque action), argent ajouté en montants fixes avec une ligne du
/// journal, parc agrandi ou réduit gratuitement (réduit : les derniers arrivés repartent).
/// </summary>
public sealed class DebugServiceTests : IDisposable
{
    private readonly TestDatabase _db = new("sacha", "ondine");
    private readonly DebugMode _mode = new();
    private readonly DebugService _debug;

    public DebugServiceTests()
    {
        _debug = new DebugService(_db.Factory, _mode, NullLogger<DebugService>.Instance);
        using var db = _db.NewContext();
        db.UserRoles.Add(new() { UserId = "sacha", RoleId = SiteRoles.AdminId });
        db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task OnlyAnAdmin_CanActivate()
    {
        Assert.False((await _debug.SetActiveAsync("ondine", true)).Ok);
        Assert.False(await _debug.IsActiveAsync("ondine"));

        Assert.True((await _debug.SetActiveAsync("sacha", true)).Ok);
        Assert.True(await _debug.IsActiveAsync("sacha"));
        Assert.True((await _debug.SetActiveAsync("sacha", false)).Ok);
        Assert.False(await _debug.IsActiveAsync("sacha"));
    }

    [Fact]
    public async Task RoleRemoved_StopsDebugMode()
    {
        await _debug.SetActiveAsync("sacha", true);
        await using (var db = _db.NewContext())
            await db.UserRoles.Where(r => r.UserId == "sacha").ExecuteDeleteAsync();

        Assert.False((await _debug.AddMoneyAsync("sacha", 10_000)).Ok);
        Assert.False(_mode.IsActive("sacha")); // coupé, même si le rôle revient
    }

    [Fact]
    public async Task Inactive_RefusesEverything()
    {
        Assert.False((await _debug.AddMoneyAsync("sacha", 10_000)).Ok);
        Assert.False((await _debug.ResizeParkAsync("sacha", grow: true)).Ok);
        _mode.Set("ondine", true); // mode resté actif pour un compte qui n'est pas administrateur
        Assert.False((await _debug.AddMoneyAsync("ondine", 10_000)).Ok);

        await using var db = _db.NewContext();
        Assert.False(await db.Wallets.AnyAsync());
        Assert.Equal(0, await db.Users.Where(u => u.Id == "sacha").Select(u => u.ParkExpansions).FirstAsync());
    }

    [Fact]
    public async Task AddMoney_FixedAmounts_WrittenInTheJournal()
    {
        await _debug.SetActiveAsync("sacha", true);
        Assert.True((await _debug.AddMoneyAsync("sacha", 10_000)).Ok); // porte-monnaie créé
        Assert.True((await _debug.AddMoneyAsync("sacha", 1_000_000)).Ok);
        Assert.False((await _debug.AddMoneyAsync("sacha", 5_000_000)).Ok); // montant envoyé par un navigateur modifié

        await using var db = _db.NewContext();
        Assert.Equal(1_010_000, await db.Wallets.Where(w => w.OwnerId == "sacha").Select(w => w.Money).FirstAsync());
        var operations = await db.WalletOperations.OrderBy(o => o.Id).ToListAsync();
        Assert.Equal([(WalletOperationKind.DebugCredit, 10_000L, 10_000L), (WalletOperationKind.DebugCredit, 1_000_000L, 1_010_000L)],
            operations.Select(o => (o.Kind, o.Amount, o.BalanceAfter)));
    }

    [Fact]
    public async Task ResizePark_FreeAndBounded()
    {
        await _debug.SetActiveAsync("sacha", true);
        Assert.False((await _debug.ResizeParkAsync("sacha", grow: false)).Ok); // déjà la plus petite taille
        for (var i = 1; i <= ParkService.MaxExpansions; i++)
            Assert.Equal(i, (await _debug.ResizeParkAsync("sacha", grow: true)).Expansions);
        Assert.False((await _debug.ResizeParkAsync("sacha", grow: true)).Ok); // déjà la plus grande

        await using var db = _db.NewContext();
        Assert.Equal(ParkService.MaxExpansions, await db.Users.Where(u => u.Id == "sacha").Select(u => u.ParkExpansions).FirstAsync());
        Assert.False(await db.WalletOperations.AnyAsync()); // gratuit, rien dans le journal
    }

    [Fact]
    public async Task ShrinkPark_LastArrivalsGoBackToTheCollection()
    {
        await _debug.SetActiveAsync("sacha", true);
        await _debug.ResizeParkAsync("sacha", grow: true); // 40 Pokémon
        var start = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        int[] ids;
        await using (var db = _db.NewContext())
        {
            var pokemon = Enumerable.Range(0, 25).Select(i => new BankPokemon
            {
                OwnerId = "sacha", Species = 25, SpeciesName = "Pikachu", Nickname = $"P{i}", Level = 10,
                DepositedAt = start, Data = [1],
            }).ToList();
            db.BankPokemon.AddRange(pokemon);
            await db.SaveChangesAsync();
            ids = pokemon.Select(p => p.Id).ToArray();
            // Arrivés dans l'ordre inverse de leur numéro : c'est la date d'arrivée qui compte
            db.ParkMembers.AddRange(ids.Select((id, i) => new ParkMember { BankPokemonId = id, OwnerId = "sacha", AddedAt = start.AddMinutes(-i) }));
            await db.SaveChangesAsync();
        }

        var result = await _debug.ResizeParkAsync("sacha", grow: false);
        Assert.True(result.Ok);
        Assert.Contains("5 derniers arrivés", result.Message);

        await using var check = _db.NewContext();
        var kept = await check.ParkMembers.Select(m => m.BankPokemonId).ToListAsync();
        Assert.Equal(ids.Skip(5).Order(), kept.Order()); // les 5 premiers numéros sont les derniers arrivés
        Assert.Equal(25, await check.BankPokemon.CountAsync()); // toujours dans la collection
    }
}
