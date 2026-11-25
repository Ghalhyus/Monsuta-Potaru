using MonsutaPotaru.Services;

namespace MonsutaPotaru.Tests;

/// <summary>
/// Équilibrage des équipes d'échecs (<see cref="ChessBalance"/>, onglet « Échecs » de l'administration) : marques des
/// tables, parts pondérées par le nombre de pièces, solde, rois menacés. Les valeurs se vérifient sur des équipes
/// fabriquées ici : celles du jeu changeront avec l'équilibrage.
/// </summary>
public sealed class ChessBalanceTests
{
    /// <summary>Une équipe de test : un type par pièce, des noms tirés de la pièce.</summary>
    private static ChessTeam Team(string id, PokeType pawn, PokeType others) => new(id,
        [.. Enum.GetValues<ChessPiece>().Select(p => new ChessMon(1, 0, $"{id}-{p}", p == ChessPiece.Pawn ? pawn : others, p))]);

    /// <summary>La (première) ligne de cette pièce.</summary>
    private static ChessLine L(ChessTeam team, ChessPiece piece) => team.LinesOf(piece)[0];

    [Fact]
    public void Count_SixteenPiecesPerTeam()
    {
        Assert.Equal(8, ChessBalance.Count(ChessPiece.Pawn));
        Assert.Equal(2, ChessBalance.Count(ChessPiece.Knight));
        Assert.Equal(1, ChessBalance.Count(ChessPiece.King));
        Assert.Equal(16, ChessBalance.PiecesPerTeam);
    }

    [Fact]
    public void Mark_SameRuleAsTheGame()
    {
        var water = Team("eau", PokeType.Water, PokeType.Water);
        var mixed = Team("mix", PokeType.Fire, PokeType.Ghost);
        var normal = Team("nor", PokeType.Normal, PokeType.Normal);

        var super = ChessBalance.Mark(L(water, ChessPiece.King), L(mixed, ChessPiece.Pawn));
        Assert.Equal((ChessEat.Super, 2d), (super.Eat, super.Effect));
        Assert.Equal("eau-King mange mix-Pawn et rejoue : c'est super efficace !", super.Sentence);

        var weak = ChessBalance.Mark(L(mixed, ChessPiece.Pawn), L(water, ChessPiece.Queen));
        Assert.Equal((ChessEat.Blocked, 0.5), (weak.Eat, weak.Effect));
        Assert.Equal("mix-Pawn ne peut pas manger eau-Queen.", weak.Sentence);

        // Sans effet : bloquée comme « pas très efficace », le multiplicateur la distingue
        var none = ChessBalance.Mark(L(normal, ChessPiece.Rook), L(mixed, ChessPiece.Rook));
        Assert.Equal((ChessEat.Blocked, 0d), (none.Eat, none.Effect));

        var neutral = ChessBalance.Mark(L(water, ChessPiece.Rook), L(normal, ChessPiece.King));
        Assert.Equal((ChessEat.Eats, 1d), (neutral.Eat, neutral.Effect));
        Assert.Equal("eau-Rook mange nor-King.", neutral.Sentence);
    }

    [Fact]
    public void Matchup_WeightedByTheNumberOfPieces()
    {
        // Eau partout contre pions Feu (8 pièces sur 16) et Normal ailleurs : tout se mange, la moitié en super efficace
        var water = Team("eau", PokeType.Water, PokeType.Water);
        var fire = Team("feu", PokeType.Fire, PokeType.Normal);
        Assert.Equal(new ChessTeamMatchup(1, 0.5), ChessBalance.Matchup(water, fire));
        // Dans l'autre sens, les pions Feu (la moitié des pièces) ne mangent rien, les Normal tout, sans super efficace
        Assert.Equal(new ChessTeamMatchup(0.5, 0), ChessBalance.Matchup(fire, water));

        Assert.Equal(50, ChessBalance.Balance(water, fire));
        Assert.Equal(-50, ChessBalance.Balance(fire, water));

        // Seuls les 8 Normal peuvent manger le roi Eau ; les 16 Eau, le roi Normal
        Assert.Equal(8, ChessBalance.KingThreats(fire, water));
        Assert.Equal(16, ChessBalance.KingThreats(water, fire));
    }

