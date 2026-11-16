using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MonsutaPotaru.Data;
using MonsutaPotaru.Services;

namespace MonsutaPotaru.Tests;

/// <summary>
/// Mondes Minecraft (All the Mons) dans « Mes sauvegardes » : identité (joueur + graine), temps de jeu, mise à jour du
/// même monde seulement, fichier rendu (dossier pokemon seul), refus dans « Dépôt / Retrait », et aucune fonction des jeux
/// (3DS, inventaire, living dex, complétion, échanges, GTS).
/// </summary>
public sealed class StoredWorldTests : IDisposable
{
    private readonly TestDatabase _db = new("sacha", "ondine");
    private readonly StoredSaveService _saves;

    public StoredWorldTests() => _saves = _db.StoredSaves();

    public void Dispose() => _db.Dispose();

    private async Task<StoredSaveInfo> StoreWorld(params CobblemonPokemon[] pc)
    {
        var result = await _saves.AddAsync("sacha", "Kantalia.zip", CobblemonTests.World(pc));
        Assert.True(result.Ok, result.Message);
        return (await _saves.ListAsync("sacha")).Single(s => s.IsMinecraft);
    }

    [Fact]
    public async Task Add_StoresTheWorld_ThenGivesBackOnlyThePokemonFolder()
    {
        var result = await _saves.AddAsync("sacha", "Kantalia.zip", CobblemonTests.World(CobblemonTests.Mon("eevee", 1)));
        Assert.True(result.Ok, result.Message);
        Assert.Equal("Monde « Kantalia » stocké : 2 Pokémon ajoutés à ta collection.", result.Message);

        var info = Assert.Single(await _saves.ListAsync("sacha"));
        Assert.True(info.IsMinecraft);
        Assert.Equal("All the Mons — Kantalia", info.Label);
        Assert.Equal("", info.TrainerId);
        Assert.Equal(29598 / 20, info.PlayTimeSeconds);
        Assert.Equal(2, info.PokemonCount);

        // Dans « Ma collection », en lecture seule, avec leur place
        var bank = _db.Bank();
        var items = await bank.GetRangeAsync("sacha", null, 0, 10, CollectionFilter.Save(info.Id));
        Assert.All(items, i => Assert.True(i.IsMinecraft));
        Assert.Equal(["Équipe 1", "Boîte 1, case 1"], items.OrderBy(i => i.Location!.Length).Select(i => i.Location));
        await using (var db = _db.NewContext())
        {
            Assert.Equal("sacha", (await db.BankPokemon.SingleAsync(p => p.Location == "Équipe 1")).OriginalTrainer);
            // Seuls les fichiers utiles sont gardés sur le serveur
            var stored = await db.StoredSaves.SingleAsync();
            Assert.Equal(CobblemonTests.Unzip(CobblemonTests.RealZip()).Keys.Order(), CobblemonTests.Unzip(stored.Data).Keys.Order());
            Assert.Equal(0, stored.Generation);
        }

        // Téléchargement : le dossier pokemon seul, level.dat et stats restent ceux du jeu
        var (fileName, data) = (await _saves.DownloadAsync("sacha", info.Id))!.Value;
        Assert.Equal("Kantalia.zip", fileName);
        Assert.All(CobblemonTests.Unzip(data).Keys, k => Assert.StartsWith("pokemon/", k));

        // Une seule fois sur tout le serveur
        Assert.Equal(StoreStatus.AlreadyStored, (await _saves.AddAsync("sacha", "copie.zip", CobblemonTests.World())).Status);
        Assert.Equal(StoredSaveService.OtherAccountMessage, (await _saves.AddAsync("ondine", "copie.zip", CobblemonTests.World())).Message);
        // Un autre monde du même joueur (autre graine) est un autre monde
        Assert.Equal(StoreStatus.Duplicate, (await _saves.AddAsync("sacha", "autre.zip", CobblemonTests.WorldVariant(seed: 42))).Status); // mêmes Pokémon

        // Suppression : le dossier pokemon est rendu, les Pokémon quittent la collection
        var deleted = (await _saves.DeleteAsync("sacha", info.Id))!.Value;
        Assert.All(CobblemonTests.Unzip(deleted.Data).Keys, k => Assert.StartsWith("pokemon/", k));
        Assert.Empty(await _saves.ListAsync("sacha"));
        Assert.Equal(0, await bank.CountAsync("sacha"));
        Assert.Contains(Directory.GetFiles(Path.Combine(_db.BackupRoot, "sacha")), f => f.EndsWith("_Kantalia_stockee-supprimee.zip"));
    }

