using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MonsutaPotaru.Components;
using MonsutaPotaru.Services;

namespace MonsutaPotaru.Tests;

/// <summary>
/// Navigation entre la page d'un autre joueur et la sienne (même composant, deux adresses) : le site entier rendu par le
/// routeur, menu compris. La page doit être recréée, sinon elle garde le compte de l'autre joueur.
/// </summary>
public sealed class RoutesTests : PageTestContext
{
    public RoutesTests() : base("ondine") { }

    [Theory]
    [InlineData(SiteThemes.Ardoise)]
    [InlineData(SiteThemes.Pokedex)]
    public void OwnPage_AfterAnotherPlayersPage_ShowsOwnAccount(string theme)
    {
        AddToBank("sacha", 25, 133);
        AddToBank("ondine", 1);
        var nav = Services.GetRequiredService<NavigationManager>();
        var cut = Render<Routes>(p => p.Add(r => r.Theme, theme));

        nav.NavigateTo("joueurs/sacha/collection");
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".collection .slot").Count));
        Assert.DoesNotContain("Ma collection", cut.Find("h1").TextContent);

        nav.NavigateTo("collection"); // « Ma collection » du menu
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".collection .slot")));
        Assert.Contains("Ma collection", cut.Find("h1").TextContent);

        nav.NavigateTo("joueurs/sacha/collection"); // et retour
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".collection .slot").Count));
    }

    [Fact]
    public void PageOwner_IsThePseudoOfTheAddress()
    {
        var withPseudo = new RouteData(typeof(MonsutaPotaru.Components.Pages.Collection), new Dictionary<string, object?> { ["Pseudo"] = "sacha" });
        Assert.Equal("sacha", Routes.PageOwnerOf(withPseudo));
        Assert.Equal("", Routes.PageOwnerOf(new RouteData(typeof(MonsutaPotaru.Components.Pages.Collection), new Dictionary<string, object?>())));
    }
}