    [Fact]
    public void TwoPawnTypes_FourPawnsEach()
    {
        // Pions Eau et Feu une colonne sur deux, le reste Normal ; en face, tout Plante
        ChessTeam mixed = new("mix",
        [
            new(1, 0, "Eau", PokeType.Water, ChessPiece.Pawn),
            new(2, 0, "Feu", PokeType.Fire, ChessPiece.Pawn),
            .. Enum.GetValues<ChessPiece>().Where(p => p != ChessPiece.Pawn).Select(p => new ChessMon(3, 0, $"mix-{p}", PokeType.Normal, p)),
        ]);
        var grass = Team("pla", PokeType.Grass, PokeType.Grass);
        var pawns = mixed.LinesOf(ChessPiece.Pawn);
        Assert.Equal([PokeType.Water, PokeType.Fire], pawns.Select(l => l.Type));
        Assert.All(pawns, l => Assert.Equal(4, ChessBalance.Count(mixed, l)));
        Assert.Equal(7, mixed.Lines(Enum.GetValues<ChessPiece>()).Count);
        Assert.Equal(16, mixed.Lines(Enum.GetValues<ChessPiece>()).Sum(l => ChessBalance.Count(mixed, l)));

        // Contre Plante : les 4 pions Eau ne mangent rien, les 4 Feu en super efficace, les 8 Normal en neutre
        Assert.Equal(new ChessTeamMatchup(12 / 16d, 4 / 16d), ChessBalance.Matchup(mixed, grass));
        Assert.Equal(12, ChessBalance.KingThreats(mixed, grass));
        // Plante contre : les pions Eau sont mangés (×2), les Feu non, le reste en neutre
        Assert.Equal(new ChessTeamMatchup(12 / 16d, 4 / 16d), ChessBalance.Matchup(grass, mixed));
    }

    [Fact]
    public void LinesOfType_OncePerPawnType()
    {
        // Pions Eau et Feu, tour Eau, le reste Normal ; en face, tout Eau (pions compris) : un seul type de pion
        ChessTeam mixed = new("mix",
        [
            new(1, 0, "Eau", PokeType.Water, ChessPiece.Pawn),
            new(2, 0, "Feu", PokeType.Fire, ChessPiece.Pawn),
            new(3, 0, "Tour", PokeType.Water, ChessPiece.Rook),
            .. new[] { ChessPiece.Knight, ChessPiece.Bishop, ChessPiece.Queen, ChessPiece.King }
                .Select(p => new ChessMon(4, 0, $"mix-{p}", PokeType.Normal, p)),
        ]);
        var water = Team("eau", PokeType.Water, PokeType.Water);
        ChessTeam[] teams = [mixed, water];

        // Eau : pion et tour de « mix », puis les six pièces de « eau » (ses huit pions comptent pour un)
        var lines = ChessBalance.LinesOfType(teams, PokeType.Water);
        Assert.Equal(8, lines.Count);
        Assert.Equal([ChessPiece.Pawn, ChessPiece.Rook], lines.Where(l => l.Team == mixed).Select(l => l.Line.Piece));
        Assert.Equal(6, lines.Count(l => l.Team == water));
        Assert.Single(ChessBalance.LinesOfType(teams, PokeType.Fire));
        Assert.Equal(4, ChessBalance.LinesOfType(teams, PokeType.Normal).Count);
        Assert.Empty(ChessBalance.LinesOfType(teams, PokeType.Dragon));
        // Toutes les lignes, une fois chacune
        Assert.Equal(7 + 6, Enum.GetValues<PokeType>().Sum(t => ChessBalance.LinesOfType(teams, t).Count));
    }

    [Fact]
    public void Points_RoundedHalfUp()
    {
        Assert.Equal(84, ChessBalance.Points(0.836));
        Assert.Equal(13, ChessBalance.Points(0.125));
        Assert.Equal(-13, ChessBalance.Points(-0.125));
        Assert.Equal(0, ChessBalance.Points(0));
    }

    [Fact]
    public void RealTeams_ConsistentFigures()
    {
        foreach (var team in ChessTeams.All)
        {
            var others = ChessTeams.All.Where(t => t != team).ToList();
            Assert.Equal(ChessTeams.All.Average(o => ChessBalance.Matchup(team, o).Edible), ChessBalance.Average(team), 9);
            Assert.Equal(others.Average(o => ChessBalance.Matchup(team, o).Edible), ChessBalance.Average(team, withoutSelf: true), 9);
            foreach (var other in ChessTeams.All)
            {
                var m = ChessBalance.Matchup(team, other);
                Assert.InRange(m.Edible, 0, 1);
                Assert.InRange(m.Super, 0, m.Edible);
                Assert.InRange(ChessBalance.KingThreats(team, other), 0, 16);
                Assert.Equal(-ChessBalance.Balance(other, team), ChessBalance.Balance(team, other));
            }
        }
    }
}
