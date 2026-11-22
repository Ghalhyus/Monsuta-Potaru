using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using MonsutaPotaru.Components.Pages;
using MonsutaPotaru.Components.Shared;

namespace MonsutaPotaru.Tests;

/// <summary>
/// Infobulles d'aide (« ? ») : le composant, sa place dans l'en-tête d'une page et d'un bloc, et les pages dont l'aide est
/// passée du texte sous le titre à une infobulle.
/// </summary>
public sealed class HelpTipTests : PageTestContext
{
    /// <summary>L'infobulle de l'en-tête (ou du bloc) : un details dans la ligne du titre, l'aide dans sa bulle.</summary>
    private static IElement Tip<T>(IRenderedComponent<T> cut, string heading) where T : IComponent
    {
        var tip = cut.Find($"{heading} > details.help-tip");
        Assert.Equal("summary", tip.FirstElementChild!.LocalName);
        Assert.NotNull(tip.QuerySelector(".help-tip-bubble"));
        return tip;
    }

    /// <summary>Une aide lisible : de courts paragraphes ou des points de liste, jamais un long bloc de texte.</summary>
    private static void AssertReadable(IElement bubble)
    {
        var blocks = bubble.QuerySelectorAll("p, li").Select(e => e.TextContent).ToList();
        if (blocks.Count == 0)
            blocks.Add(bubble.TextContent);
        foreach (var block in blocks)
        {
            var text = System.Text.RegularExpressions.Regex.Replace(block.Trim(), @"\s+", " ");
            Assert.True(text.Length <= 260, $"Bloc trop long ({text.Length} caractères) : {text}");
        }
    }

    private static void AssertPageHelp<T>(IRenderedComponent<T> cut, string text) where T : IComponent
    {
        cut.WaitForAssertion(() => Assert.Contains(text, Tip(cut, ".page-header-heading").QuerySelector(".help-tip-bubble")!.TextContent));
        AssertReadable(cut.Find(".page-header-heading .help-tip-bubble"));
        Assert.Empty(cut.FindAll(".page-header-description")); // plus de texte d'aide affiché sous le titre
    }

    private static void AssertBlockHelp<T>(IRenderedComponent<T> cut, string title, string text) where T : IComponent
    {
        cut.WaitForText(title);
        var heading = cut.FindAll(".block-heading").Single(h => h.QuerySelector(".block-title")!.TextContent.Contains(title));
        var tip = heading.QuerySelector(":scope > details.help-tip");
        Assert.NotNull(tip);
        Assert.Contains(text, tip.QuerySelector(".help-tip-bubble")!.TextContent);
        AssertReadable(tip.QuerySelector(".help-tip-bubble")!);
        Assert.DoesNotContain(text, heading.ParentElement!.ParentElement!.QuerySelector(".block-body")!.TextContent);
    }

    [Fact]
    public void HelpTip_IsAQuestionMarkThatOpensItsBubble()
    {
        var cut = Render<HelpTip>(p => p.Add(x => x.Label, "Aide : Parc").AddChildContent("Tes Pokémon vivent ici."));

        var tip = cut.Find("details.help-tip");
        Assert.False(tip.HasAttribute("open")); // fermée au départ
        var summary = tip.QuerySelector("summary.help-tip-button")!;
        Assert.Equal("?", summary.QuerySelector("[aria-hidden=true]")!.TextContent);
        Assert.Equal("Aide : Parc", summary.QuerySelector(".visually-hidden")!.TextContent); // lu par un lecteur d'écran
        Assert.Equal("Tes Pokémon vivent ici.", tip.QuerySelector(".help-tip-bubble")!.TextContent);
    }

    [Fact]
    public void PageHeaderAndBlock_HaveATipOnlyWithHelp()
    {
        var header = Render<PageHeader>(p => p.Add(x => x.Title, "Parc").Add(x => x.Help, (RenderFragment)(b => b.AddContent(0, "Aide du parc"))));
        Assert.Equal("Aide : Parc", Tip(header, ".page-header-heading").QuerySelector(".visually-hidden")!.TextContent);
        Assert.NotNull(header.Find(".page-header-heading.help-tip-anchor > h1"));

        var plain = Render<PageHeader>(p => p.Add(x => x.Title, "Ma collection"));
        Assert.Empty(plain.FindAll("details.help-tip"));

        var block = Render<Block>(p => p.Add(x => x.Title, "Nouvel échange")
            .Add(x => x.Help, (RenderFragment)(b => b.AddContent(0, "Aide de l'échange")))
            .AddChildContent("Contenu"));
        Assert.Equal("Aide de l'échange", Tip(block, ".block-heading").QuerySelector(".help-tip-bubble")!.TextContent);
        Assert.Equal("Contenu", block.Find(".block-body").TextContent);

        var plainBlock = Render<Block>(p => p.Add(x => x.Title, "Demandes reçues"));
        Assert.Empty(plainBlock.FindAll("details.help-tip"));
    }

    [Fact]
    public void PagesOfTheMenu_HaveTheirHelpInATip()
    {
        AssertPageHelp(Render<Park>(), "Tes Pokémon vivent ici");
        AssertPageHelp(Render<InternalTrade>(), "comme avec un câble entre deux consoles");
        AssertPageHelp(Render<InventoryTransfer>(), "Fais passer des objets");
        AssertPageHelp(Render<LivingDexes>(), "Un exemplaire de chaque Pokémon d'un jeu");
        AssertPageHelp(Render<RibbonList>(), "Suis les rubans de tes Pokémon");
        AssertPageHelp(Render<PlayerSearch>(), "Tape une partie de son pseudo");
        AssertPageHelp(Render<Saves>(), "apparaissent dans « Ma collection » en lecture seule");
        AssertPageHelp(Render<Sync3ds>(), "Le homebrew PokeBank Sync");
        AssertPageHelp(Render<Statistics>(), "Ton argent, tes living dex");

        AssertBlockHelp(Render<Trades>(), "Nouvel échange", "L'échange se fait en direct");
        AssertBlockHelp(Render<Battles>(), "Nouveau combat", "Le combat se fait en direct");
    }

    [Fact]
    public void LivingDex_HelpIsShort()
    {
        var cut = Render<LivingDexes>();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find(".page-header-heading .help-tip-bubble")));
        var help = cut.Find(".page-header-heading .help-tip-bubble").TextContent;
        Assert.Contains("living dex global compte ton stockage interne", help);
        Assert.DoesNotContain("Rouge, Bleu, Jaune", help); // plus la liste des jeux
        Assert.True(help.Trim().Length < 300, help);
    }

    [Fact]
    public async Task InternalTrade_FirstStepHasItsHelpInATip()
    {
        Assert.True((await StoredSaves.AddAsync("sacha", "sacha.sav", TradeServiceTests.Bw2("Sacha", 1, [25], 64))).Ok);
        Assert.True((await StoredSaves.AddAsync("sacha", "regis.sav", TradeServiceTests.Bw2("Regis", 2, [120], 7))).Ok);

        var cut = Render<InternalTrade>();

        AssertBlockHelp(cut, "1. Choisis les deux sauvegardes", "Deux sauvegardes peuvent échanger");
        Assert.NotNull(cut.Find("select.internal-trade-first")); // le reste du bloc est toujours là
    }
}
