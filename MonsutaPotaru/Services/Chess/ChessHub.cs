using System.Security.Cryptography;

namespace MonsutaPotaru.Services;

/// <summary>Étape d'une partie d'échecs en direct.</summary>
public enum ChessStage { Requested, TeamChoice, Playing, Done, Cancelled }

/// <summary>Un joueur d'une partie : compte (ou identifiant d'invité, <see cref="ChessHub.NewGuestId"/>) et pseudo.</summary>
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
/// Une partie (<see cref="ChessHub"/>) : l'hôte (<see cref="Host"/>, qui a invité un ami ou créé une partie publique) joue
/// les blancs, son adversaire (<see cref="Guest"/>) les noirs. Une partie publique (<see cref="Code"/>) n'a pas encore
/// d'adversaire tant que personne n'a saisi son code. Les deux pages s'abonnent à <see cref="Changed"/> et lisent la partie
/// par les méthodes de la session (verrou : l'autre joueur et le minuteur la changent sur d'autres fils).
/// </summary>
public sealed class ChessSession(ChessParty host, ChessParty guest, int moveSeconds, DateTime createdAt, string? code = null)
{
    internal readonly object Gate = new();

    public Guid Id { get; } = Guid.NewGuid();
    public ChessParty Host { get; } = host;
    /// <summary>L'adversaire ; pour une partie publique, personne (identifiant vide) jusqu'à la saisie du code.</summary>
    public ChessParty Guest { get; internal set; } = guest;
    /// <summary>Code d'une partie publique (8 caractères, <see cref="ChessHub.CodeAlphabet"/>) ; null pour une invitation.</summary>
    public string? Code { get; } = code;
    public bool IsPublic => Code is not null;
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
    public bool Involves(string userId) => userId.Length > 0 && (Host.UserId == userId || Guest.UserId == userId);
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
/// ne joue qu'une partie à la fois. Une invitation (à un ami) ou une partie publique (rejointe par son code, avec ou sans
/// compte) vaut <see cref="RequestLifetime"/>, tant que son auteur reste sur la page ; puis chacun choisit une équipe (pas
/// la même), et la partie commence. Chaque coup est vérifié ici
/// (<see cref="ChessGame"/>) ; un joueur qui ne joue pas dans le temps choisi par l'hôte perd (<see cref="Tick"/>), comme
/// celui qui abandonne ou quitte la page.
/// </summary>
public sealed class ChessHub : IDisposable
{
    public static readonly TimeSpan RequestLifetime = TimeSpan.FromMinutes(10);

    /// <summary>Longueur du code d'une partie publique.</summary>
    public const int CodeLength = 8;

    /// <summary>Caractères des codes : sans 0, O, 1, I ni L, qui se confondent à la lecture (31 caractères : 31⁸ codes).</summary>
    public const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    /// <summary>Longueur permise du prénom d'un invité sans compte.</summary>
    public const int GuestNameMin = 2, GuestNameMax = 20;

    /// <summary>Préfixe des identifiants d'invités sans compte (jamais celui d'un compte : ceux-ci sont des GUID).</summary>
    public const string GuestIdPrefix = "invite-";

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

    /// <summary>Identifiant d'un invité sans compte, tiré par le serveur pour la durée de sa page.</summary>
    public static string NewGuestId() => GuestIdPrefix + Guid.NewGuid().ToString("N");

    /// <summary>Code tel que saisi (minuscules, espaces, tirets) ramené à sa forme : « k7mp-q2xa » → « K7MPQ2XA ».</summary>
    public static string NormalizeCode(string? raw) =>
        new((raw ?? "").Where(c => !char.IsWhiteSpace(c) && c != '-').Select(char.ToUpperInvariant).ToArray());

    /// <summary>Code affiché en deux groupes : « K7MP Q2XA ».</summary>
    public static string FormatCode(string code) => code.Length == CodeLength ? $"{code[..4]} {code[4..]}" : code;

    /// <summary>
    /// Prénom d'un invité sans compte (venu du navigateur) : 2 à 20 caractères, lettres, chiffres, espaces, tirets,
    /// apostrophes et points. Rend le pseudo affiché (« Léo (invité) ») ou un message d'erreur.
    /// </summary>
    public static (string? Pseudo, string? Error) GuestPseudo(string? raw)
    {
        var name = string.Join(' ', (raw ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (name.Length is < GuestNameMin or > GuestNameMax)
            return (null, $"Ton prénom doit faire de {GuestNameMin} à {GuestNameMax} caractères.");
        if (!name.All(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '\'' or '’' or '.'))
            return (null, "Ton prénom ne peut contenir que des lettres, des chiffres, des espaces, des tirets, des apostrophes et des points.");
        return ($"{name} (invité)", null);
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

    /// <summary>
    /// Crée une partie publique : un code de <see cref="CodeLength"/> caractères, à donner à l'adversaire (avec ou sans
    /// compte), qui le saisit dans sa page (<see cref="JoinPublic"/>). Le temps par coup vient du navigateur.
    /// </summary>
    public (ChessSession? Session, string Message) CreatePublic(ChessParty host, int moveSeconds)
    {
        if (!ChessTiming.MoveSeconds.Contains(moveSeconds))
            return (null, "Ce temps par coup n'est pas proposé.");
        ChessSession session;
        lock (gate)
        {
            if (Busy(host.UserId))
                return (null, "Tu as déjà une partie en cours.");
            string code;
            do
                code = RandomNumberGenerator.GetString(CodeAlphabet, CodeLength);
            while (sessions.Any(s => s.Code == code));
            session = new ChessSession(host, new ChessParty("", ""), moveSeconds, Now, code);
            sessions.Add(session);
        }
        return (session, $"Partie publique créée : donne le code {FormatCode(session.Code!)} à ton adversaire.");
    }

    /// <summary>
    /// Rejoint la partie publique de ce code (saisi dans le navigateur : l'appelant limite les essais par adresse IP,
    /// <see cref="AccountLimiter.TryChessCode"/>) : les deux joueurs passent au choix de leur équipe.
    /// </summary>
    public (ChessSession? Session, string? Error) JoinPublic(string? code, ChessParty player)
    {
        var key = NormalizeCode(code);
        if (key.Length != CodeLength)
            return (null, $"Le code a {CodeLength} caractères : vérifie-le.");
        ChessSession? found;
        lock (gate)
        {
            found = sessions.FirstOrDefault(s => s.Stage == ChessStage.Requested && s.Code == key);
            if (found is null)
                return (null, "Aucune partie n'attend avec ce code : vérifie-le, ou demande un nouveau code.");
            if (found.Host.UserId == player.UserId)
                return (null, "C'est ta propre partie : donne ce code à ton adversaire.");
            if (Busy(player.UserId))
                return (null, "Tu as déjà une partie en cours.");
            lock (found.Gate)
            {
                found.Guest = player;
                found.Stage = ChessStage.TeamChoice;
                found.Notice = null;
            }
        }
        found.Notify();
        return (found, null);
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
                s.Notice = s.IsPublic
                    ? "Personne n'a rejoint la partie à temps : le code a expiré."
                    : $"{s.Guest.Pseudo} n'a pas répondu à temps : l'invitation a expiré.";
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
