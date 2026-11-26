using System.Security.Cryptography;

namespace MonsutaPotaru.Services;

/// <summary>Étape d'une partie de cache-cache : salle d'attente, tours, fin (ou annulée).</summary>
public enum HideSeekStage { Lobby, Playing, Done, Cancelled }

/// <summary>
/// Moment d'un tour : le décompte (la cible annoncée, le parc caché le temps que chacun le charge :
/// <see cref="HideSeekSettings.CountdownSeconds"/>), on cherche, puis on montre la cible (qui l'a trouvée, ou personne)
/// avant le tour suivant.
/// </summary>
public enum HideSeekPhase { Countdown, Searching, Reveal }

/// <summary>Un joueur : compte (ou identifiant d'invité sans compte, <see cref="ChessHub.NewGuestId"/>) et pseudo.</summary>
public sealed record HideSeekParty(string UserId, string Pseudo)
{
    public bool IsGuest => MiniGamePlay.AccountOrNull(UserId) is null;
}

/// <summary>Rythme des tours : pénalité d'un mauvais clic, cible montrée à la fin du tour (le décompte est un réglage).</summary>
public static class HideSeekTiming
{
    public static readonly TimeSpan Penalty = TimeSpan.FromSeconds(3);
    public static readonly TimeSpan Reveal = TimeSpan.FromSeconds(5);
}

/// <summary>Un joueur vu par les pages : pseudo, Pokémon trouvés, parti en cours de route.</summary>
public sealed record HideSeekScore(string UserId, string Pseudo, int Found, bool Left, bool IsHost);

/// <summary>Résultat d'un clic sur un Pokémon : trouvé, raté (et jusqu'à quand le joueur est bloqué), ou refusé (message).</summary>
public sealed record HideSeekClick(bool Found, string? Message, DateTime? BlockedUntil = null)
{
    public static HideSeekClick Ignored(string? message = null) => new(false, message);
}

/// <summary>
/// Ce qu'une page montre d'une partie, à un instant (copie : la partie change sur d'autres fils). Pendant la recherche, la
/// cible n'est connue que par son nom et son image (<see cref="Target"/>), pas par son numéro (<see cref="TargetId"/> : à la
/// fin du tour seulement).
/// </summary>
public sealed record HideSeekSnapshot(
    HideSeekStage Stage, HideSeekSettings Settings, IReadOnlyList<HideSeekScore> Players, IReadOnlyList<HideSeekParty> Invited,
    int RoundNumber, HideSeekPhase Phase, HideSeekRound? Round, HideSeekMon? Target, int? TargetId, string? FoundBy,
    DateTime? StartsAt, DateTime? Deadline, DateTime? RevealUntil, DateTime? BlockedUntil, string? Notice);

/// <summary>
/// Une partie (<see cref="HideSeekHub"/>) : l'hôte choisit les réglages dans la salle d'attente, invite des amis ou donne
/// le code (8 caractères, <see cref="ChessHub.CodeAlphabet"/>) à d'autres joueurs, avec ou sans compte, puis la lance. Les
/// pages s'abonnent à <see cref="Changed"/> et lisent la partie par <see cref="Snapshot"/> (verrou : les autres joueurs et
/// le minuteur la changent sur d'autres fils).
/// </summary>
public sealed class HideSeekSession(HideSeekParty host, HideSeekSettings settings, DateTime createdAt, string code)
{
    internal readonly object Gate = new();

    public Guid Id { get; } = Guid.NewGuid();
    public HideSeekParty Host { get; } = host;
    public string Code { get; } = code;
    public DateTime CreatedAt { get; } = createdAt;
    public DateTime ExpiresAt => CreatedAt + HideSeekHub.LobbyLifetime;
    /// <summary>Partie finie dont celle-ci est la suite (« Recommencer »), null sinon.</summary>
    public Guid? RematchOf { get; internal init; }

    public HideSeekStage Stage { get; internal set; }
    public bool IsOver => Stage is HideSeekStage.Done or HideSeekStage.Cancelled;
    /// <summary>Dernier événement à montrer à tous (annulation, hôte parti, joueur parti).</summary>
    public string? Notice { get; internal set; }

