using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MonsutaPotaru.Components.Pages;
using MonsutaPotaru.Services;

namespace MonsutaPotaru.Tests;

/// <summary>
/// Page « Transfert d'inventaire » : choix des deux sauvegardes, compteurs, envoi par les flèches ou par le glisser-déposer
/// (simulé : Drop), objets grisés, annulation, validation.
/// </summary>
public sealed class InventoryTransferPageTests : PageTestContext
{
    private const int PokeBall = 4, Potion = 17, HealthFeather = 565; // Plume Santé : inconnue de Platine

    private async Task<int> StorePlatinum()
    {
        Assert.True((await StoredSaves.AddAsync("sacha", "platine.sav", SaveSessionTests.CreatePlatinumSave())).Ok);
        return (await StoredSaves.ListAsync("sacha")).Single(s => s.FileName == "platine.sav").Id;
    }

    private static IElement Column(IRenderedComponent<InventoryTransfer> cut, string side) => cut.Find($".transfer-column-{side}");

    private static IElement Row(IRenderedComponent<InventoryTransfer> cut, string side, int item) =>
        cut.Find($".transfer-column-{side} .transfer-item[data-item=\"{item}\"]");

    private static Task Tab(IRenderedComponent<InventoryTransfer> cut, string side, string title) =>
        cut.FindAll($".transfer-column-{side} .nav-tabs button").First(b => b.TextContent.Contains(title)).ClickAsync(new());

    private static string Counter(IRenderedComponent<InventoryTransfer> cut, string side, int item) =>
        Row(cut, side, item).QuerySelector("input[type=number]")!.GetAttribute("value")!;

    private static string Text(IElement element) => PageTestExtensions.Normalize(element.TextContent);

    [Fact]
    public async Task WithLessThanTwoSaves_SaysSo()
    {
        await StoreBw2();
        var cut = Render<InventoryTransfer>();
        cut.WaitForText("Il faut au moins deux sauvegardes stockées");
        Assert.Contains("Transfert d'inventaire", cut.Text());
    }

