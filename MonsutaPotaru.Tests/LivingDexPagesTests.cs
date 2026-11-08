using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MonsutaPotaru.Components.Pages;
using MonsutaPotaru.Data;
using MonsutaPotaru.Services;

namespace MonsutaPotaru.Tests;

/// <summary>Pages « Living dex » : liste (création, modes, tri) et un living dex (filtres, fiche, colonnes, suppression).</summary>
public sealed class LivingDexPagesTests : PageTestContext
{
    private LivingDexService Dexes => Services.GetRequiredService<LivingDexService>();
    private NavigationManager Navigation => Services.GetRequiredService<NavigationManager>();

    [Fact]
    public async Task List_CreatesTheGlobalLivingDex()
    {
        AddToBank("sacha", 25, 133);
        var cut = Render<LivingDexes>();
        cut.WaitForText("Tu n'as pas encore de living dex.");

        await cut.Click("+ Créer un living dex");
        cut.WaitForText("Nouveau living dex");
        Assert.True(cut.Button("Créer").HasAttribute("disabled"));
        await cut.Click("Annuler");
        await cut.Click("+ Créer un living dex");

        // Générations dépliées puis repliées ; un jeu sans sauvegarde
        foreach (var toggle in cut.FindAll(".livingdex-generation-toggle").Select((_, i) => i).ToList())
            await cut.FindAll(".livingdex-generation-toggle")[toggle].ClickAsync(new());
        await cut.FindAll("input[name=dexGame]")[2].ChangeAsync(new() { Value = true });
        cut.WaitForText("disponible");
        await cut.FindAll(".livingdex-generation-toggle")[0].ClickAsync(new());

        // Le global chromatique, puis le global
        await cut.FindAll("input[name=dexGame]")[1].ChangeAsync(new() { Value = true });
        cut.WaitForText("Seuls tes chromatiques comptent");
        await cut.FindAll("input[name=dexGame]")[0].ChangeAsync(new() { Value = true });
        cut.WaitForText("Les chromatiques n'y comptent pas");
        await cut.Click("Créer");
        Assert.Contains("livingdex/", Navigation.Uri);
        Assert.Single(await Dexes.ListAsync("sacha"));
    }

    [Fact]
    public async Task List_CreatesAGameLivingDex_OnAStoredSave()
    {
        var saveId = await StoreReal("sacha", "noir.sav");
        var cut = Render<LivingDexes>();
        await cut.Click("+ Créer un living dex");
        var candidate = (await Dexes.ListCandidatesAsync("sacha")).Single();
        var game = candidate.Games.First();
        var generation = LivingDexGames.Get(game).Generation;
        await cut.FindAll(".livingdex-generation-toggle")[generation - 1].ClickAsync(new());
        var radio = cut.FindAll(".livingdex-game").First(l => l.TextContent.Contains(LivingDexGames.Get(game).Name)).QuerySelector("input")!;
        await radio.ChangeAsync(new() { Value = true });
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#dexSave")));
        await cut.Find("#dexSave").ChangeAsync(new() { Value = saveId.ToString() });
        await cut.Find("#dexSave").ChangeAsync(new() { Value = "pas un nombre" });
        await cut.Find("#dexSave").ChangeAsync(new() { Value = saveId.ToString() });
        await cut.Click("Créer");
        Assert.Contains("livingdex/", Navigation.Uri);
    }

