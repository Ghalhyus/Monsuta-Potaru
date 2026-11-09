using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Park = MonsutaPotaru.Components.Pages.Park;
using MonsutaPotaru.Data;
using MonsutaPotaru.Services;

namespace MonsutaPotaru.Tests;

/// <summary>Page « Parc » : choix des Pokémon, fiche d'un Pokémon cliqué, vidage, agrandissement, achat d'un parc, mode debug.</summary>
public sealed class ParkPageTests : PageTestContext
{
    public ParkPageTests()
    {
        PokemonSprites.Load(RealSavesTests.WebRootWithImages()); // sprites Donjon Mystère : qui peut aller au parc
        ParkHabitats.Load(RealSavesTests.WebRoot());
    }

    private ParkService Parks => Services.GetRequiredService<ParkService>();

    [Fact]
    public async Task Chooser_PutsPokemonInThePark_ThenOneIsClickedAndRemoved()
    {
        var ids = AddToBank("sacha", 25, 1, 4, 7);
        var cut = Render<Park>();
        cut.WaitForText("Le parc est vide.");

        await cut.Click("Choisir des Pokémon");
        cut.WaitForText("Pokémon du parc");
        var count = cut.FindAll(".park-candidates .slot").Count;
        Assert.True(count >= 3, cut.Markup);
        foreach (var i in Enumerable.Range(0, count))
            await cut.FindAll(".park-candidates .slot")[i].ClickAsync(new());
        await cut.FindAll(".park-candidates .slot")[count - 1].ClickAsync(new()); // décoché
        // « Ceux de ce parc » suit le parc enregistré : rien avant « Valider »
        await cut.Find("select.park-shown").ChangeAsync(new() { Value = "ThisPark" });
        cut.WaitForText("Ce parc n'a pas encore de Pokémon.");
        await cut.Find("select.park-shown").ChangeAsync(new() { Value = "All" });
        await cut.Find(".dialog-panel input[type=search]").InputAsync(new() { Value = "zzz" });
        cut.WaitForText("Aucun Pokémon ne correspond.");
        await cut.Find(".dialog-panel input[type=search]").InputAsync(new() { Value = "" });
        await cut.Click("Valider");
        cut.WaitForText($"{count - 1} / 20");

        // Fiche d'un Pokémon cliqué dans le parc (son humeur), puis départ
        var parkId = (await Parks.ListParksAsync("sacha"))[0].Id;
        var members = (await Parks.ListAsync("sacha", parkId)).Select(m => m.Id).ToList();
        var outside = ids.Except(members).First();
        ids = [members[0]];
        await cut.Instance.OnPokemonClicked(ids[0]);
        cut.WaitForText("Retirer du parc");
        await cut.Click("Fermer");
        await cut.Instance.OnPokemonClicked(outside); // pas dans le parc : rien
        Assert.False(cut.HasButton("Retirer du parc"));
        await cut.Instance.OnPokemonClicked(ids[0]);
        await cut.Find(".collection-backdrop").ClickAsync(new());
        await cut.Instance.OnPokemonClicked(ids[0]);
        await cut.Click("Retirer du parc");
        cut.WaitForText("a quitté le parc");
        Assert.Contains($"{count - 2} / 20", cut.Text());

        // Vider le parc : confirmation, annulation, puis pour de bon
        await cut.Click("Vider le parc");
        cut.WaitForText("Vider le parc ?");
        await cut.Find(".dialog-backdrop").ClickAsync(new());
        await cut.Click("Vider le parc");
        await cut.Buttons("Annuler")[0].ClickAsync(new());
        await cut.Click("Vider le parc");
        await cut.Find(".dialog-panel .btn-danger").ClickAsync(new());
        cut.WaitForText("Le parc est vide.");
        await cut.Find(".alert .btn-close").ClickAsync(new());
    }

