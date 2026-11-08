using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MonsutaPotaru.Components.Pages;
using MonsutaPotaru.Data;

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
