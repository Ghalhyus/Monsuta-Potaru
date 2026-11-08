using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MonsutaPotaru.Components.Pages;
using MonsutaPotaru.Components.Shared;
using MonsutaPotaru.Data;
using MonsutaPotaru.Services;

namespace MonsutaPotaru.Tests;

/// <summary>Page « Inventaire » : porte-monnaie, retrait d'argent, stockage des objets (onglets, sources, tris, grille), journal.</summary>
public sealed class InventoryPageTests : PageTestContext
{
    private const int PokeBall = 4, Potion = 17, RareCandy = 50, Nugget = 92, OranBerry = 155, Tm01 = 328;

    private Task<int> StoreWithItems(string owner = "sacha", string ot = "Ludo") =>
        StoreBw2(owner, ot, 100_000, (Potion, 5), (PokeBall, 12), (RareCandy, 3), (Nugget, 2), (OranBerry, 4), (Tm01, 1));

    [Fact]
    public async Task Withdraw_MovesMoneyToTheWallet_InThreeSteps()
    {
        await StoreWithItems();
        var cut = Render<Inventory>();
        cut.WaitForText("Porte-monnaie");
        Assert.Contains("0 ₽", cut.Text());

        await cut.Click("Retirer de l'argent d'une sauvegarde");
        cut.WaitForText("1/3");
        await cut.Find(".dialog-panel .list-group-item").ClickAsync(new());
        cut.WaitForText("2/3");
        Assert.Contains("Indique un montant.", cut.Text());
        await cut.Find("#withdraw-amount").InputAsync(new() { Value = "0" });
        Assert.Contains("supérieur à 0", cut.Text());
        await cut.Find("#withdraw-amount").InputAsync(new() { Value = "999999" });
        Assert.Contains("ne contient que", cut.Text());
        await cut.Click("Retour");
        cut.WaitForText("1/3");
        await cut.Find(".dialog-panel .list-group-item").ClickAsync(new());
        await cut.Find("#withdraw-amount").InputAsync(new() { Value = "30000" });
        await cut.Click("Suivant");
        cut.WaitForText("3/3");
        await cut.Click("Retour");
        cut.WaitForText("2/3");
        await cut.Click("Suivant");
        await cut.Click("Confirmer le retrait");
        cut.WaitForText("30 000 ₽");
        Assert.Equal(30_000, await Services.GetRequiredService<InventoryService>().GetBalanceAsync("sacha"));

        // Journal de l'opération, puis fermeture
        await cut.Click("Journal");
        cut.WaitForText("Journal des opérations");
        Assert.Contains("Retrait d'une sauvegarde", cut.Text());
        await cut.Click("Fermer le journal");
        Assert.DoesNotContain("Journal des opérations", cut.Text());
    }

    [Fact]
    public async Task Withdraw_WithoutStoredSave_SaysSo_AndCancels()
    {
        var cut = Render<Inventory>();
        await cut.Click("Retirer de l'argent d'une sauvegarde");
        cut.WaitForText("Aucune sauvegarde stockée");
        await cut.Click("Annuler");
        Assert.DoesNotContain("1/3", cut.Text());

        await cut.Click("Journal");
        cut.WaitForText("Aucune opération pour l'instant.");
        await cut.Find(".journal-panel .btn-close").ClickAsync(new());
        Assert.DoesNotContain("Journal des opérations", cut.Text());
        await cut.Click("Retirer de l'argent d'une sauvegarde");
        await cut.Find(".dialog-backdrop").ClickAsync(new());
        Assert.DoesNotContain("1/3", cut.Text());
    }

