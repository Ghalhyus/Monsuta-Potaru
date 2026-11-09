using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MonsutaPotaru.Components.Pages;
using PKHeX.Core;

namespace MonsutaPotaru.Tests;

/// <summary>Page « Échange interne » : les deux sauvegardes, le Pokémon envoyé, le reçu, la confirmation, et les retours.</summary>
public sealed class InternalTradePageTests : PageTestContext
{
    private NavigationManager Navigation => Services.GetRequiredService<NavigationManager>();

    /// <summary>Noire 2 de Sacha (Pikachu dans l'équipe, Kadabra en boîte) et de Regis (Stari, Carapuce en boîte).</summary>
    private async Task<(int Sacha, int Regis)> StoreTwo()
    {
        Assert.True((await StoredSaves.AddAsync("sacha", "sacha.sav", TradeServiceTests.Bw2("Sacha", 1, [25], 64))).Ok);
        Assert.True((await StoredSaves.AddAsync("sacha", "regis.sav", TradeServiceTests.Bw2("Regis", 2, [120], 7))).Ok);
        var saves = await StoredSaves.ListAsync("sacha");
        return (saves.Single(s => s.FileName == "sacha.sav").Id, saves.Single(s => s.FileName == "regis.sav").Id);
    }

    private static Task Pick<T>(IRenderedComponent<T> cut, string name) where T : IComponent =>
        cut.FindAll(".trade-candidate").First(b => b.TextContent.Contains(name)).ClickAsync(new());

    private async Task<SaveFile> Read(int saveId)
    {
        await using var db = Db.NewContext();
        var save = await db.StoredSaves.SingleAsync(s => s.Id == saveId);
        Assert.True(SaveUtil.TryGetSaveFile(save.Data.ToArray(), out var sav, save.FileName));
        return sav!;
    }

    [Fact]
    public async Task FullTrade_WithBackAtEachStep()
    {
        var (sacha, regis) = await StoreTwo();
        await StoreReal("sacha", "soulsilver.sav"); // seule de sa génération : grisée
        var cut = Render<InternalTrade>();

        // 1. Les deux sauvegardes
        cut.WaitForText("1. Choisis les deux sauvegardes");
        Assert.Contains("(aucune sauvegarde compatible)", cut.Find("select.internal-trade-first").TextContent);
        Assert.True(cut.Find("select.internal-trade-second").HasAttribute("disabled"));
        await cut.Find("select.internal-trade-first").ChangeAsync(new() { Value = sacha.ToString() });
        Assert.Equal(2, cut.FindAll("select.internal-trade-second option").Count); // « Choisis… » et Regis, pas elle-même
        Assert.True(cut.Button("Valider").HasAttribute("disabled"));
        await cut.Find("select.internal-trade-second").ChangeAsync(new() { Value = regis.ToString() });
        await cut.Click("Valider");
        Assert.EndsWith($"echange-interne?de={sacha}&vers={regis}", Navigation.Uri);

        // 2. Le Pokémon envoyé ; Retour garde les deux sauvegardes
        cut.WaitForText("2. Choisis le Pokémon envoyé");
        await cut.Click("Retour");
        cut.WaitForText("1. Choisis les deux sauvegardes");
        Assert.Equal(regis.ToString(), cut.Find("select.internal-trade-second").GetAttribute("value"));
        await cut.Click("Valider");
        cut.WaitForText("2. Choisis le Pokémon envoyé");
        Assert.True(cut.Button("Valider").HasAttribute("disabled"));
        await Pick(cut, "Kadabra");

        // 3. Le Pokémon reçu ; Retour garde le Kadabra
        await cut.Click("Valider");
        cut.WaitForText("3. Choisis le Pokémon reçu");
        Assert.Contains("en échange de Kadabra", cut.Text());
        await cut.Click("Retour");
        cut.WaitForText("2. Choisis le Pokémon envoyé");
        Assert.Contains("Kadabra", cut.Find(".trade-candidate.selected").TextContent);
        await cut.Click("Valider");
        cut.WaitForText("3. Choisis le Pokémon reçu");
        await Pick(cut, "Stari");

        // 4. Confirmation, évolution annoncée ; Retour garde le Stari
        await cut.Click("Valider");
        cut.WaitForText("Kadabra évoluera en Alakazam");
        await cut.Click("Retour");
        cut.WaitForText("3. Choisis le Pokémon reçu");
        Assert.Contains("Stari", cut.Find(".trade-candidate.selected").TextContent);
        await cut.Click("Valider");
        cut.WaitForText("4. Confirme l'échange");
        await cut.Click("Échanger");
        cut.WaitForText("Échange réussi");
        Assert.Contains("a reçu Kadabra, qui a évolué en Alakazam", cut.Text());

        Assert.Equal(120, (await Read(sacha)).GetBoxSlotAtIndex(0, 0).Species);
        Assert.Equal(65, (await Read(regis)).GetPartySlotAtIndex(0).Species);

        // Nouvel échange : retour aux sauvegardes, toujours choisies ; elles sont maintenant à télécharger.
        await cut.Click("Nouvel échange");
        cut.WaitForText("1. Choisis les deux sauvegardes");
        cut.WaitForText("pas encore téléchargée");
    }

