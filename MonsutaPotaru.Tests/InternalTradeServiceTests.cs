using Microsoft.EntityFrameworkCore;
using MonsutaPotaru.Services;
using PKHeX.Core;

namespace MonsutaPotaru.Tests;

/// <summary>
/// Échange interne (deux sauvegardes du même compte) et règle de compatibilité commune aux échanges (<see cref="TradeFamily"/> :
/// contexte PKHeX, Time Capsule, cartouches japonaises).
/// </summary>
public sealed class InternalTradeServiceTests : IDisposable
{
    private readonly TestDatabase _db = new("sacha", "ondine");
    private readonly TradeService _trades;
    private readonly StoredSaveService _saves;

    public InternalTradeServiceTests()
    {
        _trades = _db.Trades();
        _saves = _db.StoredSaves();
    }

    public void Dispose() => _db.Dispose();

    private static readonly string RealSaves = Path.Combine(AppContext.BaseDirectory, "Sauvegardes");

    private async Task<int> Store(string owner, string file, byte[] data)
    {
        var result = await _saves.AddAsync(owner, file, data);
        Assert.True(result.Ok, $"{file} : {result.Message}");
        return (await _saves.ListAsync(owner)).Single(s => s.FileName == file).Id;
    }

    private Task<int> StoreReal(string file) => Store("sacha", file, File.ReadAllBytes(Path.Combine(RealSaves, file)));

    private async Task<SaveFile> Read(int saveId)
    {
        await using var db = _db.NewContext();
        var save = await db.StoredSaves.SingleAsync(s => s.Id == saveId);
        Assert.True(SaveUtil.TryGetSaveFile(save.Data.ToArray(), out var sav, save.FileName));
        return sav!;
    }

    [Theory]
    [InlineData(EntityContext.Gen5, GameBoyRegion.International, EntityContext.Gen5, GameBoyRegion.International, true)]  // Noire ↔ Noire 2
    [InlineData(EntityContext.Gen4, GameBoyRegion.International, EntityContext.Gen5, GameBoyRegion.International, false)] // Pal Park / Poké Transfert : à sens unique
    [InlineData(EntityContext.Gen6, GameBoyRegion.International, EntityContext.Gen7, GameBoyRegion.International, false)] // X ↔ Soleil : non
    [InlineData(EntityContext.Gen7, GameBoyRegion.International, EntityContext.Gen7b, GameBoyRegion.International, false)] // Soleil ↔ Let's Go : non
    [InlineData(EntityContext.Gen1, GameBoyRegion.International, EntityContext.Gen2, GameBoyRegion.International, true)]  // Time Capsule
    [InlineData(EntityContext.Gen1, GameBoyRegion.Japanese, EntityContext.Gen2, GameBoyRegion.Japanese, true)]            // Time Capsule japonais
    [InlineData(EntityContext.Gen1, GameBoyRegion.Japanese, EntityContext.Gen1, GameBoyRegion.International, false)]      // japonais ↔ européen : non
    [InlineData(EntityContext.Gen2, GameBoyRegion.Japanese, EntityContext.Gen2, GameBoyRegion.International, false)]
    [InlineData(EntityContext.Gen2, GameBoyRegion.Korean, EntityContext.Gen2, GameBoyRegion.International, true)]         // Or coréen ↔ Cristal
    [InlineData(EntityContext.Gen2, GameBoyRegion.Korean, EntityContext.Gen1, GameBoyRegion.International, false)]        // pas de Time Capsule coréen
    [InlineData(EntityContext.Gen2, GameBoyRegion.Korean, EntityContext.Gen2, GameBoyRegion.Japanese, false)]
    [InlineData(EntityContext.Gen2, GameBoyRegion.International, EntityContext.Gen3, GameBoyRegion.International, false)]
    public void TradeFamily_FollowsTheConsole(EntityContext a, GameBoyRegion ra, EntityContext b, GameBoyRegion rb, bool expected)
    {
        Assert.Equal(expected, new TradeFamily(a, ra).CanTradeWith(new TradeFamily(b, rb)));
        Assert.Equal(expected, new TradeFamily(b, rb).CanTradeWith(new TradeFamily(a, ra))); // dans les deux sens
    }

    [Fact]
    public void TradeFamily_ReadsTheRegionOfAGameBoySave()
    {
        Assert.Equal(new TradeFamily(EntityContext.Gen1, GameBoyRegion.Japanese), TradeFamily.Of(new SAV1(LanguageID.Japanese)));
        Assert.Equal(new TradeFamily(EntityContext.Gen1, GameBoyRegion.International), TradeFamily.Of(new SAV1(LanguageID.French)));
        Assert.Equal(new TradeFamily(EntityContext.Gen2, GameBoyRegion.Korean), TradeFamily.Of(new SAV2(LanguageID.Korean)));
        Assert.Equal(new TradeFamily(EntityContext.Gen5, GameBoyRegion.International), TradeFamily.Of(new SAV5B2W2()));
    }

