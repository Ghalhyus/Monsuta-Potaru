namespace MonsutaPotaru.Services;

/// <summary>Étape d'une partie d'échecs en direct.</summary>
public enum ChessStage { Requested, TeamChoice, Playing, Done, Cancelled }

/// <summary>Un joueur d'une partie : compte et pseudo.</summary>
public sealed record ChessParty(string UserId, string Pseudo);

/// <summary>
/// Rythme d'une partie : temps par coup (choisi par l'hôte parmi <see cref="MoveSeconds"/>) et durée des animations du
/// plateau (echecs.js, qui reçoit ces valeurs) : le temps du joueur suivant ne court qu'après elles (<see cref="Playback"/>).
/// </summary>
public static class ChessTiming
{
    public static readonly IReadOnlyList<int> MoveSeconds = [30, 60, 120, 300, 600];
    public const int DefaultMoveSeconds = 120;

    /// <summary>Marche d'une case.</summary>
    public static readonly TimeSpan WalkPerSquare = TimeSpan.FromMilliseconds(350);
    /// <summary>Attaque et blessure, puis le Pokémon mangé qui rejoint les K.O.</summary>
    public static readonly TimeSpan Strike = TimeSpan.FromMilliseconds(1600);
    /// <summary>Marge (le réseau, la page qui lance l'animation).</summary>
    public static readonly TimeSpan Margin = TimeSpan.FromSeconds(1);

    public static TimeSpan Playback(ChessStep step) =>
        WalkPerSquare * step.Squares + (step.CapturedId is null ? TimeSpan.Zero : Strike) + Margin;

    /// <summary>« 30 s », « 2 min ».</summary>
    public static string Label(int seconds) => seconds < 60 ? $"{seconds} s" : $"{seconds / 60} min";
}

/// <summary>
/// Une partie entre deux amis connectés (<see cref="ChessHub"/>) : l'hôte (<see cref="Host"/>, qui a invité) joue les
/// blancs, l'invité (<see cref="Guest"/>) les noirs. Les deux pages s'abonnent à <see cref="Changed"/> et lisent la partie
/// par les méthodes de la session (verrou : l'autre joueur et le minuteur la changent sur d'autres fils).
/// </summary>
public sealed class ChessSession(ChessParty host, ChessParty guest, int moveSeconds, DateTime createdAt)
{
    internal readonly object Gate = new();

    public Guid Id { get; } = Guid.NewGuid();
    public ChessParty Host { get; } = host;
    public ChessParty Guest { get; } = guest;
    /// <summary>Temps pour jouer un coup, en secondes (choisi par l'hôte).</summary>
    public int MoveSeconds { get; } = moveSeconds;
    public DateTime CreatedAt { get; } = createdAt;
    public DateTime ExpiresAt => CreatedAt + ChessHub.RequestLifetime;

    public ChessStage Stage { get; internal set; }
    /// <summary>Dernier événement à montrer aux deux (refus, annulation, abandon, temps écoulé).</summary>
    public string? Notice { get; internal set; }

    internal string? HostTeam, GuestTeam;
    internal ChessGame? Game;
    internal DateTime? Deadline;

    public bool IsOver => Stage is ChessStage.Done or ChessStage.Cancelled;
    public bool Involves(string userId) => Host.UserId == userId || Guest.UserId == userId;
    public bool IsHost(string userId) => Host.UserId == userId;
    public ChessParty Me(string userId) => IsHost(userId) ? Host : Guest;
    public ChessParty Other(string userId) => IsHost(userId) ? Guest : Host;
    public ChessColor ColorOf(string userId) => IsHost(userId) ? ChessColor.White : ChessColor.Black;
    public ChessParty Player(ChessColor color) => color == ChessColor.White ? Host : Guest;

    /// <summary>Équipe choisie par ce joueur (null : pas encore).</summary>
    public ChessTeam? TeamOf(string userId)
    {
        lock (Gate)
            return ChessTeams.Find(IsHost(userId) ? HostTeam : GuestTeam);
    }

    /// <summary>Équipe déjà prise par l'autre joueur (grisée pour celui-ci).</summary>
    public ChessTeam? TakenBy(string userId) => TeamOf(Other(userId).UserId);

    /// <summary>Les coups joués, à partir du n° <paramref name="from"/> (la page les anime un à un).</summary>
    public IReadOnlyList<ChessStep> Steps(int from)
    {
        lock (Gate)
            return Game is null || from >= Game.Steps.Count ? [] : [.. Game.Steps.Skip(from)];
    }

