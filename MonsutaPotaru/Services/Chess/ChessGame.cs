namespace MonsutaPotaru.Services;

/// <summary>
/// Une pièce de la partie : son numéro (le même toute la partie, pour l'animer), son camp et son Pokémon ; chromatique
/// (<see cref="Shiny"/>) pour les noirs d'une partie où les deux joueurs ont la même équipe (on distingue ainsi les camps).
/// </summary>
public sealed record ChessUnit(int Id, ChessColor Color, ChessMon Mon)
{
    public ChessPiece Piece => Mon.Piece;
    public PokeType Type => Mon.Type;
    public bool Shiny { get; init; }

    /// <summary>Image fixe du Pokémon (chromatique s'il l'est).</summary>
    public string SpriteUrl => Mon.SpriteUrlOf(Shiny);

    /// <summary>Dossier de ses sprites animés (chromatiques s'il l'est).</summary>
    public string DonjonFolder => Mon.DonjonFolderOf(Shiny);
}

/// <summary>Comment la partie s'est finie.</summary>
public enum ChessEnd { None, Checkmate, KingEaten, Stalemate, OnlyKings, Forfeit, Timeout, Left }

/// <summary>
/// Un coup : de la case <see cref="From"/> à la case <see cref="To"/> (0 à 63 : 8 × rangée + colonne, a1 = 0, h8 = 63).
/// <see cref="Revive"/> : numéro du Pokémon K.O. qui prend la place d'un pion arrivé au bout du plateau (à son arrivée, ou
/// plus tard : alors <see cref="From"/> = <see cref="To"/>). <see cref="Pass"/> : le tour bonus n'est pas joué.
/// </summary>
public sealed record ChessMove(int From, int To, int? Revive = null)
{
    public static readonly ChessMove Pass = new(-1, -1);
    public bool IsPass => From < 0;
    public bool IsSwap => From >= 0 && From == To;
}

/// <summary>
/// Une case où la pièce choisie peut aller : la pièce mangée (null : case vide), l'efficacité de la prise (2 : tour
/// bonus), et s'il faut choisir le Pokémon K.O. qui remplace le pion (<see cref="Revives"/>, les numéros possibles).
/// </summary>
public sealed record ChessTarget(int To, int? VictimId, double Effect, IReadOnlyList<int> Revives);

/// <summary>Une pièce adverse que la pièce choisie atteint mais ne peut pas manger (pas très efficace ou sans effet).</summary>
public sealed record ChessBlocked(int Square, int VictimId, double Effect);

/// <summary>
/// Une pièce adverse face à la pièce choisie, où qu'elle soit : <see cref="Effect"/> &gt; 1, faible (super efficace :
/// cadre vert) ; &lt; 1, elle résiste (la pièce ne peut pas la manger : cadre rouge).
/// </summary>
public sealed record ChessMatchup(int Square, int VictimId, double Effect);

/// <summary>Une pièce sur le plateau.</summary>
public sealed record ChessPlaced(int Id, int Square);

/// <summary>Où sont les pièces : sur le plateau, et les K.O. de chaque camp (dans l'ordre où ils sont tombés).</summary>
public sealed record ChessPosition(IReadOnlyList<ChessPlaced> Board, IReadOnlyList<int> WhiteKo, IReadOnlyList<int> BlackKo);

/// <summary>
/// Un coup joué, de quoi l'animer et le raconter : la pièce qui bouge, la pièce mangée (et sa case : celle d'arrivée, sauf
/// en prise en passant), la tour du roque, le Pokémon K.O. revenu à la place d'un pion (qui part chez les K.O.), si le
/// joueur rejoue (<see cref="Bonus"/>), si le roi adverse est en échec, la fin de la partie et la position d'après.
/// </summary>
public sealed record ChessStep(
    int Number, ChessColor Color, ChessMove Move, int UnitId, int? CapturedId, int CapturedSquare, double Effect,
    int? RookId, int RookFrom, int RookTo, int? RevivedId, bool Bonus, bool Check, ChessColor? Winner, ChessEnd End,
    string Text, ChessPosition After)
{
    /// <summary>
    /// Nombre de cases parcourues sur le plateau (durée de la marche) ; un échange de place : aucune (les marches vers et
    /// depuis les K.O. sont comptées par <see cref="ChessTiming.Playback"/>).
    /// </summary>
    public int Squares => Move.IsPass || Move.IsSwap ? 0 : Math.Max(ChessGame.Distance(Move.From, Move.To), RookId is null ? 0 : 2);
}

