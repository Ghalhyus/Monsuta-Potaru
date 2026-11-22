using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MonsutaPotaru.Components.Pages;
using MonsutaPotaru.Services;

namespace MonsutaPotaru.Tests;

/// <summary>
/// Page « Échecs » : deux amis sur la même page (même ChessHub), invitation, choix des équipes, coups joués par des clics
/// sur les cases (le plateau, echecs.js, est simulé : il ne fait que signaler les cases), fenêtre « Pièces », abandon.
/// </summary>
public sealed class ChessPageTests : PageTestContext
{
    private static int Sq(string name) => ChessGame.Square(name[1] - '1', name[0] - 'a');

    private async Task<(IRenderedComponent<Chess> Sacha, IRenderedComponent<Chess> Ondine)> Invited()
    {
        await MakeFriends("sacha", "ondine");
        var sacha = Render<Chess>();
        var ondine = RenderAs<Chess>("ondine");
        sacha.WaitForText("Choisis un ami");
        Assert.Single(sacha.FindAll(".page-header")); // le bandeau du titre, jusqu'au début de la partie
        Assert.Equal(["30 s par coup", "1 min par coup", "2 min par coup", "5 min par coup", "10 min par coup"],
            sacha.FindAll("select.chess-time option").Select(o => o.TextContent));
        Assert.True(sacha.Find("button.chess-invite").HasAttribute("disabled"));
        await sacha.Find("select.chess-friend").ChangeAsync(new() { Value = "ondine" });
        await sacha.Find("select.chess-time").ChangeAsync(new() { Value = "300" });
        await sacha.Click("Inviter");
        sacha.WaitForText("En attente de la réponse de ondine");
        sacha.WaitForText("5 min par coup");
        ondine.WaitForText("t'invite à une partie d'échecs Pokémon (5 min par coup)");
        return (sacha, ondine);
    }

    private static async Task Click(IRenderedComponent<Chess> cut, string square) =>
        await cut.InvokeAsync(() => cut.Instance.OnSquareClicked(Sq(square)));

