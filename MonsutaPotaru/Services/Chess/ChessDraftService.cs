using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MonsutaPotaru.Data;
using PKHeX.Core;

namespace MonsutaPotaru.Services;

/// <summary>
/// Les cases d'un brouillon d'équipe d'échecs : deux types de pion (le premier en a, c, e, g, le second en b, d, f, h),
/// chacun avec une ou deux espèces jouées à tour de rôle (comme Nidoran♂ et Nidoran♀), puis une pièce par case.
/// </summary>
public enum ChessDraftSlot { PawnA1, PawnA2, PawnB1, PawnB2, Rook, Knight, Bishop, Queen, King }

/// <summary>Le Pokémon d'une case d'un brouillon : espèce, forme et le type qu'il garde (un des siens).</summary>
public sealed record ChessDraftMember(ChessDraftSlot Slot, ushort Species, byte Form, PokeType Type)
{
    public ChessPiece Piece => ChessDraft.PieceOf(Slot);

    public ChessDexEntry? Entry => ChessDex.Find(Species, Form);

    public string Name => ChessDex.Name(Species, Form);

    public string SpriteUrl => PokemonSprites.Url(Species, Form, EntityContext.Gen9, false, 0, false);

    public ChessMon ToMon() => new(Species, Form, Name, Type, Piece);
}

