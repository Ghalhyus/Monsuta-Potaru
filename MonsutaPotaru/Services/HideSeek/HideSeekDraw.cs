using System.Collections.Concurrent;
using MonsutaPotaru.Data;
using PKHeX.Core;

namespace MonsutaPotaru.Services;

/// <summary>D'où viennent les Pokémon d'une partie de cache-cache.</summary>
public enum HideSeekPool
{
    /// <summary>« Ma collection » de l'hôte.</summary>
    Host,
    /// <summary>Les collections de tous les joueurs qui ont un compte.</summary>
    AllPlayers,
    /// <summary>Des espèces tirées au hasard, hors des collections.</summary>
    Random,
}

/// <summary>Humeur que doivent avoir les Pokémon du parc d'une partie (comme au Parc : d'après les habitats de la carte).</summary>
public enum HideSeekMood
{
    /// <summary>Tous les Pokémon, quelle que soit leur humeur.</summary>
    All,
    /// <summary>Neutres ou heureux : pas les malheureux.</summary>
    NotSad,
    /// <summary>Heureux seulement : un habitat qu'ils aiment couvre plus de la moitié du parc.</summary>
    Happy,
}

/// <summary>
/// Réglages d'une partie de cache-cache, choisis par l'hôte dans la salle d'attente (venus du navigateur :
/// <see cref="Error"/> les revérifie) : nombre de tours, durée d'un tour, Pokémon surnommés seulement (pas avec des
/// Pokémon aléatoires, qui n'ont pas de surnom), d'où viennent les Pokémon, paysage (null : un au hasard à chaque tour),
/// nombre de Pokémon dans le parc (la taille de la carte en dépend, <see cref="MapSize"/>), durée du décompte avant
/// chaque tour (la cible annoncée, le parc caché le temps que chacun le charge), humeur demandée à tous les Pokémon du parc
/// (<see cref="HideSeekCandidate.Fits"/>) et complément par des Pokémon aléatoires quand le choix n'en a pas assez (sans :
/// moins de Pokémon, sur la même carte).
/// </summary>
public sealed record HideSeekSettings(
    int Rounds = HideSeekSettings.DefaultRounds,
    int RoundSeconds = HideSeekSettings.DefaultSeconds,
    bool NicknamedOnly = false,
    HideSeekPool Pool = HideSeekPool.Host,
    ParkLandscape? Landscape = null,
    int Count = HideSeekSettings.DefaultCount,
    int CountdownSeconds = HideSeekSettings.DefaultCountdown,
    HideSeekMood Mood = HideSeekMood.All,
    bool FillWithRandom = true)
{
    public const int MinRounds = 1, MaxRounds = 30, DefaultRounds = 5;
    public const int MinSeconds = 15, MaxSeconds = 300, DefaultSeconds = 60;
    public const int MinCount = 20, MaxCount = 80, DefaultCount = 40;
    /// <summary>Au moins 3 s : le parc du tour se charge pendant le décompte.</summary>
    public const int MinCountdown = 3, MaxCountdown = 10, DefaultCountdown = 5;

    /// <summary>Taille de la carte (comme les parcs : 20 Pokémon par taille, de 1 à 4).</summary>
    public int MapSize => Math.Clamp((Count + 19) / 20, 1, 4);

    /// <summary>L'option « surnommés seulement » compte (pas avec des Pokémon aléatoires).</summary>
    public bool Nicknamed => NicknamedOnly && Pool != HideSeekPool.Random;

    /// <summary>Ce qui ne va pas dans ces réglages (valeurs trafiquées), ou null.</summary>
    public string? Error
    {
        get
        {
            if (Rounds is < MinRounds or > MaxRounds)
                return $"Le nombre de tours va de {MinRounds} à {MaxRounds}.";
            if (RoundSeconds is < MinSeconds or > MaxSeconds)
                return $"Un tour dure de {MinSeconds} secondes à {MaxSeconds / 60} minutes.";
            if (Count is < MinCount or > MaxCount)
                return $"Le parc compte de {MinCount} à {MaxCount} Pokémon.";
            if (CountdownSeconds is < MinCountdown or > MaxCountdown)
                return $"Le décompte dure de {MinCountdown} à {MaxCountdown} secondes.";
            if (!Enum.IsDefined(Pool))
                return "Ces Pokémon ne sont pas proposés.";
            if (Landscape is { } landscape && !Enum.IsDefined(landscape))
                return "Ce parc n'existe pas.";
            if (!Enum.IsDefined(Mood))
                return "Cette humeur n'est pas proposée.";
            return null;
        }
    }

    /// <summary>« 1 min 30 », « 45 s », « 2 min ».</summary>
    public static string Duration(int seconds)
    {
        var (m, s) = (seconds / 60, seconds % 60);
        if (m == 0)
            return $"{s} s";
        return s == 0 ? $"{m} min" : $"{m} min {s:00}";
    }

    /// <summary>Nom du choix de Pokémon (« La collection de l'hôte »...).</summary>
    public static string PoolName(HideSeekPool pool) => pool switch
    {
        HideSeekPool.AllPlayers => "Les collections de tous les joueurs",
        HideSeekPool.Random => "Des Pokémon aléatoires (hors collection)",
        _ => "La collection de l'hôte",
    };

    /// <summary>Nom du choix d'humeur (« Heureux seulement »...).</summary>
    public static string MoodName(HideSeekMood mood) => mood switch
    {
        HideSeekMood.NotSad => "Neutres et heureux",
        HideSeekMood.Happy => "Heureux seulement",
        _ => "Tous",
    };

    /// <summary>« (heureux dans le parc) » après un nombre de Pokémon, ou rien sans humeur demandée.</summary>
    public string MoodNote => Mood switch
    {
        HideSeekMood.Happy => " (heureux dans le parc)",
        HideSeekMood.NotSad => " (pas malheureux dans le parc)",
        _ => "",
    };

    /// <summary>« Parc au hasard à chaque tour », « Plage ».</summary>
    public string LandscapeName => Landscape is { } l ? ParkLandscapes.Name(l) : "Au hasard à chaque tour";
}

