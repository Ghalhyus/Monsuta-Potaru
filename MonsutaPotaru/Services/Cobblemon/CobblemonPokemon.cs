using System.Globalization;
using System.Text;
using PKHeX.Core;

namespace MonsutaPotaru.Services;

/// <summary>
/// Un Pokémon de Cobblemon (modpack All the Mons), tel que le mod l'enregistre : un ensemble NBT gardé intact (il repart
/// tel quel dans Minecraft), lu pour l'affichage. Les identifiants de Cobblemon (« cobblemon:fennekin », « tailwhip »,
/// « cobblemon:quirky ») sont ceux de Showdown : retrouvés dans les noms anglais de PKHeX sans espaces ni ponctuation.
/// </summary>
public sealed class CobblemonPokemon(NbtCompound nbt)
{
    /// <summary>Extension enregistrée dans <see cref="Data.BankPokemon.Extension"/> : marque les Pokémon de Minecraft.</summary>
    public const string Extension = "cobblemon";

    /// <summary>Jeu dont les noms de formes servent aux images (celui de PKHeX qui connaît le plus de formes régionales).</summary>
    public const EntityContext FormContext = EntityContext.Gen9;

    public NbtCompound Nbt { get; } = nbt;

    /// <summary>Identifiant unique du Pokémon (UUID de Minecraft, quatre entiers), « 3c66b109-… » ; vide si absent.</summary>
    public string Uuid => (Nbt["UUID"] as NbtArray)?.Values is int[] { Length: 4 } parts ? FormatUuid(parts) : "";

    /// <summary>Empreinte (voir <see cref="PokemonFingerprint"/>) : l'UUID, qui suit le Pokémon partout.</summary>
    public string Fingerprint => $"cobblemon:{Uuid}";

    /// <summary>« cobblemon:fennekin ».</summary>
    public string SpeciesId => Nbt.String("Species") ?? "";

    /// <summary>Numéro national de l'espèce ; 0 si inconnue de PKHeX (espèce ajoutée par un autre mod).</summary>
    public ushort Species => (ushort)Math.Max(0, Lookup(SpeciesIds, SpeciesId));

    public string FormId => Nbt.String("FormId") ?? "normal";

    /// <summary>
    /// Numéro de forme de PKHeX en 9e génération (pour l'image et les types) ; 0 pour la forme normale, ou pour une forme
    /// que la 9e génération ne connaît pas (Pikachu Cosplayeur, Floette Éternelle, formes propres à Cobblemon).
    /// </summary>
    public byte Form => Species == 0 || IsBaseForm && Species != Unown ? (byte)0 : FormIndex(Species, Resolve(minorCore: false));

    /// <summary>
    /// Forme par défaut de l'espèce : « normal » chez Cobblemon, ou une forme « -Bias » (Cubone « Alola-Bias » : un bébé
    /// d'aspect ordinaire, qui évoluera en forme régionale).
    /// </summary>
    public bool IsBaseForm => IsBase(Key(FormId));

    private static bool IsBase(string id) => id is "" or "normal" || id.EndsWith("bias", StringComparison.Ordinal);

    /// <summary>
    /// Forme reconnue par le living dex All the Mons : le nom anglais de PKHeX (« Alola », « Rock Star », « C-Red »),
    /// cherché dans les jeux de la 9e à la 6e génération, après les noms que Cobblemon écrit autrement (Basculin
    /// « Blue-Striped » : « Blue ») ; une forme propre à Cobblemon garde son identifiant (« matcha », « patrickyu »).
    /// Zarbi : sa lettre (trait « character » : « ! » et « ? » n'ont pas d'identifiant de forme chez Cobblemon) ; Météno :
    /// sa couleur de noyau, même sous sa carapace (forme « meteor » et trait « core_color »).
    /// </summary>
    public string FormKey => Resolve(minorCore: true);

    /// <summary>Clé de forme ; <paramref name="minorCore"/> faux : un Météno sous sa carapace le reste (image de la collection).</summary>
    private string Resolve(bool minorCore)
    {
        if (Species == Unown && FeatureValue("character") is { Length: 1 } letter)
            return letter.ToUpperInvariant();
        if (minorCore && Species == Minior && (IsBaseForm || Key(FormId) == "meteor") && FeatureValue("core_color") is { Length: > 1 } color)
            return $"C-{char.ToUpperInvariant(color[0])}{color[1..].ToLowerInvariant()}";
        return FormKeyOf(Species, FormId);
    }