/// <summary>
/// Les règles des échecs Pokémon (choisies par l'utilisateur, le 2026-11-22), sans le réseau ni l'affichage : les
/// déplacements, le roque et la prise en passant des échecs, et en plus :
/// <list type="bullet">
/// <item>une pièce ne mange une autre que si son type est au moins neutre contre elle ; super efficace, elle rejoue
/// aussitôt (elle seule, et elle peut passer), une fois seulement : une prise super efficace pendant ce tour bonus ne
/// donne pas d'autre tour (choix de l'utilisateur, 2026-11-22 ; avant, les tours bonus s'enchaînaient) ;</item>
/// <item>un tour bonus qui mange le roi gagne la partie ; hors de là, échec et mat comme aux échecs (un roi n'est en
/// échec que si une pièce adverse peut vraiment le manger) ; pat, ou deux rois seuls : nulle. Une prise super efficace
/// peut laisser son roi mangeable si le tour bonus qui suit le met à l'abri (le roi mange, se retrouve en échec, et
/// rejoue pour s'en aller : demandé par l'utilisateur, 2026-11-22) ; ce tour bonus ne peut alors pas être passé ;</item>
/// <item>un pion au bout du plateau prend la place d'un Pokémon K.O. de son camp (pas d'un pion), qui revient sur sa case
/// (le pion part chez les K.O.) ; sans K.O., il attend, et l'échange se fait plus tard, à la place d'un coup.</item>
/// </list>
/// Un Pokémon revenu sur un tour bonus prend ce tour bonus.
/// </summary>
public sealed class ChessGame
{
    private static readonly ChessPiece[] BackRank =
        [ChessPiece.Rook, ChessPiece.Knight, ChessPiece.Bishop, ChessPiece.Queen, ChessPiece.King, ChessPiece.Bishop, ChessPiece.Knight, ChessPiece.Rook];
    private static readonly (int Rank, int File)[] KnightJumps = [(1, 2), (2, 1), (2, -1), (1, -2), (-1, -2), (-2, -1), (-2, 1), (-1, 2)];
    private static readonly (int Rank, int File)[] Straight = [(1, 0), (-1, 0), (0, 1), (0, -1)];
    private static readonly (int Rank, int File)[] Diagonal = [(1, 1), (1, -1), (-1, 1), (-1, -1)];

    private readonly ChessUnit?[] board = new ChessUnit?[64];
    private readonly Dictionary<int, ChessUnit> units = [];
    private readonly List<ChessUnit> whiteKo = [], blackKo = [];
    private readonly List<ChessStep> steps = [];
    // Roques encore permis : roi et tour jamais déplacés (la tour pas mangée non plus)
    private bool whiteShort = true, whiteLong = true, blackShort = true, blackLong = true;

    public ChessGame(ChessTeam white, ChessTeam black)
    {
        White = white;
        Black = black;
        var id = 0;
        foreach (var (color, team, back, pawns) in new[] { (ChessColor.White, white, 0, 1), (ChessColor.Black, black, 7, 6) })
        {
            for (var file = 0; file < 8; file++)
                Place(NewUnit(id++, color, team.Of(BackRank[file])), Square(back, file));
            for (var file = 0; file < 8; file++)
                Place(NewUnit(id++, color, team.PawnAt(file)), Square(pawns, file));
        }
    }

    /// <summary>Les deux camps ont la même équipe : les noirs sont chromatiques.</summary>
    public bool SameTeams => White.Id == Black.Id;

    private ChessUnit NewUnit(int id, ChessColor color, ChessMon mon) =>
        new(id, color, mon) { Shiny = SameTeams && color == ChessColor.Black };

    private void Place(ChessUnit unit, int square)
    {
        units[unit.Id] = unit;
        board[square] = unit;
    }