    [Fact]
    public async Task Storage_ShowsEachTab_EachSource_SearchAndSorts()
    {
        var saveId = await StoreWithItems();
        await StoreBw2("sacha", "Rival");
        var cut = Render<Inventory>();
        cut.WaitForAssertion(() => Assert.Contains(cut.FindAll("select.inventory-source option"), o => o.GetAttribute("value") == $"sauvegarde-{saveId}"));

        // « Tout » (par défaut) : les objets des sauvegardes, cliquables pour voir où ils sont
        foreach (var tab in new[] { "Pokéballs", "Baies", "CT", "Objets", "Médicaments" })
            await cut.FindAll("[role=tab]").First(t => t.TextContent.Contains(tab)).ClickAsync(new());
        Assert.Contains("Lecture seule : clique sur un objet", cut.Text());
        await cut.FindAll("table button.btn-link").First().ClickAsync(new());
        cut.WaitForText("Total");
        Assert.Contains("Ludo", cut.Find(".journal-panel").TextContent);
        await cut.Find(".journal-panel .btn-close").ClickAsync(new());

        // Recherche sans accents, puis rien trouvé
        await cut.Find("input.inventory-search").InputAsync(new() { Value = "SUPER bonbon" });
        Assert.Single(cut.FindAll("tbody tr"));
        await cut.Find("input.inventory-search").InputAsync(new() { Value = "zzz" });
        cut.WaitForText("Rien ne correspond à « zzz »");
        await cut.Find("input.inventory-search").InputAsync(new() { Value = "" });

        // Tris : nom, quantité, par défaut, chacun dans les deux sens
        foreach (var sort in new[] { "nom", "quantite", "defaut" })
        {
            await cut.Find(".inventory-sort select").ChangeAsync(new() { Value = sort });
            await cut.Find(".inventory-sort button").ClickAsync(new());
            await cut.Find(".inventory-sort button").ClickAsync(new());
        }

        // Grille : colonnes au curseur, puis automatiques ; détail depuis une tuile
        await cut.Click("Grille");
        Assert.NotEmpty(cut.FindAll(".item-tile"));
        await cut.Find(".inventory-columns input").InputAsync(new() { Value = "4" });
        Assert.Contains("--item-columns: 4", cut.Markup);
        await cut.FindAll("button").First(b => b.TextContent.Trim() == "Auto").ClickAsync(new());
        await cut.Find(".item-tile-button").ClickAsync(new());
        cut.WaitForText("Total");
        await cut.Click("Liste");

        // Sac d'une seule sauvegarde (lecture seule), puis interne (vide)
        await cut.Find("select.inventory-source").ChangeAsync(new() { Value = $"sauvegarde-{saveId}" });
        cut.WaitForText("Lecture seule : copie de cette sauvegarde");
        await cut.Click("Grille");
        Assert.NotEmpty(cut.FindAll("div.item-tile"));
        await cut.Click("Liste");
        await cut.Find("select.inventory-source").ChangeAsync(new() { Value = "interne" });
        cut.WaitForText("Aucun");
        await cut.Find("select.inventory-source").ChangeAsync(new() { Value = "tout" });
    }