    /// <summary>Valeur d'un trait de l'espèce (« Features » : <c>{"cobblemon:feature_id": "decoration", "decoration": "star"}</c>).</summary>
    public string? FeatureValue(string feature) =>
        (Nbt.List("Features")?.Items ?? []).OfType<NbtCompound>()
            .FirstOrDefault(f => f.String("cobblemon:feature_id") == feature)?.String(feature);

    /// <summary>Décoration de Charmilly (0 Fraise à 6 Ruban, ordre de PKHeX) ; null sans décoration connue.</summary>
    public byte? Sweet => Species == Alcremie && FeatureValue("decoration") is { } d && Array.IndexOf(Sweets, d) is var i and >= 0
        ? (byte)i : null;

    /// <summary>Facteur Gigamax (ajouté par Mega Showdown, gardé par Cobblemon).</summary>
    public bool CanGigantamax => Nbt.Flag("GmaxFactor");

    /// <summary>
    /// Numéro de l'espèce pour le living dex All the Mons : le national, ou celui que le modpack donne à ses propres
    /// espèces (Piglich 9901, Creepyon 9902) ; 0 pour une espèce inconnue.
    /// </summary>
    public ushort DexNumber => Species != 0 ? Species
        : CustomSpecies.TryGetValue(Key(SpeciesId.Contains(':') ? SpeciesId[(SpeciesId.IndexOf(':') + 1)..] : SpeciesId), out var n) ? n : (ushort)0;

    /// <summary>Espèces propres à All the Mons (mod « allthemons »), avec le numéro que le mod leur donne.</summary>
    public static readonly IReadOnlyDictionary<string, ushort> CustomSpecies = new Dictionary<string, ushort> { ["piglich"] = 9901, ["creepyon"] = 9902 };

    private const ushort Unown = 201, Minior = 774, Alcremie = 869;

    /// <summary>Décorations de Charmilly chez Cobblemon, dans l'ordre de PKHeX (<c>FormArgument</c>).</summary>
    private static readonly string[] Sweets = ["strawberry", "berry", "love", "star", "clover", "flower", "ribbon"];

    /// <summary>Formes que Cobblemon nomme autrement que PKHeX : identifiant de Cobblemon → nom anglais de PKHeX.</summary>
    private static readonly Dictionary<(ushort Species, string Id), string> FormAliases = new()
    {
        [(550, "bluestriped")] = "Blue", [(550, "whitestriped")] = "White", // Bargantua
        [(649, "douse")] = "Water", [(649, "shock")] = "Electric", [(649, "burn")] = "Fire", [(649, "chill")] = "Ice", // Genesect
        [(658, "bond")] = "Ash", // Amphinobi Lien Naturel (forme 1 de PKHeX ; « Ash » de Cobblemon est la forme de combat)
        [(774, "red")] = "C-Red", [(774, "orange")] = "C-Orange", [(774, "yellow")] = "C-Yellow", [(774, "green")] = "C-Green",
        [(774, "blue")] = "C-Blue", [(774, "indigo")] = "C-Indigo", [(774, "violet")] = "C-Violet", // noyaux de Météno
        [(800, "duskmane")] = "Dusk", [(800, "dawnwings")] = "Dawn", // Necrozma
        [(925, "four")] = "Family of Four", // Famignol
        // Ogerpon téracristallisé : son masque
        [(1017, "tealtera")] = "Teal", [(1017, "wellspringtera")] = "Wellspring", [(1017, "hearthflametera")] = "Hearthflame",
        [(1017, "cornerstonetera")] = "Cornerstone",
    };

    /// <summary>Jeux où chercher le nom d'une forme, de la 9e à la 6e génération (Floette Éternelle : Légendes Z-A ; Pikachu Cosplayeur : 6e).</summary>
    private static readonly EntityContext[] KeyContexts =
        [EntityContext.Gen9, EntityContext.Gen9a, EntityContext.Gen8, EntityContext.Gen8a, EntityContext.Gen8b, EntityContext.Gen7, EntityContext.Gen6];

