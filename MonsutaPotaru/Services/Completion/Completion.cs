using MonsutaPotaru.Data;
using PKHeX.Core;

namespace MonsutaPotaru.Services;

/// <summary>Section d'une complétion : chacune pèse autant dans le pourcentage global, quel que soit son nombre d'étapes.</summary>
public enum CompletionSection { Story, Pokedex, Trainers, Items, Encounters, Quests }

/// <summary>État d'une étape : faite, à faire, ou ratée (plus faisable dans cette partie ; elle compte quand même).</summary>
public enum CompletionState { Missing, Done, Missed }

/// <summary>
/// Une ligne des tables générées (<see cref="Gen1CompletionData"/> par outils/completion_gen1.py,
/// <see cref="Gen2CompletionData"/> par outils/completion_gen2.py). Conditions : atomes séparés par « + » (toutes),
/// alternatives par « | » (une suffit) ; « E:n » drapeau d'événement n, « T:n » objet n caché (1re génération : ramassé,
/// Pokémon battu), « R:octet:bit » bit d'un octet de la sauvegarde, « B:n » badge n (1re génération), « D:n » Pokémon n
/// capturé, « U:octet:n » Zarbi de forme n capturé (liste du Pokédex des Zarbi, 2e génération), « S:octet:n » scène d'une
/// carte arrivée à n au moins (combat contre le rival passé, 2e génération), « F » Panthéon, « P »
/// amitié de Pikachu au maximum (1re génération).
/// </summary>
/// <param name="Zone">Lieu, dans l'ordre de l'aventure (titre de groupe).</param>
/// <param name="Detail">Précision sur le lieu (« sous-sol 2 », « Arène, caché »).</param>
/// <param name="Missed">Condition qui rend l'étape impossible si elle n'est pas faite (Rockets de la Sylphe SARL).</param>
/// <param name="Team">Équipe d'un dresseur : « numéro national:niveau », séparés par des virgules.</param>
/// <param name="Item">Objet, numéro du jeu.</param>
/// <param name="Tm">CT (1 à 50) ou CS (51 et suivantes), 2e génération : son nom vient de sa capacité.</param>
/// <param name="Count">Quantité d'objets trouvés (rien pour un seul).</param>
/// <param name="Coins">Jetons trouvés ou offerts.</param>
/// <param name="Species">Pokémon rencontré, reçu ou à capturer.</param>
/// <param name="Form">Forme de ce Pokémon (lettre de Zarbi).</param>
/// <param name="Give">Pokémon donné en échange de <paramref name="Species"/>.</param>
public sealed record CompletionRow(CompletionSection Section, string Zone, string? Detail, string Done, string? Label = null,
    string? Missed = null, string? Team = null, int Item = 0, int Tm = 0, int Count = 0, int Coins = 0, ushort Species = 0,
    byte Form = 0, ushort Give = 0);

/// <summary>Une étape d'une complétion, lue dans la sauvegarde, avec son texte prêt à afficher.</summary>
/// <param name="Species">Pokémon de l'étape (Pokédex, rencontres, Zarbi) : son image.</param>
public sealed record CompletionStep(CompletionSection Section, string Zone, string? Detail, string Label, CompletionState State,
    ushort Species = 0, byte Form = 0)
{
    public bool Done => State == CompletionState.Done;
}

/// <summary>Progression d'une section : étapes faites, ratées, et en tout.</summary>
public sealed record CompletionSectionProgress(CompletionSection Section, int Done, int Missed, int Total)
{
    public string Name => CompletionSections.Name(Section);
}

/// <summary>
/// Progression d'une complétion : chaque section du jeu compte pour la même part (le Pokédex et ses 151 ou 251 Pokémon
/// n'écrasent pas l'histoire) ; le pourcentage global est la moyenne des pourcentages des sections.
/// </summary>
public sealed record CompletionProgress(IReadOnlyList<CompletionSectionProgress> Sections)
{
    /// <summary>Pourcentage global en dixièmes, arrondi vers le bas : 1 000 seulement quand tout est fait.</summary>
    public int Tenths
    {
        get
        {
            var counted = Sections.Where(s => s.Total > 0).ToList();
            if (counted.Count == 0)
                return 0;
            var average = counted.Average(s => (decimal)s.Done / s.Total);
            return (int)Math.Floor(average * 1000);
        }
    }

    /// <summary>« 57,3 % », comme les barres des living dex.</summary>
    public string Percent => LivingDexGames.Percent(Tenths, 1000);

