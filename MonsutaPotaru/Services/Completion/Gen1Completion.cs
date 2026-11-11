using MonsutaPotaru.Data;
using PKHeX.Core;

namespace MonsutaPotaru.Services;

/// <summary>Section d'une complétion : chacune pèse autant dans le pourcentage global, quel que soit son nombre d'étapes.</summary>
public enum CompletionSection { Story, Pokedex, Trainers, Items, Encounters }

/// <summary>État d'une étape : faite, à faire, ou ratée (plus faisable dans cette partie ; elle compte quand même).</summary>
public enum CompletionState { Missing, Done, Missed }

/// <summary>
/// Une ligne des tables de <see cref="Gen1CompletionData"/> (générées par outils/completion_gen1.py). Conditions : atomes
/// séparés par « + » (toutes), alternatives par « | » (une suffit) ; « E:n » drapeau d'événement n, « T:n » objet n caché
/// (ramassé, battu), « R:octet:bit » bit d'un octet de la sauvegarde, « B:n » badge n, « D:n » Pokémon n capturé,
/// « F » Panthéon, « P » amitié de Pikachu au maximum.
/// </summary>
/// <param name="Zone">Lieu, dans l'ordre de l'aventure (titre de groupe).</param>
/// <param name="Detail">Précision sur le lieu (« sous-sol 2 », « Arène, caché »).</param>
/// <param name="Missed">Condition qui rend l'étape impossible si elle n'est pas faite (Rockets de la Sylphe SARL).</param>
/// <param name="Team">Équipe d'un dresseur : « numéro national:niveau », séparés par des virgules.</param>
/// <param name="Item">Objet, numéro de Rouge / Bleu / Jaune.</param>
/// <param name="Coins">Jetons trouvés ou offerts.</param>
/// <param name="Species">Pokémon rencontré ou reçu.</param>
/// <param name="Give">Pokémon donné en échange de <paramref name="Species"/>.</param>
public sealed record Gen1Row(CompletionSection Section, string Zone, string? Detail, string Done, string? Label = null,
    string? Missed = null, string? Team = null, int Item = 0, int Coins = 0, ushort Species = 0, ushort Give = 0);

/// <summary>Une étape d'une complétion, lue dans la sauvegarde, avec son texte prêt à afficher.</summary>
/// <param name="Species">Pokémon de l'étape (Pokédex, rencontres) : son image.</param>
public sealed record CompletionStep(CompletionSection Section, string Zone, string? Detail, string Label, CompletionState State,
    ushort Species = 0)
{
    public bool Done => State == CompletionState.Done;
}

/// <summary>Progression d'une section : étapes faites, ratées, et en tout.</summary>
public sealed record CompletionSectionProgress(CompletionSection Section, int Done, int Missed, int Total)
{
    public string Name => CompletionSections.Name(Section);
}

/// <summary>
/// Progression d'une complétion : chaque section compte pour la même part (le Pokédex et ses 151 Pokémon n'écrasent pas
/// l'histoire) ; le pourcentage global est la moyenne des pourcentages des sections.
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

    public static CompletionProgress Of(IEnumerable<CompletionStep> steps) =>
        new([.. CompletionSections.All.Select(section =>
        {
            var inSection = steps.Where(s => s.Section == section).ToList();
            return new CompletionSectionProgress(section, inSection.Count(s => s.Done),
                inSection.Count(s => s.State == CompletionState.Missed), inSection.Count);
        })]);
}

/// <summary>Noms et adresses des sections, dans l'ordre de la page.</summary>
public static class CompletionSections
{
    public static readonly IReadOnlyList<CompletionSection> All =
        [CompletionSection.Story, CompletionSection.Pokedex, CompletionSection.Trainers, CompletionSection.Items, CompletionSection.Encounters];

    public static string Name(CompletionSection section) => section switch
    {
        CompletionSection.Story => "Histoire",
        CompletionSection.Pokedex => "Pokédex",
        CompletionSection.Trainers => "Dresseurs",
        CompletionSection.Items => "Objets",
        _ => "Rencontres uniques",
    };

    /// <summary>Nom dans l'adresse (?section=dresseurs).</summary>
    public static string Slug(CompletionSection section) => section switch
    {
        CompletionSection.Story => "histoire",
        CompletionSection.Pokedex => "pokedex",
        CompletionSection.Trainers => "dresseurs",
        CompletionSection.Items => "objets",
        _ => "rencontres",
    };

    public static CompletionSection FromSlug(string? slug) =>
        All.FirstOrDefault(s => Slug(s) == slug?.ToLowerInvariant(), CompletionSection.Story);
}

/// <summary>
/// Complétion de Rouge, Bleu et Jaune (versions internationales) : les tables de <see cref="Gen1CompletionData"/>, reprises
/// des désassemblages pokered et pokeyellow, lues dans les drapeaux de la sauvegarde. Les sauvegardes japonaises (autres
/// adresses) ne sont pas prises en charge.
/// </summary>
public static class Gen1Completion
{
    /// <summary>Jeux qui ont une complétion.</summary>
    public static readonly IReadOnlyList<LivingDexGame> Games = [LivingDexGame.Red, LivingDexGame.Blue, LivingDexGame.Yellow];