    /// <summary>
    /// Une partie qui commence d'une position donnée (pièces sur leurs cases, Pokémon déjà K.O.), pour les tests : les
    /// roques ne restent permis qu'au roi et aux tours encore sur leurs cases de départ.
    /// </summary>
    public static ChessGame FromPosition(ChessTeam white, ChessTeam black, ChessColor turn,
        IEnumerable<(ChessColor Color, ChessPiece Piece, int Square)> pieces, IEnumerable<(ChessColor Color, ChessPiece Piece)>? ko = null)
    {
        var game = new ChessGame(white, black);
        Array.Clear(game.board);
        game.units.Clear();
        var id = 0;
        foreach (var (color, piece, square) in pieces)
        {
            var team = color == ChessColor.White ? white : black;
            game.Place(game.NewUnit(id++, color, piece == ChessPiece.Pawn ? team.PawnAt(FileOf(square)) : team.Of(piece)), square);
        }
        foreach (var (color, piece) in ko ?? [])
        {
            var unit = game.NewUnit(id++, color, (color == ChessColor.White ? white : black).Of(piece));
            game.units[unit.Id] = unit;
            (color == ChessColor.White ? game.whiteKo : game.blackKo).Add(unit);
        }
        bool Has(int square, ChessColor color, ChessPiece piece) => game.board[square] is { } u && u.Color == color && u.Piece == piece;
        game.whiteShort = Has(4, ChessColor.White, ChessPiece.King) && Has(7, ChessColor.White, ChessPiece.Rook);
        game.whiteLong = Has(4, ChessColor.White, ChessPiece.King) && Has(0, ChessColor.White, ChessPiece.Rook);
        game.blackShort = Has(60, ChessColor.Black, ChessPiece.King) && Has(63, ChessColor.Black, ChessPiece.Rook);
        game.blackLong = Has(60, ChessColor.Black, ChessPiece.King) && Has(56, ChessColor.Black, ChessPiece.Rook);
        game.Turn = turn;
        return game;
    }

    public ChessTeam White { get; }
    public ChessTeam Black { get; }

    /// <summary>Camp qui doit jouer.</summary>
    public ChessColor Turn { get; private set; } = ChessColor.White;

    /// <summary>Case de la pièce qui joue son tour bonus (null : tour normal).</summary>
    public int? BonusSquare { get; private set; }

    /// <summary>Case sautée par le dernier pion avancé de deux cases (prise en passant possible au coup suivant).</summary>
    public int? EnPassant { get; private set; }

    public ChessColor? Winner { get; private set; }
    public ChessEnd End { get; private set; }
    public bool IsOver => End != ChessEnd.None;

    public IReadOnlyList<ChessStep> Steps => steps;
    public IReadOnlyCollection<ChessUnit> Units => units.Values;
    public ChessUnit Unit(int id) => units[id];
    public ChessUnit? At(int square) => square is >= 0 and < 64 ? board[square] : null;

    /// <summary>Pokémon K.O. d'un camp, dans l'ordre où ils sont tombés.</summary>
    public IReadOnlyList<ChessUnit> Ko(ChessColor color) => color == ChessColor.White ? whiteKo : blackKo;

    public ChessPosition Position() => new(
        [.. Enumerable.Range(0, 64).Where(s => board[s] is not null).Select(s => new ChessPlaced(board[s]!.Id, s))],
        [.. whiteKo.Select(u => u.Id)], [.. blackKo.Select(u => u.Id)]);

    // ---------- Cases ----------

    public static int Square(int rank, int file) => rank * 8 + file;
    public static int RankOf(int square) => square / 8;
    public static int FileOf(int square) => square % 8;

    /// <summary>Nom de la case (« e4 »).</summary>
    public static string Name(int square) => $"{(char)('a' + FileOf(square))}{RankOf(square) + 1}";

    /// <summary>Nombre de cases d'une marche : en ligne droite (ou en diagonale), et le L du cavalier (3).</summary>
    public static int Distance(int from, int to)
    {
        var (dr, df) = (Math.Abs(RankOf(to) - RankOf(from)), Math.Abs(FileOf(to) - FileOf(from)));
        return dr != df && dr != 0 && df != 0 ? dr + df : Math.Max(dr, df);
    }

    private static bool Inside(int rank, int file) => rank is >= 0 and < 8 && file is >= 0 and < 8;
    private static int Forward(ChessColor color) => color == ChessColor.White ? 1 : -1;
    private static int LastRank(ChessColor color) => color == ChessColor.White ? 7 : 0;
    private static ChessColor Other(ChessColor color) => color == ChessColor.White ? ChessColor.Black : ChessColor.White;