/// <summary>
/// Un Pokémon qui peut être tiré : d'une collection (son dresseur, son surnom) ou d'une espèce au hasard
/// (<see cref="HideSeekDraw.RandomPool"/>, sans dresseur). Types et nature : pour le paysage (<see cref="ParkLandscapes.Refusal"/>)
/// et l'allure au parc (parc.js).
/// </summary>
public sealed record HideSeekCandidate(
    string? OwnerPseudo, ushort Species, byte Form, EntityContext Context, bool Shiny, bool Female, int HeldItem,
    string SpeciesName, string Nickname, byte Type1, byte Type2, byte Nature)
{
    /// <summary>
    /// Ce qui rend deux Pokémon pareils à l'œil : l'espèce et la forme telles que dessinées (le dossier de sprites, sans
    /// chromatique ni femelle). Deux Pokémon de même clé ne vont jamais dans le même parc.
    /// </summary>
    public string Key { get; } = PokemonSprites.DonjonFolder(Species, Form, Context, false, false, HeldItem);

    public bool IsNicknamed => Nickname.Length > 0 && Nickname != SpeciesName;

    /// <summary>« Bob (Pikachu) » s'il a un surnom, sinon « Pikachu ».</summary>
    public string DisplayName => IsNicknamed ? $"{Nickname} ({SpeciesName})" : SpeciesName;

    public bool Accepted(ParkLandscape landscape) => ParkLandscapes.Refusal(landscape, Type1, Type2) is null;

    /// <summary>
    /// Peut aller dans le parc de ce paysage pour cette partie : le paysage l'accepte (<see cref="Accepted"/>) et son
    /// humeur sur la carte de la partie (<see cref="HideSeekSettings.MapSize"/>) est celle demandée (<see cref="HideSeekSettings.Mood"/>).
    /// </summary>
    public bool Fits(ParkLandscape landscape, HideSeekSettings settings) => Accepted(landscape) && settings.Mood switch
    {
        HideSeekMood.NotSad => HideSeekDraw.MoodIn(this, landscape, settings.MapSize) != ParkMood.Sad,
        HideSeekMood.Happy => HideSeekDraw.MoodIn(this, landscape, settings.MapSize) == ParkMood.Happy,
        _ => true,
    };
}

