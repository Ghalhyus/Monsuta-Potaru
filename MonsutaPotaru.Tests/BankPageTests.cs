using Bunit;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MonsutaPotaru.Components.Pages;
using MonsutaPotaru.Services;

namespace MonsutaPotaru.Tests;

/// <summary>Page « Transfert » : chargement d'une sauvegarde, dépôts et retraits (clics, sélections, glisser-déposer), validation.</summary>
public sealed class BankPageTests : PageTestContext
{
    private async Task<IRenderedComponent<Bank>> RenderWithSave(byte[]? save = null)
    {
        var cut = Render<Bank>();
        cut.WaitForText("Charge ta sauvegarde");
        await Upload(cut, new FakeBrowserFile("noir2.sav", save ?? SaveSessionTests.CreateBw2Save()));
        cut.WaitForText("Sauvegarde chargée");
        return cut;
    }

    /// <summary>Fichier choisi dans le champ d'envoi de la page.</summary>
    internal static Task Upload<T>(IRenderedComponent<T> cut, IBrowserFile file) where T : class, Microsoft.AspNetCore.Components.IComponent
    {
        var input = cut.FindComponent<InputFile>();
        return cut.InvokeAsync(() => input.Instance.OnChange.InvokeAsync(new InputFileChangeEventArgs([file])));
    }

    private static IReadOnlyList<AngleSharp.Dom.IElement> SaveSlots(IRenderedComponent<Bank> cut) =>
        cut.FindAll("section:first-child .box-grid .slot");

    private static IReadOnlyList<AngleSharp.Dom.IElement> BankSlots(IRenderedComponent<Bank> cut) =>
        cut.FindAll(".bank-zone .box-grid .slot");

    [Fact]
    public async Task UnreadableFile_IsRefused()
    {
        var cut = Render<Bank>();
        await Upload(cut, new FakeBrowserFile("texte.sav", [1, 2, 3]));
        cut.WaitForText("Fichier non reconnu");
        await cut.Find(".alert .btn-close").ClickAsync(new());
        Assert.Empty(cut.FindAll(".alert"));
        await Upload(cut, new FakeBrowserFile("gros.sav", [], fail: true));
        cut.WaitForText("Impossible de lire le fichier");
    }

    [Fact]
    public async Task StoredSave_IsReadOnlyHere()
    {
        var data = SaveSessionTests.CreateBw2Save();
        Assert.True((await StoredSaves.AddAsync("ondine", "noir2.sav", data)).Ok);
        var cut = Render<Bank>();
        await Upload(cut, new FakeBrowserFile("noir2.sav", data));
        cut.WaitForAssertion(() => Assert.Contains("alert-danger", cut.Markup));
    }

