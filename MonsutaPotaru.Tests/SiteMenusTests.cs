using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MonsutaPotaru.Components.Layout;
using MonsutaPotaru.Data;
using MonsutaPotaru.Services;

namespace MonsutaPotaru.Tests;

/// <summary>
/// Menu latéral en rubriques (Mes Pokémon, Inventaire, Défis, Multijoueurs, Sauvegardes), le même dans le cadre du thème
/// Ardoise (<see cref="ArdoiseLayout"/>) et dans le menu des thèmes Classique et Pokédex (<see cref="NavMenu"/>) :
/// rubriques fermées, chacune ouverte d'elle-même sur l'une de ses pages ; porte-monnaie dans la barre du haut.
/// </summary>
public sealed class SiteMenusTests : PageTestContext
{
    private static readonly string[] Sections = ["Mes Pokémon", "Inventaire", "Défis", "Multijoueurs", "Sauvegardes"];

    private static readonly string[][] Pages =
    [
        ["Ma collection", "Parc", "Dépôt / Retrait", "Échange interne"],
        ["Banque d'objets", "Transfert d'inventaire"],
        ["Living dex", "Rubans"],
        ["Rechercher un joueur", "Amis", "Échange", "Combat"],
        ["Mes sauvegardes", "Synchronisation 3DS"],
    ];

    private static readonly string[][] Hrefs =
    [
        ["collection", "parc", "transfert", "echange-interne"],
        ["inventaire", "inventaire/transfert"],
        ["livingdex", "rubans"],
        ["joueurs", "amis", "echanges", "combats"],
        ["sauvegardes", "sauvegardes/3ds"],
    ];

    private void GoTo(string url) => Services.GetRequiredService<NavigationManager>().NavigateTo(url);

    private IRenderedComponent<ArdoiseLayout> Ardoise(string url)
    {
        GoTo(url);
        return Render<ArdoiseLayout>();
    }

    private IRenderedComponent<NavMenu> Classic(string url)
    {
        GoTo(url);
        return Render<NavMenu>();
    }

    private static IEnumerable<IElement> ArdoiseGroups(IRenderedComponent<ArdoiseLayout> cut) => cut.FindAll(".ardoise-nav > .ardoise-group");

    private static IEnumerable<IElement> ClassicGroups(IRenderedComponent<NavMenu> cut) => cut.FindAll("nav > .nav-group > details").Take(Sections.Length);

    private static string Text(IElement e) => e.TextContent.Trim();

    [Fact]
    public void Ardoise_SectionsAndPages_InTheMenuOrder()
    {
        var cut = Ardoise("");

        var groups = ArdoiseGroups(cut).ToList();
        Assert.Equal(Sections, groups.Select(g => Text(g.QuerySelector(".ardoise-group-title .ardoise-label")!)));
        for (var i = 0; i < groups.Count; i++)
        {
            Assert.Equal(Sections[i], Text(groups[i].QuerySelector(".ardoise-subitems-title")!)); // titre du menu replié
            Assert.Equal(Pages[i], groups[i].QuerySelectorAll(".ardoise-subitems a .ardoise-label").Select(Text));
            Assert.Equal(Hrefs[i], groups[i].QuerySelectorAll(".ardoise-subitems a").Select(a => a.GetAttribute("href")));
            Assert.Equal(Sections[i], groups[i].QuerySelector(".ardoise-group-title")!.GetAttribute("title")); // survol, menu replié
        }
        // Accueil seul avant les rubriques ; chaque rubrique a son icône (seule visible, menu replié)
        Assert.Equal(["Accueil"], cut.FindAll(".ardoise-nav > a .ardoise-label").Select(Text));
        Assert.Equal(["bi-pokeball-nav-menu", "bi-bag-nav-menu", "bi-trophy-nav-menu", "bi-person-badge-nav-menu", "bi-save-nav-menu"],
            groups.Select(g => g.QuerySelector(".ardoise-group-title .bi")!.ClassList.Last()));
    }

    [Fact]
    public void Classic_SectionsAndPages_InTheMenuOrder()
    {
        var cut = Classic("");

        var groups = ClassicGroups(cut).ToList();
        Assert.Equal(Sections, groups.Select(g => Text(g.QuerySelector("summary")!)));
        for (var i = 0; i < groups.Count; i++)
        {
            Assert.Equal(Pages[i], groups[i].QuerySelectorAll(".nav-subitems a").Select(Text));
            Assert.Equal(Hrefs[i], groups[i].QuerySelectorAll(".nav-subitems a").Select(a => a.GetAttribute("href")));
        }
        Assert.Equal("Accueil", Text(cut.Find("nav > .nav-item a")));
    }

