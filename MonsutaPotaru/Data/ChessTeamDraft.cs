namespace MonsutaPotaru.Data;

/// <summary>
/// Brouillon d'équipe d'échecs (administration → « Brouillons d'échecs ») : une équipe proposée par un administrateur pour
/// essayer l'équilibrage, jamais jouée. Les parties ne lisent que <c>ChessTeams.All</c> (le code) : un brouillon équilibré
/// est recopié dans le code à la main. Commun à tous les administrateurs.
/// </summary>
public class ChessTeamDraft
{
    public int Id { get; set; }

    /// <summary>Note libre pour s'y retrouver (« Gallame sans Combat »), 100 caractères au plus ; vide permis.</summary>
    public string Note { get; set; } = "";

    /// <summary>
    /// Identifiant de l'équipe du jeu que ce brouillon modifie (« psy ») : il la remplace dans l'équilibrage. Null : une
    /// nouvelle équipe, ajoutée aux autres.
    /// </summary>
    public string? BaseTeam { get; set; }

    /// <summary>
    /// Les Pokémon, en JSON (<c>ChessDraftMember</c> : case, espèce, forme, type) ; les cases vides n'y sont pas.
    /// </summary>
    public string Members { get; set; } = "[]";

    /// <summary>Compté dans l'équilibrage (case « Avec les brouillons ») ; un seul brouillon compté par équipe du jeu.</summary>
    public bool Counted { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>Administrateur qui l'a modifié en dernier (pas de clé étrangère : un compte supprimé ne l'efface pas).</summary>
    public string UpdatedBy { get; set; } = "";
}