    [Fact]
    public async Task InternalSaves_PairsByConsoleRules_SameGameIncluded_NeverItself()
    {
        var b2 = await Store("sacha", "b2.sav", TradeServiceTests.Bw2("Sacha", 1, [25]));
        var b2Bis = await Store("sacha", "b2-bis.sav", TradeServiceTests.Bw2("Regis", 2, [133]));
        var platinum = await Store("sacha", "platine.sav", SaveSessionTests.CreatePlatinumSave(3));
        await Store("ondine", "b2.sav", TradeServiceTests.Bw2("Ondine", 3, [7])); // d'un autre compte : jamais proposée

        var saves = await _trades.InternalSavesAsync("sacha");
        Assert.Equal([b2, b2Bis, platinum], saves.Select(s => s.Save.Id));
        Assert.Equal([b2Bis], saves[0].Partners);
        Assert.Equal([b2], saves[1].Partners);
        Assert.Empty(saves[2].Partners); // seule de sa génération
    }

    [Fact]
    public async Task InternalTrade_SwapsBothWays_EvolvesKadabra_ButNotInStats()
    {
        var first = await Store("sacha", "b2.sav", TradeServiceTests.Bw2("Sacha", 1, [25, 4], 64)); // Pikachu, Salamèche ; Kadabra en boîte
        var second = await Store("sacha", "w2.sav", TradeServiceTests.Bw2("Regis", 2, [120], 7));   // Stari ; Carapuce en boîte

        var sent = (await _trades.CandidatesAsync("sacha", first, "sacha", second)).Single(c => c.Species == 64);
        var received = (await _trades.InternalReceivableAsync("sacha", first, sent, second)).Single(c => c.Species == 120);
        Assert.Null(received.Problem);

        var preview = await _trades.InternalPreviewAsync("sacha", first, sent, second, received);
        Assert.Null(preview.Problem);
        Assert.Equal("Kadabra évoluera en Alakazam en arrivant dans « Jeu de génération 5 — Regis ».", preview.FirstEvolution);
        Assert.Null(preview.SecondEvolution);

        var outcome = await _trades.ExecuteInternalAsync("sacha", first, sent, second, received);
        Assert.True(outcome.Ok, outcome.FirstMessage);
        Assert.Contains("« Jeu de génération 5 — Sacha » a reçu Stari, et « Jeu de génération 5 — Regis » a reçu Kadabra, qui a évolué en Alakazam.", outcome.FirstMessage);

        Assert.Equal(120, (await Read(first)).GetBoxSlotAtIndex(0, 0).Species); // le Stari à la place du Kadabra
        var regis = await Read(second);
        Assert.Equal(65, regis.GetPartySlotAtIndex(0).Species); // Alakazam à la place du Stari
        Assert.Equal("Ludo", regis.GetPartySlotAtIndex(0).OriginalTrainerName); // dresseur d'origine inchangé (CreatePk5)
        Assert.True(regis.GetCaught(65));

        // Les deux à télécharger (trace anti-copie), mais pas un échange entre amis.
        Assert.All(await _saves.ListAsync("sacha"), s => Assert.Equal((true, "un échange de Pokémon"), (s.MustDownloadBeforeUpdate, s.ServerChanges)));
        Assert.Equal(new TradeStats(0, 0), await _trades.StatsAsync("sacha"));
        await using var db = _db.NewContext();
        Assert.Empty(db.TradeRecords);
    }

    [Fact]
    public async Task InternalTrade_EggThatWouldLeaveOnlyEggs_IsGreyedAndRefused()
    {
        var first = await Store("sacha", "b2.sav", TradeServiceTests.Bw2("Sacha", 1, [25])); // un seul Pokémon dans l'équipe
        var data = TradeServiceTests.Bw2("Regis", 2, [120], 7);
        Assert.True(SaveUtil.TryGetSaveFile(data, out var regis));
        var egg = SaveSessionTests.CreatePk5(133, 1, "Regis", 9);
        egg.IsEgg = true;
        egg.RefreshChecksum();
        regis!.SetBoxSlotAtIndex(egg, 0, 1);
        var second = await Store("sacha", "w2.sav", regis.Write().ToArray());

        var pikachu = (await _trades.CandidatesAsync("sacha", first, "sacha", second)).Single();
        var receivable = await _trades.InternalReceivableAsync("sacha", first, pikachu, second);
        var eggCandidate = receivable.Single(c => c.IsEgg);
        Assert.Equal("L'équipe de « Jeu de génération 5 — Sacha » garderait seulement des œufs.", eggCandidate.Problem);
        Assert.All(receivable.Where(c => !c.IsEgg), c => Assert.Null(c.Problem));

        var refused = await _trades.ExecuteInternalAsync("sacha", first, pikachu, second, eggCandidate);
        Assert.False(refused.Ok);
        Assert.Equal(25, (await Read(first)).GetPartySlotAtIndex(0).Species); // rien n'a bougé
    }