    /// <summary>Clé de forme d'un identifiant de Cobblemon (voir <see cref="FormKey"/>) ; forme normale : celle de base chez PKHeX.</summary>
    public static string FormKeyOf(ushort species, string formId)
    {
        var id = Key(formId);
        if (species == 0)
            return id is "" ? "normal" : id;
        if (IsBase(id))
            return GlobalLivingDex.FormKey(species, 0, FormContext);
        if (FormAliases.TryGetValue((species, id), out var alias))
            return alias;
        foreach (var context in KeyContexts)
        {
            var names = FormConverter.GetFormList(species, English.types, English.forms, GameInfo.GenderSymbolASCII, context);
            if (Array.Find(names, n => Key(n) == id) is { } name)
                return name;
        }
        return id;
    }

    /// <summary>Surnom donné en jeu, ou null : texte simple, ou composant de texte de Minecraft (« text »).</summary>
    public string? Nickname => Nbt["Nickname"] switch
    {
        NbtValue { Value: string s } => string.IsNullOrWhiteSpace(s) ? null : TextOf(s),
        NbtCompound c => c.String("text") is { Length: > 0 } t ? t : null,
        _ => null,
    };

    public int Level => (int)Math.Clamp(Nbt.Integer("Level") ?? 1, 1, 255);
    public bool IsShiny => Nbt.Flag("Shiny");

    /// <summary>Sexe au format de PKHeX : 0 mâle, 1 femelle, 2 aucun.</summary>
    public byte Gender => Nbt.String("Gender") switch { "MALE" => 0, "FEMALE" => 1, _ => 2 };

    /// <summary>Nature (index PKHeX), la menthe en priorité comme dans le jeu ; null si inconnue.</summary>
    public byte? Nature
    {
        get
        {
            var id = Nbt.String("MintedNature") is { Length: > 0 } minted ? minted : Nbt.String("Nature") ?? "";
            return Lookup(NatureIds, id) is var n and >= 0 ? (byte)n : null;
        }
    }

    /// <summary>Talent (index PKHeX) ; 0 si inconnu.</summary>
    public int Ability => Math.Max(0, Lookup(AbilityIds, Nbt.Compound("Ability")?.String("AbilityName") ?? ""));

    /// <summary>Capacités : numéro PKHeX (0 si inconnue) et identifiant de Cobblemon.</summary>
    public IReadOnlyList<(ushort Move, string Id)> Moves =>
        (Nbt.List("MoveSet")?.Items ?? []).OfType<NbtCompound>()
            .Select(m => m.String("MoveName") ?? "")
            .Where(id => id != "")
            .Select(id => ((ushort)Math.Max(0, Lookup(MoveIds, id)), id))
            .ToList();

    /// <summary>Objet tenu (« cobblemon:leftovers », « minecraft:stick ») ou null.</summary>
    public string? HeldItemId =>
        Nbt.Compound("HeldItem")?.String("id") is { Length: > 0 } id && id != "minecraft:air" ? id : null;

    /// <summary>Numéro PKHeX de l'objet tenu, s'il existe dans les jeux ; 0 sinon (objet de Minecraft) ou sans objet.</summary>
    public int HeldItem => HeldItemId is { } id ? Math.Max(0, Lookup(ItemIds, id)) : 0;

    /// <summary>IV de base (PV, Attaque, Défense, Atq. Spé., Déf. Spé., Vitesse), de 0 à 31.</summary>
    public byte[] Ivs
    {
        get
        {
            var ivs = Nbt.Compound("IVs")?.Compound("Base");
            return StatKeys.Select(k => (byte)Math.Clamp(ivs?.Integer(k) ?? 0, 0, 31)).ToArray();
        }
    }

    /// <summary>Ball de capture, au nom anglais de PKHeX quand il le connaît.</summary>
    public string BallId => Nbt.String("CaughtBall") ?? "";

    /// <summary>Dresseur d'origine : l'UUID du joueur (« PLAYER ») ou un nom (dresseur non joueur).</summary>
    public string OriginalTrainerId => Nbt.String("PokemonOriginalTrainer") ?? "";
    public string? OriginalTrainerName => Nbt.String("PokemonOriginalTrainerName") is { Length: > 0 } name ? name : null;

    /// <summary>
    /// Attaché à un enclos (Pasture) : le bloc garde son identifiant, le retirer du PC laisserait l'enclos en désaccord.
    /// À reprendre dans le jeu avant de le déposer.
    /// </summary>
    public bool IsTethered => Nbt.Contains("TetheringId");

    /// <summary>Le Pokémon sans compression (ce que garde <see cref="Data.BankPokemon.Data"/>).</summary>
    public byte[] ToBytes() => Services.Nbt.Write(Nbt, gzip: false);

    public static CobblemonPokemon FromBytes(byte[] data) => new(Services.Nbt.Read(data));

