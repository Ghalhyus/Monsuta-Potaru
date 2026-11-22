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
    public async Task PoisonTeam_ShowsBothNidoranOnThePawnLine()
    {
        var (sacha, ondine) = await Invited();
        await ondine.Click("Accepter");
        ondine.WaitForText("Choisis ton équipe");
        Assert.Equal(["Équipe Psy", "Équipe Feu", "Équipe Électrik", "Équipe Poison", "Équipe Eau", "Équipe Plante", "Équipe Sol"],
            ondine.FindAll(".chess-team h3").Select(h => h.TextContent));
        // Une ligne « Pion », deux images
        var pawn = ondine.FindAll(".chess-team")[3].QuerySelectorAll(".chess-team-list li").Last();
        Assert.Equal(2, pawn.QuerySelectorAll(".chess-mon-images img").Length);
        Assert.Equal("Nidoran♂ / Nidoran♀", pawn.QuerySelector(".chess-mon-name")!.TextContent);
        await ondine.FindAll(".chess-team")[3].QuerySelector("button")!.ClickAsync(new()); // Poison
        sacha.WaitForText("Choisis ton équipe");
        await sacha.FindAll(".chess-team")[0].QuerySelector("button")!.ClickAsync(new()); // Psy
        sacha.WaitForText("À toi de jouer.");

        // Aide : toujours six colonnes, la colonne des pions avec les deux Nidoran (Psy contre Poison : ×2)
        await sacha.Click("Aide");
        var columns = sacha.FindAll(".chess-help-table")[0].QuerySelectorAll("thead th.chess-help-col");
        Assert.Equal(6, columns.Length);
        Assert.Equal(2, columns[5].QuerySelectorAll("img").Length);
        Assert.Contains("Nidoran♂ / Nidoran♀", columns[5].TextContent);
        Assert.Contains(sacha.FindAll(".chess-help-table")[0].QuerySelectorAll("td"),
            td => td.GetAttribute("title") == "Tarsal mange Nidoran♂ ou Nidoran♀ et rejoue : c'est super efficace !");
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
        // Sur le plateau, la prise de Tarsal en f2 n'a pas les coins dorés (« il rejouera ») : un seul tour bonus
        ondine.WaitForAssertion(() => Assert.Equal(false, LastMarkedMove(Sq("f2"))?.GetType().GetProperty("super")!.GetValue(LastMarkedMove(Sq("f2")))));

        // Pendant son tour bonus, Démolosse mange encore un Tarsal (super efficace) : il ne rejoue pas une deuxième fois
        await Click(ondine, "f2");
        ondine.WaitForText("Démolosse mange Tarsal en f2. C'est super efficace, mais Démolosse a déjà rejoué.");
        sacha.WaitForText("À toi de jouer.");
        Assert.False(ondine.HasButton("Passer"));

        // 4. a4 Cd3 5. h3 Cxb2 : un nouveau tour, un nouveau tour bonus. Démolosse ne peut pas manger Gardevoir (Fée) en
        // d1 : expliqué, rien n'est joué ; il passe
        await Click(sacha, "a3"); await Click(sacha, "a4");
        ondine.WaitForText("À toi de jouer.");
        await Click(ondine, "f2"); await Click(ondine, "d3");
        sacha.WaitForText("À toi de jouer.");
        await Click(sacha, "h2"); await Click(sacha, "h3");
        ondine.WaitForText("À toi de jouer.");
        await Click(ondine, "d3"); await Click(ondine, "b2");
        ondine.WaitForText("Super efficace ! Démolosse rejoue");
        await Click(ondine, "d1");
        ondine.WaitForText("Pas très efficace : Démolosse (Ténèbres) ne peut pas manger Gardevoir (Fée).");
        await ondine.Click("Passer");
        ondine.WaitForText("Démolosse ne rejoue pas.");
        sacha.WaitForText("À toi de jouer.");
    }

    /// <summary>Case permise marquée sur le plateau vers <paramref name="square"/>, dans le dernier « highlight » qui en a.</summary>
    private object? LastMarkedMove(int square) => JSInterop.Invocations.Where(i => i.Identifier == "highlight")
        .Select(i => i.Arguments[0]!.GetType().GetProperty("moves")!.GetValue(i.Arguments[0]) as System.Collections.IEnumerable)
        .Select(moves => moves!.Cast<object>().FirstOrDefault(m => (int)m.GetType().GetProperty("to")!.GetValue(m)! == square))
        .LastOrDefault(m => m is not null);

    /// <summary>Valeur « idle » passée par la page à echecs.js à la création de chaque plateau.</summary>
    private bool?[] BoardsIdle() => [.. JSInterop.Invocations.Where(i => i.Identifier == "create")
        .Select(i => i.Arguments[2]?.GetType().GetProperty("idle")?.GetValue(i.Arguments[2]) as bool?)];

    [Fact]
    public async Task IdleAnimations_FollowTheAccount_AndAreSavedFromTheGame()
    {
        var preferences = Services.GetRequiredService<PreferencesService>();
        Assert.True((await preferences.SetChessIdleAsync("sacha", false)).Ok); // Sacha les a coupées dans « Préférences »
        var (sacha, ondine) = await Invited();
        await ondine.Click("Accepter");
        ondine.WaitForText("Choisis ton équipe");
        await ondine.FindAll(".chess-team")[1].QuerySelector("button")!.ClickAsync(new());
        sacha.WaitForText("Choisis ton équipe");
        await sacha.FindAll(".chess-team")[0].QuerySelector("button")!.ClickAsync(new());
        sacha.WaitForText("À toi de jouer.");
        ondine.WaitForText("Au tour de sacha");

        // Chaque plateau naît avec le réglage de son joueur
        Assert.False(sacha.Find("#chess-idle").HasAttribute("checked"));
        Assert.True(ondine.Find("#chess-idle").HasAttribute("checked"));
        sacha.WaitForAssertion(() => Assert.Equal(2, BoardsIdle().Length));
        Assert.Equal([false, true], BoardsIdle().Order());

        // Sacha les remet pendant la partie : le plateau suit, le compte le garde
        await sacha.Find("#chess-idle").ChangeAsync(new() { Value = true });
        Assert.Equal(true, JSInterop.Invocations.Last(i => i.Identifier == "setIdle").Arguments[0]);
        Assert.True((await preferences.SortsAsync("sacha")).ChessIdle);
        await ondine.Find("#chess-idle").ChangeAsync(new() { Value = false });
        Assert.Equal(false, JSInterop.Invocations.Last(i => i.Identifier == "setIdle").Arguments[0]);
        Assert.False((await preferences.SortsAsync("ondine")).ChessIdle);
        Assert.True((await preferences.SortsAsync("sacha")).ChessIdle);
    }

    /// <summary>Sacha crée une partie publique (sans ami) ; rend son code.</summary>
    private static async Task<string> CreatePublic(IRenderedComponent<Chess> sacha)
    {
        sacha.WaitForText("Nouvelle partie");
        await sacha.Click("Créer une partie publique");
        sacha.WaitForText("Donne ce code à ton adversaire");
        var code = ChessHub.NormalizeCode(sacha.Find(".chess-code").TextContent);
        Assert.Equal(ChessHub.CodeLength, code.Length);
        return code;
    }

    /// <summary>Un autre code que celui-ci (même longueur, mêmes caractères permis).</summary>
    private static string OtherCode(string code) => (code[0] == 'A' ? "B" : "A") + code[1..];

    [Fact]
    public async Task PublicGame_JoinedWithoutAccount()
    {
        var sacha = Render<Chess>();
        sacha.WaitForText("Pour inviter un ami, ajoute-en dans Amis."); // pas d'ami : la partie publique reste possible
        await sacha.Find("select.chess-time").ChangeAsync(new() { Value = "60" });
        var code = await CreatePublic(sacha);
        sacha.WaitForText("Partie publique");
        sacha.WaitForText("1 min par coup");

        // Le lien, le code déjà rempli, copié dans le presse-papiers
        JSInterop.SetupVoid("navigator.clipboard.writeText", _ => true).SetVoidResult();
        await sacha.Click("Copier le lien");
        sacha.WaitForText("Lien copié : envoie-le à ton adversaire.");
        Assert.Equal($"http://localhost/mini-jeux/echecs?code={code}",
            JSInterop.Invocations.Last(i => i.Identifier == "navigator.clipboard.writeText").Arguments[0]);

        // Sans compte : seulement « Rejoindre une partie », avec un prénom
        var guest = RenderAnonymous<Chess>();
        guest.WaitForText("Rejoindre une partie");
        Assert.DoesNotContain("Nouvelle partie", guest.Text());
        Assert.Empty(guest.FindAll("button.chess-public"));
        Assert.Contains("Connecte-toi", guest.Text());
        await guest.Find("input.chess-code-input").ChangeAsync(new() { Value = code.ToLowerInvariant() });
        await guest.Click("Rejoindre");
        guest.WaitForText("Ton prénom doit faire de 2 à 20 caractères.");
        await guest.Find("input.chess-guest-name").ChangeAsync(new() { Value = "Léo" });
        await guest.Find("input.chess-code-input").ChangeAsync(new() { Value = OtherCode(code) });
        await guest.Click("Rejoindre");
        guest.WaitForText("Aucune partie n'attend avec ce code");
        await guest.Find("input.chess-code-input").ChangeAsync(new() { Value = $"{code[..4]}-{code[4..]}".ToLowerInvariant() });
        await guest.Click("Rejoindre");

        // Choix des équipes, puis la partie : l'invité joue les noirs
        guest.WaitForText("Partie contre sacha");
        sacha.WaitForText("Partie contre Léo (invité)");
        await guest.FindAll(".chess-team")[1].QuerySelector("button")!.ClickAsync(new());
        await sacha.FindAll(".chess-team")[0].QuerySelector("button")!.ClickAsync(new());
        guest.WaitForText("Tu joues les noirs · 1 min par coup");
        sacha.WaitForText("Partie contre Léo (invité)");

        // L'invité a la case des animations au repos, pour cette partie seulement (pas de compte où la garder)
        Assert.True(guest.Find("#chess-idle").HasAttribute("checked"));
        await guest.Find("#chess-idle").ChangeAsync(new() { Value = false });
        Assert.Equal(false, JSInterop.Invocations.Last(i => i.Identifier == "setIdle").Arguments[0]);
        Assert.False(guest.Find("#chess-idle").HasAttribute("checked"));
        Assert.DoesNotContain("gardé dans ton compte", guest.Text());
        Assert.True((await Services.GetRequiredService<PreferencesService>().SortsAsync("sacha")).ChessIdle);

        // L'invité abandonne : victoire pour Sacha
        JSInterop.Setup<bool>("confirm", _ => true).SetResult(true);
        await guest.Click("Abandonner");
        sacha.WaitForText("Victoire ! (abandon)");
        sacha.WaitForText("Léo (invité) abandonne la partie.");
    }

    [Fact]
    public async Task PublicGame_JoinedByLink_AndCodeTriesAreLimited()
    {
        // Partie créée sans la page de Sacha : sa page bloquerait la navigation vers le lien (quitter annule la partie)
        var session = Services.GetRequiredService<ChessHub>().CreatePublic(new ChessParty("sacha", "sacha"), 120).Session!;
        var code = session.Code!;

        // Ondine, connectée, ouvre le lien : le code est rempli, sans prénom à donner
        Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>()
            .NavigateTo($"mini-jeux/echecs?code={code.ToLowerInvariant()}");
        var ondine = RenderAs<Chess>("ondine");
        ondine.WaitForText("Rejoindre une partie");
        Assert.Equal(ChessHub.FormatCode(code), ondine.Find("input.chess-code-input").GetAttribute("value"));
        Assert.Empty(ondine.FindAll("input.chess-guest-name"));
        await ondine.Click("Rejoindre");
        ondine.WaitForText("Choisis ton équipe");
        Assert.Equal((ChessStage.TeamChoice, "ondine"), (session.Stage, session.Guest.Pseudo));

        // Essais de codes limités par adresse IP (Ondine en a déjà fait un) : au-delà, le code n'est plus cherché
        var guest = RenderAnonymous<Chess>();
        await guest.Find("input.chess-guest-name").ChangeAsync(new() { Value = "Léo" });
        await guest.Find("input.chess-code-input").ChangeAsync(new() { Value = OtherCode(code) });
        for (var i = 1; i < AccountLimiter.ChessCodesPerMinute; i++)
        {
            await guest.Click("Rejoindre");
            guest.WaitForText("Aucune partie n'attend avec ce code");
        }
        await guest.Click("Rejoindre");
        guest.WaitForText(AccountLimiter.TooManyChessCodesMessage);
    }
}
