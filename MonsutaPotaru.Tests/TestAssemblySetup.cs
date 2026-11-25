using System.Runtime.CompilerServices;
using MonsutaPotaru.Services;

namespace MonsutaPotaru.Tests;

/// <summary>
/// Préparation commune à tous les tests, faite une fois au chargement de l'assemblage (avant le premier test).
/// </summary>
internal static class TestAssemblySetup
{
    /// <summary>
    /// Liste des images (<see cref="PokemonSprites"/>) et types de cases des cartes du parc (<see cref="ParkHabitats"/>) :
    /// des états statiques, que des classes de tests chargeaient elles-mêmes pendant que d'autres, lancées en même temps
    /// (xUnit fait tourner les classes en parallèle), les lisaient. Le résultat dépendait alors de l'ordre des classes,
    /// qui n'est pas le même sur le PC et en CI. Chargés ici dès le départ, ils sont les mêmes pour tous les tests. Sans
    /// les images, la liste reste vide (les tests qui en ont besoin le disent par <see cref="RealSavesTests.WebRootWithImages"/>).
    /// </summary>
    [ModuleInitializer]
    internal static void LoadSharedData()
    {
        var webRoot = RealSavesTests.WebRoot();
        PokemonSprites.Load(webRoot);
        ParkHabitats.Load(webRoot);
    }
}
