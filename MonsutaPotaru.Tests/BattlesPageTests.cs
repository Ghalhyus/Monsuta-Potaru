using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MonsutaPotaru.Components.Pages;

namespace MonsutaPotaru.Tests;

/// <summary>
/// Page « Combats » : deux amis sur la même page (même BattleHub, vrai simulateur), demande, équipes, menus du combat,
/// abandon. Les étapes du combat se jouent au rythme de BattleTiming : les attentes sont longues.
/// </summary>
public sealed class BattlesPageTests : PageTestContext
{
    private static readonly TimeSpan Long = TimeSpan.FromSeconds(60);

    private async Task<(IRenderedComponent<Battles> Sacha, IRenderedComponent<Battles> Ondine)> Requested()
    {
        await MakeFriends("sacha", "ondine");
        await StoreReal("sacha", "vert-feuille.sav");
        await StoreReal("ondine", "rouge-feu.sav");
        var sacha = Render<Battles>();
        var ondine = RenderAs<Battles>("ondine");
        sacha.WaitForText("Choisis un ami");
        await sacha.Find("select.battle-friend").ChangeAsync(new() { Value = "ondine" });
        sacha.WaitForText("Proposer le combat");
        await sacha.Click("Proposer le combat");
        sacha.WaitForText("En attente de la réponse de ondine");
        ondine.WaitForText("te propose un combat");
        return (sacha, ondine);
    }

    /// <summary>
    /// Choisit les premiers Pokémon proposés, avec <paramref name="lead"/> en tête (le premier est retiré puis remis au
    /// bout), puis valide l'équipe. Une tête faible évite un K.O. au premier tour (remplacement forcé, pas de menu).
    /// </summary>
    private static async Task ChooseTeam(IRenderedComponent<Battles> cut, int lead = 1)
    {
        cut.WaitForText("Valider l'équipe");
        // La consigne reste affichée, le détail des règles est dans l'infobulle.
        var hint = cut.Find(".battle-team-hint");
        Assert.Contains("le premier entre en combat", hint.QuerySelector(":scope > span")!.TextContent);
        Assert.Contains("sans objet du sac", hint.QuerySelector("details.help-tip .help-tip-bubble")!.TextContent);
        var size = cut.FindAll(".battle-team-slot").Count;
        var order = new[] { 0, lead }.Concat(Enumerable.Range(1, cut.FindAll(".trade-candidate").Count).Where(i => i != lead)).Take(size);
        foreach (var i in order)
            await cut.FindAll(".trade-candidate")[i].ClickAsync(new());
        await cut.Find(".battle-team-slot .btn-close").ClickAsync(new());
        await cut.FindAll(".trade-candidate")[0].ClickAsync(new());
        await cut.Click("Valider l'équipe");
    }

    [Fact]
    public async Task Battle_TeamsThenMenus_ThenForfeit()
    {
        var (sacha, ondine) = await Requested();
        await ondine.Click("Accepter");
        await ChooseTeam(sacha, lead: 4); // Chenipan niveau 4 (vert-feuille.sav), face au Férosinge niveau 13 d'Ondine
        sacha.WaitForText("Ton équipe est prête");
        await ChooseTeam(ondine);

        // Premier tour : les menus de Sacha, puis une attaque de chaque côté
        sacha.WaitForAssertion(() => Assert.True(sacha.HasButton("ATTAQUE")), Long);
        await sacha.Click("ATTAQUE");
        Assert.NotEmpty(sacha.FindAll(".battle-move"));
        await sacha.Click("RETOUR");
        await sacha.Click("POKéMON");
        Assert.NotEmpty(sacha.FindAll(".battle-member"));
        await sacha.Click("RETOUR");
        await sacha.Click("ATTAQUE");
        await sacha.Find(".battle-move:not([disabled])").ClickAsync(new());
        sacha.WaitForText("En attente de ondine");
        ondine.WaitForAssertion(() => Assert.True(ondine.HasButton("ATTAQUE")), Long);
        await ondine.Click("ATTAQUE");
        await ondine.Find(".battle-move:not([disabled])").ClickAsync(new());

        // Abandon de Sacha (confirmé), fin du combat des deux côtés
        sacha.WaitForAssertion(() => Assert.True(sacha.HasButton("FUITE")), Long);
        await sacha.Click("FUITE"); // « confirm » répond non : rien
        JSInterop.Setup<bool>("confirm", _ => true).SetResult(true);
        await sacha.Click("FUITE");
        sacha.WaitForAssertion(() => Assert.True(sacha.HasButton("Nouveau combat")), Long);
        ondine.WaitForAssertion(() => Assert.Contains("Victoire !", ondine.Text()), Long);
        await sacha.Click("Nouveau combat");
        sacha.WaitForText("Nouveau combat");
    }

    [Fact]
    public async Task Request_Refused_ThenCancelledFromTeamBuilding()
    {
        var (sacha, ondine) = await Requested();
        await ondine.Click("Refuser");
        sacha.WaitForText("Retour à l'accueil");
        await sacha.Click("Nouveau combat");
        await sacha.Find("select.battle-friend").ChangeAsync(new() { Value = "ondine" });
        await sacha.Click("Proposer le combat");
        ondine.WaitForText("te propose un combat");
        await ondine.Click("Accepter");
        sacha.WaitForText("Valider l'équipe");

        // Quitter la page : « confirm » répond non, puis oui (le combat est annulé pour les deux)
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("amis");
        Assert.Contains("Valider l'équipe", sacha.Text());
        JSInterop.Setup<bool>("confirm", _ => true).SetResult(true);
        navigation.NavigateTo("amis");
        ondine.WaitForText("sacha a quitté le combat.");
        await ondine.Click("Nouveau combat");

        await sacha.Click("Nouveau combat");
        await sacha.Find("select.battle-friend").ChangeAsync(new() { Value = "ondine" });
        await sacha.Click("Proposer le combat");
        await sacha.Click("Annuler le combat");
        ondine.WaitForAssertion(() => Assert.DoesNotContain("te propose un combat", ondine.Text()));
        await DisposeComponentsAsync();
    }

    [Fact]
    public async Task WithoutFriend_OrMatchingSave_SaysSo()
    {
        var alone = Render<Battles>();
        alone.WaitForText("Il te faut un ami pour combattre");
        await MakeFriends("sacha", "pierre");
        var cut = Render<Battles>();
        await cut.Find("select.battle-friend").ChangeAsync(new() { Value = "pierre" });
        cut.WaitForText("aucune sauvegarde stockée de la même génération");
        await cut.Find("select.battle-friend").ChangeAsync(new() { Value = "" });
    }
}
