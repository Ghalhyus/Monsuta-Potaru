using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MonsutaPotaru.Components.Pages;
using MonsutaPotaru.Data;
using MonsutaPotaru.Services;

namespace MonsutaPotaru.Tests;

/// <summary>
/// Page « Cache-cache » : salle d'attente (réglages de l'hôte, invitation d'un ami, code pour un invité sans compte),
/// lancement, clics sur les Pokémon (le parc, parc.js, est simulé : la page reçoit seulement le numéro cliqué), tours,
/// écran de fin et « Recommencer ». Le temps du hub avance à la main.
/// </summary>
public sealed class HideSeekPageTests : PageTestContext
{
    private sealed class ManualTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private readonly ManualTime _time = new();
    private readonly HideSeekHub _hub;

    public HideSeekPageTests()
    {
        PokemonSprites.Load(RealSavesTests.WebRootWithImages()); // sprites Donjon Mystère : qui peut aller au parc
        _hub = new HideSeekHub(_time, ticking: false);
        Services.AddSingleton(_hub); // remplace celui de PageTestContext : le temps avance à la main
    }

    private async Task Wait(TimeSpan duration)
    {
        _time.Now += duration;
        await Task.Run(_hub.Tick);
    }

    /// <summary>Sacha crée la partie et règle 2 tours de 20 Pokémon aléatoires dans la forêt ; Ondine, invitée, accepte.</summary>
    private async Task<(IRenderedComponent<HideSeek> Sacha, IRenderedComponent<HideSeek> Ondine)> Lobby()
    {
        await MakeFriends("sacha", "ondine");
        var sacha = Render<HideSeek>();
        var ondine = RenderAs<HideSeek>("ondine");
        sacha.WaitForText("Créer une partie");
        Assert.Single(sacha.FindAll(".page-header"));
        await sacha.Click("Créer une partie");
        sacha.WaitForText("Salle d'attente");
        Assert.Contains("Joueurs (1 / 8)", sacha.Text());
        // Collection vide : rien à chercher, la partie ne peut pas être lancée
        sacha.WaitForText("Aucun Pokémon dans ce choix : change les réglages.");
        Assert.True(sacha.Find("button.hide-seek-start").HasAttribute("disabled"));

        await sacha.ChangeOn("select.hide-seek-pool", nameof(HideSeekPool.Random));
        await sacha.ChangeOn("input.hide-seek-rounds", "2");
        await sacha.ChangeOn("input.hide-seek-count", "20");
        await sacha.ChangeOn("input.hide-seek-seconds", "30");
        await sacha.ChangeOn("input.hide-seek-countdown", "4");
        await sacha.ChangeOn("select.hide-seek-landscape", nameof(ParkLandscape.Forest));
        sacha.WaitForText("Pokémon différents peuvent être tirés.");
        Assert.True(sacha.Find("input.hide-seek-nicknamed").HasAttribute("disabled")); // pas de surnom au hasard
        // Valeur trafiquée : refusée, les réglages ne changent pas
        await sacha.ChangeOn("input.hide-seek-count", "500");
        sacha.WaitForText("Le parc compte de 20 à 80 Pokémon.");
        Assert.Equal(20, _hub.Active("sacha")!.Snapshot("sacha").Settings.Count);

        await sacha.ChangeOn("select.hide-seek-friend", "ondine");
        await sacha.Click("Inviter");
        sacha.WaitForText("Invitation envoyée à ondine.");
        sacha.WaitForText("invité, en attente de sa réponse");
        ondine.WaitForText("t'invite à une partie de cache-cache (2 tours, 30 s par tour, 20 Pokémon)");
        await ondine.Click("Accepter");
        ondine.WaitForText("En attente du lancement de la partie par sacha");
        Assert.Contains("Parc : Forêt", ondine.Text());
        Assert.Contains("Décompte de 4 s avant chaque tour", ondine.Text());
        Assert.Contains("Des Pokémon aléatoires (hors collection)", ondine.Text());
        Assert.Empty(ondine.FindAll("button.hide-seek-start")); // seul l'hôte lance
        sacha.WaitForText("Joueurs (2 / 8)");
        return (sacha, ondine);
    }