/// <summary>
/// Un Pokémon du parc d'un tour : numéro dans ce tour (de 1 au nombre de Pokémon, dans le désordre : il ne dit rien de la
/// cible), nom affiché, dresseur (null pour un Pokémon aléatoire), sprites et allure (parc.js), image fixe (la cible).
/// </summary>
public sealed record HideSeekMon(int Id, string Name, string? OwnerPseudo, ushort Species, string SpriteFolder, string ImageUrl,
    int Family, byte Nature, string? Moves);

/// <summary>
/// Un tour : son paysage, la taille de sa carte, ses Pokémon, la cible (numéro dans <see cref="Pokemon"/>) et la graine du
/// hasard du parc (parc.js, option sync : tous les joueurs voient les mêmes Pokémon aux mêmes endroits).
/// </summary>
public sealed record HideSeekRound(int Number, ParkLandscape Landscape, int MapSize, IReadOnlyList<HideSeekMon> Pokemon, int TargetId,
    int Seed = 0)
{
    public HideSeekMon Target => Pokemon.First(p => p.Id == TargetId);
}

/// <summary>
/// Tirage des tours d'une partie de cache-cache. Dans un parc, jamais deux Pokémon de même <see cref="HideSeekCandidate.Key"/>
/// (espèce et forme) ; une cible ne revient pas d'un tour à l'autre ; chaque Pokémon peut vivre dans le paysage du tour
/// (l'océan : seulement ceux qui nagent) et a des sprites Donjon Mystère (il peut aller au parc). La cible vient toujours du
/// choix de l'hôte (<see cref="HideSeekSettings.Pool"/>) ; s'il n'a pas assez de Pokémon différents pour remplir le parc,
/// des Pokémon aléatoires complètent (sauf si l'hôte l'a refusé : le parc en a moins). Avec une humeur demandée
/// (<see cref="HideSeekSettings.Mood"/>), tous les Pokémon du parc l'ont, la cible comme les autres.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5394:Do not use insecure randomness",
    Justification = "Tirage d'un jeu : Random (graine du système, fixée dans les tests) suffit, rien de secret n'en dépend.")]
public static class HideSeekDraw
{
    /// <summary>
    /// Nuit du parc d'un tour (parc.js, option sync) : 0 le jour, 1 la nuit (22 h – 6 h), progressive le soir (19 h – 22 h)
    /// et à l'aube (6 h – 8 h), comme <c>nightLevel</c> de parc.js, mais d'après l'heure de Paris au début du tour (le
    /// serveur, commun à tous les joueurs : la nuit change le comportement des Pokémon, qui dorment plus).
    /// </summary>
    public static double NightLevel(DateTime utc)
    {
        var h = ParisTime(utc).TimeOfDay.TotalHours;
        if (h >= 22 || h < 6)
            return 1;
        if (h >= 19)
            return (h - 19) / 3;
        if (h < 8)
            return 1 - (h - 6) / 2;
        return 0;
    }

    /// <summary>Heure de Paris ; sans base des fuseaux horaires (serveur incomplet) : heure d'hiver.</summary>
    private static DateTime ParisTime(DateTime utc)
    {
        utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        try
        {
            return TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris"));
        }
        catch (TimeZoneNotFoundException)
        {
            return utc.AddHours(1);
        }
    }

    /// <summary>Dernière espèce tirée au hasard (9e génération).</summary>
    public const ushort LastSpecies = 1025;

