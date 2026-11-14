using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MonsutaPotaru.Components.Pages;
using MonsutaPotaru.Data;
using MonsutaPotaru.Services;
using PKHeX.Core;

namespace MonsutaPotaru.Tests;

/// <summary>Pages « Complétion (100 %) » : liste (création) et une complétion (sections, filtres, étapes ratées, suppression).</summary>
public sealed class CompletionPagesTests : PageTestContext
{
    private CompletionService Completions => Services.GetRequiredService<CompletionService>();
    private NavigationManager Navigation => Services.GetRequiredService<NavigationManager>();

    /// <summary>Complétion de Rouge sur la vraie sauvegarde de Rouge de ce compte.</summary>
    private async Task<int> RedCompletion(string owner = "sacha")
    {
        var saveId = await StoreReal(owner, "rouge.sav");
        var created = await Completions.CreateAsync(owner, saveId, LivingDexGame.Red);
        Assert.True(created.Ok, created.Message);
        return created.Id!.Value;
    }

    [Fact]
    public async Task List_CreatesACompletion_OnAStoredSave()
    {
        await StoreReal("sacha", "rouge.sav");
        var cut = Render<CompletionList>();
        cut.WaitForText("Tu n'as aucune sauvegarde de suivie.");

        await cut.Click("+ Ajouter une sauvegarde");
        cut.WaitForText("Nouvelle complétion");
        Assert.True(cut.Button("Créer").HasAttribute("disabled"));
        await cut.Click("Annuler");
        await cut.Click("+ Ajouter une sauvegarde");

        // Générations repliées au départ (9, de Rouge à Écarlate / Violet : 37 jeux), dépliées une à une par leur titre
        Assert.Empty(cut.FindAll("input[name=completionGame]"));
        Assert.Equal(9, cut.FindAll(".livingdex-generation-toggle").Count);
        Assert.Equal("▸1re génération (3 jeux)", cut.FindAll(".livingdex-generation-toggle")[0].TextContent.Trim());
        foreach (var toggle in Enumerable.Range(0, 9))
            await cut.FindAll(".livingdex-generation-toggle")[toggle].ClickAsync(new());
        Assert.Equal(37, cut.FindAll("input[name=completionGame]").Count);

        // Jaune : pas de sauvegarde ; Rouge : la sauvegarde proposée d'office
        await cut.FindAll("input[name=completionGame]")[2].ChangeAsync(new() { Value = true });
        cut.WaitForText("Aucune sauvegarde de Pokémon Jaune disponible");
        await cut.FindAll("input[name=completionGame]")[0].ChangeAsync(new() { Value = true });
        cut.WaitForAssertion(() => Assert.Contains("rouge.sav", cut.Find("#completionSave").TextContent));
        // Repliée, la 1re génération montre encore le jeu choisi
        await cut.FindAll(".livingdex-generation-toggle")[0].ClickAsync(new());
        Assert.Equal(35, cut.FindAll("input[name=completionGame]").Count);
        Assert.Equal("false", cut.FindAll(".livingdex-generation-toggle")[0].GetAttribute("aria-expanded"));
        await cut.Find("#completionSave").ChangeAsync(new() { Value = "0" });
        Assert.True(cut.Button("Créer").HasAttribute("disabled"));
        await cut.Find("#completionSave").ChangeAsync(new() { Value = cut.Find("#completionSave option").GetAttribute("value") });
        await cut.Click("Créer");
        Assert.Contains("completion/", Navigation.Uri);

        // La carte : progression globale et par section ; Rouge est grisé à la création suivante
        var list = Render<CompletionList>();
        list.WaitForText("35,8 %");
        Assert.Contains("Histoire 64,7 %", list.Text());
        Assert.Contains("rouge.sav", list.Text());
        await list.Click("+ Ajouter une sauvegarde");
        await list.FindAll(".livingdex-generation-toggle")[0].ClickAsync(new());
        list.WaitForText("Pokémon Rouge (déjà créée)");
        Assert.Equal("Tu as déjà cette complétion", list.Find("label.taken").GetAttribute("title"));
    }

