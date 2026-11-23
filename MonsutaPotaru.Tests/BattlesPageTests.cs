using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MonsutaPotaru.Components.Pages;
using MonsutaPotaru.Services;

namespace MonsutaPotaru.Tests;

/// <summary>
/// Page « Combats » : deux amis sur la même page (même BattleHub, vrai simulateur), demande, équipes, menus du combat,
/// disposition des écrans, abandon. Les étapes du combat se jouent au rythme de BattleTiming : les attentes sont longues.
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
    /// bout), puis valide l'équipe.
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
        // Choisis dans leurs préférences : empilés pour Sacha, côte à côte et agrandis pour Ondine
        SetSorts("sacha", new SortPreferences { Battle = BattleLayout.Stacked });
        SetSorts("ondine", new SortPreferences { Battle = BattleLayout.SideBySide, BattleEnlarged = true });
        var (sacha, ondine) = await Requested();
        await ondine.Click("Accepter");
        await ChooseTeam(sacha, lead: 4); // Chenipan niveau 4 (vert-feuille.sav, Sécrétion), face au Férosinge niveau 13 d'Ondine (Groz'Yeux)
        sacha.WaitForText("Ton équipe est prête");
        // Choix des équipes : pas de bouton Agrandir, et la ligne des sauvegardes reste, même pour qui a choisi les écrans agrandis
        Assert.False(sacha.HasButton("Agrandir"));
        Assert.False(ondine.HasButton("Réduire"));
        Assert.Empty(ondine.FindAll(".battle-block.agrandi, .battle-block.en-cours"));
        await ChooseTeam(ondine);

        // Premier tour : les menus de Sacha, puis une attaque sans dégâts de chaque côté. Un K.O. forcerait un
        // remplacement (pas de menu, donc pas d'ABANDONNER) : le Balayage de Férosinge met K.O. Chenipan sur un coup critique.
        sacha.WaitForAssertion(() => Assert.True(sacha.HasButton("ATTAQUE")), Long);
        // Menu principal : pas de bouton SAC (pas de sac en combat de câble), ABANDONNER avant POKéMON (à sa gauche) ;
        // une Pokéball par Pokémon de chaque équipe, six places par rangée
        Assert.False(sacha.HasButton("SAC"));
        Assert.Equal(["ATTAQUE", "ABANDONNER", "POKéMON"], sacha.FindAll(".battle-menu button").Select(b => b.TextContent.Trim()));
        Assert.NotNull(sacha.Find(".battle-fight img")); // l'icône du Pokémon au combat
        var size = sacha.FindAll(".battle-balls.mine .battle-ball.ok").Count;
        Assert.InRange(size, 1, 6);
        Assert.Equal(6, sacha.FindAll(".battle-balls.mine .battle-ball").Count);
        Assert.Equal(6, sacha.FindAll(".battle-balls.foe .battle-ball").Count);
        Assert.NotEmpty(sacha.FindAll(".battle-balls.foe .battle-ball.ok"));
        Assert.Empty(sacha.FindAll(".battle-ball.ko"));
        Assert.Matches(@"^\d+/\d+$", sacha.Find(".battle-info.mine .battle-hp-text").TextContent);
        Assert.Empty(sacha.FindAll(".battle-info.foe .battle-hp-text")); // PV de l'adversaire : la barre seulement

        // Capacités : quatre emplacements (les vides sans bouton), puis ANNULER
        await sacha.Click("ATTAQUE");
        Assert.NotEmpty(sacha.FindAll("button.battle-move"));
        Assert.Equal(4, sacha.FindAll(".battle-move").Count);
        Assert.Empty(sacha.FindAll(".battle-fight"));
        await sacha.Click("ANNULER");

        // Équipe : un panneau par Pokémon (niveau, PV), qui ouvre sa fiche (celui qui est au combat compris) ; la barre
        // des Pokéballs laisse la place aux panneaux, le retour ramène au menu principal
        await sacha.Click("POKéMON");
        Assert.Equal(size, sacha.FindAll(".battle-member").Count);
        var lead = sacha.Find(".battle-member.active");
        Assert.False(lead.HasAttribute("disabled"));
        Assert.Contains("N.4", lead.QuerySelector(".battle-member-foot")!.TextContent);
        Assert.Contains("Choisis un Pokémon.", sacha.Find(".battle-team-text").TextContent);
        Assert.NotEmpty(sacha.FindAll(".battle-bottom.equipe")); // la feuille de style y cache la barre, sauf en mode hybride

        // Fiche du Pokémon au combat (Chenipan) : écran de sélection, panneau grisé (il y est déjà), minuteur affiché
        await lead.ClickAsync(new());
        var shift = sacha.Find(".battle-sheet.selection .battle-shift");
        Assert.True(shift.HasAttribute("disabled"));
        Assert.Contains("AU COMBAT", shift.TextContent);
        Assert.Matches(@"^\d+ s$", sacha.Find(".battle-sheet.selection > .battle-timer").TextContent);
        Assert.Equal(["RÉSUMÉ", "CAPACITÉS", "↵"], sacha.FindAll(".battle-sheet-foot button").Select(b => b.TextContent.Trim()));
        Assert.NotEmpty(sacha.FindAll(".battle-bottom.equipe"));

        // Résumé : types, niveau, talent et sa description, objet, PV et les cinq statistiques
        await sacha.Click("RÉSUMÉ");
        Assert.Equal("Chenipan", sacha.Find(".battle-sheet.resume .battle-sheet-name").TextContent);
        Assert.Equal(["Insecte"], sacha.FindAll(".battle-sheet-types .type-bug").Select(t => t.TextContent));
        Assert.Equal("N.4", sacha.Find(".battle-sheet-label.niveau").TextContent);
        Assert.Contains("Écran Poudre", sacha.Find(".battle-sheet-left").TextContent);
        Assert.False(string.IsNullOrWhiteSpace(sacha.Find(".battle-sheet-left .battle-sheet-text").TextContent));
        Assert.Contains("Aucun objet tenu", sacha.Find(".battle-sheet-item").TextContent);
        Assert.Empty(sacha.FindAll(".battle-sheet-item img"));
        Assert.Equal(["PV", "ATTAQUE", "DÉFENSE", "ATQ. SPÉ.", "DÉF. SPÉ.", "VITESSE"],
            sacha.FindAll(".battle-sheet-stats .battle-stat > :first-child").Select(s => s.TextContent));
        Assert.All(sacha.FindAll(".battle-sheet-stats .battle-stat > :last-child"), value => Assert.Matches(@"^\d+(/\d+)?$", value.TextContent));

        // Flèches : Pokémon suivant puis précédent de l'équipe, sans quitter le résumé
        await sacha.Find("button[aria-label='Pokémon suivant']").ClickAsync(new());
        Assert.NotEqual("Chenipan", sacha.Find(".battle-sheet.resume .battle-sheet-name").TextContent);
        await sacha.Find("button[aria-label='Pokémon précédent']").ClickAsync(new());
        Assert.Equal("Chenipan", sacha.Find(".battle-sheet-name").TextContent);

        // Capacités : quatre emplacements (Charge et Sécrétion, deux vides), PP de chacune ; un clic ouvre le détail
        await sacha.Click("CAPACITÉS");
        Assert.Equal(4, sacha.FindAll(".battle-sheet.capacites .battle-sheet-move").Count);
        Assert.Equal(["Charge", "Sécrétion"], sacha.FindAll("button.battle-sheet-move .battle-sheet-move-name").Select(m => m.TextContent));
        Assert.Contains("PP 35/35", sacha.Find("button.battle-sheet-move").TextContent);
        await sacha.Find("button.battle-sheet-move").ClickAsync(new());
        Assert.Equal(["Physique", "35", "95"], sacha.FindAll(".battle-sheet.detail .battle-sheet-facts .battle-stat > :last-child").Select(s => s.TextContent));
        Assert.False(string.IsNullOrWhiteSpace(sacha.Find(".battle-sheet-detail .battle-sheet-text").TextContent));
        await sacha.Find("button[aria-label='Capacité suivante']").ClickAsync(new());
        Assert.Equal("Sécrétion", sacha.Find(".battle-sheet-detail .battle-sheet-move-name").TextContent);
        Assert.Equal(["Statut", "—", "95"], sacha.FindAll(".battle-sheet-facts .battle-stat > :last-child").Select(s => s.TextContent));
        await sacha.Find("button[aria-label='Capacité précédente']").ClickAsync(new());
        Assert.Equal("Charge", sacha.Find(".battle-sheet-detail .battle-sheet-move-name").TextContent);

        // Retours : détail → capacités → (résumé) → écran de sélection → équipe → menu principal
        await sacha.Find(".battle-sheet-btn.retour").ClickAsync(new());
        Assert.NotEmpty(sacha.FindAll(".battle-sheet.capacites"));
        await sacha.Click("RÉSUMÉ");
        Assert.NotEmpty(sacha.FindAll(".battle-sheet.resume"));
        await sacha.Find(".battle-sheet-btn.retour").ClickAsync(new());
        Assert.NotEmpty(sacha.FindAll(".battle-sheet.selection"));
        await sacha.Find(".battle-sheet-btn.retour").ClickAsync(new());
        Assert.NotEmpty(sacha.FindAll(".battle-team-menu"));
        await sacha.Find(".battle-back.retour").ClickAsync(new());
        Assert.Empty(sacha.FindAll(".battle-bottom.equipe"));

        // Disposition : empilés pour Sacha, côte à côte pour Ondine (leurs préférences) ; le bouton l'applique
        // tout de suite et la retient dans le compte
        var preferences = Services.GetRequiredService<PreferencesService>();
        Assert.Empty(sacha.FindAll(".battle.cote"));
        Assert.NotEmpty(ondine.FindAll(".battle.cote"));
        Assert.Equal("true", sacha.Button("Empilés").GetAttribute("aria-pressed"));
        await sacha.Click("Côte à côte");
        Assert.NotEmpty(sacha.FindAll(".battle.cote"));
        Assert.Equal("true", sacha.Button("Côte à côte").GetAttribute("aria-pressed"));
        Assert.Equal(BattleLayout.SideBySide, (await preferences.SortsAsync("sacha")).Battle);
        await sacha.Click("Côte à côte"); // déjà choisie : rien

        // Mode hybride : le menu passe dans un panneau à droite du terrain (ATTAQUE, POKéMON, ABANDONNER en colonne) ; la
        // ligne des sauvegardes et les boutons de disposition restent au-dessus du combat
        await sacha.Click("Hybride");
        Assert.NotEmpty(sacha.FindAll(".battle.hybride"));
        Assert.Empty(sacha.FindAll(".battle.cote"));
        Assert.Equal(BattleLayout.Hybrid, (await preferences.SortsAsync("sacha")).Battle);
        Assert.Equal(["ATTAQUE", "POKéMON", "ABANDONNER"], sacha.FindAll(".battle-menu button").Select(b => b.TextContent.Trim()));
        Assert.Contains("Ta sauvegarde", sacha.Find(".block-body > p").TextContent);
        Assert.Equal("Hybride", sacha.Find(".battle-zone > .battle-layout:first-child button.active").TextContent);

        await sacha.Click("Empilés");
        Assert.Empty(sacha.FindAll(".battle.cote, .battle.hybride"));
        Assert.Equal(BattleLayout.Stacked, (await preferences.SortsAsync("sacha")).Battle);

        // Agrandir, juste à côté du titre du bloc (.en-cours met la ligne du titre en rangée) : classe .agrandi (la feuille de style cache la ligne des sauvegardes et les
        // boutons de disposition, et lève la taille maximale), retenu dans le compte sans toucher à la disposition ;
        // Réduire revient à la taille normale. Ondine, qui l'a choisi dans ses préférences, est déjà agrandie.
        Assert.NotEmpty(ondine.FindAll(".battle-block.agrandi .battle.cote"));
        Assert.Empty(sacha.FindAll(".battle-block.agrandi"));
        var enlarge = sacha.Find(".battle-block.en-cours > .block-header > .block-actions .battle-agrandir");
        Assert.Equal("Agrandir", enlarge.TextContent);
        Assert.Equal("false", enlarge.GetAttribute("aria-pressed"));
        await sacha.Click("Agrandir");
        Assert.NotEmpty(sacha.FindAll(".battle-block.agrandi"));
        Assert.NotNull(sacha.Find(".battle-block.agrandi .battle-saves"));
        Assert.Equal("true", sacha.Button("Réduire").GetAttribute("aria-pressed"));
        var enlarged = await preferences.SortsAsync("sacha");
        Assert.True(enlarged.BattleEnlarged);
        Assert.Equal(BattleLayout.Stacked, enlarged.Battle);
        await sacha.Click("Réduire");
        Assert.Empty(sacha.FindAll(".battle-block.agrandi"));
        Assert.False((await preferences.SortsAsync("sacha")).BattleEnlarged);

        // Sacha change de Pokémon par la fiche du deuxième de son équipe (panneau CHANGER) ; Ondine attaque sans dégâts
        await sacha.Click("POKéMON");
        await sacha.FindAll(".battle-member")[1].ClickAsync(new());
        var next = sacha.Find(".battle-shift .battle-shift-name > span").TextContent;
        Assert.Contains("CHANGER", sacha.Find(".battle-shift").TextContent);
        Assert.False(sacha.Find(".battle-shift").HasAttribute("disabled"));
        await sacha.Find(".battle-shift").ClickAsync(new());
        sacha.WaitForText("En attente de ondine");
        ondine.WaitForAssertion(() => Assert.True(ondine.HasButton("ATTAQUE")), Long);
        await ondine.Click("ATTAQUE");
        await ondine.Click("Groz");
        sacha.WaitForAssertion(() => Assert.Equal(next, sacha.Find(".battle-info.mine .battle-info-name > span").TextContent), Long);

        // Abandon de Sacha (confirmé), fin du combat des deux côtés
        sacha.WaitForAssertion(() => Assert.True(sacha.HasButton("ABANDONNER")), Long);
        await sacha.Click("ABANDONNER"); // « confirm » répond non : rien
        JSInterop.Setup<bool>("confirm", _ => true).SetResult(true);
        await sacha.Click("ABANDONNER");
        sacha.WaitForAssertion(() => Assert.True(sacha.HasButton("Nouveau combat")), Long);
        ondine.WaitForAssertion(() => Assert.Contains("Victoire !", ondine.Text()), Long);
        await sacha.Click("Nouveau combat");
        sacha.WaitForText("Nouveau combat");
    }

    [Fact]
    public async Task TeamBuilding_SearchAndChips_KeepTheTeam()
    {
        var (sacha, ondine) = await Requested();
        await ondine.Click("Accepter");
        sacha.WaitForText("Valider l'équipe");
        var all = sacha.FindAll(".trade-candidate").Count;
        Assert.True(all > 1);

        // Pas de filtre « Œuf » : l'équipe n'en a jamais.
        await sacha.Click("+ Filtre");
        Assert.DoesNotContain(sacha.FindAll(".filter-popover .dropdown-item"), b => b.TextContent == "Œuf");
        await sacha.Click("+ Filtre");

        // Le premier Pokémon choisi, puis une recherche qui le cache : il reste dans l'équipe.
        var first = sacha.Find(".trade-candidate .trade-candidate-name").TextContent.Replace(" ✨", "");
        await sacha.Find(".trade-candidate").ClickAsync(new());
        await sacha.Find(".pokemon-picker-text").InputAsync(new() { Value = "aucun pokémon de ce nom" });
        sacha.WaitForText("Aucun Pokémon ne correspond.");
        Assert.Empty(sacha.FindAll(".trade-candidate"));
        Assert.Contains(first, sacha.Find(".battle-team-slot:not(.empty)").TextContent);
        await sacha.Find(".pokemon-picker-text").InputAsync(new() { Value = "" });
        Assert.Equal(all, sacha.FindAll(".trade-candidate").Count);
        await DisposeComponentsAsync();
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
    public async Task Rematch_AfterACancelledBattle_TeamsChosenAgain()
    {
        var (sacha, ondine) = await Requested();
        await ondine.Click("Accepter");
        await ChooseTeam(sacha);
        sacha.WaitForText("Ton équipe est prête");
        var saves = ondine.Find(".battle-saves").TextContent;
        await sacha.Click("Annuler le combat"); // annulé pendant le choix des équipes : on peut recommencer
        ondine.WaitForText("sacha a annulé le combat.");

        await ondine.Click("Recommencer avec sacha");
        ondine.WaitForText("En attente de la réponse de sacha");
        sacha.WaitForAssertion(() => Assert.Contains("ondine te propose de recommencer.", sacha.Find(".battle-rematch-offer").TextContent));
        Assert.False(sacha.HasButton("Recommencer avec ondine")); // accepter revient au même
        await sacha.Find(".battle-rematch-accept").ClickAsync(new());

        // Mêmes sauvegardes ; l'équipe de Sacha est à rechoisir
        sacha.WaitForText("Valider l'équipe");
        ondine.WaitForText("Valider l'équipe");
        Assert.Equal(saves, ondine.Find(".battle-saves").TextContent);
        Assert.Empty(sacha.FindAll(".battle-team-slot:not(.empty)"));
        await DisposeComponentsAsync();
    }

    [Fact]
    public async Task Rematch_Refused_ThenNotOfferedAgain()
    {
        var (sacha, ondine) = await Requested();
        await ondine.Click("Accepter");
        sacha.WaitForText("Valider l'équipe");
        await ondine.Click("Annuler le combat");
        sacha.WaitForText("ondine a annulé le combat.");
        await sacha.Click("Recommencer avec ondine");
        ondine.WaitForAssertion(() => Assert.Single(ondine.FindAll(".battle-rematch-offer")));
        await ondine.Find(".battle-rematch-refuse").ClickAsync(new());
        sacha.WaitForText("ondine a refusé le combat.");
        Assert.False(sacha.HasButton("Recommencer avec ondine"));
        ondine.WaitForAssertion(() => Assert.True(ondine.HasButton("Recommencer avec sacha")));
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
