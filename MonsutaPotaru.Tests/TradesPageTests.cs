using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MonsutaPotaru.Components.Pages;

namespace MonsutaPotaru.Tests;

/// <summary>Page « Échanges » : deux amis sur la même page (même TradeHub), demande, choix, validation, annulations.</summary>
public sealed class TradesPageTests : PageTestContext
{
    private async Task<(IRenderedComponent<Trades> Sacha, IRenderedComponent<Trades> Ondine)> Requested()
    {
        await MakeFriends("sacha", "ondine");
        await StoreBw2("sacha", "Sacha");
        await StoreBw2("ondine", "Ondine");
        var sacha = Render<Trades>();
        var ondine = RenderAs<Trades>("ondine");
        sacha.WaitForText("Choisis un ami");
        await sacha.Find("select.trade-friend").ChangeAsync(new() { Value = "ondine" });
        sacha.WaitForText("Proposer l'échange");
        await sacha.Click("Proposer l'échange");
        sacha.WaitForText("En attente de la réponse de ondine");
        ondine.WaitForText("te propose un échange");
        return (sacha, ondine);
    }

    [Fact]
    public async Task Trade_BothChoose_ThenBothConfirm()
    {
        var (sacha, ondine) = await Requested();
        await ondine.Click("Accepter");
        sacha.WaitForText("Pokémon de ondine que ta sauvegarde peut recevoir");
        ondine.WaitForText("Pokémon de sacha que ta sauvegarde peut recevoir");

        await sacha.Find(".trade-candidate").ClickAsync(new());
        ondine.WaitForAssertion(() => Assert.DoesNotContain("n'a pas encore choisi", ondine.Text()));
        await ondine.Find(".trade-candidate").ClickAsync(new());
        sacha.WaitForText("Valider l'échange");

        // Un refus ramène au choix ; puis les deux valident
        await sacha.Click("Refuser");
        sacha.WaitForText("Choisis ci-dessous");
        await sacha.Find(".trade-candidate").ClickAsync(new());
        await ondine.Find(".trade-candidate").ClickAsync(new());
        sacha.WaitForText("Valider l'échange");
        await sacha.Click("Valider l'échange");
        ondine.WaitForText("sacha a validé.");
        await ondine.Click("Valider l'échange");
        sacha.WaitForAssertion(() => Assert.True(sacha.HasButton("Nouvel échange")), TimeSpan.FromSeconds(10));
        ondine.WaitForAssertion(() => Assert.True(ondine.HasButton("Nouvel échange")));
        await sacha.Click("Nouvel échange");
        sacha.WaitForText("Nouvel échange");
    }

    [Fact]
    public async Task Request_Refused_ThenCancelled()
    {
        var (sacha, ondine) = await Requested();
        await ondine.Click("Refuser");
        sacha.WaitForText("Retour à l'accueil");
        await sacha.Click("Nouvel échange");

        await sacha.Find("select.trade-friend").ChangeAsync(new() { Value = "ondine" });
        await sacha.Click("Proposer l'échange");
        sacha.WaitForText("En attente");
        await sacha.Click("Annuler l'échange");
        ondine.WaitForAssertion(() => Assert.DoesNotContain("te propose un échange", ondine.Text()));
        await sacha.Click("Nouvel échange");
        await sacha.Find("select.trade-friend").ChangeAsync(new() { Value = "" });
    }

    [Fact]
    public async Task LeavingThePage_AsksFirst_ThenCancelsTheTrade()
    {
        var (sacha, ondine) = await Requested();
        await ondine.Click("Accepter");
        sacha.WaitForText("Choisis ci-dessous");
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("amis"); // « confirm » répond non (JavaScript simulé)
        Assert.Contains("Choisis ci-dessous", sacha.Text());
        JSInterop.Setup<bool>("confirm", _ => true).SetResult(true);
        navigation.NavigateTo("amis");
        ondine.WaitForText("sacha a quitté l'échange.");
        await DisposeComponentsAsync();
    }

    [Fact]
    public async Task WithoutFriend_OrMatchingSave_SaysSo()
    {
        var alone = Render<Trades>();
        alone.WaitForText("Il te faut un ami pour échanger");
        await MakeFriends("sacha", "pierre");
        var cut = Render<Trades>();
        await cut.Find("select.trade-friend").ChangeAsync(new() { Value = "pierre" });
        cut.WaitForText("aucune sauvegarde stockée de la même génération");
    }
}
