using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MonsutaPotaru.Components.Pages;
using MonsutaPotaru.Data;
using MonsutaPotaru.Services;

namespace MonsutaPotaru.Tests;

/// <summary>Pages « Amis », « Mes sauvegardes », « 3DS », « Administration », « Statistiques » et celles des joueurs.</summary>
public sealed class AccountPagesTests : PageTestContext
{
    private FriendService Friends => Services.GetRequiredService<FriendService>();

    // ---------- Amis ----------

    [Fact]
    public async Task Friends_RequestsByCode_AcceptDeclineCancelRemove()
    {
        var cut = Render<Friends>();
        cut.WaitForText("Tu n'as pas encore d'ami");
        await cut.FindAll("[role=tab]")[1].ClickAsync(new());

        // Code ami : régénéré (confirmation), puis une demande envoyée avec le code d'Ondine, une autre refusée
        await cut.Click("Régénérer");
        await cut.Click("Annuler");
        await cut.Click("Régénérer");
        await cut.Click("Tirer un nouveau code");
        cut.WaitForAssertion(() => Assert.Contains("alert-success", cut.Markup));
        await cut.Find(".friend-code-input").InputAsync(new() { Value = "ZZZZ-ZZZZ" });
        await cut.Find("form").SubmitAsync();
        cut.WaitForAssertion(() => Assert.Contains("alert-danger", cut.Markup));
        await cut.Find(".friend-code-input").InputAsync(new() { Value = await Friends.CodeAsync("ondine") });
        await cut.Find("form").SubmitAsync();
        cut.WaitForAssertion(() => Assert.Contains("alert-success", cut.Markup));

        // Demandes : celle envoyée, annulée ; celles reçues, acceptée et refusée
        cut.WaitForText("ondine");
        await cut.Click("Annuler");
        Assert.True((await Friends.RequestAsync("ondine", "sacha")).Ok);
        Assert.True((await Friends.RequestAsync("pierre", "sacha")).Ok);
        var requests = Render<Friends>();
        await requests.FindAll("[role=tab]")[1].ClickAsync(new());
        await requests.Click("Accepter");
        await requests.Click("Refuser");
        await requests.FindAll("[role=tab]")[0].ClickAsync(new());
        requests.WaitForText("pierre");

        // Recherche parmi les amis, puis retrait (confirmation)
        await requests.Find(".friend-search").InputAsync(new() { Value = "zzz" });
        await requests.Find(".friend-search").InputAsync(new() { Value = "pie" });
        await requests.Click("Retirer");
        await requests.Click("Annuler");
        await requests.Click("Retirer");
        await requests.Find(".btn-danger").ClickAsync(new());
        requests.WaitForText("n'est plus ton ami");
        await requests.Find(".alert .btn-close").ClickAsync(new());
    }