/// <summary>
/// Un brouillon d'équipe d'échecs (table <see cref="ChessTeamDraft"/>), lu pour l'administration. <see cref="Team"/> :
/// l'équipe qu'il donnerait, null tant qu'une case obligatoire est vide (il n'est alors pas compté dans l'équilibrage).
/// </summary>
public sealed record ChessDraft(int Id, string Note, string? BaseTeam, bool Counted, IReadOnlyList<ChessDraftMember> Members,
    DateTime UpdatedAt)
{
    /// <summary>Cases à remplir pour que l'équipe soit jouable (la seconde espèce d'un pion est facultative).</summary>
    public static readonly ChessDraftSlot[] Required =
        [ChessDraftSlot.PawnA1, ChessDraftSlot.PawnB1, ChessDraftSlot.Rook, ChessDraftSlot.Knight, ChessDraftSlot.Bishop,
         ChessDraftSlot.Queen, ChessDraftSlot.King];

    /// <summary>Ordre d'affichage des cases : celui des listes d'équipes (roi d'abord, pions à la fin).</summary>
    public static readonly ChessDraftSlot[] DisplayOrder =
        [ChessDraftSlot.King, ChessDraftSlot.Queen, ChessDraftSlot.Rook, ChessDraftSlot.Bishop, ChessDraftSlot.Knight,
         ChessDraftSlot.PawnA1, ChessDraftSlot.PawnA2, ChessDraftSlot.PawnB1, ChessDraftSlot.PawnB2];

    public static ChessPiece PieceOf(ChessDraftSlot slot) => slot switch
    {
        ChessDraftSlot.Rook => ChessPiece.Rook,
        ChessDraftSlot.Knight => ChessPiece.Knight,
        ChessDraftSlot.Bishop => ChessPiece.Bishop,
        ChessDraftSlot.Queen => ChessPiece.Queen,
        ChessDraftSlot.King => ChessPiece.King,
        _ => ChessPiece.Pawn,
    };

    /// <summary>Nom d'une case (« Pion en a, c, e, g », « Pion en b, d, f, h (2e espèce) », « Tour »).</summary>
    public static string SlotName(ChessDraftSlot slot) => slot switch
    {
        ChessDraftSlot.PawnA1 => "Pion en a, c, e, g",
        ChessDraftSlot.PawnA2 => "Pion en a, c, e, g (2e espèce)",
        ChessDraftSlot.PawnB1 => "Pion en b, d, f, h",
        ChessDraftSlot.PawnB2 => "Pion en b, d, f, h (2e espèce)",
        _ => ChessTeams.PieceName(PieceOf(slot)),
    };

    /// <summary>« Brouillon 3 ».</summary>
    public string Title => $"Brouillon {Id}";

    public ChessDraftMember? At(ChessDraftSlot slot) => Members.FirstOrDefault(m => m.Slot == slot);

    public bool Complete => Required.All(s => At(s) is not null);

    /// <summary>L'équipe du jeu qu'il modifie (null : nouvelle équipe, ou équipe retirée du jeu depuis).</summary>
    public ChessTeam? Base => ChessTeams.Find(BaseTeam);

    /// <summary>
    /// L'équipe qu'il donnerait (identifiant « brouillon-3 ») : pions du premier type, puis du second, puis les pièces ;
    /// null s'il est incomplet.
    /// </summary>
    public ChessTeam? Team => Complete
        ? new($"brouillon-{Id}", [.. Enum.GetValues<ChessDraftSlot>().Select(At).OfType<ChessDraftMember>().Select(m => m.ToMon())],
            $"brouillon {Id}")
        : null;

    /// <summary>Nom affiché : celui de son roi (« Équipe Gallame »), ou « Équipe sans roi ».</summary>
    public string TeamName => At(ChessDraftSlot.King) is { } king ? $"Équipe {king.Name}" : "Équipe sans roi";

    /// <summary>
    /// Ce qui l'empêcherait d'entrer tel quel dans le jeu, ou mérite un coup d'œil : cases vides, images manquantes (le
    /// plateau montrerait une autre forme, ou un Pokémon ordinaire dans une partie à même équipe), espèce en double, deux
    /// pions du même type (une seule ligne « Pion »).
    /// </summary>
    public IReadOnlyList<string> Warnings()
    {
        var list = new List<string>();
        var empty = Required.Where(s => At(s) is null).Select(SlotName).ToList();
        if (empty.Count > 0)
            list.Add($"À remplir avant d'être compté dans l'équilibrage : {string.Join(", ", empty)}.");
        foreach (var m in Members.OrderBy(m => Array.IndexOf(DisplayOrder, m.Slot)))
        {
            if (!PokemonSprites.HasImage(m.Species, m.Form, EntityContext.Gen9, false))
                list.Add($"{m.Name} ({SlotName(m.Slot)}) n'a pas d'image fixe : la liste des équipes montrerait {(m.Form == 0 ? "une image vide" : "l'image de l'espèce")}.");
            if (!PokemonSprites.HasDonjonForm(m.Species, m.Form, EntityContext.Gen9, false))
                list.Add($"{m.Name} ({SlotName(m.Slot)}) n'a pas de sprites animés : le plateau ne peut pas le montrer{(m.Form == 0 ? "" : " (il montrerait la forme de base)")}.");
            else if (!PokemonSprites.HasDonjonForm(m.Species, m.Form, EntityContext.Gen9, true))
                list.Add($"{m.Name} ({SlotName(m.Slot)}) n'a pas de sprites animés chromatiques : dans une partie où les deux joueurs ont cette équipe, il resterait ordinaire chez les noirs.");
            if (m.Entry is { } entry && !entry.Types.Contains(m.Type))
                list.Add($"{m.Name} ({SlotName(m.Slot)}) est {PokeTypes.Name(m.Type)}, qui n'est pas un de ses types.");
        }
        foreach (var twice in Members.GroupBy(m => m.Species).Where(g => g.Count() > 1))
            list.Add($"{ChessDex.Name(twice.Key, 0)} est {twice.Count()} fois dans l'équipe ({string.Join(", ", twice.Select(m => SlotName(m.Slot)))}).");
        if (At(ChessDraftSlot.PawnA1) is { } a && At(ChessDraftSlot.PawnB1) is { } b && a.Type == b.Type)
            list.Add($"Les deux pions sont {PokeTypes.Name(a.Type)} : une seule ligne « Pion » dans les listes et les tables.");
        return list;
    }

    /// <summary>
    /// Texte à coller dans la conversation avec Claude, pour l'entrer dans le jeu (<c>ChessTeams.All</c>) : chaque case
    /// avec le nom, l'espèce, la forme et le type gardé.
    /// </summary>
    public string ExportText()
    {
        var text = new StringBuilder();
        text.Append($"Échecs : {Title}, {TeamName}");
        text.Append(Base is { } b ? $", modifie l'{b.InSentence} (identifiant « {b.Id} »)" : ", nouvelle équipe");
        text.AppendLine(Note.Length > 0 ? $" ; note : {Note}" : "");
        foreach (var slot in DisplayOrder)
        {
            if (At(slot) is not { } m)
            {
                if (Required.Contains(slot))
                    text.AppendLine($"- {SlotName(slot)} : (vide)");
                continue;
            }
            var form = m.Form == 0 ? "" : $", forme {m.Form}";
            text.AppendLine($"- {SlotName(slot)} : {m.Name} (n° {m.Species}{form}), {PokeTypes.Name(m.Type)}");
        }
        foreach (var warning in Warnings())
            text.AppendLine($"Attention : {warning}");
        return text.ToString().TrimEnd();
    }

    /// <summary>Les cases d'une équipe du jeu (pour partir d'elle) : ses deux lignes de pions, puis une pièce par case.</summary>
    public static IReadOnlyList<ChessDraftMember> MembersOf(ChessTeam team)
    {
        var list = new List<ChessDraftMember>();
        ChessDraftSlot[][] pawnSlots = [[ChessDraftSlot.PawnA1, ChessDraftSlot.PawnA2], [ChessDraftSlot.PawnB1, ChessDraftSlot.PawnB2]];
        var pawns = team.LinesOf(ChessPiece.Pawn);
        for (var line = 0; line < Math.Min(pawns.Count, 2); line++)
            for (var i = 0; i < Math.Min(pawns[line].Members.Count, 2); i++)
                list.Add(From(pawnSlots[line][i], pawns[line].Members[i]));
        foreach (var slot in new[] { ChessDraftSlot.Rook, ChessDraftSlot.Knight, ChessDraftSlot.Bishop, ChessDraftSlot.Queen, ChessDraftSlot.King })
            list.Add(From(slot, team.Of(PieceOf(slot))));
        return list;

        static ChessDraftMember From(ChessDraftSlot slot, ChessMon mon) => new(slot, mon.Species, mon.Form, mon.Type);
    }
}

