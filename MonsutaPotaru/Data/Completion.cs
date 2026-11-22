namespace MonsutaPotaru.Data;

/// <summary>
/// Complétion d'un compte (« Défis » → « Complétion (100 %) ») : le suivi d'une partie stockée vers le 100 %, lu dans
/// ses drapeaux (histoire, Pokédex, dresseurs, objets, rencontres : <see cref="Services.Gen1Completion"/>). Une par jeu
/// et par compte, une sauvegarde pour une seule complétion ; supprimée avec la sauvegarde. Une même sauvegarde peut
/// aussi avoir son living dex.
/// </summary>
public class Completion
{
    public int Id { get; set; }

    public string OwnerId { get; set; } = "";
    public ApplicationUser? Owner { get; set; }

    /// <summary>Jeu de la partie (les jaquettes et les noms sont ceux des living dex : <see cref="Services.LivingDexGames"/>).</summary>
    public LivingDexGame Game { get; set; }

    public int StoredSaveId { get; set; }
    public StoredSave? StoredSave { get; set; }

    public DateTime CreatedAt { get; set; }
}
