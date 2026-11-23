namespace MonsutaPotaru.Data;

/// <summary>
/// Un joueur d'une partie de cache-cache terminée (Mini-jeux → Cache-cache), pour les statistiques : une ligne par joueur
/// (invités sans compte compris, « invite-… »), reliées par <see cref="GameId"/>. Écrite à la fin de la partie (tous les
/// tours joués, ou un seul joueur restant) ; pas pour une partie annulée ou arrêtée par le départ de l'hôte. Jamais
/// modifiée ; sans clé étrangère vers les comptes, comme <see cref="ChessRecord"/>.
/// </summary>
public class HideSeekRecord
{
    public int Id { get; set; }

    /// <summary>La partie (les lignes de ses joueurs ont le même).</summary>
    public Guid GameId { get; set; }

    public DateTime At { get; set; }
    public string UserId { get; set; } = "";

    /// <summary>Cibles trouvées par ce joueur.</summary>
    public int Found { get; set; }

    /// <summary>Tours joués dans la partie (moins que prévu si elle s'est arrêtée faute de joueurs).</summary>
    public int Rounds { get; set; }

    /// <summary>Résultat (<c>MiniGameOutcome</c> : « Won », « Lost », « Draw ») ; un joueur parti en cours de route a perdu.</summary>
    public string Outcome { get; set; } = "";
}