    [Fact]
    public async Task ChooseTwoSaves_SendWithArrowsCountersAndDrop_ThenValidate()
    {
        var platine = await StorePlatinum(); // 10 Potion, 30 Poké Ball...
        var b2 = await StoreBw2(items: [(PokeBall, 3), (HealthFeather, 2)]);
        var nav = Services.GetRequiredService<NavigationManager>();
        var cut = Render<InventoryTransfer>();

        // La gauche d'abord, puis la droite (sans celle de gauche)
        cut.WaitForText("Choisis la première sauvegarde.");
        Assert.Contains("Choisis d'abord la sauvegarde de gauche.", Text(Column(cut, "droite")));
        await cut.FindAll(".transfer-column-gauche .list-group-item").Single(b => b.TextContent.Contains("platine.sav")).ClickAsync(new());
        cut.WaitForText("Choisis la seconde sauvegarde.");
        Assert.Single(cut.FindAll(".transfer-column-droite .list-group-item"));
        await cut.Find(".transfer-column-droite .list-group-item").ClickAsync(new());
        Assert.Contains($"gauche={platine}", nav.Uri);
        Assert.Contains($"droite={b2}", nav.Uri);
        Assert.True(cut.Button("Valider").HasAttribute("disabled")); // rien à transférer

        // Compteur : + et −, saisie ramenée au maximum (10 Potion), puis 4
        await Tab(cut, "gauche", "Médicaments");
        Assert.Equal("1", Counter(cut, "gauche", Potion));
        var plus = Row(cut, "gauche", Potion).QuerySelectorAll("button").First(b => b.TextContent == "+");
        await plus.ClickAsync(new());
        await Row(cut, "gauche", Potion).QuerySelectorAll("button").First(b => b.TextContent == "+").ClickAsync(new());
        Assert.Equal("3", Counter(cut, "gauche", Potion));
        await Row(cut, "gauche", Potion).QuerySelectorAll("button").First(b => b.TextContent == "−").ClickAsync(new());
        Assert.Equal("2", Counter(cut, "gauche", Potion));
        await Row(cut, "gauche", Potion).QuerySelector("input")!.ChangeAsync(new() { Value = "50" });
        Assert.Equal("10", Counter(cut, "gauche", Potion));
        await Row(cut, "gauche", Potion).QuerySelector("input")!.ChangeAsync(new() { Value = "abc" });
        Assert.Equal("1", Counter(cut, "gauche", Potion));
        await Row(cut, "gauche", Potion).QuerySelector("input")!.ChangeAsync(new() { Value = "4" });

        // Envoi par la flèche : −4 à gauche, +4 à droite (en surbrillance), récapitulatif
        await Row(cut, "gauche", Potion).QuerySelector(".transfer-send")!.ClickAsync(new());
        Assert.Contains("−4", Text(Row(cut, "gauche", Potion)));
        Assert.Contains("× 6", Text(Row(cut, "gauche", Potion)));
        await Tab(cut, "droite", "Médicaments");
        Assert.Contains("transfer-item-incoming", Row(cut, "droite", Potion).ClassList);
        Assert.Contains("+4", Text(Row(cut, "droite", Potion)));
        Assert.Contains("4 × Potion", cut.Find(".transfer-recap").TextContent);
        Assert.Contains("4 objets à transférer", cut.Text());

        // Grisé : la Plume Santé n'existe pas en Platine
        var feather = Row(cut, "droite", HealthFeather);
        Assert.Contains("transfer-item-greyed", feather.ClassList);
        Assert.Equal("false", feather.GetAttribute("data-draggable"));
        Assert.Contains("n'existe pas dans Platine", feather.GetAttribute("title"));

        // Glisser-déposer (le navigateur appelle Drop) : une Poké Ball de droite à gauche ; une valeur trafiquée ne fait rien
        await cut.InvokeAsync(() => cut.Instance.Drop("droite", PokeBall));
        await cut.InvokeAsync(() => cut.Instance.Drop("ailleurs", PokeBall));
        await cut.InvokeAsync(() => cut.Instance.Drop("droite", HealthFeather));
        Assert.Contains("1 × Poké Ball", cut.Find(".transfer-recap").TextContent);
        Assert.DoesNotContain("Plume", cut.Find(".transfer-recap").TextContent);

        // Renvoyer une Potion de droite à gauche annule une partie de l'envoi
        await Tab(cut, "droite", "Médicaments");
        await Row(cut, "droite", Potion).QuerySelector(".transfer-send")!.ClickAsync(new());
        Assert.Contains("−3", Text(Row(cut, "gauche", Potion)));

        // Recherche et tris de la colonne de gauche
        await cut.Find(".transfer-column-gauche input[type=search]").InputAsync(new() { Value = "super" });
        Assert.All(cut.FindAll(".transfer-column-gauche .transfer-item .item-name"), n => Assert.Contains("Super", n.TextContent));
        await cut.Find(".transfer-column-gauche input[type=search]").InputAsync(new() { Value = "introuvable" });
        Assert.Contains("Rien ne correspond", Text(Column(cut, "gauche")));
        await cut.Find(".transfer-column-gauche input[type=search]").InputAsync(new() { Value = "" });
        await cut.Find(".transfer-column-gauche .inventory-sort select").ChangeAsync(new() { Value = "quantite" });
        await cut.Find(".transfer-column-gauche .inventory-sort button").ClickAsync(new());
        Assert.Contains("Décroissant", Text(Column(cut, "gauche")));
        var counts = cut.FindAll(".transfer-column-gauche .item-count").Select(c => int.Parse(c.TextContent.Replace("×", "").Trim())).ToList();
        Assert.Equal(counts.OrderDescending(), counts);
        await cut.Find(".transfer-column-gauche .inventory-sort select").ChangeAsync(new() { Value = "nom" });
        Assert.Contains("A → Z", Text(Column(cut, "gauche")));

        // Annuler : tout revient comme au départ
        await cut.Click("Annuler");
        Assert.Empty(cut.FindAll(".transfer-recap"));
        Assert.Empty(Row(cut, "gauche", Potion).QuerySelectorAll(".item-name .badge"));

        // De nouveau 2 Potion et une Poké Ball, puis Valider
        await Row(cut, "gauche", Potion).QuerySelector("input")!.ChangeAsync(new() { Value = "2" });
        await Row(cut, "gauche", Potion).QuerySelector(".transfer-send")!.ClickAsync(new());
        await cut.InvokeAsync(() => cut.Instance.Drop("droite", PokeBall));
        await cut.Click("Valider");
        cut.WaitForText("Transfert fait entre");
        Assert.Empty(cut.FindAll(".transfer-recap"));
        var sources = await Services.GetRequiredService<InventoryService>().ListItemSourcesAsync("sacha");
        Assert.Equal(8, sources.Single(s => s.SaveId == platine).CountOf(Potion));
        Assert.Equal(31, sources.Single(s => s.SaveId == platine).CountOf(PokeBall));
        Assert.Equal((2, 2), (sources.Single(s => s.SaveId == b2).CountOf(Potion), sources.Single(s => s.SaveId == b2).CountOf(PokeBall)));
        cut.WaitForAssertion(() => Assert.Contains("× 8", Text(Row(cut, "gauche", Potion)))); // sacs relus

        // Le journal de l'inventaire nomme l'opération
        var inventory = Render<Inventory>();
        await inventory.Click("Journal");
        inventory.WaitForText("Objets transférés entre sauvegardes");

        // Changer la sauvegarde de gauche : la liste revient, l'adresse l'oublie
        await cut.Click("Changer");
        cut.WaitForText("Choisis la première sauvegarde.");
        Assert.DoesNotContain("gauche=", nav.Uri);
    }

