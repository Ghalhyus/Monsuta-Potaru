using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PKHeX.Core;
using MonsutaPotaru.Data;
using MonsutaPotaru.Services;

namespace MonsutaPotaru.Tests;

/// <summary>Suivi des rubans : ajout, Pokémon retrouvé par son empreinte (sauvegarde mise à jour, parti, revenu), tri, recherche.</summary>
public sealed class RibbonServiceTests : IDisposable
{
    private readonly TestDatabase _db = new("sacha", "ondine");
    private readonly PokemonInfo _info = new();
    private RibbonService Service() => new(_db.Factory, NullLogger<RibbonService>.Instance);

    public void Dispose() => _db.Dispose();

    /// <summary>Un Pokémon de Noir 2 au stockage interne (PID propre au dresseur et au numéro), avec ces rubans.</summary>
    private BankPokemon Bank(string owner, ushort species, int index, Action<PK5>? ribbons = null, bool egg = false)
    {
        var pk = SaveSessionTests.CreatePk5(species, 20, owner, index);
        ribbons?.Invoke(pk);
        pk.IsEgg = egg;
        pk.RefreshChecksum();
        using var db = _db.NewContext();
        var entry = BankService.CreateEntry(_info, pk, owner, DateTime.UtcNow);
        db.BankPokemon.Add(entry);
        db.SaveChanges();
        return entry;
    }

    /// <summary>Sauvegarde de Noir 2 stockée par ce compte, avec ce Pokémon en boîte 1, case 1.</summary>
    private async Task<(int SaveId, byte[] Data)> StoreSave(string owner, PK5 pk, int hours)
    {
        var sav = new SAV5B2W2 { OT = "Ludo", TID16 = 12345, SID16 = 4000, PlayedHours = hours };
        sav.SetBoxSlotAtIndex(pk, 0, 0);
        var data = sav.Write().ToArray();
        var saves = _db.StoredSaves();
        var existing = (await saves.ListAsync(owner)).SingleOrDefault();
        var result = existing is null ? await saves.AddAsync(owner, "noir2.sav", data) : await saves.UpdateAsync(owner, existing.Id, "noir2.sav", data);
        Assert.True(result.Ok, result.Message);
        return ((await saves.ListAsync(owner)).Single().Id, data);
    }

    [Fact]
    public async Task Add_ThenList_CountsTheRibbons()
    {
        var pikachu = Bank("sacha", 25, 1, pk => { pk.RibbonChampionSinnoh = true; pk.RibbonG3Cool = true; });
        var service = Service();

        var added = await service.AddAsync("sacha", pikachu.Id);
        Assert.True(added.Ok, added.Message);
        Assert.Equal("Pikachu ajouté au suivi des rubans.", added.Message);

        var card = Assert.Single(await service.ListAsync("sacha"));
        Assert.Equal((2, 88), (card.Owned, card.Total)); // Maître de Sinnoh, Sang-Froid de Hoenn
        Assert.Equal("Stockage interne", card.Location);
        Assert.True(card.Present);
        Assert.False(card.HasNickname);
        Assert.Empty(await service.ListAsync("ondine"));

        var view = await service.GetAsync("sacha", added.Id);
        Assert.NotNull(view);
        Assert.Equal(pikachu.Id, view.BankId);
        Assert.Equal(2, view.Earned);
        Assert.Equal(1, view.Ribbons.Single(r => r.Ribbon.Key == Ribbons.ContestMemory).Count);
        Assert.Null(await service.GetAsync("ondine", added.Id)); // pas son suivi
    }

    [Fact]
    public async Task Add_RefusesTwice_Eggs_AndOtherPlayersPokemon()
    {
        var pikachu = Bank("sacha", 25, 1);
        var egg = Bank("sacha", 172, 2, egg: true);
        var theirs = Bank("ondine", 120, 1);
        var service = Service();

        Assert.True((await service.AddAsync("sacha", pikachu.Id)).Ok);
        Assert.Equal("Pikachu est déjà suivi.", (await service.AddAsync("sacha", pikachu.Id)).Message);
        Assert.Equal("Un œuf ne peut pas avoir de rubans : fais-le éclore d'abord.", (await service.AddAsync("sacha", egg.Id)).Message);
        Assert.Equal("Ce Pokémon n'est plus dans ta collection.", (await service.AddAsync("sacha", theirs.Id)).Message); // trafiqué
        Assert.False((await service.AddAsync("sacha", 9999)).Ok);

        await using (var db = _db.NewContext())
            await db.BankPokemon.Where(p => p.Id == theirs.Id).ExecuteUpdateAsync(p => p.SetProperty(x => x.Fingerprint, ""));
        Assert.Equal("Ce Pokémon ne peut pas être suivi : il n'a pas pu être identifié.", (await service.AddAsync("ondine", theirs.Id)).Message);
    }