    [Fact]
    public void Friends_RequestsTab_FromTheAddress()
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("amis?onglet=demandes");
        var cut = Render<Friends>();
        Assert.Contains("active", cut.FindAll("[role=tab]")[1].ClassName);
    }

    // ---------- Mes sauvegardes ----------

    [Fact]
    public async Task Saves_AddUpdateDownloadDelete()
    {
        var cut = Render<Saves>();
        cut.WaitForText("Ajouter une sauvegarde");
        var data = SaveSessionTests.CreateBw2Save();
        await BankPageTests.Upload(cut, new BankPageTests.FakeBrowserFile("noir2.sav", data));
        cut.WaitForAssertion(() => Assert.Contains("alert-success", cut.Markup));
        await BankPageTests.Upload(cut, new BankPageTests.FakeBrowserFile("texte.sav", [1, 2, 3]));
        cut.WaitForAssertion(() => Assert.Contains("alert-danger", cut.Markup));
        await BankPageTests.Upload(cut, new BankPageTests.FakeBrowserFile("cassé.sav", [], fail: true));
        cut.WaitForText("Opération impossible");

        // Mise à jour : la même (refusée : pas plus de temps de jeu), puis plus ancienne (confirmation)
        var inputs = cut.FindComponents<Microsoft.AspNetCore.Components.Forms.InputFile>();
        await cut.InvokeAsync(() => inputs[1].Instance.OnChange.InvokeAsync(
            new(new[] { new BankPageTests.FakeBrowserFile("noir2.sav", data) })));
        var older = new PKHeX.Core.SAV5B2W2(data.ToArray()) { PlayedHours = 0, PlayedMinutes = 1 };
        older.SetBoxSlotAtIndex(SaveSessionTests.CreatePk5(25, 20), 0, 0);
        await cut.InvokeAsync(() => cut.FindComponents<Microsoft.AspNetCore.Components.Forms.InputFile>()[1].Instance.OnChange.InvokeAsync(
            new(new[] { new BankPageTests.FakeBrowserFile("noir2.sav", older.Write().ToArray()) })));
        if (cut.HasButton("Remplacer quand même"))
        {
            await cut.Click("Annuler");
            await cut.InvokeAsync(() => cut.FindComponents<Microsoft.AspNetCore.Components.Forms.InputFile>()[1].Instance.OnChange.InvokeAsync(
                new(new[] { new BankPageTests.FakeBrowserFile("noir2.sav", older.Write().ToArray()) })));
            await cut.Click("Remplacer quand même");
        }

        await cut.Click("Télécharger");
        await cut.Click("Supprimer");
        cut.WaitForText("Télécharger et supprimer");
        await cut.Click("Annuler");
        await cut.Click("Supprimer");
        await cut.Click("Supprimer sans télécharger");
        cut.WaitForText("Le fichier ne te sera pas rendu");
        await cut.Click("Annuler");
        await cut.Click("Supprimer");
        await cut.Click("Télécharger et supprimer");
        cut.WaitForText("téléchargée et retirée du serveur");
    }

    [Fact]
    public async Task Saves_DeleteWithoutDownload_AlsoRemovesItsLivingDex()
    {
        await StoreReal("sacha", "noir.sav");
        var dexes = Services.GetRequiredService<LivingDexService>();
        var candidate = (await dexes.ListCandidatesAsync("sacha")).Single();
        Assert.True((await dexes.CreateAsync("sacha", candidate.SaveId, candidate.Games[0])).Ok);
        var cut = Render<Saves>();
        await cut.Click("Supprimer");
        cut.WaitForText("sera supprimé aussi");
        await cut.Click("Supprimer sans télécharger");
        await cut.Click("Oui, supprimer sans télécharger");
        cut.WaitForAssertion(() => Assert.Empty(Rows<StoredSave>()));
        await cut.Click("Agrandir");
        await cut.Click("Annuler");
    }

    // ---------- 3DS ----------

    [Fact]
    public async Task Sync3ds_PairsAConsole_ThenUnlinksIt()
    {
        var cut = Render<Sync3ds>();
        cut.WaitForText("Relier une 3DS");
        await cut.Click("Relier une 3DS");
        await cut.Click("Annuler");
        await cut.Click("Relier une 3DS");
        // La console tape un code (ici un nouveau du même compte) : la page voit arriver la 3DS à sa prochaine relecture
        var sync = Services.GetRequiredService<SyncService>();
        var pairing = await sync.CreatePairingCodeAsync("sacha");
        Assert.True((await sync.LinkAsync(pairing.Code, "3DS du salon")).Ok);
        cut.WaitForAssertion(() => Assert.Contains("reliée à ton compte", cut.Text()), TimeSpan.FromSeconds(15));

        await cut.Click("Retirer");
        await cut.Click("Annuler");
        await cut.Click("Retirer");
        await cut.Click("Retirer cette 3DS");
        cut.WaitForText("retirée");
        await cut.Find(".alert .btn-close").ClickAsync(new());
    }

    // ---------- Administration ----------

    [Fact]
    public async Task Admin_InviteCodeDebugModeAndRoles()
    {
        MakeAdmin("sacha");
        await AdminService.InitializeAsync(Db.NewContext(), Db.Config, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
        var cut = Render<Admin>();
        cut.WaitForText("Code d'invitation");
        await cut.Click("Régénérer");
        await cut.Click("Annuler");
        await cut.Click("Régénérer");
        await cut.Click("Tirer un nouveau code");
        cut.WaitForAssertion(() => Assert.Contains("alert-success", cut.Markup));
        await cut.Click("Activer");
        cut.WaitForText("Mode debug activé");
        await cut.Click("Désactiver");
        await cut.Click("Rendre administrateur");
        cut.WaitForText("Retirer le rôle administrateur");
        await cut.Buttons("Retirer le rôle administrateur")[^1].ClickAsync(new());
        await cut.Find(".alert .btn-close").ClickAsync(new());
    }

    [Fact]
    public void Admin_NotAnAdministrator_SeesNothing()
    {
        var cut = Render<Admin>();
        cut.WaitForAssertion(() => Assert.DoesNotContain("Code d'invitation :", cut.Text()));
        Assert.Empty(cut.FindAll("[role=tab]"));
    }

    [Fact]
    public async Task Admin_ChessTab_BalanceTables()
    {
        MakeAdmin("sacha");
        await AdminService.InitializeAsync(Db.NewContext(), Db.Config, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("administration");
        var cut = Render<Admin>();
        cut.WaitForText("Code d'invitation");
        Assert.Empty(cut.FindAll(".chess-balance-big"));

        await cut.Click("Échecs");
        cut.WaitForText("Rois menacés");
        Assert.EndsWith("administration?onglet=echecs", navigation.Uri);
        Assert.DoesNotContain("Code d'invitation", cut.Text());

        // Synthèse : 7 × 7 parts et la moyenne, celles de ChessBalance
        var psy = ChessTeams.Find("psy")!;
        var fire = ChessTeams.Find("feu")!;
        var summary = cut.FindAll("#chess-balance-summary tbody tr");
        Assert.Equal(ChessTeams.All.Count, summary.Count);
        Assert.Contains($"{ChessBalance.Points(ChessBalance.Matchup(psy, fire).Edible)} %", summary[0].QuerySelectorAll("td")[1].TextContent);
        Assert.Contains($"{ChessBalance.Points(ChessBalance.Average(psy))} %", summary[0].QuerySelectorAll("td")[^1].TextContent);
        Assert.Empty(cut.FindAll(".chess-balance-excluded"));
        // Case cochée : moyenne sur les six autres équipes, la case du miroir grisée
        cut.Find("#chess-balance-without-self").Change(true);
        Assert.Contains($"{ChessBalance.Points(ChessBalance.Average(psy, true))} %",
            cut.FindAll("#chess-balance-summary tbody tr")[0].QuerySelectorAll("td")[^1].TextContent);
        Assert.Equal(ChessTeams.All.Count, cut.FindAll(".chess-balance-excluded").Count);
        Assert.Equal($"{ChessBalance.KingThreats(fire, psy)}",
            cut.FindAll("#chess-balance-kings tbody tr")[0].QuerySelectorAll("td")[1].TextContent.Trim());
        Assert.Equal("—", cut.FindAll("#chess-balance-balance tbody tr")[0].QuerySelectorAll("td")[0].TextContent);

        // Deux grandes tables par équipe : ses 6 pièces contre les 42 des sept équipes, la sienne d'abord
        var big = cut.FindAll("table.chess-balance-big");
        Assert.Equal(2 * ChessTeams.All.Count, big.Count);
        Assert.All(big, t => Assert.Equal(6 * 6 * ChessTeams.All.Count, t.QuerySelectorAll("td").Length));
        Assert.Equal("Même équipe", big[0].QuerySelector(".chess-balance-group")!.TextContent);
        // Gallame (Combat) ne peut pas manger Malvalame (Spectre) : sans effet, marqué à part
        var noEffect = big[0].QuerySelectorAll("td.blocked").First(td => td.GetAttribute("title")!.StartsWith("Gallame ne peut pas manger Malvalame"));
        Assert.Equal("✗0", noEffect.TextContent.Replace(" ", ""));
        Assert.Contains("×0 : sans effet", noEffect.GetAttribute("title"));
        // Liens vers chaque équipe : l'adresse entière (base « / »)
        Assert.Contains(cut.FindAll(".chess-balance-nav a"), a => a.GetAttribute("href") == "administration?onglet=echecs#equipe-sol");
        Assert.NotNull(cut.Find("#equipe-sol"));

        await cut.Click("Général");
        cut.WaitForText("Code d'invitation");
        Assert.EndsWith("administration", navigation.Uri);
    }

    [Fact]
    public async Task Admin_ChessTab_FromTheAddress()
    {
        MakeAdmin("sacha");
        await AdminService.InitializeAsync(Db.NewContext(), Db.Config, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
        Services.GetRequiredService<NavigationManager>().NavigateTo("administration?onglet=echecs");
        var cut = Render<Admin>();
        cut.WaitForText("Rois menacés");
        Assert.Contains("active", cut.FindAll("[role=tab]")[1].ClassName);
    }

    // ---------- Joueurs ----------

    [Fact]
    public async Task PlayerProfile_RequestAndAccept()
    {
        var cut = Render<PlayerProfilePage>(p => p.Add(x => x.Pseudo, "ondine"));
        cut.WaitForText("Demander en ami");
        await cut.Click("Demander en ami");
        cut.WaitForAssertion(() => Assert.Contains("alert-success", cut.Markup));
        await cut.Find(".alert .btn-close").ClickAsync(new());

        Assert.True((await Friends.RequestAsync("pierre", "sacha")).Ok);
        var pierre = Render<PlayerProfilePage>(p => p.Add(x => x.Pseudo, "pierre"));
        await pierre.Click("Accepter");
        pierre.WaitForAssertion(() => Assert.Contains("alert-success", pierre.Markup));

        UpdateUser("ondine", u => u.Audience = ProfileAudience.Friends);
        Render<PlayerProfilePage>(p => p.Add(x => x.Pseudo, "ondine"));
        Render<PlayerProfilePage>(p => p.Add(x => x.Pseudo, "sacha")); // soi-même
        var unknown = Render<PlayerProfilePage>(p => p.Add(x => x.Pseudo, "personne"));
        unknown.WaitForAssertion(() => Assert.DoesNotContain("Demander en ami", unknown.Text()));
    }

    [Fact]
    public async Task PlayerSearch_FindsPlayers()
    {
        var cut = Render<PlayerSearch>();
        await cut.Find("input").InputAsync(new() { Value = "on" });
        cut.WaitForText("ondine");
        await cut.Find("input").InputAsync(new() { Value = "zzz" });
        await cut.Find("input").InputAsync(new() { Value = "" });
    }

    [Fact]
    public void Statistics_ShowTradesAndBattles()
    {
        var cut = Render<Statistics>();
        cut.WaitForText("Statistiques");
    }
}