    [Fact]
    public async Task InternalTrade_RefusesOtherAccounts_TheSameSave_AndChangedPokemon()
    {
        var mine = await Store("sacha", "b2.sav", TradeServiceTests.Bw2("Sacha", 1, [25, 4]));
        var mine2 = await Store("sacha", "w2.sav", TradeServiceTests.Bw2("Regis", 2, [120, 7]));
        var hers = await Store("ondine", "b2.sav", TradeServiceTests.Bw2("Ondine", 3, [133, 1]));
        var pikachu = (await _trades.CandidatesAsync("sacha", mine, "sacha", mine2)).First();
        var staryu = (await _trades.CandidatesAsync("sacha", mine2, "sacha", mine)).First();
        var eevee = (await _trades.CandidatesAsync("ondine", hers, "ondine", hers)).FirstOrDefault();
        Assert.Null(eevee); // une sauvegarde avec elle-même : rien

        // Identifiants envoyés par un navigateur modifié : la sauvegarde d'Ondine n'est pas à Sacha.
        Assert.Empty(await _trades.CandidatesAsync("sacha", hers, "sacha", mine));
        Assert.Empty(await _trades.InternalReceivableAsync("sacha", mine, pikachu, hers));
        var theirs = await _trades.ExecuteInternalAsync("sacha", mine, pikachu, hers, staryu);
        Assert.False(theirs.Ok);
        Assert.False(theirs.Retry);
        var itself = await _trades.ExecuteInternalAsync("sacha", mine, pikachu, mine, pikachu);
        Assert.False(itself.Ok);
        Assert.Contains("elle-même", itself.FirstMessage);

        // Un Pokémon qui n'est plus le même (empreinte) : choisir de nouveau.
        var stale = await _trades.InternalPreviewAsync("sacha", mine, pikachu with { Fingerprint = "autre" }, mine2, staryu);
        Assert.NotNull(stale.Problem);
        Assert.True(stale.Retry);
        Assert.Equal(25, (await Read(mine)).GetPartySlotAtIndex(0).Species);
    }

    /// <summary>Vraies sauvegardes : Rouge ↔ Bleu, Rouge ↔ Argent (Time Capsule), Rouge Feu ↔ Vert Feuille, Noir ↔ Noire 2.</summary>
    [Fact]
    public async Task RealSaves_TimeCapsule_OnlyFirstGenerationPokemonGoBack()
    {
        var red = await StoreReal("rouge.sav");
        var blue = await StoreReal("bleu.sav");
        var silver = await StoreReal("argent.sav");
        var black = await StoreReal("noir.sav");
        var black2 = await StoreReal("noire-2.sav");
        var soulSilver = await StoreReal("soulsilver.sav");

        var saves = (await _trades.InternalSavesAsync("sacha")).ToDictionary(s => s.Save.Id, s => s.Partners.Order().ToList());
        Assert.Equal(new[] { blue, silver }.Order(), saves[red]);
        Assert.Equal(new[] { red, blue }.Order(), saves[silver]);
        Assert.Equal([black2], saves[black]);
        Assert.Empty(saves[soulSilver]); // 4e génération : rien d'autre ici

        // D'Argent vers Rouge : seulement les espèces et les attaques de la 1re génération, sans œuf.
        var fromSilver = await _trades.CandidatesAsync("sacha", silver, "sacha", red);
        var silverSave = await Read(silver);
        var all = silverSave.GetAllPKM().Where(p => p.Species > 0).ToList();
        Assert.All(fromSilver, c => Assert.True(c.Species <= 151 && !c.IsEgg, c.Name));
        Assert.True(fromSilver.Count < all.Count || all.All(p => p.Species <= 151 && !p.IsEgg));

        // Un vrai échange par le Time Capsule : chacun arrive au format de l'autre jeu.
        var fromRed = await _trades.CandidatesAsync("sacha", red, "sacha", silver);
        Assert.NotEmpty(fromRed);
        Assert.NotEmpty(fromSilver);
        var sent = fromRed[0];
        var received = (await _trades.InternalReceivableAsync("sacha", red, sent, silver)).First(c => c.Problem is null);
        var outcome = await _trades.ExecuteInternalAsync("sacha", red, sent, silver, received);
        Assert.True(outcome.Ok, outcome.FirstMessage);
        var redAfter = await Read(red);
        Assert.IsType<PK1>(sent.Slot.InParty ? redAfter.GetPartySlotAtIndex(sent.Slot.Slot) : redAfter.GetBoxSlotAtIndex(sent.Slot.Box, sent.Slot.Slot));
        var silverAfter = await Read(silver);
        var arrived = received.Slot.InParty ? silverAfter.GetPartySlotAtIndex(received.Slot.Slot) : silverAfter.GetBoxSlotAtIndex(received.Slot.Box, received.Slot.Slot);
        Assert.IsType<PK2>(arrived);
        Assert.Equal(PokemonFingerprint.Of(arrived), sent.Fingerprint); // même Pokémon (empreinte sans l'espèce)
    }
}