    private static IReadOnlyList<HideSeekCandidate>? _randomPool;

    /// <summary>Humeurs déjà calculées : elles ne dépendent que des types, du paysage et de la taille de la carte.</summary>
    private static readonly ConcurrentDictionary<(byte, byte, ParkLandscape, int), ParkMood> Moods = new();

    /// <summary>
    /// Humeur de ce Pokémon dans ce paysage, sur la carte de cette taille (<see cref="ParkHabitats.MoodOf"/>, comme au Parc).
    /// Types de cases pas encore lus : neutre, sans le garder.
    /// </summary>
    public static ParkMood MoodIn(HideSeekCandidate candidate, ParkLandscape landscape, int size)
    {
        if (ParkHabitats.Map(landscape, size) is not { } map)
            return ParkMood.Normal;
        return Moods.GetOrAdd((candidate.Type1, candidate.Type2, landscape, size),
            _ => ParkHabitats.MoodOf(candidate.Type1, candidate.Type2, map).Mood);
    }

    /// <summary>
    /// Nombre de Pokémon différents (clés) de ce choix qui peuvent aller dans le parc : dans ce paysage, ou, sans paysage,
    /// dans l'un d'eux au moins.
    /// </summary>
    public static int Usable(HideSeekSettings settings, IReadOnlyList<HideSeekCandidate> pool, ParkLandscape? landscape) =>
        pool.Where(c => landscape is { } l ? c.Fits(l, settings) : ParkLandscapes.All.Any(l => c.Fits(l, settings)))
            .Select(c => c.Key).Distinct().Count();

    /// <summary>
    /// Les Pokémon aléatoires (<see cref="RandomPool(Func{ushort, bool})"/>), calculés une fois : il faut redémarrer le
    /// site pour qu'une espèce dont les sprites viennent d'arriver y entre.
    /// </summary>
    public static IReadOnlyList<HideSeekCandidate> RandomPool()
    {
        if (_randomPool is { Count: > 0 } pool)
            return pool;
        return _randomPool = RandomPool(PokemonSprites.HasDonjonSprites); // vide (images absentes) : recalculé la fois suivante
    }

    /// <summary>Les Pokémon aléatoires : chaque espèce qui a des sprites Donjon Mystère (<paramref name="hasSprites"/>), dans sa forme ordinaire.</summary>
    public static IReadOnlyList<HideSeekCandidate> RandomPool(Func<ushort, bool> hasSprites)
    {
        var list = new List<HideSeekCandidate>();
        for (ushort species = 1; species <= LastSpecies; species++)
        {
            if (!hasSprites(species))
                continue;
            var (type1, type2) = PokemonTraits.Types(species, 0);
            var name = SpeciesName.GetSpeciesName(species, (int)LanguageID.French);
            list.Add(new HideSeekCandidate(null, species, 0, EntityContext.Gen9, false, false, 0, name, name, type1, type2,
                (byte)(species % 25)));
        }
        return list;
    }

    /// <summary>
    /// Ce qui empêche de lancer la partie avec ce choix de Pokémon (une cible différente par tour, qui peut vivre dans le
    /// paysage choisi), ou null.
    /// </summary>
    public static string? Check(HideSeekSettings settings, IReadOnlyList<HideSeekCandidate> pool)
    {
        var usable = Usable(settings, pool, settings.Landscape);
        if (usable >= settings.Rounds)
            return null;
        var where = settings.Landscape is { } landscape ? $" qui peuvent vivre {ParkLandscapes.Where(landscape)}" : "";
        var what = settings.Pool switch
        {
            HideSeekPool.AllPlayers => "Vos collections ont",
            HideSeekPool.Random => "Il n'y a que",
            _ => "Ta collection a",
        };
        var nicknamed = settings.Nicknamed ? " surnommés" : "";
        var mood = settings.MoodNote;
        return usable == 0
            ? $"Aucun Pokémon{nicknamed}{where}{mood} dans ce choix : change les réglages."
            : $"{what} {usable} Pokémon{nicknamed} différents{where}{mood} : il en faut un par tour ({settings.Rounds}). Baisse le nombre de tours ou change les réglages.";
    }

