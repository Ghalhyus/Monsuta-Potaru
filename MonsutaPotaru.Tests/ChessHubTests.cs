using MonsutaPotaru.Services;

namespace MonsutaPotaru.Tests;

/// <summary>
/// Parties d'échecs en direct (<see cref="ChessHub"/>) : invitation, choix des équipes (à l'aveugle ; la même : gardée
/// ou changée), coups vérifiés, aperçu pendant le tour adverse,
/// temps écoulé, abandon, page quittée, invitation expirée ; résultats enregistrés (<see cref="ChessRecorder"/>) et
/// comptés pour les statistiques (<see cref="ChessService"/>).
/// </summary>
public sealed class ChessHubTests : IDisposable
{
    private sealed class ManualTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private readonly ManualTime _time = new();
    private readonly ChessHub _hub;
    private readonly TestDatabase _db = new("sacha", "ondine", "pierre");
    private static readonly ChessParty Sacha = new("sacha", "Sacha");
    private static readonly ChessParty Ondine = new("ondine", "Ondine");
    private static readonly ChessParty Pierre = new("pierre", "Pierre");

    public ChessHubTests() => _hub = new ChessHub(_time, ticking: false);

    public void Dispose()
    {
        _hub.Dispose();
        _db.Dispose();
    }

    private static int Sq(string name) => ChessGame.Square(name[1] - '1', name[0] - 'a');

    private static ChessMove Move(string from, string to) => new(Sq(from), Sq(to));

    private ChessSession Playing(int seconds = 60)
    {
        var (session, _) = _hub.Request(Sacha, Ondine, seconds);
        Assert.NotNull(session);
        Assert.Null(_hub.Accept(session, "ondine"));
        Assert.Null(_hub.ChooseTeam(session, "ondine", "feu"));
        Assert.Null(_hub.ChooseTeam(session, "sacha", "psy"));
        Assert.Equal(ChessStage.Playing, session.Stage);
        return session;
    }

    [Fact]
    public void Request_ChecksTimeAndBusyPlayers()
    {
        Assert.Null(_hub.Request(Sacha, Ondine, 45).Session); // temps trafiqué
        Assert.Null(_hub.Request(Sacha, Sacha, 60).Session);
        var (session, message) = _hub.Request(Sacha, Ondine, 60);
        Assert.NotNull(session);
        Assert.Contains("Invitation envoyée à Ondine", message);
        Assert.Same(session, _hub.Incoming("ondine").Single());
        Assert.Same(session, _hub.Active("sacha"));
        Assert.Null(_hub.Active("ondine")); // une invitation reçue n'est pas encore sa partie
        Assert.Equal("Tu as déjà une partie en cours.", _hub.Request(Sacha, Pierre, 60).Message);
        Assert.Equal("Cette invitation n'est plus valable.", _hub.Accept(session, "pierre"));
    }