    [Fact]
    public async Task List_CreationRefused_SaysWhy()
    {
        var saveId = await StoreReal("sacha", "rouge.sav");
        var cut = Render<CompletionList>();
        await cut.Click("+ Ajouter une sauvegarde");
        await cut.FindAll(".livingdex-generation-toggle")[0].ClickAsync(new());
        await cut.FindAll("input[name=completionGame]")[1].ChangeAsync(new() { Value = true }); // Bleu
        cut.WaitForAssertion(() => Assert.False(cut.Button("Créer").HasAttribute("disabled")));
        await Completions.CreateAsync("sacha", saveId, LivingDexGame.Red); // créée entre-temps (autre onglet)
        await cut.Click("Créer");
        cut.WaitForText("Cette sauvegarde a déjà une complétion.");
        await cut.Find(".alert .btn-close").ClickAsync(new());
        Assert.DoesNotContain("alert-danger", cut.Markup);
    }

    [Fact]
    public async Task Page_SectionsFiltersAndDelete()
    {
        var id = await RedCompletion();
        var cut = Render<CompletionPage>(p => p.Add(x => x.Id, id));
        cut.WaitForText("Sauvegarde :");
        Assert.Contains("35,8 %", cut.Text());
        Assert.Equal(["Histoire", "Pokédex", "Dresseurs", "Objets", "Rencontres uniques"],
            cut.FindAll(".completion-section strong").Select(s => s.TextContent));
        // Histoire (par défaut) : les étapes groupées par lieu, faites ou à faire
        Assert.Contains("Badge Prisme (Érika)", cut.Text());
        Assert.Contains("Argenta 1 / 1", cut.Text());
        Assert.NotEmpty(cut.FindAll(".completion-step.done"));
        Assert.NotEmpty(cut.FindAll(".completion-step.missing"));

        // Boutons : section et filtre dans l'adresse
        await cut.Click("Dresseurs");
        Assert.Contains("section=dresseurs", Navigation.Uri);
        await cut.Click("Manquantes");
        Assert.Contains("filtre=manquants", Navigation.Uri);
        await cut.Click("Histoire");
        Assert.DoesNotContain("section=", Navigation.Uri);
        await cut.Click("Toutes");
        Assert.DoesNotContain("filtre=", Navigation.Uri);

        // Suppression : confirmation, annulation, puis pour de bon
        await cut.Click("Supprimer cette complétion");
        cut.WaitForText("La complétion sera supprimée");
        await cut.Click("Annuler");
        await cut.Click("Supprimer cette complétion");
        await cut.Find(".alert .btn-danger").ClickAsync(new());
        Assert.EndsWith("completion", Navigation.Uri);
        Assert.Empty(await Completions.ListAsync("sacha"));
    }

    [Theory]
    [InlineData("?section=pokedex", "#025")]
    [InlineData("?section=pokedex&filtre=faits", "#0")]
    [InlineData("?section=dresseurs&filtre=faits", "Rattata, Abo (niv. 11)")]
    [InlineData("?section=objets&filtre=manquants", "CS04 Force")]
    [InlineData("?section=rencontres", "Canarticho (contre Piafabec)")]
    public async Task Page_FromTheAddress(string query, string expected)
    {
        var id = await RedCompletion();
        Navigation.NavigateTo($"completion/{id}{query}");
        var cut = Render<CompletionPage>(p => p.Add(x => x.Id, id));
        cut.WaitForText(expected);
        if (query.Contains("pokedex"))
            Assert.Equal(query.Contains("faits") ? 47 : 151, cut.FindAll(".completion-dex .livingdex-slot img[data-remplacement]").Count);
    }

    [Fact]
    public async Task Page_SecondGeneration_SixSectionsAndUnown()
    {
        var saveId = await StoreReal("sacha", "argent.sav");
        var id = (await Completions.CreateAsync("sacha", saveId, LivingDexGame.Silver)).Id!.Value;
        Navigation.NavigateTo($"completion/{id}?section=quetes");
        var cut = Render<CompletionPage>(p => p.Add(x => x.Id, id));
        cut.WaitForText("Pokémon Argent");
        Assert.Contains("Chaque section compte pour un sixième du total.", cut.Text());
        Assert.Equal(6, cut.FindAll(".completion-section").Count);
        Assert.Contains("Capturer Zarbi Z", cut.Text());
        // Chaque Zarbi a l'image de sa lettre (sprites d'Argent)
        Assert.Contains(cut.FindAll(".completion-step img"), i => i.GetAttribute("src") == "images/livingdex/argent/201-z.png");
    }

