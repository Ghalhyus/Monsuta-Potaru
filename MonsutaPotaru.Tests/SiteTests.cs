using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MonsutaPotaru.Data;
using MonsutaPotaru.Services;

namespace MonsutaPotaru.Tests;

/// <summary>
/// Le site entier, démarré comme en vrai (Program.cs) par WebApplicationFactory, avec sa propre base SQLite, ses copies et
/// ses clés dans un dossier temporaire : inscription, connexion, pages du compte (formulaires envoyés par le serveur), toutes
/// les pages rendues côté serveur, et l'API de la 3DS.
/// </summary>
public sealed class SiteFactory : WebApplicationFactory<Program>
{
    public const string InviteCode = "code-des-tests";
    public const string Password = "Mot-de-passe-1";
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "monsuta-potaru-site", Guid.NewGuid().ToString("N"));

    /// <summary>Base du site de test (jamais Data/app.db, la base de l'appli lancée en développement).</summary>
    public string DatabasePath => Path.Combine(_folder, "site.db");

    /// <summary>Environnement du site : Development (par défaut) ou Production (page d'erreur, HSTS, CSP stricte).</summary>
    public string EnvironmentName { get; init; } = "Development";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_folder);
        builder.UseEnvironment(EnvironmentName);
        builder.UseSetting("ConnectionStrings:DefaultConnection", $"DataSource={DatabasePath};Pooling=False");
        builder.UseSetting("Bank:BackupPath", Path.Combine(_folder, "backups"));
        builder.UseSetting("Bank:KeysPath", Path.Combine(_folder, "keys"));
        builder.UseSetting("Bank:InviteCode", InviteCode);
        builder.UseSetting("Bank:AdminPseudo", "Sacha");
    }

    /// <summary>Navigateur qui garde ses cookies (connexion, jetons antiforgery).</summary>
    public HttpClient Browser() => CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = true, HandleCookies = true });

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { Directory.Delete(_folder, recursive: true); }
        catch (IOException) { /* fichier encore ouvert : le dossier temporaire sera nettoyé plus tard */ }
    }
}

public sealed class SiteTests(SiteFactory site) : IClassFixture<SiteFactory>
{
    private static readonly HtmlParser Parser = new();

    /// <summary>
    /// Envoie un formulaire de la page comme le navigateur : ses champs (cachés compris : jeton antiforgery, nom du formulaire)
    /// avec leurs valeurs, remplacées ou complétées par <paramref name="values"/>.
    /// </summary>
    private static async Task<(HttpResponseMessage Response, string Html)> Submit(HttpClient browser, string url, string formName,
        IDictionary<string, string> values)
    {
        var page = await browser.GetStringAsync(url);
        var document = Parser.ParseDocument(page);
        var form = document.QuerySelectorAll("form").FirstOrDefault(f =>
                       f.QuerySelector($"input[name=_handler][value='{formName}']") is not null)
                   ?? throw new InvalidOperationException($"Formulaire « {formName} » absent de {url} :\n{page}");
        var fields = form.QuerySelectorAll("input[name]")
            .Where(i => i.GetAttribute("type") is not ("checkbox" or "radio") || i.HasAttribute("checked"))
            .GroupBy(i => i.GetAttribute("name")!)
            .ToDictionary(g => g.Key, g => g.First().GetAttribute("value") ?? "");
        foreach (var (name, value) in values)
            fields[name] = value;
        var action = form.GetAttribute("action") is { Length: > 0 } a ? a : url;
        var response = await browser.PostAsync(action, new FormUrlEncodedContent(fields));
        return (response, await response.Content.ReadAsStringAsync());
    }

    private static Task<(HttpResponseMessage Response, string Html)> Register(HttpClient browser, string pseudo, string email,
        string invite = SiteFactory.InviteCode) =>
        Submit(browser, "/Account/Register", "register", new Dictionary<string, string>
        {
            ["Input.Pseudo"] = pseudo,
            ["Input.Email"] = email,
            ["Input.Password"] = SiteFactory.Password,
            ["Input.ConfirmPassword"] = SiteFactory.Password,
            ["Input.InviteCode"] = invite,
        });