    public ChessSnapshot Snapshot(string userId)
    {
        lock (Gate)
        {
            var game = Game;
            var color = ColorOf(userId);
            return new ChessSnapshot(Stage, game?.Steps.Count ?? 0, game?.Turn, game is not null && !game.IsOver && game.Turn == color,
                game?.BonusSquare, Deadline, game?.Winner, game?.End ?? ChessEnd.None, game?.Position(), Notice);
        }
    }

    /// <summary>Toutes les pièces de la partie (numéro → Pokémon), pour le plateau et la fenêtre « Pièces ».</summary>
    public IReadOnlyList<ChessUnit> Units()
    {
        lock (Gate)
            return Game is null ? [] : [.. Game.Units.OrderBy(u => u.Id)];
    }

    /// <summary>Ce que la pièce de cette case peut faire, si c'est à ce joueur de la jouer (sinon rien).</summary>
    public ChessOptions Options(string userId, int square)
    {
        lock (Gate)
        {
            if (Game is not { } game || Stage != ChessStage.Playing || game.Turn != ColorOf(userId) || game.At(square)?.Color != game.Turn)
                return ChessOptions.None;
            return new ChessOptions(game.Targets(square), game.Blocked(square), game.SwapsFrom(square));
        }
    }

    /// <summary>La pièce de cette case (null : case vide, ou partie pas commencée).</summary>
    public ChessUnit? At(int square)
    {
        lock (Gate)
            return Game?.At(square);
    }

    /// <summary>Quelque chose a changé : les pages se redessinent.</summary>
    public event Action? Changed;
    internal void Notify() => Changed?.Invoke();
}

/// <summary>Ce qu'une page montre d'une partie, à un instant (copie : la partie change sur un autre fil).</summary>
public sealed record ChessSnapshot(
    ChessStage Stage, int StepCount, ChessColor? Turn, bool MyTurn, int? BonusSquare, DateTime? Deadline, ChessColor? Winner,
    ChessEnd End, ChessPosition? Position, string? Notice);

/// <summary>Ce que peut faire la pièce choisie : ses cases, les pièces qu'elle ne peut pas manger, ses échanges avec un K.O.</summary>
public sealed record ChessOptions(IReadOnlyList<ChessTarget> Targets, IReadOnlyList<ChessBlocked> Blocked, IReadOnlyList<int> Swaps)
{
    public static readonly ChessOptions None = new([], [], []);
    public bool Any => Targets.Count > 0 || Swaps.Count > 0;
}

/// <summary>
/// Parties d'échecs Pokémon en direct (Mini-jeux → Échecs), en mémoire : un redémarrage du serveur les arrête. Un joueur
/// ne joue qu'une partie à la fois. Une invitation vaut <see cref="RequestLifetime"/>, tant que son auteur reste sur la
/// page ; puis chacun choisit une équipe (pas la même), et la partie commence. Chaque coup est vérifié ici
/// (<see cref="ChessGame"/>) ; un joueur qui ne joue pas dans le temps choisi par l'hôte perd (<see cref="Tick"/>), comme
/// celui qui abandonne ou quitte la page.
/// </summary>
public sealed class ChessHub : IDisposable
{
    public static readonly TimeSpan RequestLifetime = TimeSpan.FromMinutes(10);

    private readonly object gate = new();
    private readonly List<ChessSession> sessions = [];
    private readonly Timer? timer;
    private readonly TimeProvider time;

    public ChessHub() : this(TimeProvider.System) { }

