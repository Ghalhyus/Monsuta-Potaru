using Microsoft.EntityFrameworkCore;
using MonsutaPotaru.Data;
using MonsutaPotaru.Services;

namespace MonsutaPotaru.Tests;

/// <summary>
/// Parties de cache-cache en direct (<see cref="HideSeekHub"/>) : salle d'attente (réglages de l'hôte, invitations, code,
/// 8 joueurs au plus), lancement, clics (trouvé, raté et bloqué), temps écoulé, tours suivants, fin et résultats
/// (<see cref="HideSeekRecorder"/>, <see cref="HideSeekService.PlaysAsync"/>), départs, « Recommencer ».
/// </summary>
public sealed class HideSeekHubTests : IDisposable
{
    private sealed class ManualTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private readonly ManualTime _time = new();
    private readonly HideSeekHub _hub;
    private readonly TestDatabase _db = new("sacha", "ondine", "pierre");
    private static readonly HideSeekParty Sacha = new("sacha", "Sacha");
    private static readonly HideSeekParty Ondine = new("ondine", "Ondine");
    private static readonly HideSeekParty Pierre = new("pierre", "Pierre");

    public HideSeekHubTests() => _hub = new HideSeekHub(_time, ticking: false);

    public void Dispose()
    {
        _hub.Dispose();
        _db.Dispose();
    }

    private void Wait(TimeSpan duration)
    {
        _time.Now += duration;
        _hub.Tick();
    }

    private static IReadOnlyList<HideSeekRound> Rounds(HideSeekSettings settings)
    {
        List<HideSeekCandidate> pool = [.. Enumerable.Range(1, 40).Select(s => HideSeekDrawTests.Mon((ushort)s))];
        var (rounds, error) = HideSeekDraw.Draw(settings, pool, HideSeekDrawTests.Fill, new Random(7));
        Assert.Null(error);
        return rounds!;
    }

    /// <summary>Sacha crée la partie, Ondine (amie invitée) et Pierre (par le code) entrent, Sacha la lance.</summary>
    private HideSeekSession Playing(HideSeekSettings? settings = null)
    {
        settings ??= new HideSeekSettings(Rounds: 2, RoundSeconds: 30, Count: 20, Landscape: ParkLandscape.Forest);
        var (session, error) = _hub.Create(Sacha, settings);
        Assert.Null(error);
        Assert.True(_hub.Invite(session!, "sacha", Ondine).Ok);
        Assert.Null(_hub.Accept(session!, "ondine"));
        Assert.Null(_hub.JoinWithCode(session!.Code, Pierre).Error);
        Assert.Null(_hub.Start(session, "sacha", settings, session.PlayersNow(), Rounds(settings)));
        Wait(TimeSpan.FromSeconds(settings.CountdownSeconds));
        Assert.Equal(HideSeekPhase.Searching, session.Snapshot("sacha").Phase);
        return session;
    }

    private static int WrongId(HideSeekSession session) =>
        session.Snapshot("sacha").Round!.Pokemon.First(p => p.Id != TargetOf(session)).Id;

    /// <summary>La cible du tour (connue du serveur : la page n'envoie au navigateur que son nom et son image).</summary>
    private static int TargetOf(HideSeekSession session) => session.Snapshot("sacha").Target!.Id;