    [Fact]
    public void Teams_AreChosenBlind_AndHostPlaysWhite()
    {
        var (session, _) = _hub.Request(Sacha, Ondine, 120);
        Assert.Null(_hub.Accept(session!, "ondine"));
        Assert.Equal("Cette équipe n'existe pas.", _hub.ChooseTeam(session!, "ondine", "pirate"));
        Assert.Null(_hub.ChooseTeam(session!, "ondine", "feu"));
        Assert.Equal("Tu as déjà choisi ton équipe.", _hub.ChooseTeam(session!, "ondine", "psy"));

        // Sacha sait qu'Ondine a choisi, pas laquelle
        Assert.Equal(new ChessTeamChoice(null, true, null, false, false, false), session!.TeamChoice("sacha"));
        Assert.Equal("feu", session.TeamChoice("ondine").Mine!.Id);
        Assert.False(session.TeamChoice("ondine").OtherChose);

        // Ondine revient sur son choix tant que Sacha n'a pas choisi
        Assert.Equal("Tu n'as pas encore choisi d'équipe.", ChessHub.ClearTeam(session, "sacha"));
        Assert.Equal("Cette partie n'attend plus d'équipe.", ChessHub.ClearTeam(session, "pierre"));
        Assert.Equal("Cette partie n'attend plus d'équipe.", _hub.KeepTeam(session, "pierre"));
        Assert.Contains("vous n'avez plus la même", _hub.KeepTeam(session, "ondine")); // pas la même : rien à garder
        var changed = 0;
        session.Changed += () => changed++;
        Assert.Null(ChessHub.ClearTeam(session, "ondine"));
        Assert.Equal(1, changed);
        Assert.False(session.TeamChoice("sacha").OtherChose);
        Assert.False(session.TeamChoice("sacha").OtherChanging); // pas de même équipe : rien à annoncer
        Assert.Null(session.TeamOf("ondine"));
        Assert.Null(_hub.ChooseTeam(session, "ondine", "feu"));
        Assert.Null(_hub.ChooseTeam(session, "sacha", "electrique"));
        Assert.Equal(ChessStage.Playing, session.Stage);
        Assert.Contains("la partie a commencé, ton équipe ne change plus", ChessHub.ClearTeam(session, "ondine"));
        Assert.Equal("feu", session.TeamOf("ondine")!.Id);
        Assert.Equal(ChessColor.White, session.ColorOf("sacha"));
        Assert.Equal("Pikachu", session.At(Sq("e2"))!.Mon.Name);
        Assert.Equal("Salamèche", session.At(Sq("e7"))!.Mon.Name);
        Assert.True(session.Snapshot("sacha").MyTurn);
        Assert.False(session.Snapshot("ondine").MyTurn);
        Assert.Equal(_time.Now.UtcDateTime + ChessTiming.Margin + TimeSpan.FromSeconds(120), session.Snapshot("sacha").Deadline);
        Assert.False(session.SameTeams);
        Assert.DoesNotContain(session.Units(), u => u.Shiny);
    }

    [Fact]
    public void SameTeam_IsKeptByBoth_OrChanged()
    {
        var (session, _) = _hub.Request(Sacha, Ondine, 120);
        Assert.Null(_hub.Accept(session!, "ondine"));
        Assert.Null(_hub.ChooseTeam(session!, "ondine", "feu"));
        Assert.Null(_hub.ChooseTeam(session!, "sacha", "feu"));

        // La même : elle se dévoile aux deux, la partie attend
        Assert.Equal(ChessStage.TeamChoice, session!.Stage);
        Assert.Equal("feu", session.TeamChoice("sacha").Same!.Id);
        Assert.Equal("feu", session.TeamChoice("ondine").Same!.Id);

        // Sacha la garde ; Ondine en change : Sacha l'apprend, et peut garder la sienne ou changer aussi
        Assert.Null(_hub.KeepTeam(session, "sacha"));
        Assert.Equal((true, false), (session.TeamChoice("sacha").IKeep, session.TeamChoice("sacha").OtherKeeps));
        Assert.True(session.TeamChoice("ondine").OtherKeeps);
        Assert.Null(ChessHub.ClearTeam(session, "ondine"));
        var sacha = session.TeamChoice("sacha");
        Assert.Equal(("feu", null, false, true), (sacha.Mine!.Id, sacha.Same, sacha.IKeep, sacha.OtherChanging));
        Assert.False(session.TeamChoice("ondine").OtherChanging); // c'est elle qui change
        Assert.True(session.TeamChoice("ondine").OtherChose);

        // Sacha change aussi : les deux rechoisissent, plus rien à annoncer
        Assert.Null(ChessHub.ClearTeam(session, "sacha"));
        Assert.False(session.TeamChoice("sacha").OtherChanging);
        Assert.False(session.TeamChoice("ondine").OtherChanging);
        Assert.Equal((false, false), (session.TeamChoice("sacha").OtherChose, session.TeamChoice("ondine").OtherChose));

        // Encore la même (Poison) : les deux la gardent, la partie commence, les noirs en chromatique
        Assert.Null(_hub.ChooseTeam(session, "sacha", "poison"));
        Assert.Null(_hub.ChooseTeam(session, "ondine", "poison"));
        Assert.Null(_hub.KeepTeam(session, "ondine"));
        Assert.Equal(ChessStage.TeamChoice, session.Stage);
        Assert.Null(_hub.KeepTeam(session, "sacha"));
        Assert.Equal(ChessStage.Playing, session.Stage);
        Assert.True(session.SameTeams);
        Assert.Equal("poison", session.TeamOf("ondine")!.Id);
        Assert.All(session.Units(), u => Assert.Equal(u.Color == ChessColor.Black, u.Shiny));
        var blackKing = session.Units().Single(u => u is { Color: ChessColor.Black, Piece: ChessPiece.King });
        Assert.Equal((blackKing.Mon.DonjonFolderOf(true), blackKing.Mon.SpriteUrlOf(true)), (blackKing.DonjonFolder, blackKing.SpriteUrl));
        var whiteKing = session.Units().Single(u => u is { Color: ChessColor.White, Piece: ChessPiece.King });
        Assert.Equal(whiteKing.Mon.SpriteUrl, whiteKing.SpriteUrl);
        Assert.Equal("Cette partie n'attend plus d'équipe.", _hub.KeepTeam(session, "sacha"));
    }