    internal HideSeekSettings Settings = settings;
    internal readonly List<HideSeekParty> Players = [host];
    internal readonly List<HideSeekParty> Invited = [];
    internal readonly HashSet<string> Left = [];
    internal readonly Dictionary<string, int> Found = [];
    internal readonly Dictionary<string, DateTime> Blocked = [];
    internal IReadOnlyList<HideSeekRound> Rounds = [];
    internal int RoundIndex = -1;
    internal HideSeekPhase Phase;
    internal DateTime? StartsAt, Deadline, RevealUntil;
    internal string? FoundBy;

    public bool IsHost(string userId) => Host.UserId == userId;
    public bool IsPlayer(string userId) => Players.Any(p => p.UserId == userId);

    /// <summary>Joueurs encore là (pas partis en cours de route).</summary>
    internal IEnumerable<HideSeekParty> Active => Players.Where(p => !Left.Contains(p.UserId));

    internal HideSeekRound? Round => RoundIndex >= 0 && RoundIndex < Rounds.Count ? Rounds[RoundIndex] : null;

    /// <summary>Le pseudo d'un joueur (ou d'un invité), vide s'il n'en est pas.</summary>
    public string PseudoOf(string userId)
    {
        lock (Gate)
            return Players.Concat(Invited).FirstOrDefault(p => p.UserId == userId)?.Pseudo ?? "";
    }

    /// <summary>Joueurs qui ont un compte, encore là : leurs collections (choix « tous les joueurs »).</summary>
    public IReadOnlyList<HideSeekParty> PlayersNow()
    {
        lock (Gate)
            return [.. Active];
    }

    public HideSeekSnapshot Snapshot(string userId)
    {
        lock (Gate)
        {
            var round = Stage == HideSeekStage.Lobby ? null : Round;
            var revealed = Phase == HideSeekPhase.Reveal || IsOver;
            var scores = Players.Select(p => new HideSeekScore(p.UserId, p.Pseudo, Found.GetValueOrDefault(p.UserId), Left.Contains(p.UserId), IsHost(p.UserId)))
                .ToList();
            return new HideSeekSnapshot(Stage, Settings, scores, [.. Invited], RoundIndex + 1, Phase, round, round?.Target,
                revealed ? round?.TargetId : null, FoundBy is { } f ? Players.First(p => p.UserId == f).Pseudo : null,
                StartsAt, Deadline, RevealUntil, Blocked.TryGetValue(userId, out var until) ? until : null, Notice);
        }
    }

    /// <summary>Quelque chose a changé : les pages se redessinent.</summary>
    public event Action? Changed;
    internal void Notify() => Changed?.Invoke();
}

/// <summary>
/// Parties de cache-cache en direct (Mini-jeux → Cache-cache), en mémoire : un redémarrage du serveur les arrête. De 2 à
/// <see cref="MaxPlayers"/> joueurs ; un joueur ne joue qu'une partie à la fois. À chaque tour, un parc et ses Pokémon
/// (tirés par <see cref="HideSeekDraw"/>) : le premier qui clique sur la cible gagne le tour ; un mauvais clic bloque le
/// joueur <see cref="HideSeekTiming.Penalty"/> ; le temps écoulé sans que personne ne trouve, personne ne marque. Celui qui a
/// trouvé le plus de cibles gagne la partie (à égalité en tête : nulle pour ceux-là).
/// </summary>
public sealed class HideSeekHub : IDisposable
{
    public const int MaxPlayers = 8;

    /// <summary>Durée d'une salle d'attente (invitations et code), tant que l'hôte reste sur la page.</summary>
    public static readonly TimeSpan LobbyLifetime = TimeSpan.FromMinutes(15);

    private const string AlreadyPlaying = "Tu as déjà une partie de cache-cache en cours.";
    private const string Full = "La partie est complète (8 joueurs).";

    private readonly Lock gate = new();
    private readonly List<HideSeekSession> sessions = [];
    private readonly Timer? timer;
    private readonly TimeProvider time;

    public HideSeekHub() : this(TimeProvider.System) { }

    /// <param name="ticking">Faux pour les tests : <see cref="Tick"/> est alors appelé à la main.</param>
    public HideSeekHub(TimeProvider time, bool ticking = true)
    {
        this.time = time;
        if (ticking)
            timer = new Timer(_ => Tick(), null, TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(250));
    }

    /// <summary>Invitations reçues ou retirées pour ce compte (pastille du menu, liste de la page).</summary>
    public event Action<string>? RequestsChanged;

    /// <summary>Partie terminée (tous les tours joués, ou plus qu'un joueur) : pour les statistiques.</summary>
    public event Action<HideSeekSession>? Finished;

