using MonsutaPotaru.Services;

namespace MonsutaPotaru.Tests;

/// <summary>Échanges en direct : demande, acceptation, choix, validation (refus et double accord), annulation, expiration.</summary>
public sealed class TradeHubTests : IDisposable
{
    private sealed class ManualTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private readonly ManualTime _time = new();
    private readonly TradeHub _hub;
    private static readonly TradeParty Sacha = new("sacha", "Sacha", 1, "Noire 2 — Sacha");
    private static readonly TradeParty Ondine = new("ondine", "Ondine", 2, "Blanche 2 — Ondine");
    private static readonly TradeParty Pierre = new("pierre", "Pierre", 3, "Noire — Pierre");

    public TradeHubTests() => _hub = new TradeHub(_time);

    public void Dispose() => _hub.Dispose();

    private static TradeCandidate Pokemon(ushort species) =>
        new(new TradeSlot(0, species), $"empreinte-{species}", species, $"n°{species}", $"n°{species}", 10, false, false, "", "Boîte 1",
            new PokemonFilterTraits(species, false, $"n°{species}", $"n°{species}", 10, 0, false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, FormKind.Standard, false));

    private TradeSession Started()
    {
        var (session, _) = _hub.Request(Sacha, Ondine);
        Assert.NotNull(session);
        Assert.Null(_hub.Accept(session, "ondine"));
        return session;
    }

    [Fact]
    public void Request_IsSeenByTheOther_UntilAccepted()
    {
        var changed = new List<string>();
        _hub.RequestsChanged += changed.Add;
        var (session, message) = _hub.Request(Sacha, Ondine);
        Assert.Contains("Ondine", message);
        Assert.Same(session, Assert.Single(_hub.Incoming("ondine")));
        Assert.Same(session, _hub.Active("sacha"));
        Assert.Null(_hub.Active("ondine")); // une demande reçue n'est pas encore un échange
        Assert.Equal(["ondine"], changed);

        Assert.Null(_hub.Request(Sacha, Pierre).Session); // un échange à la fois
        Assert.NotNull(_hub.Accept(session!, "sacha")); // pas la sienne à accepter
        Assert.Null(_hub.Accept(session!, "ondine"));
        Assert.Equal(TradeStage.Choosing, session!.Stage);
        Assert.Same(session, _hub.Active("ondine"));
        Assert.Empty(_hub.Incoming("ondine"));
        Assert.Null(_hub.Request(Pierre, Ondine).Session); // Ondine échange déjà
    }

    [Fact]
    public void BothChoose_ThenARefusal_GoesBackToChoosing()
    {
        var session = Started();
        _hub.Choose(session, "sacha", Pokemon(120));
        Assert.Equal(TradeStage.Choosing, session.Stage);
        _hub.Choose(session, "ondine", Pokemon(64));
        Assert.Equal(TradeStage.Confirming, session.Stage);
        Assert.Equal(64, session.WantedFromMe("sacha")!.Species);

        Assert.False(_hub.Confirm(session, "sacha", true));
        Assert.True(session.Confirmed("sacha"));
        Assert.False(_hub.Confirm(session, "ondine", false)); // refus
        Assert.Equal(TradeStage.Choosing, session.Stage);
        Assert.Null(session.FirstWants);
        Assert.False(session.Confirmed("sacha"));
        Assert.Contains("Ondine a refusé", session.Notice);
    }

    [Fact]
    public void BothConfirm_TheSecondExecutes_ThenItIsDone()
    {
        var session = Started();
        _hub.Choose(session, "sacha", Pokemon(120));
        _hub.Choose(session, "ondine", Pokemon(64));
        Assert.False(_hub.Confirm(session, "ondine", true));
        Assert.True(_hub.Confirm(session, "sacha", true)); // le second à valider fait l'échange
        Assert.Equal(TradeStage.Trading, session.Stage);
        _hub.Cancel(session, "ondine"); // trop tard : l'écriture est en cours
        Assert.Equal(TradeStage.Trading, session.Stage);

        _hub.Complete(session, new TradeOutcome(true, "reçu Kadabra", "reçu Stari"));
        Assert.Equal(TradeStage.Done, session.Stage);
        Assert.Null(_hub.Active("sacha"));
        Assert.NotNull(_hub.Request(Sacha, Ondine).Session); // un nouvel échange est possible
    }

    [Fact]
    public void FailedTrade_RetryGoesBackToChoosing_OtherwiseCancels()
    {
        var session = Started();
        _hub.Choose(session, "sacha", Pokemon(120));
        _hub.Choose(session, "ondine", Pokemon(64));
        _hub.Confirm(session, "sacha", true);
        _hub.Confirm(session, "ondine", true);
        _hub.Complete(session, TradeOutcome.Failed("a changé"));
        Assert.Equal(TradeStage.Choosing, session.Stage);
        Assert.Equal("a changé", session.Notice);
    }