    [Fact]
    public async Task Page_MissedSteps_AndAnUnreadableSave()
    {
        // Rouge, Giovanni vaincu à la Sylphe SARL sans les Rockets : 30 Rockets ratés (la sauvegarde modifiée en mémoire)
        var data = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Sauvegardes", "rouge.sav"));
        Assert.True(SaveUtil.TryGetSaveFile(data, out var sav, "rouge.sav"));
        var giovanni = Gen1CompletionData.RedBlue.Single(r => r.Label == "Libérer la Sylphe SARL (Giovanni vaincu)");
        ((SAV1)sav!).SetEventFlag(int.Parse(giovanni.Done[2..]), true);
        Assert.True((await StoredSaves.AddAsync("sacha", "rouge.sav", sav.Write().ToArray())).Ok);
        var saveId = (await StoredSaves.ListAsync("sacha")).Single().Id;
        var id = (await Completions.CreateAsync("sacha", saveId, LivingDexGame.Red)).Id!.Value;

        Navigation.NavigateTo($"completion/{id}?section=dresseurs&filtre=manquants");
        var cut = Render<CompletionPage>(p => p.Add(x => x.Id, id));
        cut.WaitForText("30 ratés");
        Assert.Equal(30, cut.FindAll(".completion-step.missed").Count);
        Assert.Contains("raté", cut.Find(".completion-step.missed .badge").TextContent);

        // Sauvegarde illisible : la page le dit, sans étapes
        using (var db = Db.NewContext())
        {
            db.StoredSaves.Single(s => s.Id == saveId).Data = new byte[16];
            db.SaveChanges();
        }
        Navigation.NavigateTo($"completion/{id}?filtre=faits");
        var broken = Render<CompletionPage>(p => p.Add(x => x.Id, id));
        broken.WaitForText("ne se lit plus");
        Assert.Contains("Rien de fait dans cette section", broken.Text());
        Navigation.NavigateTo($"completion/{id}?filtre=manquants");
        var done = Render<CompletionPage>(p => p.Add(x => x.Id, id));
        done.WaitForText("Section Histoire terminée");

        var missing = Render<CompletionPage>(p => p.Add(x => x.Id, 9999));
        missing.WaitForText("Cette complétion n'existe plus.");
    }

    /// <summary>Noms des jeux des cartes, dans l'ordre affiché.</summary>
    private static List<string> CardGames(IRenderedComponent<CompletionList> cut) =>
        [.. cut.FindAll(".livingdex-card strong").Select(s => s.TextContent)];

    [Fact]
    public async Task List_Sort_ByGenerationOrCompletion()
    {
        await RedCompletion();
        var silver = await StoreReal("sacha", "argent.sav");
        Assert.True((await Completions.CreateAsync("sacha", silver, LivingDexGame.Silver)).Ok);
        var y = await StoreReal("sacha", "y.sav");
        Assert.True((await Completions.CreateAsync("sacha", y, LivingDexGame.Y)).Ok);
        var listed = await Completions.ListAsync("sacha");
        var byGame = listed.Select(c => c.Game.Name).ToList();
        var byCompletion = CompletionGames.Sort(listed, CompletionSort.Completion, false).Select(c => c.Game.Name).ToList();
        Assert.NotEqual(byGame, byCompletion); // sinon le test ne prouve rien
        for (var i = 1; i < listed.Count; i++)
            Assert.True(CompletionProgress.CompareAverages(listed.Single(c => c.Game.Name == byCompletion[i - 1]).Progress,
                listed.Single(c => c.Game.Name == byCompletion[i]).Progress) <= 0);

        // Par génération (le tri d'origine), puis l'inverse ; par complétion dans les deux sens
        var cut = Render<CompletionList>();
        cut.WaitForAssertion(() => Assert.Equal(byGame, CardGames(cut)));
        Assert.Equal("Ordre croissant", cut.Find(".livingdex-choice button[aria-label]").GetAttribute("aria-label"));
        await cut.Find(".livingdex-choice button[aria-label]").ClickAsync(new());
        Assert.Contains("tri=generation&ordre=desc", Navigation.Uri);
        var descending = Render<CompletionList>();
        descending.WaitForAssertion(() => Assert.Equal(Enumerable.Reverse(byGame), CardGames(descending)));
        await descending.Find("#completionSort").ChangeAsync(new() { Value = "completion" });
        Assert.Contains("tri=completion&ordre=desc", Navigation.Uri);
        var bestFirst = Render<CompletionList>();
        bestFirst.WaitForAssertion(() => Assert.Equal(Enumerable.Reverse(byCompletion), CardGames(bestFirst)));
        Navigation.NavigateTo("completion?tri=completion");
        var worstFirst = Render<CompletionList>();
        worstFirst.WaitForAssertion(() => Assert.Equal(byCompletion, CardGames(worstFirst)));

        // Tri du compte : appliqué sans tri dans l'adresse, et pas écrit quand on y revient
        SetSorts("sacha", new SortPreferences { Completion = new(CompletionSort.Completion, true) });
        Navigation.NavigateTo("completion");
        var fromAccount = Render<CompletionList>();
        fromAccount.WaitForAssertion(() => Assert.Equal(Enumerable.Reverse(byCompletion), CardGames(fromAccount)));
        await fromAccount.Find("#completionSort").ChangeAsync(new() { Value = "generation" });
        Assert.Contains("tri=generation&ordre=desc", Navigation.Uri);
        Navigation.NavigateTo("completion?tri=generation&ordre=desc");
        var back = Render<CompletionList>();
        back.WaitForAssertion(() => Assert.NotEmpty(back.FindAll(".livingdex-card")));
        await back.Find("#completionSort").ChangeAsync(new() { Value = "completion" });
        Assert.DoesNotContain("tri=", Navigation.Uri);
    }

