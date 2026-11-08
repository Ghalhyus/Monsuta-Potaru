using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MonsutaPotaru.Components.Layout;

namespace MonsutaPotaru.Tests;

/// <summary>
/// Cadre du thème Ardoise : le compte en bas du menu latéral (rubrique au pseudo ; sans compte, inscription et connexion),
/// en plus du menu du compte de la barre du haut.
/// </summary>
public sealed class ArdoiseLayoutTests : PageTestContext
{
    private IRenderedComponent<ArdoiseLayout> RenderAt(string url)
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo(url);
        return Render<ArdoiseLayout>();
    }

    [Fact]
    public void SideMenu_HasTheAccount_ClosedOutsideItsPages()
    {
        var cut = RenderAt("collection");

        var side = cut.Find(".ardoise-side-account");
        Assert.Equal("S", side.QuerySelector(".ardoise-avatar")!.TextContent);
        Assert.Equal("sacha", side.QuerySelector(".ardoise-group-title .ardoise-label")!.TextContent);
        Assert.Equal(["Account/Manage", "statistiques"], side.QuerySelectorAll("a").Select(a => a.GetAttribute("href")));
        Assert.Equal("Account/Logout", side.QuerySelector("form")!.GetAttribute("action"));
        Assert.Equal("collection", side.QuerySelector("input[name=ReturnUrl]")!.GetAttribute("value"));
        Assert.Contains("Déconnexion", side.QuerySelector("form button[type=submit]")!.TextContent);

        var group = side.QuerySelector(".ardoise-group")!;
        Assert.False(group.ClassList.Contains("ouvert"));
        Assert.Equal("false", group.QuerySelector(".ardoise-group-title")!.GetAttribute("aria-expanded"));

        // Le menu de la barre du haut reste
        Assert.Single(cut.FindAll(".ardoise-topbar .ardoise-account"));
    }

    [Theory]
    [InlineData("statistiques")]
    [InlineData("Account/Manage/Themes")]
    public void SideMenu_AccountOpensOnItsPages(string url)
    {
        var cut = RenderAt(url);

        var group = cut.Find(".ardoise-side-account .ardoise-group");
        Assert.True(group.ClassList.Contains("ouvert"));
        Assert.Equal("true", group.QuerySelector(".ardoise-group-title")!.GetAttribute("aria-expanded"));
        Assert.Single(group.QuerySelectorAll("a.active"));
    }

    [Fact]
    public void SideMenu_WithoutAccount_OffersRegisterAndLogin()
    {
        AddAuthorization().SetNotAuthorized();
        var cut = RenderAt("");

        var side = cut.Find(".ardoise-side-account");
        Assert.Equal(["Account/Register", "Account/Login"], side.QuerySelectorAll("a").Select(a => a.GetAttribute("href")));
        Assert.Equal(["Inscription", "Connexion"], side.QuerySelectorAll("a .ardoise-label").Select(l => l.TextContent));
        Assert.Empty(side.QuerySelectorAll("form"));
        Assert.Empty(cut.FindAll(".ardoise-account"));
    }
}