    private HideSeekSnapshot Shown => _hub.Active("sacha")!.Snapshot("sacha");

    [Fact]
    public async Task Lobby_MoodAndRandomFill_SetByTheHost_ShownToTheOthers()
    {
        ParkHabitats.Load(RealSavesTests.WebRoot()); // types de cases des cartes : humeur des Pokémon
        // La collection de Sacha : trois Pokémon Eau, deux Feu
        await using (var db = Db.NewContext())
        {
            db.BankPokemon.AddRange(new (ushort, string)[] { (7, "Carapuce"), (54, "Psykokwak"), (129, "Magicarpe"), (4, "Salamèche"), (37, "Goupix") }
                .Select(p => HideSeekDrawTests.Row("sacha", p.Item1, p.Item2, p.Item2)));
            await db.SaveChangesAsync();
        }
        var happy = new ushort[] { 7, 54, 129, 4, 37 }
            .Count(s => HideSeekDraw.MoodIn(HideSeekDrawTests.Mon(s), ParkLandscape.Beach, 1) == ParkMood.Happy);
        Assert.InRange(happy, 1, 4); // le filtre retire quelqu'un, il reste de quoi jouer

        await MakeFriends("sacha", "ondine");
        var sacha = Render<HideSeek>();
        var ondine = RenderAs<HideSeek>("ondine");
        sacha.WaitForText("Créer une partie");
        await sacha.Click("Créer une partie");
        sacha.WaitForText("Salle d'attente");
        await sacha.ChangeOn("input.hide-seek-rounds", "1");
        await sacha.ChangeOn("input.hide-seek-count", "20");
        await sacha.ChangeOn("select.hide-seek-landscape", nameof(ParkLandscape.Beach));
        sacha.WaitForText("5 Pokémon différents dans ce choix peuvent vivre à la plage : des Pokémon aléatoires complètent le parc.");
        Assert.True(sacha.Find("input.hide-seek-fill").HasAttribute("checked")); // complété par défaut, tous les Pokémon par défaut
        Assert.Equal(nameof(HideSeekMood.All), sacha.Find("select.hide-seek-mood").GetAttribute("value"));

        // Sans complément : le parc n'a que les Pokémon du choix
        await sacha.ChangeOn("input.hide-seek-fill", false);
        sacha.WaitForText("5 Pokémon différents dans ce choix peuvent vivre à la plage : le parc n'en aura que 5.");
        // Heureux seulement : ceux qui aiment la plage
        await sacha.ChangeOn("select.hide-seek-mood", nameof(HideSeekMood.Happy));
        sacha.WaitForText($"{happy} Pokémon différents dans ce choix peuvent vivre à la plage (heureux dans le parc) : le parc n'en aura que {happy}.");
        Assert.Contains("un Pokémon est heureux quand un habitat qu'il aime couvre plus de la moitié du parc", sacha.Text());
        // Paysage au hasard : le nombre dépend du paysage du tour
        await sacha.ChangeOn("select.hide-seek-landscape", "");
        sacha.WaitForText("selon le paysage du tour, le parc en aura de");
        await sacha.ChangeOn("select.hide-seek-landscape", nameof(ParkLandscape.Beach));
        // Valeur trafiquée : refusée
        await sacha.ChangeOn("select.hide-seek-mood", "9");
        sacha.WaitForText("Cette humeur n'est pas proposée.");
        Assert.Equal(HideSeekMood.Happy, Shown.Settings.Mood);
        Assert.False(Shown.Settings.FillWithRandom);
        // Des Pokémon aléatoires : pas de case « compléter » (ils remplissent le parc eux-mêmes)
        await sacha.ChangeOn("select.hide-seek-pool", nameof(HideSeekPool.Random));
        sacha.WaitForText("Pokémon différents (heureux dans le parc) peuvent être tirés.");
        Assert.Empty(sacha.FindAll("input.hide-seek-fill"));
        await sacha.ChangeOn("select.hide-seek-pool", nameof(HideSeekPool.Host));

        await sacha.ChangeOn("select.hide-seek-friend", "ondine");
        await sacha.Click("Inviter");
        ondine.WaitForText("t'invite à une partie de cache-cache");
        await ondine.Click("Accepter");
        ondine.WaitForText("En attente du lancement de la partie par sacha");
        Assert.Contains("Humeur des Pokémon : heureux dans le parc", ondine.Text());
        Assert.Contains("Pas de Pokémon au hasard pour compléter le parc", ondine.Text());

        // La partie : seulement les Pokémon heureux de la collection
        await sacha.Click("Lancer la partie");
        sacha.WaitForText("Prépare-toi…");
        Assert.NotNull(Shown.Target!.OwnerPseudo); // la cible vient de la collection
        await Wait(TimeSpan.FromSeconds(HideSeekSettings.DefaultCountdown));
        sacha.WaitForText("Cherche-le dans le parc !");
        Assert.Equal($"Tour 1 sur 1 · Plage · {happy} Pokémon", PageTestExtensions.Normalize(sacha.Find(".hide-seek-round").TextContent));
    }