    private DateTime Now => time.GetUtcNow().UtcDateTime;

    /// <summary>L'heure du hub (UTC) : les pages comptent les secondes restantes avec elle.</summary>
    public DateTime UtcNow => Now;

    /// <summary>Partie en cours de ce joueur (salle d'attente comprise), s'il en a une.</summary>
    public HideSeekSession? Active(string userId)
    {
        lock (gate)
            return ActiveLocked(userId);
    }

    private HideSeekSession? ActiveLocked(string userId) =>
        sessions.FirstOrDefault(s => !s.IsOver && s.IsPlayer(userId) && !s.Left.Contains(userId));

    /// <summary>Invitations reçues, en attente de réponse.</summary>
    public List<HideSeekSession> Incoming(string userId)
    {
        lock (gate)
            return sessions.Where(s => s.Stage == HideSeekStage.Lobby && s.Invited.Any(p => p.UserId == userId)).ToList();
    }

    /// <summary>Crée une salle d'attente : l'hôte y règle la partie, invite des amis ou donne le code.</summary>
    public (HideSeekSession? Session, string? Error) Create(HideSeekParty host, HideSeekSettings settings)
    {
        if (settings.Error is { } error)
            return (null, error);
        HideSeekSession session;
        lock (gate)
        {
            if (ActiveLocked(host.UserId) is not null)
                return (null, AlreadyPlaying);
            session = new HideSeekSession(host, settings, Now, NewCode());
            sessions.Add(session);
        }
        return (session, null);
    }

    private string NewCode()
    {
        string code;
        do
            code = RandomNumberGenerator.GetString(ChessHub.CodeAlphabet, ChessHub.CodeLength);
        while (sessions.Any(s => s.Code == code));
        return code;
    }

    /// <summary>L'hôte change les réglages (venus du navigateur : revérifiés), dans la salle d'attente. Message d'erreur, ou null.</summary>
    public static string? UpdateSettings(HideSeekSession session, string userId, HideSeekSettings settings)
    {
        if (settings.Error is { } error)
            return error;
        lock (session.Gate)
        {
            if (session.Stage != HideSeekStage.Lobby || !session.IsHost(userId))
                return "Seul l'hôte règle la partie, avant de la lancer.";
            session.Settings = settings;
        }
        session.Notify();
        return null;
    }

    /// <summary>
    /// L'hôte invite un ami (l'appelant a vérifié l'amitié : la page ne propose que ses amis). Message affiché à l'hôte.
    /// </summary>
    public (bool Ok, string Message) Invite(HideSeekSession session, string userId, HideSeekParty friend)
    {
        lock (gate)
        {
            lock (session.Gate)
            {
                if (session.Stage != HideSeekStage.Lobby || !session.IsHost(userId))
                    return (false, "Seul l'hôte invite, avant de lancer la partie.");
                if (session.IsPlayer(friend.UserId))
                    return (false, $"{friend.Pseudo} est déjà dans la partie.");
                if (session.Invited.Any(p => p.UserId == friend.UserId))
                    return (false, $"{friend.Pseudo} est déjà invité.");
                if (session.Players.Count + session.Invited.Count >= MaxPlayers)
                    return (false, Full);
                session.Invited.Add(friend);
            }
        }
        session.Notify();
        RequestsChanged?.Invoke(friend.UserId);
        return (true, $"Invitation envoyée à {friend.Pseudo}.");
    }

    /// <summary>Accepte une invitation : le joueur entre dans la salle d'attente.</summary>
    public string? Accept(HideSeekSession session, string userId)
    {
        lock (gate)
        {
            lock (session.Gate)
            {
                var invited = session.Invited.FirstOrDefault(p => p.UserId == userId);
                if (session.Stage != HideSeekStage.Lobby || invited is null)
                    return "Cette invitation n'est plus valable.";
                if (ActiveLocked(userId) is not null)
                    return AlreadyPlaying;
                if (session.Players.Count >= MaxPlayers)
                    return Full;
                session.Invited.Remove(invited);
                session.Players.Add(invited);
            }
        }
        session.Notify();
        RequestsChanged?.Invoke(userId);
        return null;
    }

