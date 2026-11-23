using MonsutaPotaru.Services;

namespace MonsutaPotaru.Tests;

/// <summary>
/// Règles des échecs Pokémon (<see cref="ChessGame"/>) : prises selon les types, tour bonus en chaîne, roi mangé, échec
/// et mat d'après les types, roque, prise en passant, pion au bout du plateau, pat et rois seuls.
/// </summary>
public sealed class ChessGameTests
{
    private static readonly ChessTeam Fire = ChessTeams.Find("feu")!;
    private static readonly ChessTeam Psy = ChessTeams.Find("psy")!;
    private static readonly ChessTeam Electric = ChessTeams.Find("electrique")!;
    private static readonly ChessTeam Poison = ChessTeams.Find("poison")!;

    private const ChessColor W = ChessColor.White;
    private const ChessColor B = ChessColor.Black;

    /// <summary>Case d'après son nom (« e4 »).</summary>
    private static int Sq(string name) => ChessGame.Square(name[1] - '1', name[0] - 'a');

    private static ChessGame Position(ChessTeam white, ChessTeam black, ChessColor turn, string pieces,
        IEnumerable<(ChessColor, ChessPiece)>? ko = null) =>
        ChessGame.FromPosition(white, black, turn, pieces.Split(' ').Select(p =>
            (p[0] == 'w' ? W : B, p[1] switch
            {
                'P' => ChessPiece.Pawn, 'R' => ChessPiece.Rook, 'N' => ChessPiece.Knight, 'B' => ChessPiece.Bishop,
                'Q' => ChessPiece.Queen, _ => ChessPiece.King,
            }, Sq(p[2..]))), ko);

    private static ChessStep Play(ChessGame game, string from, string to, int? revive = null)
    {
        var step = game.Play(new ChessMove(Sq(from), Sq(to), revive));
        Assert.True(step is not null, $"{from}-{to} refusé");
        return step;
    }

    [Fact]
    public void Teams_HaveOnePokemonPerPiece()
    {
        Assert.Equal(["psy", "feu", "electrique", "poison", "eau", "plante", "sol"], ChessTeams.All.Select(t => t.Id));
        foreach (var team in ChessTeams.All)
        {
            Assert.Equal(Enum.GetValues<ChessPiece>(), team.Members.Select(m => m.Piece).Distinct().Order());
            // Une seule pièce peut avoir deux espèces, le pion ; les Pokémon d'une même pièce ont le même type (aide)
            foreach (var piece in Enum.GetValues<ChessPiece>())
            {
                var members = team.MembersOf(piece);
                Assert.True(members.Count == 1 || piece == ChessPiece.Pawn && members.Count == 2, $"{team.Id} : {piece}");
                Assert.Single(members.Select(m => m.Type).Distinct());
            }
        }
        Assert.Null(ChessTeams.Find("pirate"));
        Assert.Equal("Raichu d'Alola", Electric.Of(ChessPiece.Queen).Name);
        Assert.Equal("Nidoran♂ / Nidoran♀", Poison.NamesOf(ChessPiece.Pawn));
        Assert.Equal("Nidoran♂ ou Nidoran♀", Poison.NamesInSentence(ChessPiece.Pawn));
        Assert.Equal("Libégon", ChessTeams.Find("sol")!.Of(ChessPiece.Knight).Name);
    }

    [Fact]
    public void Start_NidoranPawnsAlternate()
    {
        var game = new ChessGame(Poison, Fire);
        var whites = Enumerable.Range(0, 8).Select(file => game.At(Sq($"{(char)('a' + file)}2"))!.Mon.Name);
        Assert.Equal(["Nidoran♂", "Nidoran♀", "Nidoran♂", "Nidoran♀", "Nidoran♂", "Nidoran♀", "Nidoran♂", "Nidoran♀"], whites);
        Assert.Equal("Salamèche", game.At(Sq("b7"))!.Mon.Name);
        var black = new ChessGame(Fire, Poison);
        Assert.Equal("Nidoran♂", black.At(Sq("a7"))!.Mon.Name);
        Assert.Equal("Nidoran♀", black.At(Sq("h7"))!.Mon.Name);
        Assert.Equal("Nidoking", black.At(Sq("e8"))!.Mon.Name);
    }

