namespace MonsutaPotaru.Data;

/// <summary>
/// Partie d'échecs Pokémon terminée (Mini-jeux → Échecs), pour les statistiques des joueurs : qui, quand, avec quelles
/// équipes, en combien de coups, comment elle s'est finie et le vainqueur (null : nulle). Écrite à la fin de la partie, y
/// compris par abandon ou temps écoulé ; pas pour une invitation refusée ou annulée. Jamais modifiée ; sans clé étrangère
/// vers les comptes, comme <see cref="BattleRecord"/>.
/// </summary>
public class ChessRecord
{
    public int Id { get; set; }
    public DateTime At { get; set; }

    /// <summary>L'hôte (celui qui a invité), qui joue les blancs.</summary>
    public string WhiteId { get; set; } = "";
    public string BlackId { get; set; } = "";

    /// <summary>Identifiants des équipes (<c>ChessTeams.All</c> : « psy », « feu »…).</summary>
    public string WhiteTeam { get; set; } = "";
    public string BlackTeam { get; set; } = "";

    /// <summary>Coups joués (tours bonus et passes compris).</summary>
    public int Moves { get; set; }

    /// <summary>
    /// Pièces mangées par les blancs, par les noirs (roi et prise en passant compris ; l'échange d'un pion au bout du
    /// plateau n'en est pas une). 0 pour les parties d'avant la migration <c>ChessCaptures</c> (2026-11-22).
    /// </summary>
    public int WhiteCaptures { get; set; }
    public int BlackCaptures { get; set; }

    /// <summary>Fin de la partie (<c>ChessEnd</c> : « Checkmate », « KingEaten », « Timeout »...).</summary>
    public string End { get; set; } = "";

    /// <summary><see cref="WhiteId"/> ou <see cref="BlackId"/> ; null pour une nulle.</summary>
    public string? WinnerId { get; set; }
}
