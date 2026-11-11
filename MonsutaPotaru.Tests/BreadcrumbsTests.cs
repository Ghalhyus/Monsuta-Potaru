using MonsutaPotaru.Components.Layout;

namespace MonsutaPotaru.Tests;

/// <summary>Fil d'Ariane du thème Ardoise : chemin de la page dans le menu, déduit de son adresse.</summary>
public class BreadcrumbsTests
{
    private static string Labels(string url) => string.Join(" › ", Breadcrumbs.For(url).Select(c => c.Label));

    [Theory]
    [InlineData("", "Accueil")]
    [InlineData("transfert", "Mes Pokémon › Dépôt / Retrait")]
    [InlineData("banque", "Mes Pokémon › Dépôt / Retrait")]
    [InlineData("collection?tri=numero", "Mes Pokémon › Ma collection")]
    [InlineData("parc", "Mes Pokémon › Parc")]
    [InlineData("inventaire", "Inventaire › Banque d'objets")]
    [InlineData("inventaire/transfert?gauche=1&droite=2", "Inventaire › Transfert d'inventaire")]
    [InlineData("echange-interne?de=1&vers=2", "Mes Pokémon › Échange interne")]
    [InlineData("livingdex", "Défis › Living dex")]
    [InlineData("livingdex/12", "Défis › Living dex › Détail")]
    [InlineData("rubans?tri=nom", "Défis › Rubans")]
    [InlineData("rubans/7", "Défis › Rubans › Détail")]
    [InlineData("completion", "Défis › Complétion (100 %)")]
    [InlineData("completion/7?section=objets", "Défis › Complétion (100 %) › Détail")]
    [InlineData("sauvegardes", "Sauvegardes › Mes sauvegardes")]
    [InlineData("sauvegardes/3ds", "Sauvegardes › Synchronisation 3DS")]
    [InlineData("echanges", "Multijoueurs › Échange")]
    [InlineData("combats", "Multijoueurs › Combat")]
    [InlineData("amis", "Multijoueurs › Amis")]
    [InlineData("joueurs", "Multijoueurs › Rechercher un joueur")]
    [InlineData("joueurs/Sacha", "Multijoueurs › Rechercher un joueur › Sacha")]
    [InlineData("joueurs/Sacha/parc", "Multijoueurs › Rechercher un joueur › Sacha › Parc")]
    [InlineData("joueurs/Sacha/inventaire", "Multijoueurs › Rechercher un joueur › Sacha › Inventaire")]
    [InlineData("joueurs/Sacha/livingdex/3", "Multijoueurs › Rechercher un joueur › Sacha › Living dex › Détail")]
    [InlineData("joueurs/Sacha/completion", "Multijoueurs › Rechercher un joueur › Sacha › Complétion (100 %)")]
    [InlineData("joueurs/Sacha/completion/3", "Multijoueurs › Rechercher un joueur › Sacha › Complétion (100 %) › Détail")]
    [InlineData("statistiques", "Mon compte › Statistiques")]
    [InlineData("statistiques?onglet=multijoueurs", "Mon compte › Statistiques")]
    [InlineData("joueurs/Sacha/statistiques", "Multijoueurs › Rechercher un joueur › Sacha › Statistiques")]
    [InlineData("Account/Manage", "Mon compte › Profil")]
    [InlineData("Account/Manage/Themes", "Mon compte › Thèmes")]
    [InlineData("Account/Manage/Preferences", "Mon compte › Préférences")]
    [InlineData("Account/Manage/DeletePersonalData", "Mon compte › Données personnelles › Suppression du compte")]
    [InlineData("Account/Login?ReturnUrl=%2Fparc", "Connexion")]
    [InlineData("page-inconnue", "")]
    public void For_FollowsTheMenu(string url, string expected) => Assert.Equal(expected, Labels(url));

    [Fact]
    public void For_LinksEveryStepButTheLast()
    {
        var crumbs = Breadcrumbs.For("joueurs/Jean%20Luc/livingdex/3");

        Assert.Equal([null, "joueurs", "joueurs/Jean%20Luc", "joueurs/Jean%20Luc/livingdex", null], crumbs.Select(c => c.Href));
        Assert.Equal("Jean Luc", crumbs[2].Label); // pseudo décodé pour l'affichage, encodé dans le lien
    }

    [Fact]
    public void For_SectionsHaveNoLink()
    {
        // Les rubriques du menu n'ont pas de page à elles : seule la liste des living dex est un lien.
        Assert.Equal([null, "livingdex", null], Breadcrumbs.For("livingdex/4").Select(c => c.Href));
        Assert.Equal([null, "rubans", null], Breadcrumbs.For("rubans/4").Select(c => c.Href));
        Assert.Equal([null, "completion", null], Breadcrumbs.For("completion/4").Select(c => c.Href));
        Assert.Equal([null, null], Breadcrumbs.For("inventaire/transfert").Select(c => c.Href));
    }
}
