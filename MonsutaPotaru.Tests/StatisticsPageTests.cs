using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MonsutaPotaru.Components.Pages;
using MonsutaPotaru.Data;
using MonsutaPotaru.Services;

namespace MonsutaPotaru.Tests;

/// <summary>Page « Statistiques » : onglets à gauche (Générale, Défis, Multijoueurs), la sienne ou celle d'un autre joueur.</summary>
public sealed class StatisticsPageTests : PageTestContext
{
    private LivingDexService Dexes => Services.GetRequiredService<LivingDexService>();
    private RibbonService RibbonStore => Services.GetRequiredService<RibbonService>();
    private CompletionService Completions => Services.GetRequiredService<CompletionService>();
    private NavigationManager Navigation => Services.GetRequiredService<NavigationManager>();

    private IRenderedComponent<Statistics> Open(string query = "", string? pseudo = null)
    {
        Navigation.NavigateTo("statistiques" + query);
        return pseudo is null ? Render<Statistics>() : Render<Statistics>(p => p.Add(x => x.Pseudo, pseudo));
    }

    [Theory]
    [InlineData(null, Statistics.StatsTab.General)]
    [InlineData("defis", Statistics.StatsTab.Challenges)]
    [InlineData("DEFIS", Statistics.StatsTab.Challenges)]
    [InlineData("multijoueurs", Statistics.StatsTab.Multiplayer)]
    [InlineData("n-importe-quoi", Statistics.StatsTab.General)]
    public void Tab_InTheAddress(string? slug, Statistics.StatsTab tab)
    {
        Assert.Equal(tab, Statistics.TabFromSlug(slug));
        Assert.Equal(tab, Statistics.TabFromSlug(Statistics.TabSlug(tab))); // aller-retour
        Assert.Equal(tab == Statistics.StatsTab.General, Statistics.TabSlug(tab) is null); // l'onglet par défaut n'est pas écrit
    }

    [Fact]
    public void General_ShowsMoneyDepositedAndSpent()
    {
        GiveMoney("sacha", 2_000);
        using (var db = Db.NewContext())
        {
            db.WalletOperations.Add(new WalletOperation { OwnerId = "sacha", Kind = WalletOperationKind.BuyCollectionSlots, Amount = -500 });
            db.SaveChanges();
        }

        var cut = Open();

        cut.WaitForText("Argent déposé");
        var values = cut.FindAll(".stat-value").Select(v => v.TextContent).ToList();
        Assert.Equal([InventoryService.FormatMoney(2_500), InventoryService.FormatMoney(500)], values);
        Assert.Contains("ton porte-monnaie", cut.Text());
        // Onglets : Générale actif, les autres par l'adresse.
        var tabs = cut.FindAll(".stats-tabs a");
        Assert.Equal(["Générale", "Défis", "Multijoueurs", "Mini-jeux"], tabs.Select(t => t.TextContent));
        Assert.EndsWith("statistiques?onglet=mini-jeux", tabs[3].GetAttribute("href"));
        Assert.Equal("page", tabs[0].GetAttribute("aria-current"));
        Assert.EndsWith("statistiques?onglet=defis", tabs[1].GetAttribute("href"));
        Assert.DoesNotContain("onglet", tabs[0].GetAttribute("href"));
    }

    [Fact]
    public async Task Challenges_LivingDexAverages_ShinyApart()
    {
        var empty = Open("?onglet=defis");
        empty.WaitForText("Aucun living dex pour l'instant.");
        Assert.Contains("Aucun living dex chromatique pour l'instant.", empty.Text());
        Assert.Equal(2, empty.FindAll("a[href='livingdex']").Count);
        Assert.Equal("page", empty.FindAll(".stats-tabs a")[1].GetAttribute("aria-current"));

        AddToBank("sacha", 1, 4, 7);
        Assert.True((await Dexes.CreateGlobalAsync("sacha")).Ok);

        var cut = Open("?onglet=defis");

        cut.WaitForText("Moyenne de tes living dex");
        var normal = cut.FindAll(".block")[0];
        Assert.Equal("1", normal.QuerySelector(".block-count")?.TextContent);
        // Le global n'a pas de Régional ; National : 3 / 1 025 = 0,2 %.
        Assert.Contains("Aucun living dex n'a ce mode.", normal.TextContent);
        Assert.Equal(["Général", "Régional", "National", "Complet"], normal.QuerySelectorAll(".stat-bar-label").Select(l => l.TextContent));
        Assert.Contains(LivingDexGames.Percent(2, 1_000), normal.QuerySelectorAll(".livingdex-progress-label").Select(l => l.TextContent));
        Assert.Equal(["—", "0 / 1", "0 / 1"], normal.QuerySelectorAll(".stat-value").Select(v => v.TextContent));
        Assert.Contains("Aucun living dex chromatique pour l'instant.", cut.FindAll(".block")[1].TextContent);
    }