    /// <summary>La pièce peut manger celle-ci (son type est au moins neutre contre elle).</summary>
    public static bool CanEat(PokeType attacker, PokeType victim) => PokeTypes.Effectiveness(attacker, victim) >= 1;

    // ---------- Coups permis ----------

    /// <summary>Les Pokémon K.O. de ce camp qui peuvent revenir à la place d'un pion (tous sauf les pions).</summary>
    private List<int> Revivable(ChessColor color) => [.. Ko(color).Where(u => u.Piece != ChessPiece.Pawn).Select(u => u.Id)];

    /// <summary>
    /// Coups permis de la pièce de cette case pour le camp qui joue (vide si ce n'est pas à elle : autre camp, autre pièce
    /// pendant un tour bonus, partie finie). Un coup qui laisse son roi mangeable est interdit, sauf s'il mange le roi adverse
    /// ou si c'est une prise super efficace dont le tour bonus peut mettre le roi à l'abri (<see cref="Legal"/>).
    /// </summary>
    public List<ChessMove> MovesFrom(int square)
    {
        if (IsOver || At(square) is not { } unit || unit.Color != Turn || (BonusSquare is { } bonus && bonus != square))
            return [];
        return [.. Candidates(square, unit).Where(Legal)];
    }

    /// <summary>Tous les coups permis du camp qui joue (le passe du tour bonus compris).</summary>
    public List<ChessMove> LegalMoves()
    {
        if (IsOver)
            return [];
        if (BonusSquare is { } bonus)
            return CanPass ? [.. MovesFrom(bonus), ChessMove.Pass] : MovesFrom(bonus);
        return [.. Enumerable.Range(0, 64).SelectMany(MovesFrom)];
    }

    /// <summary>Cases où la pièce choisie peut aller, pour l'affichage (un coup par case ; les choix de K.O. regroupés).</summary>
    public List<ChessTarget> Targets(int square)
    {
        var moves = MovesFrom(square);
        return [.. moves.Where(m => !m.IsSwap).GroupBy(m => m.To).Select(g =>
        {
            var victim = VictimOf(g.First());
            return new ChessTarget(g.Key, victim?.Unit.Id, victim is { } v ? PokeTypes.Effectiveness(At(square)!.Type, v.Unit.Type) : 1,
                [.. g.Where(m => m.Revive is not null).Select(m => m.Revive!.Value)]);
        })];
    }

    /// <summary>Pokémon K.O. qui peuvent prendre la place de ce pion, déjà au bout du plateau (coup « échange »).</summary>
    public List<int> SwapsFrom(int square) => [.. MovesFrom(square).Where(m => m.IsSwap).Select(m => m.Revive!.Value)];

    /// <summary>Pièces adverses que la pièce de cette case atteint sans pouvoir les manger (types), pour l'expliquer au joueur.</summary>
    public List<ChessBlocked> Blocked(int square)
    {
        if (At(square) is not { } unit)
            return [];
        List<ChessBlocked> blocked = [];
        for (var s = 0; s < 64; s++)
        {
            if (board[s] is { } victim && victim.Color != unit.Color && Reaches(board, square, s, unit) && !CanEat(unit.Type, victim.Type))
                blocked.Add(new ChessBlocked(s, victim.Id, PokeTypes.Effectiveness(unit.Type, victim.Type)));
        }
        return blocked;
    }

    /// <summary>
    /// Pièces adverses du plateau, même hors de portée, auxquelles la pièce de cette case est super efficace ou qui lui
    /// résistent (les neutres n'y sont pas) : les cadres vert et rouge du plateau.
    /// </summary>
    public List<ChessMatchup> Matchups(int square)
    {
        if (At(square) is not { } unit)
            return [];
        List<ChessMatchup> matchups = [];
        for (var s = 0; s < 64; s++)
        {
            if (board[s] is { } victim && victim.Color != unit.Color && PokeTypes.Effectiveness(unit.Type, victim.Type) is var effect && effect != 1)
                matchups.Add(new ChessMatchup(s, victim.Id, effect));
        }
        return matchups;
    }