    [Fact]
    public async Task Game_FromInvitationToForfeit()
    {
        var (sacha, ondine) = await Invited();
        await ondine.Click("Accepter");
        ondine.WaitForText("Choisis ton équipe");
        sacha.WaitForText("Choisis ton équipe");
        Assert.DoesNotContain("Invitation envoyée", sacha.Text()); // l'ami a répondu

        // Ondine prend l'équipe Feu : grisée pour Sacha
        await ondine.FindAll(".chess-team")[1].QuerySelector("button")!.ClickAsync(new());
        ondine.WaitForText("Tu as choisi l'équipe Feu");
        sacha.WaitForAssertion(() => Assert.True(sacha.Button("Prise par ondine").HasAttribute("disabled")));

        // Ondine change d'avis tant que Sacha n'a pas choisi : l'équipe Feu redevient libre, puis elle la reprend
        await ondine.Click("Changer d'équipe");
        ondine.WaitForText("Tu pourras en changer tant que sacha n'a pas choisi la sienne.");
        sacha.WaitForAssertion(() => Assert.False(sacha.HasButton("Prise par ondine")));
        await ondine.FindAll(".chess-team")[1].QuerySelector("button")!.ClickAsync(new());
        ondine.WaitForText("Tu as choisi l'équipe Feu");
        sacha.WaitForAssertion(() => Assert.True(sacha.Button("Prise par ondine").HasAttribute("disabled")));
        await sacha.FindAll(".chess-team")[0].QuerySelector("button")!.ClickAsync(new());

        sacha.WaitForText("À toi de jouer.");
        ondine.WaitForText("Au tour de sacha");
        Assert.Contains("Tu joues les blancs", sacha.Text());
        Assert.Contains("Tu joues les noirs", ondine.Text());
        Assert.NotNull(sacha.Find(".chess-board"));
        // Pendant la partie : pas de bandeau (tout tient dans la fenêtre), la partie dans la colonne à côté du plateau
        Assert.Empty(sacha.FindAll(".page-header"));
        Assert.Equal("Échecs", sacha.Find("h1.visually-hidden").TextContent);
        Assert.Contains("Partie contre ondine", sacha.Find(".chess-play .chess-side").TextContent);
        Assert.NotNull(sacha.Find(".chess-side .chess-forfeit"));

        // Ondine clique pendant le tour de Sacha : rien ne se joue, la pièce est seulement décrite
        await Click(ondine, "e7");
        ondine.WaitForText("Salamèche (Feu) · Pion.");
        await Click(ondine, "e5");
        Assert.Equal(ChessStage.Playing, Services.GetRequiredService<ChessHub>().Active("sacha")!.Stage);

        // Sacha choisit un Tarsal, puis sa case
        await Click(sacha, "e2");
        sacha.WaitForText("Tarsal (Psy) · Pion.");
        await Click(sacha, "e4");
        sacha.WaitForText("Tarsal va en e4.");
        ondine.WaitForText("À toi de jouer.");
        sacha.WaitForText("Au tour de ondine");

        // Une pièce adverse décrite, et une pièce qui ne peut pas bouger
        await Click(ondine, "d2");
        ondine.WaitForText("Tarsal (Psy) · Pion adverse.");
        await Click(ondine, "a8");
        ondine.WaitForText("Boumata ne peut pas bouger.");
        await Click(ondine, "d7");
        await Click(ondine, "d5");
        sacha.WaitForText("À toi de jouer.");

        // Prise neutre : Tarsal (Psy) mange Salamèche (Feu)
        await Click(sacha, "e4");
        await Click(sacha, "d5");
        sacha.WaitForText("Tarsal mange Salamèche en d5.");

        // Fenêtre « Pièces » : les deux équipes, un K.O. compté
        await sacha.Click("Pièces");
        var pieces = sacha.Find(".chess-pieces").TextContent;
        Assert.Contains("Toi · blancs", pieces);
        Assert.Contains("ondine · noirs", pieces);
        Assert.Contains("Gardevoir", pieces);
        Assert.Contains("Fée", pieces);
        Assert.Contains("7 sur le plateau · 1 K.O.", PageTestExtensions.Normalize(pieces));
        await sacha.Click("Fermer");
        Assert.Empty(sacha.FindAll(".chess-pieces"));

        // Fenêtre « Aide » : qui mange qui, dans les deux sens (Sacha : équipe Psy, Ondine : équipe Feu)
        await sacha.Click("Aide");
        var tables = sacha.FindAll(".chess-help-table");
        Assert.Equal(["Tes Pokémon contre les siens", "Ceux de ondine contre les tiens"],
            tables.Select(t => t.QuerySelector("caption")!.TextContent));
        foreach (var table in tables)
        {
            Assert.Equal(6, table.QuerySelectorAll("thead th.chess-help-col").Length);
            Assert.All(table.QuerySelectorAll("tbody tr"), row => Assert.Equal(6, row.QuerySelectorAll("td").Length));
        }
        // Colonnes : roi, reine, tour, fou, cavalier, pion
        string Mark(int table, string attacker, int column) =>
            tables[table].QuerySelectorAll("tbody tr").Single(r => r.QuerySelector("th")!.TextContent.Contains(attacker))
                .QuerySelectorAll("td")[column].TextContent;
        Assert.Equal("×2", Mark(0, "Gallame", 4)); // Combat contre Démolosse (Ténèbres)
        Assert.Equal("×2", Mark(0, "Gardevoir", 2)); // Fée contre Boumata (Dragon)
        Assert.Equal("✗", Mark(0, "Tarsal", 4)); // Psy contre Ténèbres : sans effet
        Assert.Equal("✗", Mark(0, "Kaorine", 0)); // Sol contre Dracaufeu (Vol) : sans effet
        Assert.Equal("✓", Mark(0, "Tarsal", 5)); // Psy contre Salamèche (Feu)
        Assert.Equal("×2", Mark(1, "Démolosse", 5)); // Ténèbres contre Tarsal (Psy)
        Assert.Equal("✗", Mark(1, "Boumata", 1)); // Dragon contre Gardevoir (Fée)
        Assert.Equal("✓", Mark(1, "Salamèche", 5));
        Assert.Contains(tables[0].QuerySelectorAll("td"), td => td.GetAttribute("title") == "Gallame mange Démolosse et rejoue : c'est super efficace !");
        Assert.Contains("il ne peut pas manger", sacha.Find(".chess-help-legend").TextContent);
        await sacha.Click("Fermer");
        Assert.Empty(sacha.FindAll(".chess-help"));

        // Abandon (confirmé) : défaite pour Sacha, victoire pour Ondine
        JSInterop.Setup<bool>("confirm", _ => true).SetResult(true);
        await sacha.Click("Abandonner");
        sacha.WaitForText("Défaite… (abandon)");
        ondine.WaitForText("Victoire ! (abandon)");
        Assert.Contains("sacha abandonne la partie.", ondine.Text());
        await sacha.Click("Nouvelle partie");
        sacha.WaitForText("Choisis un ami");
    }

    [Fact]
    public async Task BlockedCapture_IsExplained_AndBonusTurnCanBePassed()
    {
        var (sacha, ondine) = await Invited();
        await ondine.Click("Accepter");
        ondine.WaitForText("Choisis ton équipe");
        await ondine.FindAll(".chess-team")[1].QuerySelector("button")!.ClickAsync(new()); // Feu
        sacha.WaitForText("Choisis ton équipe");
        await sacha.FindAll(".chess-team")[0].QuerySelector("button")!.ClickAsync(new()); // Psy
        sacha.WaitForText("À toi de jouer.");

        // 1. e4 Cf6 2. a3 Cxe4 (Ténèbres contre Psy : super efficace, Démolosse rejoue)
        await Click(sacha, "e2"); await Click(sacha, "e4");
        ondine.WaitForText("À toi de jouer.");
        await Click(ondine, "g8"); await Click(ondine, "f6");
        sacha.WaitForText("À toi de jouer.");
        await Click(sacha, "a2"); await Click(sacha, "a3");
        ondine.WaitForText("À toi de jouer.");
        await Click(ondine, "f6"); await Click(ondine, "e4");
        ondine.WaitForText("Super efficace ! Démolosse rejoue");
        Assert.True(ondine.HasButton("Passer"));
        sacha.WaitForText("Au tour de ondine");

        // Démolosse ne peut pas manger Gallame (Combat) : expliqué, rien n'est joué
        await Click(ondine, "f2"); // Tarsal : super efficace encore
        ondine.WaitForText("Démolosse mange Tarsal en f2.");
        ondine.WaitForText("Super efficace ! Démolosse rejoue");
        await Click(ondine, "d1");
        ondine.WaitForText("Pas très efficace : Démolosse (Ténèbres) ne peut pas manger Gardevoir (Fée).");
        await ondine.Click("Passer");
        ondine.WaitForText("Démolosse ne rejoue pas.");
        sacha.WaitForText("À toi de jouer.");
    }
}
