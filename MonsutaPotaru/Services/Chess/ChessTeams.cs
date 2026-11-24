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
    public string SpriteUrl => SpriteUrlOf(false);

    /// <summary>Image fixe, chromatique ou non (les noirs d'une partie où les deux joueurs ont la même équipe le sont).</summary>
    public string SpriteUrlOf(bool shiny) => PokemonSprites.Url(Species, Form, EntityContext.Gen9, false, 0, shiny);

    /// <summary>
    /// Dossier des sprites animés Donjon Mystère (« images/donjon/0026/0001/ ») : le plateau. Chromatique : le dossier
    /// chromatique s'il existe (Roserade n'en a pas : sprites ordinaires).
    /// </summary>
    public string DonjonFolderOf(bool shiny) => PokemonSprites.DonjonFolder(Species, Form, EntityContext.Gen9, shiny, false, 0);
}

/// <summary>
/// Une ligne des listes et des tables de type : une pièce d'un type, avec ses Pokémon (un seul, sauf les pions Nidoran♂ et
/// Nidoran♀ de l'équipe Nidoking, tous deux Poison : une ligne, deux images).
/// </summary>
public sealed record ChessLine(ChessPiece Piece, IReadOnlyList<ChessMon> Members)
{
    /// <summary>Le premier Pokémon de la ligne (tous ont le même type).</summary>
    public ChessMon Mon => Members[0];

    public PokeType Type => Mon.Type;

    /// <summary>Noms dans une liste (« Nidoran♂ / Nidoran♀ »).</summary>
    public string Names => string.Join(" / ", Members.Select(m => m.Name));

    /// <summary>Noms dans une phrase (« Nidoran♂ ou Nidoran♀ »).</summary>
    public string NamesInSentence => string.Join(" ou ", Members.Select(m => m.Name));

    /// <summary>Ce Pokémon est-il de cette ligne ?</summary>
    public bool Has(ChessMon mon) => Members.Contains(mon);
}

/// <summary>
/// Une équipe toute faite : un Pokémon par pièce, sauf les pions. Les pions ont deux types joués une colonne sur deux
/// (demandé le 2026-11-24 : Tarsal Psy en a, c, e, g, Lippouti Glace en b, d, f, h) ; un type peut avoir deux espèces à
/// tour de rôle (Poison : Nidoran♂ en a et e, Nidoran♀ en c et g, Toxizap Électrik en b, d, f, h). Listes et tables de type :
/// une ligne par pièce et par type (<see cref="LinesOf"/>), donc deux pour les pions. L'identifiant (« poison ») est
/// gardé en base ; le nom vient du roi (« Équipe Nidoking », demandé par l'utilisateur le 2026-11-22 : avant, le type du
/// thème, « Équipe Poison »).
/// </summary>
/// <param name="Draft">
/// Brouillon de l'administration (« brouillon 3 », ajouté au nom), jamais joué ; null pour une équipe du jeu.
/// </param>
public sealed record ChessTeam(string Id, IReadOnlyList<ChessMon> Members, string? Draft = null)
{
    /// <summary>« Équipe Nidoking » : le nom de son roi (« Équipe Nidoking (brouillon 3) » pour un brouillon).</summary>
    public string Name => Draft is null ? $"Équipe {Of(ChessPiece.King).Name}" : $"Équipe {Of(ChessPiece.King).Name} ({Draft})";

    /// <summary>Le (premier) Pokémon de cette pièce.</summary>
    public ChessMon Of(ChessPiece piece) => Members.First(m => m.Piece == piece);

    /// <summary>Tous les Pokémon de cette pièce, dans l'ordre de l'équipe (un seul, sauf les pions).</summary>
    public IReadOnlyList<ChessMon> MembersOf(ChessPiece piece) => [.. Members.Where(m => m.Piece == piece)];

    /// <summary>Les lignes de cette pièce : une par type, dans l'ordre de l'équipe (deux pour les pions, une sinon).</summary>
    public IReadOnlyList<ChessLine> LinesOf(ChessPiece piece) =>
        [.. MembersOf(piece).GroupBy(m => m.Type).Select(g => new ChessLine(piece, [.. g]))];

    /// <summary>Les lignes de ces pièces, dans cet ordre (celui des listes : <c>ChessTeamList.Order</c>).</summary>
    public IReadOnlyList<ChessLine> Lines(IEnumerable<ChessPiece> order) => [.. order.SelectMany(LinesOf)];

    /// <summary>
    /// Le pion de départ de cette colonne (0 = a) : les types à tour de rôle (une colonne sur deux), et dans un type ses
    /// espèces à tour de rôle (Nidoran♂ en a, Nidoran♀ en c, Nidoran♂ en e…).
    /// </summary>
    public ChessMon PawnAt(int file)
    {
        var lines = LinesOf(ChessPiece.Pawn);
        var line = lines[file % lines.Count];
        return line.Members[file / lines.Count % line.Members.Count];
    }

    /// <summary>Nom sans « Équipe » (« Dracaufeu ») : tuile « Équipe favorite » des statistiques.</summary>
    public string ShortName => Of(ChessPiece.King).Name;

    /// <summary>Nom dans une phrase (« l'équipe Dracaufeu »).</summary>
    public string InSentence => Name.Replace("Équipe", "équipe", StringComparison.Ordinal);
}