    /// <summary>
    /// Entre dans la salle d'attente de ce code (saisi dans le navigateur : l'appelant limite les essais par adresse IP,
    /// <see cref="AccountLimiter.TryChessCode"/>), avec ou sans compte.
    /// </summary>
    public (HideSeekSession? Session, string? Error) JoinWithCode(string? code, HideSeekParty player)
    {
        var key = ChessHub.NormalizeCode(code);
        if (key.Length != ChessHub.CodeLength)
            return (null, $"Le code a {ChessHub.CodeLength} caractères : vérifie-le.");
        HideSeekSession? found;
        var wasInvited = false;
        lock (gate)
        {
            found = sessions.FirstOrDefault(s => s.Stage == HideSeekStage.Lobby && s.Code == key);
            if (found is null)
                return (null, "Aucune partie n'attend avec ce code : vérifie-le, ou demande un nouveau code.");
            if (found.IsPlayer(player.UserId))
                return (null, found.IsHost(player.UserId) ? "C'est ta propre partie : donne ce code aux autres joueurs." : "Tu es déjà dans cette partie.");
            if (ActiveLocked(player.UserId) is not null)
                return (null, AlreadyPlaying);
            lock (found.Gate)
            {
                if (found.Players.Count >= MaxPlayers)
                    return (null, Full);
                wasInvited = found.Invited.RemoveAll(p => p.UserId == player.UserId) > 0;
                found.Players.Add(player);
            }
        }
        found.Notify();
        if (wasInvited)
            RequestsChanged?.Invoke(player.UserId);
        return (found, null);
    }

    /// <summary>
    /// L'hôte lance la partie avec ces tours (tirés par la page avec les joueurs <paramref name="players"/> et les
    /// réglages <paramref name="settings"/> : refusé s'ils ont changé entre-temps). Les invitations restantes tombent.
    /// </summary>
    public string? Start(HideSeekSession session, string userId, HideSeekSettings settings, IReadOnlyList<HideSeekParty> players,
        IReadOnlyList<HideSeekRound> rounds)
    {
        List<HideSeekParty> dropped;
        lock (gate)
        {
            lock (session.Gate)
            {
                if (session.Stage != HideSeekStage.Lobby || !session.IsHost(userId))
                    return "Seul l'hôte lance la partie, depuis la salle d'attente.";
                if (session.Players.Count < 2)
                    return "Il faut au moins un autre joueur pour lancer la partie.";
                if (session.Settings != settings || !session.Players.Select(p => p.UserId).SequenceEqual(players.Select(p => p.UserId)))
                    return "Les joueurs ou les réglages viennent de changer : relance la partie.";
                if (rounds.Count != settings.Rounds)
                    return "Les tours de la partie ne correspondent pas aux réglages.";
                dropped = [.. session.Invited];
                session.Invited.Clear();
                session.Rounds = rounds;
                session.Stage = HideSeekStage.Playing;
                session.Notice = null;
                foreach (var p in session.Players)
                    session.Found[p.UserId] = 0;
                BeginRound(session, 0);
            }
        }
        session.Notify();
        foreach (var p in dropped)
            RequestsChanged?.Invoke(p.UserId);
        return null;
    }

    /// <summary>Tour n° <paramref name="index"/> (à partir de 0) : quelques secondes de préparation, puis la recherche.</summary>
    private void BeginRound(HideSeekSession session, int index)
    {
        session.RoundIndex = index;
        session.Phase = HideSeekPhase.Countdown;
        session.FoundBy = null;
        session.Blocked.Clear();
        session.StartsAt = Now + TimeSpan.FromSeconds(session.Settings.CountdownSeconds);
        session.Deadline = session.StartsAt + TimeSpan.FromSeconds(session.Settings.RoundSeconds);
        session.RevealUntil = null;
    }

