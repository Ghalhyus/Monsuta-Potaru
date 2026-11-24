namespace MonsutaPotaru.Services;

/// <summary>Ce qu'une pièce peut faire contre une autre, d'après leurs types (même règle que les parties).</summary>
public enum ChessEat
{
    /// <summary>Pas très efficace ou sans effet : elle ne la mange pas.</summary>
    Blocked,

    /// <summary>Neutre : elle la mange.</summary>
    Eats,

    /// <summary>Super efficace : elle la mange et rejoue.</summary>
    Super,
}

/// <summary>
/// Case d'une table de type : ce que fait la pièce qui mange (<see cref="Eat"/>), le multiplicateur des dégâts (2, 1,
/// 0,5 ou 0 : l'onglet « Échecs » de l'administration distingue « sans effet ») et la phrase affichée au survol.
/// </summary>
public sealed record ChessMark(ChessEat Eat, double Effect, string Sentence);

/// <summary>
/// Une équipe contre une autre, pondéré par le nombre de pièces (8 pions, 2 tours, 2 cavaliers, 2 fous, une reine, un
/// roi : 16 × 16 paires) : part des paires où la pièce de <c>attackers</c> peut manger celle de <c>victims</c>
/// (<see cref="Edible"/>), dont celles où c'est super efficace (<see cref="Super"/>), entre 0 et 1.
/// </summary>
public sealed record ChessTeamMatchup(double Edible, double Super);

/// <summary>
/// Équilibrage des équipes d'échecs (onglet « Échecs » de l'administration) : tout est calculé d'après
/// <see cref="ChessTeams.All"/> et la table des types, rien n'est stocké. Les Pokémon d'une même pièce ont le même
/// type : le premier suffit.
/// </summary>
public static class ChessBalance
{
    /// <summary>Nombre de pièces de ce genre dans une équipe au début de la partie.</summary>
    public static int Count(ChessPiece piece) => piece switch
    {
        ChessPiece.Pawn => 8,
        ChessPiece.Rook or ChessPiece.Knight or ChessPiece.Bishop => 2,
        _ => 1,
    };

    /// <summary>Nombre de pièces d'une équipe (16).</summary>
    public static int PiecesPerTeam => Enum.GetValues<ChessPiece>().Sum(Count);

    /// <summary>La pièce de la ligne contre celle de la colonne : prise, multiplicateur, phrase.</summary>
    public static ChessMark Mark(ChessTeam attackers, ChessPiece attackerPiece, ChessTeam victims, ChessPiece victimPiece)
    {
        var (attacker, victim) = (attackers.Of(attackerPiece).Type, victims.Of(victimPiece).Type);
        var (who, whom) = (attackers.NamesInSentence(attackerPiece), victims.NamesInSentence(victimPiece));
        var effect = PokeTypes.Effectiveness(attacker, victim);
        if (!ChessGame.CanEat(attacker, victim))
            return new(ChessEat.Blocked, effect, $"{who} ne peut pas manger {whom}.");
        return effect > 1
            ? new(ChessEat.Super, effect, $"{who} mange {whom} et rejoue : c'est super efficace !")
            : new(ChessEat.Eats, effect, $"{who} mange {whom}.");
    }

    /// <summary><paramref name="attackers"/> contre <paramref name="victims"/>, pondéré par le nombre de pièces.</summary>
    public static ChessTeamMatchup Matchup(ChessTeam attackers, ChessTeam victims)
    {
        int edible = 0, super = 0;
        foreach (var a in Enum.GetValues<ChessPiece>())
            foreach (var v in Enum.GetValues<ChessPiece>())
            {
                var weight = Count(a) * Count(v);
                var eat = Mark(attackers, a, victims, v).Eat;
                if (eat != ChessEat.Blocked)
                    edible += weight;
                if (eat == ChessEat.Super)
                    super += weight;
            }
        double pairs = PiecesPerTeam * PiecesPerTeam;
        return new(edible / pairs, super / pairs);
    }

    /// <summary>
    /// Part mangeable moyenne de cette équipe contre les sept équipes, elle comprise (deux joueurs peuvent prendre la même),
    /// ou contre les six autres seulement (<paramref name="withoutSelf"/> : case de l'onglet « Échecs »).
    /// </summary>
    public static double Average(ChessTeam team, bool withoutSelf = false)
    {
        var opponents = ChessTeams.All.Where(t => !withoutSelf || t.Id != team.Id).ToList();
        return opponents.Count == 0 ? 0 : opponents.Average(t => Matchup(team, t).Edible);
    }

    /// <summary>Solde, en points : part mangeable de <paramref name="team"/> contre <paramref name="other"/>, moins l'inverse.</summary>
    public static int Balance(ChessTeam team, ChessTeam other) =>
        Points(Matchup(team, other).Edible - Matchup(other, team).Edible);

    /// <summary>Nombre de pièces de <paramref name="attackers"/> (sur 16) qui peuvent manger le roi de <paramref name="defenders"/>.</summary>
    public static int KingThreats(ChessTeam attackers, ChessTeam defenders) =>
        Enum.GetValues<ChessPiece>()
            .Where(p => ChessGame.CanEat(attackers.Of(p).Type, defenders.Of(ChessPiece.King).Type))
            .Sum(Count);

    /// <summary>Une part (0 à 1) en points de pourcentage arrondis (0,836 → 84).</summary>
    public static int Points(double share) => (int)Math.Round(share * 100, MidpointRounding.AwayFromZero);
}
