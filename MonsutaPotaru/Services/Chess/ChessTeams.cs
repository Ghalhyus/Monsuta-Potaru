using PKHeX.Core;

namespace MonsutaPotaru.Services;

/// <summary>Les six pièces des échecs.</summary>
public enum ChessPiece { Pawn, Rook, Knight, Bishop, Queen, King }

/// <summary>Les deux camps : l'hôte joue les blancs, l'invité les noirs.</summary>
public enum ChessColor { White, Black }

/// <summary>
/// Le Pokémon d'une pièce : espèce et forme (pour ses images), nom affiché, son seul type (un Pokémon à deux types n'en
/// garde qu'un) et la pièce qu'il joue.
/// </summary>
public sealed record ChessMon(ushort Species, byte Form, string Name, PokeType Type, ChessPiece Piece)
{
    /// <summary>Image fixe (« images/pokemon/26-alola.png ») : choix des équipes, fenêtre « Pièces ».</summary>
    public string SpriteUrl => PokemonSprites.Url(Species, Form, EntityContext.Gen9, false, 0, false);

    /// <summary>Dossier des sprites animés Donjon Mystère (« images/donjon/0026/0001/ ») : le plateau.</summary>
    public string DonjonFolder => PokemonSprites.DonjonFolder(Species, Form, EntityContext.Gen9, false, false, 0);
}

/// <summary>Une équipe toute faite : un Pokémon par pièce.</summary>
public sealed record ChessTeam(string Id, string Name, IReadOnlyList<ChessMon> Members)
{
    public ChessMon Of(ChessPiece piece) => Members.First(m => m.Piece == piece);

    /// <summary>Nom dans une phrase (« l'équipe Feu »).</summary>
    public string InSentence => Name.Replace("Équipe", "équipe", StringComparison.Ordinal);
}

/// <summary>
/// Les équipes par défaut (choisies par l'utilisateur, le 2026-11-22) ; les joueurs ne font pas encore les leurs. Deux
/// joueurs d'une partie ne peuvent pas prendre la même (les pièces d'un même type ne se mangent souvent pas entre elles :
/// la partie serait bloquée).
/// </summary>
public static class ChessTeams
{
    public static readonly IReadOnlyList<ChessTeam> All =
    [
        new("psy", "Équipe Psy",
        [
            new(280, 0, "Tarsal", PokeType.Psychic, ChessPiece.Pawn),
            new(344, 0, "Kaorine", PokeType.Ground, ChessPiece.Rook),
            new(528, 0, "Rhinolove", PokeType.Flying, ChessPiece.Knight),
            new(199, 0, "Roigada", PokeType.Water, ChessPiece.Bishop),
            new(282, 0, "Gardevoir", PokeType.Fairy, ChessPiece.Queen),
            new(475, 0, "Gallame", PokeType.Fighting, ChessPiece.King),
        ]),
        new("feu", "Équipe Feu",
        [
            new(4, 0, "Salamèche", PokeType.Fire, ChessPiece.Pawn),
            new(776, 0, "Boumata", PokeType.Dragon, ChessPiece.Rook),
            new(229, 0, "Démolosse", PokeType.Dark, ChessPiece.Knight),
            new(937, 0, "Malvalame", PokeType.Ghost, ChessPiece.Bishop),
            new(655, 0, "Goupelin", PokeType.Psychic, ChessPiece.Queen),
            new(6, 0, "Dracaufeu", PokeType.Flying, ChessPiece.King),
        ]),
        new("electrique", "Équipe Électrik",
        [
            new(25, 0, "Pikachu", PokeType.Electric, ChessPiece.Pawn),
            new(462, 0, "Magnézone", PokeType.Steel, ChessPiece.Rook),
            new(596, 0, "Mygavolt", PokeType.Bug, ChessPiece.Knight),
            new(479, 2, "Motisma Lavage", PokeType.Water, ChessPiece.Bishop),
            new(26, 1, "Raichu d'Alola", PokeType.Psychic, ChessPiece.Queen),
            new(785, 0, "Tokorico", PokeType.Fairy, ChessPiece.King),
        ]),
    ];

    /// <summary>L'équipe de cet identifiant (valeur venue du navigateur : null si elle n'existe pas).</summary>
    public static ChessTeam? Find(string? id) => All.FirstOrDefault(t => t.Id == id);

    /// <summary>Nom français de la pièce (« Cavalier »).</summary>
    public static string PieceName(ChessPiece piece) => piece switch
    {
        ChessPiece.Pawn => "Pion",
        ChessPiece.Rook => "Tour",
        ChessPiece.Knight => "Cavalier",
        ChessPiece.Bishop => "Fou",
        ChessPiece.Queen => "Reine",
        _ => "Roi",
    };

    /// <summary>Symbole de la pièce (♙ ♖ ♘ ♗ ♕ ♔), le même pour les deux camps.</summary>
    public static string Symbol(ChessPiece piece) => piece switch
    {
        ChessPiece.Pawn => "♙",
        ChessPiece.Rook => "♖",
        ChessPiece.Knight => "♘",
        ChessPiece.Bishop => "♗",
        ChessPiece.Queen => "♕",
        _ => "♔",
    };
}