    [Fact]
    public async Task Chooser_RefusesMoreThanTheCapacity_AndCanBeCancelled()
    {
        AddToBank("sacha", [.. Enumerable.Range(1, 21).Select(i => (ushort)i)]);
        var cut = Render<Park>();
        await cut.Click("Choisir les Pokémon");
        cut.WaitForText("Pokémon du parc");
        foreach (var i in Enumerable.Range(0, 21))
            await cut.FindAll(".park-candidates .slot")[i].ClickAsync(new());
        cut.WaitForText("Le parc est plein");
        await cut.Click("Tout retirer");
        Assert.Contains("· 0 / 20", cut.Text());
        await cut.Click("Annuler");
        Assert.Empty(cut.FindAll(".park-candidates"));
        await cut.Click("Choisir les Pokémon");
        await cut.Find(".dialog-backdrop").ClickAsync(new());
        Assert.Empty(cut.FindAll(".park-candidates"));
    }

    [Fact]
    public async Task EmptyCollection_SaysSo()
    {
        var cut = Render<Park>();
        await cut.Click("Choisir les Pokémon");
        cut.WaitForText("Ta collection n'a pas encore de Pokémon.");
        await cut.Find("select.park-shown").ChangeAsync(new() { Value = "NoPark" });
        cut.WaitForText("Ta collection n'a pas encore de Pokémon.");
    }

    [Fact]
    public async Task Chooser_ShowsAllPokemon_ThoseOfThisPark_OrThoseInNoPark()
    {
        // Pikachu dans la Forêt, Carapuce à la Plage, Bulbizarre et Salamèche dans aucun parc
        var ids = AddToBank("sacha", 25, 7, 1, 4);
        GiveMoney("sacha", ParkService.ParkPrice);
        var forest = (await Parks.ListParksAsync("sacha"))[0].Id;
        var beach = await Parks.BuyAsync("sacha", ParkLandscape.Beach);
        Assert.True(beach.Ok, beach.Message);
        Assert.True((await Parks.SetAsync("sacha", forest, [ids[0]])).Ok);
        Assert.True((await Parks.SetAsync("sacha", beach.ParkId, [ids[1]])).Ok);
        var cut = Render<Park>();
        await cut.Click("Choisir les Pokémon");
        cut.WaitForText("Pokémon du parc");
        string[] Shown() => [.. cut.FindAll(".park-candidates .slot").Select(slot => slot.GetAttribute("title")!.Split('(', ')')[1])]; // « Surnom (Espèce) N.10 »

        Assert.Equal("All", cut.Find("select.park-shown").GetAttribute("value"));
        Assert.Equal(["Bulbizarre", "Salamèche", "Carapuce", "Pikachu"], Shown());
        await cut.Find("select.park-shown").ChangeAsync(new() { Value = "ThisPark" });
        Assert.Equal(["Pikachu"], Shown());
        await cut.Find("select.park-shown").ChangeAsync(new() { Value = "NoPark" });
        Assert.Equal(["Bulbizarre", "Salamèche"], Shown());

        // Cocher ou décocher ne change pas la liste avant « Valider » : Bulbizarre reste dans « aucun parc »
        await cut.FindAll(".park-candidates .slot")[0].ClickAsync(new());
        Assert.Equal(["Bulbizarre", "Salamèche"], Shown());
        Assert.Contains("· 2 / 20", cut.Text());
        await cut.Find(".dialog-panel input[type=search]").InputAsync(new() { Value = "zzz" });
        cut.WaitForText("Aucun Pokémon ne correspond.");
        await cut.Find(".dialog-panel input[type=search]").InputAsync(new() { Value = "" });
        await cut.Click("Valider");
        cut.WaitForText("2 / 20");

        // Réouverture : « Tous les Pokémon » ; Bulbizarre est maintenant dans ce parc
        await cut.Click("Choisir les Pokémon");
        Assert.Equal("All", cut.Find("select.park-shown").GetAttribute("value"));
        await cut.Find("select.park-shown").ChangeAsync(new() { Value = "ThisPark" });
        Assert.Equal(["Bulbizarre", "Pikachu"], Shown());
        await cut.Find("select.park-shown").ChangeAsync(new() { Value = "NoPark" });
        Assert.Equal(["Salamèche"], Shown());
        await cut.FindAll(".park-candidates .slot")[0].ClickAsync(new());
        await cut.Click("Valider");
        await cut.Click("Choisir les Pokémon");
        await cut.Find("select.park-shown").ChangeAsync(new() { Value = "NoPark" });
        cut.WaitForText("Tous tes Pokémon vivent déjà dans un parc.");
    }