    [Fact]
    public void Lobby_SettingsInvitationsAndCode()
    {
        Assert.Equal("Le parc compte de 20 à 80 Pokémon.", _hub.Create(Sacha, new HideSeekSettings(Count: 5)).Error);
        var (session, _) = _hub.Create(Sacha, new HideSeekSettings())!;
        Assert.NotNull(session);
        Assert.Same(session, _hub.Active("sacha"));
        Assert.Equal("Tu as déjà une partie de cache-cache en cours.", _hub.Create(Sacha, new HideSeekSettings()).Error);
        Assert.Equal(ChessHub.CodeLength, session.Code.Length);

        // Seul l'hôte règle la partie ; une valeur trafiquée est refusée
        Assert.Null(HideSeekHub.UpdateSettings(session, "sacha", new HideSeekSettings(Rounds: 3)));
        Assert.Equal(3, session.Snapshot("ondine").Settings.Rounds);
        Assert.Equal("Un tour dure de 15 secondes à 5 minutes.", HideSeekHub.UpdateSettings(session, "sacha", new HideSeekSettings(RoundSeconds: 999)));
        Assert.Equal("Le décompte dure de 3 à 10 secondes.", HideSeekHub.UpdateSettings(session, "sacha", new HideSeekSettings(CountdownSeconds: 2)));
        Assert.NotNull(HideSeekHub.UpdateSettings(session, "ondine", new HideSeekSettings()));

        // Invitation : reçue, puis acceptée ; refusée par un autre
        var notified = new List<string>();
        _hub.RequestsChanged += notified.Add;
        Assert.Equal((true, "Invitation envoyée à Ondine."), _hub.Invite(session, "sacha", Ondine));
        Assert.Equal((false, "Ondine est déjà invité."), _hub.Invite(session, "sacha", Ondine));
        Assert.False(_hub.Invite(session, "ondine", Pierre).Ok); // seul l'hôte invite
        Assert.Same(session, _hub.Incoming("ondine").Single());
        Assert.Null(_hub.Active("ondine"));
        Assert.Null(_hub.Accept(session, "ondine"));
        Assert.Equal(["Sacha", "Ondine"], session.Snapshot("sacha").Players.Select(p => p.Pseudo));
        Assert.Empty(_hub.Incoming("ondine"));
        Assert.True(_hub.Invite(session, "sacha", Pierre).Ok);
        _hub.Leave(session, "pierre"); // refuse
        Assert.Empty(session.Snapshot("sacha").Invited);
        Assert.Equal(["ondine", "ondine", "pierre", "pierre"], notified);

        // Le code : mal tapé, le sien, un invité sans compte
        Assert.Equal("Le code a 8 caractères : vérifie-le.", _hub.JoinWithCode("ABC", Pierre).Error);
        Assert.Equal("C'est ta propre partie : donne ce code aux autres joueurs.", _hub.JoinWithCode(session.Code, Sacha).Error);
        Assert.Equal("Tu es déjà dans cette partie.", _hub.JoinWithCode(session.Code, Ondine).Error);
        var guest = new HideSeekParty(ChessHub.NewGuestId(), "Léo (invité)");
        Assert.Same(session, _hub.JoinWithCode(ChessHub.FormatCode(session.Code).ToLowerInvariant(), guest).Session);
        Assert.True(guest.IsGuest);
        Assert.False(Sacha.IsGuest);

        // Un joueur (pas l'hôte) quitte la salle d'attente : il en sort, la partie reste
        _hub.Leave(session, guest.UserId);
        Assert.Equal(2, session.Snapshot("sacha").Players.Count);
        Assert.Equal(HideSeekStage.Lobby, session.Stage);
    }

    [Fact]
    public void Lobby_AtMostEightPlayers_AndHostCancels()
    {
        var (session, _) = _hub.Create(Sacha, new HideSeekSettings());
        for (var i = 1; i < HideSeekHub.MaxPlayers; i++)
            Assert.Null(_hub.JoinWithCode(session!.Code, new HideSeekParty($"invite-{i}", $"Joueur {i}")).Error);
        Assert.Equal("La partie est complète (8 joueurs).", _hub.JoinWithCode(session!.Code, Pierre).Error);
        Assert.Equal((false, "La partie est complète (8 joueurs)."), _hub.Invite(session, "sacha", Ondine));

        _hub.Leave(session, "sacha");
        Assert.Equal(HideSeekStage.Cancelled, session.Stage);
        Assert.Equal("Sacha a annulé la partie.", session.Notice);
        Assert.Null(_hub.Active("sacha"));
    }

    [Fact]
    public void Lobby_Expires()
    {
        var (session, _) = _hub.Create(Sacha, new HideSeekSettings());
        Assert.True(_hub.Invite(session!, "sacha", Ondine).Ok);
        Wait(HideSeekHub.LobbyLifetime);
        Assert.Equal(HideSeekStage.Cancelled, session!.Stage);
        Assert.Equal("La partie n'a pas été lancée à temps : la salle d'attente a fermé.", session.Notice);
        Assert.Empty(_hub.Incoming("ondine"));
    }