    [Fact]
    public void Options_PreviewOwnPieces_DuringTheOtherTurn()
    {
        var session = Playing(); // Sacha (Psy, blancs) contre Ondine (Feu, noirs) : aux blancs
        // Ondine regarde son pion e7 (Salamèche, Feu) : ni cases ni échange (elle ne peut pas jouer) ; pas les pièces de Sacha
        var preview = session.Options("ondine", Sq("e7"));
        Assert.Empty(preview.Targets);
        Assert.Empty(preview.Swaps);
        Assert.Empty(session.Options("ondine", Sq("e2")).Targets);
        Assert.Equal("Ce n'est pas à toi de jouer.", _hub.Play(session, "ondine", Move("e7", "e5")));

        // Cadres : Salamèche (Feu) contre l'équipe Psy, sur tout le plateau. Tarsal (Psy), Gallame (Combat), Gardevoir
        // (Fée), Rhinolove (Vol) : neutres ; Kaorine (Sol) : neutre ; Roigada (Eau) résiste (0,5) ; les pions Lippouti
        // (Glace, b2, d2, f2, h2) lui sont faibles (×2)
        var matchups = preview.Matchups;
        Assert.All(matchups, m => Assert.NotEqual(1, m.Effect));
        Assert.Equal([Sq("c1"), Sq("f1")], matchups.Where(m => m.Effect < 1).Select(m => m.Square).Order());
        Assert.Equal([Sq("b2"), Sq("d2"), Sq("f2"), Sq("h2")], matchups.Where(m => m.Effect > 1).Select(m => m.Square).Order());
    }

    [Fact]
    public void Play_ChecksTurnAndRules()
    {
        var session = Playing();
        Assert.Equal("Ce n'est pas à toi de jouer.", _hub.Play(session, "ondine", Move("e7", "e5")));
        Assert.Equal("Ce coup n'est pas permis.", _hub.Play(session, "sacha", Move("e2", "e5")));
        Assert.Equal("Ce coup n'est pas permis.", _hub.Play(session, "sacha", Move("e7", "e5"))); // pièce de l'autre
        Assert.Equal("La partie n'est pas en cours.", _hub.Play(session, "pierre", Move("e2", "e4")));
        Assert.Empty(session.Options("ondine", Sq("e7")).Swaps); // pas son tour : un aperçu seulement
        Assert.Equal(2, session.Options("sacha", Sq("e2")).Targets.Count);

        var changed = 0;
        session.Changed += () => changed++;
        Assert.Null(_hub.Play(session, "sacha", Move("e2", "e4")));
        Assert.Equal(1, changed);
        var step = Assert.Single(session.Steps(0));
        Assert.Equal("Tarsal va en e4.", step.Text);
        var snapshot = session.Snapshot("ondine");
        Assert.True(snapshot.MyTurn);
        Assert.Equal(_time.Now.UtcDateTime + ChessTiming.Playback(step) + TimeSpan.FromSeconds(60), snapshot.Deadline);
    }

