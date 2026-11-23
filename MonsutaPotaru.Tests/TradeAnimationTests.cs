using Bunit;
using MonsutaPotaru.Components.Shared;
using MonsutaPotaru.Services;

namespace MonsutaPotaru.Tests;

/// <summary>
/// Animation d'échange (<see cref="TradeAnimation"/>) : textes de chaque moment, œufs, évolutions, et ce que fait la page
/// avec wwwroot/js/echange.js (lancement, son coupé, fin, arrêt), musiques données seulement si leur fichier est là
/// (<see cref="TradeSounds"/>). Le déroulé lui-même est vérifié dans le navigateur.
/// </summary>
public sealed class TradeAnimationTests : PageTestContext
{
    private static readonly TradeShowPokemon Kadabra = new("Kadabra", "images/pokemon/64.png", false, "Alakazam", "images/pokemon/65.png");
    private static readonly TradeShowPokemon Egg = new("Œuf", null, true);

    /// <summary>Dossier du site avec ces musiques seulement, pour <see cref="TradeSounds.Load"/>.</summary>
    private static string WebRootWith(params string[] files)
    {
        var root = Directory.CreateTempSubdirectory("sons-").FullName;
        Directory.CreateDirectory(Path.Combine(root, "images", "sons"));
        foreach (var file in files)
            File.WriteAllBytes(Path.Combine(root, "images", "sons", file), [0]);
        TradeSounds.Load(root);
        return root;
    }

    private static string Lines(IRenderedComponent<TradeAnimation> cut) =>
        string.Join(" | ", cut.FindAll("[data-ligne]").Select(l => $"{l.GetAttribute("data-ligne")}: {l.TextContent}"));

    [Fact]
    public void Texts_ForEachMoment_AndEvolutions()
    {
        var cut = Render<TradeAnimation>(p => p.Add(a => a.Show, new TradeShow("Ondine", new("Bob", "images/pokemon/120.png", false), Kadabra)));
        Assert.Equal("envoi: Envoi de Bob à Ondine. | envoi: Au revoir, Bob ! | arrivee: Ondine t'envoie Kadabra. "
            + "| arrivee: Prends bien soin de Kadabra ! | evolution-0-debut: Hein ? Kadabra évolue ! "
            + "| evolution-0-fin: Félicitations ! Ton Kadabra a évolué en Alakazam !", Lines(cut));
        Assert.Equal(["images/pokemon/64.png", "images/pokemon/65.png"],
            cut.FindAll("[data-acteur='evolution-0'] img").Select(i => i.GetAttribute("src")));
        Assert.Contains("Passer", cut.Find(".trade-show-controls").TextContent);
    }

    [Fact]
    public void Eggs_AreDrawn_WithoutTheirSpecies()
    {
        var cut = Render<TradeAnimation>(p => p.Add(a => a.Show, new TradeShow("Ondine", Egg, Egg)));
        Assert.Equal("envoi: Envoi d'un œuf à Ondine. | envoi: Au revoir, petit œuf ! | arrivee: Ondine t'envoie un œuf. "
            + "| arrivee: Prends bien soin de cet œuf !", Lines(cut));
        Assert.Equal(2, cut.FindAll(".trade-show-egg").Count);
        Assert.Empty(cut.FindAll(".trade-show-screen img")); // l'image d'un œuf révélerait son espèce
    }

    [Fact]
    public void Music_OnlyTheFilesThatArePresent()
    {
        var root = WebRootWith(TradeSounds.TradeFile, TradeSounds.CongratulationsFile);
        try
        {
            var cut = Render<TradeAnimation>(p => p.Add(a => a.Show, new TradeShow("Ondine", Egg, Kadabra)));
            var show = cut.Find(".trade-show");
            Assert.Equal("images/sons/echange.mp3", show.GetAttribute("data-musique-echange"));
            Assert.Null(show.GetAttribute("data-musique-evolution")); // fichier absent : rien n'est demandé
            Assert.Equal("images/sons/felicitations.mp3", show.GetAttribute("data-musique-felicitations"));
            Assert.Equal(2, cut.FindAll(".trade-show-controls button").Count); // son, Passer

            // Sans évolution, ni musique d'évolution ni fanfare.
            var plain = Render<TradeAnimation>(p => p.Add(a => a.Show, new TradeShow("Ondine", Egg, Egg)));
            Assert.Null(plain.Find(".trade-show").GetAttribute("data-musique-felicitations"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void WithoutMusic_Silent_AndNoSoundButton()
    {
        var root = WebRootWith();
        try
        {
            var cut = Render<TradeAnimation>(p => p.Add(a => a.Show, new TradeShow("Ondine", Egg, Kadabra)));
            Assert.DoesNotContain("data-musique", cut.Find(".trade-show").OuterHtml);
            Assert.Equal("Passer", Assert.Single(cut.FindAll(".trade-show-controls button")).TextContent);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Module_PlaysTheShow_MutesTheSound_EndsAndStops()
    {
        var root = WebRootWith(TradeSounds.TradeFile, TradeSounds.EvolutionFile, TradeSounds.CongratulationsFile);
        var module = JSInterop.SetupModule("./js/echange.js");
        module.Setup<bool>("muted").SetResult(true); // son coupé lors d'un précédent échange
        module.SetupVoid("play", _ => true).SetVoidResult();
        module.SetupVoid("setMuted", _ => true).SetVoidResult();
        module.SetupVoid("stop", _ => true).SetVoidResult();
        var closed = false;
        var cut = Render<TradeAnimation>(p => p
            .Add(a => a.Show, new TradeShow("Ondine", Kadabra with { EvolvedSpecies = null, EvolvedSpriteUrl = null }, Egg))
            .Add(a => a.OnClosed, () => closed = true));

        cut.WaitForAssertion(() => Assert.Single(module.VerifyInvoke("play").Arguments.Skip(1)));
        Assert.Equal("Remettre le son", cut.Find(".trade-show-controls button").GetAttribute("aria-label"));
        await cut.Find(".trade-show-controls button").ClickAsync(new());
        Assert.False((bool?)module.VerifyInvoke("setMuted").Arguments[1]);
        Assert.Equal("Couper le son", cut.Find(".trade-show-controls button").GetAttribute("aria-label"));

        await cut.InvokeAsync(cut.Instance.Ended); // fin du déroulé (echange.js)
        await cut.Click("Continuer");
        Assert.True(closed);

        await DisposeComponentsAsync();
        module.VerifyInvoke("stop");
        Directory.Delete(root, recursive: true);
    }
}