    [Theory]
    [InlineData(PokeType.Fire, PokeType.Grass, 2)]
    [InlineData(PokeType.Fire, PokeType.Water, 0.5)]
    [InlineData(PokeType.Ground, PokeType.Flying, 0)]
    [InlineData(PokeType.Dragon, PokeType.Fairy, 0)]
    [InlineData(PokeType.Psychic, PokeType.Dark, 0)]
    [InlineData(PokeType.Dark, PokeType.Psychic, 2)]
    [InlineData(PokeType.Fire, PokeType.Psychic, 1)]
    [InlineData(PokeType.Steel, PokeType.Fairy, 2)]
    public void Effectiveness_FollowsTheTypeChart(PokeType attacker, PokeType defender, double expected) =>
        Assert.Equal(expected, PokeTypes.Effectiveness(attacker, defender));

    [Fact]
    public void Start_TwentyMovesForWhite()
    {
        var game = new ChessGame(Fire, Psy);
        Assert.Equal(20, game.LegalMoves().Count);
        Assert.Equal("Salamèche", game.At(Sq("e2"))!.Mon.Name);
        Assert.Equal("Gallame", game.At(Sq("e8"))!.Mon.Name);
        Assert.Empty(game.MovesFrom(Sq("e7"))); // pas aux noirs
    }

    [Fact]
    public void NeutralCapture_NoBonus()
    {
        var game = Position(Fire, Psy, W, "wKa1 wPe4 bPd5 bKh8");
        var step = Play(game, "e4", "d5"); // Feu contre Psy : neutre
        Assert.Equal(1, step.Effect);
        Assert.False(step.Bonus);
        Assert.Equal(B, game.Turn);
        Assert.Equal("Tarsal", Assert.Single(game.Ko(B)).Mon.Name);
        Assert.Contains("Salamèche mange Tarsal en d5.", step.Text);
    }

    [Fact]
    public void WeakCapture_IsRefusedAndExplained()
    {
        var game = Position(Fire, Psy, W, "wKa1 wPe4 bBd5 bKh8");
        Assert.DoesNotContain(game.MovesFrom(Sq("e4")), m => m.To == Sq("d5")); // Feu contre Eau : pas très efficace
        var blocked = Assert.Single(game.Blocked(Sq("e4")));
        Assert.Equal((Sq("d5"), 0.5), (blocked.Square, blocked.Effect));
        Assert.Null(game.Play(new ChessMove(Sq("e4"), Sq("d5"))));
    }

    [Fact]
    public void SuperEffective_GivesOneBonusTurn_ThenPass()
    {
        // Démolosse (Ténèbres) contre Tarsal (Psy) : super efficace, il rejoue
        var game = Position(Fire, Psy, W, "wKa1 wNe4 bPf6 bPd7 bKh1");
        var first = Play(game, "e4", "f6");
        Assert.True(first.Bonus);
        Assert.Equal(W, game.Turn);
        Assert.Equal(Sq("f6"), game.BonusSquare);
        Assert.Empty(game.MovesFrom(Sq("a1"))); // seul Démolosse rejoue
        Assert.Contains(ChessMove.Pass, game.LegalMoves());
        Assert.Contains("super efficace", first.Text);

        var pass = game.Play(ChessMove.Pass)!;
        Assert.False(pass.Bonus);
        Assert.Equal(B, game.Turn);
        Assert.Null(game.BonusSquare);
        Assert.Null(game.Play(ChessMove.Pass)); // plus de tour bonus
    }

