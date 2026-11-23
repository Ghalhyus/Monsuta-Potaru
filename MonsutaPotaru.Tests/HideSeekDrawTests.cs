using MonsutaPotaru.Data;
using MonsutaPotaru.Services;
using PKHeX.Core;

namespace MonsutaPotaru.Tests;

/// <summary>
/// Cache-cache : réglages revérifiés (<see cref="HideSeekSettings"/>), tirage des tours (<see cref="HideSeekDraw"/> : un
/// Pokémon par espèce et forme dans un parc, cibles différentes d'un tour à l'autre et venues du choix de l'hôte, paysage
/// respecté, parc complété par des Pokémon aléatoires) et Pokémon des collections (<see cref="HideSeekService.PoolAsync"/>).
/// </summary>
public sealed class HideSeekDrawTests : IDisposable
{
    private readonly TestDatabase _db = new("sacha", "ondine", "pierre");

    public void Dispose() => _db.Dispose();

    private static readonly HideSeekParty Sacha = new("sacha", "Sacha");
    private static readonly HideSeekParty Ondine = new("ondine", "Ondine");

    /// <summary>Un Pokémon d'une collection (types de l'espèce, comme BankPokemon.Type1).</summary>
    internal static HideSeekCandidate Mon(ushort species, string? owner = "Sacha", string? nickname = null, bool shiny = false)
    {
        var (type1, type2) = PokemonTraits.Types(species, 0);
        var name = SpeciesName.GetSpeciesName(species, (int)LanguageID.French);
        return new HideSeekCandidate(owner, species, 0, EntityContext.Gen9, shiny, false, 0, name, nickname ?? name, type1, type2, 0);
    }

    /// <summary>Les Pokémon aléatoires, toutes les espèces comptées comme ayant des sprites.</summary>
    internal static IReadOnlyList<HideSeekCandidate> Fill { get; } = HideSeekDraw.RandomPool(_ => true);

    [Fact]
    public void Settings_AreCheckedAndGiveTheMapSize()
    {
        Assert.Null(new HideSeekSettings().Error);
        Assert.Equal(2, new HideSeekSettings().MapSize); // 40 Pokémon : la 2e taille
        Assert.Equal(1, new HideSeekSettings(Count: 20).MapSize);
        Assert.Equal(3, new HideSeekSettings(Count: 41).MapSize);
        Assert.Equal(4, new HideSeekSettings(Count: 80).MapSize);
        Assert.Equal("Le nombre de tours va de 1 à 30.", new HideSeekSettings(Rounds: 0).Error);
        Assert.Equal("Le nombre de tours va de 1 à 30.", new HideSeekSettings(Rounds: 31).Error);
        Assert.Equal("Un tour dure de 15 secondes à 5 minutes.", new HideSeekSettings(RoundSeconds: 10).Error);
        Assert.Equal("Le parc compte de 20 à 80 Pokémon.", new HideSeekSettings(Count: 19).Error);
        Assert.Equal("Le parc compte de 20 à 80 Pokémon.", new HideSeekSettings(Count: 81).Error);
        Assert.NotNull(new HideSeekSettings(Pool: (HideSeekPool)9).Error);
        Assert.NotNull(new HideSeekSettings(Landscape: (ParkLandscape)99).Error);
        // Pas de surnom pour des Pokémon aléatoires
        Assert.True(new HideSeekSettings(NicknamedOnly: true).Nicknamed);
        Assert.False(new HideSeekSettings(NicknamedOnly: true, Pool: HideSeekPool.Random).Nicknamed);
        Assert.Equal("45 s", HideSeekSettings.Duration(45));
        Assert.Equal("1 min", HideSeekSettings.Duration(60));
        Assert.Equal("1 min 30", HideSeekSettings.Duration(90));
    }

    [Fact]
    public void Draw_UniquePokemonPerPark_TargetsFromThePool_NeverTwice()
    {
        // Trois Pikachu (dont un chromatique) comptent pour une seule espèce
        List<HideSeekCandidate> pool = [Mon(25), Mon(25, shiny: true), Mon(25, nickname: "Bob"), Mon(1), Mon(4), Mon(7), Mon(133)];
        var settings = new HideSeekSettings(Rounds: 5, Count: 30, Landscape: ParkLandscape.Forest);
        var (rounds, error) = HideSeekDraw.Draw(settings, pool, Fill, new Random(1));
        Assert.Null(error);
        Assert.Equal(5, rounds!.Count);
        var poolSpecies = pool.Select(p => p.Species).ToHashSet();
        foreach (var round in rounds)
        {
            Assert.Equal(ParkLandscape.Forest, round.Landscape);
            Assert.Equal(2, round.MapSize);
            Assert.Equal(30, round.Pokemon.Count);
            Assert.Equal(Enumerable.Range(1, 30), round.Pokemon.Select(p => p.Id)); // numéros 1 à 30, dans le désordre des Pokémon
            Assert.Equal(30, round.Pokemon.Select(p => p.Species).Distinct().Count()); // ici, une forme par espèce
            Assert.Contains(round.Target.Species, poolSpecies);
            // Tout le choix de l'hôte est dans le parc (une fois par espèce), le reste vient des aléatoires
            Assert.True(poolSpecies.All(s => round.Pokemon.Any(p => p.Species == s)));
            Assert.Equal(poolSpecies.Count, round.Pokemon.Count(p => p.OwnerPseudo == "Sacha"));
        }
        Assert.Equal(5, rounds.Select(r => r.Target.Species).Distinct().Count()); // une cible ne revient pas
    }

