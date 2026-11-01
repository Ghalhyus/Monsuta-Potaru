namespace MonsutaPotaru.Data;

/// <summary>
/// Pokémon de la collection installé dans l'un des parcs de son dresseur (un seul à la fois ; 20 à 80 par parc selon ses
/// agrandissements, voir <see cref="Services.ParkService"/>).
/// Supprimé avec le Pokémon : retiré de la collection, ou recréé par la mise à jour de sa sauvegarde stockée, il quitte le parc.
/// </summary>
public class ParkMember
{
    public int BankPokemonId { get; set; }
    public BankPokemon? BankPokemon { get; set; }

    /// <summary>Dresseur du Pokémon (celui de <see cref="BankPokemon"/>), pour compter et lister sans jointure.</summary>
    public string OwnerId { get; set; } = "";

    /// <summary>Parc où il vit (<see cref="Park.OwnerId"/> : le même dresseur).</summary>
    public int ParkId { get; set; }
    public Park? Park { get; set; }

    public DateTime AddedAt { get; set; }
}