    private static async Task ClickPokemon(IRenderedComponent<HideSeek> cut, int id) =>
        await cut.InvokeAsync(() => cut.Instance.OnPokemonClicked(id));

    [Fact]
    public async Task Game_FromLobbyToRematch()
    {
        var (sacha, ondine) = await Lobby();
        await sacha.Click("Lancer la partie");
        sacha.WaitForText("Prépare-toi…");
        ondine.WaitForText("Prépare-toi…");
        // Pendant la partie : pas de bandeau, le parc et ses infos par-dessus ; le décompte dans un pop-up qui annonce la
        // cible (le parc caché derrière), sans rappel de la cible dans le panneau
        Assert.Empty(sacha.FindAll(".page-header"));
        Assert.Equal("Cache-cache", sacha.Find("h1.visually-hidden").TextContent);
        var target = Shown.Target!;
        var announce = PageTestExtensions.Normalize(ondine.Find(".hide-seek-cover .hide-seek-announce").TextContent);
        Assert.Contains("Tour 1 sur 2 · Forêt", announce);
        Assert.Contains($"Trouve {target.Name}", announce);
        Assert.Equal("4", ondine.Find(".hide-seek-announce-count").TextContent); // le décompte réglé par l'hôte
        Assert.Empty(sacha.FindAll(".hide-seek-panel .hide-seek-target"));
        Assert.Contains("Tour 1 sur 2", sacha.Find(".hide-seek-panel").TextContent);
        // Le parc de chacun : même graine (le tour) et même début de la recherche (le serveur) : mêmes Pokémon aux mêmes endroits
        var syncs = JSInterop.Invocations["create"]
            .Select(i => System.Text.Json.JsonSerializer.SerializeToElement(i.Arguments[2]).GetProperty("sync")).ToList();
        Assert.Equal(2, syncs.Count); // Sacha et Ondine
        Assert.All(syncs, s =>
        {
            Assert.Equal(Shown.Round!.Seed, s.GetProperty("seed").GetInt32());
            Assert.InRange(s.GetProperty("startsIn").GetDouble(), 3.9, 4.0); // le décompte réglé, le temps du hub arrêté
            Assert.Equal(HideSeekDraw.NightLevel(Shown.StartsAt!.Value), s.GetProperty("night").GetDouble()); // 13 h à Paris : le jour
        });

        // Avant la fin de la préparation, un clic ne compte pas
        await ClickPokemon(sacha, target.Id);
        sacha.WaitForText("Attends le début du tour.");
        await Wait(TimeSpan.FromSeconds(4));
        sacha.WaitForText("Cherche-le dans le parc !");
        Assert.Empty(sacha.FindAll(".hide-seek-cover"));
        Assert.Contains(target.Name, sacha.Find(".hide-seek-panel .hide-seek-target").TextContent); // le rappel sur le parc

        // Ondine se trompe : bloquée ; Sacha trouve
        var wrong = Shown.Round!.Pokemon.First(p => p.Id != target.Id);
        await ClickPokemon(ondine, wrong.Id);
        ondine.WaitForText($"Raté ! C'était {wrong.Name}. Tu es bloqué 3 secondes.");
        Assert.NotNull(ondine.Find(".hide-seek-blocked"));
        await ClickPokemon(sacha, target.Id);
        sacha.WaitForText($"Trouvé ! C'était bien {target.Name}.");
        sacha.WaitForText("Bravo, tu l'as trouvé !");
        ondine.WaitForText("sacha l'a trouvé.");
        Assert.Contains("La cible était", ondine.Find(".hide-seek-target").TextContent);
        Assert.Equal(["sacha 1", "ondine 0"], sacha.FindAll(".hide-seek-scores li").Select(li => PageTestExtensions.Normalize(li.TextContent)));

        // 2e tour : personne ne trouve
        await Wait(HideSeekTiming.Reveal);
        sacha.WaitForText("Tour 2 sur 2");
        await Wait(TimeSpan.FromSeconds(4));
        await Wait(TimeSpan.FromSeconds(30));
        ondine.WaitForText("Temps écoulé : personne ne l'a trouvé.");
        await Wait(HideSeekTiming.Reveal);
        sacha.WaitForText("Victoire !");
        ondine.WaitForText("Défaite…");
        Assert.Empty(ondine.FindAll("button.hide-seek-rematch")); // seul l'hôte recommence

        // Recommencer : Ondine voit la proposition sur son écran de fin
        await sacha.Click("Recommencer avec les mêmes joueurs");
        sacha.WaitForText("Salle d'attente");
        sacha.WaitForText("ondine est invité");
        ondine.WaitForText("sacha propose de recommencer avec les mêmes réglages.");
        await ondine.ClickOn("button.hide-seek-rematch-accept");
        ondine.WaitForText("En attente du lancement de la partie par sacha");
        sacha.WaitForText("Joueurs (2 / 8)");
        Assert.Equal(2, _hub.Active("sacha")!.Snapshot("sacha").Settings.Rounds);
    }