    [Fact]
    public async Task Deposit_ThenCommit_ThenWithdraw()
    {
        var cut = await RenderWithSave();

        // Un Pokémon : sa fiche, son dépôt, l'annulation
        await SaveSlots(cut)[0].ClickAsync(new());
        cut.WaitForText("Déposer dans le stockage interne");
        await cut.Click("Déposer dans le stockage interne");
        cut.WaitForText("Modifications en attente");
        await cut.Click("annuler");
        Assert.DoesNotContain("Modifications en attente", cut.Text());

        // Deux Pokémon (Ctrl+clic), déposés puis validés
        await SaveSlots(cut)[0].ClickAsync(new());
        await SaveSlots(cut)[1].ClickAsync(new MouseEventArgs { CtrlKey = true });
        cut.WaitForText("2 Pokémon sélectionnés");
        await cut.Click("Désélectionner");
        await SaveSlots(cut)[0].ClickAsync(new());
        await SaveSlots(cut)[1].ClickAsync(new MouseEventArgs { ShiftKey = true });
        await cut.Click("Déposer les 2");
        cut.WaitForText("2 dépôts ajoutés");
        Assert.Contains("+2 en attente", cut.Text());
        await cut.Click("Changer de fichier");
        cut.WaitForText("Valide ou annule d'abord");
        await cut.Click("Valider et télécharger la sauvegarde");
        cut.WaitForText("Stockage interne mis à jour");
        Assert.Equal(2, (await Services.GetRequiredService<BankService>().ListAsync("sacha")).Count);

        // Retrait d'un Pokémon de la banque : sa fiche, son fichier, puis tout annuler
        await BankSlots(cut)[0].ClickAsync(new());
        cut.WaitForText("Retirer vers la sauvegarde");
        await cut.Click("Télécharger le fichier");
        await cut.Click("Retirer vers la sauvegarde");
        cut.WaitForText("Retrait ajouté");
        Assert.Contains("Retrait :", cut.Text());
        await cut.Click("annuler");
        await BankSlots(cut)[0].ClickAsync(new());
        await BankSlots(cut)[1].ClickAsync(new MouseEventArgs { ShiftKey = true });
        cut.WaitForText("2 Pokémon sélectionnés");
        await cut.Click("Retirer les 2");
        cut.WaitForText("2 retraits ajoutés");
        await cut.Click("Tout annuler");
        cut.WaitForText("Modifications annulées.");

        // Sélection d'une page entière, multiple au toucher, recherche
        await cut.Buttons("Tout sélectionner")[1].ClickAsync(new());
        cut.WaitForText("2 Pokémon sélectionnés");
        await cut.Find("#multiSelect").ChangeAsync(new() { Value = true });
        await BankSlots(cut)[0].ClickAsync(new());
        cut.WaitForText("Retirer vers la sauvegarde");
        await BankSlots(cut)[1].ClickAsync(new());
        await cut.Find(".bank-zone input").InputAsync(new() { Value = "zzz" });
        cut.WaitForText("Le stockage interne est vide");
        await cut.Find(".bank-zone input").InputAsync(new() { Value = "" });
        cut.WaitForAssertion(() => Assert.Equal(2, BankSlots(cut).Count));

        await cut.Click("Changer de fichier");
        cut.WaitForText("Charge ta sauvegarde");
    }

    [Fact]
    public async Task IncompatibleBankPokemon_AreGreyed_AndCannotBeChosen()
    {
        // Pikachu va dans Noir 2 ; Marisson (n° 650, 6e génération) n'y existe pas.
        AddToBank("sacha", 25, 650);
        var cut = Render<Bank>();
        cut.WaitForText("Charge ta sauvegarde");
        Assert.Empty(cut.FindAll(".bank-zone .slot.locked")); // sans sauvegarde, rien n'est grisé

        await Upload(cut, new FakeBrowserFile("noir2.sav", SaveSessionTests.CreateBw2Save()));
        cut.WaitForText("Sauvegarde chargée");
        var locked = Assert.Single(cut.FindAll(".bank-zone .slot.locked"));
        Assert.Contains("Marisson", locked.GetAttribute("title"));
        Assert.Contains("ne peut pas aller dans cette sauvegarde", locked.GetAttribute("title"));
        Assert.Equal("false", locked.GetAttribute("draggable"));

        // Un clic donne la raison sans le sélectionner, même avec Ctrl
        await locked.ClickAsync(new());
        cut.WaitForText("n'existe pas dans cette génération");
        Assert.Empty(cut.FindAll(".bank-zone .slot.selected"));
        await cut.Find(".bank-zone .slot.locked").ClickAsync(new MouseEventArgs { CtrlKey = true });
        Assert.Empty(cut.FindAll(".bank-zone .slot.selected"));

        // « Tout sélectionner » le saute : seul Pikachu est choisi, puis retiré
        await cut.Buttons("Tout sélectionner")[1].ClickAsync(new());
        Assert.Contains("Pikachu", Assert.Single(cut.FindAll(".bank-zone .slot.selected")).GetAttribute("title"));
        await cut.Click("Retirer vers la sauvegarde");
        cut.WaitForText("Retrait ajouté");
    }