    [Fact]
    public async Task Preferences_ReadFromTheBrowser_AreApplied()
    {
        await StoreWithItems();
        JSInterop.Setup<string?>("localStorage.getItem", "monsuta-potaru.inventaire.affichage").SetResult("grille");
        JSInterop.Setup<string?>("localStorage.getItem", "monsuta-potaru.inventaire.colonnes").SetResult("3");
        JSInterop.Setup<string?>("localStorage.getItem", "monsuta-potaru.inventaire.tris")
            .SetResult("""{"medicaments":"quantite:desc","baies":"nom","inconnu":"nom"}""");
        Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().NavigateTo("inventaire?onglet=medicaments&source=tout");
        var cut = Render<Inventory>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".item-tile")));
        Assert.Contains("--item-columns: 3", cut.Markup);
        Assert.Contains("↓ Décroissant", cut.Text());
        await cut.Instance.OnGridColumnsChanged(5);
    }

    [Fact]
    public async Task Preferences_Unreadable_AreIgnored()
    {
        JSInterop.Setup<string?>("localStorage.getItem", "monsuta-potaru.inventaire.tris").SetResult("pas du json");
        JSInterop.Setup<string?>("localStorage.getItem", "monsuta-potaru.inventaire.affichage").SetException(new Microsoft.JSInterop.JSException("bloqué"));
        var cut = Render<Inventory>();
        cut.WaitForText("Porte-monnaie");
        Assert.Contains("Liste", cut.Text());
        await DisposeComponentsAsync();
    }

    [Fact]
    public async Task Items_WithdrawFromASave_ThenDepositThemBack()
    {
        var saveId = await StoreWithItems();
        var cut = Render<Inventory>();
        cut.WaitForText("Stockage");

        // Retrait : sauvegarde → quantités (onglets, recherche, erreurs) → confirmation
        await cut.Click("Retirer d'une sauvegarde");
        cut.WaitForText("Retirer des objets");
        await cut.Find(".dialog-panel .list-group-item").ClickAsync(new());
        cut.WaitForText("2/3");
        Assert.True(cut.Button("Suivant").HasAttribute("disabled"));
        var potion = cut.Find("input.item-quantity");
        await potion.InputAsync(new() { Value = "abc" });
        cut.WaitForText("Nombre entier attendu.");
        await cut.Find("input.item-quantity").InputAsync(new() { Value = "-1" });
        cut.WaitForText("Pas de quantité négative.");
        await cut.Find("input.item-quantity").InputAsync(new() { Value = "999" });
        cut.WaitForText("au maximum.");
        await cut.Find("input.item-quantity").InputAsync(new() { Value = "" });
        await cut.ClickAll("Tout");
        foreach (var tab in new[] { "Pokéballs", "Médicaments", "Baies", "CT" })
        {
            await cut.FindAll(".dialog-panel [role=tab]").First(t => t.TextContent.Contains(tab)).ClickAsync(new());
            await cut.ClickAll("Tout");
        }
        await cut.Find(".dialog-panel input[type=search]").InputAsync(new() { Value = "zzz" });
        cut.WaitForText("Rien ne correspond");
        await cut.Find(".dialog-panel input[type=search]").InputAsync(new() { Value = "" });
        await cut.Click("Suivant");
        cut.WaitForText("3/3");
        await cut.Click("Retour");
        await cut.Click("Suivant");
        await cut.Click("Confirmer le retrait");
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".dialog-panel")));
        Assert.Contains("alert-success", cut.Markup);
        var stock = await Services.GetRequiredService<InventoryService>().GetItemsAsync("sacha");
        Assert.Contains(stock, s => s.ItemId == RareCandy && s.Count == 3);

        // Dépôt : tout l'inventaire retourne dans le sac
        await cut.Find("select.inventory-source").ChangeAsync(new() { Value = "interne" });
        await cut.Click("Déposer dans une sauvegarde");
        cut.WaitForText("Déposer des objets");
        await cut.Find(".dialog-panel .list-group-item").ClickAsync(new());
        cut.WaitForText("2/3");
        await cut.ClickAll("Tout");
        await cut.Click("Suivant");
        cut.WaitForText("ton jeu n'a pas encore ces objets");
        await cut.Click("Confirmer le dépôt");
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".dialog-panel")));
        Assert.Contains(saveId, (await StoredSaves.ListAsync("sacha")).Select(s => s.Id));
    }

    [Fact]
    public async Task Items_Dialog_CanBeCancelled()
    {
        var cut = Render<Inventory>();
        await cut.Click("Retirer d'une sauvegarde");
        cut.WaitForText("Aucune sauvegarde stockée");
        await cut.Click("Annuler");
        Assert.Empty(cut.FindAll(".dialog-panel"));
        await cut.Click("Retirer d'une sauvegarde");
        await cut.Find(".dialog-backdrop").ClickAsync(new());
        Assert.Empty(cut.FindAll(".dialog-panel"));
    }

    [Fact]
    public async Task ItemDialog_GenerationThreeSave_HasABagAndAPc()
    {
        var data = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Sauvegardes", "rouge-feu.sav"));
        Assert.True((await StoredSaves.AddAsync("sacha", "rouge-feu.sav", data)).Ok);
        var cut = Render<ItemTransferDialog>(p => p.Add(d => d.UserId, "sacha").Add(d => d.Stock, []).Add(d => d.Deposit, false));
        cut.WaitForText("sac");
        await cut.Find(".list-group-item").ClickAsync(new());
        cut.WaitForText("2/3");
        await cut.FindAll(".nav-pills button").First(b => b.TextContent.Contains("PC")).ClickAsync(new());
        if (cut.FindAll("input.item-quantity").Count > 0)
            await cut.Click("Tout");
        await cut.FindAll(".nav-pills button").First(b => b.TextContent.Contains("Sac")).ClickAsync(new());
        await cut.Click("Tout");
        await cut.Click("Suivant");
        cut.WaitForText("3/3");
        Assert.Contains("Sac", cut.Text());
    }

    [Fact]
    public async Task Debug_AddsMoney_ForAnAdministrator()
    {
        MakeAdmin("sacha");
        Assert.True((await Services.GetRequiredService<DebugService>().SetActiveAsync("sacha", true)).Ok);
        var cut = Render<Inventory>();
        cut.WaitForText("Mode debug");
        await cut.Click("+ 10 000 ₽");
        cut.WaitForText("10 000 ₽");
        Assert.Equal(10_000, await Services.GetRequiredService<InventoryService>().GetBalanceAsync("sacha"));
    }

    [Fact]
    public async Task Visitor_SeesAPublicInventory_ReadOnly()
    {
        await StoreWithItems("ondine", "Ondine");
        var cut = Render<Inventory>(p => p.Add(i => i.Pseudo, "ondine"));
        cut.WaitForText("Inventaire");
        Assert.False(cut.HasButton("Retirer de l'argent"));
        Assert.False(cut.HasButton("Journal"));
        cut.WaitForAssertion(() => Assert.True(cut.FindAll("select.inventory-source option").Count > 2));
    }

    [Fact]
    public void Visitor_HiddenPlayer_ShowsNothing()
    {
        UpdateUser("ondine", u => u.Audience = ProfileAudience.Nobody);
        var cut = Render<Inventory>(p => p.Add(i => i.Pseudo, "ondine"));
        cut.WaitForAssertion(() => Assert.DoesNotContain("Porte-monnaie", cut.Text()));
        var unknown = Render<Inventory>(p => p.Add(i => i.Pseudo, "personne"));
        unknown.WaitForAssertion(() => Assert.DoesNotContain("Porte-monnaie", unknown.Text()));
    }
}
