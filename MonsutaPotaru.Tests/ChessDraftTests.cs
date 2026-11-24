using Microsoft.EntityFrameworkCore;
using MonsutaPotaru.Data;
using MonsutaPotaru.Services;

namespace MonsutaPotaru.Tests;

/// <summary>
/// Brouillons d'équipes d'échecs (administration → « Brouillons d'échecs ») : catalogue des Pokémon (<see cref="ChessDex"/>),
/// réservé aux administrateurs, valeurs revérifiées, pions à deux espèces, un seul brouillon compté par équipe du jeu,
/// équipes de l'équilibrage, remarques et texte à copier.
/// </summary>
public sealed class ChessDraftTests : IDisposable
{
    private readonly TestDatabase _db = new("sacha", "ondine");
    private readonly ChessDraftService _drafts;

    public ChessDraftTests()
    {
        PokemonSprites.Load(RealSavesTests.WebRootWithImages()); // remarques sur les images (Roserade sans sprites chromatiques)
        _drafts = new ChessDraftService(_db.Factory);
        using var db = _db.NewContext();
        db.UserRoles.Add(new() { UserId = "sacha", RoleId = SiteRoles.AdminId });
        db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private async Task<ChessDraft> Draft(int id) => (await _drafts.ListAsync("sacha"))!.Single(d => d.Id == id);

    private async Task<int> NewDraft(string? baseTeam = null) => (await _drafts.CreateAsync("sacha", baseTeam)).Id!.Value;

    // ---------- Catalogue ----------

    [Fact]
    public void Dex_NamesFormsAndRealTypes()
    {
        Assert.Equal("Raichu d'Alola", ChessDex.Name(26, 1));
        Assert.Equal([PokeType.Electric, PokeType.Psychic], ChessDex.Find(26, 1)!.Types);
        Assert.Equal("Caninos d'Hisui", ChessDex.Name(58, 1));
        Assert.Equal("Motisma Lavage", ChessDex.Name(479, 2));
        Assert.Equal("Miaouss de Galar", ChessDex.Name(52, 2));
        Assert.Equal("Méga-Dracaufeu X", ChessDex.Name(6, 1));
        Assert.Equal([PokeType.Fire, PokeType.Dragon], ChessDex.Find(6, 1)!.Types);
        Assert.Equal("Primo-Kyogre", ChessDex.Name(382, 1));
        Assert.Equal([PokeType.Ice], ChessDex.Find(131, 0)!.Types.Skip(1)); // Lokhlass : Eau / Glace
        Assert.Null(ChessDex.Find(26, 9));
        Assert.Null(ChessDex.Find(0, 0));
    }

    [Fact]
    public void Dex_Search_WithoutAccentsOrByNumber()
    {
        Assert.Equal("Libégon", ChessDex.Search("libegon")[0].Name);
        Assert.Equal(["Raichu", "Raichu d'Alola"], ChessDex.Search("raichu").Take(2).Select(e => e.Name));
        Assert.All(ChessDex.Search("26"), e => Assert.Equal(26, e.Species));
        Assert.Empty(ChessDex.Search("  "));
        Assert.True(ChessDex.Search("a").Count <= 30);
        // Ceux qui commencent par le texte d'abord : « Pikachu » avant « Méga-… »
        Assert.StartsWith("Pika", ChessDex.Search("pika")[0].Name);
    }

    /// <summary>Chaque Pokémon des équipes du jeu est dans le catalogue, sous le même nom, avec un de ses vrais types.</summary>
    [Fact]
    public void Dex_KnowsEveryGameTeamMember()
    {
        foreach (var mon in ChessTeams.All.SelectMany(t => t.Members))
        {
            var entry = ChessDex.Find(mon.Species, mon.Form);
            Assert.True(entry is not null, $"{mon.Name} absent du catalogue");
            Assert.Equal(mon.Name, entry.Name);
            Assert.Contains(mon.Type, entry.Types);
        }
    }

    /// <summary>
    /// Chaque Pokémon des équipes du jeu a son image fixe et ses sprites animés (de sa forme), chromatiques aussi, sauf
    /// Roserade, Minidraco et Dedenne (pas de dossier chromatique : ils restent ordinaires chez les noirs d'une partie à
    /// même équipe).
    /// </summary>
    [Fact]
    public void GameTeams_HaveTheirImages()
    {
        foreach (var mon in ChessTeams.All.SelectMany(t => t.Members))
        {
            Assert.True(PokemonSprites.HasImage(mon.Species, mon.Form, PKHeX.Core.EntityContext.Gen9, false), $"{mon.Name} : pas d'image fixe");
            Assert.True(PokemonSprites.HasDonjonForm(mon.Species, mon.Form, PKHeX.Core.EntityContext.Gen9, false), $"{mon.Name} : pas de sprites animés");
            Assert.True(mon.Species is 407 or 147 or 702 || PokemonSprites.HasDonjonForm(mon.Species, mon.Form, PKHeX.Core.EntityContext.Gen9, true),
                $"{mon.Name} : pas de sprites animés chromatiques");
        }
    }

    // ---------- Droits et valeurs revérifiées ----------

    [Fact]
    public async Task NotAdmin_Refused()
    {
        var id = await NewDraft();
        Assert.Null(await _drafts.ListAsync("ondine"));
        Assert.Equal(AdminService.NotAdminMessage, (await _drafts.CreateAsync("ondine", null)).Message);
        Assert.False((await _drafts.SetMemberAsync("ondine", id, ChessDraftSlot.King, 475, 0, null)).Ok);
        Assert.False((await _drafts.SetCountedAsync("ondine", id, true)).Ok);
        Assert.False((await _drafts.DeleteAsync("ondine", id)).Ok);
        Assert.False((await _drafts.DuplicateAsync("ondine", id)).Ok);
        Assert.False((await _drafts.SetNoteAsync("ondine", id, "x")).Ok);
        Assert.Empty((await Draft(id)).Members);
    }

    [Fact]
    public async Task TamperedValues_Refused()
    {
        var id = await NewDraft();
        Assert.Equal("Cette équipe n'existe pas.", (await _drafts.CreateAsync("sacha", "inconnue")).Message);
        Assert.Equal("Ce Pokémon n'existe pas.", (await _drafts.SetMemberAsync("sacha", id, ChessDraftSlot.King, 2000, 0, null)).Message);
        Assert.Equal("Cette case n'existe pas.", (await _drafts.SetMemberAsync("sacha", id, (ChessDraftSlot)42, 475, 0, null)).Message);
        Assert.Equal("Gallame n'est pas de type Feu.",
            (await _drafts.SetMemberAsync("sacha", id, ChessDraftSlot.King, 475, 0, PokeType.Fire)).Message);
        Assert.Equal("Ce brouillon n'existe plus.", (await _drafts.SetMemberAsync("sacha", 99, ChessDraftSlot.King, 475, 0, null)).Message);
        Assert.False((await _drafts.SetNoteAsync("sacha", id, new string('a', 101))).Ok);
        Assert.Empty((await Draft(id)).Members);
    }

    // ---------- Cases ----------

    [Fact]
    public async Task SetMember_KeepsARealType()
    {
        var id = await NewDraft();
        var result = await _drafts.SetMemberAsync("sacha", id, ChessDraftSlot.King, 475, 0, null);
        Assert.Equal("Roi : Gallame (Psy).", result.Message); // son premier type
        await _drafts.SetMemberAsync("sacha", id, ChessDraftSlot.King, 475, 0, PokeType.Fighting);
        Assert.Equal(PokeType.Fighting, (await Draft(id)).At(ChessDraftSlot.King)!.Type);
        // Un autre Pokémon garde le type de la case s'il l'a aussi : Lucario (Combat / Acier)
        await _drafts.SetMemberAsync("sacha", id, ChessDraftSlot.King, 448, 0, null);
        Assert.Equal(PokeType.Fighting, (await Draft(id)).At(ChessDraftSlot.King)!.Type);
        var draft = await Draft(id);
        Assert.Equal("Équipe Lucario", draft.TeamName);
        Assert.False(draft.Complete);
        Assert.Null(draft.Team);
    }

    [Fact]
    public async Task SecondPawn_SameTypeAsTheFirst()
    {
        var id = await NewDraft();
        Assert.Equal("Choisis d'abord la première espèce de ce pion.",
            (await _drafts.SetMemberAsync("sacha", id, ChessDraftSlot.PawnA2, 29, 0, null)).Message);
        await _drafts.SetMemberAsync("sacha", id, ChessDraftSlot.PawnA1, 32, 0, null); // Nidoran♂, Poison
        Assert.True((await _drafts.SetMemberAsync("sacha", id, ChessDraftSlot.PawnA2, 29, 0, null)).Ok); // Nidoran♀
        Assert.Equal(PokeType.Poison, (await Draft(id)).At(ChessDraftSlot.PawnA2)!.Type);
        Assert.False((await _drafts.SetMemberAsync("sacha", id, ChessDraftSlot.PawnA2, 25, 0, null)).Ok); // Pikachu, pas Poison

        // La première change de type : la seconde est retirée
        var changed = await _drafts.SetMemberAsync("sacha", id, ChessDraftSlot.PawnA1, 25, 0, null);
        Assert.Contains("Nidoran♀ retiré", changed.Message);
        Assert.Null((await Draft(id)).At(ChessDraftSlot.PawnA2));

        // Retirer la première remonte la seconde
        await _drafts.SetMemberAsync("sacha", id, ChessDraftSlot.PawnA1, 32, 0, null);
        await _drafts.SetMemberAsync("sacha", id, ChessDraftSlot.PawnA2, 29, 0, null);
        await _drafts.ClearMemberAsync("sacha", id, ChessDraftSlot.PawnA1);
        var draft = await Draft(id);
        Assert.Equal(29, draft.At(ChessDraftSlot.PawnA1)!.Species);
        Assert.Null(draft.At(ChessDraftSlot.PawnA2));
    }

    [Fact]
    public async Task FromGameTeam_SameTeamUntilChanged()
    {
        var poison = ChessTeams.Find("poison")!;
        var id = await NewDraft("poison");
        var draft = await Draft(id);
        Assert.Equal("poison", draft.BaseTeam);
        Assert.Equal(poison.Members.Select(m => (m.Species, m.Form, m.Type, m.Piece)),
            draft.Team!.Members.Select(m => (m.Species, m.Form, m.Type, m.Piece)));
        Assert.Equal("Équipe Nidoking (brouillon " + id + ")", draft.Team.Name);
        // Mêmes pions aux mêmes colonnes
        Assert.Equal(Enumerable.Range(0, 8).Select(f => poison.PawnAt(f).Species), Enumerable.Range(0, 8).Select(f => draft.Team.PawnAt(f).Species));
        Assert.Equal(ChessBalance.Matchup(poison, poison), ChessBalance.Matchup(draft.Team, draft.Team));

        var copy = (await _drafts.DuplicateAsync("sacha", id)).Id!.Value;
        Assert.Equal(draft.Members, (await Draft(copy)).Members);
        Assert.Equal("poison", (await Draft(copy)).BaseTeam);
        Assert.True((await _drafts.DeleteAsync("sacha", id)).Ok);
        Assert.False((await _drafts.DeleteAsync("sacha", id)).Ok);
    }

    // ---------- Équilibrage ----------

    [Fact]
    public async Task Counted_OnePerGameTeam_AndBalanceTeams()
    {
        var first = await NewDraft("psy");
        var second = await NewDraft("psy");
        var added = await NewDraft("feu");
        await _drafts.SetMemberAsync("sacha", added, ChessDraftSlot.King, 448, 0, null); // un autre roi
        await using (var db = _db.NewContext())
            await db.ChessTeamDrafts.Where(d => d.Id == added).ExecuteUpdateAsync(s => s.SetProperty(d => d.BaseTeam, (string?)null));

        await _drafts.SetCountedAsync("sacha", first, true);
        var result = await _drafts.SetCountedAsync("sacha", second, true);
        Assert.Contains($"Brouillon {first} ne l'est plus", result.Message);
        await _drafts.SetCountedAsync("sacha", added, true);
        var drafts = (await _drafts.ListAsync("sacha"))!;
        Assert.False(drafts.Single(d => d.Id == first).Counted);

        var set = ChessDraftService.Teams(drafts);
        Assert.Equal(ChessTeams.All.Count + 1, set.Teams.Count);
        Assert.Equal($"brouillon-{second}", set.Teams[0].Id); // à la place de l'équipe Gallame
        Assert.Equal("psy", set.OriginOf(set.Teams[0])!.Id);
        Assert.Equal($"brouillon-{added}", set.Teams[^1].Id); // nouvelle équipe, à la fin
        Assert.Null(set.OriginOf(set.Teams[^1]));
        Assert.Same(ChessTeams.All[1], set.Teams[1]);
        // Moyenne sur huit équipes
        Assert.Equal(set.Teams.Average(t => ChessBalance.Matchup(set.Teams[1], t).Edible), ChessBalance.Average(set.Teams[1], set.Teams), 9);

        // Un brouillon incomplet n'est pas compté
        await _drafts.ClearMemberAsync("sacha", added, ChessDraftSlot.Queen);
        Assert.Equal(ChessTeams.All.Count, ChessDraftService.Teams((await _drafts.ListAsync("sacha"))!).Teams.Count);
    }

    // ---------- Remarques et texte ----------

    [Fact]
    public async Task Warnings_AndExportText()
    {
        var id = await NewDraft("plante"); // Roserade : pas de sprites animés chromatiques
        await _drafts.SetMemberAsync("sacha", id, ChessDraftSlot.Rook, 389, 0, PokeType.Ground); // Torterra, déjà le roi
        await _drafts.SetMemberAsync("sacha", id, ChessDraftSlot.PawnB1, 389, 0, PokeType.Grass); // même type que Tortipouss
        await _drafts.SetNoteAsync("sacha", id, "  essai  ");
        var draft = await Draft(id);
        var warnings = draft.Warnings();
        Assert.Contains(warnings, w => w.StartsWith("Roserade (Reine) n'a pas de sprites animés chromatiques", StringComparison.Ordinal));
        Assert.Contains(warnings, w => w.StartsWith("Torterra est 3 fois dans l'équipe", StringComparison.Ordinal));
        Assert.Contains("Les deux pions sont Plante : une seule ligne « Pion » dans les listes et les tables.", warnings);
        Assert.Equal("essai", draft.Note);

        var text = draft.ExportText();
        Assert.StartsWith($"Échecs : Brouillon {id}, Équipe Torterra, modifie l'équipe Torterra (identifiant « plante ») ; note : essai", text);
        Assert.Contains("- Roi : Torterra (n° 389), Sol", text);
        Assert.Contains("- Pion en a, c, e, g : Tortipouss (n° 387), Plante", text);
        Assert.Contains("Attention : Roserade (Reine)", text);

        var empty = await Draft(await NewDraft());
        Assert.Contains("- Roi : (vide)", empty.ExportText());
        Assert.StartsWith("À remplir avant d'être compté dans l'équilibrage : Pion en a, c, e, g, Pion en b, d, f, h, Tour", empty.Warnings()[0]);
    }

    [Fact]
    public async Task Form_ExportedWithItsNumber()
    {
        var id = await NewDraft("electrique");
        Assert.Contains("- Reine : Raichu d'Alola (n° 26, forme 1), Psy", (await Draft(id)).ExportText());
    }

    [Fact]
    public async Task UnknownJson_Ignored()
    {
        var id = await NewDraft();
        await using (var db = _db.NewContext())
            await db.ChessTeamDrafts.Where(d => d.Id == id).ExecuteUpdateAsync(s => s.SetProperty(d => d.Members, "pas du JSON"));
        Assert.Empty((await Draft(id)).Members);
        await using (var db = _db.NewContext())
            await db.ChessTeamDrafts.Where(d => d.Id == id).ExecuteUpdateAsync(s => s.SetProperty(d => d.Members,
                """[{"Slot":"King","Species":475,"Form":0,"Type":"Fighting"},{"Slot":"King","Species":6,"Form":0,"Type":"Fire"},{"Slot":"Queen","Species":5000,"Form":0,"Type":"Fire"}]"""));
        var draft = await Draft(id);
        Assert.Equal(475, Assert.Single(draft.Members).Species);
    }
}