    public bool Complete => Sections.All(s => s.Done == s.Total);

    /// <summary>« un cinquième », « un sixième » : la part de chaque section dans le total.</summary>
    public string Share => Sections.Count switch
    {
        2 => "la moitié",
        3 => "un tiers",
        4 => "un quart",
        5 => "un cinquième",
        6 => "un sixième",
        _ => $"1/{Sections.Count}",
    };

    /// <summary>Sections que le jeu a (la 1re génération n'a pas de quêtes annexes), dans l'ordre de la page.</summary>
    public static CompletionProgress Of(IEnumerable<CompletionStep> steps)
    {
        var list = steps.ToList();
        return new([.. CompletionSections.All.Where(section => list.Any(s => s.Section == section)).Select(section =>
        {
            var inSection = list.Where(s => s.Section == section).ToList();
            return new CompletionSectionProgress(section, inSection.Count(s => s.Done),
                inSection.Count(s => s.State == CompletionState.Missed), inSection.Count);
        })]);
    }
}

/// <summary>Noms et adresses des sections, dans l'ordre de la page.</summary>
public static class CompletionSections
{
    public static readonly IReadOnlyList<CompletionSection> All =
    [
        CompletionSection.Story, CompletionSection.Pokedex, CompletionSection.Trainers, CompletionSection.Items,
        CompletionSection.Encounters, CompletionSection.Quests,
    ];

    public static string Name(CompletionSection section) => section switch
    {
        CompletionSection.Story => "Histoire",
        CompletionSection.Pokedex => "Pokédex",
        CompletionSection.Trainers => "Dresseurs",
        CompletionSection.Items => "Objets",
        CompletionSection.Encounters => "Rencontres uniques",
        _ => "Quêtes annexes",
    };

    /// <summary>Nom dans l'adresse (?section=dresseurs).</summary>
    public static string Slug(CompletionSection section) => section switch
    {
        CompletionSection.Story => "histoire",
        CompletionSection.Pokedex => "pokedex",
        CompletionSection.Trainers => "dresseurs",
        CompletionSection.Items => "objets",
        CompletionSection.Encounters => "rencontres",
        _ => "quetes",
    };

    public static CompletionSection FromSlug(string? slug) =>
        All.FirstOrDefault(s => Slug(s) == slug?.ToLowerInvariant(), CompletionSection.Story);
}

/// <summary>
/// Jeux qui ont une complétion : Rouge, Bleu et Jaune (<see cref="Gen1Completion"/>), Or, Argent et Cristal
/// (<see cref="Gen2Completion"/>), versions internationales (une sauvegarde japonaise ou coréenne a d'autres adresses).
/// </summary>
public static class CompletionGames
{
    public static readonly IReadOnlyList<LivingDexGame> All =
        [LivingDexGame.Red, LivingDexGame.Blue, LivingDexGame.Yellow, LivingDexGame.Gold, LivingDexGame.Silver, LivingDexGame.Crystal];

    /// <summary>Jeux possibles pour cette sauvegarde (Rouge ou Bleu, Or ou Argent : PKHeX ne les distingue pas).</summary>
    public static IReadOnlyList<LivingDexGame> For(SaveFile sav) => sav switch
    {
        SAV1 { Japanese: false } => [.. LivingDexGames.For(sav).Intersect(All)],
        SAV2 { Japanese: false, Korean: false } => [.. LivingDexGames.For(sav).Intersect(All)],
        _ => [],
    };

    /// <summary>Étapes de la partie dans ce jeu ; null si la sauvegarde n'en est pas une partie.</summary>
    public static List<CompletionStep>? Steps(SaveFile sav, LivingDexGame game)
    {
        if (!For(sav).Contains(game))
            return null;
        return sav is SAV1 sav1 ? Gen1Completion.Steps(sav1, game) : Gen2Completion.Steps((SAV2)sav, game);
    }

    /// <summary>Étapes de la table, avec celles du Pokédex (toutes les espèces du jeu) juste après l'histoire.</summary>
    internal static List<CompletionStep> Steps(IReadOnlyList<CompletionRow> rows, CompletionFlags flags, int pokedex,
        Func<CompletionRow, string> label)
    {
        var steps = rows.Select(r => new CompletionStep(r.Section, r.Zone, r.Detail, label(r), flags.State(r), r.Species, r.Form)).ToList();
        var dex = Enumerable.Range(1, pokedex).Select(n => new CompletionStep(CompletionSection.Pokedex, "Pokédex", null,
            $"#{n:000} {CompletionLabels.French.specieslist[n]}", flags.Test($"D:{n}") ? CompletionState.Done : CompletionState.Missing,
            (ushort)n));
        return [.. steps.Where(s => s.Section == CompletionSection.Story), .. dex, .. steps.Where(s => s.Section != CompletionSection.Story)];
    }
}

