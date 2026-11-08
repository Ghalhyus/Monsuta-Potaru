using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MonsutaPotaru.Components.Pages;
using MonsutaPotaru.Data;
using MonsutaPotaru.Services;

namespace MonsutaPotaru.Tests;

/// <summary>Page « Ma collection » : liste, recherche, origine, tris, colonnes, fiche, filtres en pastilles, visite d'un joueur.</summary>
public sealed class CollectionPageTests : PageTestContext
{
    private IReadOnlyList<AngleSharp.Dom.IElement> Slots(IRenderedComponent<Collection> cut) => cut.FindAll(".collection .slot");

    [Fact]
    public void EmptyCollection_SaysSo()
    {
        var cut = Render<Collection>();
        cut.WaitForText("Ta collection est vide pour l'instant.");
    }

    [Fact]
    public async Task List_SearchSourceSortsColumns_AndCard()
    {
        AddToBank("sacha", 25, 133, 1);
        var saveId = await StoreBw2("sacha", "Ludo");
        var cut = Render<Collection>();
        cut.WaitForAssertion(() => Assert.Equal(5, Slots(cut).Count));

        // Fiche d'un Pokémon du stockage interne (téléchargement), puis d'une sauvegarde stockée
        await Slots(cut)[0].ClickAsync(new());
        cut.WaitForText("Fermer");
        if (cut.HasButton("Télécharger le fichier"))
            await cut.Click("Télécharger le fichier");
        await cut.Click("Fermer");
        await cut.Find(".collection-toolbar select.collection-filter").ChangeAsync(new() { Value = saveId.ToString() });
        cut.WaitForAssertion(() => Assert.Equal(2, Slots(cut).Count));
        await Slots(cut)[0].ClickAsync(new());
        cut.WaitForText("💾");
        await cut.Find(".collection-backdrop").ClickAsync(new());
        await cut.Find(".collection-toolbar select.collection-filter").ChangeAsync(new() { Value = "banque" });
        cut.WaitForAssertion(() => Assert.Equal(3, Slots(cut).Count));
        await cut.Find(".collection-toolbar select.collection-filter").ChangeAsync(new() { Value = "" });

        // Recherche
        await cut.Find(".collection-toolbar input").InputAsync(new() { Value = "zzz" });
        cut.WaitForText("Aucun Pokémon ne correspond.");
        await cut.Find(".collection-toolbar input").InputAsync(new() { Value = "" });

        // Tris et sens
        foreach (var sort in new[] { "nom", "pokedex", "arrivee" })
        {
            await cut.Find(".collection-sort select").ChangeAsync(new() { Value = sort });
            await cut.Find(".collection-sort button").ClickAsync(new());
            await cut.Find(".collection-sort button").ClickAsync(new());
        }

        // Colonnes : au curseur, puis la largeur de la fenêtre
        await cut.Find(".collection-columns input").InputAsync(new() { Value = "4" });
        await cut.Find(".collection-columns input").ChangeAsync(new() { Value = "4" });
        await cut.Instance.OnScreenSizeChanged(2);
        await DisposeComponentsAsync();
    }

    [Fact]
    public void Address_GivesSourceSortAndFilters()
    {
        AddToBank("sacha", 25, 4);
        Services.GetRequiredService<NavigationManager>().NavigateTo("collection?source=banque&tri=nom&ordre=desc&colonnes=6&type=feu");
        var cut = Render<Collection>();
        cut.WaitForText("Type");
        Assert.Contains("↓ Z → A", cut.Text());
    }