    [Fact]
    public async Task Add_NeedsLevelDatAndStats_AndRoom()
    {
        Assert.Equal(CobblemonSave.NoLevelMessage, (await _saves.AddAsync("sacha", "w.zip", CobblemonTests.WorldVariant(withoutLevel: true))).Message);
        Assert.Equal(CobblemonSave.NoStatsMessage, (await _saves.AddAsync("sacha", "w.zip", CobblemonTests.WorldVariant(withoutStats: true))).Message);
        Assert.Contains("Zip illisible", (await _saves.AddAsync("sacha", "w.zip", [(byte)'P', (byte)'K', 3, 4, 9])).Message);

        await using (var db = _db.NewContext())
            await db.Users.Where(u => u.Id == "sacha").ExecuteUpdateAsync(u => u.SetProperty(x => x.CollectionLimit, 1));
        Assert.Equal(StoreStatus.CollectionFull, (await _saves.AddAsync("sacha", "w.zip", CobblemonTests.World(CobblemonTests.Mon("eevee", 1)))).Status);
        Assert.Empty(await _saves.ListAsync("sacha"));
    }

    [Fact]
    public async Task Add_RefusesAnOldCopy()
    {
        // Le Feunnec est déposé dans le stockage interne : le monde d'avant le dépôt est une vieille copie
        var session = new SaveSession();
        Assert.True(session.TryLoad(CobblemonTests.World(), "w.zip", out _));
        Assert.True(session.TryDeposit(0, 0, out _));
        await _db.Bank().CommitAsync("sacha", session);
        var refused = await _saves.AddAsync("sacha", "w.zip", CobblemonTests.World());
        Assert.Equal(StoreStatus.Duplicate, refused.Status);
        Assert.Contains("Feunnec", refused.Message);
    }

    [Fact]
    public async Task Update_SameWorldOnly_WithMorePlayTime()
    {
        var info = await StoreWorld();
        var newer = await _saves.UpdateAsync("sacha", info.Id, "Kantalia.zip",
            CobblemonTests.WorldVariant(playTimeSeconds: 5000, pc: [CobblemonTests.Mon("eevee", 1)]));
        Assert.True(newer.Ok, newer.Message);
        info = Assert.Single(await _saves.ListAsync("sacha"));
        Assert.Equal((5000, 2), (info.PlayTimeSeconds, info.PokemonCount));
        Assert.Contains(Directory.GetFiles(Path.Combine(_db.BackupRoot, "sacha")), f => f.EndsWith("_Kantalia_stockee-remplacee.zip"));

        // Moins de temps de jeu : confirmation
        var older = CobblemonTests.WorldVariant(playTimeSeconds: 10);
        var asked = await _saves.UpdateAsync("sacha", info.Id, "Kantalia.zip", older);
        Assert.Equal((StoreStatus.OlderPlayTime, 5000, 10), (asked.Status, asked.OldPlayTime, asked.NewPlayTime));
        Assert.True((await _saves.UpdateAsync("sacha", info.Id, "Kantalia.zip", older, acceptOlder: true)).Ok);

        // Autre monde, jeu, fichiers manquants, sauvegarde absente
        Assert.Contains("pas le même monde", (await _saves.UpdateAsync("sacha", info.Id, "x.zip", CobblemonTests.WorldVariant(seed: 1))).Message);
        Assert.Contains("pas ce monde Minecraft", (await _saves.UpdateAsync("sacha", info.Id, "noir2.sav", SaveSessionTests.CreateBw2Save())).Message);
        Assert.Equal(CobblemonSave.NoStatsMessage, (await _saves.UpdateAsync("sacha", info.Id, "x.zip", CobblemonTests.WorldVariant(withoutStats: true))).Message);
        Assert.Equal(StoreStatus.NotFound, (await _saves.UpdateAsync("ondine", info.Id, "x.zip", CobblemonTests.World())).Status);

        // Une sauvegarde de jeu ne se remplace pas par un monde
        Assert.True((await _saves.AddAsync("sacha", "noir2.sav", SaveSessionTests.CreateBw2Save())).Ok);
        var game = (await _saves.ListAsync("sacha")).Single(s => !s.IsMinecraft);
        Assert.Contains("monde Minecraft, pas cette partie", (await _saves.UpdateAsync("sacha", game.Id, "x.zip", CobblemonTests.World())).Message);

        // Collection trop petite pour la nouvelle version
        await using (var db = _db.NewContext())
            await db.Users.Where(u => u.Id == "sacha").ExecuteUpdateAsync(u => u.SetProperty(x => x.CollectionLimit, 2));
        var full = await _saves.UpdateAsync("sacha", info.Id, "Kantalia.zip",
            CobblemonTests.WorldVariant(playTimeSeconds: 6000, pc: [CobblemonTests.Mon("eevee", 1), CobblemonTests.Mon("pikachu", 2)]));
        Assert.Equal(StoreStatus.CollectionFull, full.Status);
    }