    [Fact]
    public async Task List_ShowsEachMode_AndSort()
    {
        AddToBank("sacha", 25, 133);
        await Dexes.CreateGlobalAsync("sacha");
        await Dexes.CreateGlobalAsync("sacha", shiny: true);
        await StoreReal("sacha", "noir.sav");
        var candidate = (await Dexes.ListCandidatesAsync("sacha")).Single();
        Assert.True((await Dexes.CreateAsync("sacha", candidate.SaveId, candidate.Games.First())).Ok);

        foreach (var query in new[] { "", "?mode=regional", "?mode=complet", "?mode=ultime&tri=completion&ordre=desc" })
        {
            Navigation.NavigateTo("livingdex" + query);
            var cut = Render<LivingDexes>();
            cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".livingdex-card")));
            await cut.FindAll(".livingdex-choice button")[0].ClickAsync(new());
            await cut.Find("#dexSort").ChangeAsync(new() { Value = "completion" });
            await cut.Find(".livingdex-choice button[aria-label]").ClickAsync(new());
        }
        // La fenêtre de création marque les jeux déjà pris
        var list = Render<LivingDexes>();
        await list.Click("+ Créer un living dex");
        list.WaitForText("(déjà créé)");
    }

    [Fact]
    public async Task List_CreationRefused_SaysWhy()
    {
        var cut = Render<LivingDexes>();
        await cut.Click("+ Créer un living dex");
        await Dexes.CreateGlobalAsync("sacha"); // créé entre-temps (autre onglet)
        await cut.FindAll("input[name=dexGame]")[0].ChangeAsync(new() { Value = true });
        await cut.Click("Créer");
        cut.WaitForAssertion(() => Assert.Contains("alert-danger", cut.Markup));
        await cut.Find(".alert .btn-close").ClickAsync(new());
    }

    [Fact]
    public async Task Page_FiltersCardColumnsAndDelete()
    {
        AddToBank("sacha", 25, 133);
        var id = (await Dexes.CreateGlobalAsync("sacha")).Id!.Value;
        var cut = Render<LivingDexPage>(p => p.Add(x => x.Id, id));
        cut.WaitForText("Pokémon de ton stockage interne");

        // Fiche d'un Pokémon possédé
        await cut.Find("button.livingdex-slot").ClickAsync(new());
        cut.WaitForText("Fermer");
        await cut.Click("Fermer");
        await cut.Find("button.livingdex-slot").ClickAsync(new());
        await cut.Find(".collection-backdrop").ClickAsync(new());

        // Modes et filtres (dans l'adresse), colonnes
        await cut.Buttons("Ultime")[0].ClickAsync(new());
        await cut.Buttons("Manquants")[0].ClickAsync(new());
        await cut.Buttons("Tous")[0].ClickAsync(new());
        await cut.Find(".collection-columns input").InputAsync(new() { Value = "5" });
        await cut.Find(".collection-columns input").ChangeAsync(new() { Value = "5" });
        await cut.Instance.OnScreenSizeChanged(1);

        // Suppression : confirmation, annulation, puis pour de bon
        await cut.Click("Supprimer ce living dex");
        cut.WaitForText("Le living dex sera supprimé");
        await cut.Click("Annuler");
        await cut.Click("Supprimer ce living dex");
        await cut.Find(".alert .btn-danger").ClickAsync(new());
        Assert.EndsWith("livingdex", Navigation.Uri);
        Assert.Empty(await Dexes.ListAsync("sacha"));
        await DisposeComponentsAsync();
    }

    [Fact]
    public async Task Page_FromTheAddress_AndForAGame()
    {
        var shiny = (await Dexes.CreateGlobalAsync("sacha", shiny: true)).Id!.Value;
        foreach (var query in new[] { "?filtre=manquants&mode=ultime", "?filtre=possedes&colonnes=8" })
        {
            Navigation.NavigateTo($"livingdex/{shiny}{query}");
            var page = Render<LivingDexPage>(p => p.Add(x => x.Id, shiny));
            page.WaitForText("chromatiques");
        }

        await StoreReal("sacha", "noir.sav");
        var candidate = (await Dexes.ListCandidatesAsync("sacha")).Single();
        var id = (await Dexes.CreateAsync("sacha", candidate.SaveId, candidate.Games.First())).Id!.Value;
        foreach (var query in new[] { "?mode=regional", "?filtre=possedes", "?filtre=manquants" })
        {
            Navigation.NavigateTo($"livingdex/{id}{query}");
            var cut = Render<LivingDexPage>(p => p.Add(x => x.Id, id));
            cut.WaitForText("Sauvegarde :");
            if (cut.FindAll("button.livingdex-slot").Count > 0)
            {
                await cut.Find("button.livingdex-slot").ClickAsync(new());
                cut.WaitForText("Fermer");
            }
        }

        var missing = Render<LivingDexPage>(p => p.Add(x => x.Id, 9999));
        missing.WaitForText("Ce living dex n'existe plus.");
    }

    [Fact]
    public async Task Visitor_SeesPublicLivingDexes()
    {
        AddToBank("ondine", 120);
        var id = (await Dexes.CreateGlobalAsync("ondine")).Id!.Value;
        var list = Render<LivingDexes>(p => p.Add(x => x.Pseudo, "ondine"));
        list.WaitForAssertion(() => Assert.NotEmpty(list.FindAll(".livingdex-card")));
        Assert.False(list.HasButton("+ Créer un living dex"));
        var page = Render<LivingDexPage>(p => p.Add(x => x.Id, id).Add(x => x.Pseudo, "ondine"));
        page.WaitForText("du stockage interne de ondine");
        Assert.False(page.HasButton("Supprimer ce living dex"));

        var none = Render<LivingDexes>(p => p.Add(x => x.Pseudo, "pierre"));
        none.WaitForText("pierre n'a pas encore de living dex.");
        UpdateUser("pierre", u => u.Audience = ProfileAudience.Nobody);
        var hidden = Render<LivingDexes>(p => p.Add(x => x.Pseudo, "pierre"));
        hidden.WaitForAssertion(() => Assert.DoesNotContain("living dex", hidden.Text()));
        var hiddenPage = Render<LivingDexPage>(p => p.Add(x => x.Id, id).Add(x => x.Pseudo, "pierre"));
        hiddenPage.WaitForAssertion(() => Assert.DoesNotContain("Living dex", hiddenPage.Text()));
    }
}