    [Fact]
    public async Task Candidates_SkipTrackedPokemonAndEggs_AndSearch()
    {
        var pikachu = Bank("sacha", 25, 1);
        Bank("sacha", 133, 2);
        Bank("sacha", 172, 3, egg: true);
        var service = Service();
        Assert.Equal(["Évoli", "Pikachu"], (await service.ListCandidatesAsync("sacha", null)).Select(c => c.SpeciesName));
        Assert.Equal(["Pikachu"], (await service.ListCandidatesAsync("sacha", " pika ")).Select(c => c.SpeciesName));

        await service.AddAsync("sacha", pikachu.Id);
        var candidate = Assert.Single(await service.ListCandidatesAsync("sacha", null));
        Assert.Equal(("Évoli", "Stockage interne", 20), (candidate.SpeciesName, candidate.Location, (int)candidate.Level));
    }

    /// <summary>
    /// Dans une sauvegarde stockée, le Pokémon change de ligne à chaque mise à jour : le suivi le retrouve par son empreinte
    /// et reprend ses nouveaux rubans ; parti, il garde les derniers ; revenu, il se remet à jour.
    /// </summary>
    [Fact]
    public async Task StoredSavePokemon_FollowedAcrossUpdates_AndKeptWhenGone()
    {
        var pk = SaveSessionTests.CreatePk5(25, 20, "Ludo", 7);
        await StoreSave("sacha", pk, 10);
        var service = Service();
        var first = (await service.ListCandidatesAsync("sacha", null)).Single();
        Assert.EndsWith(" — Ludo · Boîte 1, case 1", first.Location); // jeu, dresseur, place
        var id = (await service.AddAsync("sacha", first.BankId)).Id;

        // Mise à jour de la sauvegarde : un ruban de plus, nouvelle ligne en base
        pk.RibbonChampionSinnoh = true;
        pk.RibbonLegend = true;
        pk.RefreshChecksum();
        await StoreSave("sacha", pk, 11);
        var view = (await service.GetAsync("sacha", id))!;
        Assert.Equal(2, view.Earned);
        Assert.NotEqual(first.BankId, view.BankId);
        Assert.Equal(first.Location, view.Location);

        // Le Pokémon quitte la sauvegarde : le suivi reste, avec ses derniers rubans
        await StoreSave("sacha", SaveSessionTests.CreatePk5(133, 5, "Ludo", 8), 12);
        var gone = Assert.Single(await service.ListAsync("sacha"));
        Assert.False(gone.Present);
        Assert.Equal(2, gone.Owned);
        Assert.Null((await service.GetAsync("sacha", id))!.BankId);

        // Il revient (au stockage interne, avec un ruban de plus) : le suivi le retrouve
        pk.RibbonEffort = true;
        pk.RefreshChecksum();
        using (var db = _db.NewContext())
        {
            db.BankPokemon.Add(BankService.CreateEntry(_info, pk, "sacha", DateTime.UtcNow));
            db.SaveChanges();
        }
        var back = Assert.Single(await service.ListAsync("sacha"));
        Assert.Equal(("Stockage interne", 3), (back.Location, back.Owned));
    }

    [Fact]
    public async Task Delete_OnlyTheOwnersTracker_AndThePokemonStays()
    {
        var pikachu = Bank("sacha", 25, 1);
        var service = Service();
        var id = (await service.AddAsync("sacha", pikachu.Id)).Id;
        Assert.False(await service.DeleteAsync("ondine", id));
        Assert.True(await service.DeleteAsync("sacha", id));
        Assert.Empty(await service.ListAsync("sacha"));
        await using var db = _db.NewContext();
        Assert.True(await db.BankPokemon.AnyAsync(p => p.Id == pikachu.Id));
    }

    [Fact]
    public void Sort_ByArrivalNameOrCompletion_TiesInArrivalOrder()
    {
        var at = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        RibbonSummary Card(int id, string name, int owned) => new(id, name, name, "", 5, false, owned, 88, null, at.AddDays(id));
        List<RibbonSummary> cards = [Card(1, "Zarbi", 10), Card(2, "Évoli", 3), Card(3, "Abo", 10), Card(4, "Mew", 0)];

        Assert.Equal([1, 2, 3, 4], RibbonService.Sort(cards, RibbonSort.Arrival, false).Select(c => c.Id));
        Assert.Equal([4, 3, 2, 1], RibbonService.Sort(cards, RibbonSort.Arrival, true).Select(c => c.Id));
        Assert.Equal([3, 2, 4, 1], RibbonService.Sort(cards, RibbonSort.Name, false).Select(c => c.Id)); // « Évoli » parmi les E
        Assert.Equal([1, 4, 2, 3], RibbonService.Sort(cards, RibbonSort.Name, true).Select(c => c.Id));
        Assert.Equal([4, 2, 1, 3], RibbonService.Sort(cards, RibbonSort.Completion, false).Select(c => c.Id));
        Assert.Equal([1, 3, 2, 4], RibbonService.Sort(cards, RibbonSort.Completion, true).Select(c => c.Id));

        Assert.True(RibbonService.Matches(cards[1], "evo"));
        Assert.True(RibbonService.Matches(cards[1], "  "));
        Assert.False(RibbonService.Matches(cards[1], "pika"));
        Assert.True(RibbonService.Matches(new RibbonSummary(5, "Pikou", "Pikachu", "", 5, false, 0, 88, null, at), "pikach"));
        Assert.False(RibbonService.IsNickname("PIKACHU", "Pikachu"));
        Assert.True(RibbonService.IsNickname("Pikou", "Pikachu"));
    }
}
