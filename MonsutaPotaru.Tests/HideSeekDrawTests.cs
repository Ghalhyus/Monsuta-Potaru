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
        Assert.Equal(5, rounds.Select(r => r.Seed).Distinct().Count()); // graine du parc (parc.js, sync) : une par tour
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
    public void Draw_WithAMood_EveryPokemonOfTheParkHasIt_TheTargetToo()
    {
        ParkHabitats.Load(RealSavesTests.WebRoot()); // types de cases des cartes : humeur des Pokémon
        Assert.NotNull(new HideSeekSettings(Mood: (HideSeekMood)9).Error);
        Assert.Equal("Heureux seulement", HideSeekSettings.MoodName(HideSeekMood.Happy));
        // À la plage (deux tiers de mer, 2e taille), les Pokémon aléatoires ont toutes les humeurs : le filtre a de quoi trier
        foreach (var mood in new[] { ParkMood.Happy, ParkMood.Normal, ParkMood.Sad })
            Assert.Contains(Fill, c => HideSeekDraw.MoodIn(c, ParkLandscape.Beach, 2) == mood);
        // Eau (Carapuce, Psykokwak, Magicarpe, Stari, Tentacool) et Feu (Salamèche, Goupix, Caninos, Ponyta)
        List<HideSeekCandidate> pool = [.. new ushort[] { 7, 54, 129, 120, 72, 4, 37, 58, 77 }.Select(s => Mon(s))];
        foreach (var (mood, allowed) in new (HideSeekMood, ParkMood[])[]
                 {
                     (HideSeekMood.Happy, [ParkMood.Happy]),
                     (HideSeekMood.NotSad, [ParkMood.Happy, ParkMood.Normal]),
                 })
        {
            var settings = new HideSeekSettings(Rounds: 3, Count: 40, Landscape: ParkLandscape.Beach, Mood: mood);
            var (rounds, error) = HideSeekDraw.Draw(settings, pool, Fill, new Random(7));
            Assert.Null(error);
            foreach (var round in rounds!)
            {
                Assert.Equal(40, round.Pokemon.Count); // complété par des aléatoires qui ont aussi la bonne humeur
                Assert.All(round.Pokemon, p => Assert.Contains(HideSeekDraw.MoodIn(Mon(p.Species), ParkLandscape.Beach, 2), allowed));
                Assert.Contains(HideSeekDraw.MoodIn(Mon(round.Target.Species), ParkLandscape.Beach, 2), allowed);
                Assert.Equal("Sacha", round.Target.OwnerPseudo);
            }
        }
        // Aucun Pokémon Feu n'est heureux à la plage
        var fire = new HideSeekSettings(Rounds: 1, Landscape: ParkLandscape.Beach, Mood: HideSeekMood.Happy);
        Assert.Equal("Aucun Pokémon qui peuvent vivre à la plage (heureux dans le parc) dans ce choix : change les réglages.",
            HideSeekDraw.Check(fire, [Mon(4), Mon(37), Mon(58)]));
        Assert.Equal(0, HideSeekDraw.Usable(fire, [Mon(4), Mon(37)], ParkLandscape.Beach));
    }

    [Fact]
    public void Draw_WithAMood_AndRandomLandscapes_EachRoundGoesWhereItsTargetIsHappy()
    {
        ParkHabitats.Load(RealSavesTests.WebRoot());
        List<HideSeekCandidate> pool = [.. Enumerable.Range(1, 151).Select(s => Mon((ushort)s))];
        var settings = new HideSeekSettings(Rounds: 20, Count: 20, Mood: HideSeekMood.Happy);
        var (rounds, error) = HideSeekDraw.Draw(settings, pool, Fill, new Random(11));
        Assert.Null(error);
        Assert.All(rounds!, r => Assert.All(r.Pokemon, p => Assert.Equal(ParkMood.Happy, HideSeekDraw.MoodIn(Mon(p.Species), r.Landscape, 1))));
        Assert.True(rounds!.Select(r => r.Landscape).Distinct().Count() > 1);
    }

    [Fact]
    public void Draw_WithoutRandomFill_ThePoolOnly_FewerPokemon_SameMap()
    {
        List<HideSeekCandidate> pool = [Mon(25), Mon(25, shiny: true), Mon(1), Mon(4), Mon(7), Mon(133), Mon(152)];
        var settings = new HideSeekSettings(Rounds: 3, Count: 60, Landscape: ParkLandscape.Forest, FillWithRandom: false);
        var (rounds, error) = HideSeekDraw.Draw(settings, pool, Fill, new Random(2));
        Assert.Null(error);
        Assert.All(rounds!, r =>
        {
            Assert.Equal(6, r.Pokemon.Count); // les 6 espèces du choix, aucune aléatoire
            Assert.All(r.Pokemon, p => Assert.Equal("Sacha", p.OwnerPseudo));
            Assert.Equal(Enumerable.Range(1, 6), r.Pokemon.Select(p => p.Id));
            Assert.Equal(3, r.MapSize); // la carte du nombre réglé (60 Pokémon)
        });
        // Avec des Pokémon aléatoires, l'option ne change rien : ils sont eux-mêmes le choix
        var random = HideSeekDraw.Draw(new HideSeekSettings(Rounds: 1, Count: 30, Pool: HideSeekPool.Random, FillWithRandom: false),
            Fill, Fill, new Random(2));
        Assert.Equal(30, random.Rounds![0].Pokemon.Count);
    }

    [Fact]
    public void NightLevel_ParisTime_SameForEveryPlayer()
    {
        // Novembre : UTC+1 ; juillet : UTC+2
        Assert.Equal(0, HideSeekDraw.NightLevel(new DateTime(2026, 11, 23, 11, 0, 0, DateTimeKind.Utc))); // midi
        Assert.Equal(1, HideSeekDraw.NightLevel(new DateTime(2026, 11, 23, 22, 0, 0, DateTimeKind.Utc))); // 23 h
        Assert.Equal(1, HideSeekDraw.NightLevel(new DateTime(2026, 11, 23, 3, 0, 0, DateTimeKind.Utc))); // 4 h
        Assert.Equal(0.5, HideSeekDraw.NightLevel(new DateTime(2026, 11, 23, 19, 30, 0, DateTimeKind.Utc)), 6); // 20 h 30
        Assert.Equal(0.5, HideSeekDraw.NightLevel(new DateTime(2026, 11, 23, 6, 0, 0, DateTimeKind.Utc)), 6); // 7 h, l'aube
        Assert.Equal(2.5 / 3, HideSeekDraw.NightLevel(new DateTime(2026, 7, 1, 19, 30, 0, DateTimeKind.Utc)), 6); // 21 h 30 l'été
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

    internal static BankPokemon Row(string owner, ushort species, string name, string nickname, bool egg = false)
    {
        var (type1, type2) = PokemonTraits.Types(species, 0);
        return new BankPokemon
        {
            OwnerId = owner, Species = species, SpeciesName = name, Nickname = nickname, IsEgg = egg, Level = 10,
            Context = (byte)EntityContext.Gen5, Type1 = type1, Type2 = type2, Data = [1], DepositedAt = DateTime.UtcNow,
        };
    }
}