    [Fact]
    public async Task FilterChips_EachCriterion_CanBeAddedSetAndRemoved()
    {
        AddToBank("sacha", 25, 133, 1, 4, 7);
        var cut = Render<Collection>();
        cut.WaitForAssertion(() => Assert.NotEmpty(Slots(cut)));

        for (var round = 0; round < 12; round++)
        {
            await cut.Click("+ Filtre");
            var items = cut.FindAll(".filter-chip-add .dropdown-item");
            if (items.Count == 0)
                break;
            await items[0].ClickAsync(new());
            var popover = cut.Find(".filter-chip.open .filter-popover");
            var boxes = popover.QuerySelectorAll("input[type=checkbox]").Length;
            for (var i = 0; i < Math.Min(boxes, 4); i++)
                await cut.FindAll(".filter-chip.open .filter-popover input[type=checkbox]")[i].ChangeAsync(new() { Value = true });
            if (boxes > 0)
                await cut.FindAll(".filter-chip.open .filter-popover input[type=checkbox]")[0].ChangeAsync(new() { Value = false });
            var radios = cut.FindAll(".filter-chip.open .filter-popover input[type=radio]");
            if (radios.Count > 0)
            {
                await radios[1].ChangeAsync(new() { Value = true });
                await cut.FindAll(".filter-chip.open .filter-popover input[type=radio]")[2].ChangeAsync(new() { Value = true });
            }
            var numbers = cut.FindAll(".filter-chip.open .filter-popover input[type=number]");
            for (var i = 0; i < numbers.Count; i++)
                await cut.FindAll(".filter-chip.open .filter-popover input[type=number]")[i].ChangeAsync(new() { Value = i == 0 ? "5" : "50" });
            if (cut.FindAll(".filter-chip.open .filter-popover input[type=search]") is [var search])
            {
                await search.InputAsync(new() { Value = "zzz" });
                cut.WaitForText("Aucun talent ne correspond.");
                await cut.Find(".filter-chip.open .filter-popover input[type=search]").InputAsync(new() { Value = "" });
            }
            // Fermer la pastille, la rouvrir, fermer en cliquant à côté
            await cut.Find(".filter-chip.open .filter-chip-label").ClickAsync(new());
            await cut.FindAll(".filter-chip .filter-chip-label")[^1].ClickAsync(new());
            await cut.Find(".filter-backdrop").ClickAsync(new());
        }
        Assert.True(cut.HasButton("Tout effacer"));

        // Un niveau seul, puis retrait de chaque pastille une à une
        foreach (var _ in Enumerable.Range(0, 3))
            await cut.FindAll(".filter-chip-remove")[0].ClickAsync(new());
        await cut.Click("Tout effacer");
        Assert.Empty(cut.FindAll(".filter-chip"));
        await cut.Click("+ Filtre");
        await cut.Click("+ Filtre");
    }