    [Fact]
    public void Timeout_LosesTheGame()
    {
        var session = Playing(30);
        ChessSession? finished = null;
        _hub.Finished += s => finished = s;
        _time.Now += TimeSpan.FromSeconds(30);
        _hub.Tick();
        Assert.Equal(ChessStage.Playing, session.Stage); // la marge du début
        _time.Now += TimeSpan.FromSeconds(2);
        _hub.Tick();
        Assert.Equal(ChessStage.Done, session.Stage);
        var snapshot = session.Snapshot("ondine");
        Assert.Equal((ChessColor.Black, ChessEnd.Timeout), (snapshot.Winner, snapshot.End));
        Assert.Contains("Sacha n'a pas joué à temps", snapshot.Notice);
        Assert.Same(session, finished);
        Assert.Null(_hub.Active("sacha"));
    }

    [Fact]
    public void Forfeit_AndLeaving()
    {
        var session = Playing();
        _hub.Forfeit(session, "ondine");
        Assert.Equal((ChessColor.White, ChessEnd.Forfeit), (session.Snapshot("sacha").Winner, session.Snapshot("sacha").End));
        Assert.Equal("Ondine abandonne la partie.", session.Notice);

        _hub.Leave(session, "sacha"); // finie : sans effet
        Assert.Equal(ChessEnd.Forfeit, session.Snapshot("sacha").End);

        var other = Playing();
        _hub.Leave(other, "sacha", "Sacha a quitté la partie.");
        Assert.Equal((ChessColor.Black, ChessEnd.Left), (other.Snapshot("sacha").Winner, other.Snapshot("sacha").End));
    }

    [Fact]
    public void Invitation_RefusedCancelledOrExpired()
    {
        var (refused, _) = _hub.Request(Sacha, Ondine, 60);
        _hub.Leave(refused!, "ondine");
        Assert.Equal((ChessStage.Cancelled, "Ondine a refusé la partie."), (refused!.Stage, refused.Notice));

        var (choosing, _) = _hub.Request(Sacha, Ondine, 60);
        _hub.Accept(choosing!, "ondine");
        _hub.Leave(choosing!, "sacha");
        Assert.Equal((ChessStage.Cancelled, "Sacha a annulé la partie."), (choosing!.Stage, choosing.Notice));

        var (expired, _) = _hub.Request(Sacha, Ondine, 60);
        var requests = new List<string>();
        _hub.RequestsChanged += requests.Add;
        _time.Now += ChessHub.RequestLifetime;
        _hub.Tick();
        Assert.Equal(ChessStage.Cancelled, expired!.Stage);
        Assert.Contains("l'invitation a expiré", expired.Notice);
        Assert.Equal(["ondine"], requests);
        Assert.Empty(_hub.Incoming("ondine"));
    }

    [Fact]
    public void PublicGame_IsJoinedOnceByItsCode()
    {
        Assert.Null(_hub.CreatePublic(Sacha, 45).Session); // temps trafiqué
        var (session, message) = _hub.CreatePublic(Sacha, 60);
        Assert.NotNull(session);
        var code = session.Code!;
        Assert.Equal(ChessHub.CodeLength, code.Length);
        Assert.All(code, c => Assert.Contains(c, ChessHub.CodeAlphabet));
        Assert.Contains($"donne le code {ChessHub.FormatCode(code)}", message);
        Assert.True(session.IsPublic);
        Assert.Same(session, _hub.Active("sacha"));
        Assert.False(session.Involves("")); // pas encore d'adversaire
        Assert.Equal("Tu as déjà une partie en cours.", _hub.CreatePublic(Sacha, 60).Message);
        Assert.Empty(_hub.Incoming("ondine")); // une partie publique n'est l'invitation de personne

        // Code mal tapé, trop court, ou son propre code
        Assert.Equal("Le code a 8 caractères : vérifie-le.", _hub.JoinPublic("ABC", Ondine).Error);
        var other = code[0] == 'A' ? "B" + code[1..] : "A" + code[1..];
        Assert.Contains("Aucune partie n'attend avec ce code", _hub.JoinPublic(other, Ondine).Error);
        Assert.Equal("C'est ta propre partie : donne ce code à ton adversaire.", _hub.JoinPublic(code, Sacha).Error);

        // Saisi en minuscules, avec un tiret et des espaces : rejoint ; l'invité joue les noirs
        var guestId = ChessHub.NewGuestId();
        var typed = $" {code[..4].ToLowerInvariant()}-{code[4..]} ";
        var (joined, error) = _hub.JoinPublic(typed, new ChessParty(guestId, "Léo (invité)"));
        Assert.Null(error);
        Assert.Same(session, joined);
        Assert.Equal((ChessStage.TeamChoice, "Léo (invité)"), (session.Stage, session.Guest.Pseudo));
        Assert.Equal(ChessColor.Black, session.ColorOf(guestId));
        Assert.Same(session, _hub.Active(guestId));

        // Une seule fois
        Assert.Contains("Aucune partie n'attend avec ce code", _hub.JoinPublic(code, Ondine).Error);
        Assert.Null(_hub.ChooseTeam(session, guestId, "feu"));
        Assert.Null(_hub.ChooseTeam(session, "sacha", "psy"));
        Assert.Equal(ChessStage.Playing, session.Stage);
    }

