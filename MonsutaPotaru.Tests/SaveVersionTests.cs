using Microsoft.EntityFrameworkCore;
using PKHeX.Core;
using MonsutaPotaru.Data;
using MonsutaPotaru.Services;

namespace MonsutaPotaru.Tests;

/// <summary>
/// Rouge Feu / Vert Feuille : PKHeX répond toujours « Rouge Feu » ; <see cref="SaveVersion"/> reconnaît Vert Feuille
/// d'après le jeu d'origine des Pokémon du dresseur (vraies sauvegardes de <c>Sauvegardes/</c>).
/// </summary>
public sealed class SaveVersionTests : IDisposable
{
    private readonly TestDatabase _db = new("sacha");
    private readonly PokemonInfo _info = new();

    private static byte[] RealSave(string file) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Sauvegardes", file));

    private static SaveFile Read(string file)
    {
        Assert.True(SaveUtil.TryGetSaveFile(RealSave(file), out var sav, file));
        SaveVersion.Refine(sav!);
        return sav!;
    }

    [Fact]
    public void LeafGreen_IsRecognised_FromTrainersPokemon()
    {
        Assert.True(SaveUtil.TryGetSaveFile(RealSave("vert-feuille.sav"), out var raw));
        Assert.Equal(GameVersion.FR, raw!.Version); // ce que dit PKHeX

        var sav = Read("vert-feuille.sav");
        Assert.Equal(GameVersion.LG, sav.Version);
        Assert.Equal("Vert Feuille", _info.GameName(sav));
        Assert.Equal("Defense", BattleTeam.DeoxysForme(sav.Version));
        Assert.Equal("-defense", BattleSprites.For(sav).DeoxysSuffix);
        Assert.Equal([LivingDexGame.FireRed, LivingDexGame.LeafGreen], LivingDexGames.For(sav)); // toujours les deux

        // Clé d'identité inchangée : celle des sauvegardes stockées avant, et stable au fil de la partie.
        Assert.Equal(SaveIdentity.Of(raw).Key, SaveIdentity.Of(sav).Key);
        // Rien n'est écrit dans le fichier.
        Assert.Equal(raw.Write().ToArray(), sav.Write().ToArray());
    }

    [Fact]
    public void FireRed_StaysFireRed()
    {
        var sav = Read("rouge-feu.sav");
        Assert.Equal(GameVersion.FR, sav.Version);
        Assert.Equal("Rouge Feu", _info.GameName(sav));
    }

    [Fact]
    public void WithoutTrainersPokemon_KeepsPkhexAnswer()
    {
        var sav = new SAV3FRLG { OT = "Sacha", TID16 = 1, SID16 = 2 };
        var traded = new PK3 { Species = 25, TID16 = 9, SID16 = 9, OriginalTrainerName = "Autre", Version = GameVersion.LG };
        sav.SetBoxSlotAtIndex(traded, 0, 0); // reçu par échange : ne compte pas
        SaveVersion.Refine(sav);
        Assert.Equal(GameVersion.FR, sav.Version);
    }

    [Fact]
    public void SaveSession_RecognisesLeafGreen()
    {
        var session = new SaveSession();
        Assert.True(session.TryLoad(RealSave("vert-feuille.sav"), "vert-feuille.sav", out var error), error);
        Assert.Equal(GameVersion.LG, session.Save!.Version);
    }

    [Fact]
    public async Task StoredSaves_AreNamedLeafGreen_AndOldOnesRenamedAtStartup()
    {
        var saves = _db.StoredSaves();
        var result = await saves.AddAsync("sacha", "vert-feuille.sav", RealSave("vert-feuille.sav"));
        Assert.True(result.Ok, result.Message);
        Assert.Equal("Vert Feuille", Assert.Single(await saves.ListAsync("sacha")).GameName);

        // Sauvegarde enregistrée avant la correction : notée « Rouge Feu », renommée au démarrage.
        await using (var db = _db.NewContext())
            await db.StoredSaves.ExecuteUpdateAsync(s => s.SetProperty(x => x.GameName, "Rouge Feu"));
        await using (var db = _db.NewContext())
            await StoredSaveService.FillGameNamesAsync(db, _info);
        Assert.Equal("Vert Feuille", Assert.Single(await saves.ListAsync("sacha")).GameName);

        // La mise à jour du même fichier est acceptée (même clé d'identité).
        var id = Assert.Single(await saves.ListAsync("sacha")).Id;
        Assert.NotEqual(StoreStatus.WrongTrainer, (await saves.UpdateAsync("sacha", id, "vert-feuille.sav", RealSave("vert-feuille.sav"))).Status);
    }

    public void Dispose() => _db.Dispose();
}
