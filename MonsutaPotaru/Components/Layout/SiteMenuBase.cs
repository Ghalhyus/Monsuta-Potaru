using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Routing;
using MonsutaPotaru.Services;

namespace MonsutaPotaru.Components.Layout;

/// <summary>
/// Ce que les menus du site ont en commun (menu du thème Classique et du Pokédex, <c>NavMenu</c> ; cadre du thème Ardoise,
/// <c>ArdoiseLayout</c>) : adresse de la page affichée, solde du porte-monnaie et demandes reçues (amis, échanges,
/// combats), tenus à jour en direct.
/// </summary>
public abstract class SiteMenuBase : ComponentBase, IDisposable
{
    [Inject] protected NavigationManager NavigationManager { get; set; } = default!;
    [Inject] protected WalletState Wallet { get; set; } = default!;
    [Inject] private FriendService Friends { get; set; } = default!;
    [Inject] private TradeHub Trades { get; set; } = default!;
    [Inject] private BattleHub Battles { get; set; } = default!;

    [CascadingParameter] private Task<AuthenticationState>? AuthState { get; set; }

    /// <summary>Adresse de la page affichée, relative à la racine du site (« sauvegardes/3ds »).</summary>
    protected string? currentUrl;

    private string? userId;
    protected int pendingRequests; // demandes d'ami reçues, relues à chaque navigation
    protected int tradeRequests; // demandes d'échange reçues (en direct, TradeHub)
    protected int battleRequests; // demandes de combat reçues (en direct, BattleHub)

    /// <summary>Toutes les demandes reçues : la pastille du menu « Multijoueurs ».</summary>
    protected int PlayerRequests => pendingRequests + tradeRequests + battleRequests;

    // Rubriques du menu : fermées, chacune ouverte d'elle-même sur l'une de ses pages (puis ouvertes et fermées d'un clic).

    /// <summary>Rubrique « Mes Pokémon » ouverte : sur « Ma collection », « Parc », « Dépôt / Retrait » ou « Échange interne ».</summary>
    protected bool pokemonOpen => IsOn("collection", "parc", "transfert", "banque", "echange-interne");

    /// <summary>Rubrique « Inventaire » ouverte : sur « Banque d'objets » ou « Transfert d'inventaire ».</summary>
    protected bool inventoryOpen => IsOn("inventaire");

    /// <summary>Rubrique « Défis » ouverte : sur les living dex ou les rubans (la liste ou l'un d'eux).</summary>
    protected bool challengesOpen => IsOn("livingdex", "rubans");

    /// <summary>Rubrique « Sauvegardes » ouverte : sur « Mes sauvegardes » ou « Synchronisation 3DS ».</summary>
    protected bool savesOpen => IsOn("sauvegardes");

    /// <summary>Rubrique « Multijoueurs » ouverte : sur la recherche, les pages d'un joueur, « Amis », « Échange » ou « Combat ».</summary>
    protected bool playersOpen => IsOn("joueurs", "amis", "echanges", "combats");

    /// <summary>
    /// La page affichée est l'une de ces pages ou une page sous elles (« livingdex/3 », « collection?tri=nom ») ; la
    /// collection d'un autre joueur (« joueurs/sacha/collection ») relève de « joueurs ».
    /// </summary>
    private bool IsOn(params string[] pages)
    {
        if (currentUrl is not { } url)
            return false;
        var first = url.Split(['/', '?', '#'])[0];
        return pages.Any(p => string.Equals(first, p, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Menu du pseudo ouvert : sur les paramètres du profil ou les statistiques.</summary>
    protected bool accountOpen => currentUrl is { } url && (url.StartsWith("Account/Manage", StringComparison.OrdinalIgnoreCase) || IsOn("statistiques"));

    protected override async Task OnInitializedAsync()
    {
        currentUrl = NavigationManager.ToBaseRelativePath(NavigationManager.Uri);
        NavigationManager.LocationChanged += OnLocationChanged;
        Wallet.Changed += OnWalletChanged;
        Friends.Changed += OnFriendsChanged;
        Trades.RequestsChanged += OnTradeRequestsChanged;
        Battles.RequestsChanged += OnBattleRequestsChanged;

        // Solde du porte-monnaie pour l'utilisateur connecté (rafraîchi par « Banque d'objets » après un retrait).
        userId = AuthState is null ? null : (await AuthState).User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is not null)
        {
            await Wallet.RefreshAsync(userId);
            pendingRequests = await Friends.PendingCountAsync(userId);
            tradeRequests = Trades.Incoming(userId).Count;
            battleRequests = Battles.Incoming(userId).Count;
        }
    }

    private void OnBattleRequestsChanged(string user)
    {
        if (user != userId)
            return;
        InvokeAsync(() =>
        {
            battleRequests = Battles.Incoming(user).Count;
            StateHasChanged();
        });
    }

    private void OnTradeRequestsChanged(string user)
    {
        if (user != userId)
            return;
        InvokeAsync(() =>
        {
            tradeRequests = Trades.Incoming(user).Count;
            StateHasChanged();
        });
    }

    private void OnWalletChanged() => InvokeAsync(StateHasChanged);

    private void OnFriendsChanged() => InvokeAsync(async () =>
    {
        if (userId is not null)
            pendingRequests = await Friends.PendingCountAsync(userId);
        StateHasChanged();
    });

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e) => InvokeAsync(async () =>
    {
        currentUrl = NavigationManager.ToBaseRelativePath(e.Location);
        if (userId is not null)
            pendingRequests = await Friends.PendingCountAsync(userId);
        StateHasChanged();
    });

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Se désabonne des événements (navigation, porte-monnaie, demandes reçues).</summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
            return;
        NavigationManager.LocationChanged -= OnLocationChanged;
        Wallet.Changed -= OnWalletChanged;
        Friends.Changed -= OnFriendsChanged;
        Trades.RequestsChanged -= OnTradeRequestsChanged;
        Battles.RequestsChanged -= OnBattleRequestsChanged;
    }
}