    [Fact]
    public async Task DragAndDrop_DepositsAndWithdraws()
    {
        var cut = await RenderWithSave();

        // Boîtes : flèches et liste
        await cut.Find("[aria-label='Boîte suivante']").ClickAsync(new());
        await cut.Find("[aria-label='Boîte précédente']").ClickAsync(new());
        await cut.Find(".box-nav select").ChangeAsync(new() { Value = "0" });

        // Toute la boîte glissée vers le stockage interne
        await cut.Buttons("Tout sélectionner")[0].ClickAsync(new());
        await SaveSlots(cut)[0].DragStartAsync(new());
        Assert.Contains("Lâche ici", cut.Text());
        await cut.Find(".bank-zone").DropAsync(new());
        cut.WaitForText("2 dépôts ajoutés");
        await cut.Click("Valider et télécharger la sauvegarde");
        cut.WaitForText("Stockage interne mis à jour");

        // Un Pokémon de la banque glissé sur une case vide (la flèche change de boîte pendant le glissement)
        await BankSlots(cut)[0].DragStartAsync(new());
        await cut.Find("[aria-label='Boîte suivante']").DragEnterAsync(new());
        await cut.Find("[aria-label='Boîte précédente']").DragEnterAsync(new());
        await SaveSlots(cut)[5].DropAsync(new());
        cut.WaitForText("Retrait ajouté");
        // Un autre lâché sur la boîte : à partir de la première case libre
        await BankSlots(cut)[1].DragStartAsync(new());
        await BankSlots(cut)[1].DragEndAsync(new());
        await BankSlots(cut)[1].DragStartAsync(new());
        await cut.Find("section:first-child .box-grid").DropAsync(new());
        cut.WaitForText("Retrait ajouté");
        // Remettre en banque un Pokémon tout juste retiré annule le retrait
        await SaveSlots(cut)[0].ClickAsync(new());
        await cut.Click("Déposer dans le stockage interne");
        cut.WaitForText("1 retrait annulé");
    }

    [Fact]
    public async Task FullCollection_RefusesDeposits_UntilSlotsAreBought()
    {
        var stored = await StoreBw2("sacha", "Ludo", money: 900_000);
        UpdateUser("sacha", u => u.CollectionLimit = 2);
        var inventory = Services.GetRequiredService<InventoryService>();
        Assert.True((await inventory.WithdrawFromSaveAsync("sacha", stored, 900_000)).Ok);
        var cut = await RenderWithSave();
        cut.WaitForText("collection pleine");

        await SaveSlots(cut)[0].ClickAsync(new());
        await cut.Click("Déposer dans le stockage interne");
        cut.WaitForAssertion(() => Assert.Contains("alert-danger", cut.Markup));

        await cut.Click("Agrandir");
        cut.WaitForText("1/2");
        Assert.Contains("Indique un nombre de lots", cut.Text());
        await cut.Find("#purchase-lots").InputAsync(new() { Value = "200000" });
        Assert.Contains("Trop de lots", cut.Text());
        await cut.Find("#purchase-lots").InputAsync(new() { Value = "1000" });
        Assert.Contains("il manque", cut.Text());
        await cut.Find("#purchase-lots").InputAsync(new() { Value = "1" });
        await cut.Click("Suivant");
        cut.WaitForText("2/2");
        await cut.Click("Retour");
        await cut.Click("Annuler");
        await cut.Click("Agrandir");
        await cut.Find("#purchase-lots").InputAsync(new() { Value = "1" });
        await cut.Click("Suivant");
        await cut.Click("Confirmer l'achat");
        cut.WaitForAssertion(() => Assert.DoesNotContain("collection pleine", cut.Text()));
        await cut.Click("Agrandir");
        await cut.Find(".dialog-backdrop").ClickAsync(new());
    }