    [Fact]
    public void FilterChips_LevelSummaries()
    {
        AddToBank("sacha", 25);
        foreach (var query in new[] { "niveau=5-", "niveau=-50", "niveau=5-5", "niveau=5-50", "iv=pv20,vit31", "type=feu,eau,plante,normal&generation=1,2&oeuf=non&mega=oui" })
        {
            Services.GetRequiredService<NavigationManager>().NavigateTo("collection?" + query);
            var cut = Render<Collection>();
            cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".filter-chip")));
        }
    }

    // ---------- Mode compact ----------

    private static IReadOnlyList<AngleSharp.Dom.IElement> Folders(IRenderedComponent<Collection> cut) => cut.FindAll(".collection .slot.folder");

    [Fact]
    public async Task Compact_GroupsBySpecies_FoldersCountAndUnfoldInTheGrid()
    {
        var ids = AddToBank("sacha", 25, 133, 25, 1, 25, 7, 4);
        using (var db = Db.NewContext()) // deux œufs (les deux derniers) : un dossier « Œufs », à la fin
        {
            foreach (var egg in db.BankPokemon.Where(p => p.Id == ids[5] || p.Id == ids[6]))
                egg.IsEgg = true;
            db.SaveChanges();
        }
        var nav = Services.GetRequiredService<NavigationManager>();
        var cut = Render<Collection>();
        cut.WaitForAssertion(() => Assert.Equal(7, Slots(cut).Count));
        Assert.Empty(Folders(cut));

        // Interrupteur : Pikachu ×3, Évoli et Bulbizarre seuls (cases normales), Œufs ×2
        await cut.Find("#collection-compact").ChangeAsync(new() { Value = true });
        cut.WaitForAssertion(() => Assert.Equal(4, Slots(cut).Count));
        Assert.Contains("compact=1", nav.Uri);
        Assert.Equal(["3", "2"], Folders(cut).Select(f => f.QuerySelector(".count")!.TextContent));
        Assert.StartsWith("Pikachu : 3 Pokémon", Folders(cut)[0].GetAttribute("title"));
        Assert.StartsWith("Œufs : 2 Pokémon", Folders(cut)[1].GetAttribute("title"));
        Assert.Equal("false", Folders(cut)[0].GetAttribute("aria-expanded"));

        // Ouvrir Pikachu : ses 3 Pokémon suivent le dossier ; ouvrir aussi les œufs ; un Pokémon ouvre sa fiche
        await Folders(cut)[0].ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Equal(7, Slots(cut).Count));
        Assert.Equal("true", Folders(cut)[0].GetAttribute("aria-expanded"));
        Assert.Contains("open", Folders(cut)[0].ClassList);
        var unfolded = Slots(cut);
        Assert.All(unfolded.Skip(1).Take(3), s => Assert.Contains("in-folder", s.ClassList));
        Assert.DoesNotContain("in-folder", unfolded[4].ClassList);
        await Folders(cut)[1].ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Equal(9, Slots(cut).Count));
        await Slots(cut)[1].ClickAsync(new());
        cut.WaitForText("Fermer");
        await cut.Click("Fermer");

        // Tris, recherche et colonnes gardent les dossiers ouverts
        foreach (var sort in new[] { "nom", "pokedex", "arrivee" })
        {
            await cut.Find(".collection-sort select").ChangeAsync(new() { Value = sort });
            await cut.Find(".collection-sort button").ClickAsync(new());
            cut.WaitForAssertion(() => Assert.Equal(9, Slots(cut).Count));
        }
        await cut.Find(".collection-toolbar input").InputAsync(new() { Value = "pika" });
        cut.WaitForAssertion(() => Assert.Equal(4, Slots(cut).Count));
        await cut.Find(".collection-toolbar input").InputAsync(new() { Value = "" });
        await cut.Find(".collection-columns input").InputAsync(new() { Value = "4" });
        cut.WaitForAssertion(() => Assert.Equal(9, Slots(cut).Count));

        // Refermer, puis quitter le mode compact (les dossiers ouverts sont oubliés)
        await Folders(cut)[0].ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Equal(6, Slots(cut).Count));
        await cut.Find("#collection-compact").ChangeAsync(new() { Value = false });
        cut.WaitForAssertion(() => Assert.Equal(7, Slots(cut).Count));
        Assert.DoesNotContain("compact=", nav.Uri);
        await cut.Find("#collection-compact").ChangeAsync(new() { Value = true });
        cut.WaitForAssertion(() => Assert.Equal(4, Slots(cut).Count));
        await DisposeComponentsAsync();
    }

    [Fact]
    public async Task Compact_FromTheAddress_WithFilters_AndForAVisitor()
    {
        AddToBank("ondine", 120, 120, 121);
        await StoreBw2("ondine", "Ondine"); // un Pikachu et un Salamèche, dans une sauvegarde
        Services.GetRequiredService<NavigationManager>().NavigateTo("joueurs/ondine/collection?compact=1");
        var cut = Render<Collection>(p => p.Add(c => c.Pseudo, "ondine"));
        cut.WaitForAssertion(() => Assert.Equal(4, Slots(cut).Count)); // Stari ×2, Staross, Pikachu, Salamèche
        Assert.True(cut.Find("#collection-compact").HasAttribute("checked"));
        await Folders(cut).Single().ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Equal(6, Slots(cut).Count));
        await Slots(cut)[1].ClickAsync(new());
        cut.WaitForText("Fermer");
        Assert.False(cut.HasButton("Télécharger le fichier"));
        await cut.Click("Fermer");

        // Origine : le stockage interne seul (le dossier Stari reste ouvert)
        await cut.Find(".collection-toolbar select.collection-filter").ChangeAsync(new() { Value = "banque" });
        cut.WaitForAssertion(() => Assert.Equal(4, Slots(cut).Count));
        await DisposeComponentsAsync();

        // Pastilles dans l'adresse : seul Salamèche est de type Feu, une case normale
        Services.GetRequiredService<NavigationManager>().NavigateTo("joueurs/ondine/collection?compact=1&type=feu");
        var fire = Render<Collection>(p => p.Add(c => c.Pseudo, "ondine"));
        fire.WaitForAssertion(() => Assert.Single(Slots(fire)));
        Assert.Empty(Folders(fire));
        await DisposeComponentsAsync();
    }

    [Fact]
    public async Task Sort_FromTheAccount_UnlessTheAddressSaysOtherwise()
    {
        AddToBank("sacha", 1, 4, 25); // Bulbizarre, Salamèche, puis Pikachu (le plus récent)
        SetSorts("sacha", new SortPreferences { Collection = new(CollectionSortBy.Name) });
        var navigation = Services.GetRequiredService<NavigationManager>();
        var cut = Render<Collection>();
        cut.WaitForAssertion(() => Assert.Equal(3, Slots(cut).Count));
        string Order() => cut.Find(".collection-sort button").TextContent;
        Assert.Equal("↑ A → Z", Order());
        Assert.Contains("Bulbizarre", Slots(cut)[0].OuterHtml);

        // L'arrivée, qui n'est plus le tri du compte, s'écrit dans l'adresse ; le nom n'y est pas écrit
        await cut.Find(".collection-sort select").ChangeAsync(new() { Value = "arrivee" });
        Assert.Contains("tri=arrivee", navigation.Uri);
        cut.WaitForAssertion(() => Assert.Contains("Pikachu", Slots(cut)[0].OuterHtml));
        await cut.Find(".collection-sort select").ChangeAsync(new() { Value = "nom" });
        Assert.DoesNotContain("tri=", navigation.Uri);
        await cut.Find(".collection-sort button").ClickAsync(new());
        Assert.Contains("tri=nom&ordre=desc", navigation.Uri);
        await DisposeComponentsAsync();

        // L'adresse l'emporte (ancien lien « ?ordre=asc » : l'arrivée, les plus anciens d'abord)
        navigation.NavigateTo("collection?ordre=asc");
        var old = Render<Collection>();
        old.WaitForAssertion(() => Assert.Equal("↑ Anciens d'abord", old.Find(".collection-sort button").TextContent));
        await DisposeComponentsAsync();

        // Chez un autre joueur : le tri de celui qui regarde
        AddToBank("ondine", 120);
        navigation.NavigateTo("joueurs/ondine/collection");
        var visit = Render<Collection>(p => p.Add(c => c.Pseudo, "ondine"));
        visit.WaitForAssertion(() => Assert.Equal("↑ A → Z", visit.Find(".collection-sort button").TextContent));
        await DisposeComponentsAsync();
    }

    [Fact]
    public async Task Visitor_SeesAPublicCollection_ReadOnly()
    {
        AddToBank("ondine", 120);
        await StoreBw2("ondine", "Ondine");
        var cut = Render<Collection>(p => p.Add(c => c.Pseudo, "ondine"));
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".collection .slot").Count));
        Assert.DoesNotContain("Emplacements", cut.Text());
        await cut.FindAll(".collection .slot")[0].ClickAsync(new());
        cut.WaitForText("Fermer");
        Assert.False(cut.HasButton("Télécharger le fichier"));

        UpdateUser("pierre", u => u.Audience = ProfileAudience.Nobody);
        var hidden = Render<Collection>(p => p.Add(c => c.Pseudo, "pierre"));
        hidden.WaitForAssertion(() => Assert.DoesNotContain("Collection", hidden.Text()));
    }
}
