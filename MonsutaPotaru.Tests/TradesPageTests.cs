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

        // Chacun voit son animation : ce qu'il envoie à l'autre, ce qu'il reçoit ; « Passer » la ferme pour de bon.
        Assert.Contains("à ondine.", sacha.Find(".trade-show").TextContent);
        Assert.Contains("ondine t'envoie", sacha.Find(".trade-show").TextContent);
        Assert.Contains("à sacha.", ondine.Find(".trade-show").TextContent);
        await sacha.Click("Passer");
        Assert.Empty(sacha.FindAll(".trade-show"));
        sacha.Render();
        Assert.Empty(sacha.FindAll(".trade-show"));
        Assert.Single(ondine.FindAll(".trade-show"));
        await sacha.Click("Nouvel échange");
        sacha.WaitForText("Nouvel échange");
    }

    [Fact]
    public async Task SearchAndChips_FilterTheFriendsPokemon()
    {
        var (sacha, ondine) = await Requested();
        await ondine.Click("Accepter");
        sacha.WaitForText("Pokémon de ondine que ta sauvegarde peut recevoir");
        Assert.Equal(2, sacha.FindAll(".trade-candidate").Count); // Pikachu et Salamèche

        await sacha.Find(".pokemon-picker-text").InputAsync(new() { Value = "SALA" });
        Assert.Contains("Salamèche", Assert.Single(sacha.FindAll(".trade-candidate")).TextContent);
        await sacha.Find(".pokemon-picker-text").InputAsync(new() { Value = "" });

        await sacha.Click("+ Filtre");
        await sacha.Click("Niveau");
        await sacha.Find("input[aria-label='Niveau minimal']").ChangeAsync(new() { Value = "10" });
        await sacha.Find(".trade-candidate").ClickAsync(new());
        sacha.WaitForAssertion(() => Assert.Contains("Salamèche", sacha.Find(".trade-candidate.selected").TextContent));
        await sacha.Find("input[aria-label='Niveau minimal']").ChangeAsync(new() { Value = "50" });
        sacha.WaitForText("Aucun Pokémon ne correspond.");
        await DisposeComponentsAsync();
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

    /// <summary>Un échange fait jusqu'au bout, les deux pages sur l'écran de fin.</summary>
    private async Task<(IRenderedComponent<Trades> Sacha, IRenderedComponent<Trades> Ondine)> Traded()
    {
        var (sacha, ondine) = await Requested();
        await ondine.Click("Accepter");
        sacha.WaitForText("Pokémon de ondine que ta sauvegarde peut recevoir");
        ondine.WaitForText("Pokémon de sacha que ta sauvegarde peut recevoir");
        await sacha.Find(".trade-candidate").ClickAsync(new());
        ondine.WaitForAssertion(() => Assert.DoesNotContain("n'a pas encore choisi", ondine.Text()));
        await ondine.Find(".trade-candidate").ClickAsync(new());
        sacha.WaitForText("Valider l'échange");
        await sacha.Click("Valider l'échange");
        ondine.WaitForText("sacha a validé.");
        await ondine.Click("Valider l'échange");
        sacha.WaitForAssertion(() => Assert.True(sacha.HasButton("Recommencer avec ondine")), TimeSpan.FromSeconds(10));
        ondine.WaitForAssertion(() => Assert.True(ondine.HasButton("Recommencer avec sacha")));
        return (sacha, ondine);
    }

    [Fact]
    public async Task Rematch_IsAcceptedFromTheEndScreen_SameSaves()
    {
        var (sacha, ondine) = await Traded();
        var saves = ondine.Find("p.small.text-muted").TextContent; // « Ta sauvegarde : … · Sa sauvegarde : … »
        await sacha.Click("Recommencer avec ondine");
        sacha.WaitForText("En attente de la réponse de ondine");
        Assert.Contains("Demande envoyée à ondine", sacha.Text());

        // Ondine, toujours sur son écran de fin : la proposition s'y affiche, sans passer par la liste
        ondine.WaitForAssertion(() => Assert.Contains("sacha te propose de recommencer l'échange", ondine.Find(".trade-rematch-offer").TextContent));
        Assert.False(ondine.HasButton("Recommencer avec sacha")); // accepter revient au même
        await ondine.Find(".trade-rematch-accept").ClickAsync(new());
        sacha.WaitForText("Pokémon de ondine que ta sauvegarde peut recevoir");
        ondine.WaitForText("Pokémon de sacha que ta sauvegarde peut recevoir");
        Assert.Equal(saves, ondine.Find("p.small.text-muted").TextContent); // les mêmes sauvegardes
        await DisposeComponentsAsync();
    }

    [Fact]
    public async Task Rematch_Refused_ThenNotOfferedAgain()
    {
        var (sacha, ondine) = await Traded();
        await ondine.Click("Recommencer avec sacha");
        sacha.WaitForAssertion(() => Assert.Single(sacha.FindAll(".trade-rematch-offer")));
        await sacha.Find(".trade-rematch-refuse").ClickAsync(new());
        ondine.WaitForText("sacha a refusé la demande d'échange.");
        Assert.False(ondine.HasButton("Recommencer avec sacha")); // une demande refusée ne se relance pas
        sacha.WaitForAssertion(() => Assert.Empty(sacha.FindAll(".trade-rematch-offer")));
        Assert.True(sacha.HasButton("Recommencer avec ondine")); // lui peut encore proposer
        await DisposeComponentsAsync();
    }

    [Fact]
    public async Task Rematch_AfterTheyAreNoLongerFriends_SaysSo()
    {
        var (sacha, ondine) = await Traded();
        await using (var context = Db.NewContext())
        {
            context.Friendships.RemoveRange(context.Friendships);
            await context.SaveChangesAsync();
        }
        await sacha.Click("Recommencer avec ondine");
        sacha.WaitForText("ne peuvent plus échanger ensemble");
        Assert.Empty(ondine.FindAll(".trade-rematch-offer"));
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
        cut.WaitForText("aucune paire de sauvegardes stockées qui puissent échanger");
    }
}
