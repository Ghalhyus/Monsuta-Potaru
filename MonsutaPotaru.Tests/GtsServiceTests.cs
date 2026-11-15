using Microsoft.EntityFrameworkCore;
using MonsutaPotaru.Data;
using MonsutaPotaru.Services;
using PKHeX.Core;

namespace MonsutaPotaru.Tests;

/// <summary>
/// GTS (<see cref="GtsService"/>) : dépôt depuis une sauvegarde ou le stockage interne, recherche, échange (évolutions,
/// familles de jeux, critères revérifiés), boîte de réception (annulation, expiration, sauvegarde supprimée, récupération).
/// </summary>
public sealed class GtsServiceTests : IDisposable
{
    private readonly TestDatabase _db = new("sacha", "ondine", "pierre");
    private readonly GtsEvents _events = new();
    private readonly GtsService _gts;
    private readonly StoredSaveService _saves;
    private readonly List<string> _notified = [];

    private const ushort Pikachu = 25, Charmander = 4, Kadabra = 64, Alakazam = 65, Machoke = 67, Machamp = 68, Staryu = 120;

    public GtsServiceTests()
    {
        _gts = _db.Gts(_events);
        _saves = _db.StoredSaves();
        _events.InboxChanged += _notified.Add;
        using var db = _db.NewContext();
        foreach (var user in db.Users)
            user.UserName = user.Id; // pseudo affiché sur les offres
        db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private async Task<int> Store(string owner, string file, byte[] data)
    {
        var result = await _saves.AddAsync(owner, file, data);
        Assert.True(result.Ok, result.Message);
        return (await _saves.ListAsync(owner)).Single(s => s.FileName == file).Id;
    }

    private async Task<SaveFile> Read(int saveId)
    {
        await using var db = _db.NewContext();
        var save = await db.StoredSaves.SingleAsync(s => s.Id == saveId);
        Assert.True(SaveUtil.TryGetSaveFile(save.Data.ToArray(), out var sav, save.FileName));
        return sav!;
    }

    /// <summary>Identifiant dans la collection du Pokémon de cette espèce (d'une sauvegarde, ou du stockage interne).</summary>
    private int BankId(string owner, ushort species, int? saveId = null)
    {
        using var db = _db.NewContext();
        return db.BankPokemon.Single(p => p.OwnerId == owner && p.Species == species && p.StoredSaveId == saveId).Id;
    }

    /// <summary>Met ce Pokémon (Noire 2) dans le stockage interne.</summary>
    private int AddToBank(string owner, ushort species, byte level = 30, int index = 50)
    {
        using var db = _db.NewContext();
        var entry = BankService.CreateEntry(new PokemonInfo(), SaveSessionTests.CreatePk5(species, level, owner, index), owner, DateTime.UtcNow);
        db.BankPokemon.Add(entry);
        db.SaveChanges();
        return entry.Id;
    }

    private List<GtsPokemon> Rows()
    {
        using var db = _db.NewContext();
        return db.GtsPokemon.AsNoTracking().OrderBy(g => g.Id).ToList();
    }

    [Fact]
    public async Task Deposit_FromASave_LeavesTheSave_MarksIt_AndStaysInTheAccount()
    {
        var save = await Store("sacha", "b2.sav", TradeServiceTests.Bw2("Sacha", 1, [Pikachu], Kadabra));
        var kadabra = BankId("sacha", Kadabra, save);
        var usage = await _db.Bank().GetUsageAsync("sacha");

        var choice = await _gts.DepositChoiceAsync("sacha", kadabra);
        Assert.NotNull(choice);
        Assert.Null(choice.Problem);
        Assert.True(choice.FromSave);
        Assert.Equal("Noire, Blanche, Noire 2, Blanche 2", choice.Games);
        Assert.DoesNotContain(choice.Species, s => s.Id > 649); // espèces de la 5e génération au plus

        var result = await _gts.DepositAsync("sacha", kadabra, new GtsWanted(Machoke, 20, 40));
        Assert.True(result.Ok, result.Message);
        Assert.Contains("Kadabra est sur la GTS jusqu'au", result.Message);
        Assert.Contains("contre Machopeur, N.20 à 40", result.Message);
        Assert.Contains("Télécharge cette sauvegarde", result.Message);

        Assert.Equal(0, (await Read(save)).GetBoxSlotAtIndex(0, 0).Species); // la case est vide
        var info = (await _saves.ListAsync("sacha")).Single();
        Assert.True(info.MustDownloadBeforeUpdate);
        Assert.Equal("un passage par la GTS", info.ServerChanges);
        Assert.Equal(usage, await _db.Bank().GetUsageAsync("sacha")); // il garde sa place dans la collection

        var offer = Assert.Single(await _gts.MyOffersAsync("sacha"));
        Assert.Equal("Kadabra", offer.Pokemon.Name);
        Assert.Equal("Machopeur, N.20 à 40", offer.Wanted);
        Assert.Equal("Jeu de génération 5 — Sacha", offer.Origin);
        Assert.Equal(GtsStatus.Offered, Assert.Single(Rows()).Status);

        // Une vieille copie de la sauvegarde (avec le Kadabra) est refusée : il est encore au compte, sur la GTS.
        var refused = await _saves.UpdateAsync("sacha", save, "b2.sav", SaveSessionTests.AddOneHourOfPlay(TradeServiceTests.Bw2("Sacha", 1, [Pikachu], Kadabra)));
        Assert.False(refused.Ok);
    }

    [Fact]
    public async Task Deposit_ACopyOfAPokemonOnTheGts_IsRefusedAsADuplicate()
    {
        var bank = AddToBank("sacha", Kadabra);
        Assert.True((await _gts.DepositAsync("sacha", bank, new GtsWanted(Machoke))).Ok);
        using (var db = _db.NewContext())
            Assert.False(db.BankPokemon.Any(p => p.Id == bank)); // il a quitté le stockage interne

        // La sauvegarde d'où il venait, envoyée de nouveau : doublon (GTS).
        var sav = new SAV5B2W2 { OT = "sacha", TID16 = 12345, SID16 = 0, PlayedHours = 10 };
        sav.SetBoxSlotAtIndex(SaveSessionTests.CreatePk5(Kadabra, 30, "sacha", 50), 0, 0);
        var result = await _saves.AddAsync("sacha", "vieille.sav", sav.Write().ToArray());
        Assert.False(result.Ok);
        Assert.Contains("Kadabra N.30 (GTS)", result.Message);
    }

    [Fact]
    public async Task Deposit_Refusals()
    {
        var save = await Store("sacha", "b2.sav", TradeServiceTests.Bw2("Sacha", 1, [Pikachu], Charmander, Kadabra, Staryu, Machoke, 1, 7));

        // Le dernier Pokémon de l'équipe ne la quitte pas.
        var last = await _gts.DepositAsync("sacha", BankId("sacha", Pikachu, save), new GtsWanted(Machoke));
        Assert.False(last.Ok);
        Assert.Contains("dernier Pokémon", last.Message);

        // Une espèce que le jeu ne connaît pas, un sexe que l'espèce n'a pas.
        var unknown = await _gts.DepositAsync("sacha", BankId("sacha", Kadabra, save), new GtsWanted(900));
        Assert.Contains("n'existe pas dans le jeu", unknown.Message);
        var genderless = await _gts.DepositAsync("sacha", BankId("sacha", Kadabra, save), new GtsWanted(Staryu, Gender: 1));
        Assert.Contains("Stari n'a pas de sexe", genderless.Message);
        var male = await _gts.DepositAsync("sacha", BankId("sacha", Kadabra, save), new GtsWanted(Machoke, Gender: 1));
        Assert.True(male.Ok, male.Message); // Machopeur peut être femelle

        // Cinq offres au plus (la sauvegarde a été modifiée par le dépôt : on la relit à chaque fois).
        foreach (var species in new ushort[] { Charmander, Staryu, Machoke, 1 })
            Assert.True((await _gts.DepositAsync("sacha", BankId("sacha", species, save), new GtsWanted(Pikachu))).Ok);
        var sixth = await _gts.DepositAsync("sacha", BankId("sacha", 7, save), new GtsWanted(Pikachu));
        Assert.False(sixth.Ok);
        Assert.Contains("déjà 5 offres", sixth.Message);
    }

    [Fact]
    public void PickProblem_GraysEggsAndSpecialPlaces()
    {
        BankListItem Item(bool egg, int? save, string? location) => new(1, 25, "Pikachu", "Pikachu", 5, false, egg, true, save, "", location);
        Assert.Null(GtsService.PickProblem(Item(false, null, null)));
        Assert.Null(GtsService.PickProblem(Item(false, 3, "Équipe 2")));
        Assert.Null(GtsService.PickProblem(Item(false, 3, "Ma boîte, case 12")));
        Assert.Contains("œuf", GtsService.PickProblem(Item(true, null, null)));
        Assert.Contains("ni dans l'équipe ni dans les boîtes", GtsService.PickProblem(Item(false, 3, "Pension 1")));
    }

    [Fact]
    public async Task Search_FiltersOthersOffers_ShowsWhoAndWhetherIOwnTheWantedPokemon()
    {
        Assert.True((await _gts.DepositAsync("ondine", AddToBank("ondine", Kadabra, 30, 1), new GtsWanted(Machoke, 20, 40))).Ok);
        Assert.True((await _gts.DepositAsync("pierre", AddToBank("pierre", Kadabra, 50, 2), new GtsWanted(Pikachu))).Ok);
        Assert.True((await _gts.DepositAsync("sacha", AddToBank("sacha", Kadabra, 30, 3), new GtsWanted(Pikachu))).Ok);
        Assert.True((await _gts.DepositAsync("ondine", AddToBank("ondine", Staryu, 10, 4), new GtsWanted(Pikachu))).Ok);
        using (var db = _db.NewContext())
        {
            db.Users.Single(u => u.Id == "pierre").Audience = ProfileAudience.Nobody;
            db.SaveChanges();
        }

        var all = await _gts.SearchAsync("sacha", new GtsWanted(null), onlyOwned: false);
        Assert.Equal(3, all.Count); // jamais les siennes
        var kadabras = await _gts.SearchAsync("sacha", new GtsWanted(Kadabra), onlyOwned: false);
        Assert.Equal(["Un dresseur", "ondine"], kadabras.Select(o => o.Pseudo ?? "Un dresseur").Order(StringComparer.Ordinal));
        Assert.Single(await _gts.SearchAsync("sacha", new GtsWanted(Kadabra, MinLevel: 40), onlyOwned: false));
        Assert.Empty(await _gts.SearchAsync("sacha", new GtsWanted(Kadabra, Shiny: true), onlyOwned: false));
        Assert.All(all, o => Assert.False(o.Owned));
        Assert.Empty(await _gts.SearchAsync("sacha", new GtsWanted(null), onlyOwned: true));

        // Un Machopeur N.30 de 5e génération répond à l'offre d'Ondine ; un Pikachu de 4e génération à aucune (jeux de 5e).
        AddToBank("sacha", Machoke, 30, 5);
        using (var db = _db.NewContext())
        {
            var pk = new PK4 { Species = Pikachu, CurrentLevel = 10, OriginalTrainerName = "sacha", PID = 99 };
            db.BankPokemon.Add(BankService.CreateEntry(new PokemonInfo(), pk, "sacha", DateTime.UtcNow));
            db.SaveChanges();
        }
        var owned = await _gts.SearchAsync("sacha", new GtsWanted(null), onlyOwned: true);
        var only = Assert.Single(owned);
        Assert.Equal("ondine", only.Pseudo);
        Assert.True(only.Owned);
        Assert.Equal("Machopeur, N.20 à 40", only.Wanted);
    }

    [Fact]
    public async Task Accept_FromASave_SwapsBothWays_EvolvesBoth_AndCountsInStats()
    {
        var hers = await Store("ondine", "b2.sav", TradeServiceTests.Bw2("Ondine", 2, [Pikachu], Kadabra));
        Assert.True((await _gts.DepositAsync("ondine", BankId("ondine", Kadabra, hers), new GtsWanted(Machoke, 20, 40))).Ok);
        var mine = await Store("sacha", "b2.sav", TradeServiceTests.Bw2("Sacha", 1, [Pikachu, Machoke]));
        var offer = Assert.Single(await _gts.SearchAsync("sacha", new GtsWanted(Kadabra), onlyOwned: true));
        _notified.Clear();

        var candidate = Assert.Single(await _gts.CandidatesAsync("sacha", offer.Id));
        Assert.Null(candidate.Problem);
        Assert.Equal("Jeu de génération 5 — Sacha · Équipe 2", candidate.Origin);

        var preview = await _gts.PreviewAsync("sacha", offer.Id, candidate.BankId);
        Assert.Null(preview.Problem);
        Assert.Equal("Kadabra évoluera en Alakazam en arrivant dans « Jeu de génération 5 — Sacha ».", preview.ReceivedEvolution);
        Assert.Equal("Machopeur évoluera en Mackogneur en arrivant chez l'autre joueur.", preview.GivenEvolution);

        var result = await _gts.AcceptAsync("sacha", offer.Id, candidate.BankId);
        Assert.True(result.Ok, result.Message);
        Assert.StartsWith("Échange réussi : tu as reçu Kadabra, qui a évolué en Alakazam dans « Jeu de génération 5 — Sacha ».", result.Message);

        Assert.Equal(Alakazam, (await Read(mine)).GetPartySlotAtIndex(1).Species); // à la place du Machopeur
        Assert.True((await _saves.ListAsync("sacha")).Single().MustDownloadBeforeUpdate);
        var inbox = Assert.Single(await _gts.InboxAsync("ondine"));
        Assert.Equal(GtsStatus.Received, inbox.Status);
        Assert.Equal(Machamp, inbox.Pokemon.Species);
        Assert.Equal("Échange avec sacha : tu as reçu Machopeur, qui a évolué en Mackogneur contre ton Kadabra.", inbox.Note);
        Assert.Equal(["ondine"], _notified);
        Assert.Equal(1, await _gts.InboxCountAsync("ondine"));
        Assert.Empty(await _gts.MyOffersAsync("ondine"));
        Assert.Equal(new TradeStats(1, 1), await _db.Trades().StatsAsync("ondine"));

        // L'offre n'est plus là : un second échange est refusé.
        var again = await _gts.AcceptAsync("pierre", offer.Id, AddToBank("pierre", Machoke));
        Assert.False(again.Ok);
        Assert.Contains("n'est plus disponible", again.Message);
    }

    [Fact]
    public async Task Accept_FromInternalStorage_KeepsBothFormats()
    {
        Assert.True((await _gts.DepositAsync("ondine", AddToBank("ondine", Staryu, 30, 1), new GtsWanted(Pikachu))).Ok);
        var pikachu = AddToBank("sacha", Pikachu, 30, 2);
        var offer = Assert.Single(await _gts.SearchAsync("sacha", new GtsWanted(null), onlyOwned: true));

        var result = await _gts.AcceptAsync("sacha", offer.Id, pikachu);
        Assert.True(result.Ok, result.Message);
        Assert.Equal("Échange réussi : tu as reçu Stari dans ton stockage interne.", result.Message);
        using var db = _db.NewContext();
        var bank = db.BankPokemon.Where(p => p.OwnerId == "sacha").ToList();
        Assert.Equal(Staryu, Assert.Single(bank).Species);
        Assert.Equal(Pikachu, Assert.Single(await _gts.InboxAsync("ondine")).Pokemon.Species);
    }

    [Fact]
    public async Task Accept_RechecksTheWantedPokemonAndTheGames()
    {
        Assert.True((await _gts.DepositAsync("ondine", AddToBank("ondine", Kadabra, 30, 1), new GtsWanted(Machoke, 20, 40))).Ok);
        var offerId = Rows().Single().Id;

        // Trop haut : pas dans la liste, et refusé si l'identifiant vient du navigateur.
        var tooHigh = AddToBank("sacha", Machoke, 50, 2);
        Assert.Empty(await _gts.CandidatesAsync("sacha", offerId));
        var refused = await _gts.AcceptAsync("sacha", offerId, tooHigh);
        Assert.False(refused.Ok);
        Assert.Contains("ne répond pas à la demande", refused.Message);

        // Le Pokémon d'un autre compte : refusé.
        var hers = AddToBank("ondine", Machoke, 30, 3);
        Assert.False((await _gts.AcceptAsync("sacha", offerId, hers)).Ok);

        // Un Machopeur de Platine (4e génération) : dans la liste, grisé.
        var platinum = await Store("sacha", "platine.sav", PlatinumWith(Machoke));
        var candidate = Assert.Single(await _gts.CandidatesAsync("sacha", offerId));
        Assert.Contains("ne peut pas échanger avec les jeux de l'offre", candidate.Problem);
        Assert.False((await _gts.AcceptAsync("sacha", offerId, candidate.BankId)).Ok);
        Assert.Equal(Machoke, (await Read(platinum)).GetBoxSlotAtIndex(0, 0).Species); // rien d'écrit

        // Sa propre offre : introuvable.
        Assert.Empty(await _gts.CandidatesAsync("ondine", offerId));
    }

    /// <summary>Une Platine avec ce Pokémon (niveau 30) dans la boîte 1.</summary>
    private static byte[] PlatinumWith(ushort species)
    {
        Assert.True(SaveUtil.TryGetSaveFile(SaveSessionTests.CreatePlatinumSave(0), out var sav));
        var pk = new PK4 { Species = species, OriginalTrainerName = "Lucas", TID16 = 24680, SID16 = 13579, PID = 1234, Version = GameVersion.Pt, Language = 2 };
        pk.CurrentLevel = 30;
        pk.RefreshChecksum();
        sav!.SetBoxSlotAtIndex(pk, 0, 0);
        return sav.Write().ToArray();
    }

    [Fact]
    public async Task Cancel_Expiry_AndSaveDeletion_FillTheInbox()
    {
        var save = await Store("sacha", "b2.sav", TradeServiceTests.Bw2("Sacha", 1, [Pikachu], Kadabra, Machoke));
        Assert.True((await _gts.DepositAsync("sacha", BankId("sacha", Kadabra, save), new GtsWanted(Pikachu))).Ok);
        Assert.True((await _gts.DepositAsync("sacha", BankId("sacha", Machoke, save), new GtsWanted(Pikachu))).Ok);
        Assert.True((await _gts.DepositAsync("sacha", AddToBank("sacha", Staryu), new GtsWanted(Pikachu))).Ok);
        var (kadabra, machoke, staryu) = (Rows()[0].Id, Rows()[1].Id, Rows()[2].Id);

        var cancel = await _gts.CancelAsync("sacha", staryu);
        Assert.True(cancel.Ok, cancel.Message);
        Assert.False((await _gts.CancelAsync("sacha", staryu)).Ok); // déjà annulée
        Assert.False((await _gts.CancelAsync("ondine", kadabra)).Ok); // pas la sienne

        using (var db = _db.NewContext())
        {
            db.GtsPokemon.Single(g => g.Id == kadabra).ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            db.SaveChanges();
        }
        Assert.Single(await _gts.SearchAsync("ondine", new GtsWanted(null), onlyOwned: false)); // l'offre expirée n'est plus cherchable
        await _gts.ExpireAsync();

        // La sauvegarde est supprimée (après téléchargement de la version modifiée) : l'offre du Machopeur est retirée.
        Assert.Equal(new Dictionary<int, int> { [save] = 1 }, await _gts.OffersBySaveAsync("sacha"));
        Assert.NotNull(await _saves.DeleteAsync("sacha", save));

        var inbox = await _gts.InboxAsync("sacha");
        Assert.Equal(3, inbox.Count);
        Assert.Equal(GtsStatus.Cancelled, inbox.Single(i => i.Id == staryu).Status);
        Assert.Equal("Tu as annulé ton offre : Stari te revient.", inbox.Single(i => i.Id == staryu).Note);
        Assert.Equal("Personne n'a accepté ton offre en 30 jours : Kadabra te revient.", inbox.Single(i => i.Id == kadabra).Note);
        Assert.Equal(GtsStatus.SaveDeleted, inbox.Single(i => i.Id == machoke).Status);
        Assert.Empty(await _gts.MyOffersAsync("sacha"));
        Assert.Contains("sacha", _notified);
    }

    [Fact]
    public async Task Retrieve_ToInternalStorage_OrToTheFirstFreeBoxSlotOfASave()
    {
        Assert.True((await _gts.DepositAsync("sacha", AddToBank("sacha", Kadabra, 30, 1), new GtsWanted(Pikachu))).Ok);
        Assert.True((await _gts.DepositAsync("sacha", AddToBank("sacha", Staryu, 30, 2), new GtsWanted(Pikachu))).Ok);
        foreach (var row in Rows())
            await _gts.CancelAsync("sacha", row.Id);
        var save = await Store("sacha", "b2.sav", TradeServiceTests.Bw2("Sacha", 1, [Pikachu], Charmander));
        var platinum = await Store("sacha", "platine.sav", PlatinumWith(Machoke));

        var inbox = await _gts.InboxAsync("sacha");
        Assert.All(inbox, i => Assert.Equal(save, Assert.Single(i.Saves).SaveId)); // pas vers la Platine (5e génération)

        var toSave = await _gts.RetrieveAsync("sacha", inbox.Single(i => i.Pokemon.Species == Kadabra).Id, save);
        Assert.True(toSave.Ok, toSave.Message);
        Assert.Equal("Kadabra est arrivé dans « Jeu de génération 5 — Sacha » (boîte 1, case 2). Télécharge cette sauvegarde (« Mes sauvegardes », ou « Récupérer » depuis ta 3DS) et remets-la dans ton jeu.", toSave.Message);
        Assert.Equal(Kadabra, (await Read(save)).GetBoxSlotAtIndex(0, 1).Species);
        Assert.True((await _saves.ListAsync("sacha")).Single(s => s.Id == save).MustDownloadBeforeUpdate);

        var staryu = inbox.Single(i => i.Pokemon.Species == Staryu).Id;
        Assert.False((await _gts.RetrieveAsync("sacha", staryu, platinum)).Ok);
        Assert.False((await _gts.RetrieveAsync("ondine", staryu, null)).Ok); // pas sa boîte
        var toBank = await _gts.RetrieveAsync("sacha", staryu, null);
        Assert.True(toBank.Ok, toBank.Message);
        Assert.Equal("Stari est dans ton stockage interne.", toBank.Message);
        Assert.Empty(await _gts.InboxAsync("sacha"));
        Assert.Empty(Rows());
        using var db = _db.NewContext();
        Assert.True(db.BankPokemon.Any(p => p.OwnerId == "sacha" && p.Species == Staryu && p.StoredSaveId == null));
    }

    [Fact]
    public void Describe_AndGames_ReadWell()
    {
        Assert.Equal("Pikachu ♀, N.10 à 20, chromatique, légal", GtsService.Describe(GtsWanted.Create(Pikachu, 20, 10, 1, true, true)));
        Assert.Equal("Pikachu, N.5 ou plus", GtsService.Describe(GtsWanted.Create(Pikachu, 5, 0, null, null, false)));
        Assert.Equal("Pikachu ♂, N.50, non chromatique", GtsService.Describe(GtsWanted.Create(Pikachu, 50, 50, 0, false, false)));
        Assert.Equal("Or, Argent, coréens", GtsService.Games(new TradeFamily(EntityContext.Gen2, GameBoyRegion.Korean)));
        Assert.Equal("Rouge, Bleu, Jaune (et Or, Argent, Cristal), japonais", GtsService.Games(new TradeFamily(EntityContext.Gen1, GameBoyRegion.Japanese)));
        Assert.Equal((ushort)29, GtsService.FindSpecies("nidoran♀"));
        Assert.Equal((ushort)133, GtsService.FindSpecies(" EVOLI "));
        Assert.Null(GtsService.FindSpecies("Pikachou"));
    }
}
