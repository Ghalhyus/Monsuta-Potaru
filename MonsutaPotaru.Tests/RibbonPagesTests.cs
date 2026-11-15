using System.Text.RegularExpressions;
using Bunit;
using PKHeX.Core;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MonsutaPotaru.Components.Pages;
using MonsutaPotaru.Services;

namespace MonsutaPotaru.Tests;

/// <summary>Pages « Rubans » : liste (ajout d'un Pokémon, recherche, tri) et un suivi (filtres, fiche, Pokémon parti, arrêt).</summary>
public sealed class RibbonPagesTests : PageTestContext
{
    private RibbonService RibbonStore => Services.GetRequiredService<RibbonService>();
    private NavigationManager Navigation => Services.GetRequiredService<NavigationManager>();

    /// <summary>Met ces espèces au stockage interne et les suit ; rend les identifiants des suivis.</summary>
    private async Task<List<int>> Track(params ushort[] species)
    {
        var ids = new List<int>();
        foreach (var bankId in AddToBank("sacha", species))
            ids.Add((await RibbonStore.AddAsync("sacha", bankId)).Id);
        return ids;
    }

    [Fact]
    public async Task List_AddsAPokemonFromTheCollection()
    {
        var ids = AddToBank("sacha", 25, 133, 1);
        using (var db = Db.NewContext())
        {
            db.BankPokemon.Single(p => p.Id == ids[2]).IsEgg = true;
            db.SaveChanges();
        }
        var cut = Render<RibbonList>();
        cut.WaitForText("Tu ne suis encore aucun Pokémon.");

        // Le choix du dépôt GTS : toute la collection, l'œuf grisé avec sa raison
        await cut.Click("+ Suivre un Pokémon");
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".collection-picker .slot").Count));
        Assert.True(cut.Button("Suivre").HasAttribute("disabled"));
        await cut.Find(".collection-picker .slot.locked").ClickAsync(new());
        cut.WaitForText("Un œuf ne peut pas avoir de rubans : fais-le éclore d'abord.");
        Assert.True(cut.Button("Suivre").HasAttribute("disabled"));
        Assert.NotNull(cut.Find(".collection-toolbar .filter-chips")); // pastilles de « Ma collection »

        await cut.Find(".collection-toolbar input").InputAsync(new() { Value = "zzz" });
        cut.WaitForText("Aucun Pokémon ne correspond.");
        await cut.Click("Annuler");
        await cut.Click("+ Suivre un Pokémon"); // le choix repart de zéro
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".collection-picker .slot").Count));

        await cut.Find(".collection-toolbar input").InputAsync(new() { Value = "pika" });
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".collection-picker .slot")));
        await cut.Find(".collection-picker .slot").ClickAsync(new());
        Assert.Contains("selected", cut.Find(".collection-picker .slot").ClassList);
        await cut.Click("Suivre Pikachu");
        Assert.Matches(@"rubans/\d+$", Navigation.Uri);
        Assert.Single(await RibbonStore.ListAsync("sacha"));
    }

    [Fact]
    public async Task List_GraysTrackedPokemon_AndAddRefusedShowsTheMessage()
    {
        var ids = AddToBank("sacha", 25, 133);
        await RibbonStore.AddAsync("sacha", ids[1]);
        var cut = Render<RibbonList>();
        await cut.Click("+ Suivre un Pokémon");
        cut.WaitForAssertion(() => Assert.Contains("Évoli est déjà suivi.", cut.Find(".collection-picker .slot.locked").GetAttribute("title")));

        await cut.Find(".collection-picker .slot:not(.locked)").ClickAsync(new());
        await RibbonStore.AddAsync("sacha", ids[0]); // ajouté entre-temps depuis un autre onglet
        await cut.Click("Suivre Pikachu");
        cut.WaitForText("Pikachu est déjà suivi.");
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".collection-picker .slot.locked").Count)); // grisé à son tour
        Assert.True(cut.Button("Suivre").HasAttribute("disabled"));
        await cut.Find(".alert .btn-close").ClickAsync(new());
        Assert.Empty(cut.FindAll(".alert"));
    }

    [Fact]
    public async Task List_SearchesAndSorts()
    {
        await Track(25, 133, 1);
        var cut = Render<RibbonList>();
        cut.WaitForText("Pikachu");
        Assert.Equal(3, cut.FindAll(".livingdex-card").Count);
        Assert.Contains("0 / 88", cut.Find(".livingdex-card").TextContent);
        Assert.Contains("Stockage interne", cut.Find(".livingdex-card").TextContent);

        await cut.Find("input[type=search]").InputAsync(new() { Value = "evo" });
        Assert.Equal("Évoli", Assert.Single(cut.FindAll(".livingdex-card strong")).TextContent.Trim());
        await cut.Find("input[type=search]").InputAsync(new() { Value = "mewtwo" });
        cut.WaitForText("Aucun Pokémon suivi ne correspond à « mewtwo ».");

        await cut.Find("#ribbonSort").ChangeAsync(new() { Value = "nom" });
        Assert.Contains("tri=nom", Navigation.Uri);
        await cut.Click("↑");
        Assert.Contains("ordre=desc", Navigation.Uri);
    }

    [Fact]
    public async Task List_UsesTheAccountSort_AndTheAddressOne()
    {
        await Track(25, 133, 1); // Pikachu, Évoli, Bulbizarre, dans l'ordre d'ajout
        static List<string> Names(IRenderedComponent<RibbonList> cut) =>
            [.. cut.FindAll(".livingdex-card strong").Select(s => s.TextContent.Trim())];

        Assert.Equal(["Pikachu", "Évoli", "Bulbizarre"], Names(Render<RibbonList>()));
        SetSorts("sacha", new SortPreferences { Ribbons = new(RibbonSort.Name) });
        Assert.Equal(["Bulbizarre", "Évoli", "Pikachu"], Names(Render<RibbonList>()));
        Navigation.NavigateTo("rubans?tri=ajout&ordre=desc");
        Assert.Equal(["Bulbizarre", "Évoli", "Pikachu"], Names(Render<RibbonList>()));
    }

    [Fact]
    public async Task Page_ShowsTheRibbons_FiltersThem_AndOpensTheCard()
    {
        var id = (await Track(25)).Single();
        var cut = Render<RibbonPage>(p => p.Add(x => x.Id, id));
        cut.WaitForText("Panthéon");
        Assert.Equal(42, cut.FindAll(".ribbon-item").Count);
        Assert.Contains("0 / 88", cut.Find(".livingdex-header").TextContent); // les rubans des souvenirs un par un
        Assert.Contains("0 / 40", cut.Markup); // Souvenir Concours
        Assert.Contains("Stockage interne", cut.Markup);
        Assert.Contains("Pokémon Colosseum et XD", cut.Markup);

        await cut.Click("Possédés (0)");
        Assert.Contains("filtre=possedes", Navigation.Uri);
        Navigation.NavigateTo($"rubans/{id}?filtre=possedes");
        cut = Render<RibbonPage>(p => p.Add(x => x.Id, id));
        cut.WaitForText("Aucun ruban pour l'instant.");
        Assert.Empty(cut.FindAll(".ribbon-item"));
        Navigation.NavigateTo($"rubans/{id}?filtre=manquants");
        cut = Render<RibbonPage>(p => p.Add(x => x.Id, id));
        Assert.Equal(42, cut.FindAll(".ribbon-item.missing").Count);
        await cut.Click("Tous (42)");

        // Détail d'un souvenir : ses 40 rubans de concours, tous manquants ; fermé par le bouton ou le fond
        Assert.Equal(2, cut.FindAll("button.ribbon-item-button").Count);
        await cut.FindAll("button.ribbon-item-button")[0].ClickAsync(new());
        var dialog = cut.Find(".ribbon-dialog");
        Assert.Equal(40, dialog.QuerySelectorAll(".ribbon-item.missing").Length);
        Assert.Contains("Hoenn", dialog.TextContent);
        Assert.Contains("Sinnoh", dialog.TextContent);
        Assert.Contains("0 / 40 rubans", dialog.TextContent);
        Assert.DoesNotContain("pas lesquels", dialog.TextContent);
        await cut.Click("Fermer");
        Assert.Empty(cut.FindAll(".ribbon-dialog"));
        await cut.FindAll("button.ribbon-item-button")[1].ClickAsync(new());
        Assert.Equal(8, cut.FindAll(".ribbon-dialog .ribbon-item").Count);
        await cut.Find(".collection-backdrop").ClickAsync(new());
        Assert.Empty(cut.FindAll(".ribbon-dialog"));

        await cut.Find(".ribbon-sprite-button").ClickAsync(new());
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find(".collection-dialog")));
        await cut.Click("Fermer");
        Assert.Empty(cut.FindAll(".collection-dialog"));
    }

    /// <summary>Pokémon passé en 6e génération : le détail d'un souvenir entamé ne dit pas lesquels, et le dit.</summary>
    [Fact]
    public async Task Page_SouvenirDetail_OnlyTheCounterAfterGen6()
    {
        var pk = new PK6
        {
            Species = 25, Version = GameVersion.X, Language = (int)LanguageID.French, OriginalTrainerName = "Ludo",
            TID16 = 54321, PID = 0xCAFE, RibbonCountMemoryContest = 23, Move1 = 33,
        };
        pk.CurrentLevel = 30;
        pk.ClearNickname();
        pk.RefreshChecksum();
        int bankId;
        await using (var db = Db.NewContext())
        {
            var entry = BankService.CreateEntry(Services.GetRequiredService<PokemonInfo>(), pk, "sacha", DateTime.UtcNow);
            db.BankPokemon.Add(entry);
            await db.SaveChangesAsync();
            bankId = entry.Id;
        }
        var id = (await RibbonStore.AddAsync("sacha", bankId)).Id;

        var cut = Render<RibbonPage>(p => p.Add(x => x.Id, id));
        cut.WaitForText("23 / 40");
        Assert.Contains("23 / 88", cut.Find(".livingdex-header").TextContent);
        Assert.Contains("Manquants (42)", cut.Markup); // le souvenir entamé est manquant…
        Assert.Contains("Possédés (1)", cut.Markup); // …et possédé
        await cut.FindAll("button.ribbon-item-button")[0].ClickAsync(new());
        var dialog = cut.Find(".ribbon-dialog");
        Assert.Contains("23 / 40 rubans", dialog.TextContent);
        Assert.Contains("le jeu n'a gardé que le nombre de ces rubans, pas lesquels", dialog.TextContent);
        Assert.Empty(dialog.QuerySelectorAll(".ribbon-item.missing"));
        Assert.Equal(40, Regex.Matches(dialog.TextContent, "Inconnu").Count);
    }

    [Fact]
    public async Task Page_PokemonGone_KeepsItsLastRibbons()
    {
        var id = (await Track(25)).Single();
        await using (var db = Db.NewContext())
            await db.BankPokemon.ExecuteDeleteAsync(); // retiré du compte
        var cut = Render<RibbonPage>(p => p.Add(x => x.Id, id));
        cut.WaitForText("Plus dans ta collection");
        Assert.Empty(cut.FindAll(".ribbon-sprite-button")); // plus de fiche à ouvrir
        Assert.Contains("Plus dans ta collection", Render<RibbonList>().Markup);
    }

    [Fact]
    public async Task Page_StopsTheTracking_AndUnknownId()
    {
        var id = (await Track(25)).Single();
        var cut = Render<RibbonPage>(p => p.Add(x => x.Id, id));
        await cut.Click("Arrêter le suivi");
        cut.WaitForText("le Pokémon, lui, ne bouge pas");
        await cut.Click("Annuler");
        await cut.Click("Arrêter le suivi");
        await cut.Click("Supprimer");
        Assert.EndsWith("/rubans", Navigation.Uri);
        Assert.Empty(await RibbonStore.ListAsync("sacha"));

        cut = Render<RibbonPage>(p => p.Add(x => x.Id, id));
        cut.WaitForText("Ce suivi n'existe plus.");
        // Le suivi d'un autre joueur (adresse trafiquée) n'existe pas pour moi
        var theirs = (await RibbonStore.AddAsync("ondine", AddToBank("ondine", 120).Single())).Id;
        Render<RibbonPage>(p => p.Add(x => x.Id, theirs)).WaitForText("Ce suivi n'existe plus.");
    }
}