    [Fact]
    public async Task Validate_ShowsTheRefusalOfTheService()
    {
        var platine = await StorePlatinum();
        var b2 = await StoreBw2(items: [(PokeBall, 3)]);
        Services.GetRequiredService<NavigationManager>().NavigateTo($"inventaire/transfert?gauche={platine}&droite={b2}");
        var cut = Render<InventoryTransfer>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".transfer-column-gauche .transfer-item")));
        await Tab(cut, "gauche", "Médicaments");
        await Row(cut, "gauche", Potion).QuerySelector(".transfer-send")!.ClickAsync(new());

        // La sauvegarde de droite disparaît entre-temps (autre onglet)
        using (var db = Db.NewContext())
        {
            db.StoredSaves.Remove(db.StoredSaves.Single(s => s.Id == b2));
            db.SaveChanges();
        }
        await cut.Click("Valider");
        cut.WaitForText("n'existe plus");
        Assert.Contains("alert-danger", cut.Markup);
        await cut.Find(".alert-danger .btn-close").ClickAsync(new());
        Assert.Empty(cut.FindAll(".alert-danger"));
        Assert.NotEmpty(cut.FindAll(".transfer-recap")); // les objets en attente restent
    }

    [Fact]
    public async Task EachColumn_StartsWithTheSortsOfTheAccount()
    {
        // Médicaments : par quantité décroissante (« Mon compte » → « Préférences ») ; les autres onglets par défaut.
        SetSorts("sacha", new SortPreferences { Inventory = new Dictionary<ItemCategory, ItemSort> { [ItemCategory.Medicine] = new(ItemSortBy.Quantity, true) } });
        var platine = await StorePlatinum();
        var b2 = await StoreBw2(items: [(PokeBall, 3), (Potion, 50)]);
        Services.GetRequiredService<NavigationManager>().NavigateTo($"inventaire/transfert?gauche={platine}&droite={b2}");
        var cut = Render<InventoryTransfer>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".transfer-column-gauche .transfer-item")));
        Assert.Contains("Croissant", Text(Column(cut, "gauche"))); // Objets : par défaut

        await Tab(cut, "gauche", "Médicaments");
        Assert.Equal("quantite", cut.Find(".transfer-column-gauche .inventory-sort select").GetAttribute("value"));
        Assert.Contains("Décroissant", Text(Column(cut, "gauche")));
        var counts = cut.FindAll(".transfer-column-gauche .item-count").Select(c => int.Parse(c.TextContent.Replace("×", "").Trim())).ToList();
        Assert.Equal(counts.OrderDescending(), counts);

        // Un tri choisi dans une colonne ne touche ni l'autre colonne, ni les autres onglets
        await cut.Find(".transfer-column-gauche .inventory-sort button").ClickAsync(new());
        Assert.Contains("Croissant", Text(Column(cut, "gauche")));
        await Tab(cut, "droite", "Médicaments");
        Assert.Contains("Décroissant", Text(Column(cut, "droite")));
        await Tab(cut, "gauche", "Objets");
        Assert.Equal("defaut", cut.Find(".transfer-column-gauche .inventory-sort select").GetAttribute("value"));
    }

    [Fact]
    public async Task FirstGenerationSave_HasBagAndPcTabs()
    {
        var red = await StoreReal("sacha", "rouge.sav");
        var b2 = await StoreBw2(items: [(PokeBall, 3)]);
        Services.GetRequiredService<NavigationManager>().NavigateTo($"inventaire/transfert?gauche={b2}&droite={red}");
        var cut = Render<InventoryTransfer>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".transfer-column-droite .nav-pills button")));
        Assert.Empty(cut.FindAll(".transfer-column-gauche .nav-pills"));

        // Une Poké Ball de Noir 2 vers le PC de Rouge
        await cut.FindAll(".transfer-column-droite .nav-pills button").Single(b => b.TextContent.Trim() == "PC").ClickAsync(new());
        Assert.Contains("active", cut.FindAll(".transfer-column-droite .nav-pills button").Single(b => b.TextContent.Trim() == "PC").ClassList);
        await Tab(cut, "gauche", "Pokéballs");
        await Row(cut, "gauche", PokeBall).QuerySelector(".transfer-send")!.ClickAsync(new());
        Assert.Contains("sac → PC", cut.Find(".transfer-recap").TextContent);
        await cut.Click("Valider");
        cut.WaitForText("Transfert fait entre");
        var rouge = (await Services.GetRequiredService<InventoryService>().ListItemSourcesAsync("sacha")).Single(s => s.SaveId == red);
        Assert.True(rouge.CountIn(PokeBall, BagPlace.Pc) >= 1);
    }
}
