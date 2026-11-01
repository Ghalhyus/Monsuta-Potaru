using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PKHeX.Core;
using MonsutaPotaru.Data;
using MonsutaPotaru.Services;

namespace MonsutaPotaru.Tests;

/// <summary>
/// « Parc » : choix des Pokémon (20 au plus, avec des sprites), parcs et agrandissements payés avec le porte-monnaie, un
/// parc à la fois par Pokémon, départ avec le Pokémon, sprites Donjon Mystère.
/// </summary>
public sealed class ParkServiceTests : IDisposable
{
    private readonly TestDatabase _db = new("sacha", "ondine");
    private readonly ParkService _park;
    private readonly InventoryService _inventory;

    public ParkServiceTests()
    {
        PokemonSprites.Load(RealSavesTests.WebRootWithImages()); // sprites Donjon Mystère : qui peut aller au parc
        ParkHabitats.Load(RealSavesTests.WebRoot()); // types de cases des cartes : humeur des Pokémon
        _park = new ParkService(_db.Factory, NullLogger<ParkService>.Instance);
        _inventory = _db.Inventory();
    }

    public void Dispose() => _db.Dispose();

    private int Add(ushort species, string owner = "sacha", bool egg = false, bool shiny = false, MoveType type = MoveType.Normal)
    {
        using var db = _db.NewContext();
        var entry = new BankPokemon
        {
            OwnerId = owner, Species = species, SpeciesName = $"Espèce {species}", Nickname = $"N{species}", Level = 10,
            IsEgg = egg, IsShiny = shiny, Nature = 0, DepositedAt = DateTime.UtcNow, Data = [1],
            Type1 = (byte)type, Type2 = (byte)type,
        };
        db.BankPokemon.Add(entry);
        db.SaveChanges();
        return entry.Id;
    }

    /// <summary>La Forêt du joueur (créée à sa première visite).</summary>
    private async Task<int> Forest(string owner = "sacha") =>
        (await _park.ListParksAsync(owner)).Single(p => p.Landscape == ParkLandscape.Forest).Id;

    private async Task<int> Expansions(int parkId, string owner = "sacha") =>
        (await _park.ListParksAsync(owner)).Single(p => p.Id == parkId).Expansions;

    [Fact]
    public async Task GetMember_OnlyAPokemonOfThatPark()
    {
        var pikachu = Add(25);
        var mew = Add(151); // dans la collection de Sacha, pas dans son parc
        var misty = Add(120, owner: "ondine");
        var (sacha, ondine) = (await Forest(), await Forest("ondine"));
        Assert.True((await _park.SetAsync("sacha", sacha, [pikachu])).Ok);
        Assert.True((await _park.SetAsync("ondine", ondine, [misty])).Ok);

        Assert.Equal(pikachu, (await _park.GetMemberAsync("sacha", sacha, pikachu))?.Id);
        Assert.Null(await _park.GetMemberAsync("sacha", sacha, mew)); // numéro envoyé par un navigateur modifié
        Assert.Null(await _park.GetMemberAsync("sacha", sacha, misty)); // dans un autre parc
        Assert.Null(await _park.GetMemberAsync("sacha", ondine, misty)); // le parc d'un autre joueur
        Assert.Null(await _park.GetMemberAsync("ondine", ondine, pikachu));
    }

    [Fact]
    public async Task List_GivesEachPokemonsMood_InTheParkOfItsSize()
    {
        var eevee = Add(133); // Normal : aime la végétation (69 à 80 % des cartes actuelles)
        var roggenrola = Add(524, type: MoveType.Rock); // Roche : ne l'aime pas
        var snorunt = Add(361, type: MoveType.Ice); // Glace : seul le volcan lui déplaît
        var forest = await Forest();
        Assert.True((await _park.SetAsync("sacha", forest, [eevee, roggenrola, snorunt])).Ok);
        var moods = (await _park.ListAsync("sacha", forest)).ToDictionary(p => p.Id, p => p.Mood);
        Assert.Equal("happy", moods[eevee]);
        Assert.Equal("sad", moods[roggenrola]);
        Assert.Equal("normal", moods[snorunt]);
    }