    [Fact]
    public void SuperEffective_DuringTheBonusTurn_DoesNotGiveAnother()
    {
        // Démolosse mange deux Tarsal de suite : le second, pendant son tour bonus, ne le fait pas rejouer encore
        var game = Position(Fire, Psy, W, "wKa1 wNe4 bPf6 bPd7 bPb6 bKh1");
        Assert.True(Play(game, "e4", "f6").Bonus);
        var second = Play(game, "f6", "d7");
        Assert.False(second.Bonus);
        Assert.Equal(2, second.Effect);
        Assert.Equal("Démolosse mange Tarsal en d7. C'est super efficace, mais Démolosse a déjà rejoué.", second.Text);
        Assert.Equal(B, game.Turn);
        Assert.Null(game.BonusSquare);
        Assert.DoesNotContain(ChessMove.Pass, game.LegalMoves());

        // Au tour suivant de ce camp, une nouvelle prise super efficace redonne un tour bonus
        Play(game, "h1", "h2");
        Assert.True(Play(game, "d7", "b6").Bonus);
        Assert.Equal(Sq("b6"), game.BonusSquare);
    }

    [Fact]
    public void SuperEffective_KingCanEatIntoCheck_ThenMustEscapeOnTheBonusTurn()
    {
        // Gallame (roi Combat) mange Démolosse (Ténèbres) : super efficace ; en e5, Boumata (tour Dragon, a5) peut le manger,
        // mais il rejoue aussitôt et peut s'en aller : le coup est permis (bug signalé par l'utilisateur, 2026-11-22)
        var game = Position(Psy, Fire, W, "wKe4 bNe5 bRa5 bKh8");
        Assert.Contains(game.MovesFrom(Sq("e4")), m => m.To == Sq("e5"));
        var eat = Play(game, "e4", "e5");
        Assert.True(eat.Bonus);
        Assert.True(eat.Check is false); // pas d'échec au roi adverse
        Assert.Equal("Gallame mange Démolosse en e5. C'est super efficace ! Gallame rejoue. Il doit mettre son roi à l'abri.", eat.Text);
        Assert.True(game.InCheck(W));

        // Passer le laisserait mangeable : interdit ; seules les cases hors de la rangée 5 restent
        Assert.False(game.CanPass);
        Assert.DoesNotContain(ChessMove.Pass, game.LegalMoves());
        Assert.Null(game.Play(ChessMove.Pass));
        Assert.Equal(["d4", "d6", "e4", "e6", "f4", "f6"], game.MovesFrom(Sq("e5")).Select(m => ChessGame.Name(m.To)).Order());
        Play(game, "e5", "e6");
        Assert.Equal(B, game.Turn);
        Assert.False(game.InCheck(W));
    }

    [Fact]
    public void SuperEffective_IntoCheck_RefusedWhenTheBonusTurnCannotEscape()
    {
        // Trois tours Dragon tiennent les rangées 4, 5 et 6 : en e5, Gallame ne pourrait aller nulle part à l'abri
        var game = Position(Psy, Fire, W, "wKe4 bNe5 bRa4 bRa5 bRa6 bKh8");
        Assert.DoesNotContain(game.MovesFrom(Sq("e4")), m => m.To == Sq("e5"));
        Assert.Null(game.Play(new ChessMove(Sq("e4"), Sq("e5"))));

        // Une prise neutre qui met son roi en échec reste interdite (pas de tour bonus) : Gallame contre Salamèche (Feu)
        var neutral = Position(Psy, Fire, W, "wKe4 bPe5 bRa5 bKh8");
        Assert.DoesNotContain(neutral.MovesFrom(Sq("e4")), m => m.To == Sq("e5"));

        // Sans échec, le tour bonus se passe toujours
        var free = Position(Psy, Fire, W, "wKe4 bNe5 bKh8");
        Play(free, "e4", "e5");
        Assert.True(free.CanPass);
        Assert.Contains(ChessMove.Pass, free.LegalMoves());
    }

