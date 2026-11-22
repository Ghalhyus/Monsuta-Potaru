using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MonsutaPotaru.Components.Pages;
using MonsutaPotaru.Data;
using MonsutaPotaru.Services;

namespace MonsutaPotaru.Tests;

/// <summary>
/// Page « GTS » : dépôt depuis la collection (grisés, critères), recherche, échange par un autre joueur, boîte de réception
/// tenue à jour en direct, récupération ; et l'annonce des offres dans la confirmation de suppression d'une sauvegarde.
/// </summary>
public sealed class GtsPageTests : PageTestContext
{
    private GtsService Gts => Services.GetRequiredService<GtsService>();

    private static Task Slot<T>(IRenderedComponent<T> cut, string name) where T : IComponent =>
        cut.FindAll(".collection-picker .slot").First(s => (s.GetAttribute("title") ?? "").Contains(name)).ClickAsync(new());

    [Fact]
    public async Task Deposit_Search_Accept_AndRetrieve()
    {
        // Sacha dépose le Kadabra de sa Noire 2 contre un Machopeur.
        Assert.True((await StoredSaves.AddAsync("sacha", "b2.sav", TradeServiceTests.Bw2("Sacha", 1, [25], 64))).Ok);
        var cut = Render<Gts>();
        cut.WaitForText("Aucune offre en ligne.");
        Assert.Contains("0 / 5", cut.Text());
        await cut.Click("Déposer un Pokémon");
        cut.WaitForText("1. Choisis le Pokémon à déposer");
        Assert.True(cut.Button("Valider").HasAttribute("disabled"));
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".collection-picker .slot").Count));
        await cut.Find(".collection-toolbar input").InputAsync(new ChangeEventArgs { Value = "kada" }); // la recherche de « Ma collection »
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".collection-picker .slot")));
        await Slot(cut, "Kadabra");
        await cut.Click("Valider");

        cut.WaitForText("2. Choisis le Pokémon demandé");
        Assert.Contains("Jeu de génération 5 — Sacha · pour : Noire, Blanche, Noire 2, Blanche 2", cut.Text());
        Assert.Contains("ton jeu l'a encore", cut.Text());
        await cut.Click("Déposer");
        cut.WaitForText("Choisis le Pokémon que tu demandes en échange.");
        await cut.Find("#gts-wanted-species").InputAsync(new ChangeEventArgs { Value = "machopeur" });
        await cut.FindAll("input.gts-level")[0].ChangeAsync(new ChangeEventArgs { Value = "20" });
        await cut.Click("Déposer");
        cut.WaitForText("Kadabra est sur la GTS jusqu'au");
        Assert.Contains("1 / 5", cut.Text());
        Assert.Contains("Machopeur, N.20 ou plus", cut.Text());

        // Ondine le trouve, avec un Machopeur N.30 du stockage interne.
        using (var db = Db.NewContext())
        {
            db.BankPokemon.Add(BankService.CreateEntry(new PokemonInfo(), SaveSessionTests.CreatePk5(67, 30, "ondine", 1), "ondine", DateTime.UtcNow));
            db.SaveChanges();
        }
        UpdateUser("ondine", u => u.UserName = "Ondine");
        UpdateUser("sacha", u => u.UserName = "Sacha");
        var hers = RenderAs<Gts>("ondine");
        hers.WaitForText("Tu as ce Pokémon");
        Assert.Contains("De Sacha", hers.Text());
        await hers.Find("#gts-search-species").InputAsync(new ChangeEventArgs { Value = "Pikachou" });
        await hers.Click("Chercher");
        hers.WaitForText("« Pikachou » n'est pas un Pokémon");
        await hers.Find("#gts-search-species").InputAsync(new ChangeEventArgs { Value = "Kadabra" });
        await hers.Click("Chercher");
        hers.WaitForText("1 offre.");
        await hers.Click("Échanger");
        hers.WaitForText("Choisis le Pokémon que tu donnes");
        await hers.FindAll(".trade-candidate").Single().ClickAsync(new());
        await hers.Click("Valider");
        hers.WaitForText("Kadabra évoluera en Alakazam en arrivant dans ton stockage interne.");
        Assert.DoesNotContain("ton jeu n'a pas encore", hers.Text()); // rien à télécharger : stockage interne
        await hers.Click("Échanger");
        hers.WaitForText("Échange réussi : tu as reçu Kadabra, qui a évolué en Alakazam dans ton stockage interne.");
        Assert.Contains("Félicitations ! Ton Kadabra a évolué en Alakazam !", hers.Find(".trade-show").TextContent);
        await hers.Click("Passer");
        Assert.Empty(hers.FindAll(".trade-show"));

        // La page de Sacha se met à jour : le Machopeur l'attend ; il le récupère dans le stockage interne.
        cut.WaitForText("Boîte de réception");
        Assert.Contains("Échange avec Ondine : tu as reçu Machopeur, qui a évolué en Mackogneur contre ton Kadabra.", cut.Text());
        Assert.Contains("0 / 5", cut.Text());
        Assert.Empty(cut.FindAll(".trade-show")); // l'animation attend la récupération
        await cut.Click("Récupérer");
        cut.WaitForText("Mackogneur est dans ton stockage interne.");
        var show = PageTestExtensions.Normalize(cut.Find(".trade-show").TextContent);
        Assert.Contains("Envoi de Kadabra à Ondine.", show);
        Assert.Contains("Ondine t'envoie Machopeur.", show);
        Assert.Contains("Félicitations ! Ton Machopeur a évolué en Mackogneur !", show);
        Assert.DoesNotContain("Boîte de réception", cut.Text());
    }

    [Fact]
    public async Task Picker_GraysEggs_AndCancelSendsToTheInbox()
    {
        var ids = AddToBank("sacha", 25, 133);
        using (var db = Db.NewContext())
        {
            db.BankPokemon.Single(p => p.Id == ids[1]).IsEgg = true;
            db.SaveChanges();
        }
        var cut = Render<Gts>();
        cut.WaitForText("Mes offres");
        await cut.Click("Déposer un Pokémon");
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".collection-picker .slot.locked")));
        Assert.Contains($"--columns: {Components.Shared.GridColumns.ForScreen(0)}", cut.Find(".collection-picker").GetAttribute("style")); // JavaScript simulé : largeur 0
        await cut.Find(".collection-columns input").InputAsync(new ChangeEventArgs { Value = "4" }); // le curseur, comme « Ma collection »
        Assert.Contains("--columns: 4", cut.Find(".collection-picker").GetAttribute("style"));
        await cut.Find(".collection-picker .slot.locked").ClickAsync(new());
        cut.WaitForText("Un œuf ne peut pas aller sur la GTS.");
        Assert.True(cut.Button("Valider").HasAttribute("disabled"));
        await cut.Click("Retour");
        await cut.Click("Déposer un Pokémon");
        cut.WaitForAssertion(() => Assert.Contains("--columns: 4", cut.Find(".collection-picker").GetAttribute("style"))); // gardé par la page
        await cut.Click("Retour");

        Assert.True((await Gts.DepositAsync("sacha", ids[0], new GtsWanted(1))).Ok);
        cut.Dispose();
        cut = Render<Gts>();
        cut.WaitForText("Bulbizarre");
        await cut.Click("Annuler");
        cut.WaitForText("Offre annulée");
        cut.WaitForText("Tu as annulé ton offre : Pikachu te revient.");
    }

    [Fact]
    public async Task Saves_DeleteConfirmation_AnnouncesTheGtsOffers()
    {
        var save = await StoreBw2("sacha");
        int pikachu;
        using (var db = Db.NewContext())
            pikachu = db.BankPokemon.Single(p => p.StoredSaveId == save && p.Species == 25).Id;
        Assert.True((await Gts.DepositAsync("sacha", pikachu, new GtsWanted(1))).Ok);
        var cut = Render<Saves>();
        await cut.Click("Supprimer");
        cut.WaitForText("Son offre de la GTS sera retirée (leur Pokémon ira dans ta boîte de réception).");
        await cut.Click("Télécharger et supprimer");
        cut.WaitForAssertion(() => Assert.Empty(Rows<StoredSave>()));
        Assert.Equal(GtsStatus.SaveDeleted, Assert.Single(Rows<GtsPokemon>()).Status);
    }
}