    [Fact]
    public void PublicGame_Expires_AndBusyPlayersCannotJoin()
    {
        var (busy, _) = _hub.Request(Pierre, Ondine, 60);
        Assert.Null(_hub.Accept(busy!, "ondine"));
        var (session, _) = _hub.CreatePublic(Sacha, 60);
        Assert.Equal("Tu as déjà une partie en cours.", _hub.JoinPublic(session!.Code, Ondine).Error);

        _time.Now += ChessHub.RequestLifetime;
        _hub.Tick();
        Assert.Equal((ChessStage.Cancelled, "Personne n'a rejoint la partie à temps : le code a expiré."), (session.Stage, session.Notice));
        Assert.Contains("Aucune partie n'attend avec ce code", _hub.JoinPublic(session.Code, new ChessParty(ChessHub.NewGuestId(), "Léo (invité)")).Error);
        Assert.Null(_hub.Active("sacha"));
    }

    [Fact]
    public void Rematch_ColorsSwap_WhoeverProposes()
    {
        var previous = Playing(seconds: 300);
        _hub.Forfeit(previous, "ondine");
        var requests = new List<string>();
        _hub.RequestsChanged += requests.Add;

        // Sacha avait les blancs et propose : il invite, mais Ondine prend les blancs (l'hôte)
        var (rematch, message) = _hub.Rematch(previous, "sacha");
        Assert.NotNull(rematch);
        Assert.Equal("Invitation envoyée à Ondine : en attente de sa réponse.", message);
        Assert.Equal((previous.Id, 300, false), (rematch.RematchOf, rematch.MoveSeconds, rematch.IsPublic));
        Assert.Equal((Ondine, Sacha), (rematch.Host, rematch.Guest));
        Assert.Equal((Sacha, Ondine), (rematch.Inviter, rematch.Invitee));
        Assert.Equal((ChessColor.White, ChessColor.Black), (rematch.ColorOf("ondine"), rematch.ColorOf("sacha")));
        Assert.Equal(["ondine"], requests);
        Assert.Same(rematch, _hub.Active("sacha")); // celui qui invite l'attend
        Assert.Null(_hub.Active("ondine"));
        Assert.Same(rematch, _hub.Incoming("ondine").Single());
        Assert.Same(rematch, _hub.RematchOffer(previous, "ondine"));
        Assert.Null(_hub.RematchOffer(previous, "sacha"));
        Assert.Equal("Cette invitation n'est plus valable.", _hub.Accept(rematch, "sacha")); // pas la sienne à accepter

        Assert.Null(_hub.Accept(rematch, "ondine"));
        Assert.Equal(ChessStage.TeamChoice, rematch.Stage);
        Assert.Null(rematch.TeamOf("sacha")); // équipes à rechoisir
        Assert.Null(_hub.ChooseTeam(rematch, "sacha", "psy"));
        Assert.Null(_hub.ChooseTeam(rematch, "ondine", "feu"));
        Assert.Equal("Ce n'est pas à toi de jouer.", _hub.Play(rematch, "sacha", Move("e7", "e5")));
        Assert.Null(_hub.Play(rematch, "ondine", Move("e2", "e4"))); // les blancs commencent : Ondine

        // Revanche de la revanche, proposée par Ondine (les blancs) : Sacha reprend les blancs
        _hub.Forfeit(rematch, "sacha");
        var (third, _) = _hub.Rematch(rematch, "ondine");
        Assert.Equal((Sacha, Ondine, Ondine), (third!.Host, third.Guest, third.Inviter));
    }