    [Fact]
    public void Cancel_EndsItForBoth_AndRequestsExpire()
    {
        var session = Started();
        var notified = 0;
        session.Changed += () => notified++;
        _hub.Cancel(session, "ondine");
        Assert.Equal(TradeStage.Cancelled, session.Stage);
        Assert.Equal("Ondine a annulé l'échange.", session.Notice);
        Assert.Equal(1, notified);
        Assert.Null(_hub.Active("sacha"));

        var (request, _) = _hub.Request(Sacha, Ondine);
        _time.Now += TradeHub.RequestLifetime - TimeSpan.FromSeconds(1);
        _hub.ExpireRequests();
        Assert.Equal(TradeStage.Requested, request!.Stage);
        _time.Now += TimeSpan.FromSeconds(2);
        _hub.ExpireRequests();
        Assert.Equal(TradeStage.Cancelled, request.Stage);
        Assert.Contains("expiré", request.Notice);
        Assert.Empty(_hub.Incoming("ondine"));

        var (declined, _) = _hub.Request(Sacha, Ondine);
        _hub.Cancel(declined!, "ondine"); // refuser une demande reçue
        Assert.Equal("Ondine a refusé la demande d'échange.", declined!.Notice);
    }

    private TradeSession Finished()
    {
        var session = Started();
        _hub.Choose(session, "sacha", Pokemon(120));
        _hub.Choose(session, "ondine", Pokemon(64));
        _hub.Confirm(session, "sacha", true);
        _hub.Confirm(session, "ondine", true);
        _hub.Complete(session, new TradeOutcome(true, "reçu Kadabra", "reçu Stari"));
        return session;
    }

    [Fact]
    public void Rematch_IsAnOfferToTheSameFriend_SeenOnTheirEndScreen()
    {
        var previous = Finished();
        var changed = new List<string>();
        _hub.RequestsChanged += changed.Add;

        // Ondine propose : c'est elle qui demande (First), avec les mêmes sauvegardes
        var (rematch, message) = _hub.Rematch(previous, Ondine, Sacha);
        Assert.NotNull(rematch);
        Assert.Equal("Demande envoyée à Sacha : en attente de sa réponse.", message);
        Assert.Equal(previous.Id, rematch.RematchOf);
        Assert.Equal(Ondine, rematch.First);
        Assert.Equal(TradeStage.Requested, rematch.Stage);
        Assert.Equal(["sacha"], changed);
        Assert.Same(rematch, _hub.RematchOffer(previous, "sacha")); // sur l'écran de fin de Sacha
        Assert.Null(_hub.RematchOffer(previous, "ondine")); // pas chez celle qui propose
        Assert.Same(rematch, Assert.Single(_hub.Incoming("sacha"))); // et dans ses demandes reçues, s'il est parti

        Assert.Null(_hub.Accept(rematch, "sacha"));
        Assert.Equal(TradeStage.Choosing, rematch.Stage);
        Assert.Null(_hub.RematchOffer(previous, "sacha"));
    }

    [Fact]
    public void Rematch_BothAtOnce_TheSecondAcceptsTheFirst()
    {
        var previous = Finished();
        var (first, _) = _hub.Rematch(previous, Sacha, Ondine);
        var (second, message) = _hub.Rematch(previous, Ondine, Sacha);
        Assert.Same(first, second);
        Assert.Equal("", message);
        Assert.Equal(TradeStage.Choosing, first!.Stage);
        Assert.Same(first, _hub.Active("ondine"));
    }

    [Fact]
    public void Rematch_OnlyAfterAnAcceptedTradeThatIsOver()
    {
        var running = Started();
        Assert.Null(_hub.Rematch(running, Sacha, Ondine).Session); // pas fini
        _hub.Cancel(running, "ondine"); // annulé en cours de route : on peut recommencer
        Assert.True(running.Accepted);
        Assert.NotNull(_hub.Rematch(running, Sacha, Ondine).Session);
        _hub.Cancel(_hub.Active("sacha")!, "sacha");

        var (refused, _) = _hub.Request(Sacha, Ondine);
        _hub.Cancel(refused!, "ondine");
        Assert.False(refused!.Accepted);
        var (none, message) = _hub.Rematch(refused, Sacha, Ondine); // demande refusée : on ne relance pas
        Assert.Null(none);
        Assert.Contains("ne peut pas être recommencé", message);

        var previous = Finished();
        Assert.Null(_hub.Rematch(previous, Pierre, Ondine).Session); // Pierre n'y était pas
        Assert.Null(_hub.Rematch(previous, Sacha, Pierre).Session); // pas avec un autre
    }

    [Fact]
    public void Rematch_RefusedOrBusy_SaysWhy()
    {
        var previous = Finished();
        var (rematch, _) = _hub.Rematch(previous, Sacha, Ondine);
        _hub.Cancel(rematch!, "ondine");
        Assert.Equal("Ondine a refusé la demande d'échange.", rematch!.Notice);
        Assert.Null(_hub.RematchOffer(previous, "ondine"));

        var (other, _) = _hub.Request(Pierre, Ondine);
        _hub.Accept(other!, "ondine");
        var (none, message) = _hub.Rematch(previous, Sacha, Ondine);
        Assert.Null(none);
        Assert.Equal("Ondine est déjà en train d'échanger : réessaie plus tard.", message);
    }
}