    [Fact]
    public void Start_OnlyByTheHost_WithAnotherPlayer_AndTheSameSettings()
    {
        var settings = new HideSeekSettings(Rounds: 2, Count: 20, CountdownSeconds: 7);
        var (session, _) = _hub.Create(Sacha, settings);
        var rounds = Rounds(settings);
        Assert.Equal("Il faut au moins un autre joueur pour lancer la partie.", _hub.Start(session!, "sacha", settings, session!.PlayersNow(), rounds));
        Assert.Null(_hub.JoinWithCode(session.Code, Ondine).Error);
        Assert.Equal("Seul l'hôte lance la partie, depuis la salle d'attente.", _hub.Start(session, "ondine", settings, session.PlayersNow(), rounds));
        // Tirés pour d'autres joueurs (Pierre entré entre-temps) : à refaire
        Assert.Equal("Les joueurs ou les réglages viennent de changer : relance la partie.",
            _hub.Start(session, "sacha", settings, [Sacha], rounds));
        Assert.Equal("Les tours de la partie ne correspondent pas aux réglages.",
            _hub.Start(session, "sacha", settings, session.PlayersNow(), rounds.Take(1).ToList()));
        Assert.True(_hub.Invite(session, "sacha", Pierre).Ok);
        Assert.Null(_hub.Start(session, "sacha", settings, session.PlayersNow(), rounds));
        Assert.Empty(_hub.Incoming("pierre")); // l'invitation restante tombe
        var shown = session.Snapshot("ondine");
        Assert.Equal(HideSeekStage.Playing, shown.Stage);
        Assert.Equal(HideSeekPhase.Countdown, shown.Phase);
        Assert.Equal(_time.Now.UtcDateTime.AddSeconds(7), shown.StartsAt); // le décompte choisi par l'hôte
        Assert.Equal(1, shown.RoundNumber);
        Assert.Null(shown.TargetId); // la cible ne se dit que par son nom et son image
        Assert.NotNull(shown.Target);
    }

    [Fact]
    public void Click_WrongBlocks_RightScores_ThenNextRound()
    {
        var session = Playing();
        var target = TargetOf(session);
        var wrong = WrongId(session);
        var wrongName = session.Snapshot("ondine").Round!.Pokemon.First(p => p.Id == wrong).Name;

        var missed = _hub.Click(session, "ondine", wrong);
        Assert.False(missed.Found);
        Assert.Equal($"Raté ! C'était {wrongName}. Tu es bloqué 3 secondes.", missed.Message);
        Assert.Equal(_time.Now.UtcDateTime + HideSeekTiming.Penalty, session.Snapshot("ondine").BlockedUntil);
        // Bloquée : même la cible ne compte pas
        Assert.Equal("Tu es bloqué après ton mauvais clic : attends un peu.", _hub.Click(session, "ondine", target).Message);
        Assert.False(_hub.Click(session, "ondine", target).Found);
        _time.Now += HideSeekTiming.Penalty;

        var found = _hub.Click(session, "pierre", target);
        Assert.True(found.Found);
        var shown = session.Snapshot("sacha");
        Assert.Equal(HideSeekPhase.Reveal, shown.Phase);
        Assert.Equal(target, shown.TargetId);
        Assert.Equal("Pierre", shown.FoundBy);
        Assert.Equal(1, shown.Players.Single(p => p.UserId == "pierre").Found);
        Assert.Null(_hub.Click(session, "ondine", target).Message); // trop tard : rien
        Assert.Equal(1, session.Snapshot("sacha").Players.Sum(p => p.Found));

        Wait(HideSeekTiming.Reveal);
        shown = session.Snapshot("sacha");
        Assert.Equal(2, shown.RoundNumber);
        Assert.Equal(HideSeekPhase.Countdown, shown.Phase);
        Assert.Null(shown.FoundBy);
        Assert.Equal("Attends le début du tour.", _hub.Click(session, "sacha", TargetOf(session)).Message);
    }

    [Fact]
    public async Task Game_TimeoutThenEnd_ResultsRecorded()
    {
        var session = Playing();
        HideSeekSession? finished = null;
        _hub.Finished += s => finished = s;

        // 1er tour : Ondine trouve
        Assert.True(_hub.Click(session, "ondine", TargetOf(session)).Found);
        Wait(HideSeekTiming.Reveal);
        Wait(TimeSpan.FromSeconds(HideSeekSettings.DefaultCountdown));

        // 2e tour : personne ne trouve avant la fin du temps
        Wait(TimeSpan.FromSeconds(30));
        var shown = session.Snapshot("sacha");
        Assert.Equal(HideSeekPhase.Reveal, shown.Phase);
        Assert.Null(shown.FoundBy);
        Assert.NotNull(shown.TargetId);
        Assert.Null(finished);
        Wait(HideSeekTiming.Reveal);

        Assert.Equal(HideSeekStage.Done, session.Stage);
        Assert.Same(session, finished);
        Assert.Null(_hub.Active("sacha"));
        Assert.Equal(MiniGameOutcome.Won, HideSeekHub.OutcomeOf(session, "ondine"));
        Assert.Equal(MiniGameOutcome.Lost, HideSeekHub.OutcomeOf(session, "sacha"));

        await HideSeekRecorder.RecordAsync(_db.Factory, session, DateTime.UtcNow);
        await using (var db = _db.NewContext())
        {
            var rows = await db.HideSeekRecords.AsNoTracking().OrderBy(r => r.UserId).ToListAsync();
            Assert.Equal(["ondine", "pierre", "sacha"], rows.Select(r => r.UserId));
            Assert.All(rows, r => Assert.Equal(session.Id, r.GameId));
            Assert.All(rows, r => Assert.Equal(2, r.Rounds));
            Assert.Equal([1, 0, 0], rows.Select(r => r.Found));
            Assert.Equal(["Won", "Lost", "Lost"], rows.Select(r => r.Outcome));
        }
        var plays = await new HideSeekService(_db.Factory).PlaysAsync("ondine");
        var play = Assert.Single(plays);
        Assert.Equal(1, play.Found);
        Assert.Equal(["pierre", "sacha"], play.Play.Opponents.Order());
        var stats = HideSeekStats.From(plays);
        Assert.Equal((1, 0, 0, 2, 1), (stats.Games.Won, stats.Games.Lost, stats.Games.Draws, stats.Games.Players, stats.Found));
    }