    /// <summary>Coups de la pièce d'après sa marche et les types, sans regarder encore son roi.</summary>
    private IEnumerable<ChessMove> Candidates(int from, ChessUnit unit)
    {
        var (rank, file) = (RankOf(from), FileOf(from));
        if (unit.Piece == ChessPiece.Pawn)
        {
            foreach (var move in PawnMoves(from, unit, rank, file))
                yield return move;
            yield break;
        }
        var reach = unit.Piece switch
        {
            ChessPiece.Knight => KnightJumps.Select(j => (j, false)),
            ChessPiece.King => Straight.Concat(Diagonal).Select(j => (j, false)),
            ChessPiece.Rook => Straight.Select(j => (j, true)),
            ChessPiece.Bishop => Diagonal.Select(j => (j, true)),
            _ => Straight.Concat(Diagonal).Select(j => (j, true)),
        };
        foreach (var ((dr, df), slides) in reach)
        {
            for (var (r, f) = (rank + dr, file + df); Inside(r, f); (r, f) = (r + dr, f + df))
            {
                var to = Square(r, f);
                if (board[to] is { } other)
                {
                    if (other.Color != unit.Color && CanEat(unit.Type, other.Type))
                        yield return new ChessMove(from, to);
                    break;
                }
                yield return new ChessMove(from, to);
                if (!slides)
                    break;
            }
        }
        if (unit.Piece == ChessPiece.King)
        {
            foreach (var castle in Castles(from, unit))
                yield return castle;
        }
    }

    private IEnumerable<ChessMove> PawnMoves(int from, ChessUnit unit, int rank, int file)
    {
        var forward = Forward(unit.Color);
        var revivable = Revivable(unit.Color);
        if (rank == LastRank(unit.Color))
        {
            foreach (var id in revivable) // déjà au bout : l'échange avec un K.O. arrivé depuis
                yield return new ChessMove(from, from, id);
            yield break;
        }
        // Arrivée au bout : un coup par Pokémon K.O. qui peut le remplacer (sinon le pion y attend)
        IEnumerable<ChessMove> Arrive(int to) => RankOf(to) == LastRank(unit.Color) && revivable.Count > 0
            ? revivable.Select(id => new ChessMove(from, to, id))
            : [new ChessMove(from, to)];

        var one = Square(rank + forward, file);
        if (board[one] is null)
        {
            foreach (var move in Arrive(one))
                yield return move;
            var start = unit.Color == ChessColor.White ? 1 : 6;
            var two = one + 8 * forward;
            if (rank == start && board[two] is null)
                yield return new ChessMove(from, two);
        }
        foreach (var df in new[] { -1, 1 })
        {
            if (!Inside(rank + forward, file + df))
                continue;
            var to = Square(rank + forward, file + df);
            if (board[to] is { } other && other.Color != unit.Color && CanEat(unit.Type, other.Type))
            {
                foreach (var move in Arrive(to))
                    yield return move;
            }
            else if (board[to] is null && to == EnPassant && board[to - 8 * forward] is { Piece: ChessPiece.Pawn } passed
                     && passed.Color != unit.Color && CanEat(unit.Type, passed.Type))
            {
                yield return new ChessMove(from, to);
            }
        }
    }

    /// <summary>Roques : roi et tour jamais déplacés, cases entre eux vides, roi ni en échec ni de passage sur une case menacée.</summary>
    private IEnumerable<ChessMove> Castles(int from, ChessUnit king)
    {
        var back = king.Color == ChessColor.White ? 0 : 7;
        if (from != Square(back, 4))
            yield break;
        var (canShort, canLong) = king.Color == ChessColor.White ? (whiteShort, whiteLong) : (blackShort, blackLong);
        var enemy = Other(king.Color);
        if ((!canShort && !canLong) || Attacked(board, from, enemy, king.Type))
            yield break;
        if (canShort && IsOwnRook(Square(back, 7), king.Color) && board[Square(back, 5)] is null && board[Square(back, 6)] is null
            && !Attacked(board, Square(back, 5), enemy, king.Type))
            yield return new ChessMove(from, Square(back, 6));
        if (canLong && IsOwnRook(Square(back, 0), king.Color) && board[Square(back, 1)] is null && board[Square(back, 2)] is null
            && board[Square(back, 3)] is null && !Attacked(board, Square(back, 3), enemy, king.Type))
            yield return new ChessMove(from, Square(back, 2));
    }

    private bool IsOwnRook(int square, ChessColor color) => board[square] is { Piece: ChessPiece.Rook } rook && rook.Color == color;

