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

        // Ondine prend l'équipe Dracaufeu : Sacha sait qu'elle a choisi, pas laquelle (toutes restent libres pour lui)
        await ondine.FindAll(".chess-team")[1].QuerySelector("button")!.ClickAsync(new());
        ondine.WaitForText("Tu as choisi l'équipe Dracaufeu");
        sacha.WaitForText("ondine a choisi son équipe.");
        Assert.DoesNotContain("Dracaufeu", sacha.Find(".chess-other-chose").TextContent);
        Assert.All(sacha.FindAll(".chess-team button"), b => Assert.False(b.HasAttribute("disabled")));
        Assert.Empty(sacha.FindAll(".chess-team.taken"));

        // Ondine change d'avis tant que Sacha n'a pas choisi, puis la reprend
        await ondine.Click("Changer d'équipe");
        ondine.WaitForText("tu pourras changer la tienne tant que sacha n'a pas choisi.");
        sacha.WaitForAssertion(() => Assert.Empty(sacha.FindAll(".chess-other-chose")));
        await ondine.FindAll(".chess-team")[1].QuerySelector("button")!.ClickAsync(new());
        ondine.WaitForText("Tu as choisi l'équipe Dracaufeu");
        sacha.WaitForText("ondine a choisi son équipe.");
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

        // Ondine clique pendant le tour de Sacha : un aperçu de sa pièce, rien ne se joue
        await Click(ondine, "e7");
        ondine.WaitForText("Aperçu (au tour de sacha) : Salamèche (Feu) · Pion.");
        await Click(ondine, "e5");
        Assert.Equal(ChessStage.Playing, Services.GetRequiredService<ChessHub>().Active("sacha")!.Stage);
        Assert.Empty(Services.GetRequiredService<ChessHub>().Active("sacha")!.Steps(0));

        // Sacha choisit un Tarsal, puis sa case
        await Click(sacha, "e2");
        sacha.WaitForText("Tarsal (Psy) · Pion.");
        await Click(sacha, "e4");
        sacha.WaitForText("Tarsal va en e4.");
        ondine.WaitForText("À toi de jouer.");
        sacha.WaitForText("Au tour de ondine");

        // Une pièce adverse : décrite, et les pièces d'Ondine qu'elle menace (demandé le 2026-11-25). Tarsal (Psy) : les
        // Démolosse (Ténèbres, b8, g8) et Goupelin (Psy, d8) lui résistent ; Hoopa Enchaîné (Spectre) : Goupelin en danger
        await Click(ondine, "c2");
        ondine.WaitForText("Tarsal (Psy) · Pion adverse. Sur le plateau, en bleu, bouclier : il ne peut pas les manger.");
        Assert.Equal([Sq("b8"), Sq("d8"), Sq("g8")], LastMarks("resist"));
        Assert.Empty(LastMarks("weak"));
        await Click(ondine, "c1");
        ondine.WaitForText("Hoopa Enchaîné (Spectre) · Fou adverse. Sur le plateau, en rouge, bouclier cassé : il leur est super efficace (tes pièces en danger)");
        Assert.Equal([Sq("d8")], LastMarks("weak"));
        Assert.Empty(LastMoves());
        await Click(ondine, "a8");
        ondine.WaitForText("Boumata ne peut pas bouger.");
        await Click(ondine, "d7");
        await Click(ondine, "d5");
        sacha.WaitForText("À toi de jouer.");

        // Prise neutre : Tarsal (Psy) mange Caninos d'Hisui (Roche, le pion d7)
        await Click(sacha, "e4");
        await Click(sacha, "d5");
        sacha.WaitForText("Tarsal mange Caninos d'Hisui en d5.");

        // Fenêtre « Pièces » : les deux équipes, un K.O. compté
        await sacha.Click("Pièces");
        var pieces = sacha.Find(".chess-pieces").TextContent;
        Assert.Contains("Toi · blancs", pieces);
        Assert.Contains("ondine · noirs", pieces);
        Assert.Contains("Gardevoir", pieces);
        Assert.Contains("Fée", pieces);
        Assert.Contains("3 sur le plateau · 1 K.O.", PageTestExtensions.Normalize(pieces)); // les Caninos d'Hisui
        await sacha.Click("Fermer");
        Assert.Empty(sacha.FindAll(".chess-pieces"));

        // « Pièces » et la table de type : boutons dans la couleur principale du site
        Assert.Contains("btn-primary", sacha.Find(".chess-pieces-button").ClassName);
        Assert.Contains("btn-primary", sacha.Find(".chess-help-button").ClassName);

        // Fenêtre « Table de type des pièces » : qui mange qui, dans les deux sens (Sacha : équipe Gallame, Ondine :
        // équipe Dracaufeu)
        await sacha.Click("Table de type des pièces");
        Assert.Equal("Table de type des pièces", sacha.Find(".chess-help h5").TextContent);
        var tables = sacha.FindAll(".chess-help-table");
        Assert.Equal(["Tes Pokémon contre les siens", "Ceux de ondine contre les tiens"],
            tables.Select(t => t.QuerySelector("caption")!.TextContent));
        foreach (var table in tables)
        {
            Assert.Equal(7, table.QuerySelectorAll("thead th.chess-help-col").Length);
            Assert.All(table.QuerySelectorAll("tbody tr"), row => Assert.Equal(7, row.QuerySelectorAll("td").Length));
        }
        // Colonnes : roi, reine, tour, fou, cavalier, pion (premier type), pion (second type)
        string Mark(int table, string attacker, int column) =>
            tables[table].QuerySelectorAll("tbody tr").Single(r => r.QuerySelector("th")!.TextContent.Contains(attacker))
                .QuerySelectorAll("td")[column].TextContent;
        Assert.Equal("×2", Mark(0, "Gallame", 4)); // Combat contre Démolosse (Ténèbres)
        Assert.Equal("×2", Mark(0, "Gardevoir", 2)); // Fée contre Boumata (Dragon)
        Assert.Equal("✗", Mark(0, "Tarsal", 4)); // Psy contre Ténèbres : sans effet
        Assert.Equal("✗", Mark(0, "Gallame", 0)); // Combat contre Dracaufeu (Vol) : pas très efficace
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
    public async Task PoisonTeam_ShowsBothNidoranOnOnePawnLine_AndZubatOnTheOther()
    {
        var (sacha, ondine) = await Invited();
        await ondine.Click("Accepter");
        ondine.WaitForText("Choisis ton équipe");
        Assert.Equal(["Équipe Gallame", "Équipe Dracaufeu", "Équipe Tokorico", "Équipe Nidoking", "Équipe Hyporoi", "Équipe Torterra", "Équipe Rhinastoc",
                "Équipe Galeking", "Équipe Dracolosse", "Équipe Diancie", "Équipe Scalpereur",
                "Équipe Ursaking", "Équipe Shifours Mille Poings", "Équipe Gueriaigle", "Équipe Terrakium",
                "Équipe Kaimorse", "Équipe Sarmuraï", "Équipe Lugulabre"],
            ondine.FindAll(".chess-team h3").Select(h => h.TextContent));
        // Deux lignes « Pion » : les Nidoran (Poison, deux images), puis Nosferapti (Vol)
        var lines = ondine.FindAll(".chess-team")[3].QuerySelectorAll(".chess-team-list li");
        Assert.Equal(7, lines.Length);
        Assert.Equal(2, lines[^2].QuerySelectorAll(".chess-mon-images img").Length);
        Assert.Equal("Nidoran♂ / Nidoran♀", lines[^2].QuerySelector(".chess-mon-name")!.TextContent);
        Assert.Equal("Nosferapti", lines[^1].QuerySelector(".chess-mon-name")!.TextContent);
        Assert.Contains("Vol", lines[^1].TextContent);
        Assert.Contains("Pion", lines[^1].TextContent);
        await ondine.FindAll(".chess-team")[3].QuerySelector("button")!.ClickAsync(new()); // Poison
        sacha.WaitForText("Choisis ton équipe");
        await sacha.FindAll(".chess-team")[0].QuerySelector("button")!.ClickAsync(new()); // Psy
        sacha.WaitForText("À toi de jouer.");

        // Table de type : sept colonnes, une par type de pion ; celle des Nidoran avec deux images (Psy contre Poison : ×2)
        await sacha.Click("Table de type des pièces");
        var columns = sacha.FindAll(".chess-help-table")[0].QuerySelectorAll("thead th.chess-help-col");
        Assert.Equal(7, columns.Length);
        Assert.Equal(2, columns[5].QuerySelectorAll("img").Length);
        Assert.Contains("Nidoran♂ / Nidoran♀", columns[5].TextContent);
        Assert.Contains("Nosferapti", columns[6].TextContent);
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

        // 1. e4 Cf6 2. g3 Cxe4 (Ténèbres contre Tarsal, Psy : super efficace, Démolosse rejoue). Tarsal en a, c, e, g ;
        // Lippouti (Glace : neutre pour Démolosse) en b, d, f, h
        await Click(sacha, "e2"); await Click(sacha, "e4");
        ondine.WaitForText("À toi de jouer.");
        await Click(ondine, "g8"); await Click(ondine, "f6");
        sacha.WaitForText("À toi de jouer.");
        await Click(sacha, "g2"); await Click(sacha, "g3");
        ondine.WaitForText("À toi de jouer.");
        await Click(ondine, "f6"); await Click(ondine, "e4");
        ondine.WaitForText("Super efficace ! Démolosse rejoue");
        Assert.True(ondine.HasButton("Passer"));
        sacha.WaitForText("Au tour de ondine");
        // Sur le plateau, la prise de Tarsal en g3 n'a pas les coins dorés (« il rejouera ») : un seul tour bonus
        ondine.WaitForAssertion(() => Assert.False((bool?)LastMarkedMove(Sq("g3"))?.GetType().GetProperty("super")!.GetValue(LastMarkedMove(Sq("g3")))));

        // Pendant son tour bonus, Démolosse mange encore un Tarsal (super efficace) : il ne rejoue pas une deuxième fois
        await Click(ondine, "g3");
        ondine.WaitForText("Démolosse mange Tarsal en g3. C'est super efficace, mais Démolosse a déjà rejoué.");
        sacha.WaitForText("À toi de jouer.");
        Assert.False(ondine.HasButton("Passer"));

        // 4. c3 Ce4 5. h3 Cxc3 : un nouveau tour, un nouveau tour bonus. Démolosse ne peut pas manger Gardevoir (Fée) en
        // d1 : expliqué, rien n'est joué ; il passe
        await Click(sacha, "c2"); await Click(sacha, "c3");
        ondine.WaitForText("À toi de jouer.");
        await Click(ondine, "g3"); await Click(ondine, "e4");
        sacha.WaitForText("À toi de jouer.");
        await Click(sacha, "h2"); await Click(sacha, "h3");
        ondine.WaitForText("À toi de jouer.");
        await Click(ondine, "e4"); await Click(ondine, "c3");
        ondine.WaitForText("Super efficace ! Démolosse rejoue");
        await Click(ondine, "d1");
        ondine.WaitForText("Pas très efficace : Démolosse (Ténèbres) ne peut pas manger Gardevoir (Fée).");
        await ondine.Click("Passer");
        ondine.WaitForText("Démolosse ne rejoue pas.");
        sacha.WaitForText("À toi de jouer.");
    }

    [Fact]
    public async Task BonusTurn_WithTheKingInCheck_CannotBePassed()
    {
        // Partie lancée par le hub, puis posée sur une position : Gallame (roi Combat) peut manger Démolosse (Ténèbres) en
        // e5, où Boumata (tour Dragon, a5) le mangerait ; il rejoue et doit s'en aller
        var hub = Services.GetRequiredService<ChessHub>();
        var session = hub.Request(new ChessParty("sacha", "sacha"), new ChessParty("ondine", "ondine"), 120).Session!;
        Assert.Null(hub.Accept(session, "ondine"));
        Assert.Null(hub.ChooseTeam(session, "sacha", "psy"));
        Assert.Null(hub.ChooseTeam(session, "ondine", "feu"));
        var position = ChessGame.FromPosition(ChessTeams.Find("psy")!, ChessTeams.Find("feu")!, ChessColor.White,
            [(ChessColor.White, ChessPiece.King, Sq("e4")), (ChessColor.Black, ChessPiece.Knight, Sq("e5")),
             (ChessColor.Black, ChessPiece.Rook, Sq("a5")), (ChessColor.Black, ChessPiece.King, Sq("h8"))]);
        // ChessSession.Game est interne : posé par réflexion (aucun accès des tests aux internes du site)
        typeof(ChessSession).GetField("Game", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(session, position);
        var sacha = Render<Chess>();
        sacha.WaitForText("À toi de jouer.");

        await Click(sacha, "e4");
        await Click(sacha, "e5");
        sacha.WaitForText("ton roi est en échec, choisis une case qui le met à l'abri.");
        Assert.False(sacha.HasButton("Passer"));
        await Click(sacha, "e6");
        sacha.WaitForText("Au tour de ondine");
        await DisposeComponentsAsync();
    }

    [Fact]
    public async Task RedCross_OnlyInHighContrast()
    {
        // Position posée : Gallame (roi Combat) en e4 atteint Scolocendre (fou Insecte) en d5 mais ne peut pas le manger
        var hub = Services.GetRequiredService<ChessHub>();
        var session = hub.Request(new ChessParty("sacha", "sacha"), new ChessParty("ondine", "ondine"), 120).Session!;
        Assert.Null(hub.Accept(session, "ondine"));
        Assert.Null(hub.ChooseTeam(session, "sacha", "psy"));
        Assert.Null(hub.ChooseTeam(session, "ondine", "feu"));
        var position = ChessGame.FromPosition(ChessTeams.Find("psy")!, ChessTeams.Find("feu")!, ChessColor.White,
            [(ChessColor.White, ChessPiece.King, Sq("e4")), (ChessColor.Black, ChessPiece.Bishop, Sq("d5")),
             (ChessColor.Black, ChessPiece.King, Sq("h8"))]);
        typeof(ChessSession).GetField("Game", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(session, position);
        var sacha = Render<Chess>();
        sacha.WaitForText("À toi de jouer.");

        // Mode par défaut : Scolocendre est bleu (bouclier), sans croix dans la légende ; la croix est dans les marques, le
        // plateau ne la dessine qu'en contraste élevé
        await Click(sacha, "e4");
        Assert.Contains(Sq("d5"), LastMarks("blocked"));
        Assert.Contains("en bleu, bouclier : il ne peut pas les manger", sacha.Find(".chess-info").TextContent);
        Assert.DoesNotContain("croix rouge", sacha.Find(".chess-info").TextContent);
        await sacha.Find("#chess-contrast").ChangeAsync(new() { Value = true });
        Assert.Contains("cadre rouge : il ne peut pas les manger ; croix rouge : à sa portée, mais son type l'empêche de les manger.",
            sacha.Find(".chess-info").TextContent);
        await sacha.Find("#chess-contrast").ChangeAsync(new() { Value = false });
        Assert.DoesNotContain("croix rouge", sacha.Find(".chess-info").TextContent);
        await DisposeComponentsAsync();
    }

    /// <summary>Case permise marquée sur le plateau vers <paramref name="square"/>, dans le dernier « highlight » qui en a.</summary>
    private object? LastMarkedMove(int square) => JSInterop.Invocations.Where(i => i.Identifier == "highlight")
        .Select(i => i.Arguments[0]!.GetType().GetProperty("moves")!.GetValue(i.Arguments[0]) as System.Collections.IEnumerable)
        .Select(moves => moves!.Cast<object>().FirstOrDefault(m => (int)m.GetType().GetProperty("to")!.GetValue(m)! == square))
        .LastOrDefault(m => m is not null);

    /// <summary>Cases permises (points et coins) du dernier « highlight » envoyé au plateau.</summary>
    private object[] LastMoves() => [.. ((System.Collections.IEnumerable)JSInterop.Invocations.Last(i => i.Identifier == "highlight").Arguments[0]!
        .GetType().GetProperty("moves")!.GetValue(JSInterop.Invocations.Last(i => i.Identifier == "highlight").Arguments[0])!).Cast<object>()];

    /// <summary>Cases d'une marque (« weak », « resist », « blocked ») dans le dernier « highlight » envoyé au plateau.</summary>
    private int[] LastMarks(string name) => [.. (JSInterop.Invocations.Last(i => i.Identifier == "highlight").Arguments[0]!
        .GetType().GetProperty(name)!.GetValue(JSInterop.Invocations.Last(i => i.Identifier == "highlight").Arguments[0])
        as IEnumerable<int> ?? []).Order()];

    [Fact]
    public async Task SelectedPiece_FramesWeakAndResistantPieces_AndCrossesBlockedOnes()
    {
        var (sacha, ondine) = await Invited();
        await ondine.Click("Accepter");
        await ChooseTeams(sacha, ondine); // Sacha : Gallame (Psy), Ondine : Dracaufeu (Feu)
        sacha.WaitForText("À toi de jouer.");

        // Gallame, sur e1, choisi : Combat contre l'équipe Feu. Super efficace contre Démolosse (Ténèbres, b8 et g8) et
        // les pions Caninos d'Hisui (Roche, b7, d7, f7, h7) ; Scolocendre (Insecte, c8 et f8), Goupelin (Psy, d8) et Dracaufeu
        // (Vol, e8) lui résistent. Rien à sa portée
        await Click(sacha, "e1");
        sacha.WaitForText("Gallame ne peut pas bouger.");
        Assert.Equal([Sq("b7"), Sq("d7"), Sq("f7"), Sq("h7"), Sq("b8"), Sq("g8")], LastMarks("weak"));
        Assert.Equal([Sq("c8"), Sq("d8"), Sq("e8"), Sq("f8")], LastMarks("resist"));
        Assert.Empty(LastMarks("blocked"));
        Assert.Contains("Sur le plateau, en rouge, bouclier cassé : il leur est super efficace ; en bleu, bouclier : il ne peut pas les manger.", sacha.Find(".chess-info").TextContent);

        // Contraste élevé : des cadres (la légende suit tout de suite, la pièce reste choisie) ; puis retour au mode par défaut
        await sacha.Find("#chess-contrast").ChangeAsync(new() { Value = true });
        Assert.True((bool?)JSInterop.Invocations.Last(i => i.Identifier == "setContrast").Arguments[0]);
        Assert.Contains("Gallame ne peut pas bouger. Sur le plateau, cadre vert : il leur est super efficace ; cadre rouge : il ne peut pas les manger.", sacha.Find(".chess-info").TextContent);
        Assert.Equal([Sq("b7"), Sq("d7"), Sq("f7"), Sq("h7"), Sq("b8"), Sq("g8")], LastMarks("weak"));
        await sacha.Find("#chess-contrast").ChangeAsync(new() { Value = false });
        Assert.False((bool?)JSInterop.Invocations.Last(i => i.Identifier == "setContrast").Arguments[0]);
        Assert.Contains("en rouge, bouclier cassé", sacha.Find(".chess-info").TextContent);

        // 1. e4 Cc6 2. e5 : Démolosse (Ténèbres) en c6 regarde Tarsal (Psy, faible)
        await Click(sacha, "e2"); await Click(sacha, "e4");
        ondine.WaitForText("À toi de jouer.");
        await Click(ondine, "b8"); await Click(ondine, "c6");
        sacha.WaitForText("À toi de jouer.");
        await Click(sacha, "e4"); await Click(sacha, "e5");
        ondine.WaitForText("À toi de jouer.");
        await Click(ondine, "c6");
        Assert.Contains(Sq("e5"), LastMarks("weak"));

        // Sacha, pendant le tour d'Ondine, regarde Hoopa Enchaîné (Spectre) en c1 : Goupelin (Psy, d8) lui est faible ;
        // les Démolosse (Ténèbres, c6 et g8) lui résistent
        await Click(sacha, "c1");
        sacha.WaitForText("Aperçu (au tour de ondine) : Hoopa Enchaîné (Spectre) · Fou.");
        Assert.Empty(LastMoves());
        Assert.Equal([Sq("d8")], LastMarks("weak"));
        Assert.Equal([Sq("c6"), Sq("g8")], LastMarks("resist"));

        // Son Tarsal e5 : pas de case montrée (il ne peut pas jouer), et cliquer sur e6 ne joue rien
        await Click(sacha, "e5");
        sacha.WaitForText("Aperçu (au tour de ondine) : Tarsal (Psy) · Pion.");
        Assert.Empty(LastMoves());
        await Click(sacha, "e6");
        Assert.Equal(3, Services.GetRequiredService<ChessHub>().Active("sacha")!.Steps(0).Count);

        // Ondine joue : l'aperçu de Sacha s'efface
        await Click(ondine, "c6"); await Click(ondine, "d4");
        sacha.WaitForText("À toi de jouer.");
        Assert.Empty(LastMarks("weak"));
    }

    [Fact]
    public async Task SameTeam_IsConfirmedByBoth_ThenBlackIsShiny()
    {
        var (sacha, ondine) = await Invited();
        await ondine.Click("Accepter");
        ondine.WaitForText("Choisis ton équipe");
        sacha.WaitForText("Choisis ton équipe");
        await ondine.FindAll(".chess-team")[1].QuerySelector("button")!.ClickAsync(new());
        sacha.WaitForText("ondine a choisi son équipe.");
        await sacha.FindAll(".chess-team")[1].QuerySelector("button")!.ClickAsync(new());

        // La même : les deux le voient, à chacun de la garder ou d'en changer, sans voir la décision de l'autre
        sacha.WaitForText("Vous avez pris la même équipe, ondine et toi : l'équipe Dracaufeu.");
        ondine.WaitForText("Vous avez pris la même équipe, sacha et toi : l'équipe Dracaufeu.");
        await sacha.Click("Garder l'équipe Dracaufeu");
        sacha.WaitForText("Tu la gardes. En attente de la décision de ondine…");
        Assert.False(sacha.HasButton("Changer d'équipe"));
        var ondineSees = ondine.Find(".chess-same-team").TextContent;
        Assert.DoesNotContain("a gardé", ondineSees);
        Assert.DoesNotContain("la garde.", ondineSees);
        Assert.True(ondine.HasButton("Garder l'équipe Dracaufeu"));

        // Ondine change : les deux décisions se dévoilent ; Sacha peut garder la sienne, ou changer aussi
        await ondine.Click("Changer d'équipe");
        ondine.WaitForText("sacha a gardé l'équipe Dracaufeu. Si tu la reprends, la partie commence avec la même équipe des deux côtés.");
        sacha.WaitForText("ondine a choisi de changer d'équipe : tu peux garder la tienne, ou en changer aussi.");
        Assert.True(sacha.HasButton("Changer d'équipe"));

        // Elle reprend la même, que Sacha a gardée : la partie commence, les noirs en chromatique
        await ondine.FindAll(".chess-team")[1].QuerySelector("button")!.ClickAsync(new());
        sacha.WaitForText("À toi de jouer.");
        ondine.WaitForText("Au tour de sacha");

        var session = Services.GetRequiredService<ChessHub>().Active("sacha")!;
        Assert.True(session.SameTeams);
        sacha.WaitForAssertion(() => Assert.Equal(2, JSInterop.Invocations.Count(i => i.Identifier == "create")));
        foreach (var create in JSInterop.Invocations.Where(i => i.Identifier == "create"))
        {
            var units = ((System.Collections.IEnumerable)create.Arguments[2]!.GetType().GetProperty("units")!.GetValue(create.Arguments[2])!).Cast<object>();
            foreach (var unit in units)
            {
                var id = (int)unit.GetType().GetProperty("id")!.GetValue(unit)!;
                Assert.Equal(session.Units().Single(u => u.Id == id).DonjonFolder, unit.GetType().GetProperty("folder")!.GetValue(unit));
            }
        }
        Assert.All(session.Units(), u => Assert.Equal(u.Color == ChessColor.Black, u.Shiny));

        // Fenêtre « Pièces » : les images des noirs en chromatique
        await sacha.Click("Pièces");
        var columns = sacha.FindAll(".chess-pieces-teams > div");
        var black = session.Units().First(u => u is { Color: ChessColor.Black, Piece: ChessPiece.King });
        Assert.Contains(black.Mon.SpriteUrlOf(true), columns[1].InnerHtml);
        Assert.DoesNotContain(black.Mon.SpriteUrlOf(true), columns[0].InnerHtml);
    }

    /// <summary>Valeur « idle » passée par la page à echecs.js à la création de chaque plateau.</summary>
    private bool?[] BoardsIdle() => BoardsOption("idle");

    /// <summary>Valeur d'une option (« idle », « contrast ») passée par la page à echecs.js à la création de chaque plateau.</summary>
    private bool?[] BoardsOption(string name) => [.. JSInterop.Invocations.Where(i => i.Identifier == "create")
        .Select(i => i.Arguments[2]?.GetType().GetProperty(name)?.GetValue(i.Arguments[2]) as bool?)];

    [Fact]
    public async Task HighContrast_FollowsTheAccount_AndIsSavedFromTheGame()
    {
        var preferences = Services.GetRequiredService<PreferencesService>();
        Assert.True((await preferences.SetChessContrastAsync("sacha", true)).Ok); // Sacha l'a mis dans « Préférences »
        var (sacha, ondine) = await Invited();
        await ondine.Click("Accepter");
        await ChooseTeams(sacha, ondine);
        sacha.WaitForText("À toi de jouer.");

        // Chaque plateau naît avec le réglage de son joueur (case « Contraste élevé », décochée par défaut)
        Assert.Equal("Contraste élevé", sacha.Find("label[for=chess-contrast]").TextContent);
        Assert.True(sacha.Find("#chess-contrast").HasAttribute("checked"));
        Assert.False(ondine.Find("#chess-contrast").HasAttribute("checked"));
        sacha.WaitForAssertion(() => Assert.Equal(2, BoardsOption("contrast").Length));
        Assert.Equal([false, true], BoardsOption("contrast").Order());

        // Sacha l'enlève pendant la partie : le plateau suit, le compte le garde, les animations au repos ne changent pas
        await sacha.Find("#chess-contrast").ChangeAsync(new() { Value = false });
        Assert.False((bool?)JSInterop.Invocations.Last(i => i.Identifier == "setContrast").Arguments[0]);
        Assert.Equal((false, true), ((await preferences.SortsAsync("sacha")).ChessContrast, (await preferences.SortsAsync("sacha")).ChessIdle));
        await ondine.Find("#chess-contrast").ChangeAsync(new() { Value = true });
        Assert.True((await preferences.SortsAsync("ondine")).ChessContrast);
        Assert.False((await preferences.SortsAsync("sacha")).ChessContrast);
    }

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

        // Chaque plateau naît avec le réglage de son joueur (case « Désactiver les animations » : cochée, figés)
        Assert.Equal("Désactiver les animations", sacha.Find("label[for=chess-idle]").TextContent);
        Assert.True(sacha.Find("#chess-idle").HasAttribute("checked"));
        Assert.False(ondine.Find("#chess-idle").HasAttribute("checked"));
        sacha.WaitForAssertion(() => Assert.Equal(2, BoardsIdle().Length));
        Assert.Equal([false, true], BoardsIdle().Order());

        // Sacha les remet pendant la partie : le plateau suit, le compte le garde
        await sacha.Find("#chess-idle").ChangeAsync(new() { Value = false });
        Assert.True((bool?)JSInterop.Invocations.Last(i => i.Identifier == "setIdle").Arguments[0]);
        Assert.True((await preferences.SortsAsync("sacha")).ChessIdle);
        await ondine.Find("#chess-idle").ChangeAsync(new() { Value = true });
        Assert.False((bool?)JSInterop.Invocations.Last(i => i.Identifier == "setIdle").Arguments[0]);
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

    /// <summary>Les deux choisissent leur équipe (Psy pour le premier, Feu pour le second) : la partie commence.</summary>
    private static async Task ChooseTeams(IRenderedComponent<Chess> first, IRenderedComponent<Chess> second)
    {
        first.WaitForText("Choisis ton équipe");
        second.WaitForText("Choisis ton équipe");
        await first.FindAll(".chess-team")[0].QuerySelector("button")!.ClickAsync(new());
        second.WaitForText("a choisi son équipe.");
        await second.FindAll(".chess-team")[1].QuerySelector("button")!.ClickAsync(new());
        first.WaitForAssertion(() => Assert.NotNull(first.Find(".chess-board")));
        second.WaitForAssertion(() => Assert.NotNull(second.Find(".chess-board")));
    }

    [Fact]
    public async Task Rematch_BetweenFriends_ColorsSwap()
    {
        var (sacha, ondine) = await Invited();
        await ondine.Click("Accepter");
        await ChooseTeams(sacha, ondine);
        JSInterop.Setup<bool>("confirm", _ => true).SetResult(true);
        await ondine.Click("Abandonner");
        sacha.WaitForText("Victoire ! (abandon)");
        ondine.WaitForText("Défaite… (abandon)");

        // Sacha (les blancs) propose ; Ondine le voit sur son écran de fin, avec la couleur qu'elle aurait
        await sacha.Click("Recommencer avec ondine");
        sacha.WaitForText("En attente de la réponse de ondine");
        Assert.Contains("Tu joues les noirs · 5 min par coup", sacha.Text());
        ondine.WaitForAssertion(() => Assert.Contains("sacha te propose de recommencer (5 min par coup). Tu jouerais les blancs.",
            PageTestExtensions.Normalize(ondine.Find(".chess-rematch-offer").TextContent)));
        Assert.False(ondine.HasButton("Recommencer avec sacha"));
        Assert.True(Services.GetRequiredService<ChessHub>().Incoming("ondine").Single().RematchOf is not null);

        await ondine.Find(".chess-rematch-accept").ClickAsync(new());
        await ChooseTeams(ondine, sacha); // équipes rechoisies
        ondine.WaitForText("Tu joues les blancs · 5 min par coup");
        ondine.WaitForText("À toi de jouer.");
        sacha.WaitForText("Au tour de ondine");
        await DisposeComponentsAsync();
    }

    [Fact]
    public async Task Rematch_Refused_IsNotOfferedAgain()
    {
        var (sacha, ondine) = await Invited();
        await ondine.Click("Accepter");
        ondine.WaitForText("Choisis ton équipe");
        await sacha.Click("Annuler la partie"); // annulée en cours de route : on peut quand même recommencer
        ondine.WaitForText("sacha a annulé la partie.");
        await ondine.Click("Recommencer avec sacha");
        ondine.WaitForText("Invitation envoyée à sacha");

        // Sacha rouvre la page (autre onglet) : l'invitation l'attend dans la liste, où il la refuse
        var again = Render<Chess>();
        again.WaitForText("t'invite à recommencer votre partie d'échecs Pokémon (5 min par coup). Tu jouerais les noirs."); // Ondine prend les blancs
        await again.Click("Refuser");
        again.WaitForAssertion(() => Assert.DoesNotContain("t'invite", again.Text()));
        ondine.WaitForText("sacha a refusé la partie.");
        Assert.False(ondine.HasButton("Recommencer avec sacha")); // une invitation refusée ne se relance pas
        await DisposeComponentsAsync();
    }

    [Fact]
    public async Task Rematch_AgainstAGuestWithoutAccount()
    {
        var sacha = Render<Chess>();
        var code = await CreatePublic(sacha);
        var guest = RenderAnonymous<Chess>();
        await guest.Find("input.chess-guest-name").ChangeAsync(new() { Value = "Léo" });
        await guest.Find("input.chess-code-input").ChangeAsync(new() { Value = code });
        await guest.Click("Rejoindre");
        await ChooseTeams(sacha, guest);
        JSInterop.Setup<bool>("confirm", _ => true).SetResult(true);
        await sacha.Click("Abandonner");
        guest.WaitForText("Victoire ! (abandon)");

        // L'invité propose : il prend les blancs ; Sacha accepte depuis son écran de fin
        await guest.Click("Recommencer avec sacha");
        guest.WaitForText("Tu joues les blancs · 2 min par coup");
        sacha.WaitForAssertion(() => Assert.Contains("Léo (invité) te propose de recommencer", sacha.Find(".chess-rematch-offer").TextContent));
        await sacha.Find(".chess-rematch-accept").ClickAsync(new());
        await ChooseTeams(guest, sacha);
        guest.WaitForText("À toi de jouer.");

        // Fin de la revanche ; Sacha propose encore, mais l'invité quitte l'écran de fin : sa proposition est refusée
        await guest.Click("Abandonner");
        sacha.WaitForText("Victoire ! (abandon)");
        await sacha.Click("Recommencer avec Léo (invité)");
        guest.WaitForAssertion(() => Assert.Single(guest.FindAll(".chess-rematch-offer")));
        await guest.Click("Nouvelle partie");
        sacha.WaitForText("Léo (invité) a quitté la page.");
        Assert.Null(Services.GetRequiredService<ChessHub>().Active("sacha"));
        await DisposeComponentsAsync();
    }

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

        // Choix des équipes, puis la partie : l'invité joue les noirs. Le message de la copie du lien disparaît dès que
        // l'adversaire arrive (il décalait le plateau)
        guest.WaitForText("Partie contre sacha");
        sacha.WaitForText("Partie contre Léo (invité)");
        Assert.DoesNotContain("Lien copié", sacha.Text());
        await guest.FindAll(".chess-team")[1].QuerySelector("button")!.ClickAsync(new());
        await sacha.FindAll(".chess-team")[0].QuerySelector("button")!.ClickAsync(new());
        guest.WaitForText("Tu joues les noirs · 1 min par coup");
        sacha.WaitForText("Partie contre Léo (invité)");

        // L'invité a la case des animations au repos, pour cette partie seulement (pas de compte où la garder)
        Assert.False(guest.Find("#chess-idle").HasAttribute("checked"));
        await guest.Find("#chess-idle").ChangeAsync(new() { Value = true });
        Assert.False((bool?)JSInterop.Invocations.Last(i => i.Identifier == "setIdle").Arguments[0]);
        Assert.True(guest.Find("#chess-idle").HasAttribute("checked"));
        Assert.DoesNotContain("gardé dans ton compte", guest.Text());
        Assert.True((await Services.GetRequiredService<PreferencesService>().SortsAsync("sacha")).ChessIdle);
        await guest.Find("#chess-contrast").ChangeAsync(new() { Value = true }); // de même pour le contraste élevé
        Assert.True((bool?)JSInterop.Invocations.Last(i => i.Identifier == "setContrast").Arguments[0]);
        Assert.DoesNotContain("gardé dans ton compte", guest.Text());
        Assert.False((await Services.GetRequiredService<PreferencesService>().SortsAsync("sacha")).ChessContrast);

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
