using System.Security.Claims;
using Bunit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MonsutaPotaru.Data;
using MonsutaPotaru.Services;

namespace MonsutaPotaru.Tests;

/// <summary>
/// Pages et composants Blazor rendus par bUnit, avec les vrais services sur une base SQLite temporaire (<see cref="TestDatabase"/>)
/// et un compte connecté (« sacha » par défaut). Le JavaScript du navigateur est simulé (mode « souple » : chaque appel répond
/// la valeur par défaut) ; un test peut préparer une réponse (<c>JSInterop.Setup</c>).
/// </summary>
public abstract class PageTestContext : BunitContext
{
    protected TestDatabase Db { get; } = new("sacha", "ondine", "pierre");

    protected PageTestContext(string userId = "sacha")
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(Db.Factory);
        Services.AddSingleton(Db.Config);
        Services.AddSingleton<IWebHostEnvironment>(new TestDatabase.FakeEnvironment());
        Services.AddLogging();
        Services.AddSingleton<PokemonInfo>();
        Services.AddScoped<AdminService>();
        Services.AddScoped<PreferencesService>();
        Services.AddSingleton<DebugMode>();
        Services.AddScoped<DebugService>();
        Services.AddScoped<BankService>();
        Services.AddScoped<StoredSaveService>();
        Services.AddScoped<InventoryService>();
        Services.AddScoped<ParkService>();
        Services.AddScoped<LivingDexService>();
        Services.AddScoped<RibbonService>();
        Services.AddScoped<StatisticsService>();
        Services.AddScoped<WalletState>();
        Services.AddScoped<SaveSession>();
        Services.AddScoped<SyncService>();
        Services.AddScoped<PlayerService>();
        Services.AddScoped<FriendService>();
        Services.AddScoped<TradeService>();
        Services.AddSingleton<TradeHub>();
        Services.AddScoped<BattleService>();
        Services.AddSingleton(_ => new BattleHub(BattleTests.Showdown(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<BattleHub>.Instance));
        // Pseudo cherché comme le fait Identity (NormalizedUserName) : pages des autres joueurs.
        foreach (var id in new[] { "sacha", "ondine", "pierre" })
            UpdateUser(id, u => u.NormalizedUserName = id.ToUpperInvariant());
        SignIn(userId);
    }

    /// <summary>Donne le rôle d'administrateur à ce compte (dans la base : AdminService, DebugService le relisent).</summary>
    protected void MakeAdmin(string userId)
    {
        using var db = Db.NewContext();
        db.UserRoles.Add(new() { UserId = userId, RoleId = SiteRoles.AdminId });
        db.SaveChanges();
    }

    /// <summary>Rend deux comptes amis (demande acceptée).</summary>
    protected async Task MakeFriends(string a, string b)
    {
        var friends = Services.GetRequiredService<FriendService>();
        Assert.True((await friends.RequestAsync(a, b)).Ok);
        var request = (await friends.ListAsync(b)).Received.Single();
        Assert.True((await friends.AcceptAsync(b, request.RequestId)).Ok);
    }

    /// <summary>Compte connecté (identifiant = pseudo dans la base de test), avec ses rôles.</summary>
    protected void SignIn(string userId, params string[] roles)
    {
        var auth = AddAuthorization();
        auth.SetAuthorized(userId);
        auth.SetClaims(new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Name, userId));
        if (roles.Length > 0)
            auth.SetRoles(roles);
    }

    /// <summary>
    /// Page rendue pour un autre compte que celui de <see cref="SignIn"/> (deux joueurs dans le même test : échanges, combats),
    /// par un état de connexion transmis à la page.
    /// </summary>
    protected IRenderedComponent<T> RenderAs<T>(string userId) where T : Microsoft.AspNetCore.Components.IComponent
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Name, userId)], "Test");
        var state = Task.FromResult(new Microsoft.AspNetCore.Components.Authorization.AuthenticationState(new ClaimsPrincipal(identity)));
        return Render<T>(p => p.AddCascadingValue(state));
    }

    /// <summary>Stocke une vraie sauvegarde de MonsutaPotaru.Tests/Sauvegardes pour ce compte ; rend son identifiant.</summary>
    protected async Task<int> StoreReal(string owner, string file)
    {
        var data = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Sauvegardes", file));
        var result = await StoredSaves.AddAsync(owner, file, data);
        Assert.True(result.Ok, result.Message);
        return (await StoredSaves.ListAsync(owner)).Single(s => s.FileName == file).Id;
    }

    protected StoredSaveService StoredSaves => Services.GetRequiredService<StoredSaveService>();

    /// <summary>Stocke une sauvegarde de Noir 2 (un Pikachu, de l'argent, des objets) et rend son identifiant.</summary>
    protected async Task<int> StoreBw2(string owner = "sacha", string ot = "Ludo", uint money = 100_000,
        params (int Item, int Count)[] items)
    {
        var sav = new PKHeX.Core.SAV5B2W2 { OT = ot, TID16 = 12345, SID16 = (ushort)(ot.Length * 1000), PlayedHours = 10, Money = money };
        sav.SetBoxSlotAtIndex(SaveSessionTests.CreatePk5(25, 5, ot), 0, 0);
        sav.SetBoxSlotAtIndex(SaveSessionTests.CreatePk5(4, 12, ot, 1), 0, 1);
        if (items.Length > 0)
            SaveSessionTests.GiveItems(sav, items);
        var result = await StoredSaves.AddAsync(owner, $"{ot}.sav", sav.Write().ToArray());
        Assert.True(result.Ok, result.Message);
        return (await StoredSaves.ListAsync(owner)).Single(s => s.TrainerName == ot).Id;
    }

    /// <summary>Met ces espèces (vrais Pokémon de Noir 2, niveau 10) dans le stockage interne ; rend leurs identifiants.</summary>
    protected List<int> AddToBank(string owner, params ushort[] species)
    {
        using var db = Db.NewContext();
        var info = Services.GetRequiredService<PokemonInfo>();
        var entries = species.Select((s, i) =>
            BankService.CreateEntry(info, SaveSessionTests.CreatePk5(s, 10, owner, 100 + i), owner, DateTime.UtcNow)).ToList();
        db.BankPokemon.AddRange(entries);
        db.SaveChanges();
        return entries.Select(e => e.Id).ToList();
    }

    /// <summary>Met cette somme dans le porte-monnaie du compte.</summary>
    protected void GiveMoney(string owner, long money)
    {
        using var db = Db.NewContext();
        if (db.Wallets.Find(owner) is { } wallet)
            wallet.Money = money;
        else
            db.Wallets.Add(new Wallet { OwnerId = owner, Money = money });
        db.SaveChanges();
    }

    /// <summary>Modifie un compte de la base de test (visibilité, limite de la collection...).</summary>
    protected void UpdateUser(string userId, Action<ApplicationUser> change)
    {
        using var db = Db.NewContext();
        var user = db.Users.Single(u => u.Id == userId);
        change(user);
        db.SaveChanges();
    }

    /// <summary>Tris par défaut d'un compte (« Mon compte » → « Préférences »).</summary>
    protected void SetSorts(string userId, SortPreferences sorts) => UpdateUser(userId, u => u.SortPreferences = sorts.ToJson());

    /// <summary>Lignes d'une table de la base de test.</summary>
    protected List<T> Rows<T>() where T : class
    {
        using var db = Db.NewContext();
        return db.Set<T>().AsNoTracking().ToList();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            Db.Dispose();
    }
}