/// <summary>Résultat d'une action sur les brouillons, message prêt à afficher ; <paramref name="Id"/> : le brouillon créé.</summary>
public sealed record ChessDraftResult(bool Ok, string Message, int? Id = null);

/// <summary>
/// Les équipes d'une comparaison d'équilibrage et, pour chacune, l'équipe du jeu qu'elle remplace (elle-même pour une
/// équipe du jeu ; null pour une nouvelle équipe) : <see cref="ChessDraftService.Teams"/>.
/// </summary>
public sealed record ChessBalanceTeams(IReadOnlyList<ChessTeam> Teams, IReadOnlyDictionary<string, ChessTeam?> Origins)
{
    /// <summary>Les équipes du jeu seules.</summary>
    public static ChessBalanceTeams Game { get; } = new(ChessTeams.All, ChessTeams.All.ToDictionary(t => t.Id, t => (ChessTeam?)t));

    public ChessTeam? OriginOf(ChessTeam team) => Origins.GetValueOrDefault(team.Id);
}

/// <summary>
/// Brouillons d'équipes d'échecs (administration → « Brouillons d'échecs ») : créer, modifier, compter dans
/// l'équilibrage, supprimer. Chaque action revérifie en base que l'appelant est administrateur, et chaque valeur venue de
/// la page (case, espèce, forme, type, équipe de départ) : un <c>&lt;select&gt;</c> peut être trafiqué. Chaque
/// modification est enregistrée aussitôt.
/// </summary>
public sealed class ChessDraftService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    public const int MaxNote = 100;
    private const string Missing = "Ce brouillon n'existe plus.";

    private static readonly JsonSerializerOptions Json = new() { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };

    /// <summary>Les brouillons, du plus récent au plus ancien ; null si l'appelant n'est pas administrateur.</summary>
    public async Task<IReadOnlyList<ChessDraft>?> ListAsync(string userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        if (!await AdminService.IsAdminAsync(db, userId))
            return null;
        var rows = await db.ChessTeamDrafts.AsNoTracking().OrderByDescending(d => d.Id).ToListAsync();
        return [.. rows.Select(Read)];
    }

    /// <summary>
    /// Les équipes à comparer : celles du jeu, où chaque brouillon compté et complet remplace l'équipe qu'il modifie (à sa
    /// place), puis les nouvelles équipes comptées.
    /// </summary>
    public static ChessBalanceTeams Teams(IEnumerable<ChessDraft> drafts)
    {
        var counted = drafts.Where(d => d.Counted && d.Team is not null).OrderBy(d => d.Id).ToList();
        var teams = new List<ChessTeam>();
        var origins = new Dictionary<string, ChessTeam?>();
        foreach (var game in ChessTeams.All)
        {
            var team = counted.FirstOrDefault(d => d.BaseTeam == game.Id)?.Team ?? game;
            teams.Add(team);
            origins[team.Id] = game;
        }
        foreach (var draft in counted.Where(d => d.Base is null))
        {
            teams.Add(draft.Team!);
            origins[draft.Team!.Id] = null;
        }
        return new(teams, origins);
    }

    /// <summary>Nouveau brouillon : vide, ou copie de l'équipe du jeu <paramref name="baseTeam"/> (qu'il modifiera).</summary>
    public async Task<ChessDraftResult> CreateAsync(string userId, string? baseTeam)
    {
        ChessTeam? team = null;
        if (!string.IsNullOrEmpty(baseTeam) && (team = ChessTeams.Find(baseTeam)) is null)
            return new(false, "Cette équipe n'existe pas.");
        await using var db = await dbFactory.CreateDbContextAsync();
        if (!await AdminService.IsAdminAsync(db, userId))
            return new(false, AdminService.NotAdminMessage);
        var now = DateTime.UtcNow;
        var row = new ChessTeamDraft
        {
            BaseTeam = team?.Id,
            Members = Write(team is null ? [] : ChessDraft.MembersOf(team)),
            CreatedAt = now,
            UpdatedAt = now,
            UpdatedBy = userId,
        };
        db.ChessTeamDrafts.Add(row);
        await db.SaveChangesAsync();
        return new(true, team is null
            ? $"Brouillon {row.Id} créé : choisis ses Pokémon."
            : $"Brouillon {row.Id} créé d'après l'{team.InSentence} : il la remplacera dans l'équilibrage.", row.Id);
    }

    /// <summary>Copie d'un brouillon (même équipe modifiée, mêmes Pokémon), pas comptée : pour essayer une variante.</summary>
    public async Task<ChessDraftResult> DuplicateAsync(string userId, int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        if (!await AdminService.IsAdminAsync(db, userId))
            return new(false, AdminService.NotAdminMessage);
        if (await db.ChessTeamDrafts.FindAsync(id) is not { } source)
            return new(false, Missing);
        var now = DateTime.UtcNow;
        var row = new ChessTeamDraft
        {
            BaseTeam = source.BaseTeam,
            Note = source.Note,
            Members = source.Members,
            CreatedAt = now,
            UpdatedAt = now,
            UpdatedBy = userId,
        };
        db.ChessTeamDrafts.Add(row);
        await db.SaveChangesAsync();
        return new(true, $"Brouillon {row.Id} : copie du brouillon {id}, pas encore compté dans l'équilibrage.", row.Id);
    }

    /// <summary>
    /// Met ce Pokémon dans cette case, avec ce type (un des siens ; null : le type qu'avait la case s'il est encore un des
    /// siens, sinon son premier). La seconde espèce d'un pion prend le type de la première.
    /// </summary>
    public async Task<ChessDraftResult> SetMemberAsync(string userId, int id, ChessDraftSlot slot, ushort species, byte form, PokeType? type)
    {
        if (!Enum.IsDefined(slot))
            return new(false, "Cette case n'existe pas.");
        if (ChessDex.Find(species, form) is not { } entry)
            return new(false, "Ce Pokémon n'existe pas.");
        if (type is { } wanted && !entry.Types.Contains(wanted))
            return new(false, $"{entry.Name} n'est pas de type {PokeTypes.Name(wanted)}.");
        return await ChangeAsync(userId, id, members =>
        {
            var first = Partner(slot) is { } p && IsSecond(slot) ? members.FirstOrDefault(m => m.Slot == p) : null;
            if (IsSecond(slot) && first is null)
                return (null, "Choisis d'abord la première espèce de ce pion.");
            var current = members.FirstOrDefault(m => m.Slot == slot);
            var kept = type
                ?? (first is not null && entry.Types.Contains(first.Type) ? first.Type : (PokeType?)null)
                ?? (current is not null && entry.Types.Contains(current.Type) ? current.Type : entry.Types[0]);
            if (first is not null && kept != first.Type)
                return (null, $"La 2e espèce d'un pion a le type de la première ({PokeTypes.Name(first.Type)}) : {entry.Name} ne l'a pas.");
            var list = members.Where(m => m.Slot != slot).Append(new ChessDraftMember(slot, species, form, kept)).ToList();
            // La première espèce d'un pion change de type : la seconde n'est plus du même type, elle est retirée.
            if (!IsSecond(slot) && Partner(slot) is { } second && list.FirstOrDefault(m => m.Slot == second) is { } other && other.Type != kept)
            {
                list.Remove(other);
                return (list, $"{ChessDraft.SlotName(slot)} : {entry.Name} ({PokeTypes.Name(kept)}). {other.Name} retiré : la 2e espèce doit avoir le même type.");
            }
            return (list, $"{ChessDraft.SlotName(slot)} : {entry.Name} ({PokeTypes.Name(kept)}).");
        });
    }

    /// <summary>Vide une case ; vider la première espèce d'un pion y remonte la seconde.</summary>
    public Task<ChessDraftResult> ClearMemberAsync(string userId, int id, ChessDraftSlot slot) =>
        ChangeAsync(userId, id, members =>
        {
            if (members.FirstOrDefault(m => m.Slot == slot) is not { } removed)
                return (members, "Cette case est déjà vide.");
            var list = members.Where(m => m.Slot != slot).ToList();
            if (!IsSecond(slot) && Partner(slot) is { } second && list.FirstOrDefault(m => m.Slot == second) is { } other)
            {
                list.Remove(other);
                list.Add(other with { Slot = slot });
            }
            return (list, $"{removed.Name} retiré ({ChessDraft.SlotName(slot)}).");
        });

    /// <summary>Change la note (100 caractères au plus, espaces autour retirés).</summary>
    public async Task<ChessDraftResult> SetNoteAsync(string userId, int id, string? note)
    {
        var text = (note ?? "").Trim();
        if (text.Length > MaxNote)
            return new(false, $"La note fait {MaxNote} caractères au plus.");
        return await UpdateAsync(userId, id, row =>
        {
            row.Note = text;
            return (true, "Note enregistrée.");
        });
    }

    /// <summary>
    /// Compte le brouillon dans l'équilibrage (ou plus) : un seul par équipe du jeu, compter celui-ci décompte les autres
    /// brouillons de la même équipe.
    /// </summary>
    public async Task<ChessDraftResult> SetCountedAsync(string userId, int id, bool counted)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        if (!await AdminService.IsAdminAsync(db, userId))
            return new(false, AdminService.NotAdminMessage);
        if (await db.ChessTeamDrafts.FindAsync(id) is not { } row)
            return new(false, Missing);
        row.Counted = counted;
        var others = new List<ChessTeamDraft>();
        if (counted && ChessTeams.Find(row.BaseTeam) is not null)
        {
            others = await db.ChessTeamDrafts.Where(d => d.Id != id && d.Counted && d.BaseTeam == row.BaseTeam).ToListAsync();
            others.ForEach(o => o.Counted = false);
        }
        await db.SaveChangesAsync();
        var draft = Read(row);
        if (!counted)
            return new(true, $"{draft.Title} n'est plus compté dans l'équilibrage.");
        var message = draft.Complete
            ? $"{draft.Title} est compté dans l'équilibrage."
            : $"{draft.Title} sera compté dans l'équilibrage une fois complet.";
        if (others.Count > 0)
            message += $" {string.Join(", ", others.Select(o => $"Brouillon {o.Id}"))} ne l'est plus (un seul par équipe du jeu).";
        return new(true, message);
    }

    public async Task<ChessDraftResult> DeleteAsync(string userId, int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        if (!await AdminService.IsAdminAsync(db, userId))
            return new(false, AdminService.NotAdminMessage);
        var deleted = await db.ChessTeamDrafts.Where(d => d.Id == id).ExecuteDeleteAsync();
        return deleted == 0 ? new(false, Missing) : new(true, $"Brouillon {id} supprimé.");
    }

    /// <summary>Seconde espèce d'un pion ?</summary>
    private static bool IsSecond(ChessDraftSlot slot) => slot is ChessDraftSlot.PawnA2 or ChessDraftSlot.PawnB2;

    /// <summary>L'autre case du même pion (A1 ↔ A2, B1 ↔ B2) ; null pour une pièce.</summary>
    private static ChessDraftSlot? Partner(ChessDraftSlot slot) => slot switch
    {
        ChessDraftSlot.PawnA1 => ChessDraftSlot.PawnA2,
        ChessDraftSlot.PawnA2 => ChessDraftSlot.PawnA1,
        ChessDraftSlot.PawnB1 => ChessDraftSlot.PawnB2,
        ChessDraftSlot.PawnB2 => ChessDraftSlot.PawnB1,
        _ => null,
    };

    /// <summary>Change les Pokémon : <paramref name="change"/> rend la nouvelle liste (null : refus) et le message.</summary>
    private Task<ChessDraftResult> ChangeAsync(string userId, int id,
        Func<IReadOnlyList<ChessDraftMember>, (IReadOnlyList<ChessDraftMember>? Members, string Message)> change) =>
        UpdateAsync(userId, id, row =>
        {
            var (members, message) = change(ReadMembers(row.Members));
            if (members is null)
                return (false, message);
            row.Members = Write(members);
            return (true, message);
        });

    /// <summary>Modifie la ligne : <paramref name="update"/> rend faux pour un refus (rien n'est enregistré) et le message.</summary>
    private async Task<ChessDraftResult> UpdateAsync(string userId, int id, Func<ChessTeamDraft, (bool Ok, string Message)> update)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        if (!await AdminService.IsAdminAsync(db, userId))
            return new(false, AdminService.NotAdminMessage);
        if (await db.ChessTeamDrafts.FindAsync(id) is not { } row)
            return new(false, Missing);
        var (ok, message) = update(row);
        if (!ok)
            return new(false, message);
        row.UpdatedAt = DateTime.UtcNow;
        row.UpdatedBy = userId;
        await db.SaveChangesAsync();
        return new(true, message);
    }

    private static ChessDraft Read(ChessTeamDraft row) =>
        new(row.Id, row.Note, row.BaseTeam, row.Counted, ReadMembers(row.Members), row.UpdatedAt);

    /// <summary>Pokémon du JSON, dans l'ordre des cases ; une case en double ou un Pokémon inconnu est ignoré.</summary>
    private static IReadOnlyList<ChessDraftMember> ReadMembers(string json)
    {
        List<ChessDraftMember> members;
        try
        {
            members = JsonSerializer.Deserialize<List<ChessDraftMember>>(json, Json) ?? [];
        }
        catch (JsonException)
        {
            members = [];
        }
        return [.. members.Where(m => Enum.IsDefined(m.Slot) && ChessDex.Find(m.Species, m.Form) is not null)
            .GroupBy(m => m.Slot).Select(g => g.First()).OrderBy(m => m.Slot)];
    }

    private static string Write(IEnumerable<ChessDraftMember> members) =>
        JsonSerializer.Serialize(members.OrderBy(m => m.Slot).Select(m => new { m.Slot, m.Species, m.Form, m.Type }), Json);
}