    public CobblemonPokemon Clone() => new((NbtCompound)Nbt.Clone());

    /// <summary>Nom affiché faute de mieux pour un identifiant inconnu de PKHeX : « minecraft:stick » → « stick ».</summary>
    public static string ShortId(string id) => (id.Contains(':') ? id[(id.IndexOf(':') + 1)..] : id).Replace('_', ' ');

    // ---------- Identifiants de Cobblemon → numéros de PKHeX ----------

    private static readonly string[] StatKeys =
        ["cobblemon:hp", "cobblemon:attack", "cobblemon:defence", "cobblemon:special_attack", "cobblemon:special_defence", "cobblemon:speed"];

    private static readonly GameStrings English = GameInfo.GetStrings("en");
    private static readonly Dictionary<string, int> SpeciesIds = Index(English.specieslist);
    private static readonly Dictionary<string, int> MoveIds = Index(English.movelist);
    private static readonly Dictionary<string, int> AbilityIds = Index(English.abilitylist);
    private static readonly Dictionary<string, int> NatureIds = Index(English.natures);
    private static readonly Dictionary<string, int> ItemIds = Index(English.itemlist);

    /// <summary>Numéro dans la liste, par identifiant (préfixe « cobblemon: » ignoré) ; -1 si absent.</summary>
    private static int Lookup(Dictionary<string, int> ids, string id) =>
        ids.TryGetValue(Key(id.Contains(':') ? id[(id.IndexOf(':') + 1)..] : id), out var index) ? index : -1;

    /// <summary>Premier numéro de chaque nom (PKHeX répète certains noms d'objets d'une génération à l'autre).</summary>
    private static Dictionary<string, int> Index(IReadOnlyList<string> names)
    {
        var ids = new Dictionary<string, int>();
        for (int i = 1; i < names.Count; i++)
            if (Key(names[i]) is { Length: > 0 } key)
                ids.TryAdd(key, i);
        return ids;
    }

    /// <summary>
    /// Identifiant à la Showdown : minuscules sans accents, ni espaces, ni ponctuation ; ♀ et ♂ deviennent f et m
    /// (« Nidoran♀ » → « nidoranf », « Flabébé » → « flabebe », « Will-O-Wisp » → « willowisp »).
    /// </summary>
    internal static string Key(string name)
    {
        var text = new StringBuilder(name.Length);
        foreach (var c in name.Replace("♀", "f").Replace("♂", "m").Normalize(NormalizationForm.FormD))
        {
            if (char.IsAsciiLetterOrDigit(c))
                text.Append(char.ToLowerInvariant(c));
        }
        return text.ToString();
    }

    private static readonly Dictionary<(ushort, string), byte> FormCache = [];

    /// <summary>Forme de PKHeX en 9e génération qui porte ce nom (<see cref="FormKey"/>, « Alola ») ; 0 si aucune.</summary>
    private static byte FormIndex(ushort species, string formKey)
    {
        lock (FormCache)
        {
            if (FormCache.TryGetValue((species, formKey), out var cached))
                return cached;
            var names = FormConverter.GetFormList(species, English.types, English.forms, GameInfo.GenderSymbolASCII, FormContext);
            var wanted = Key(formKey);
            var index = Array.FindIndex(names, n => Key(n) == wanted);
            return FormCache[(species, formKey)] = (byte)Math.Max(0, index);
        }
    }

    /// <summary>Texte d'un surnom enregistré comme composant JSON (« {"text":"Bob"} ») ou tel quel.</summary>
    private static string TextOf(string s)
    {
        if (!s.StartsWith('{') && !s.StartsWith('"'))
            return s;
        try
        {
            using var json = System.Text.Json.JsonDocument.Parse(s);
            return json.RootElement.ValueKind switch
            {
                System.Text.Json.JsonValueKind.String => json.RootElement.GetString() ?? s,
                System.Text.Json.JsonValueKind.Object when json.RootElement.TryGetProperty("text", out var t) => t.GetString() ?? s,
                _ => s,
            };
        }
        catch (System.Text.Json.JsonException)
        {
            return s;
        }
    }

    private static string FormatUuid(int[] parts)
    {
        var hex = string.Concat(parts.Select(p => ((uint)p).ToString("x8", CultureInfo.InvariantCulture)));
        return $"{hex[..8]}-{hex[8..12]}-{hex[12..16]}-{hex[16..20]}-{hex[20..]}";
    }
}
