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
    private const int PokeBall = 4, Potion = 17, RareCandy = 50, Nugget = 92, OranBerry = 155, Tm01 = 328, HoneClaws = 468; // CT01 de Noir 2 : Aiguisage

    /// <summary>Objets que <see cref="StoreWithItems"/> met dans le sac (clés du site : la CT est rangée par capacité).</summary>
    private static readonly Dictionary<int, int> SampleItems = new()
    {
        [Potion] = 5, [PokeBall] = 12, [RareCandy] = 3, [Nugget] = 2, [OranBerry] = 4, [SaveBag.MachineKey(HoneClaws)] = 1,
    };

    private Task<int> StoreWithItems(string owner = "sacha", string ot = "Ludo") =>
        StoreBw2(owner, ot, 100_000, (Potion, 5), (PokeBall, 12), (RareCandy, 3), (Nugget, 2), (OranBerry, 4), (Tm01, 1));

    private InventoryService InventoryService => Services.GetRequiredService<InventoryService>();

    /// <summary>Inventaire interne de sacha : objet → quantité.</summary>
    private async Task<Dictionary<int, int>> Stock() =>
        (await InventoryService.GetItemsAsync("sacha")).ToDictionary(i => i.ItemId, i => i.Count);

    /// <summary>Sac de cette sauvegarde stockée de sacha : objet → quantité.</summary>
    private async Task<Dictionary<int, int>> SaveItems(int saveId) =>
        (await InventoryService.ListItemSourcesAsync("sacha")).Single(s => s.SaveId == saveId).Items.ToDictionary(i => i.ItemId, i => i.Count);

    /// <summary>Noms des lignes du tableau affiché, dans l'ordre.</summary>
    private static List<string> RowNames(IRenderedComponent<Inventory> cut) =>
        cut.FindAll("tbody tr td:first-child").Select(td => PageTestExtensions.Normalize(td.TextContent)).ToList();

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

        // « Tout » (par défaut) : les objets des sauvegardes, chacun dans son onglet, cliquables pour voir où ils sont
        Task Tab(string title) => cut.InvokeAsync(() => cut.FindAll("[role=tab]").First(t => t.TextContent.Contains(title)).ClickAsync(new()));
        await Tab("CT");
        Assert.Contains("Aiguisage", Assert.Single(RowNames(cut))); // CT01 de Noir 2
        foreach (var (tab, names) in new (string, string[])[]
                 {
                     ("Pokéballs", ["Poké Ball"]), ("Baies", ["Baie Oran"]), ("Objets", ["Pépite"]), ("Médicaments", ["Potion", "Super Bonbon"]),
                 })
        {
            await Tab(tab);
            Assert.Equal(names, RowNames(cut).Order(StringComparer.Ordinal));
        }
        Assert.Contains("Lecture seule : clique sur un objet", cut.Text());
        await cut.FindAll("table button.btn-link")[0].ClickAsync(new());
        cut.WaitForText("Total");
        Assert.Contains("Ludo", cut.Find(".journal-panel").TextContent);
        await cut.Find(".journal-panel .btn-close").ClickAsync(new());

        // Recherche sans accents, puis rien trouvé
        await cut.Find("input.inventory-search").InputAsync(new() { Value = "SUPER bonbon" });
        Assert.Equal(["Super Bonbon"], RowNames(cut));
        await cut.Find("input.inventory-search").InputAsync(new() { Value = "zzz" });
        cut.WaitForText("Rien ne correspond à « zzz »");
        await cut.Find("input.inventory-search").InputAsync(new() { Value = "" });

        // Tris des médicaments (5 Potion, 3 Super Bonbon) : nom, quantité, par défaut, chacun dans les deux sens
        Task SortBy(string sort) => cut.Find(".inventory-sort select").ChangeAsync(new() { Value = sort });
        Task Flip() => cut.Find(".inventory-sort button").ClickAsync(new());
        foreach (var (sort, ascending) in new[] { ("nom", new[] { "Potion", "Super Bonbon" }), ("quantite", ["Super Bonbon", "Potion"]) })
        {
            await SortBy(sort);
            Assert.Equal(ascending, RowNames(cut));
            await Flip();
            Assert.Equal(ascending.AsEnumerable().Reverse(), RowNames(cut));
            await Flip();
        }
        await SortBy("defaut");
        var byDefault = RowNames(cut);
        await Flip();
        Assert.Equal(byDefault.AsEnumerable().Reverse(), RowNames(cut));

        // Grille : colonnes au curseur, puis automatiques ; détail depuis une tuile
        await cut.Click("Grille");
        Assert.NotEmpty(cut.FindAll(".item-tile"));
        await cut.Find(".inventory-columns input").InputAsync(new() { Value = "4" });
        Assert.Contains("--item-columns: 4", cut.Markup);
        await cut.FindAll("button").First(b => b.TextContent.Trim() == "Auto").ClickAsync(new());
        Assert.DoesNotContain("--item-columns", cut.Markup); // colonnes selon la largeur de la fenêtre
        Assert.Empty(cut.FindAll(".item-grid-fixed"));
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
    public async Task Preferences_ViewFromTheBrowser_SortFromTheAccount()
    {
        await StoreWithItems();
        JSInterop.Setup<string?>("localStorage.getItem", "monsuta-potaru.inventaire.affichage").SetResult("grille");
        JSInterop.Setup<string?>("localStorage.getItem", "monsuta-potaru.inventaire.colonnes").SetResult("3");
        SetSorts("sacha", new SortPreferences { Inventory = new Dictionary<ItemCategory, ItemSort> { [ItemCategory.Medicine] = new(ItemSortBy.Quantity, true) } });
        Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().NavigateTo("inventaire?onglet=medicaments&source=tout");
        var cut = Render<Inventory>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".item-tile")));
        Assert.Contains("--item-columns: 3", cut.Markup);
        Assert.Contains("↓ Décroissant", cut.Text());
        Assert.Equal(["Potion", "Super Bonbon"], cut.FindAll(".item-tile").Select(t => t.GetAttribute("title"))); // 5 puis 3

        // Colonnes automatiques : le navigateur dit combien il en tient (watchGridColumns), affiché à côté du curseur
        await cut.FindAll("button").First(b => b.TextContent.Trim() == "Auto").ClickAsync(new());
        await cut.Instance.OnGridColumnsChanged(5);
        Assert.Equal("Auto (5)", cut.Find(".inventory-columns .fw-semibold").TextContent);
        Assert.Equal("5", cut.Find(".inventory-columns input").GetAttribute("value"));
    }

    [Fact]
    public async Task Preferences_Unreadable_AreIgnored()
    {
        UpdateUser("sacha", u => u.SortPreferences = "pas du json"); // tris d'origine
        JSInterop.Setup<string?>("localStorage.getItem", "monsuta-potaru.inventaire.affichage").SetException(new Microsoft.JSInterop.JSException("bloqué"));
        await StoreWithItems();
        var cut = Render<Inventory>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("tbody tr")));
        Assert.Equal("true", cut.Button("Liste").GetAttribute("aria-pressed")); // affichage d'origine : la liste
        Assert.Empty(cut.FindAll(".item-tile"));
        Assert.Equal("defaut", cut.Find(".inventory-sort select").GetAttribute("value")); // tri d'origine
        Assert.Equal("↑ Croissant", cut.Find(".inventory-sort button").TextContent);
        await DisposeComponentsAsync();
    }

    [Fact]
    public async Task Sort_FromTheAddress_ElseTheAccount_KeptPerTabForTheVisit()
    {
        await StoreWithItems();
        SetSorts("sacha", new SortPreferences { Inventory = new Dictionary<ItemCategory, ItemSort> { [ItemCategory.Berries] = new(ItemSortBy.Name) } });
        var navigation = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
        navigation.NavigateTo("inventaire?onglet=objets&tri=quantite&ordre=desc");
        var cut = Render<Inventory>();
        cut.WaitForText("Stockage");
        string Order() => cut.Find(".inventory-sort button").TextContent;
        Assert.Equal("↓ Décroissant", Order()); // l'adresse l'emporte

        // Le tri du compte (ici celui d'origine) n'est pas écrit dans l'adresse ; un autre, si
        await cut.Find(".inventory-sort select").ChangeAsync(new() { Value = "defaut" });
        Assert.DoesNotContain("tri=", navigation.Uri);
        Assert.Equal("↑ Croissant", Order());
        await cut.Find(".inventory-sort select").ChangeAsync(new() { Value = "nom" });
        Assert.Contains("tri=nom", navigation.Uri);

        // Baies : le tri du compte (par nom) ; le tri d'origine choisi s'écrit alors en entier
        await cut.FindAll("[role=tab]").First(t => t.TextContent.Contains("Baies")).ClickAsync(new());
        Assert.Contains("onglet=baies", navigation.Uri);
        Assert.DoesNotContain("tri=", navigation.Uri);
        Assert.Equal("↑ A → Z", Order());
        await cut.Find(".inventory-sort select").ChangeAsync(new() { Value = "defaut" });
        Assert.Contains("tri=defaut", navigation.Uri);

        // Retour aux objets : le tri choisi pendant la visite revient, dans l'adresse
        await cut.FindAll("[role=tab]").First(t => t.TextContent.Contains("Objets")).ClickAsync(new());
        Assert.Contains("tri=nom", navigation.Uri);
        Assert.Equal("↑ A → Z", Order());
        await cut.Find(".inventory-sort button").ClickAsync(new());
        Assert.Contains("ordre=desc", navigation.Uri);
    }

    [Fact]
    public async Task Visitor_SeesTheInventory_WithHisOwnSorts()
    {
        await StoreWithItems("ondine", "Ondine");
        SetSorts("sacha", new SortPreferences { Inventory = new Dictionary<ItemCategory, ItemSort> { [ItemCategory.Items] = new(ItemSortBy.Name, true) } });
        SetSorts("ondine", new SortPreferences { Inventory = new Dictionary<ItemCategory, ItemSort> { [ItemCategory.Items] = new(ItemSortBy.Quantity) } });
        var cut = Render<Inventory>(p => p.Add(c => c.Pseudo, "ondine"));
        cut.WaitForAssertion(() => Assert.Equal("↓ Z → A", cut.Find(".inventory-sort button").TextContent)); // ceux de sacha
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
        Assert.StartsWith("Retiré de « Jeu de génération 5 — Ludo »", PageTestExtensions.Normalize(cut.Find(".alert-success").TextContent));
        Assert.Equal(SampleItems, await Stock()); // « Tout » dans chaque onglet : tout le sac
        Assert.Empty(await SaveItems(saveId));

        // Dépôt : « Tout » dans le premier onglet du dialogue, qui retourne dans le sac
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
        Assert.StartsWith("Déposé dans « Jeu de génération 5 — Ludo »", PageTestExtensions.Normalize(cut.Find(".alert-success").TextContent));
        var (stock, inSave) = (await Stock(), await SaveItems(saveId));
        Assert.NotEmpty(inSave);
        Assert.All(SampleItems, e => Assert.Equal(e.Value, stock.GetValueOrDefault(e.Key) + inSave.GetValueOrDefault(e.Key))); // rien de perdu ni de créé
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
        // Vraie partie, à son début : PC vide. On y met 2 Potion (13 en 3e génération), et 3 Poké Ball dans le sac (4 : même numéro).
        var data = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Sauvegardes", "rouge-feu.sav"));
        Assert.True(PKHeX.Core.SaveUtil.TryGetSaveFile(data, out var sav, "rouge-feu.sav"));
        SaveSessionTests.GiveItems(sav!, (PokeBall, 3));
        var bag = sav!.Inventory;
        Assert.True(bag.GetPouch(PKHeX.Core.InventoryType.PCItems).GiveItem(bag, 13, 2) > 0);
        bag.CopyTo(sav);
        Assert.True((await StoredSaves.AddAsync("sacha", "rouge-feu.sav", sav.Write().ToArray())).Ok);
        var source = (await InventoryService.ListItemSourcesAsync("sacha")).Single();
        var pcItem = Assert.Single(source.ItemsIn(BagPlace.Pc));
        Assert.Equal((Potion, 2), (pcItem.ItemId, pcItem.Count));
        var bagItem = source.ItemsIn(BagPlace.Bag).Single(i => i.ItemId == PokeBall);

        var cut = Render<ItemTransferDialog>(p => p.Add(d => d.UserId, "sacha").Add(d => d.Stock, []).Add(d => d.Deposit, false));
        cut.WaitForText("sac");
        await cut.Find(".list-group-item").ClickAsync(new());
        cut.WaitForText("2/3");
        await ChooseAll(cut, "PC", pcItem.Category, pcItem.Name);
        await ChooseAll(cut, "Sac", bagItem.Category, bagItem.Name);
        await cut.Click("Suivant");
        cut.WaitForText("3/3");

        // Un titre par endroit, et chaque objet avec la quantité de son endroit : PC n → 0, sac n → 0
        Assert.Equal(["PC", "Sac"], cut.FindAll(".dialog-panel .small.fw-semibold").Select(e => e.TextContent.Trim()).Order());
        var lines = cut.FindAll(".dialog-panel ul.small li").Select(li => PageTestExtensions.Normalize(li.TextContent)).ToList();
        Assert.Contains(lines, l => l.StartsWith($"{pcItem.Count} × {pcItem.Name}", StringComparison.Ordinal) && l.Contains($"PC : {pcItem.Count} → 0"));
        Assert.Contains(lines, l => l.StartsWith($"{bagItem.Count} × {bagItem.Name}", StringComparison.Ordinal) && l.Contains($"sac : {bagItem.Count} → 0"));
    }

    /// <summary>Étape 2 du dialogue : endroit (« PC » ou « Sac »), onglet de la catégorie, puis « Tout » sur la ligne de l'objet.</summary>
    private static async Task ChooseAll(IRenderedComponent<ItemTransferDialog> cut, string place, ItemCategory category, string item)
    {
        await cut.InvokeAsync(() => cut.FindAll(".nav-pills button").Single(b => b.TextContent.Trim().StartsWith(place, StringComparison.Ordinal)).ClickAsync(new()));
        var tabTitle = ItemCategories.All.Single(c => c.Category == category).Title;
        await cut.InvokeAsync(() => cut.FindAll(".nav-tabs button").Single(t => t.TextContent.Trim().StartsWith(tabTitle, StringComparison.Ordinal)).ClickAsync(new()));
        await cut.InvokeAsync(() => cut.FindAll("tbody tr").Single(r => r.QuerySelector("td")!.TextContent.Trim() == item)
            .QuerySelector("button")!.ClickAsync(new()));
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
        // Profil privé ou joueur inconnu : même réponse, qui ne dit pas si le joueur existe.
        UpdateUser("ondine", u => u.Audience = ProfileAudience.Nobody);
        var cut = Render<Inventory>(p => p.Add(i => i.Pseudo, "ondine"));
        cut.WaitForText("Joueur introuvable");
        Assert.Contains(PlayerService.HiddenMessage, cut.Text());
        Assert.DoesNotContain("Porte-monnaie", cut.Text());
        var unknown = Render<Inventory>(p => p.Add(i => i.Pseudo, "personne"));
        unknown.WaitForText("Joueur introuvable");
        Assert.Equal(cut.Text(), unknown.Text());
    }
}
