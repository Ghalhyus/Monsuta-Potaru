using MonsutaPotaru.Services;

namespace MonsutaPotaru.Tests;

/// <summary>
/// Parties d'échecs en direct (<see cref="ChessHub"/>) : invitation, choix des équipes (pas la même), coups vérifiés,
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
    public void Teams_CannotBeTheSame_AndHostPlaysWhite()
    {
        var (session, _) = _hub.Request(Sacha, Ondine, 120);
        Assert.Null(_hub.Accept(session!, "ondine"));
        Assert.Equal("Cette équipe n'existe pas.", _hub.ChooseTeam(session!, "ondine", "pirate"));
        Assert.Null(_hub.ChooseTeam(session!, "ondine", "feu"));
        Assert.Equal("Tu as déjà choisi ton équipe.", _hub.ChooseTeam(session!, "ondine", "psy"));

        // Ondine revient sur son choix tant que Sacha n'a pas choisi : l'équipe Feu se libère
        Assert.Equal("Tu n'as pas encore choisi d'équipe.", _hub.ClearTeam(session!, "sacha"));
        Assert.Equal("Cette partie n'attend plus d'équipe.", _hub.ClearTeam(session!, "pierre"));
        var changed = 0;
        session!.Changed += () => changed++;
        Assert.Null(_hub.ClearTeam(session!, "ondine"));
        Assert.Equal(1, changed);
        Assert.Null(session.TakenBy("sacha"));
        Assert.Null(session.TeamOf("ondine"));
        Assert.Null(_hub.ChooseTeam(session, "ondine", "feu"));
        Assert.Equal("feu", session!.TakenBy("sacha")!.Id);
        Assert.Contains("a déjà pris l'équipe Feu", _hub.ChooseTeam(session, "sacha", "feu"));
        Assert.Equal(ChessStage.TeamChoice, session.Stage);
        Assert.Null(_hub.ChooseTeam(session, "sacha", "electrique"));
        Assert.Equal(ChessStage.Playing, session.Stage);
        Assert.Contains("la partie a commencé, ton équipe ne change plus", _hub.ClearTeam(session, "ondine"));
        Assert.Equal("feu", session.TeamOf("ondine")!.Id);
        Assert.Equal(ChessColor.White, session.ColorOf("sacha"));
        Assert.Equal("Pikachu", session.At(Sq("e2"))!.Mon.Name);
        Assert.Equal("Salamèche", session.At(Sq("e7"))!.Mon.Name);
        Assert.True(session.Snapshot("sacha").MyTurn);
        Assert.False(session.Snapshot("ondine").MyTurn);
        Assert.Equal(_time.Now.UtcDateTime + ChessTiming.Margin + TimeSpan.FromSeconds(120), session.Snapshot("sacha").Deadline);
    }

    [Fact]
    public void Play_ChecksTurnAndRules()
    {
        var session = Playing();
        Assert.Equal("Ce n'est pas à toi de jouer.", _hub.Play(session, "ondine", Move("e7", "e5")));
        Assert.Equal("Ce coup n'est pas permis.", _hub.Play(session, "sacha", Move("e2", "e5")));
        Assert.Equal("Ce coup n'est pas permis.", _hub.Play(session, "sacha", Move("e7", "e5"))); // pièce de l'autre
        Assert.Equal("La partie n'est pas en cours.", _hub.Play(session, "pierre", Move("e2", "e4")));
        Assert.Empty(session.Options("ondine", Sq("e7")).Targets); // pas son tour
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