/// <summary>
/// Les équipes par défaut (choisies par l'utilisateur, le 2026-11-22 ; Poison, Eau, Plante et Sol ajoutées le même jour) ; les joueurs ne font pas encore les leurs. Deux
/// joueurs d'une partie ne peuvent pas prendre la même (les pièces d'un même type ne se mangent souvent pas entre elles :
/// la partie serait bloquée).
/// </summary>
public static class ChessTeams
{
    public static readonly IReadOnlyList<ChessTeam> All =
    [
        new("psy",
        [
            new(280, 0, "Tarsal", PokeType.Psychic, ChessPiece.Pawn),
            new(238, 0, "Lippouti", PokeType.Ice, ChessPiece.Pawn),
            new(344, 0, "Kaorine", PokeType.Ground, ChessPiece.Rook),
            new(528, 0, "Rhinolove", PokeType.Flying, ChessPiece.Knight),
            new(199, 0, "Roigada", PokeType.Water, ChessPiece.Bishop),
            new(282, 0, "Gardevoir", PokeType.Fairy, ChessPiece.Queen),
            new(475, 0, "Gallame", PokeType.Fighting, ChessPiece.King),
        ]),
        new("feu",
        [
            new(4, 0, "Salamèche", PokeType.Fire, ChessPiece.Pawn),
            new(58, 1, "Caninos d'Hisui", PokeType.Rock, ChessPiece.Pawn),
            new(776, 0, "Boumata", PokeType.Dragon, ChessPiece.Rook),
            new(229, 0, "Démolosse", PokeType.Dark, ChessPiece.Knight),
            new(937, 0, "Malvalame", PokeType.Ghost, ChessPiece.Bishop),
            new(655, 0, "Goupelin", PokeType.Psychic, ChessPiece.Queen),
            new(6, 0, "Dracaufeu", PokeType.Flying, ChessPiece.King),
        ]),
        new("electrique",
        [
            new(25, 0, "Pikachu", PokeType.Electric, ChessPiece.Pawn),
            new(587, 0, "Emolga", PokeType.Flying, ChessPiece.Pawn),
            new(462, 0, "Magnézone", PokeType.Steel, ChessPiece.Rook),
            new(596, 0, "Mygavolt", PokeType.Bug, ChessPiece.Knight),
            new(479, 2, "Motisma Lavage", PokeType.Water, ChessPiece.Bishop),
            new(26, 1, "Raichu d'Alola", PokeType.Psychic, ChessPiece.Queen),
            new(785, 0, "Tokorico", PokeType.Fairy, ChessPiece.King),
        ]),
        new("poison",
        [
            new(32, 0, "Nidoran♂", PokeType.Poison, ChessPiece.Pawn),
            new(29, 0, "Nidoran♀", PokeType.Poison, ChessPiece.Pawn),
            new(848, 0, "Toxizap", PokeType.Electric, ChessPiece.Pawn),
            new(748, 0, "Prédastérie", PokeType.Water, ChessPiece.Rook),
            new(758, 0, "Malamandre", PokeType.Fire, ChessPiece.Knight),
            new(94, 0, "Ectoplasma", PokeType.Ghost, ChessPiece.Bishop),
            new(31, 0, "Nidoqueen", PokeType.Ground, ChessPiece.Queen),
            new(34, 0, "Nidoking", PokeType.Poison, ChessPiece.King),
        ]),
        new("eau",
        [
            new(116, 0, "Hypotrempe", PokeType.Water, ChessPiece.Pawn),
            new(72, 0, "Tentacool", PokeType.Poison, ChessPiece.Pawn),
            new(131, 0, "Lokhlass", PokeType.Ice, ChessPiece.Rook),
            new(226, 0, "Démanta", PokeType.Flying, ChessPiece.Knight),
            new(658, 0, "Amphinobi", PokeType.Dark, ChessPiece.Bishop),
            new(730, 0, "Oratoria", PokeType.Fairy, ChessPiece.Queen),
            new(230, 0, "Hyporoi", PokeType.Dragon, ChessPiece.King),
        ]),
        new("plante",
        [
            new(387, 0, "Tortipouss", PokeType.Grass, ChessPiece.Pawn),
            new(187, 0, "Granivol", PokeType.Flying, ChessPiece.Pawn),
            new(103, 0, "Noadkoko", PokeType.Psychic, ChessPiece.Rook),
            new(547, 0, "Farfaduvet", PokeType.Fairy, ChessPiece.Knight),
            new(272, 0, "Ludicolo", PokeType.Water, ChessPiece.Bishop),
            new(407, 0, "Roserade", PokeType.Poison, ChessPiece.Queen),
            new(389, 0, "Torterra", PokeType.Ground, ChessPiece.King),
        ]),
        new("sol",
        [
            new(111, 0, "Rhinocorne", PokeType.Ground, ChessPiece.Pawn),
            new(322, 0, "Chamallot", PokeType.Fire, ChessPiece.Pawn),
            new(623, 0, "Golemastoc", PokeType.Ghost, ChessPiece.Rook),
            new(330, 0, "Libégon", PokeType.Dragon, ChessPiece.Knight),
            new(260, 0, "Laggron", PokeType.Water, ChessPiece.Bishop),
            new(472, 0, "Scorvol", PokeType.Flying, ChessPiece.Queen),
            new(464, 0, "Rhinastoc", PokeType.Rock, ChessPiece.King),
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