    /// <summary>
    /// Le coup ne laisse pas son roi mangeable (ou mange le roi adverse : la partie est gagnée). Une prise super efficace
    /// hors tour bonus peut le laisser mangeable un instant : la pièce rejoue aussitôt, et le coup est permis si ce tour
    /// bonus peut mettre le roi à l'abri (le roi qui mange en se mettant en échec, puis s'en va ; passer est alors
    /// interdit, voir <see cref="CanPass"/>).
    /// </summary>
    private bool Legal(ChessMove move)
    {
        var mover = board[move.From]!;
        var victim = VictimOf(move);
        if (victim is { Unit.Piece: ChessPiece.King })
            return true;
        var after = Simulate(move);
        if (KingSafe(after, mover.Color))
            return true;
        return BonusSquare is null && victim is { } v && PokeTypes.Effectiveness(mover.Type, v.Unit.Type) > 1 && BonusCanSave(move, after);
    }

    /// <summary>Le roi de ce camp n'est pas mangeable sur ce plateau (ou n'y est pas).</summary>
    private static bool KingSafe(ChessUnit?[] on, ChessColor color)
    {
        var king = Array.FindIndex(on, u => u is { Piece: ChessPiece.King } k && k.Color == color);
        return king < 0 || !Attacked(on, king, Other(color), on[king]!.Type);
    }

    /// <summary>
    /// Après cette prise super efficace (plateau <paramref name="after"/>), la pièce arrivée a au moins un coup de tour
    /// bonus permis : qui met son roi à l'abri, ou qui mange le roi adverse. Le plateau est posé le temps du calcul (les
    /// coups de la pièce se calculent sur lui), puis remis : l'appelant tient le verrou de la partie.
    /// </summary>
    private bool BonusCanSave(ChessMove move, ChessUnit?[] after)
    {
        var saved = (ChessUnit?[])board.Clone();
        var (enPassant, bonus) = (EnPassant, BonusSquare);
        Array.Copy(after, board, board.Length);
        (EnPassant, BonusSquare) = (null, move.To); // une prise n'ouvre pas de prise en passant ; tour bonus : pas d'autre
        try
        {
            return MovesFrom(move.To).Count > 0;
        }
        finally
        {
            Array.Copy(saved, board, board.Length);
            (EnPassant, BonusSquare) = (enPassant, bonus);
        }
    }

    /// <summary>
    /// Le tour bonus peut être passé : pas si son roi est mangeable (après une prise super efficace qui l'a mis en échec,
    /// la pièce doit le mettre à l'abri).
    /// </summary>
    public bool CanPass => !IsOver && BonusSquare is not null && !InCheck(Turn);

    /// <summary>La pièce mangée par ce coup et sa case (prise en passant : le pion à côté), ou null.</summary>
    private (ChessUnit Unit, int Square)? VictimOf(ChessMove move)
    {
        if (move.IsPass || move.IsSwap)
            return null;
        if (board[move.To] is { } victim)
            return (victim, move.To);
        var mover = board[move.From]!;
        if (mover.Piece == ChessPiece.Pawn && FileOf(move.From) != FileOf(move.To))
        {
            var passed = move.To - 8 * Forward(mover.Color);
            return board[passed] is { } p ? (p, passed) : null;
        }
        return null;
    }

    /// <summary>Le plateau après ce coup (pour savoir si le roi y serait mangeable).</summary>
    private ChessUnit?[] Simulate(ChessMove move)
    {
        var after = (ChessUnit?[])board.Clone();
        var mover = after[move.From]!;
        if (VictimOf(move) is { } victim)
            after[victim.Square] = null;
        after[move.From] = null;
        after[move.To] = move.Revive is { } id ? units[id] : mover;
        if (CastleRook(move, mover) is { } rook)
        {
            after[rook.To] = after[rook.From];
            after[rook.From] = null;
        }
        return after;
    }

    /// <summary>La tour qui accompagne un roque (le roi avance de deux cases), ou null.</summary>
    private static (int From, int To)? CastleRook(ChessMove move, ChessUnit mover)
    {
        if (mover.Piece != ChessPiece.King || move.IsSwap || Math.Abs(FileOf(move.To) - FileOf(move.From)) != 2)
            return null;
        var back = RankOf(move.From);
        return FileOf(move.To) == 6 ? (Square(back, 7), Square(back, 5)) : (Square(back, 0), Square(back, 3));
    }