    /// <summary>Jeux possibles pour cette sauvegarde (Rouge ou Bleu : PKHeX ne les distingue pas) ; aucun si japonaise.</summary>
    public static IReadOnlyList<LivingDexGame> For(SaveFile sav) =>
        sav is SAV1 { Japanese: false } ? [.. LivingDexGames.For(sav).Intersect(Games)] : [];

    private static readonly GameStrings French = GameInfo.GetStrings("fr");
    private static readonly string[] Items = French.GetItemStrings(EntityContext.Gen1, GameVersion.RD);

    /// <summary>Noms d'objets de PKHeX corrigés (fautes de la liste française de 1re génération).</summary>
    private static readonly Dictionary<int, string> ItemFixes = new()
    {
        [36] = "Protéine", [69] = "Boîte Jetons", [76] = "Canne", [80] = "Huile", [82] = "Élixir",
    };

    private const int FirstHm = 196, FirstTm = 201;

    /// <summary>Étapes de la partie, dans l'ordre : histoire, Pokédex (les 151), dresseurs, objets, rencontres.</summary>
    public static List<CompletionStep> Steps(SAV1 sav, LivingDexGame game)
    {
        var flags = new SaveFlags(sav);
        var rows = game == LivingDexGame.Yellow ? Gen1CompletionData.Yellow : Gen1CompletionData.RedBlue;
        var steps = rows.Select(r => Step(r, flags)).ToList();
        var pokedex = Enumerable.Range(1, 151).Select(n => new CompletionStep(CompletionSection.Pokedex, "Pokédex", null,
            $"#{n:000} {French.specieslist[n]}", sav.GetCaught((ushort)n) ? CompletionState.Done : CompletionState.Missing,
            (ushort)n));
        return [.. steps.Where(s => s.Section == CompletionSection.Story), .. pokedex, .. steps.Where(s => s.Section != CompletionSection.Story)];
    }

    private static CompletionStep Step(Gen1Row row, SaveFlags flags)
    {
        var state = flags.Test(row.Done) ? CompletionState.Done
            : row.Missed is not null && flags.Test(row.Missed) ? CompletionState.Missed
            : CompletionState.Missing;
        return new(row.Section, row.Zone, row.Detail, Label(row), state, row.Species);
    }

    /// <summary>Texte d'une étape : le sien, sinon l'équipe du dresseur, l'objet, les jetons ou le Pokémon.</summary>
    public static string Label(Gen1Row row)
    {
        if (row.Label is not null)
            return row.Label;
        if (row.Team is not null)
            return TeamLabel(row.Team);
        if (row.Item != 0)
            return ItemName(row.Item);
        if (row.Coins != 0)
            return $"{row.Coins} jetons";
        var species = French.specieslist[row.Species];
        return row.Give != 0 ? $"{species} (contre {French.specieslist[row.Give]})" : species;
    }

    /// <summary>« Rattata, Abo (niv. 11) » ; niveaux différents : « Rattata niv. 11, Abo niv. 14 ».</summary>
    private static string TeamLabel(string team)
    {
        var members = team.Split(',').Select(m => m.Split(':')).Select(p => (Species: int.Parse(p[0]), Level: int.Parse(p[1]))).ToList();
        if (members.Select(m => m.Level).Distinct().Count() == 1)
            return $"{string.Join(", ", members.Select(m => French.specieslist[m.Species]))} (niv. {members[0].Level})";
        return string.Join(", ", members.Select(m => $"{French.specieslist[m.Species]} niv. {m.Level}"));
    }

    /// <summary>Nom français d'un objet de Rouge / Bleu / Jaune ; une CT ou une CS avec sa capacité (« CT08 Plaquage »).</summary>
    public static string ItemName(int item) => item switch
    {
        >= FirstTm => $"CT{item - FirstTm + 1:00} {French.movelist[Gen1CompletionData.TmMoves[item - FirstTm]]}",
        >= FirstHm => $"CS{item - FirstHm + 1:00} {French.movelist[Gen1CompletionData.TmMoves[50 + item - FirstHm]]}",
        _ => ItemFixes.GetValueOrDefault(item) ?? Items[item],
    };

    /// <summary>Lecture des conditions des tables dans une sauvegarde (objets à interrupteur lus une seule fois).</summary>
    private sealed class SaveFlags(SAV1 sav)
    {
        private readonly bool[] hidden = sav.EventSpawnFlags;

        public bool Test(string condition) =>
            condition.Split('|').Any(alternative => alternative.Split('+').All(Atom));

        private bool Atom(string atom)
        {
            var parts = atom.Split(':');
            return parts[0] switch
            {
                "E" => sav.GetEventFlag(int.Parse(parts[1])),
                "T" => hidden[int.Parse(parts[1])],
                "R" => (sav.Data[int.Parse(parts[1])] >> int.Parse(parts[2]) & 1) != 0,
                "B" => (sav.Badges >> int.Parse(parts[1]) & 1) != 0,
                "D" => sav.GetCaught(ushort.Parse(parts[1])),
                "F" => sav.HallOfFameCount > 0,
                "P" => sav.PikaFriendship == byte.MaxValue,
                _ => throw new InvalidOperationException($"Condition inconnue : {atom}"),
            };
        }
    }
}