    [Fact]
    public void Rematch_RefusedExpiredOrCrossed()
    {
        var previous = Playing();
        _hub.Forfeit(previous, "sacha");

        // Ondine avait les noirs : elle invite et prend les blancs
        var (rematch, _) = _hub.Rematch(previous, "ondine");
        Assert.Equal((Ondine, Ondine), (rematch!.Host, rematch.Inviter));
        _hub.Leave(rematch, "sacha");
        Assert.Equal((ChessStage.Cancelled, "Sacha a refusé la partie."), (rematch.Stage, rematch.Notice));
        Assert.Null(_hub.Rematch(rematch, "ondine").Session); // une invitation refusée ne se relance pas

        var (late, _) = _hub.Rematch(previous, "sacha");
        _time.Now += ChessHub.RequestLifetime;
        _hub.Tick();
        Assert.Equal("Ondine n'a pas répondu à temps : l'invitation a expiré.", late!.Notice);

        // Les deux cliquent : la seconde demande accepte la première
        var (first, _) = _hub.Rematch(previous, "sacha");
        var (second, message) = _hub.Rematch(previous, "ondine");
        Assert.Same(first, second);
        Assert.Equal("", message);
        Assert.Equal(ChessStage.TeamChoice, first!.Stage);
    }

    [Fact]
    public void Rematch_OnlyAfterAnAcceptedGame_EvenAgainstAGuest()
    {
        var playing = Playing();
        Assert.Contains("ne peut pas être recommencée", _hub.Rematch(playing, "sacha").Message); // pas finie
        Assert.Null(_hub.Rematch(playing, "pierre").Session);
        _hub.Forfeit(playing, "sacha");
        Assert.Null(_hub.Rematch(playing, "pierre").Session); // il n'y jouait pas

        var (refused, _) = _hub.Request(Sacha, Ondine, 60);
        _hub.Leave(refused!, "ondine");
        Assert.Null(_hub.Rematch(refused!, "sacha").Session);

        var (waiting, _) = _hub.CreatePublic(Sacha, 60);
        _hub.Leave(waiting!, "sacha"); // personne n'avait rejoint
        Assert.Null(_hub.Rematch(waiting!, "sacha").Session);

        // Partie publique contre un invité sans compte : revanche possible, l'invité prend les blancs
        var guestId = ChessHub.NewGuestId();
        var open = _hub.CreatePublic(Sacha, 120).Session!;
        Assert.Null(_hub.JoinPublic(open.Code, new ChessParty(guestId, "Léo (invité)")).Error);
        _hub.Leave(open, guestId); // parti pendant le choix des équipes
        var (rematch, message) = _hub.Rematch(open, "sacha");
        Assert.Equal("Invitation envoyée à Léo (invité) : en attente de sa réponse.", message);
        Assert.Equal((guestId, ChessColor.White), (rematch!.Host.UserId, rematch.ColorOf(guestId)));
        Assert.Same(rematch, _hub.RematchOffer(open, guestId));
        _hub.Leave(rematch, guestId, "Léo (invité) a quitté la page.");
        Assert.Equal("Léo (invité) a quitté la page.", rematch.Notice);

        // L'adversaire joue déjà avec un autre
        var previous = Playing();
        _hub.Forfeit(previous, "ondine");
        _hub.Accept(_hub.Request(Pierre, Ondine, 60).Session!, "ondine");
        var (none, busy) = _hub.Rematch(previous, "sacha");
        Assert.Null(none);
        Assert.Equal("Ondine est déjà en train de jouer : réessaie plus tard.", busy);
    }