    private async Task SetWallet(string owner, long money)
    {
        using var db = _db.NewContext();
        db.Wallets.Add(new Wallet { OwnerId = owner, Money = money });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Choice_IsLimitedToTwentyFivePokemonWithSprites_OfTheTrainer()
    {
        var pikachu = Add(25);
        var mew = Add(151);
        var chikorita = Add(152); // 2e génération : bienvenue aussi
        var simisear = Add(514); // Flamoutan : pas encore de sprites chez SpriteCollab
        var egg = Add(1, egg: true);
        var misty = Add(120, owner: "ondine");
        var forest = await Forest();

        Assert.Equal("Espèce 514 n'a pas encore de sprites Donjon Mystère : il ne peut pas aller au parc.",
            (await _park.SetAsync("sacha", forest, [pikachu, simisear])).Message);
        Assert.False((await _park.SetAsync("sacha", forest, [egg])).Ok);
        Assert.False((await _park.SetAsync("sacha", forest, [misty])).Ok); // pas le sien
        var many = Enumerable.Range(0, 21).Select(_ => Add(19)).ToList();
        Assert.Equal("Le parc accueille 20 Pokémon au plus : retires-en 1.", (await _park.SetAsync("sacha", forest, many)).Message);
        Assert.Empty(await _park.ListAsync("sacha", forest));

        var result = await _park.SetAsync("sacha", forest, [mew, pikachu, chikorita]);
        Assert.True(result.Ok, result.Message);
        Assert.Equal("Parc mis à jour : 3 Pokémon.", result.Message);
        Assert.Equal(new[] { mew, pikachu, chikorita }.Order(), (await _park.ListAsync("sacha", forest)).Select(p => p.Id).Order());

        var candidates = await _park.ListCandidatesAsync("sacha", forest);
        Assert.DoesNotContain(candidates, c => c.Id == egg || c.Id == misty);
        Assert.Equal([pikachu, mew, chikorita], candidates.Where(c => c.InPark).Select(c => c.Id)); // par numéro de Pokédex
        Assert.All(candidates, c => Assert.Null(c.OtherPark));
        Assert.False(candidates.Single(c => c.Id == simisear).HasSprites); // grisé dans la fenêtre

        // Les pastilles de « Ma collection », dont la génération.
        var second = await _park.ListCandidatesAsync("sacha", forest, new PokemonCriteria { Generations = new HashSet<byte> { 2 } });
        Assert.Equal([chikorita], second.Select(c => c.Id));
    }

    [Fact]
    public async Task ListParks_CreatesTheForest_OnceOnly()
    {
        var first = await _park.ListParksAsync("sacha");
        var again = await _park.ListParksAsync("sacha");
        var forest = Assert.Single(again);
        Assert.Equal((first.Single().Id, ParkLandscape.Forest, 0, 0, 20), (forest.Id, forest.Landscape, forest.Expansions, forest.Count, forest.Capacity));
        Assert.Empty(await _park.ListParksAsync("inconnu")); // pas de compte : pas de parc
    }

    [Fact]
    public async Task Buying_AnotherLandscape_DebitsTheWallet_OnceOnly()
    {
        await SetWallet("sacha", 130_000);
        var forest = await Forest();

        var beach = await _park.BuyAsync("sacha", ParkLandscape.Beach);
        Assert.True(beach.Ok, beach.Message);
        Assert.Equal($"Nouveau parc : Plage. Il accueille 20 Pokémon, choisis ceux qui s'y installent. Solde : {InventoryService.FormatMoney(90_000)}.", beach.Message);
        Assert.Equal(90_000, beach.Balance);
        Assert.Equal([(forest, ParkLandscape.Forest), (beach.ParkId, ParkLandscape.Beach)],
            (await _park.ListParksAsync("sacha")).Select(p => (p.Id, p.Landscape)));

        // Paysage déjà acheté, la Forêt, paysage inconnu (valeurs envoyées par un navigateur modifié)
        Assert.Equal("Tu as déjà un parc « Plage ».", (await _park.BuyAsync("sacha", ParkLandscape.Beach)).Message);
        Assert.Equal("Tu as déjà un parc « Forêt ».", (await _park.BuyAsync("sacha", ParkLandscape.Forest)).Message);
        Assert.Equal("Ce paysage n'existe pas.", (await _park.BuyAsync("sacha", (ParkLandscape)99)).Message);
        Assert.Equal(90_000, await _inventory.GetBalanceAsync("sacha"));
        var operation = Assert.Single(await _inventory.ListOperationsAsync("sacha"));
        Assert.Equal((WalletOperationKind.BuyPark, -40_000L, 90_000L, "Nouveau parc : Plage"),
            (operation.Kind, operation.Amount, operation.BalanceAfter, operation.Details));

        // Chaque parc a sa taille
        Assert.True((await _park.ExpandAsync("sacha", beach.ParkId)).Ok);
        Assert.Equal((0, 1), (await Expansions(forest), await Expansions(beach.ParkId)));
        Assert.Equal("Plage : 20 → 40 Pokémon", (await _inventory.ListOperationsAsync("sacha"))[0].Details);
        Assert.Equal([ParkLandscape.Forest], (await _park.ListParksAsync("ondine")).Select(p => p.Landscape)); // Ondine : rien de changé
    }

    [Theory]
    [InlineData(0L)]      // pas de porte-monnaie
    [InlineData(39_999L)]
    public async Task Buying_IsRefused_WithoutEnoughMoney(long money)
    {
        if (money > 0) await SetWallet("sacha", money);

        var result = await _park.BuyAsync("sacha", ParkLandscape.Beach);
        Assert.False(result.Ok);
        Assert.Contains($"il manque {InventoryService.FormatMoney(40_000 - money)}", result.Message);
        Assert.Equal([ParkLandscape.Forest], (await _park.ListParksAsync("sacha")).Select(p => p.Landscape)); // transaction annulée
        Assert.Equal(money, await _inventory.GetBalanceAsync("sacha"));
        Assert.Empty(await _inventory.ListOperationsAsync("sacha"));
    }

    [Fact]
    public async Task APokemon_LivesInOneParkOnly_AndMovesWhenChosenElsewhere()
    {
        await SetWallet("sacha", 40_000);
        var squirtle = Add(7, type: MoveType.Water); // Eau : n'aime que l'eau
        var eevee = Add(133);
        var forest = await Forest();
        var beach = (await _park.BuyAsync("sacha", ParkLandscape.Beach)).ParkId;
        Assert.True((await _park.SetAsync("sacha", forest, [squirtle, eevee])).Ok);
        Assert.Equal("sad", (await _park.ListAsync("sacha", forest)).Single(p => p.Id == squirtle).Mood); // la végétation

        var candidates = await _park.ListCandidatesAsync("sacha", beach);
        Assert.All(candidates, c => Assert.Equal((false, ParkLandscape.Forest), (c.InPark, c.OtherPark)));

        var result = await _park.SetAsync("sacha", beach, [squirtle]);
        Assert.Equal("Parc mis à jour : 1 Pokémon, dont 1 venu d'un autre parc.", result.Message);
        Assert.Equal([eevee], (await _park.ListAsync("sacha", forest)).Select(p => p.Id));
        var swimmer = Assert.Single(await _park.ListAsync("sacha", beach));
        Assert.Equal((squirtle, "happy"), (swimmer.Id, swimmer.Mood)); // deux tiers de mer
        Assert.Equal([1, 1], (await _park.ListParksAsync("sacha")).Select(p => p.Count));

        var fromForest = await _park.ListCandidatesAsync("sacha", forest);
        Assert.Equal((false, ParkLandscape.Beach), fromForest.Where(c => c.Id == squirtle).Select(c => (c.InPark, c.OtherPark)).Single());
        Assert.Equal((true, (ParkLandscape?)null), fromForest.Where(c => c.Id == eevee).Select(c => (c.InPark, c.OtherPark)).Single());

        // Vider la plage ne touche pas la forêt
        Assert.True((await _park.ClearAsync("sacha", beach)).Ok);
        Assert.Equal([eevee], (await _park.ListAsync("sacha", forest)).Select(p => p.Id));
    }

    [Fact]
    public async Task ParkOfAnotherPlayer_IsRefused()
    {
        await SetWallet("sacha", 1_000_000);
        var pikachu = Add(25);
        var misty = Add(120, owner: "ondine");
        var ondine = await Forest("ondine");
        Assert.True((await _park.SetAsync("ondine", ondine, [misty])).Ok);

        // Numéro de parc envoyé par un navigateur modifié
        Assert.Equal("Ce parc n'existe pas.", (await _park.SetAsync("sacha", ondine, [pikachu])).Message);
        Assert.Equal("Ce parc n'existe pas.", (await _park.ExpandAsync("sacha", ondine)).Message);
        Assert.False((await _park.ClearAsync("sacha", ondine)).Ok);
        Assert.Empty(await _park.ListAsync("sacha", ondine));
        Assert.DoesNotContain(await _park.ListCandidatesAsync("sacha", ondine), c => c.InPark);

        Assert.Equal([misty], (await _park.ListAsync("ondine", ondine)).Select(p => p.Id));
        Assert.Equal(0, await Expansions(ondine, "ondine"));
        Assert.Equal(1_000_000, await _inventory.GetBalanceAsync("sacha"));
    }

    [Fact]
    public async Task Expanding_DebitsTheWallet_AndAddsTwentyPokemon_ThreeTimesAtMost()
    {
        await SetWallet("sacha", 130_000);
        await SetWallet("ondine", 1_000_000);
        var many = Enumerable.Range(0, 41).Select(_ => Add(19)).ToList();
        var forest = await Forest();

        var first = await _park.ExpandAsync("sacha", forest);
        Assert.True(first.Ok, first.Message);
        Assert.Equal((1, 90_000L), (first.Expansions, first.Balance));
        Assert.StartsWith("Parc agrandi : il accueille maintenant 40 Pokémon.", first.Message);
        Assert.True((await _park.SetAsync("sacha", forest, many[..40])).Ok);
        Assert.Equal("Le parc accueille 40 Pokémon au plus : retires-en 1.", (await _park.SetAsync("sacha", forest, many)).Message);

        Assert.True((await _park.ExpandAsync("sacha", forest)).Ok);
        var third = await _park.ExpandAsync("sacha", forest);
        Assert.Equal((true, 3, 10_000L), (third.Ok, third.Expansions, third.Balance));
        Assert.Equal(80, ParkService.CapacityOf(await Expansions(forest)));

        var fourth = await _park.ExpandAsync("sacha", forest); // la plus grande taille pour l'instant
        Assert.False(fourth.Ok);
        Assert.Equal("Ce parc a déjà sa plus grande taille (80 Pokémon).", fourth.Message);
        Assert.Equal(3, await Expansions(forest));
        Assert.Equal(10_000, await _inventory.GetBalanceAsync("sacha"));

        var operations = await _inventory.ListOperationsAsync("sacha"); // les plus récentes d'abord
        Assert.All(operations, op => Assert.Equal((WalletOperationKind.ExpandPark, -40_000L), (op.Kind, op.Amount)));
        Assert.Equal(["Forêt : 60 → 80 Pokémon", "Forêt : 40 → 60 Pokémon", "Forêt : 20 → 40 Pokémon"], operations.Select(o => o.Details));
        Assert.Equal(0, await Expansions(await Forest("ondine"), "ondine")); // l'achat de Sacha ne touche pas Ondine
        Assert.Equal(1_000_000, await _inventory.GetBalanceAsync("ondine"));
    }

    [Theory]
    [InlineData(0L)]      // pas de porte-monnaie
    [InlineData(39_999L)]
    public async Task Expanding_IsRefused_WithoutEnoughMoney(long money)
    {
        if (money > 0) await SetWallet("sacha", money);
        var forest = await Forest();

        var result = await _park.ExpandAsync("sacha", forest);
        Assert.False(result.Ok);
        Assert.Contains($"il manque {InventoryService.FormatMoney(40_000 - money)}", result.Message);
        Assert.Equal(0, await Expansions(forest)); // taille remise comme avant (transaction annulée)
        Assert.Equal(money, await _inventory.GetBalanceAsync("sacha"));
        Assert.Empty(await _inventory.ListOperationsAsync("sacha"));
    }

    [Fact]
    public async Task ParkAboveItsCapacity_KeepsItsPokemon_ButTakesNoNewOne()
    {
        // 30 Pokémon : la capacité d'avant les agrandissements
        var old = Enumerable.Range(0, 30).Select(_ => Add(19)).ToList();
        var forest = await Forest();
        using (var db = _db.NewContext())
        {
            db.ParkMembers.AddRange(old.Select(id => new ParkMember { BankPokemonId = id, OwnerId = "sacha", ParkId = forest, AddedAt = DateTime.UtcNow }));
            await db.SaveChangesAsync();
        }
        var newcomer = Add(25);

        Assert.True((await _park.SetAsync("sacha", forest, old[..29])).Ok); // en retirer un : permis, même au-dessus de 20
        Assert.Equal("Le parc accueille 20 Pokémon au plus : retires-en 9.", (await _park.SetAsync("sacha", forest, [.. old[..28], newcomer])).Message);
        Assert.False((await _park.SetAsync("sacha", forest, old)).Ok); // celui retiré ne revient pas tant que le parc dépasse
        Assert.Equal(29, (await _park.ListAsync("sacha", forest)).Count);
        Assert.True((await _park.SetAsync("sacha", forest, [.. old[..19], newcomer])).Ok); // revenu à 20 : de nouveau ouvert
    }

    [Fact]
    public async Task NewChoice_KeepsThoseWhoStay_InTheirOrder()
    {
        var (a, b, c) = (Add(1), Add(4), Add(7));
        var forest = await Forest();
        await _park.SetAsync("sacha", forest, [a, b]);
        await Task.Delay(10);
        await _park.SetAsync("sacha", forest, [c, b]);
        Assert.Equal([b, c], (await _park.ListAsync("sacha", forest)).Select(p => p.Id)); // b était là avant c
        Assert.True(await _park.RemoveAsync("sacha", b));
        Assert.False(await _park.RemoveAsync("ondine", c)); // pas le sien
        Assert.Equal([c], (await _park.ListAsync("sacha", forest)).Select(p => p.Id));
    }

    [Fact]
    public async Task Clear_EmptiesOnlyThatPark_AndKeepsTheCollection()
    {
        var (a, b) = (Add(1), Add(4));
        var misty = Add(120, owner: "ondine");
        var (sacha, ondine) = (await Forest(), await Forest("ondine"));
        await _park.SetAsync("sacha", sacha, [a, b]);
        await _park.SetAsync("ondine", ondine, [misty]);

        var result = await _park.ClearAsync("sacha", sacha);
        Assert.True(result.Ok, result.Message);
        Assert.Equal("Le parc est vide : ses 2 Pokémon restent dans ta collection.", result.Message);
        Assert.Empty(await _park.ListAsync("sacha", sacha));
        Assert.Equal([misty], (await _park.ListAsync("ondine", ondine)).Select(p => p.Id)); // le parc d'Ondine ne bouge pas
        using (var db = _db.NewContext())
            Assert.Equal(2, db.BankPokemon.Count(p => p.OwnerId == "sacha"));
        Assert.Equal("Le parc est déjà vide.", (await _park.ClearAsync("sacha", sacha)).Message);
    }

    [Fact]
    public async Task PokemonLeavesThePark_WhenItLeavesTheCollection()
    {
        var pikachu = Add(25);
        var forest = await Forest();
        await _park.SetAsync("sacha", forest, [pikachu]);
        using (var db = _db.NewContext())
            await db.BankPokemon.Where(p => p.Id == pikachu).ExecuteDeleteAsync(); // comme une sauvegarde mise à jour ou supprimée
        Assert.Empty(await _park.ListAsync("sacha", forest));
        using (var db = _db.NewContext())
            Assert.Empty(db.ParkMembers);
    }

    [Fact]
    public void Sprites_AndFamilies()
    {
        static string Folder(ushort species, byte form = 0, EntityContext context = EntityContext.Gen9, bool shiny = false,
            bool female = false, int heldItem = 0) =>
            PokemonSprites.DonjonFolder(species, form, context, shiny, female, heldItem).Replace("images/donjon/", "");
        Assert.Equal("0025/", Folder(25));
        Assert.Equal("0025/0000/0001/", Folder(25, shiny: true)); // chromatique
        Assert.Equal("0025/0000/0000/0002/", Folder(25, female: true)); // femelle
        Assert.Equal("0016/", Folder(16, shiny: true)); // Roucool : pas de chromatique
        Assert.Equal("0026/0001/", Folder(26, 1, EntityContext.Gen7)); // Raichu d'Alola
        Assert.Equal("0077/0001/", Folder(77, 1, EntityContext.Gen8)); // Ponyta de Galar
        Assert.Equal("0128/0001/", Folder(128, 1)); // Tauros de Paldea, race Combative : « Paldea » chez SpriteCollab
        Assert.Equal("0201/0001/", Folder(201, 1, EntityContext.Gen4)); // Zarbi B
        Assert.Equal("0006/0001/", Folder(6, heldItem: ItemStorage9ZA.GetExpectedMegaStoneOrPrimalOrb(6, 1))); // Méga-Dracaufeu X
        Assert.Equal("0006/", Folder(6, heldItem: ItemStorage9ZA.GetExpectedMegaStoneOrPrimalOrb(6, 2))); // Méga Y : pas de sprite
        Assert.True(PokemonSprites.HasDonjonSprites(152));
        Assert.False(PokemonSprites.HasDonjonSprites(514));
        Assert.Equal(172, ParkService.Family(26)); // Raichu : famille de Pichu
        Assert.Equal(ParkService.Family(1), ParkService.Family(3)); // Bulbizarre, Florizarre
    }
}