    [Fact]
    public async Task Challenges_RibbonsAndCompletions()
    {
        var empty = Open("?onglet=defis");
        empty.WaitForText("Aucun Pokémon suivi pour l'instant. Ajoutes-en un dans Rubans."); // avec l'espace avant le lien
        Assert.Contains("Aucune complétion pour l'instant. Crées-en une dans Complétion (100 %).", empty.Text());
        Assert.Contains("Aucun living dex pour l'instant. Crée-en un dans Living dex.", empty.Text());
        Assert.Single(empty.FindAll("a[href='rubans']"));
        Assert.Single(empty.FindAll("a[href='completion']"));

        // Deux Pokémon suivis sans ruban ; la complétion de Rouge (pas de quêtes annexes).
        foreach (var bankId in AddToBank("sacha", 25, 133))
            Assert.True((await RibbonStore.AddAsync("sacha", bankId)).Ok);
        var saveId = await StoreReal("sacha", "rouge.sav");
        Assert.True((await Completions.CreateAsync("sacha", saveId, LivingDexGame.Red)).Ok);
        var red = Assert.Single(await Completions.ListAsync("sacha")).Progress;

        var cut = Open("?onglet=defis");

        cut.WaitForText("Rubans gagnés");
        var ribbons = cut.FindAll(".block")[2];
        Assert.Equal("2", ribbons.QuerySelector(".block-count")?.TextContent);
        Assert.Contains($"Tes 2 Pokémon suivis : chaque ruban réuni dans un souvenir compte (complétion sur {Ribbons.Total} rubans).", ribbons.TextContent);
        // Rubans gagnés, par Pokémon, Pokémon complets.
        Assert.Equal(["0", "0", "0 / 2"], ribbons.QuerySelectorAll(".stat-value").Select(v => v.TextContent));
        Assert.Equal(["Complétion moyenne"], ribbons.QuerySelectorAll(".stat-bar-label").Select(l => l.TextContent));
        Assert.Equal(LivingDexGames.Percent(0, 1), ribbons.QuerySelector(".livingdex-progress-label")?.TextContent);

        var completion = cut.FindAll(".block")[3];
        Assert.Equal("1", completion.QuerySelector(".block-count")?.TextContent);
        Assert.Equal(["Générale", "Histoire", "Pokédex", "Dresseurs", "Objets", "Rencontres uniques", "Quêtes annexes"],
            completion.QuerySelectorAll(".stat-bar-label").Select(l => l.TextContent));
        Assert.Equal(red.Percent, completion.QuerySelector(".livingdex-progress-label")?.TextContent); // le même que la vue générale
        Assert.Contains("Aucune complétion n'a cette section.", completion.TextContent); // quêtes annexes
        Assert.Equal(Enumerable.Repeat("1 complétion", 6), completion.QuerySelectorAll(".stat-bar-count").Select(c => c.TextContent));
        Assert.Equal(["0 / 1"], completion.QuerySelectorAll(".stat-value").Select(v => v.TextContent));
        Assert.DoesNotContain("pour l'instant", ribbons.TextContent + completion.TextContent);
    }