    [Fact]
    public void Sort_ComparesTheExactAverage_AndKeepsTheGameOrderOnTies()
    {
        static CompletionProgress Of(params (int Done, int Total)[] sections) =>
            new([.. sections.Select(s => new CompletionSectionProgress(CompletionSection.Story, s.Done, 0, s.Total))]);
        // 1 / 3 et 2 / 6 : égales ; 333 / 1000 : affichée « 33,3 % » aussi, mais moins
        var third = Of((1, 3), (1, 3));
        var same = Of((2, 6), (0, 0)); // une section vide ne compte pas
        var less = Of((333, 1000));
        Assert.Equal(0, CompletionProgress.CompareAverages(third, same));
        Assert.True(CompletionProgress.CompareAverages(less, third) < 0);
        Assert.Equal(third.Percent, less.Percent);
        Assert.Equal(0, CompletionProgress.CompareAverages(Of(), Of((0, 5))));

        static CompletionSummary Card(LivingDexGame game, CompletionProgress p) => new(0, LivingDexGames.Get(game), "", "", p);
        var cards = new[] { Card(LivingDexGame.Y, third), Card(LivingDexGame.Red, same), Card(LivingDexGame.Gold, less) };
        Assert.Equal([LivingDexGame.Gold, LivingDexGame.Red, LivingDexGame.Y],
            CompletionGames.Sort(cards, CompletionSort.Completion, false).Select(c => c.Game.Game));
        Assert.Equal([LivingDexGame.Red, LivingDexGame.Y, LivingDexGame.Gold],
            CompletionGames.Sort(cards, CompletionSort.Completion, true).Select(c => c.Game.Game));
        Assert.Equal([LivingDexGame.Y, LivingDexGame.Gold, LivingDexGame.Red],
            CompletionGames.Sort(cards, CompletionSort.Generation, true).Select(c => c.Game.Game));
    }

    [Fact]
    public async Task Visitor_SeesPublicCompletions()
    {
        var id = await RedCompletion("ondine");
        var list = Render<CompletionList>(p => p.Add(x => x.Pseudo, "ondine"));
        list.WaitForAssertion(() => Assert.NotEmpty(list.FindAll(".livingdex-card")));
        Assert.False(list.HasButton("+ Ajouter une sauvegarde"));
        Assert.DoesNotContain("rouge.sav", list.Text()); // pas de nom de fichier pour les autres
        Assert.Contains($"joueurs/ondine/completion/{id}", list.Find(".livingdex-card").GetAttribute("href"));
        var page = Render<CompletionPage>(p => p.Add(x => x.Id, id).Add(x => x.Pseudo, "ondine"));
        page.WaitForText("Sauvegarde :");
        Assert.DoesNotContain("rouge.sav", page.Text());
        Assert.False(page.HasButton("Supprimer cette complétion"));

        var none = Render<CompletionList>(p => p.Add(x => x.Pseudo, "pierre"));
        none.WaitForText("pierre n'a aucune sauvegarde de suivie.");
        UpdateUser("ondine", u => u.PublicPages = PublicPages.All & ~PublicPages.Completion); // page décochée
        var hidden = Render<CompletionList>(p => p.Add(x => x.Pseudo, "ondine"));
        hidden.WaitForAssertion(() => Assert.DoesNotContain("complétion", hidden.Text()));
        var hiddenPage = Render<CompletionPage>(p => p.Add(x => x.Id, id).Add(x => x.Pseudo, "ondine"));
        hiddenPage.WaitForAssertion(() => Assert.DoesNotContain("Sauvegarde", hiddenPage.Text()));
    }
}
