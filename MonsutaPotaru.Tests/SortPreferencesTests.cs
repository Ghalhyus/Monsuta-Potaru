using MonsutaPotaru.Services;

namespace MonsutaPotaru.Tests;

/// <summary>Tris par défaut du compte (« Mon compte » → « Préférences ») : JSON en base, lecture de l'adresse, service.</summary>
public sealed class SortPreferencesTests : IDisposable
{
    private readonly TestDatabase _db = new("sacha");

    public void Dispose() => _db.Dispose();

    private static SortPreferences Sample() => new()
    {
        Inventory = new Dictionary<ItemCategory, ItemSort>
        {
            [ItemCategory.Machines] = new(ItemSortBy.Name),
            [ItemCategory.Berries] = new(ItemSortBy.Quantity, Descending: true),
            [ItemCategory.Items] = default, // le tri d'origine n'est pas écrit
        },
        Collection = new(CollectionSortBy.Dex, Reversed: true),
        LivingDex = new(LivingDexSort.Completion, Descending: true),
        Ribbons = new(RibbonSort.Completion),
    };

    [Fact]
    public void Json_KeepsEverySort_AndOnlyWhatDiffersFromTheSite()
    {
        var json = Sample().ToJson();
        Assert.Equal("""{"inventaire":{"ct":{"tri":"nom"},"baies":{"tri":"quantite","ordre":"desc"}},"collection":{"tri":"pokedex","ordre":"desc"},"livingdex":{"tri":"completion","ordre":"desc"},"rubans":{"tri":"completion"}}""", json);
        var read = SortPreferences.FromJson(json);
        Assert.Equal(new ItemSort(ItemSortBy.Name), read.InventoryFor(ItemCategory.Machines));
        Assert.Equal(new ItemSort(ItemSortBy.Quantity, true), read.InventoryFor(ItemCategory.Berries));
        Assert.Equal(default, read.InventoryFor(ItemCategory.Items));
        Assert.Equal(new CollectionSort(CollectionSortBy.Dex, true), read.Collection);
        Assert.Equal(new LivingDexOrder(LivingDexSort.Completion, true), read.LivingDex);
        Assert.Equal(new RibbonOrder(RibbonSort.Completion), read.Ribbons);
        Assert.Null(SortPreferences.Default.ToJson()); // tout d'origine : colonne vide
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("pas du json")]
    [InlineData("null")]
    [InlineData("""{"inventaire":{"inconnu":{"tri":"nom"},"ct":null},"collection":{"tri":"pirate"}}""")]
    public void Json_UnreadableOrUnknown_GivesTheSiteSorts(string? json)
    {
        var read = SortPreferences.FromJson(json);
        Assert.Empty(read.Inventory);
        Assert.Equal(default, read.Collection);
        Assert.Equal(default, read.LivingDex);
    }