    [Fact]
    public async Task MinecraftWorld_TeamAndPc_DepositThenWithdraw()
    {
        AddToBank("sacha", 25); // Pikachu d'un jeu : grisé dans un monde Minecraft
        var cut = Render<Bank>();
        cut.WaitForText("Choisir le dossier du monde");
        await Upload(cut, new FakeBrowserFile("pokemon.zip", CobblemonTests.World(CobblemonTests.Mon("eevee", 1))));
        cut.WaitForText("Sauvegarde chargée : monde Minecraft All the Mons.");
        Assert.Contains("Monde Minecraft", cut.Text());
        Assert.Equal(6, cut.Find(".box-grid").QuerySelectorAll(".slot").Length); // l'équipe
        Assert.Contains("Équipe", cut.Find(".box-nav select").TextContent);
        var locked = Assert.Single(cut.FindAll(".bank-zone .slot.locked"));
        await locked.ClickAsync(new());
        cut.WaitForText(SaveSession.GameToMinecraftRefusal);

        // Fiche d'un Pokémon de l'équipe : pas de légalité (PKHeX n'en juge pas), le pseudo pour dresseur
        await SaveSlots(cut)[0].ClickAsync(new());
        cut.WaitForText("Déposer dans le stockage interne");
        Assert.Contains("Feunnec · N.10 · Cobblemon", cut.Text());
        Assert.DoesNotContain("Légal", cut.Text());
        Assert.Contains("sacha", cut.Find(".pokemon-card").TextContent);

        // Maj+clic de l'équipe (6 cases) à la première boîte (30 cases)
        await cut.Find(".box-nav select").ChangeAsync(new() { Value = "1" });
        Assert.Equal(30, cut.Find(".box-grid").QuerySelectorAll(".slot").Length);
        await SaveSlots(cut)[0].ClickAsync(new MouseEventArgs { ShiftKey = true });
        cut.WaitForText("2 Pokémon sélectionnés");
        Assert.Contains("Équipe, case 1", cut.Text());
        Assert.Contains("Boîte 1, case 1", cut.Text());
        await cut.Click("Déposer les 2");
        cut.WaitForText("2 dépôts ajoutés");
        Assert.Contains("Dépôt : Feunnec N.10", cut.Text());
        Assert.Contains("décompresse le zip téléchargé dans le dossier de ton monde", cut.Text());
        await cut.Click("Valider et télécharger la sauvegarde");
        cut.WaitForText("décompresse maintenant le zip");
        Assert.Equal(3, (await Services.GetRequiredService<BankService>().ListAsync("sacha")).Count);

        // Retour du Feunnec : dans le PC, sans fichier à télécharger
        var feunnec = BankSlots(cut).First(s => s.GetAttribute("title")!.Contains("Feunnec"));
        await feunnec.ClickAsync(new());
        cut.WaitForText("Retirer vers la sauvegarde");
        Assert.False(cut.HasButton("Télécharger le fichier"));
        await cut.Click("Retirer vers la sauvegarde");
        cut.WaitForText("Retrait ajouté aux modifications en attente (Boîte 1, case 1).");
        Assert.Contains("Retrait : Feunnec → Boîte 1", cut.Text());

        // Glissé de la banque dans l'équipe
        await cut.Find(".box-nav select").ChangeAsync(new() { Value = "0" });
        var evoli = BankSlots(cut).First(s => s.GetAttribute("title")!.Contains("Évoli"));
        await evoli.TriggerEventAsync("ondragstart", new DragEventArgs());
        await SaveSlots(cut)[2].TriggerEventAsync("ondrop", new DragEventArgs());
        cut.WaitForText("Équipe, case 3");
    }

    /// <summary>Fichier choisi dans le navigateur, lu depuis la mémoire (ou qui échoue à la lecture).</summary>
    internal sealed class FakeBrowserFile(string name, byte[] data, bool fail = false) : IBrowserFile
    {
        public string Name => name;
        public DateTimeOffset LastModified => DateTimeOffset.Now;
        public long Size => data.Length;
        public string ContentType => "application/octet-stream";

        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) =>
            fail ? throw new IOException("lecture interrompue") : new MemoryStream(data);
    }
}