    /// <summary>
    /// Les tours de la partie (null et un message si ce choix ne suffit pas, <see cref="Check"/>). <paramref name="pool"/> :
    /// les Pokémon d'où viennent les cibles ; <paramref name="fill"/> : ceux qui complètent un parc (Pokémon aléatoires),
    /// si l'hôte le veut (<see cref="HideSeekSettings.FillWithRandom"/>).
    /// </summary>
    public static (IReadOnlyList<HideSeekRound>? Rounds, string? Error) Draw(HideSeekSettings settings,
        IReadOnlyList<HideSeekCandidate> pool, IReadOnlyList<HideSeekCandidate> fill, Random random)
    {
        if ((settings.Error ?? Check(settings, pool)) is { } error)
            return (null, error);
        var usedTargets = new HashSet<string>();
        var rounds = new List<HideSeekRound>();
        for (var number = 1; number <= settings.Rounds; number++)
        {
            // Paysage du tour : celui de l'hôte, ou un au hasard parmi ceux où une cible reste possible
            var landscape = settings.Landscape ?? Pick(random,
                ParkLandscapes.All.Where(l => pool.Any(c => !usedTargets.Contains(c.Key) && c.Fits(l, settings))).ToList());
            var targets = Groups(pool, landscape, settings, usedTargets);
            var target = Pick(random, Pick(random, targets));
            usedTargets.Add(target.Key);

            // Le reste du parc : d'autres Pokémon du choix (une clé chacun), puis des aléatoires (si l'hôte le veut)
            var chosen = new List<HideSeekCandidate> { target };
            var keys = new HashSet<string> { target.Key };
            foreach (var source in settings.FillWithRandom ? new[] { pool, fill } : [pool])
            {
                foreach (var group in Shuffle(random, Groups(source, landscape, settings, keys)))
                {
                    if (chosen.Count >= settings.Count)
                        break;
                    var mon = Pick(random, group);
                    chosen.Add(mon);
                    keys.Add(mon.Key);
                }
            }
            var ids = Shuffle(random, Enumerable.Range(1, chosen.Count).ToList());
            var mons = chosen.Select((c, i) => ToMon(ids[i], c, landscape)).OrderBy(m => m.Id).ToList();
            rounds.Add(new HideSeekRound(number, landscape, settings.MapSize, mons, ids[0], random.Next()));
        }
        return (rounds, null);
    }

    /// <summary>
    /// Les Pokémon qui peuvent aller dans ce parc (<see cref="HideSeekCandidate.Fits"/>), par clé (espèce et forme), sans les
    /// clés de <paramref name="excluded"/>.
    /// </summary>
    private static List<List<HideSeekCandidate>> Groups(IEnumerable<HideSeekCandidate> source, ParkLandscape landscape,
        HideSeekSettings settings, HashSet<string> excluded) =>
        source.Where(c => c.Fits(landscape, settings) && !excluded.Contains(c.Key))
            .GroupBy(c => c.Key).Select(g => g.ToList()).ToList();

    private static HideSeekMon ToMon(int id, HideSeekCandidate c, ParkLandscape landscape) => new(id, c.DisplayName, c.OwnerPseudo, c.Species,
        PokemonSprites.DonjonFolder(c.Species, c.Form, c.Context, c.Shiny, c.Female, c.HeldItem),
        PokemonSprites.Url(c.Species, c.Form, c.Context, c.Female, c.HeldItem, c.Shiny),
        ParkService.Family(c.Species), c.Nature, ParkHabitats.MovementOf(c.Type1, c.Type2, landscape));

    private static T Pick<T>(Random random, IReadOnlyList<T> items) => items[random.Next(items.Count)];

    private static List<T> Shuffle<T>(Random random, List<T> items)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
        return items;
    }
}