    [Fact]
    public void BonusTurn_CanEatTheKing()
    {
        // Malvalame (Spectre) mange Tarsal (super efficace), puis le roi Gallame (Combat : neutre) derrière lui
        var game = Position(Fire, Psy, W, "wKh1 wBc1 bPe3 bKg5");
        Assert.False(game.InCheck(B));
        Assert.True(Play(game, "c1", "e3").Bonus);
        var win = Play(game, "e3", "g5");
        Assert.Equal((W, ChessEnd.KingEaten), (win.Winner, win.End));
        Assert.True(game.IsOver);
        Assert.Empty(game.LegalMoves());
        Assert.DoesNotContain(game.Ko(B), u => u.Piece == ChessPiece.King); // le roi ne va pas chez les K.O.
    }

    [Fact]
    public void Checkmate_EndsTheGame()
    {
        // Goupelin (Psy) contre Gallame (Combat) : super efficace, échec ; les deux Tarsal bloquent le roi
        var game = Position(Fire, Psy, W, "wKa1 wQd1 bPg7 bPh7 bKh8");
        var step = Play(game, "d1", "d8");
        Assert.True(step.Check);
        Assert.Equal((W, ChessEnd.Checkmate), (step.Winner, step.End));
        Assert.Contains("Échec et mat !", step.Text);
    }

    [Fact]
    public void NoCheck_WhenThePieceCannotEatTheKing()
    {
        // Boumata (Dragon) ne peut rien contre Tokorico (Fée) : pas d'échec, et le roi peut marcher sur sa colonne
        var game = Position(Fire, Electric, B, "wKa1 wRe1 bKe8");
        Assert.False(game.InCheck(B));
        Assert.Contains(game.MovesFrom(Sq("e8")), m => m.To == Sq("e7"));
    }

    [Fact]
    public void PinnedPiece_CannotLeaveItsKingExposed()
    {
        // Magnézone (Acier) peut manger Dracaufeu (Vol) : Boumata, entre eux, ne quitte pas la colonne
        var game = Position(Fire, Electric, W, "wKe1 wRe4 bRe8 bKa8");
        var moves = game.MovesFrom(Sq("e4"));
        Assert.DoesNotContain(moves, m => m.To == Sq("a4"));
        Assert.Contains(moves, m => m.To == Sq("e5"));
        Assert.DoesNotContain(moves, m => m.To == Sq("e8")); // Dragon contre Acier : pas très efficace
    }

    [Fact]
    public void Castling_DependsOnWhatCanEatTheKing()
    {
        // Kaorine (Sol) ne peut rien contre Dracaufeu (Vol) : f1 n'est pas menacée, le petit roque est permis
        var free = Position(Fire, Psy, W, "wKe1 wRh1 bRf8 bKa8");
        var step = Play(free, "e1", "g1");
        Assert.Equal((Sq("h1"), Sq("f1")), (step.RookFrom, step.RookTo));
        Assert.Equal(ChessPiece.Rook, free.At(Sq("f1"))!.Piece);

        // Magnézone (Acier) le pourrait : pas de roque à travers f1
        var guarded = Position(Fire, Electric, W, "wKe1 wRh1 bRf8 bKa8");
        Assert.DoesNotContain(guarded.MovesFrom(Sq("e1")), m => m.To == Sq("g1"));
    }

    [Fact]
    public void EnPassant_FollowsTheTypes()
    {
        var game = Position(Fire, Psy, B, "wKa1 wPe5 bPd7 bKh8");
        Play(game, "d7", "d5");
        var step = Play(game, "e5", "d6");
        Assert.Equal(Sq("d5"), step.CapturedSquare);
        Assert.Null(game.At(Sq("d5")));
        Assert.Equal("Salamèche", game.At(Sq("d6"))!.Mon.Name);
    }