    private static Task<(HttpResponseMessage Response, string Html)> Login(HttpClient browser, string email, string password = SiteFactory.Password) =>
        Submit(browser, "/Account/Login", "login", new Dictionary<string, string>
        {
            ["Input.Email"] = email,
            ["Input.Password"] = password,
            ["Input.RememberMe"] = "true",
        });

    [Fact]
    public async Task Anonymous_SeesTheHome_AndIsSentToTheLogin()
    {
        var browser = site.Browser();
        var home = await browser.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, home.StatusCode);
        Assert.True(File.Exists(site.DatabasePath)); // la base temporaire, pas celle du poste
        Assert.True(home.Headers.Contains("Content-Security-Policy"));
        var page = await browser.GetAsync("/inventaire");
        Assert.Contains("/Account/Login", page.RequestMessage!.RequestUri!.ToString());
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/Account/Register")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/Account/AccessDenied")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/Account/InvalidUser")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/Error")).StatusCode);
        var json = await browser.GetAsync("/images/introuvable.json"); // en-tête « no-cache » des descriptions d'images
        Assert.Equal(HttpStatusCode.NotFound, json.StatusCode);
    }

    [Fact]
    public async Task Register_Refused_WithAWrongCode_OrInvalidFields()
    {
        var browser = site.Browser();
        var (_, wrongCode) = await Register(browser, "Refusé", "refuse@exemple.fr", invite: "mauvais");
        Assert.Contains("alert-danger", wrongCode);
        var (_, invalid) = await Submit(browser, "/Account/Register", "register", new Dictionary<string, string>
        {
            ["Input.Email"] = "pas une adresse",
            ["Input.Password"] = "court",
            ["Input.ConfirmPassword"] = "autre",
        });
        Assert.Contains("text-danger", invalid);
        var (_, login) = await Login(browser, "personne@exemple.fr", "Faux-mot-de-passe-1");
        Assert.Contains("alert-danger", login);
    }

    [Fact]
    public async Task Account_EveryPageRenders_ThenTheAccountPagesWork()
    {
        var browser = site.Browser();
        var (registered, _) = await Register(browser, "Sacha", "sacha@exemple.fr");
        Assert.Equal(HttpStatusCode.OK, registered.StatusCode);

        // Toutes les pages, rendues par le serveur (menu replié : cookie lu par App.razor)
        browser.DefaultRequestHeaders.Add("Cookie", "menu=replie");
        string[] pages =
        [
            "/", "/transfert", "/collection", "/inventaire", "/inventaire/transfert", "/parc", "/livingdex", "/livingdex/9999", "/rubans", "/rubans/9999", "/completion", "/completion/9999", "/sauvegardes",
            "/sauvegardes/3ds", "/amis", "/joueurs", "/joueurs/Sacha", "/joueurs/Sacha/collection", "/joueurs/Sacha/parc",
            "/joueurs/Sacha/inventaire", "/joueurs/Sacha/livingdex", "/joueurs/Sacha/completion", "/joueurs/Inconnu", "/echanges", "/combats",
            "/statistiques", "/administration", "/Account/Manage", "/Account/Manage/Email", "/Account/Manage/ChangePassword",
            "/Account/Manage/Themes", "/Account/Manage/Preferences", "/Account/Manage/Visibility", "/Account/Manage/PersonalData",
            "/Account/Manage/DeletePersonalData", "/Account/Pseudo",
        ];
        foreach (var url in pages)
            Assert.True((await browser.GetAsync(url)).IsSuccessStatusCode, url);

        // Thème Pokédex : le menu latéral classique sur chaque page ; puis retour à Ardoise (par défaut)
        var (themed, _) = await Submit(browser, "/Account/Manage/Themes", "theme", new Dictionary<string, string> { ["Key"] = SiteThemes.Pokedex });
        Assert.True(themed.IsSuccessStatusCode);
        foreach (var url in new[] { "/", "/inventaire", "/administration", "/amis", "/Account/Manage" })
            Assert.Contains("nav-scrollable", await browser.GetStringAsync(url));
        await Submit(browser, "/Account/Manage/Themes", "theme", new Dictionary<string, string> { ["Key"] = "inconnu" });

        // Visibilité
        var (_, visibility) = await Submit(browser, "/Account/Manage/Visibility", "visibility", new Dictionary<string, string>
        {
            ["Input.Audience"] = "Friends",
            ["Input.Collection"] = "true",
        });
        Assert.Contains("alert-success", visibility);

        // Préférences : tris par défaut enregistrés (une valeur trafiquée donne le tri d'origine), affichés, puis rétablis
        var (_, sorts) = await Submit(browser, "/Account/Manage/Preferences", "sorts", new Dictionary<string, string>
        {
            ["Action"] = "save",
            ["tri-ct"] = "nom",
            ["ordre-ct"] = "desc",
            ["tri-baies"] = "pirate",
            ["tri-collection"] = "pokedex",
            ["ordre-collection"] = "asc",
            ["tri-livingdex"] = "completion",
            ["ordre-livingdex"] = "desc",
            ["tri-rubans"] = "completion",
        });
        var saved = Parser.ParseDocument(sorts);
        Assert.Contains("Tris par défaut enregistrés.", saved.QuerySelector(".alert")?.TextContent ?? sorts);
        Assert.Equal("nom", saved.QuerySelector("select[name=tri-ct] option[selected]")?.GetAttribute("value"));
        Assert.Equal("desc", saved.QuerySelector("select[name=ordre-ct] option[selected]")?.GetAttribute("value"));
        Assert.Equal("defaut", saved.QuerySelector("select[name=tri-baies] option[selected]")?.GetAttribute("value"));
        Assert.Equal("pokedex", saved.QuerySelector("select[name=tri-collection] option[selected]")?.GetAttribute("value"));
        Assert.Equal("completion", saved.QuerySelector("select[name=tri-livingdex] option[selected]")?.GetAttribute("value"));
        Assert.Equal("completion", saved.QuerySelector("select[name=tri-rubans] option[selected]")?.GetAttribute("value"));
        var (_, reset) = await Submit(browser, "/Account/Manage/Preferences", "sorts", new Dictionary<string, string> { ["Action"] = "reset" });
        var original = Parser.ParseDocument(reset);
        Assert.Contains("Tris d'origine rétablis.", original.QuerySelector(".alert")?.TextContent ?? reset);
        Assert.Equal("arrivee", original.QuerySelector("select[name=tri-collection] option[selected]")?.GetAttribute("value"));
        Assert.Equal("desc", original.QuerySelector("select[name=ordre-collection] option[selected]")?.GetAttribute("value")); // les plus récents d'abord

        // Adresse mail : mauvais mot de passe, puis la bonne
        var (_, wrongEmail) = await Submit(browser, "/Account/Manage/Email", "change-email", new Dictionary<string, string>
        {
            ["Input.NewEmail"] = "sacha2@exemple.fr",
            ["Input.Password"] = "Faux-mot-de-passe-1",
        });
        Assert.Contains("alert-danger", wrongEmail);
        var (_, email) = await Submit(browser, "/Account/Manage/Email", "change-email", new Dictionary<string, string>
        {
            ["Input.NewEmail"] = "sacha2@exemple.fr",
            ["Input.Password"] = SiteFactory.Password,
        });
        Assert.Contains("alert-success", email);

        // Mot de passe : l'ancien faux, puis changé (et remis)
        var (_, wrongPassword) = await Submit(browser, "/Account/Manage/ChangePassword", "change-password", new Dictionary<string, string>
        {
            ["Input.OldPassword"] = "Faux-mot-de-passe-1",
            ["Input.NewPassword"] = "Nouveau-mot-2",
            ["Input.ConfirmPassword"] = "Nouveau-mot-2",
        });
        Assert.Contains("alert-danger", wrongPassword);
        var (_, changed) = await Submit(browser, "/Account/Manage/ChangePassword", "change-password", new Dictionary<string, string>
        {
            ["Input.OldPassword"] = SiteFactory.Password,
            ["Input.NewPassword"] = "Nouveau-mot-2",
            ["Input.ConfirmPassword"] = "Nouveau-mot-2",
        });
        Assert.Contains("alert-success", changed);

        // Données personnelles, puis déconnexion et reconnexion
        var tokenPage = Parser.ParseDocument(await browser.GetStringAsync("/Account/Manage/PersonalData"));
        var download = await browser.PostAsync("/Account/Manage/DownloadPersonalData", new FormUrlEncodedContent(
            tokenPage.QuerySelectorAll("form").First(f => f.GetAttribute("action")?.Contains("DownloadPersonalData") == true)
                .QuerySelectorAll("input[name]").ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "")));
        Assert.Contains("sacha2@exemple.fr", await download.Content.ReadAsStringAsync());
        var home = Parser.ParseDocument(await browser.GetStringAsync("/"));
        var logoutForm = home.QuerySelectorAll("form").First(f => f.GetAttribute("action")?.Contains("Logout") == true);
        var fields = logoutForm.QuerySelectorAll("input[name]").ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
        fields["returnUrl"] = "";
        await browser.PostAsync("/Account/Logout", new FormUrlEncodedContent(fields));
        Assert.Contains("/Account/Login", (await browser.GetAsync("/collection")).RequestMessage!.RequestUri!.ToString());
        var (_, loggedIn) = await Login(browser, "sacha2@exemple.fr", "Nouveau-mot-2");
        Assert.DoesNotContain("alert-danger", loggedIn);

        // Suppression du compte : mauvais mot de passe, puis le bon
        var (_, refused) = await Submit(browser, "/Account/Manage/DeletePersonalData", "delete-user",
            new Dictionary<string, string> { ["Input.Password"] = "Faux-mot-de-passe-1" });
        Assert.Contains("alert-danger", refused);
        var (deleted, _) = await Submit(browser, "/Account/Manage/DeletePersonalData", "delete-user",
            new Dictionary<string, string> { ["Input.Password"] = "Nouveau-mot-2" });
        Assert.True(deleted.IsSuccessStatusCode);
        var (_, gone) = await Login(browser, "sacha2@exemple.fr", "Nouveau-mot-2");
        Assert.Contains("alert-danger", gone);
    }

    [Fact]
    public async Task OldAccountWithoutPseudo_ChoosesOne()
    {
        using (var scope = site.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { UserName = "ancien@exemple.fr", Email = "ancien@exemple.fr" };
            Assert.True((await users.CreateAsync(user, SiteFactory.Password)).Succeeded);
        }
        var browser = site.Browser();
        await Login(browser, "ancien@exemple.fr");
        Assert.Contains("/Account/Pseudo", (await browser.GetAsync("/collection")).RequestMessage!.RequestUri!.ToString());
        var (_, invalid) = await Submit(browser, "/Account/Pseudo", "choose-pseudo", new Dictionary<string, string> { ["Input.Pseudo"] = "@" });
        Assert.Contains("danger", invalid);
        var (chosen, _) = await Submit(browser, "/Account/Pseudo", "choose-pseudo", new Dictionary<string, string> { ["Input.Pseudo"] = "Ancien" });
        Assert.True(chosen.IsSuccessStatusCode);
        Assert.True((await browser.GetAsync("/collection")).IsSuccessStatusCode);
    }

    [Fact]
    public async Task SyncApi_LinksAConsole_ThenSendsListsComparesAndDownloads()
    {
        var browser = site.Browser();
        await Register(browser, "Ondine", "ondine@exemple.fr");
        string code;
        using (var scope = site.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var ondine = db.Users.Single(u => u.UserName == "Ondine").Id;
            code = (await scope.ServiceProvider.GetRequiredService<SyncService>().CreatePairingCodeAsync(ondine)).Code;
        }
        var console = site.CreateClient();

        // Liaison : un mauvais code, puis le bon
        Assert.Equal(HttpStatusCode.BadRequest, (await console.PostAsJsonAsync("/api/sync/liaison", new { code = "000000", nom = "3DS" })).StatusCode);
        var link = await console.PostAsJsonAsync("/api/sync/liaison", new { code, nom = "3DS du salon" });
        Assert.Equal(HttpStatusCode.OK, link.StatusCode);
        var token = (await link.Content.ReadFromJsonAsync<SyncEndpointsLink>())!.jeton;

        Assert.Equal(HttpStatusCode.Unauthorized, (await console.GetAsync("/api/sync/compte")).StatusCode);
        foreach (var (method, url) in new[] { (HttpMethod.Post, "/api/sync/sauvegardes"), (HttpMethod.Get, "/api/sync/sauvegardes"),
                     (HttpMethod.Post, "/api/sync/sauvegardes/1/comparer"), (HttpMethod.Get, "/api/sync/sauvegardes/1") })
            Assert.Equal(HttpStatusCode.Unauthorized, (await console.SendAsync(new HttpRequestMessage(method, url))).StatusCode);
        console.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.Contains("3DS du salon", await console.GetStringAsync("/api/sync/compte"));

        // Envoi : vide, trop gros, puis une vraie sauvegarde
        Assert.Equal(HttpStatusCode.BadRequest, (await console.PostAsync("/api/sync/sauvegardes?fichier=vide.sav", new ByteArrayContent([]))).StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge,
            (await console.PostAsync("/api/sync/sauvegardes?fichier=gros.sav", new ByteArrayContent(new byte[SyncEndpointsMax + 1]))).StatusCode);
        var save = SaveSessionTests.CreateBw2Save();
        var upload = await console.PostAsync("/api/sync/sauvegardes?fichier=noir2.sav&cle=sdmc:/noir2.sav", new ByteArrayContent(save));
        Assert.Contains("ajoutee", await upload.Content.ReadAsStringAsync());
        Assert.Contains("inchangee", await (await console.PostAsync("/api/sync/sauvegardes?fichier=noir2.sav", new ByteArrayContent(save))).Content.ReadAsStringAsync());

        // Liste, comparaison, récupération
        var list = await console.GetStringAsync("/api/sync/sauvegardes");
        var id = System.Text.Json.JsonDocument.Parse(list).RootElement.GetProperty("sauvegardes")[0].GetProperty("id").GetInt32();
        Assert.Contains("a-jour", await (await console.PostAsync($"/api/sync/sauvegardes/{id}/comparer", new ByteArrayContent(save))).Content.ReadAsStringAsync());
        Assert.Contains("refusee", await (await console.PostAsync("/api/sync/sauvegardes/9999/comparer", new ByteArrayContent(save))).Content.ReadAsStringAsync());
        Assert.Contains("refusee", await (await console.PostAsync($"/api/sync/sauvegardes/{id}/comparer", new ByteArrayContent([1, 2, 3]))).Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge,
            (await console.PostAsync($"/api/sync/sauvegardes/{id}/comparer", new ByteArrayContent(new byte[SyncEndpointsMax + 1]))).StatusCode);
        Assert.Equal(save.Length, (await console.GetByteArrayAsync($"/api/sync/sauvegardes/{id}?cle=sdmc:/noir2.sav")).Length);
        Assert.Equal(HttpStatusCode.NotFound, (await console.GetAsync("/api/sync/sauvegardes/9999")).StatusCode);
    }

    private const int SyncEndpointsMax = MonsutaPotaru.Api.SyncEndpoints.MaxSaveBytes;

    private sealed record SyncEndpointsLink(string jeton, string compte, string message);
}

/// <summary>Le site en production : page d'erreur, HSTS, CSP sans les exceptions de développement.</summary>
public sealed class ProductionSiteTests : IDisposable
{
    private readonly SiteFactory _site = new() { EnvironmentName = "Production" };

    public void Dispose() => _site.Dispose();

    [Fact]
    public async Task Home_UsesTheStrictPolicy()
    {
        var home = await _site.CreateClient().GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, home.StatusCode);
        Assert.DoesNotContain("localhost", string.Join(" ", home.Headers.GetValues("Content-Security-Policy")));
        Assert.Equal(HttpStatusCode.OK, (await _site.CreateClient().GetAsync("/Error")).StatusCode);
    }
}