    /// <summary>
    /// Clic d'un joueur sur un Pokémon du parc (numéro venu du navigateur : comparé à la cible ici). Le premier qui trouve
    /// gagne le tour ; un mauvais clic le bloque <see cref="HideSeekTiming.Penalty"/>.
    /// </summary>
    public HideSeekClick Click(HideSeekSession session, string userId, int pokemonId)
    {
        HideSeekClick result;
        lock (session.Gate)
        {
            var now = Now;
            if (session.Stage != HideSeekStage.Playing || session.Round is not { } round || !session.IsPlayer(userId) || session.Left.Contains(userId))
                return HideSeekClick.Ignored();
            if (session.Phase == HideSeekPhase.Countdown && now < session.StartsAt)
                return HideSeekClick.Ignored("Attends le début du tour.");
            if (session.Phase == HideSeekPhase.Reveal || now >= session.Deadline)
                return HideSeekClick.Ignored();
            if (session.Blocked.TryGetValue(userId, out var until) && until > now)
                return new HideSeekClick(false, "Tu es bloqué après ton mauvais clic : attends un peu.", until);
            session.Phase = HideSeekPhase.Searching;
            if (pokemonId == round.TargetId)
            {
                session.Found[userId] = session.Found.GetValueOrDefault(userId) + 1;
                session.FoundBy = userId;
                Reveal(session, now);
                result = new HideSeekClick(true, $"Trouvé ! C'était bien {round.Target.Name}.");
            }
            else
            {
                var clicked = round.Pokemon.FirstOrDefault(p => p.Id == pokemonId);
                var blocked = now + HideSeekTiming.Penalty;
                session.Blocked[userId] = blocked;
                result = new HideSeekClick(false, clicked is null
                    ? "Raté !"
                    : $"Raté ! C'était {clicked.Name}. Tu es bloqué {HideSeekTiming.Penalty.TotalSeconds:0} secondes.", blocked);
            }
        }
        session.Notify();
        return result;
    }

    /// <summary>Fin de la recherche du tour (trouvée ou temps écoulé) : la cible est montrée à tous.</summary>
    private static void Reveal(HideSeekSession session, DateTime now)
    {
        session.Phase = HideSeekPhase.Reveal;
        session.RevealUntil = now + HideSeekTiming.Reveal;
        session.Blocked.Clear();
    }

    /// <summary>
    /// Quitte la partie (bouton, page fermée) : dans la salle d'attente, l'hôte l'annule et un invité refuse ; pendant la
    /// partie, l'hôte parti l'arrête (rien n'est compté), un autre joueur en sort (compté perdant) ; s'il ne reste qu'un
    /// joueur, la partie se termine. Sans effet sur une partie finie.
    /// </summary>
    public void Leave(HideSeekSession session, string userId, string? reason = null)
    {
        var finished = false;
        List<string> notified = [];
        lock (gate)
        {
            lock (session.Gate)
            {
                if (session.IsOver)
                    return;
                var pseudo = session.PseudoOf(userId);
                var invited = session.Invited.FirstOrDefault(p => p.UserId == userId);
                if (invited is not null)
                {
                    session.Invited.Remove(invited);
                    notified.Add(userId);
                }
                else if (!session.IsPlayer(userId) || session.Left.Contains(userId))
                {
                    return;
                }
                else if (session.IsHost(userId))
                {
                    session.Notice = reason ?? (session.Stage == HideSeekStage.Lobby
                        ? $"{pseudo} a annulé la partie."
                        : $"{pseudo}, l'hôte, a quitté la partie : elle s'arrête.");
                    session.Stage = HideSeekStage.Cancelled;
                    notified.AddRange(session.Invited.Select(p => p.UserId));
                    session.Invited.Clear();
                    sessions.Remove(session);
                }
                else if (session.Stage == HideSeekStage.Lobby)
                {
                    session.Players.RemoveAll(p => p.UserId == userId);
                }
                else
                {
                    session.Left.Add(userId);
                    session.Blocked.Remove(userId);
                    session.Notice = reason ?? $"{pseudo} a quitté la partie.";
                    if (session.Active.Count() < 2)
                    {
                        session.Notice += " Il ne reste qu'un joueur : la partie s'arrête.";
                        End(session);
                        finished = true;
                    }
                }
            }
        }
        if (finished)
            Finished?.Invoke(session);
        session.Notify();
        foreach (var user in notified)
            RequestsChanged?.Invoke(user);
    }

    private void End(HideSeekSession session)
    {
        session.Stage = HideSeekStage.Done;
        session.Phase = HideSeekPhase.Reveal;
        session.Deadline = null;
        session.RevealUntil = null;
        sessions.Remove(session); // les pages gardent leur référence pour l'écran de fin
    }