    [Fact]
    public async Task Expand_AndBuy_ArePaidWithTheWallet()
    {
        var cut = Render<Park>();
        await cut.Click("Agrandir le parc");
        cut.WaitForText("Il manque");
        await cut.Click("Annuler");
        await cut.Click("Acheter un parc");
        cut.WaitForText("Paysage");
        Assert.Contains("Il manque", cut.Text());
        await cut.Find(".dialog-backdrop").ClickAsync(new());

        GiveMoney("sacha", 3 * ParkService.ExpansionPrice);
        await cut.Click("Agrandir le parc");
        await cut.Click("Payer");
        cut.WaitForText("/ 40");
        // Le message de l'agrandissement, affiché quand le parc redémarre, ne compte pas dans sa hauteur (parc.js)
        Assert.True(cut.Find(".alert").HasAttribute("data-hors-mesure"));
        await cut.Click("Agrandir le parc");
        await cut.Find(".dialog-backdrop").ClickAsync(new());

        await cut.Click("Acheter un parc");
        Assert.True(cut.Button("Payer").HasAttribute("disabled")); // aucun paysage choisi
        await cut.Find("#landscape-" + (int)ParkLandscape.Beach).ChangeAsync(new() { Value = true });
        await cut.Click("Payer");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("select.park-choice")));
        var forest = (await Parks.ListParksAsync("sacha")).Single(p => p.Landscape == ParkLandscape.Forest).Id;
        await cut.Find("select.park-choice").ChangeAsync(new() { Value = forest.ToString() });
        cut.WaitForText("/ 40");
        await cut.Find("select.park-choice").ChangeAsync(new() { Value = "pas un nombre" });

        // Dans la plage, la fenêtre de choix parle de l'autre parc
        AddToBank("sacha", 25);
        await cut.Click("Choisir les Pokémon");
        cut.WaitForText("le quittent pour venir ici");
    }

    [Fact]
    public async Task Debug_ResizesThePark_AndShowsTheHabitats()
    {
        MakeAdmin("sacha");
        Assert.True((await Services.GetRequiredService<DebugService>().SetActiveAsync("sacha", true)).Ok);
        var ids = AddToBank("sacha", 25, 133);
        Assert.True((await Parks.SetAsync("sacha", (await Parks.ListParksAsync("sacha"))[0].Id, ids)).Ok);
        var cut = Render<Park>();
        cut.WaitForText("Habitats (debug)");

        await cut.Click("+ taille (debug)");
        cut.WaitForText("/ 40");
        await cut.Click("Habitats (debug)");
        Assert.NotEmpty(cut.FindAll(".park-habitat"));
        await cut.Click("− taille (debug)");
        cut.WaitForText("/ 20");
        await cut.Click("Habitats (debug)");
        Assert.Empty(cut.FindAll(".park-habitat"));

        await cut.Instance.OnPokemonClicked(ids[0]);
        await cut.Click("Attaquer (debug)");
        cut.WaitForText("Aucun autre Pokémon à attaquer.");
        await DisposeComponentsAsync();
    }

    [Fact]
    public async Task Visitor_WatchesAPublicPark_WithoutChanging()
    {
        var ids = AddToBank("ondine", 7);
        Assert.True((await Parks.SetAsync("ondine", (await Parks.ListParksAsync("ondine"))[0].Id, ids)).Ok);
        var cut = Render<Park>(p => p.Add(x => x.Pseudo, "ondine"));
        cut.WaitForText("Les Pokémon de ondine vivent ici");
        Assert.False(cut.HasButton("Choisir les Pokémon"));
        await cut.Instance.OnPokemonClicked(ids[0]);
        cut.WaitForText("Fermer");
        Assert.False(cut.HasButton("Retirer du parc"));

        UpdateUser("pierre", u => u.Audience = ProfileAudience.Nobody);
        var hidden = Render<Park>(p => p.Add(x => x.Pseudo, "pierre"));
        hidden.WaitForAssertion(() => Assert.DoesNotContain("vivent ici", hidden.Text()));
    }
}
