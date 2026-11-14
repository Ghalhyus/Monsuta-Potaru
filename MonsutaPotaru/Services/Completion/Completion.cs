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
/// amitié de Pikachu au maximum (1re génération) ; en 4e génération (<see cref="Gen4CompletionData"/>, par
/// outils/completion_gen4.py) « B:n » badge n (HeartGold / SoulSilver : 0 à 7 Johto, 8 à 15 Kanto), « N » Pokédex National,
/// « W:n:v » variable de script n (0x4000 retiré) arrivée à v au moins, « Z:n » forme n de Zarbi au Pokédex (A à Z, puis
/// ! et ?), « M:espèce:forme » forme vue au Pokédex (Motisma, Giratina, Shaymin) ; en 5e génération
/// (<see cref="Gen5CompletionData"/>, par outils/completion_gen5.py) « K:n » objet n dans le sac, « SB:champ:v » Métro de
/// Combat (mode Super ouvert, meilleure série), « PW:n » tournoi n du PWT remporté, « MD:n » médaille n obtenue, « PR:n »
/// parure n du Music-Hall possédée ; en 6e génération (<see cref="Gen6CompletionData"/>, par outils/completion_gen6.py)
/// « H:n » objet n dans le sac ou tenu par un Pokémon (Méga-Gemmes), « ST:n » épreuve n du Super Training réussie (un
/// meilleur temps enregistré) ; « W:n:v » variable de script n arrivée à v au moins (sans retrait de 0x4000) ; en 7e
/// génération (<see cref="Gen7CompletionData"/>, par outils/completion_gen7.py) « PS:n » tampon n du Passe Dresseur
/// (<see cref="Stamp7"/>), « BT:mode:super:n » meilleure série de l'Arbre de Combat (mode 0 Solo, 1 Duo, 2 Multi ; super
/// 1 pour le mode Super) arrivée à n au moins, « ZC:n » cellule Zygarde n ramassée, « TF:n » titre n de Let's Go débloqué
/// (1 à 153 : les Experts, 154 Grand Maître, 155 Maître de Combat) ; en 8e génération (<see cref="Gen8CompletionData"/>,
/// par outils/completion_gen8.py) « X:clé » bloc booléen levé (Épée / Bouclier, Légendes Arceus : 32 bits de la clé de
/// FlagsEditorEX, en hexadécimal), « XV:clé:n » bloc numérique arrivé à n au moins, « HI:n » objet caché n d'Épée / Bouclier
/// ramassé, « SF:n » drapeau système de DÉ/PS (badges 124 à 131), « BTM:n » rang Expert de la Tour de Combat de DÉ/PS
/// arrivé à n, « UF:n » forme n de Zarbi au Pokédex de DÉ/PS, « UG:n » objet n des Grands Souterrains possédé (statues),
/// « LF:espèce:forme » forme obtenue au Pokédex de Hisui, « LC:n » / « LP:n » espèce n de Hisui au niveau de recherche 10
/// / Parfaite.
/// </summary>
/// <param name="Zone">Lieu, dans l'ordre de l'aventure (titre de groupe).</param>
/// <param name="Detail">Précision sur le lieu (« sous-sol 2 », « Arène, caché »).</param>
/// <param name="Missed">Condition qui rend l'étape impossible si elle n'est pas faite (Rockets de la Sylphe SARL).</param>
/// <param name="Team">Équipe d'un dresseur : « numéro national:niveau », séparés par des virgules.</param>
/// <param name="Item">Objet, numéro du jeu.</param>
/// <param name="Tm">CT (1 à 50, 1 à 92 en 4e génération) ou CS (les suivantes) : son nom vient de sa capacité.</param>
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
/// (<see cref="Gen2Completion"/>), versions internationales (une sauvegarde japonaise ou coréenne a d'autres adresses) ;
/// Rubis, Saphir, Émeraude, Rouge Feu et Vert Feuille (<see cref="Gen3Completion"/>), Diamant, Perle, Platine, HeartGold et
/// SoulSilver (<see cref="Gen4Completion"/>), Noir, Blanc, Noir 2 et Blanc 2 (<see cref="Gen5Completion"/>), X, Y, Rubis Oméga
/// et Saphir Alpha (<see cref="Gen6Completion"/>), Soleil, Lune, Ultra-Soleil, Ultra-Lune et Let's Go
/// (<see cref="Gen7Completion"/>), Épée, Bouclier, Diamant Étincelant, Perle Scintillante et Légendes Arceus
/// (<see cref="Gen8Completion"/>), toutes les langues.
/// </summary>
public static class CompletionGames
{
    public static readonly IReadOnlyList<LivingDexGame> All =
    [
        LivingDexGame.Red, LivingDexGame.Blue, LivingDexGame.Yellow, LivingDexGame.Gold, LivingDexGame.Silver, LivingDexGame.Crystal,
        LivingDexGame.Ruby, LivingDexGame.Sapphire, LivingDexGame.Emerald, LivingDexGame.FireRed, LivingDexGame.LeafGreen,
        LivingDexGame.Diamond, LivingDexGame.Pearl, LivingDexGame.Platinum, LivingDexGame.HeartGold, LivingDexGame.SoulSilver,
        LivingDexGame.Black, LivingDexGame.White, LivingDexGame.Black2, LivingDexGame.White2,
        LivingDexGame.X, LivingDexGame.Y, LivingDexGame.OmegaRuby, LivingDexGame.AlphaSapphire,
        LivingDexGame.Sun, LivingDexGame.Moon, LivingDexGame.UltraSun, LivingDexGame.UltraMoon,
        LivingDexGame.LetsGoPikachu, LivingDexGame.LetsGoEevee,
        LivingDexGame.Sword, LivingDexGame.Shield, LivingDexGame.BrilliantDiamond, LivingDexGame.ShiningPearl,
        LivingDexGame.LegendsArceus,
    ];

