using Bunit;
using MonsutaPotaru.Components.Pages;
using MonsutaPotaru.Components.Shared;
using MonsutaPotaru.Services;

namespace MonsutaPotaru.Tests;

/// <summary>
/// Pages avec un monde Minecraft : « Mes sauvegardes » (ajout par zip ou par dossier, mise à jour, suppression) et
/// « Dépôt / Retrait » (dossier choisi). Le JavaScript qui trie le dossier est simulé : on appelle directement ce que le
/// composant reçoit (<see cref="WorldFolderPicker.ReceiveAsync"/>, <see cref="WorldFolderPicker.OnWorldRefused"/>).
/// </summary>
public sealed class StoredWorldPageTests : PageTestContext
{
    private static Task Receive<T>(IRenderedComponent<T> cut, int picker, byte[] payload, string folder = "Kantalia")
        where T : class, Microsoft.AspNetCore.Components.IComponent =>
        cut.InvokeAsync(() => cut.FindComponents<WorldFolderPicker>()[picker].Instance.ReceiveAsync(folder, payload));

    private static Task Refuse<T>(IRenderedComponent<T> cut, string code) where T : class, Microsoft.AspNetCore.Components.IComponent =>
        cut.InvokeAsync(() => cut.FindComponent<WorldFolderPicker>().Instance.OnWorldRefused(code));

    private static byte[] Payload(byte[] zip) =>
        CobblemonTests.FolderPayload([.. CobblemonTests.Unzip(zip).Select(f => (f.Key, f.Value))]);

    [Fact]
    public async Task Saves_WorldByZipOrFolder_UpdateAndDelete()
    {
        var cut = Render<Saves>();
        cut.WaitForText("Ajouter un monde Minecraft");

        // Dossier refusé par le navigateur, puis envoi trafiqué
        await Refuse(cut, "pas-un-monde");
        cut.WaitForText(WorldFolderPicker.NotAWorldMessage);
        await Refuse(cut, "trop-gros");
        cut.WaitForText(WorldFolderPicker.TooBigMessage);
        await Receive(cut, 0, [1, 2, 3]);
        cut.WaitForText("Envoi du dossier illisible");

        // Ajout par le dossier du monde
        await Receive(cut, 0, CobblemonTests.RealFolderPayload());
        cut.WaitForText("Monde « Kantalia » stocké : 1 Pokémon");
        Assert.Contains("All the Mons — Kantalia", cut.Text());
        Assert.DoesNotContain("()", cut.Find(".list-group-item").TextContent);
        Assert.Contains("Kantalia.zip", cut.Text());

        // Mise à jour par le dossier (bouton de la ligne), plus de temps de jeu
        await Receive(cut, 1, Payload(CobblemonTests.WorldVariant(playTimeSeconds: 9000, pc: [CobblemonTests.Mon("eevee", 1)])));
        cut.WaitForText("Monde mis à jour : 2 Pokémon.");
        Assert.Contains("2 Pokémon", cut.Find(".list-group-item").TextContent);
        // Moins de temps de jeu : confirmation
        await Receive(cut, 1, Payload(CobblemonTests.WorldVariant(playTimeSeconds: 60)));
        cut.WaitForText("Remplacer quand même");
        await cut.Click("Remplacer quand même");
        cut.WaitForText("Monde mis à jour : 1 Pokémon.");

        // Suppression : le dossier pokemon est rendu
        await cut.Click("Supprimer");
        cut.WaitForText("Le dossier pokemon du monde va être");
        await cut.Click("Télécharger et supprimer");
        cut.WaitForText("téléchargée et retirée du serveur");

        // Ajout par un zip, comme une sauvegarde
        await BankPageTests.Upload(cut, new BankPageTests.FakeBrowserFile("Kantalia.zip", CobblemonTests.World()));
        cut.WaitForText("Monde « Kantalia » stocké");
        // Un monde se met à jour par son dossier seulement : pas d'envoi de fichier sur sa ligne
        Assert.Single(cut.FindComponents<Microsoft.AspNetCore.Components.Forms.InputFile>());
        Assert.DoesNotContain("(zip)", cut.Find(".list-group-item").TextContent);
    }

    [Fact]
    public async Task Transfer_WorldFolder_AndStoredWorldRefused()
    {
        var cut = Render<Bank>();
        cut.WaitForText("Choisir le dossier du monde");
        await Refuse(cut, "pas-un-monde");
        cut.WaitForText(WorldFolderPicker.NotAWorldMessage);

        await Receive(cut, 0, CobblemonTests.RealFolderPayload());
        cut.WaitForText("Sauvegarde chargée : monde Minecraft All the Mons.");
        Assert.Contains("Kantalia.zip", cut.Text());
        await cut.Click("Changer de fichier");

        // Stocké dans « Mes sauvegardes » : refusé ici
        Assert.True((await StoredSaves.AddAsync("sacha", "Kantalia.zip", CobblemonTests.World())).Ok);
        await Receive(cut, 0, CobblemonTests.RealFolderPayload());
        cut.WaitForText("déjà stockée dans ta collection");
    }
}
