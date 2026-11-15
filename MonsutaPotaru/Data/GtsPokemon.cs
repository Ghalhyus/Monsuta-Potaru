namespace MonsutaPotaru.Data;

/// <summary>
/// État d'un Pokémon de la GTS : en ligne (une offre), ou dans la boîte de réception de son propriétaire, en attendant
/// qu'il le récupère (reçu en échange, offre expirée, annulée, ou retirée avec la sauvegarde dont il venait).
/// </summary>
public enum GtsStatus : byte { Offered, Received, Expired, Cancelled, SaveDeleted }

/// <summary>
/// Un Pokémon confié à la GTS (Multijoueurs → GTS) : il a quitté la collection de son propriétaire (stockage interne ou
/// sauvegarde stockée) au dépôt. Tant que <see cref="Status"/> vaut <see cref="GtsStatus.Offered"/>, c'est une offre : ce
/// Pokémon contre celui décrit par les colonnes <c>Wanted…</c>. Ensuite, c'est une case de la boîte de réception : le
/// Pokémon reçu en échange (les octets sont remplacés), ou le Pokémon déposé, rendu. Il compte dans la taille de la
/// collection et dans les doublons tant qu'il est ici (<see cref="Services.CollectionCapacity"/>,
/// <see cref="Services.PokemonFingerprint"/>).
/// </summary>
public class GtsPokemon
{
    public int Id { get; set; }

    public string OwnerId { get; set; } = "";
    public ApplicationUser? Owner { get; set; }

    public GtsStatus Status { get; set; }

    // ---------- Le Pokémon (octets bruts déchiffrés, comme BankPokemon), et de quoi l'afficher ----------
    public byte[] Data { get; set; } = [];
    /// <summary>PKHeX.Core.EntityContext du fichier.</summary>
    public byte Context { get; set; }
    public string Extension { get; set; } = "";
    public ushort Species { get; set; }
    public byte Form { get; set; }
    public string SpeciesName { get; set; } = "";
    public string Nickname { get; set; } = "";
    public byte Level { get; set; }
    /// <summary>0 mâle, 1 femelle, 2 aucun.</summary>
    public byte Gender { get; set; }
    public int HeldItem { get; set; }
    public bool IsShiny { get; set; }
    public bool IsLegal { get; set; }
    /// <summary>Empreinte (<see cref="Services.PokemonFingerprint"/>) : doublons du compte.</summary>
    public string Fingerprint { get; set; } = "";

    // ---------- L'offre ----------
    /// <summary>
    /// Jeux qui peuvent accepter l'offre (<see cref="Services.TradeFamily"/>) : ceux de la sauvegarde d'où vient le
    /// Pokémon, ou de son format s'il sort du stockage interne.
    /// </summary>
    public byte FamilyContext { get; set; }
    public byte FamilyRegion { get; set; }
    /// <summary>D'où vient le Pokémon déposé : « Noire — Ghal », ou « Stockage interne ».</summary>
    public string Origin { get; set; } = "";
    /// <summary>
    /// Sauvegarde stockée d'où vient le Pokémon déposé : sa suppression retire l'offre (le Pokémon va dans la boîte de
    /// réception). Null s'il sort du stockage interne, ou une fois l'offre terminée.
    /// </summary>
    public int? StoredSaveId { get; set; }
    public StoredSave? StoredSave { get; set; }

    /// <summary>Espèce demandée en échange.</summary>
    public ushort WantedSpecies { get; set; }
    /// <summary>Niveaux minimal et maximal demandés, compris ; null : pas de borne.</summary>
    public byte? WantedMinLevel { get; set; }
    public byte? WantedMaxLevel { get; set; }
    /// <summary>Sexe demandé : 0 mâle, 1 femelle ; null : peu importe.</summary>
    public byte? WantedGender { get; set; }
    /// <summary>Chromatique demandé ou refusé ; null : peu importe.</summary>
    public bool? WantedShiny { get; set; }
    /// <summary>Refuse les Pokémon que l'analyse de légalité de PKHeX signale.</summary>
    public bool WantedLegalOnly { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }

    // ---------- La boîte de réception ----------
    /// <summary>Arrivée dans la boîte de réception (fin de l'offre).</summary>
    public DateTime? ArrivedAt { get; set; }
    /// <summary>Ce qui s'est passé, prêt à afficher (« Reçu de Sacha en échange de ton Kadabra. »).</summary>
    public string? Note { get; set; }
}
