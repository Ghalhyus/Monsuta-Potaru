namespace MonsutaPotaru.Data;

/// <summary>
/// Suivi des rubans d'un Pokémon (« Défis » → « Rubans »). Le Pokémon est retrouvé par son empreinte
/// (<see cref="BankPokemon.Fingerprint"/>), pas par sa ligne : celles d'une sauvegarde stockée sont recréées à chaque mise
/// à jour, et il peut passer d'une sauvegarde au stockage interne. On garde son dernier état connu (octets et de quoi
/// l'afficher) : s'il quitte le compte (échangé, retiré vers une partie non stockée), le suivi reste avec ses derniers
/// rubans, et se remet à jour s'il revient. Un suivi par Pokémon et par compte.
/// </summary>
public class RibbonTracker
{
    public int Id { get; set; }

    public string OwnerId { get; set; } = "";
    public ApplicationUser? Owner { get; set; }

    /// <summary>Empreinte du Pokémon suivi (jamais vide : un Pokémon sans empreinte ne peut pas être suivi).</summary>
    public string Fingerprint { get; set; } = "";

    // Dernier état connu du Pokémon (mêmes champs que BankPokemon), mis à jour quand il change dans le compte.
    public byte[] Data { get; set; } = [];
    public byte Context { get; set; }
    public ushort Species { get; set; }
    public byte Form { get; set; }
    public string SpeciesName { get; set; } = "";
    public string Nickname { get; set; } = "";
    public byte Level { get; set; }
    public bool IsShiny { get; set; }
    public byte? Gender { get; set; }
    public int? HeldItem { get; set; }

    public DateTime CreatedAt { get; set; }
}