    [Fact]
    public async Task StoredWorld_IsRefusedInTransfer()
    {
        await StoreWorld();
        var session = new SaveSession();
        Assert.True(session.TryLoad(CobblemonTests.World(), "w.zip", out _));
        Assert.NotNull(await _saves.FindHolderAsync(session.IdentityKey!)); // la page refuse le chargement
        Assert.True(session.TryDeposit(0, 0, out _));
        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => _db.Bank().CommitAsync("sacha", session));
        Assert.Contains("stockée sur le serveur", refused.Message);
    }

    [Fact]
    public async Task StoredWorld_StaysOutOfGameFeatures()
    {
        Assert.True((await _saves.AddAsync("sacha", "noir2.sav", SaveSessionTests.CreateBw2Save())).Ok);
        var world = await StoreWorld();

        // Living dex : seulement celui d'All the Mons ; complétion : pas proposé
        var dexes = new LivingDexService(_db.Factory, NullLogger<LivingDexService>.Instance);
        Assert.Equal([LivingDexGame.AllTheMons], (await dexes.ListCandidatesAsync("sacha")).Single(c => c.SaveId == world.Id).Games);
        Assert.False((await dexes.CreateAsync("sacha", world.Id, LivingDexGame.Global)).Ok);
        var completions = new CompletionService(_db.Factory, NullLogger<CompletionService>.Instance);
        Assert.DoesNotContain(await completions.ListCandidatesAsync("sacha"), c => c.SaveId == world.Id);

        // Inventaire : ni argent ni objets à lire
        var inventory = _db.Inventory();
        Assert.DoesNotContain(await inventory.ListMoneySourcesAsync("sacha"), s => s.SaveId == world.Id);
        Assert.DoesNotContain(await inventory.ListItemSourcesAsync("sacha"), s => s.SaveId == world.Id);
        Assert.False((await inventory.WithdrawFromSaveAsync("sacha", world.Id, 100)).Ok);

        // Échange interne : absent
        Assert.DoesNotContain(await _db.Trades().InternalSavesAsync("sacha"), s => s.Save.Id == world.Id);

        // 3DS : jamais envoyé à une console
        var sync = _db.Sync();
        var code = await sync.CreatePairingCodeAsync("sacha");
        var link = await sync.LinkAsync(code.Code, "3DS");
        var caller = (await sync.AuthenticateAsync(link.Token))!;
        var entries = await sync.ListForDeviceAsync(caller);
        Assert.Single(entries);
        Assert.DoesNotContain(entries, e => e.Id == world.Id);

        // GTS : ses Pokémon ne partent pas
        var item = (await _db.Bank().GetRangeAsync("sacha", null, 0, 10, CollectionFilter.Save(world.Id))).Single();
        Assert.Equal(GtsService.MinecraftRefusal, GtsService.PickProblem(item));
        Assert.Null(await _db.Gts().DepositChoiceAsync("sacha", item.Id));
    }
}