    /// <summary>Une pièce de ce camp pourrait manger un Pokémon de ce type sur cette case.</summary>
    private static bool Attacked(ChessUnit?[] on, int square, ChessColor by, PokeType victim)
    {
        for (var s = 0; s < 64; s++)
        {
            if (on[s] is { } unit && unit.Color == by && CanEat(unit.Type, victim) && Reaches(on, s, square, unit))
                return true;
        }
        return false;
    }

    /// <summary>La pièce atteint cette case pour y manger (marche de la pièce, chemin libre ; pion : en diagonale).</summary>
    private static bool Reaches(ChessUnit?[] on, int from, int to, ChessUnit unit)
    {
        var (dr, df) = (RankOf(to) - RankOf(from), FileOf(to) - FileOf(from));
        if (dr == 0 && df == 0)
            return false;
        switch (unit.Piece)
        {
            case ChessPiece.Pawn:
                return dr == Forward(unit.Color) && Math.Abs(df) == 1;
            case ChessPiece.Knight:
                return (Math.Abs(dr), Math.Abs(df)) is (1, 2) or (2, 1);
            case ChessPiece.King:
                return Math.Max(Math.Abs(dr), Math.Abs(df)) == 1;
        }
        var straight = dr == 0 || df == 0;
        var diagonal = Math.Abs(dr) == Math.Abs(df);
        if ((unit.Piece == ChessPiece.Rook && !straight) || (unit.Piece == ChessPiece.Bishop && !diagonal) || (!straight && !diagonal))
            return false;
        var (sr, sf) = (Math.Sign(dr), Math.Sign(df));
        for (var (r, f) = (RankOf(from) + sr, FileOf(from) + sf); Square(r, f) != to; (r, f) = (r + sr, f + sf))
        {
            if (on[Square(r, f)] is not null)
                return false;
        }
        return true;
    }

    /// <summary>Le roi de ce camp est en échec : une pièce adverse peut le manger.</summary>
    public bool InCheck(ChessColor color)
    {
        var king = Array.FindIndex(board, u => u is { Piece: ChessPiece.King } k && k.Color == color);
        return king >= 0 && Attacked(board, king, Other(color), board[king]!.Type);
    }

    // ---------- Jouer ----------

    /// <summary>Joue ce coup s'il est permis (sinon null) : la position change et le coup est raconté.</summary>
    public ChessStep? Play(ChessMove move)
    {
        if (!LegalMoves().Contains(move))
            return null;
        var color = Turn;
        var onBonusTurn = BonusSquare is not null;
        if (move.IsPass)
        {
            var passer = board[BonusSquare!.Value]!;
            BonusSquare = null;
            return Record(color, move, passer.Id, null, -1, 1, null, -1, -1, null, false, $"{passer.Mon.Name} ne rejoue pas.");
        }

        var mover = board[move.From]!;
        var victim = VictimOf(move);
        var effect = victim is { } v ? PokeTypes.Effectiveness(mover.Type, v.Unit.Type) : 1;
        var rook = CastleRook(move, mover);
        ChessUnit? revived = move.Revive is { } reviveId ? units[reviveId] : null;

        // Position
        if (victim is { } eaten)
        {
            board[eaten.Square] = null;
            if (eaten.Unit.Piece != ChessPiece.King)
                (eaten.Unit.Color == ChessColor.White ? whiteKo : blackKo).Add(eaten.Unit);
        }
        board[move.From] = null;
        board[move.To] = mover;
        if (rook is { } r)
        {
            board[r.To] = board[r.From];
            board[r.From] = null;
        }
        if (revived is not null)
        {
            var ko = color == ChessColor.White ? whiteKo : blackKo;
            ko.Remove(revived);
            ko.Add(mover); // le pion part chez les K.O.
            board[move.To] = revived;
        }
        UpdateCastling(move, mover, victim);
        EnPassant = mover.Piece == ChessPiece.Pawn && Math.Abs(RankOf(move.To) - RankOf(move.From)) == 2
            ? (move.From + move.To) / 2
            : null;

        var text = Story(mover, move, victim?.Unit, rook is not null, revived);
        if (victim is { Unit.Piece: ChessPiece.King })
        {
            (Winner, End) = (color, ChessEnd.KingEaten);
            BonusSquare = null;
            return Record(color, move, mover.Id, victim?.Unit.Id, victim?.Square ?? -1, effect, rook is null ? null : board[rook.Value.To]!.Id,
                rook?.From ?? -1, rook?.To ?? -1, revived?.Id, false, $"{text} Le roi est mangé : victoire !");
        }

        // Tour bonus : super efficace, et la pièce (ou le Pokémon revenu à sa place) peut encore bouger ; un seul par tour
        BonusSquare = null;
        var bonus = false;
        if (victim is not null && effect > 1 && onBonusTurn)
        {
            text += $" C'est super efficace, mais {board[move.To]!.Mon.Name} a déjà rejoué.";
        }
        else if (victim is not null && effect > 1)
        {
            BonusSquare = move.To;
            bonus = MovesFrom(move.To).Count > 0;
            if (!bonus)
                BonusSquare = null;
            text += bonus
                ? $" C'est super efficace ! {board[move.To]!.Mon.Name} rejoue."
                : $" C'est super efficace, mais {board[move.To]!.Mon.Name} ne peut plus bouger.";
            if (bonus && InCheck(color))
                text += " Il doit mettre son roi à l'abri.";
        }
        return Record(color, move, mover.Id, victim?.Unit.Id, victim?.Square ?? -1, effect, rook is null ? null : board[rook.Value.To]!.Id,
            rook?.From ?? -1, rook?.To ?? -1, revived?.Id, bonus, text);
    }