/// <summary>Textes des étapes : le leur, sinon l'équipe du dresseur, l'objet, les jetons ou le Pokémon.</summary>
public static class CompletionLabels
{
    internal static readonly GameStrings French = GameInfo.GetStrings("fr");

    /// <param name="item">Nom d'un objet du jeu (numéro de la table).</param>
    public static string Of(CompletionRow row, Func<int, string> item)
    {
        if (row.Label is not null)
            return row.Label;
        if (row.Team is not null)
            return Team(row.Team);
        if (row.Item != 0)
            return row.Count > 1 ? $"{item(row.Item)} ×{row.Count}" : item(row.Item);
        if (row.Coins != 0)
            return $"{row.Coins} jetons";
        var species = French.specieslist[row.Species];
        return row.Give != 0 ? $"{species} (contre {French.specieslist[row.Give]})" : species;
    }

    /// <summary>« Rattata, Abo (niv. 11) » ; niveaux différents : « Rattata niv. 11, Abo niv. 14 ».</summary>
    public static string Team(string team)
    {
        var members = team.Split(',').Select(m => m.Split(':')).Select(p => (Species: int.Parse(p[0]), Level: int.Parse(p[1]))).ToList();
        if (members.Select(m => m.Level).Distinct().Count() == 1)
            return $"{string.Join(", ", members.Select(m => French.specieslist[m.Species]))} (niv. {members[0].Level})";
        return string.Join(", ", members.Select(m => $"{French.specieslist[m.Species]} niv. {m.Level}"));
    }

    /// <summary>« CT08 Plaquage », « CS01 Coupe » : <paramref name="number"/> de 1 à 50 pour une CT, puis les CS.</summary>
    public static string Machine(int number, IReadOnlyList<ushort> moves) =>
        number <= 50 ? $"CT{number:00} {French.movelist[moves[number - 1]]}" : $"CS{number - 50:00} {French.movelist[moves[number - 1]]}";
}

/// <summary>Lecture des conditions des tables dans une sauvegarde de 1re ou de 2e génération.</summary>
internal sealed class CompletionFlags(SaveFile sav)
{
    private readonly bool[] hidden = sav is SAV1 sav1 ? sav1.EventSpawnFlags : [];

    public CompletionState State(CompletionRow row) =>
        Test(row.Done) ? CompletionState.Done
        : row.Missed is not null && Test(row.Missed) ? CompletionState.Missed
        : CompletionState.Missing;

    public bool Test(string condition) =>
        condition.Split('|').Any(alternative => alternative.Split('+').All(Atom));

    private bool Atom(string atom)
    {
        var parts = atom.Split(':');
        return parts[0] switch
        {
            "E" => Event(int.Parse(parts[1])),
            "T" => hidden[int.Parse(parts[1])],
            "R" => (sav.Data[int.Parse(parts[1])] >> int.Parse(parts[2]) & 1) != 0,
            "B" => (((SAV1)sav).Badges >> int.Parse(parts[1]) & 1) != 0,
            "D" => sav.GetCaught(ushort.Parse(parts[1])),
            "U" => Unown(int.Parse(parts[1]), int.Parse(parts[2])),
            "S" => sav.Data[int.Parse(parts[1])] >= int.Parse(parts[2]),
            "F" => ((SAV1)sav).HallOfFameCount > 0,
            "P" => ((SAV1)sav).PikaFriendship == byte.MaxValue,
            _ => throw new InvalidOperationException($"Condition inconnue : {atom}"),
        };
    }

    private bool Event(int flag) => sav switch
    {
        SAV1 sav1 => sav1.GetEventFlag(flag),
        SAV2 sav2 => sav2.GetEventFlag(flag),
        _ => throw new InvalidOperationException("Pas de drapeaux d'événements dans cette sauvegarde."),
    };

    /// <summary>Le Pokédex des Zarbi : les formes capturées, dans l'ordre (forme + 1 ; 0 pour une place vide).</summary>
    private bool Unown(int offset, int form) => sav.Data.Slice(offset, 26).Contains((byte)(form + 1));
}