    [Theory]
    [InlineData("Léo", "Léo (invité)")]
    [InlineData("  Marie   Ève ", "Marie Ève (invité)")]
    [InlineData("O'Neil-Jr.", "O'Neil-Jr. (invité)")]
    [InlineData("L", null)]
    [InlineData("Un prénom bien trop long", null)]
    [InlineData("<script>", null)]
    [InlineData(null, null)]
    public void GuestPseudo_IsChecked(string? typed, string? expected)
    {
        var (pseudo, error) = ChessHub.GuestPseudo(typed);
        Assert.Equal(expected, pseudo);
        Assert.Equal(expected is null, error is not null);
    }

    [Fact]
    public void Codes_AreNormalizedAndFormatted()
    {
        Assert.Equal("K7MPQ2XA", ChessHub.NormalizeCode(" k7mp-q2xa\t"));
        Assert.Equal("K7MP Q2XA", ChessHub.FormatCode("K7MPQ2XA"));
        Assert.StartsWith(ChessHub.GuestIdPrefix, ChessHub.NewGuestId());
        Assert.NotEqual(ChessHub.NewGuestId(), ChessHub.NewGuestId());
    }

    [Fact]
    public async Task FinishedGames_AreRecordedAndCounted()
    {
        var won = Playing();
        Assert.Null(_hub.Play(won, "sacha", Move("e2", "e4")));
        _hub.Forfeit(won, "ondine");
        await ChessRecorder.RecordAsync(_db.Factory, won, DateTime.UtcNow);

        var cancelled = _hub.Request(Sacha, Ondine, 60).Session!;
        _hub.Leave(cancelled, "sacha");
        await ChessRecorder.RecordAsync(_db.Factory, cancelled, DateTime.UtcNow); // pas de partie : rien

        using (var db = _db.NewContext())
        {
            var record = Assert.Single(db.ChessRecords);
            Assert.Equal(("sacha", "ondine", "psy", "feu", 1, "Forfeit", "sacha"),
                (record.WhiteId, record.BlackId, record.WhiteTeam, record.BlackTeam, record.Moves, record.End, record.WinnerId));
        }
        var stats = new ChessService(_db.Factory);
        async Task<(int, int, int, int, int, int, string?)> Counts(string userId)
        {
            var c = await stats.StatsAsync(userId);
            return (c.Games.Won, c.Games.Lost, c.Games.Draws, c.Games.Players, c.PiecesTaken, c.PiecesLost, c.FavoriteTeam?.Id);
        }
        Assert.Equal((1, 0, 0, 1, 0, 0, "psy"), await Counts("sacha"));
        Assert.Equal((0, 1, 0, 1, 0, 0, "feu"), await Counts("ondine"));
        Assert.Equal((0, 0, 0, 0, 0, 0, null), await Counts("pierre"));

        // Partie publique contre un invité sans compte : comptée pour l'hôte (l'invité n'a pas de statistiques)
        var guestId = ChessHub.NewGuestId();
        var open = _hub.CreatePublic(Sacha, 60).Session!;
        Assert.Null(_hub.JoinPublic(open.Code, new ChessParty(guestId, "Léo (invité)")).Error);
        Assert.Null(_hub.ChooseTeam(open, guestId, "feu"));
        Assert.Null(_hub.ChooseTeam(open, "sacha", "psy"));
        // Tarsal (Psy) mange Salamèche (Feu) : une pièce prise par les blancs
        Assert.Null(_hub.Play(open, "sacha", Move("e2", "e4")));
        Assert.Null(_hub.Play(open, guestId, Move("d7", "d5")));
        Assert.Null(_hub.Play(open, "sacha", Move("e4", "d5")));
        _hub.Forfeit(open, "sacha");
        await ChessRecorder.RecordAsync(_db.Factory, open, DateTime.UtcNow);
        using (var db = _db.NewContext())
            Assert.Single(db.ChessRecords, r => r.BlackId == guestId && r.WinnerId == guestId && r.WhiteCaptures == 1 && r.BlackCaptures == 0);
        Assert.Equal((1, 1, 0, 1, 1, 0, "psy"), await Counts("sacha")); // l'invité n'est pas un adversaire différent
    }
}