    /// <summary>Fin du coup : au tour de l'autre (sauf tour bonus), échec, mat, pat ou rois seuls.</summary>
    private ChessStep Record(ChessColor color, ChessMove move, int unitId, int? capturedId, int capturedSquare, double effect,
        int? rookId, int rookFrom, int rookTo, int? revivedId, bool bonus, string text)
    {
        var enemy = Other(color);
        var check = !IsOver && InCheck(enemy);
        if (!IsOver && !bonus)
        {
            Turn = enemy;
            if (board.Count(u => u is not null) == 2)
            {
                End = ChessEnd.OnlyKings;
                text += " Il ne reste que les deux rois : match nul.";
            }
            else if (LegalMoves().Count == 0)
            {
                (Winner, End) = check ? (color, ChessEnd.Checkmate) : ((ChessColor?)null, ChessEnd.Stalemate);
                text += check ? " Échec et mat !" : " Pat : match nul.";
            }
            else if (check)
            {
                text += " Échec !";
            }
        }
        else if (check && bonus)
        {
            text += " Échec !";
        }
        var step = new ChessStep(steps.Count + 1, color, move, unitId, capturedId, capturedSquare, effect, rookId, rookFrom, rookTo,
            revivedId, bonus, check, Winner, End, text, Position());
        steps.Add(step);
        return step;
    }

    private void UpdateCastling(ChessMove move, ChessUnit mover, (ChessUnit Unit, int Square)? victim)
    {
        if (mover.Piece == ChessPiece.King)
        {
            if (mover.Color == ChessColor.White)
                (whiteShort, whiteLong) = (false, false);
            else
                (blackShort, blackLong) = (false, false);
        }
        foreach (var square in new[] { move.From, victim?.Square ?? -1 })
        {
            switch (square)
            {
                case 0: whiteLong = false; break;
                case 7: whiteShort = false; break;
                case 56: blackLong = false; break;
                case 63: blackShort = false; break;
            }
        }
    }

    /// <summary>Ce qu'a fait la pièce (« Salamèche mange Kaorine en e5. »).</summary>
    private static string Story(ChessUnit mover, ChessMove move, ChessUnit? victim, bool castle, ChessUnit? revived)
    {
        var name = mover.Mon.Name;
        if (move.IsSwap)
            return $"{name} laisse sa place à {revived!.Mon.Name}, qui revient en {Name(move.To)}.";
        var text = castle
            ? $"Roque : {name} et sa tour changent de côté."
            : victim is not null
                ? $"{name} mange {victim.Mon.Name} en {Name(move.To)}."
                : $"{name} va en {Name(move.To)}.";
        if (revived is not null)
            text += $" Au bout du plateau, {name} laisse sa place à {revived.Mon.Name}.";
        return text;
    }

    /// <summary>Le joueur de ce camp abandonne, dépasse le temps ou quitte la page : l'autre gagne.</summary>
    public void Resign(ChessColor color, ChessEnd reason)
    {
        if (IsOver)
            return;
        (Winner, End) = (Other(color), reason);
        BonusSquare = null;
    }
}