    /// <summary>
    /// « Recommencer » (l'hôte d'une partie finie) : une nouvelle salle d'attente, mêmes réglages, où les autres joueurs
    /// encore là sont invités (avec ou sans compte : ils voient la proposition sur leur écran de fin).
    /// </summary>
    public (HideSeekSession? Session, string Message) Rematch(HideSeekSession previous, string userId)
    {
        HideSeekSession session;
        List<HideSeekParty> others;
        lock (gate)
        {
            if (previous.Stage != HideSeekStage.Done || !previous.IsHost(userId))
                return (null, "Seul l'hôte peut recommencer une partie finie : crée une nouvelle partie.");
            if (ActiveLocked(userId) is not null)
                return (null, AlreadyPlaying);
            lock (previous.Gate)
                others = previous.Active.Where(p => p.UserId != userId).ToList();
            session = new HideSeekSession(previous.Host, previous.Settings, Now, NewCode()) { RematchOf = previous.Id };
            session.Invited.AddRange(others);
            sessions.Add(session);
        }
        foreach (var p in others)
            RequestsChanged?.Invoke(p.UserId);
        previous.Notify(); // les autres écrans de fin voient la proposition
        if (others.Count == 0)
            return (session, "Nouvelle partie créée avec les mêmes réglages.");
        var invited = others.Count == 1 ? "est invité" : "sont invités";
        return (session, $"Nouvelle partie créée avec les mêmes réglages : {string.Join(", ", others.Select(p => p.Pseudo))} {invited}.");
    }

    /// <summary>Proposition de l'hôte de recommencer cette partie finie, adressée à ce joueur et pas encore acceptée.</summary>
    public HideSeekSession? RematchOffer(HideSeekSession previous, string userId)
    {
        lock (gate)
            return sessions.FirstOrDefault(s => s.Stage == HideSeekStage.Lobby && s.RematchOf == previous.Id && s.Invited.Any(p => p.UserId == userId));
    }

    /// <summary>
    /// Plusieurs fois par seconde : salles d'attente expirées, début de la recherche, temps écoulé (personne ne marque),
    /// tour suivant ou fin de la partie.
    /// </summary>
    public void Tick()
    {
        var now = Now;
        List<HideSeekSession> changed = [];
        List<HideSeekSession> finished = [];
        List<string> notified = [];
        lock (gate)
        {
            foreach (var s in sessions.ToList())
            {
                lock (s.Gate)
                {
                    if (s.Stage == HideSeekStage.Lobby && s.ExpiresAt <= now)
                    {
                        s.Stage = HideSeekStage.Cancelled;
                        s.Notice = "La partie n'a pas été lancée à temps : la salle d'attente a fermé.";
                        notified.AddRange(s.Invited.Select(p => p.UserId));
                        s.Invited.Clear();
                        sessions.Remove(s);
                        changed.Add(s);
                    }
                    else if (s.Stage == HideSeekStage.Playing && Advance(s, now) is { } step)
                    {
                        changed.Add(s);
                        if (step)
                            finished.Add(s);
                    }
                }
            }
        }
        foreach (var s in finished)
            Finished?.Invoke(s);
        foreach (var s in changed)
            s.Notify();
        foreach (var user in notified)
            RequestsChanged?.Invoke(user);
    }

    /// <summary>Fait avancer le tour en cours ; null : rien n'a changé, vrai : la partie vient de finir.</summary>
    private bool? Advance(HideSeekSession s, DateTime now)
    {
        switch (s.Phase)
        {
            case HideSeekPhase.Countdown when now >= s.StartsAt:
                s.Phase = HideSeekPhase.Searching;
                return false;
            case HideSeekPhase.Searching when now >= s.Deadline:
                Reveal(s, now); // personne n'a trouvé : égalité pour ce tour
                return false;
            case HideSeekPhase.Reveal when now >= s.RevealUntil:
                if (s.RoundIndex + 1 < s.Rounds.Count)
                {
                    BeginRound(s, s.RoundIndex + 1);
                    return false;
                }
                End(s);
                return true;
            default:
                return null;
        }
    }

    /// <summary>
    /// Résultat d'un joueur à la fin : les joueurs partis en cours de route perdent ; parmi les autres, celui qui a trouvé
    /// le plus de cibles gagne, plusieurs à égalité en tête font nulle (et personne n'a rien trouvé : nulle pour tous).
    /// </summary>
    public static MiniGameOutcome OutcomeOf(HideSeekSession session, string userId)
    {
        lock (session.Gate)
        {
            if (session.Left.Contains(userId))
                return MiniGameOutcome.Lost;
            var scores = session.Active.Select(p => session.Found.GetValueOrDefault(p.UserId)).ToList();
            var best = scores.Max();
            var mine = session.Found.GetValueOrDefault(userId);
            if (mine < best)
                return MiniGameOutcome.Lost;
            return scores.Count(s => s == best) > 1 ? MiniGameOutcome.Draw : MiniGameOutcome.Won;
        }
    }

    public void Dispose() => timer?.Dispose();
}