    [Fact]
    public void Multiplayer_TradesAndBattles()
    {
        var empty = Open("?onglet=multijoueurs");
        empty.WaitForText("Aucun échange pour l'instant");
        Assert.Contains("Aucun combat pour l'instant", empty.Text());
        Assert.DoesNotContain("Échecs", empty.Text()); // dans l'onglet Mini-jeux

        using (var db = Db.NewContext())
        {
            var at = DateTime.UtcNow;
            db.TradeRecords.AddRange(
                new TradeRecord { At = at, FirstId = "sacha", SecondId = "ondine" },
                new TradeRecord { At = at, FirstId = "ondine", SecondId = "sacha" });
            db.BattleRecords.AddRange(
                new BattleRecord { At = at, FirstId = "sacha", SecondId = "ondine", Format = "f", WinnerId = "sacha" },
                new BattleRecord { At = at, FirstId = "pierre", SecondId = "sacha", Format = "f", WinnerId = "pierre" },
                new BattleRecord { At = at, FirstId = "pierre", SecondId = "sacha", Format = "f" });
            db.SaveChanges();
        }

        var cut = Open("?onglet=multijoueurs");

        cut.WaitForAssertion(() => Assert.Equal(6, cut.FindAll(".stat-value").Count));
        // Échanges réussis, partenaires ; combats : victoires, défaites, nuls, adversaires.
        Assert.Equal(["2", "1", "1", "1", "1", "2"], cut.FindAll(".stat-value").Select(v => v.TextContent));
        Assert.DoesNotContain("pour l'instant", cut.Text());
    }