    [Theory]
    [InlineData("", -1)]
    [InlineData("collection?tri=nom", 0)]
    [InlineData("parc", 0)]
    [InlineData("transfert", 0)]
    [InlineData("banque", 0)]
    [InlineData("echange-interne?de=1&vers=2", 0)]
    [InlineData("inventaire", 1)]
    [InlineData("inventaire/transfert?gauche=1&droite=2", 1)]
    [InlineData("livingdex", 2)]
    [InlineData("livingdex/3", 2)]
    [InlineData("rubans", 2)]
    [InlineData("rubans/4?filtre=manquants", 2)]
    [InlineData("joueurs/ondine/inventaire", 3)] // l'inventaire d'un autre joueur relève de Multijoueurs
    [InlineData("echanges", 3)]
    [InlineData("combats", 3)]
    [InlineData("sauvegardes/3ds", 4)]
    [InlineData("statistiques", -1)]
    public void Sections_ClosedExceptOnTheirPages(string url, int open)
    {
        var ardoise = ArdoiseGroups(Ardoise(url)).ToList();
        var classic = ClassicGroups(Classic(url)).ToList();

        for (var i = 0; i < Sections.Length; i++)
        {
            Assert.Equal(i == open, ardoise[i].ClassList.Contains("ouvert"));
            Assert.Equal(i == open ? "true" : "false", ardoise[i].QuerySelector(".ardoise-group-title")!.GetAttribute("aria-expanded"));
            Assert.Equal(i == open, classic[i].HasAttribute("open"));
        }
    }

    [Theory]
    [InlineData("inventaire", "Banque d'objets")]
    [InlineData("inventaire/transfert", "Transfert d'inventaire")]
    [InlineData("sauvegardes", "Mes sauvegardes")]
    public void OnlyThePageShown_IsActive(string url, string page)
    {
        // « Banque d'objets » (inventaire) ne s'allume pas sur « Transfert d'inventaire » (inventaire/transfert)
        Assert.Equal([page], Ardoise(url).FindAll(".ardoise-nav a.active .ardoise-label").Select(Text));
        Assert.Equal([page], Classic(url).FindAll("nav a.active").Select(Text));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Administration_OnlyForAdministrators(bool admin)
    {
        if (admin)
            SignIn("sacha", SiteRoles.Admin);

        Assert.Equal(admin ? 1 : 0, Ardoise("").FindAll(".ardoise-nav > a[href=administration]").Count);
        Assert.Equal(admin ? 1 : 0, Classic("").FindAll("a[href=administration]").Count);
    }

    [Fact]
    public void Wallet_InTheTopBar_NotInTheMenu()
    {
        GiveMoney("sacha", 12_345);
        var money = InventoryService.FormatMoney(12_345);

        var ardoise = Ardoise("collection");
        var wallet = ardoise.Find(".ardoise-topbar a.ardoise-wallet");
        Assert.Equal("inventaire", wallet.GetAttribute("href"));
        Assert.Contains(money, wallet.TextContent);
        Assert.DoesNotContain(money, ardoise.Find(".ardoise-menu").TextContent);

        // Classique et Pokédex : le menu ne l'affiche plus, la barre du haut (MainLayout) a WalletLink
        Assert.DoesNotContain(money, Classic("collection").Markup);
    }

    [Fact]
    public async Task WalletLink_ShowsTheBalance_AndFollowsItsChanges()
    {
        var cut = Render<WalletLink>();
        Assert.Empty(cut.FindAll("a")); // pas encore lu (ou pas de compte) : rien

        GiveMoney("sacha", 500);
        var state = Services.GetRequiredService<WalletState>();
        await cut.InvokeAsync(() => state.RefreshAsync("sacha"));
        var link = cut.Find("a.top-wallet");
        Assert.Equal("inventaire", link.GetAttribute("href"));
        Assert.Equal("Ton porte-monnaie (Banque d'objets)", link.GetAttribute("title"));
        Assert.Contains(InventoryService.FormatMoney(500), link.TextContent);

        GiveMoney("sacha", 800); // retrait d'argent : la page « Banque d'objets » relit le solde
        await cut.InvokeAsync(() => state.RefreshAsync("sacha"));
        Assert.Contains(InventoryService.FormatMoney(800), cut.Find("a.top-wallet").TextContent);
    }
}