    [Fact]
    public async Task Search_FiltersByNameOrPlace()
    {
        var (sacha, regis) = await StoreTwo();
        Navigation.NavigateTo($"echange-interne?de={sacha}&vers={regis}");
        var cut = Render<InternalTrade>();
        cut.WaitForText("2. Choisis le Pokémon envoyé");
        Assert.Equal(2, cut.FindAll(".trade-candidate").Count);
        await cut.Find(".internal-trade-search").InputAsync(new() { Value = "boite 1" }); // sans accent
        Assert.Contains("Kadabra", Assert.Single(cut.FindAll(".trade-candidate")).TextContent);
        await cut.Find(".internal-trade-search").InputAsync(new() { Value = "Mew" });
        cut.WaitForText("Aucun Pokémon ne correspond à « Mew »");
    }

    [Fact]
    public async Task PairInTheAddress_FromAnotherAccount_IsRefused()
    {
        var (sacha, _) = await StoreTwo();
        var hers = await StoreBw2("ondine", "Ondine");
        Navigation.NavigateTo($"echange-interne?de={sacha}&vers={hers}");
        var cut = Render<InternalTrade>();
        cut.WaitForText("Ces deux sauvegardes ne peuvent pas échanger");
        cut.WaitForText("1. Choisis les deux sauvegardes");
        Assert.Equal(sacha.ToString(), cut.Find("select.internal-trade-first").GetAttribute("value"));

        // Valeur trafiquée dans la liste : la sauvegarde d'Ondine n'est pas retenue.
        await cut.Find("select.internal-trade-second").ChangeAsync(new() { Value = hers.ToString() });
        Assert.True(cut.Button("Valider").HasAttribute("disabled"));
        await cut.Find("select.internal-trade-first").ChangeAsync(new() { Value = hers.ToString() });
        Assert.True(cut.Find("select.internal-trade-second").HasAttribute("disabled"));
    }

    [Fact]
    public async Task WithoutCompatibleSaves_SaysSo()
    {
        await StoreBw2("sacha", "Sacha");
        await StoreReal("sacha", "soulsilver.sav");
        var cut = Render<InternalTrade>();
        cut.WaitForText("Il te faut deux sauvegardes stockées qui peuvent échanger");
    }

    [Fact]
    public async Task ASaveThatChanged_SendsBackToTheChoice()
    {
        var (sacha, regis) = await StoreTwo();
        Navigation.NavigateTo($"echange-interne?de={sacha}&vers={regis}");
        var cut = Render<InternalTrade>();
        cut.WaitForText("2. Choisis le Pokémon envoyé");
        await Pick(cut, "Pikachu");
        await cut.Click("Valider");
        cut.WaitForText("3. Choisis le Pokémon reçu");
        await Pick(cut, "Carapuce");
        await cut.Click("Valider");
        cut.WaitForText("4. Confirme l'échange");

        // La sauvegarde de Regis est mise à jour entre-temps (sa boîte change) : on rechoisit.
        var other = TradeServiceTests.Bw2("Regis", 2, [120]);
        await using (var db = Db.NewContext())
        {
            var save = await db.StoredSaves.SingleAsync(s => s.Id == regis);
            save.Data = other;
            await db.SaveChangesAsync();
        }
        await cut.Click("Échanger");
        cut.WaitForText("a changé depuis le choix");
        cut.WaitForText("2. Choisis le Pokémon envoyé");
        Assert.Empty(cut.FindAll(".trade-candidate.selected"));

        // Une sauvegarde supprimée : retour aux sauvegardes.
        await Pick(cut, "Pikachu");
        await cut.Click("Valider");
        cut.WaitForText("3. Choisis le Pokémon reçu");
        await Pick(cut, "Stari");
        await using (var db = Db.NewContext())
        {
            db.StoredSaves.Remove(await db.StoredSaves.SingleAsync(s => s.Id == regis));
            await db.SaveChangesAsync();
        }
        await cut.Click("Valider");
        cut.WaitForText("n'existe plus");
        cut.WaitForText("Il te faut deux sauvegardes stockées");
    }

    [Fact]
    public async Task AnEggThatWouldLeaveOnlyEggs_IsGreyed()
    {
        Assert.True((await StoredSaves.AddAsync("sacha", "sacha.sav", TradeServiceTests.Bw2("Sacha", 1, [25]))).Ok);
        var data = TradeServiceTests.Bw2("Regis", 2, [120]);
        Assert.True(SaveUtil.TryGetSaveFile(data, out var regisSave));
        var egg = SaveSessionTests.CreatePk5(133, 1, "Regis", 9);
        egg.IsEgg = true;
        egg.RefreshChecksum();
        regisSave!.SetBoxSlotAtIndex(egg, 0, 0);
        Assert.True((await StoredSaves.AddAsync("sacha", "regis.sav", regisSave.Write().ToArray())).Ok);
        var saves = await StoredSaves.ListAsync("sacha");
        Navigation.NavigateTo($"echange-interne?de={saves[0].Id}&vers={saves[1].Id}");

        var cut = Render<InternalTrade>();
        cut.WaitForText("2. Choisis le Pokémon envoyé");
        await Pick(cut, "Pikachu");
        await cut.Click("Valider");
        cut.WaitForText("3. Choisis le Pokémon reçu");
        var eggButton = cut.FindAll(".trade-candidate").Single(b => b.TextContent.Contains("Œuf"));
        Assert.True(eggButton.HasAttribute("disabled"));
        Assert.Contains("garderait seulement des œufs", eggButton.GetAttribute("title"));
    }
}