    [Fact]
    public void Address_ReadsEachPage_WithItsOwnDirection()
    {
        Assert.Null(SiteSorts.ItemFromQuery(new(null, null)));
        Assert.Equal(new ItemSort(ItemSortBy.Default, true), SiteSorts.ItemFromQuery(new(null, "desc")));
        Assert.Equal(new ItemSort(ItemSortBy.Default), SiteSorts.ItemFromQuery(new("pirate", "asc")));

        // Collection : « ordre » donne le sens réel ; l'arrivée est décroissante d'elle-même
        Assert.Null(SiteSorts.CollectionFromQuery(new(null, null)));
        Assert.Equal(new CollectionSort(CollectionSortBy.Arrival), SiteSorts.CollectionFromQuery(new("arrivee", null)));
        Assert.Equal(new CollectionSort(CollectionSortBy.Arrival), SiteSorts.CollectionFromQuery(new("arrivee", "desc")));
        Assert.Equal(new CollectionSort(CollectionSortBy.Arrival, true), SiteSorts.CollectionFromQuery(new(null, "asc")));
        Assert.Equal(new CollectionSort(CollectionSortBy.Name, true), SiteSorts.CollectionFromQuery(new("nom", "desc")));
        Assert.Equal(new SortQuery("arrivee", "asc"), SiteSorts.CollectionQuery(new(CollectionSortBy.Arrival, true)));
        Assert.Equal(new SortQuery("nom", "desc"), SiteSorts.CollectionQuery(new(CollectionSortBy.Name, true)));
        Assert.Equal(new SortQuery("pokedex", null), SiteSorts.CollectionQuery(new(CollectionSortBy.Dex)));
        Assert.True(SiteSorts.IsDescending(default));

        // Quantité : reconnue en mode compact seulement (ailleurs, et dans les préférences, comme une valeur inconnue)
        Assert.Equal(new CollectionSort(CollectionSortBy.Quantity), SiteSorts.CollectionFromQuery(new("quantite", null), compact: true));
        Assert.Equal(new CollectionSort(CollectionSortBy.Quantity, true), SiteSorts.CollectionFromQuery(new("quantite", "asc"), compact: true));
        Assert.Equal(new CollectionSort(CollectionSortBy.Arrival), SiteSorts.CollectionFromQuery(new("quantite", null)));
        Assert.True(SiteSorts.IsDescending(new(CollectionSortBy.Quantity)));
        Assert.Equal(new SortQuery("quantite", "asc"), SiteSorts.CollectionQuery(new(CollectionSortBy.Quantity, true)));
        Assert.Equal(new CollectionSort(), SortPreferences.FromJson("""{"collection":{"tri":"quantite"}}""").Collection);
        Assert.DoesNotContain(SiteSorts.CollectionChoices, c => c.By == CollectionSortBy.Quantity);

        Assert.Null(SiteSorts.LivingDexFromQuery(new(null, null)));
        Assert.Equal(new LivingDexOrder(LivingDexSort.Generation, true), SiteSorts.LivingDexFromQuery(new("generation", "desc")));
        Assert.Equal(new SortQuery("completion", null), SiteSorts.LivingDexQuery(new(LivingDexSort.Completion)));
        Assert.Null(SiteSorts.RibbonFromQuery(new(null, null)));
        Assert.Equal(new RibbonOrder(RibbonSort.Name, true), SiteSorts.RibbonFromQuery(new("nom", "desc")));
        Assert.Equal(new RibbonOrder(), SiteSorts.RibbonFromQuery(new("pirate", null))); // trafiqué : le tri d'origine
        Assert.Equal(new SortQuery("ajout", "desc"), SiteSorts.RibbonQuery(new(RibbonSort.Arrival, true)));
    }

    [Fact]
    public void Address_WritesNothingForTheAccountSort()
    {
        var accountDefault = new SortQuery("nom", null);
        Assert.Equal(new Dictionary<string, object?> { ["tri"] = null, ["ordre"] = null }, SiteSorts.AddressFor(new("nom", null), accountDefault));
        Assert.Equal(new Dictionary<string, object?> { ["tri"] = "arrivee", ["ordre"] = null }, SiteSorts.AddressFor(new("arrivee", null), accountDefault));
    }

    [Fact]
    public async Task Service_SavesAndReadsTheSorts_OfAnExistingAccount()
    {
        var service = new PreferencesService(_db.Factory);
        Assert.Same(SortPreferences.Default, await service.SortsAsync(null));
        Assert.Empty((await service.SortsAsync("sacha")).Inventory);

        var saved = await service.SetSortsAsync("sacha", Sample());
        Assert.True(saved.Ok);
        Assert.Equal("Tris par défaut enregistrés.", saved.Message);
        Assert.Equal(new CollectionSort(CollectionSortBy.Dex, true), (await service.SortsAsync("sacha")).Collection);

        Assert.True((await service.SetSortsAsync("sacha", SortPreferences.Default)).Ok);
        using (var db = _db.NewContext())
            Assert.Null(db.Users.Single(u => u.Id == "sacha").SortPreferences); // tris d'origine : colonne vide

        var gone = await service.SetSortsAsync("disparu", Sample());
        Assert.False(gone.Ok);
        Assert.Equal("Ce compte n'existe plus.", gone.Message);
        Assert.Same(SortPreferences.Default, await service.SortsAsync("disparu"));
    }
}