    /// <summary>Jeux possibles pour cette sauvegarde (Rouge ou Bleu, Or ou Argent, Diamant ou Perle : PKHeX ne les distingue pas).</summary>
    public static IReadOnlyList<LivingDexGame> For(SaveFile sav) => sav switch
    {
        SAV1 { Japanese: false } => [.. LivingDexGames.For(sav).Intersect(All)],
        SAV2 { Japanese: false, Korean: false } => [.. LivingDexGames.For(sav).Intersect(All)],
        SAV3 or SAV4 or SAV5 or SAV6XY or SAV6AO or SAV7SM or SAV7USUM or SAV7b or SAV8SWSH or SAV8BS or SAV8LA =>
            [.. LivingDexGames.For(sav).Intersect(All)], // drapeaux lus par PKHeX : toutes les langues
        _ => [],
    };

    /// <summary>Étapes de la partie dans ce jeu ; null si la sauvegarde n'en est pas une partie.</summary>
    public static List<CompletionStep>? Steps(SaveFile sav, LivingDexGame game)
    {
        if (!For(sav).Contains(game))
            return null;
        return sav switch
        {
            SAV1 sav1 => Gen1Completion.Steps(sav1, game),
            SAV2 sav2 => Gen2Completion.Steps(sav2, game),
            SAV4 sav4 => Gen4Completion.Steps(sav4, game),
            SAV5 sav5 => Gen5Completion.Steps(sav5, game),
            SAV6 sav6 => Gen6Completion.Steps(sav6, game),
            SAV7 or SAV7b => Gen7Completion.Steps(sav, game),
            SAV8SWSH or SAV8BS or SAV8LA => Gen8Completion.Steps(sav, game),
            _ => Gen3Completion.Steps((SAV3)sav, game),
        };
    }