    [Fact]
    public void Outcome_TiedLeadersDraw_NobodyFoundIsADraw()
    {
        var session = Playing(new HideSeekSettings(Rounds: 1, RoundSeconds: 15, Count: 20));
        Wait(TimeSpan.FromSeconds(15));
        Wait(HideSeekTiming.Reveal);
        Assert.Equal(HideSeekStage.Done, session.Stage);
        Assert.All(["sacha", "ondine", "pierre"], id => Assert.Equal(MiniGameOutcome.Draw, HideSeekHub.OutcomeOf(session, id)));
    }

    [Fact]
    public void Leave_PlayerLoses_LastOneStanding_HostStopsTheGame()
    {
        var session = Playing();
        _hub.Leave(session, "pierre");
        Assert.Equal(HideSeekStage.Playing, session.Stage);
        Assert.Equal("Pierre a quitté la partie.", session.Notice);
        Assert.True(session.Snapshot("sacha").Players.Single(p => p.UserId == "pierre").Left);
        Assert.Null(_hub.Active("pierre"));
        Assert.Null(_hub.Click(session, "pierre", TargetOf(session)).Message); // il ne joue plus

        // Ondine part aussi : Sacha reste seul, la partie se termine et il gagne
        HideSeekSession? finished = null;
        _hub.Finished += s => finished = s;
        _hub.Leave(session, "ondine");
        Assert.Equal(HideSeekStage.Done, session.Stage);
        Assert.Same(session, finished);
        Assert.Equal("Ondine a quitté la partie. Il ne reste qu'un joueur : la partie s'arrête.", session.Notice);
        Assert.Equal(MiniGameOutcome.Won, HideSeekHub.OutcomeOf(session, "sacha"));
        Assert.Equal(MiniGameOutcome.Lost, HideSeekHub.OutcomeOf(session, "ondine"));

        // L'hôte qui part arrête la partie, sans résultat
        var other = Playing();
        _hub.Leave(other, "sacha");
        Assert.Equal(HideSeekStage.Cancelled, other.Stage);
        Assert.Equal("Sacha, l'hôte, a quitté la partie : elle s'arrête.", other.Notice);
    }

    [Fact]
    public void Rematch_SameSettings_OthersInvited()
    {
        var settings = new HideSeekSettings(Rounds: 1, RoundSeconds: 15, Count: 20, Landscape: ParkLandscape.Forest);
        var session = Playing(settings);
        Assert.NotNull(_hub.Rematch(session, "sacha").Message); // pas encore finie
        Assert.Null(_hub.Rematch(session, "sacha").Session);
        Wait(TimeSpan.FromSeconds(15));
        Wait(HideSeekTiming.Reveal);

        Assert.Null(_hub.Rematch(session, "ondine").Session); // seul l'hôte
        var (again, message) = _hub.Rematch(session, "sacha");
        Assert.NotNull(again);
        Assert.Equal("Nouvelle partie créée avec les mêmes réglages : Ondine, Pierre sont invités.", message);
        Assert.Equal(settings, again.Snapshot("sacha").Settings);
        Assert.Same(again, _hub.RematchOffer(session, "ondine"));
        Assert.Null(_hub.Accept(again, "ondine"));
        Assert.Null(_hub.RematchOffer(session, "ondine"));
        Assert.Equal(["Sacha", "Ondine"], again.Snapshot("sacha").Players.Select(p => p.Pseudo));
        Assert.Equal(session.Id, again.RematchOf);
    }
}
