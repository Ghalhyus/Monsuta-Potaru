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

        var super = ChessBalance.Mark(water, ChessPiece.King, mixed, ChessPiece.Pawn);
        Assert.Equal((ChessEat.Super, 2d), (super.Eat, super.Effect));
        Assert.Equal("eau-King mange mix-Pawn et rejoue : c'est super efficace !", super.Sentence);

        var weak = ChessBalance.Mark(mixed, ChessPiece.Pawn, water, ChessPiece.Queen);
        Assert.Equal((ChessEat.Blocked, 0.5), (weak.Eat, weak.Effect));
        Assert.Equal("mix-Pawn ne peut pas manger eau-Queen.", weak.Sentence);

        // Sans effet : bloquée comme « pas très efficace », le multiplicateur la distingue
        var none = ChessBalance.Mark(normal, ChessPiece.Rook, mixed, ChessPiece.Rook);
        Assert.Equal((ChessEat.Blocked, 0d), (none.Eat, none.Effect));

        var neutral = ChessBalance.Mark(water, ChessPiece.Rook, normal, ChessPiece.King);
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
