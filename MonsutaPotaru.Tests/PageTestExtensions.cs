using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;

namespace MonsutaPotaru.Tests;

/// <summary>Raccourcis des tests de pages : trouver un bouton par son texte, cliquer, lire le texte affiché.</summary>
internal static class PageTestExtensions
{
    /// <summary>Premier bouton (ou lien) dont le texte contient <paramref name="text"/>.</summary>
    public static IElement Button<T>(this IRenderedComponent<T> cut, string text) where T : IComponent =>
        cut.FindAll("button, a").FirstOrDefault(b => Normalize(b.TextContent).Contains(Normalize(text), StringComparison.Ordinal))
        ?? throw new InvalidOperationException($"Aucun bouton « {text} » :\n{cut.Markup}");

    public static bool HasButton<T>(this IRenderedComponent<T> cut, string text) where T : IComponent =>
        cut.FindAll("button, a").Any(b => Normalize(b.TextContent).Contains(Normalize(text), StringComparison.Ordinal));

    /// <summary>Tous les boutons qui portent ce texte.</summary>
    public static List<AngleSharp.Dom.IElement> Buttons<T>(this IRenderedComponent<T> cut, string text) where T : IComponent =>
        cut.FindAll("button, a").Where(b => Normalize(b.TextContent).Contains(Normalize(text), StringComparison.Ordinal)).ToList();

    /// <summary>Espaces regroupés (les montants s'écrivent avec des espaces insécables).</summary>
    public static string Normalize(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>
    /// Clique sur le bouton qui porte ce texte et attend la fin de son traitement. Trouvé et cliqué d'un seul tenant
    /// (<c>InvokeAsync</c>) : une page qui se redessine d'elle-même (combat Showdown, partie d'échecs, minuteries) ne peut
    /// pas remplacer le bouton entre les deux (sinon « There is no event handler with ID … », vu en CI le 2026-11-24).
    /// </summary>
    public static Task Click<T>(this IRenderedComponent<T> cut, string text) where T : IComponent =>
        cut.InvokeAsync(() => cut.Button(text).ClickAsync(new()));

    /// <summary>Clique sur l'élément n° <paramref name="index"/> de ce sélecteur, trouvé et cliqué d'un seul tenant (voir <see cref="Click{T}"/>).</summary>
    public static Task ClickOn<T>(this IRenderedComponent<T> cut, string selector, int index = 0) where T : IComponent =>
        cut.InvokeAsync(() => cut.FindAll(selector)[index].ClickAsync(new()));

    /// <summary>Clique sur l'élément que rend <paramref name="find"/> (bouton d'une carte...), trouvé et cliqué d'un seul tenant (voir <see cref="Click{T}"/>).</summary>
    public static Task ClickOn<T>(this IRenderedComponent<T> cut, Func<IRenderedComponent<T>, IElement> find) where T : IComponent =>
        cut.InvokeAsync(() => find(cut).ClickAsync(new()));

    /// <summary>Change la valeur du premier élément de ce sélecteur (liste, case, champ), trouvé et changé d'un seul tenant (voir <see cref="Click{T}"/>).</summary>
    public static Task ChangeOn<T>(this IRenderedComponent<T> cut, string selector, object? value) where T : IComponent =>
        cut.InvokeAsync(() => cut.Find(selector).ChangeAsync(new() { Value = value }));

    /// <summary>Tape cette valeur dans le premier champ de ce sélecteur (événement « input »), d'un seul tenant (voir <see cref="Click{T}"/>).</summary>
    public static Task InputOn<T>(this IRenderedComponent<T> cut, string selector, object? value) where T : IComponent =>
        cut.InvokeAsync(() => cut.Find(selector).InputAsync(new() { Value = value }));

    /// <summary>Texte de la page, espaces regroupés (pour les Assert.Contains).</summary>
    public static string Text<T>(this IRenderedComponent<T> cut) where T : IComponent =>
        Normalize(string.Concat(cut.Nodes.Select(n => n.TextContent + " ")));

    /// <summary>Attend que le texte de la page contienne <paramref name="expected"/> (traitements lancés après l'affichage).</summary>
    public static void WaitForText<T>(this IRenderedComponent<T> cut, string expected) where T : IComponent =>
        cut.WaitForAssertion(() => Assert.Contains(Normalize(expected), cut.Text()), TimeSpan.FromSeconds(10));

    /// <summary>Clique sur chacun des boutons qui portent ce texte (« Tout » de chaque ligne), relus après chaque clic.</summary>
    public static async Task ClickAll<T>(this IRenderedComponent<T> cut, string text) where T : IComponent
    {
        for (var i = 0; i < cut.Buttons(text).Count; i++)
        {
            var index = i;
            await cut.InvokeAsync(() => cut.Buttons(text)[index].ClickAsync(new()));
        }
    }
}