    [Fact]
    public void PawnAtTheEnd_SwapsWithAKnockedOutPokemon()
    {
        var game = Position(Fire, Psy, W, "wKa1 wPb7 bKh1", ko: [(W, ChessPiece.Queen), (W, ChessPiece.Pawn)]);
        var target = Assert.Single(game.Targets(Sq("b7")), t => t.To == Sq("b8"));
        var queen = Assert.Single(target.Revives); // le pion K.O. ne revient pas
        Assert.Null(game.Play(new ChessMove(Sq("b7"), Sq("b8")))); // il faut choisir le Pokémon qui revient
        var step = Play(game, "b7", "b8", queen);
        Assert.Equal(queen, step.RevivedId);
        Assert.Equal("Goupelin", game.At(Sq("b8"))!.Mon.Name);
        Assert.Contains(game.Ko(W), u => u.Id == step.UnitId);
        Assert.DoesNotContain(game.Ko(W), u => u.Id == queen);
    }

    [Fact]
    public void PawnAtTheEnd_WaitsForAKnockedOutPokemon()
    {
        var game = Position(Fire, Psy, W, "wKa1 wPb7 wRd4 bRd8 bKh1");
        Play(game, "b7", "b8"); // aucun K.O. : le pion attend
        Assert.Equal(ChessPiece.Pawn, game.At(Sq("b8"))!.Piece);
        Play(game, "d8", "d4"); // Kaorine (Sol) mange Boumata (Dragon) : neutre
        var swaps = game.SwapsFrom(Sq("b8"));
        var rook = Assert.Single(swaps);
        var step = Play(game, "b8", "b8", rook);
        Assert.True(step.Move.IsSwap);
        Assert.Equal("Boumata", game.At(Sq("b8"))!.Mon.Name);
    }

    [Fact]
    public void Playback_CountsTheWalkToTheKnockedOut()
    {
        // Kaorine mange Boumata en d4 : Boumata marche jusqu'à la première place des K.O. blancs. Vu des blancs (306 px) :
        // demi-case, 158 px jusqu'à l'espace à droite du plateau, trois lignes vers le haut, 20 px, demi-case ; vu des
        // noirs (plateau retourné, K.O. blancs à gauche) : deux lignes seulement mais 52 px dans le panneau, 306 px aussi
        var game = Position(Fire, Psy, B, "wKa1 wRd4 bRd8 bKh8");
        var capture = Play(game, "d8", "d4");
        Assert.Equal(306.0 / 32, ChessTiming.KoWalkSquares(Sq("d4"), W, 0));
        Assert.Equal(ChessTiming.WalkPerSquare * (4 + 306.0 / 32) + ChessTiming.Strike + ChessTiming.Margin, ChessTiming.Playback(capture));

        // Le pion en b8 échange sa place avec Boumata : la plus longue marche est celle vue des noirs (b8 en bas, la place
        // en haut : 498 px), le revenu marche en même temps
        var waiting = Position(Fire, Psy, W, "wKa1 wPb8 bKh8", ko: [(W, ChessPiece.Rook)]);
        var swap = Play(waiting, "b8", "b8", Assert.Single(waiting.SwapsFrom(Sq("b8"))));
        Assert.True(swap.Move.IsSwap);
        Assert.Equal(0, swap.Squares);
        Assert.Equal(ChessTiming.WalkPerSquare * (498.0 / 32) + ChessTiming.Margin, ChessTiming.Playback(swap));
    }

    [Fact]
    public void Stalemate_IsADraw()
    {
        var game = Position(Fire, Psy, W, "wKa1 wQg1 bKh8");
        var step = Play(game, "g1", "g6");
        Assert.Equal((null, ChessEnd.Stalemate), (step.Winner, step.End));
    }

    [Fact]
    public void OnlyKings_IsADraw()
    {
        var game = Position(Fire, Psy, W, "wKe1 bPd2 bKe8");
        var step = Play(game, "e1", "d2");
        Assert.Equal(ChessEnd.OnlyKings, step.End);
        Assert.Null(step.Winner);
    }

    [Fact]
    public void Resign_GivesTheWinToTheOther()
    {
        var game = new ChessGame(Fire, Psy);
        game.Resign(W, ChessEnd.Timeout);
        Assert.Equal((B, ChessEnd.Timeout), (game.Winner, game.End));
        Assert.Empty(game.LegalMoves());
    }
}