    /// <param name="ticking">Faux pour les tests : <see cref="Tick"/> est alors appelé à la main.</param>
    public ChessHub(TimeProvider time, bool ticking = true)
    {
        this.time = time;
        if (ticking)
            timer = new Timer(_ => Tick(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    /// <summary>Invitations reçues ou annulées pour ce compte (pastille du menu, liste de la page).</summary>
    public event Action<string>? RequestsChanged;

    /// <summary>Partie terminée (victoire, défaite ou nulle) : pour les statistiques.</summary>
    public event Action<ChessSession>? Finished;

    private DateTime Now => time.GetUtcNow().UtcDateTime;

    /// <summary>Partie en cours de ce joueur (invitation envoyée comprise), s'il en a une.</summary>
    public ChessSession? Active(string userId)
    {
        lock (gate)
            return sessions.FirstOrDefault(s => !s.IsOver && s.Involves(userId) && (s.Stage != ChessStage.Requested || s.IsHost(userId)));
    }

    /// <summary>Invitations reçues, en attente de réponse.</summary>
    public List<ChessSession> Incoming(string userId)
    {
        lock (gate)
            return sessions.Where(s => s.Stage == ChessStage.Requested && s.Guest.UserId == userId).ToList();
    }

    private bool Busy(string userId) =>
        sessions.Any(s => !s.IsOver && s.Involves(userId) && (s.Stage != ChessStage.Requested || s.IsHost(userId)));

    /// <summary>
    /// Invite un ami (l'appelant a vérifié l'amitié : la page ne propose que ses amis). Le temps par coup vient du
    /// navigateur : il doit être l'un de <see cref="ChessTiming.MoveSeconds"/>.
    /// </summary>
    public (ChessSession? Session, string Message) Request(ChessParty host, ChessParty guest, int moveSeconds)
    {
        if (!ChessTiming.MoveSeconds.Contains(moveSeconds))
            return (null, "Ce temps par coup n'est pas proposé.");
        if (host.UserId == guest.UserId)
            return (null, "Tu ne peux pas jouer contre toi-même.");
        ChessSession session;
        lock (gate)
        {
            if (Busy(host.UserId))
                return (null, "Tu as déjà une partie en cours.");
            if (sessions.Any(s => !s.IsOver && s.Stage != ChessStage.Requested && s.Involves(guest.UserId)))
                return (null, $"{guest.Pseudo} est déjà en train de jouer : réessaie plus tard.");
            session = new ChessSession(host, guest, moveSeconds, Now);
            sessions.Add(session);
        }
        RequestsChanged?.Invoke(guest.UserId);
        return (session, $"Invitation envoyée à {guest.Pseudo} : en attente de sa réponse.");
    }

    /// <summary>Accepte une invitation reçue : les deux joueurs passent au choix de leur équipe.</summary>
    public string? Accept(ChessSession session, string userId)
    {
        lock (gate)
        {
            if (session.Stage != ChessStage.Requested || session.Guest.UserId != userId)
                return "Cette invitation n'est plus valable.";
            if (Busy(userId))
                return "Tu as déjà une partie en cours.";
            session.Stage = ChessStage.TeamChoice;
            session.Notice = null;
        }
        session.Notify();
        RequestsChanged?.Invoke(userId);
        return null;
    }

    /// <summary>
    /// Équipe choisie par ce joueur (identifiant venu du navigateur : revérifié), définitive ; pas celle de l'autre. Quand
    /// les deux sont choisies, la partie commence : aux blancs (l'hôte) de jouer.
    /// </summary>
    public string? ChooseTeam(ChessSession session, string userId, string teamId)
    {
        if (ChessTeams.Find(teamId) is not { } team)
            return "Cette équipe n'existe pas.";
        lock (session.Gate)
        {
            if (session.Stage != ChessStage.TeamChoice || !session.Involves(userId))
                return "Cette partie n'attend plus d'équipe.";
            var host = session.IsHost(userId);
            if ((host ? session.HostTeam : session.GuestTeam) is not null)
                return "Tu as déjà choisi ton équipe.";
            if ((host ? session.GuestTeam : session.HostTeam) == team.Id)
                return $"{session.Other(userId).Pseudo} a déjà pris l'{team.InSentence} : choisis-en une autre.";
            if (host)
                session.HostTeam = team.Id;
            else
                session.GuestTeam = team.Id;
            if (session.HostTeam is not null && session.GuestTeam is not null)
            {
                session.Game = new ChessGame(ChessTeams.Find(session.HostTeam)!, ChessTeams.Find(session.GuestTeam)!);
                session.Stage = ChessStage.Playing;
                session.Deadline = Now + ChessTiming.Margin + TimeSpan.FromSeconds(session.MoveSeconds);
            }
        }
        session.Notify();
        return null;
    }

    /// <summary>
    /// Ce joueur revient sur son choix d'équipe : possible tant que l'autre n'a pas choisi la sienne (dès que les deux ont
    /// choisi, la partie commence). Message d'erreur, ou null.
    /// </summary>
    public string? ClearTeam(ChessSession session, string userId)
    {
        lock (session.Gate)
        {
            if (!session.Involves(userId))
                return "Cette partie n'attend plus d'équipe.";
            if (session.Stage != ChessStage.TeamChoice)
                return session.Stage == ChessStage.Playing
                    ? $"{session.Other(userId).Pseudo} a choisi son équipe : la partie a commencé, ton équipe ne change plus."
                    : "Cette partie n'attend plus d'équipe.";
            var host = session.IsHost(userId);
            if ((host ? session.HostTeam : session.GuestTeam) is null)
                return "Tu n'as pas encore choisi d'équipe.";
            if (host)
                session.HostTeam = null;
            else
                session.GuestTeam = null;
        }
        session.Notify();
        return null;
    }

    /// <summary>Coup de ce joueur (case de départ et d'arrivée venues du navigateur : vérifié par les règles). Message d'erreur, ou null.</summary>
    public string? Play(ChessSession session, string userId, ChessMove move)
    {
        ChessStep? step;
        lock (session.Gate)
        {
            if (session.Stage != ChessStage.Playing || !session.Involves(userId) || session.Game is not { } game)
                return "La partie n'est pas en cours.";
            if (game.Turn != session.ColorOf(userId))
                return "Ce n'est pas à toi de jouer.";
            step = game.Play(move);
            if (step is null)
                return "Ce coup n'est pas permis.";
            session.Deadline = game.IsOver ? null : Now + ChessTiming.Playback(step) + TimeSpan.FromSeconds(session.MoveSeconds);
            if (game.IsOver)
                session.Stage = ChessStage.Done;
        }
        if (step.End != ChessEnd.None)
            Finish(session);
        session.Notify();
        return null;
    }

    /// <summary>Le joueur abandonne : l'autre gagne.</summary>
    public void Forfeit(ChessSession session, string userId) =>
        ForceLoss(session, userId, ChessEnd.Forfeit, $"{session.Me(userId).Pseudo} abandonne la partie.");

    private void ForceLoss(ChessSession session, string userId, ChessEnd reason, string notice)
    {
        lock (session.Gate)
        {
            if (session.Stage != ChessStage.Playing || !session.Involves(userId) || session.Game is not { } game)
                return;
            game.Resign(session.ColorOf(userId), reason);
            session.Stage = ChessStage.Done;
            session.Deadline = null;
            session.Notice = notice;
        }
        Finish(session);
        session.Notify();
    }

    private void Finish(ChessSession session)
    {
        lock (gate)
            sessions.Remove(session); // les pages gardent leur référence pour la fin de la partie
        Finished?.Invoke(session);
    }

    /// <summary>
    /// Annule (bouton « Annuler », invitation refusée) ou, pendant la partie, abandonne (page quittée, onglet fermé) ; sans
    /// effet sur une partie finie.
    /// </summary>
    public void Leave(ChessSession session, string userId, string? reason = null)
    {
        if (session.Stage == ChessStage.Playing)
        {
            ForceLoss(session, userId, ChessEnd.Left, reason ?? $"{session.Me(userId).Pseudo} a quitté la partie.");
            return;
        }
        lock (gate)
        {
            if (session.IsOver || !session.Involves(userId))
                return;
            var refused = session.Stage == ChessStage.Requested && !session.IsHost(userId);
            session.Stage = ChessStage.Cancelled;
            session.Notice = reason ?? (refused ? $"{session.Me(userId).Pseudo} a refusé la partie." : $"{session.Me(userId).Pseudo} a annulé la partie.");
            sessions.Remove(session);
        }
        session.Notify();
        RequestsChanged?.Invoke(session.Guest.UserId);
    }

    /// <summary>Chaque seconde : invitations expirées, et joueur qui n'a pas joué à temps (défaite).</summary>
    public void Tick()
    {
        var now = Now;
        List<ChessSession> expired;
        List<(ChessSession Session, string UserId)> late = [];
        lock (gate)
        {
            expired = sessions.Where(s => s.Stage == ChessStage.Requested && s.ExpiresAt <= now).ToList();
            foreach (var s in expired)
            {
                s.Stage = ChessStage.Cancelled;
                s.Notice = $"{s.Guest.Pseudo} n'a pas répondu à temps : l'invitation a expiré.";
                sessions.Remove(s);
            }
            foreach (var s in sessions.Where(s => s.Stage == ChessStage.Playing))
            {
                lock (s.Gate)
                {
                    if (s.Deadline <= now && s.Game is { IsOver: false } game)
                        late.Add((s, s.Player(game.Turn).UserId));
                }
            }
        }
        foreach (var s in expired)
        {
            s.Notify();
            RequestsChanged?.Invoke(s.Guest.UserId);
        }
        foreach (var (s, userId) in late)
            ForceLoss(s, userId, ChessEnd.Timeout, $"Temps écoulé ! {s.Me(userId).Pseudo} n'a pas joué à temps et perd la partie.");
    }

    public void Dispose() => timer?.Dispose();
}