    [Fact]
    public void MiniGames_GeneralChessAndHideSeek()
    {
        var empty = Open("?onglet=mini-jeux");
        empty.WaitForText("Aucune partie pour l'instant : essaie les Échecs ou le Cache-cache.");
        Assert.Contains("invite un ami, ou crée une partie publique", empty.Text());
        Assert.Contains("crée une partie dans Cache-cache", empty.Text());
        Assert.Equal(["0", "0", "0", "0", "0", "—", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0"],
            empty.FindAll(".stat-value").Select(v => v.TextContent));
        Assert.Empty(empty.FindAll(".stat-team")); // pas d'équipe favorite

        using (var db = Db.NewContext())
        {
            var at = DateTime.UtcNow;
            db.ChessRecords.AddRange(
                // Victoire en Psy (3 pièces prises, 1 perdue), nulle en Feu (chez les noirs : 2 prises, 4 perdues)
                new ChessRecord { At = at.AddHours(-3), WhiteId = "sacha", BlackId = "ondine", WhiteTeam = "psy", BlackTeam = "feu",
                    End = "Checkmate", WinnerId = "sacha", WhiteCaptures = 3, BlackCaptures = 1 },
                new ChessRecord { At = at.AddHours(-2), WhiteId = "ondine", BlackId = "sacha", WhiteTeam = "psy", BlackTeam = "feu",
                    End = "Stalemate", WhiteCaptures = 4, BlackCaptures = 2 },
                // Défaite en Feu contre un invité sans compte, la plus récente : à égalité (2 parties), Feu l'emporte
                new ChessRecord { At = at.AddHours(-1), WhiteId = "sacha", BlackId = ChessHub.GuestIdPrefix + "abc", WhiteTeam = "feu",
                    BlackTeam = "psy", End = "Forfeit", WinnerId = ChessHub.GuestIdPrefix + "abc" },
                new ChessRecord { At = at.AddHours(-4), WhiteId = "sacha", BlackId = "pierre", WhiteTeam = "psy", BlackTeam = "feu",
                    End = "Timeout", WinnerId = "pierre" },
                new ChessRecord { At = at, WhiteId = "ondine", BlackId = "pierre", WhiteTeam = "psy", BlackTeam = "feu", End = "Checkmate" }); // pas la sienne
            // Cache-cache : une victoire (3 trouvés) contre Ondine et un invité, une nulle (1 trouvé) avec Pierre
            Guid first = Guid.NewGuid(), second = Guid.NewGuid(), other = Guid.NewGuid();
            db.HideSeekRecords.AddRange(
                new HideSeekRecord { GameId = first, At = at.AddHours(-5), UserId = "sacha", Found = 3, Rounds = 5, Outcome = "Won" },
                new HideSeekRecord { GameId = first, At = at.AddHours(-5), UserId = "ondine", Found = 1, Rounds = 5, Outcome = "Lost" },
                new HideSeekRecord { GameId = first, At = at.AddHours(-5), UserId = ChessHub.GuestIdPrefix + "x", Found = 0, Rounds = 5, Outcome = "Lost" },
                new HideSeekRecord { GameId = second, At = at.AddHours(-6), UserId = "sacha", Found = 1, Rounds = 2, Outcome = "Draw" },
                new HideSeekRecord { GameId = second, At = at.AddHours(-6), UserId = "pierre", Found = 1, Rounds = 2, Outcome = "Draw" },
                new HideSeekRecord { GameId = other, At = at, UserId = "ondine", Found = 2, Rounds = 2, Outcome = "Won" }); // pas la sienne
            db.SaveChanges();
        }

        var cut = Open("?onglet=mini-jeux");

        cut.WaitForAssertion(() => Assert.Equal(20, cut.FindAll(".stat-value").Count));
        // Générale : jouées, victoires, défaites, nulles, joueurs (comptes), favori ; Échecs : les mêmes, prises, perdues,
        // équipe ; Cache-cache : les mêmes, Pokémon trouvés.
        Assert.Equal(["6", "2", "2", "2", "2", "Échecs", "4", "1", "2", "1", "2", "5", "5", "Dracaufeu", "2", "1", "0", "1", "2", "4"],
            cut.FindAll(".stat-value").Select(v => v.TextContent));
        Assert.Equal(["4 parties", "2 parties"], cut.FindAll(".stat-note").Select(n => n.TextContent));
        // Groupes : Parties, Favori ; Parties, Pièces, Équipe favorite avec ses six Pokémon (roi d'abord) ; Parties, Recherche
        Assert.Equal(["Parties", "Favori", "Parties", "Pièces", "Équipe favorite", "Parties", "Recherche"],
            cut.FindAll(".stat-group-title").Select(t => t.TextContent));
        Assert.Equal(["Jouées", "Gagnées", "Perdues", "Nulles", "Joueurs différents"],
            cut.FindAll(".stat-tiles")[0].QuerySelectorAll(".card .small.text-muted").Select(l => l.TextContent));
        var members = cut.FindAll(".stat-team li"); // la carte de l'équipe : nom, parties, puis ses Pokémon
        Assert.Equal(6, members.Count);
        Assert.Contains("Dracaufeu", members[0].TextContent);
        Assert.Contains("Roi", members[0].TextContent);
        Assert.DoesNotContain("pour l'instant", cut.Text());
        Assert.Contains("mangés par tes pièces", cut.Text());
    }

    [Fact]
    public async Task Visitor_SeesTheStatistics_WhenTheProfileShowsThem()
    {
        GiveMoney("ondine", 300);
        Assert.True((await Dexes.CreateGlobalAsync("ondine")).Ok);

        var general = Open(pseudo: "ondine");
        general.WaitForText("son porte-monnaie");
        Assert.Contains(InventoryService.FormatMoney(300), general.Text());
        Assert.Contains("Lecture seule", general.Text()); // en-tête du joueur
        Assert.Contains("Statistiques", general.Find(".nav-tabs .nav-link.active").TextContent);

        var challenges = Open("?onglet=defis", "ondine");
        challenges.WaitForText("Moyenne de ses living dex");
        Assert.DoesNotContain("Crée-en un", challenges.Text());
        Assert.Contains("Aucun Pokémon suivi pour l'instant.", challenges.Text());
        Assert.DoesNotContain("Ajoutes-en", challenges.Text());
        Assert.DoesNotContain("Crées-en", challenges.Text());

        Assert.True((await RibbonStore.AddAsync("ondine", AddToBank("ondine", 25)[0])).Ok);
        var ribbons = Open("?onglet=defis", "ondine");
        ribbons.WaitForText("Son Pokémon suivi :");

        var multiplayer = Open("?onglet=multijoueurs", "ondine");
        multiplayer.WaitForText("Échanges réussis");
        Assert.DoesNotContain("pour l'instant", multiplayer.Text());

        var miniGames = Open("?onglet=mini-jeux", "ondine");
        miniGames.WaitForText("Équipe favorite");
        Assert.Contains("mangés par ses pièces", miniGames.Text());
        Assert.DoesNotContain("pour l'instant", miniGames.Text());

        // Page décochée dans « Visibilité » : comme un joueur inconnu.
        UpdateUser("ondine", u => u.PublicPages = PublicPages.All & ~PublicPages.Statistics);
        var hidden = Open(pseudo: "ondine");
        hidden.WaitForText(PlayerService.HiddenMessage);
        Assert.DoesNotContain("Argent", hidden.Text());
    }
}