    [Fact]
    public async Task Guest_JoinsWithTheCode_AndLeaves()
    {
        var sacha = Render<HideSeek>();
        await sacha.Click("Créer une partie");
        sacha.WaitForText("Salle d'attente");
        var code = _hub.Active("sacha")!.Code;
        Assert.Contains(ChessHub.FormatCode(code), sacha.Find(".hide-seek-code").TextContent);

        var guest = RenderAnonymous<HideSeek>();
        guest.WaitForText("Rejoindre une partie");
        Assert.Empty(guest.FindAll("button.hide-seek-create")); // sans compte : seulement rejoindre
        await guest.ChangeOn("input.hide-seek-code-input", code);
        await guest.ChangeOn("input.hide-seek-guest-name", "L");
        await guest.Click("Rejoindre");
        guest.WaitForText("Ton prénom doit faire de 2 à 20 caractères.");
        await guest.ChangeOn("input.hide-seek-guest-name", "Léo");
        await guest.Click("Rejoindre");
        guest.WaitForText("En attente du lancement de la partie par sacha");
        sacha.WaitForText("Léo (invité)");
        sacha.WaitForText("Joueurs (2 / 8)");

        await guest.Click("Quitter la partie");
        guest.WaitForText("Rejoindre une partie");
        sacha.WaitForText("Joueurs (1 / 8)");

        // L'hôte annule : la salle d'attente ferme
        await sacha.Click("Annuler la partie");
        Assert.Null(_hub.Active("sacha"));
    }
}
