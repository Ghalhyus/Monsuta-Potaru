using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MonsutaPotaru.Components.Pages;
using MonsutaPotaru.Data;
using MonsutaPotaru.Services;

namespace MonsutaPotaru.Tests;

/// <summary>Page « Statistiques » : onglets à gauche (Générale, Défis, Multijoueurs), la sienne ou celle d'un autre joueur.</summary>
public sealed class StatisticsPageTests : PageTestContext
{
    private LivingDexService Dexes => Services.GetRequiredService<LivingDexService>();
    private NavigationManager Navigation => Services.GetRequiredService<NavigationManager>();

    private IRenderedComponent<Statistics> Open(string query = "", string? pseudo = null)
    {
        Navigation.NavigateTo("statistiques" + query);
        return pseudo is null ? Render<Statistics>() : Render<Statistics>(p => p.Add(x => x.Pseudo, pseudo));
    }

    [Theory]
    [InlineData(null, Statistics.StatsTab.General)]
    [InlineData("defis", Statistics.StatsTab.Challenges)]
    [InlineData("DEFIS", Statistics.StatsTab.Challenges)]
    [InlineData("multijoueurs", Statistics.StatsTab.Multiplayer)]
    [InlineData("n-importe-quoi", Statistics.StatsTab.General)]
    public void Tab_InTheAddress(string? slug, Statistics.StatsTab tab)
    {
        Assert.Equal(tab, Statistics.TabFromSlug(slug));
        Assert.Equal(tab, Statistics.TabFromSlug(Statistics.TabSlug(tab))); // aller-retour
        Assert.Equal(tab == Statistics.StatsTab.General, Statistics.TabSlug(tab) is null); // l'onglet par défaut n'est pas écrit
    }

    [Fact]
    public void General_ShowsMoneyDepositedAndSpent()
    {
        GiveMoney("sacha", 2_000);
        using (var db = Db.NewContext())
        {
            db.WalletOperations.Add(new WalletOperation { OwnerId = "sacha", Kind = WalletOperationKind.BuyCollectionSlots, Amount = -500 });
            db.SaveChanges();
        }

        var cut = Open();

        cut.WaitForText("Argent déposé");
        var values = cut.FindAll(".stat-value").Select(v => v.TextContent).ToList();
        Assert.Equal([InventoryService.FormatMoney(2_500), InventoryService.FormatMoney(500)], values);
        Assert.Contains("ton porte-monnaie", cut.Text());
        // Onglets : Générale actif, les autres par l'adresse.
        var tabs = cut.FindAll(".stats-tabs a");
        Assert.Equal(["Générale", "Défis", "Multijoueurs"], tabs.Select(t => t.TextContent));
        Assert.Equal("page", tabs[0].GetAttribute("aria-current"));
        Assert.EndsWith("statistiques?onglet=defis", tabs[1].GetAttribute("href"));
        Assert.DoesNotContain("onglet", tabs[0].GetAttribute("href"));
    }

    [Fact]
    public async Task Challenges_LivingDexAverages_ShinyApart()
    {
        var empty = Open("?onglet=defis");
        empty.WaitForText("Aucun living dex pour l'instant.");
        Assert.Contains("Aucun living dex chromatique pour l'instant.", empty.Text());
        Assert.Equal(2, empty.FindAll("a[href='livingdex']").Count);
        Assert.Equal("page", empty.FindAll(".stats-tabs a")[1].GetAttribute("aria-current"));

        AddToBank("sacha", 1, 4, 7);
        Assert.True((await Dexes.CreateGlobalAsync("sacha")).Ok);

        var cut = Open("?onglet=defis");

        cut.WaitForText("Moyenne de tes living dex");
        var normal = cut.FindAll(".block")[0];
        Assert.Equal("1", normal.QuerySelector(".block-count")?.TextContent);
        // Le global n'a pas de Régional ; National : 3 / 1 025 = 0,2 %.
        Assert.Contains("Aucun living dex n'a ce mode.", normal.TextContent);
        Assert.Equal(["Général", "Régional", "National", "Complet"], normal.QuerySelectorAll(".stat-bar-label").Select(l => l.TextContent));
        Assert.Contains(LivingDexGames.Percent(2, 1_000), normal.QuerySelectorAll(".livingdex-progress-label").Select(l => l.TextContent));
        Assert.Equal(["—", "0 / 1", "0 / 1"], normal.QuerySelectorAll(".stat-value").Select(v => v.TextContent));
        Assert.Contains("Aucun living dex chromatique pour l'instant.", cut.FindAll(".block")[1].TextContent);
    }

    [Fact]
    public void Multiplayer_TradesAndBattles()
    {
        var empty = Open("?onglet=multijoueurs");
        empty.WaitForText("Aucun échange pour l'instant");
        Assert.Contains("Aucun combat pour l'instant", empty.Text());

        using (var db = Db.NewContext())
        {
            var at = DateTime.UtcNow;
            db.TradeRecords.AddRange(
                new TradeRecord { At = at, FirstId = "sacha", SecondId = "ondine" },
                new TradeRecord { At = at, FirstId = "ondine", SecondId = "sacha" });
            db.BattleRecords.AddRange(
                new BattleRecord { At = at, FirstId = "sacha", SecondId = "ondine", Format = "f", WinnerId = "sacha" },
                new BattleRecord { At = at, FirstId = "pierre", SecondId = "sacha", Format = "f", WinnerId = "pierre" },
                new BattleRecord { At = at, FirstId = "pierre", SecondId = "sacha", Format = "f" });
            db.SaveChanges();
        }

        var cut = Open("?onglet=multijoueurs");

        cut.WaitForAssertion(() => Assert.Equal(6, cut.FindAll(".stat-value").Count));
        // Échanges réussis, partenaires ; victoires, défaites, nuls, adversaires.
        Assert.Equal(["2", "1", "1", "1", "1", "2"], cut.FindAll(".stat-value").Select(v => v.TextContent));
        Assert.DoesNotContain("pour l'instant", cut.Text());
    }

    [Fact]
    public async Task Visitor_SeesTheStatistics_WhenTheProfileShowsThem()
    {
        GiveMoney("ondine", 300);
        Assert.True((await Dexes.CreateGlobalAsync("ondine")).Ok);

        var general = Open(pseudo: "ondine");
        general.WaitForText("son porte-monnaie");
        Assert.Contains(InventoryService.FormatMoney(300), general.Text());
        Assert.Contains("Lecture seule", general.Text()); // en-tête du joueur
        Assert.Contains("Statistiques", general.Find(".nav-tabs .nav-link.active").TextContent);

        var challenges = Open("?onglet=defis", "ondine");
        challenges.WaitForText("Moyenne de ses living dex");
        Assert.DoesNotContain("Crée-en un", challenges.Text());

        var multiplayer = Open("?onglet=multijoueurs", "ondine");
        multiplayer.WaitForText("Échanges réussis");
        Assert.DoesNotContain("pour l'instant", multiplayer.Text());

        // Page décochée dans « Visibilité » : comme un joueur inconnu.
        UpdateUser("ondine", u => u.PublicPages = PublicPages.All & ~PublicPages.Statistics);
        var hidden = Open(pseudo: "ondine");
        hidden.WaitForText(PlayerService.HiddenMessage);
        Assert.DoesNotContain("Argent", hidden.Text());
    }
}