    [Fact]
    public void Draw_TheTargetCanBeNicknamed_AndRandomLandscapesChange()
    {
        List<HideSeekCandidate> pool = [.. Enumerable.Range(1, 40).Select(s => Mon((ushort)s, nickname: $"Nom{s}"))];
        var (rounds, _) = HideSeekDraw.Draw(new HideSeekSettings(Rounds: 30, Count: 20), pool, Fill, new Random(3));
        Assert.All(rounds!, r => Assert.StartsWith("Nom", r.Target.Name));
        Assert.Contains("(", rounds![0].Target.Name); // « Nom12 (Évoli) »
        Assert.True(rounds.Select(r => r.Landscape).Distinct().Count() > 2);
        Assert.All(rounds, r => Assert.Equal(1, r.MapSize));
    }

    [Fact]
    public void Draw_Ocean_OnlySwimmers()
    {
        // Magicarpe (Eau), Pikachu (Électrik), Psykokwak (Eau) : le Pikachu n'y va pas
        List<HideSeekCandidate> pool = [Mon(129), Mon(25), Mon(54)];
        var settings = new HideSeekSettings(Rounds: 2, Count: 25, Landscape: ParkLandscape.Ocean);
        var (rounds, error) = HideSeekDraw.Draw(settings, pool, Fill, new Random(5));
        Assert.Null(error);
        Assert.All(rounds!.SelectMany(r => r.Pokemon), p => Assert.Contains("swim", p.Moves));
        Assert.Equal([54, 129], rounds!.Select(r => (int)r.Target.Species).Order());

        // Trois tours : il n'y a que deux nageurs dans la collection
        var tooMany = HideSeekDraw.Draw(settings with { Rounds = 3 }, pool, Fill, new Random(5));
        Assert.Null(tooMany.Rounds);
        Assert.Equal("Ta collection a 2 Pokémon différents qui peuvent vivre dans l'océan : il en faut un par tour (3). " +
                     "Baisse le nombre de tours ou change les réglages.", tooMany.Error);
    }

    [Fact]
    public void Check_EmptyOrTooSmallPool()
    {
        Assert.Equal("Aucun Pokémon surnommés dans ce choix : change les réglages.",
            HideSeekDraw.Check(new HideSeekSettings(NicknamedOnly: true), []));
        Assert.Equal("Vos collections ont 1 Pokémon différents : il en faut un par tour (5). Baisse le nombre de tours ou change les réglages.",
            HideSeekDraw.Check(new HideSeekSettings(Pool: HideSeekPool.AllPlayers), [Mon(25), Mon(25, shiny: true)]));
        Assert.Null(HideSeekDraw.Check(new HideSeekSettings(Rounds: 1), [Mon(25)]));
        Assert.NotNull(HideSeekDraw.Draw(new HideSeekSettings(Rounds: 0), [Mon(25)], Fill, new Random()).Error);
    }

    [Fact]
    public void RandomPool_EverySpeciesWithSprites_NoNickname()
    {
        var pool = HideSeekDraw.RandomPool(s => s is 25 or 150);
        Assert.Equal([25, 150], pool.Select(p => (int)p.Species));
        Assert.All(pool, p => Assert.False(p.IsNicknamed));
        Assert.Null(pool[0].OwnerPseudo);
        Assert.Equal(HideSeekDraw.LastSpecies, Fill.Count);
    }

    [Fact]
    public async Task Pool_ComesFromTheCollections_NicknamedOnlyIfAsked()
    {
        using (var db = _db.NewContext())
        {
            db.BankPokemon.AddRange(
                Row("sacha", 25, "Pikachu", "Pikachu"),
                Row("sacha", 1, "Bulbizarre", "Bulbi"),
                Row("sacha", 4, "Salamèche", "Salamèche", egg: true),
                Row("ondine", 7, "Carapuce", "Cara"),
                Row("pierre", 74, "Racaillou", "Caillou"));
            db.SaveChanges();
        }
        var service = new HideSeekService(_db.Factory);
        HideSeekParty[] players = [Sacha, Ondine, new("invite-123", "Léo (invité)")];

        var host = await service.PoolAsync(new HideSeekSettings(), Sacha, players, _ => true);
        Assert.Equal([1, 25], host.Select(p => (int)p.Species).Order()); // pas l'œuf
        Assert.All(host, p => Assert.Equal("Sacha", p.OwnerPseudo));

        var all = await service.PoolAsync(new HideSeekSettings(Pool: HideSeekPool.AllPlayers), Sacha, players, _ => true);
        Assert.Equal([1, 7, 25], all.Select(p => (int)p.Species).Order()); // pas Pierre : il ne joue pas
        Assert.Equal("Ondine", all.Single(p => p.Species == 7).OwnerPseudo);

        var nicknamed = await service.PoolAsync(new HideSeekSettings(Pool: HideSeekPool.AllPlayers, NicknamedOnly: true), Sacha, players, _ => true);
        Assert.Equal(["Bulbi (Bulbizarre)", "Cara (Carapuce)"], nicknamed.Select(p => p.DisplayName).Order());

        Assert.Empty(await service.PoolAsync(new HideSeekSettings(), Sacha, players, _ => false)); // sans sprites : pas au parc
        Assert.Equal(2, (await service.PoolAsync(new HideSeekSettings(Pool: HideSeekPool.Random), Sacha, players, s => s is 1 or 2)).Count);
    }

    private static BankPokemon Row(string owner, ushort species, string name, string nickname, bool egg = false)
    {
        var (type1, type2) = PokemonTraits.Types(species, 0);
        return new BankPokemon
        {
            OwnerId = owner, Species = species, SpeciesName = name, Nickname = nickname, IsEgg = egg, Level = 10,
            Context = (byte)EntityContext.Gen5, Type1 = type1, Type2 = type2, Data = [1], DepositedAt = DateTime.UtcNow,
        };
    }
}
