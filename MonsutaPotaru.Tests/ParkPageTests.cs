using Bunit;
using Microsoft.EntityFrameworkCore;
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
        // Sprites Donjon Mystère (qui peut aller au parc) et types de cases des cartes : chargés une fois pour tous par
        // TestAssemblySetup ; ici, seulement un message clair si les images n'ont pas été récupérées.
        RealSavesTests.WebRootWithImages();
    }

    private ParkService Parks => Services.GetRequiredService<ParkService>();

    /// <summary>Démarrages du parc dans le navigateur (parc.js : create) : carte, taille, Pokémon déplaçables.</summary>
    private List<(string Map, int Size, bool CanMove)> Starts() =>
        [.. JSInterop.Invocations.Where(i => i.Identifier == "create").Select(i => i.Arguments[2]!)
            .Select(o => ((string)Option(o, "map")!, (int)Option(o, "size")!, (bool)Option(o, "canMove")!))];

    private static object? Option(object options, string name) => options.GetType().GetProperty(name)!.GetValue(options);

    /// <summary>Pokémon envoyés au parc dans le navigateur la dernière fois (parc.js : setPokemon).</summary>
    private List<int> ShownInThePark() =>
        [.. ((IEnumerable<ParkPokemon>)JSInterop.Invocations.Last(i => i.Identifier == "setPokemon").Arguments[0]!).Select(p => p.Id)];

    [Fact]
    public async Task Chooser_PutsPokemonInThePark_ThenOneIsClickedAndRemoved()
    {
        var ids = AddToBank("sacha", 25, 1, 4, 7);
        var cut = Render<Park>();
        cut.WaitForText("Le parc est vide.");
        // Le parc démarre dans le navigateur avec la carte de la Forêt, sans agrandissement ; le propriétaire déplace ses Pokémon
        cut.WaitForAssertion(() => Assert.Equal([("parc-1", 1, true)], Starts()));
        Assert.Empty(ShownInThePark());

        await cut.Click("Choisir des Pokémon");
        cut.WaitForText("Pokémon du parc");
        var count = cut.FindAll(".park-candidates .slot").Count;
        Assert.Equal(ids.Count, count);
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
        Assert.Equal(members, ShownInThePark()); // envoyés au parc du navigateur
        var outside = ids.Except(members).First();
        ids = [members[0]];
        await cut.Instance.OnPokemonClicked(ids[0]);
        cut.WaitForText("Retirer du parc");
        await cut.Click("Fermer");
        await cut.Instance.OnPokemonClicked(outside); // pas dans le parc : rien
        Assert.False(cut.HasButton("Retirer du parc"));
        await cut.Instance.OnPokemonClicked(ids[0]);
        await cut.Find(".collection-backdrop").ClickAsync(new());
        Assert.False(cut.HasButton("Retirer du parc")); // clic à côté : fiche fermée
        await cut.Instance.OnPokemonClicked(ids[0]);
        await cut.Click("Retirer du parc");
        cut.WaitForText("a quitté le parc");
        Assert.Contains($"{count - 2} / 20", cut.Text());
        Assert.DoesNotContain(ids[0], ShownInThePark());

        // Vider le parc : confirmation, annulation (clic à côté, « Annuler » : rien ne part), puis pour de bon
        await cut.Click("Vider le parc");
        cut.WaitForText("Vider le parc ?");
        await cut.Find(".dialog-backdrop").ClickAsync(new());
        Assert.DoesNotContain("Vider le parc ?", cut.Text());
        await cut.Click("Vider le parc");
        await cut.Buttons("Annuler")[0].ClickAsync(new());
        Assert.DoesNotContain("Vider le parc ?", cut.Text());
        Assert.Equal(count - 2, (await Parks.ListAsync("sacha", parkId)).Count);
        await cut.Click("Vider le parc");
        await cut.Find(".dialog-panel .btn-danger").ClickAsync(new());
        cut.WaitForText("Le parc est vide.");
        Assert.Empty(ShownInThePark());
        await cut.Find(".alert .btn-close").ClickAsync(new());
        Assert.Empty(cut.FindAll(".alert"));
    }

    [Fact]
    public async Task Chooser_KeepsTheChoiceOpen_WhenTheServerRefusesIt()
    {
        // Un Pokémon coché disparaît de la collection avant « Valider » (sauvegarde mise à jour dans un autre onglet)
        var ids = AddToBank("sacha", 25, 1);
        var cut = Render<Park>();
        await cut.Click("Choisir les Pokémon");
        cut.WaitForText("Pokémon du parc");
        await cut.ClickOn(".park-candidates .slot", 0);
        await cut.ClickOn(".park-candidates .slot", 1);
        using (var db = Db.NewContext())
            await db.BankPokemon.Where(p => p.Id == ids[0]).ExecuteDeleteAsync();
        await cut.Click("Valider");
        cut.WaitForText("Seuls tes Pokémon (pas les œufs) peuvent aller au parc.");
        Assert.NotEmpty(cut.FindAll(".park-candidates")); // la fenêtre reste ouverte, rien n'a changé
        Assert.Contains("· 2 / 20", cut.Text());
        Assert.Empty(await Parks.ListAsync("sacha", (await Parks.ListParksAsync("sacha"))[0].Id));
        Assert.Empty(ShownInThePark());
    }

    [Fact]
    public async Task Chooser_FiltersWithTheChips_LikeMyCollection()
    {
        AddToBank("sacha", 25, 152); // Pikachu (1re génération), Germignon (2e)
        var cut = Render<Park>();
        await cut.Click("Choisir les Pokémon");
        cut.WaitForText("Pokémon du parc");
        Assert.Equal(2, cut.FindAll(".park-candidates .slot").Count);

        await cut.Click("+ Filtre");
        await cut.ClickOn(c => c.FindAll(".filter-chip-add .dropdown-item").Single(i => i.TextContent.Contains("Génération")));
        await cut.ChangeOn(".filter-chip.open .filter-popover input[type=checkbox]", true); // la première : 1re génération
        cut.WaitForAssertion(() => Assert.Equal("Pikachu", Assert.Single(cut.FindAll(".park-candidates .slot")).GetAttribute("title")!.Split('(', ')')[1]));
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
    public async Task Chooser_GreysThePokemonThatCannotCome_AtTheEnd()
    {
        // Tissenboule (n° 917) n'a pas encore de sprites Donjon Mystère ; l'océan ne prend que ceux qui nagent.
        AddToBank("sacha", 917, 25, 7);
        GiveMoney("sacha", ParkService.ParkPrice);
        var ocean = await Parks.BuyAsync("sacha", ParkLandscape.Ocean);
        Assert.True(ocean.Ok, ocean.Message);
        var cut = Render<Park>();
        string[] Shown() => [.. cut.FindAll(".park-candidates .slot").Select(slot => slot.GetAttribute("title")!.Split('(', ')')[1])];
        string[] Locked() => [.. cut.FindAll(".park-candidates .slot.locked").Select(slot => slot.GetAttribute("title")!.Split('(', ')')[1])];

        // Forêt : Tissenboule grisé, à la fin
        await cut.Click("Choisir les Pokémon");
        cut.WaitForText("Pokémon du parc");
        Assert.Equal("Tissenboule", Shown()[^1]);
        Assert.Equal(["Tissenboule"], Locked());
        Assert.Contains("pas encore de sprite", cut.Find(".park-candidates .slot.locked").GetAttribute("title"));
        await cut.Find(".park-candidates .slot.locked").ClickAsync(new());
        cut.WaitForText("Tissenboule n'a pas encore de sprites Donjon Mystère");
        Assert.Contains("· 0 / 20", cut.Text());
        await cut.Click("Annuler");

        // Océan : seul Carapuce peut venir, en tête ; les deux autres grisés après lui
        await cut.Find("select.park-choice").ChangeAsync(new() { Value = ocean.ParkId.ToString() });
        await cut.Click("Choisir les Pokémon");
        cut.WaitForText("Pokémon du parc");
        Assert.Equal("Carapuce", Shown()[0]);
        Assert.Equal(["Pikachu", "Tissenboule"], Locked().Order());
        Assert.Empty(cut.FindAll(".park-candidates .slot.locked ~ .slot:not(.locked)")); // aucun Pokémon permis après un grisé
        Assert.Equal(3, Shown().Length);
        // Clic sur un grisé par le paysage : la raison, et il n'est pas choisi
        await cut.ClickOn(c => c.FindAll(".park-candidates .slot.locked").Single(s => s.GetAttribute("title")!.Contains("Pikachu")));
        cut.WaitForText("Pikachu ne nage pas : seuls les Pokémon qui nagent (type Eau) peuvent vivre dans l'océan.");
        Assert.Contains("· 0 / 20", cut.Text());
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
        // Redémarré avec la carte de la taille suivante
        cut.WaitForAssertion(() => Assert.Equal([("parc-1", 1, true), ("parc-1", 2, true)], Starts()));
        Assert.Equal(1, JSInterop.Invocations.Count(i => i.Identifier == "dispose")); // l'ancien parc arrêté avant
        await cut.Click("Agrandir le parc");
        await cut.Find(".dialog-backdrop").ClickAsync(new());
        Assert.False(cut.HasButton($"Payer {InventoryService.FormatMoney(ParkService.ExpansionPrice)}")); // fenêtre fermée, rien payé
        Assert.Equal(2 * ParkService.ExpansionPrice, await Services.GetRequiredService<InventoryService>().GetBalanceAsync("sacha"));

        await cut.Click("Acheter un parc");
        Assert.True(cut.Button("Payer").HasAttribute("disabled")); // aucun paysage choisi
        await cut.Find("#landscape-" + (int)ParkLandscape.Beach).ChangeAsync(new() { Value = true });
        await cut.Click("Payer");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("select.park-choice")));
        Assert.Equal(ParkService.ExpansionPrice, await Services.GetRequiredService<InventoryService>().GetBalanceAsync("sacha"));
        cut.WaitForAssertion(() => Assert.Equal(("plage-1", 1, true), Starts()[^1])); // la plage, sans agrandissement
        var forest = (await Parks.ListParksAsync("sacha")).Single(p => p.Landscape == ParkLandscape.Forest).Id;
        await cut.Find("select.park-choice").ChangeAsync(new() { Value = forest.ToString() });
        cut.WaitForText("/ 40");
        Assert.Equal(("parc-1", 2, true), Starts()[^1]); // retour à la forêt, à sa taille
        // Valeur trafiquée dans la liste déroulante : rien ne change
        await cut.Find("select.park-choice").ChangeAsync(new() { Value = "pas un nombre" });
        Assert.Equal(4, Starts().Count);
        Assert.Contains("/ 40", cut.Text());

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
    public async Task AttackedPokemon_GetsItsMalus_InTheParkAndItsCard()
    {
        var ids = AddToBank("sacha", 133, 25); // Évoli, heureux dans la forêt
        var forest = (await Parks.ListParksAsync("sacha"))[0].Id;
        Assert.True((await Parks.SetAsync("sacha", forest, ids)).Ok);
        var cut = Render<Park>();
        cut.WaitForText("2 / 20");

        // Fiche ouverte pendant l'attaque (parc.js) : portrait et ligne du malus à jour
        await cut.Instance.OnPokemonClicked(ids[0]);
        cut.WaitForText("Retirer du parc");
        Assert.Empty(cut.FindAll(".park-malus"));
        var malus = await cut.Instance.OnPokemonAttacked(ids[0]);
        Assert.Equal(1, malus?.Levels);
        Assert.InRange(malus!.Seconds, 895, 900);
        cut.WaitForAssertion(() => Assert.Contains("Attaqué : neutre au lieu d'heureux encore 15 min.", cut.Find(".park-malus").TextContent));
        Assert.Equal(2, (await cut.Instance.OnPokemonAttacked(ids[0]))?.Levels);
        cut.WaitForAssertion(() => Assert.Contains("malheureux au lieu d'heureux encore 30 min", cut.Find(".park-malus").TextContent));

        // Fiche rouverte plus tard, ou d'un autre Pokémon
        await cut.Click("Fermer");
        await cut.Instance.OnPokemonClicked(ids[0]);
        cut.WaitForText("Attaqué : malheureux");
        Assert.Null(await cut.Instance.OnPokemonAttacked(999_999)); // pas dans le parc
        await cut.Click("Fermer");
        Assert.NotNull(await cut.Instance.OnPokemonAttacked(ids[1])); // fiche fermée : rien à mettre à jour
        await cut.Instance.OnPokemonClicked(ids[1]);
        cut.WaitForText("Attaqué :");
    }

    [Fact]
    public async Task Visitor_WatchesAPublicPark_WithoutChanging()
    {
        var ids = AddToBank("ondine", 7);
        Assert.True((await Parks.SetAsync("ondine", (await Parks.ListParksAsync("ondine"))[0].Id, ids)).Ok);
        var cut = Render<Park>(p => p.Add(x => x.Pseudo, "ondine"));
        cut.WaitForText("Les Pokémon de ondine vivent ici");
        Assert.False(cut.HasButton("Choisir les Pokémon"));
        // Son parc à elle, où le visiteur ne peut rien déplacer
        cut.WaitForAssertion(() => Assert.Equal([("parc-1", 1, false)], Starts()));
        Assert.Equal(ids, ShownInThePark());
        Assert.Null(await cut.Instance.OnPokemonAttacked(ids[0])); // les attaques chez un visiteur ne comptent pas
        Assert.Null(await Parks.GetMalusAsync("ondine", (await Parks.ListParksAsync("ondine"))[0].Id, ids[0]));
        await cut.Instance.OnPokemonClicked(ids[0]);
        cut.WaitForText("Fermer");
        Assert.False(cut.HasButton("Retirer du parc"));

        UpdateUser("pierre", u => u.Audience = ProfileAudience.Nobody);
        var hidden = Render<Park>(p => p.Add(x => x.Pseudo, "pierre"));
        hidden.WaitForAssertion(() => Assert.DoesNotContain("vivent ici", hidden.Text()));
    }
}