    /// <summary>Étapes de la table, avec celles du Pokédex (toutes les espèces du jeu) juste après l'histoire.</summary>
    internal static List<CompletionStep> Steps(IReadOnlyList<CompletionRow> rows, CompletionFlags flags, int pokedex,
        Func<CompletionRow, string> label) => Steps(rows, flags, Enumerable.Range(1, pokedex), label);

    /// <summary>Étapes de la table, avec celles du Pokédex (les espèces données, Let's Go : Kanto, Meltan et Melmetal).</summary>
    internal static List<CompletionStep> Steps(IReadOnlyList<CompletionRow> rows, CompletionFlags flags, IEnumerable<int> pokedex,
        Func<CompletionRow, string> label)
    {
        var steps = rows.Select(r => new CompletionStep(r.Section, r.Zone, r.Detail, label(r), flags.State(r), r.Species, r.Form)).ToList();
        var dex = pokedex.Select(n => new CompletionStep(CompletionSection.Pokedex, "Pokédex", null,
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

    /// <summary>
    /// « CT08 Plaquage », « CS01 Coupe » : <paramref name="number"/> de 1 à <paramref name="tms"/> pour une CT (50 jusqu'à
    /// la 3e génération, 92 en 4e), puis les CS.
    /// </summary>
    public static string Machine(int number, IReadOnlyList<ushort> moves, int tms = 50) =>
        number <= tms ? $"CT{number:00} {French.movelist[moves[number - 1]]}" : $"CS{number - tms:00} {French.movelist[moves[number - 1]]}";
}

/// <summary>Lecture des conditions des tables dans une sauvegarde (de la 1re à la 8e génération).</summary>
internal sealed class CompletionFlags(SaveFile sav)
{
    /// <summary>Cellule Zygarde ramassée (0 : absente, 1 : visible sur la carte ; éditeur des cellules de PKHeX).</summary>
    private const ushort ZygardeCellReceived = 2;

    /// <summary>Objets cachés d'Épée / Bouclier : trois blocs de 512 (jeu, Isolarmure, Couronneige ; FlagsEditorEX).</summary>
    private static readonly uint[] HiddenItemBlocks = [0x6148F6AC, 0xE479EE37, 0xE579EFCA];

    /// <summary>État d'un objet caché d'Épée / Bouclier ramassé (2 : il reviendra, 3 : pour de bon).</summary>
    private const byte HiddenItemObtained = 2;

    private readonly bool[] hidden = sav is SAV1 sav1 ? sav1.EventSpawnFlags : [];

    private readonly Lazy<IReadOnlyList<UndergroundItem8b>> underground =
        new(() => sav is SAV8BS bdsp ? bdsp.Underground.ReadItems() : []);

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
            "B" => (Badges() >> int.Parse(parts[1]) & 1) != 0,
            "D" => sav.GetCaught(ushort.Parse(parts[1])),
            "U" => Unown(int.Parse(parts[1]), int.Parse(parts[2])),
            "S" => sav.Data[int.Parse(parts[1])] >= int.Parse(parts[2]),
            "F" => ((SAV1)sav).HallOfFameCount > 0,
            "P" => ((SAV1)sav).PikaFriendship == byte.MaxValue,
            "N" => sav switch
            {
                SAV5 sav5 => sav5.Zukan.IsNationalDexUnlocked,
                SAV8BS bdsp => bdsp.Zukan.HasNationalDex,
                _ => ((SAV4)sav).NationalDex,
            },
            "W" => Work(int.Parse(parts[1])) >= int.Parse(parts[2]),
            "Z" => ((SAV4)sav).Dex.GetUnownForm(byte.Parse(parts[1])),
            "M" => ((SAV4)sav).Dex.GetForms(ushort.Parse(parts[1])).Contains(byte.Parse(parts[2])),
            "K" => HasItem(int.Parse(parts[1])),
            "H" => HasItem(int.Parse(parts[1])) || IsHeld(int.Parse(parts[1])),
            "ST" => SuperTraining().GetTime1(int.Parse(parts[1])) > 0,
            "SB" => Subway(parts[1]) >= int.Parse(parts[2]),
            "PW" => ((SAV5B2W2)sav).PWT.GetPWTRecord(int.Parse(parts[1])) > 0,
            "MD" => ((SAV5B2W2)sav).Medals[int.Parse(parts[1])].IsObtained,
            "PR" => ((SAV5)sav).Musical.GetHasProp(int.Parse(parts[1])),
            "PS" => (((SAV7)sav).Misc.Stamps >> int.Parse(parts[1]) & 1) != 0,
            "BT" => ((SAV7)sav).BattleTree.GetTreeStreak(int.Parse(parts[1]), parts[2] == "1", max: true) >= int.Parse(parts[3]),
            "ZC" => ((SAV7)sav).EventWork.GetZygardeCell(int.Parse(parts[1])) == ZygardeCellReceived,
            "TF" => ((SAV7b)sav).EventWork.GetTitleFlag(int.Parse(parts[1])),
            "X" => Block(parts[1]) is { Type: SCTypeCode.Bool2 },
            "XV" => Block(parts[1]) is { Type: > SCTypeCode.Array } block && Convert.ToInt64(block.GetValue()) >= long.Parse(parts[2]),
            "HI" => HiddenItem(int.Parse(parts[1])),
            "SF" => ((SAV8BS)sav).FlagWork.GetSystemFlag(int.Parse(parts[1])),
            "BTM" => ((SAV8BS)sav).BattleTower.MasterRankMax >= int.Parse(parts[1]),
            "UF" => ((SAV8BS)sav).Zukan.GetHasFormFlag(201, byte.Parse(parts[1]), false)
                || ((SAV8BS)sav).Zukan.GetHasFormFlag(201, byte.Parse(parts[1]), true),
            "UG" => underground.Value.Any(i => i.Index == int.Parse(parts[1]) && i.Count > 0),
            "LF" => ((SAV8LA)sav).PokedexSave.HasAnyPokeObtainFlags(ushort.Parse(parts[1]), byte.Parse(parts[2])),
            "LC" => ((SAV8LA)sav).PokedexSave.IsComplete(ushort.Parse(parts[1])),
            "LP" => ((SAV8LA)sav).PokedexSave.IsPerfect(ushort.Parse(parts[1])),
            _ => throw new InvalidOperationException($"Condition inconnue : {atom}"),
        };
    }

    /// <summary>
    /// Badges, un bit chacun : 1re génération ; 4e (HeartGold / SoulSilver : Johto dans <c>Badges</c>, Kanto dans
    /// <c>Badges16</c>, 8 bits malgré son nom : mis bout à bout, 0 à 7 Johto, 8 à 15 Kanto).
    /// </summary>
    private int Badges() => sav switch
    {
        SAV1 sav1 => sav1.Badges,
        SAV4HGSS hgss => hgss.Badges | hgss.Badges16 << 8,
        SAV4 sav4 => sav4.Badges,
        SAV5 sav5 => sav5.Misc.Badges,
        SAV6 sav6 => sav6.Badges,
        SAV8SWSH swsh => swsh.Badges,
        _ => throw new InvalidOperationException("Pas de badges lisibles dans cette sauvegarde."),
    };

    private bool Event(int flag) => sav switch
    {
        SAV1 sav1 => sav1.GetEventFlag(flag),
        SAV2 sav2 => sav2.GetEventFlag(flag),
        SAV3 sav3 => sav3.GetEventFlag(flag),
        SAV4 sav4 => sav4.GetEventFlag(flag),
        SAV5 sav5 => sav5.EventWork.GetEventFlag(flag),
        SAV6 sav6 => sav6.EventWork.GetEventFlag(flag),
        SAV7 sav7 => sav7.EventWork.GetEventFlag(flag),
        SAV7b lgpe => lgpe.EventWork.GetFlag(flag),
        SAV8BS bdsp => bdsp.FlagWork.GetFlag(flag),
        _ => throw new InvalidOperationException("Pas de drapeaux d'événements dans cette sauvegarde."),
    };

    /// <summary>Variable de script : 4e génération (0x4000 retiré), 6e génération (numéro de FlagsEditorEX ; pas utilisées en 7e).</summary>
    private int Work(int index) => sav switch
    {
        SAV4 sav4 => sav4.GetWork(index),
        SAV6 sav6 => sav6.EventWork.GetWork(index),
        _ => throw new InvalidOperationException("Pas de variables de script lisibles dans cette sauvegarde."),
    };

    /// <summary>Bloc d'Épée / Bouclier ou de Légendes Arceus (clé en hexadécimal) ; null s'il manque (sauvegarde sans DLC).</summary>
    private SCBlock? Block(string key)
    {
        var accessor = sav switch
        {
            SAV8SWSH swsh => swsh.Accessor,
            SAV8LA la => la.Accessor,
            _ => throw new InvalidOperationException("Pas de blocs dans cette sauvegarde."),
        };
        return accessor.TryGetBlock(Convert.ToUInt32(key, 16), out var block) ? block : null;
    }

    /// <summary>Objet caché n d'Épée / Bouclier ramassé (4 octets par objet : état, puis tirage) ; bloc absent : pas encore.</summary>
    private bool HiddenItem(int index)
    {
        var block = Block(HiddenItemBlocks[index / 512].ToString("X8"));
        return block is not null && block.Data[index % 512 * 4] >= HiddenItemObtained;
    }

    /// <summary>Super Training (6e génération) : un meilleur temps enregistré pour une épreuve, c'est qu'elle est réussie.</summary>
    private SuperTrainBlock SuperTraining() => sav switch
    {
        SAV6XY xy => xy.SuperTrain,
        SAV6AO ao => ao.SuperTrain,
        _ => throw new InvalidOperationException("Pas de Super Training dans cette sauvegarde."),
    };

    /// <summary>Objet tenu par un Pokémon de l'équipe ou des boîtes (Méga-Gemmes : elles se donnent à tenir).</summary>
    private bool IsHeld(int item) => sav.PartyData.Concat(sav.BoxData).Any(pk => pk.Species != 0 && pk.HeldItem == item);

    /// <summary>Objet dans une poche du sac (CS, objets rares : ils ne se jettent pas).</summary>
    private bool HasItem(int item) => sav.Inventory.Pouches.Any(p => p.Items.Any(i => i.Index == item && i.Count > 0));

    /// <summary>
    /// Métro de Combat (5e génération) : « SuperSingle », « SuperDouble », « SuperMulti » (1 : mode Super ouvert, les Chefs
    /// battus au 21e combat) ; « SuperSingleRecord »… (meilleure série en mode Super : 49 et plus, les Chefs battus).
    /// </summary>
    private int Subway(string field)
    {
        var subway = ((SAV5)sav).BattleSubway;
        return field switch
        {
            "SuperSingle" => subway.SuperSingle ? 1 : 0,
            "SuperDouble" => subway.SuperDouble ? 1 : 0,
            "SuperMulti" => subway.SuperMulti ? 1 : 0,
            "SuperSingleRecord" => subway.SuperSingleRecord,
            "SuperDoubleRecord" => subway.SuperDoubleRecord,
            "SuperMultiNPCRecord" => subway.SuperMultiNPCRecord,
            _ => throw new InvalidOperationException($"Champ du Métro inconnu : {field}"),
        };
    }

    /// <summary>Le Pokédex des Zarbi : les formes capturées, dans l'ordre (forme + 1 ; 0 pour une place vide).</summary>
    private bool Unown(int offset, int form) => sav.Data.Slice(offset, 26).Contains((byte)(form + 1));
}
