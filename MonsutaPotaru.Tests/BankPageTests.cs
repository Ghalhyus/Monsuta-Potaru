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
