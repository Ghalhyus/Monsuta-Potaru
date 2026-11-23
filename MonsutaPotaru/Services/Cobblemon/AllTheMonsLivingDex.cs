using PKHeX.Core;
using MonsutaPotaru.Data;

namespace MonsutaPotaru.Services;

/// <summary>
/// Living dex All the Mons : compté dans l'équipe et le PC d'un monde Minecraft stocké (chromatiques compris), un par
/// compte. Mêmes modes et mêmes cases que le living dex global (<see cref="GlobalLivingDex"/>), adaptés à ce que le modpack
/// contient (Cobblemon 1.8.4 et ses mods : modccc et Mega Showdown rendent les 1 025 espèces disponibles, zamega ajoute
/// des Méga) :
/// <list type="bullet">
/// <item>retirées : ce que Cobblemon n'a pas (Pichu Troizépi, Pokémon Dominants, Pikachu et Évoli Partenaires de Let's
/// Go, capes de Papilord, motifs de Lépidonille et Pérégrain, Arceus Plaque Légende, Fragilady Reine) ;</item>
/// <item>ajoutées en Complet et en Ultime : toutes les Méga de Légendes Z-A et de Méga-Dimension (celles de PKHeX, que
/// Mega Showdown et zamega ont toutes), Stari Patrickyu (forme propre au modpack), Floette Éternelle, et les deux espèces
/// du modpack, Piglich et Creepyon (aussi en National, après Pêchaminus) ;</item>
/// <item>ajoutées en Ultime : les coupes de Couafarel (celles des jeux et les six de Cobblemon : dans Cobblemon, la coupe
/// tient dans le PC) et les trois motifs de Prismillon propres à Cobblemon.</item>
/// </list>
/// Une forme est reconnue par <see cref="CobblemonPokemon.FormKey"/>. Tables relevées le 2026-11-16 dans les fichiers du
/// modpack (dossiers <c>data/cobblemon/species</c> des mods) : à revoir à une mise à jour du modpack.
/// </summary>
public static class AllTheMonsLivingDex
{
    public static readonly LivingDexGameInfo Game =
        new(LivingDexGame.AllTheMons, "All the Mons", "minecraft/all-the-mons.png", null, 0, "", [], GlobalLivingDex.MaxSpecies);

    /// <summary>Images propres au modpack (rendus des modèles 3D du mod, <c>outils/rendu-cobblemon.py</c>), versionnées.</summary>
    public const string ImageFolder = "minecraft/pokemon/";

    public static bool Is(LivingDexGame game) => game == LivingDexGame.AllTheMons;

    public static IReadOnlyList<GlobalSlot> Slots(LivingDexMode mode) => mode switch
    {
        LivingDexMode.Complete => CompleteSlots.Value,
        LivingDexMode.Ultimate => UltimateSlots.Value,
        _ => NationalSlots.Value,
    };

    private const ushort Staryu = 120, Mothim = 414, Scatterbug = 664, Spewpa = 665, Vivillon = 666, Floette = 670,
        Furfrou = 676, Pumpkaboo = 710, Gourgeist = 711;

    /// <summary>Les deux espèces du modpack, avec le numéro que leur donne le mod (après les 1 025 du national).</summary>
    private static readonly GlobalSlot[] CustomSpecies =
    [
        new(9901, 0, CobblemonPokemon.FormContext, "normal", "Piglich", AnyForm: true, Image: $"{ImageFolder}piglich.png"),
        new(9902, 0, CobblemonPokemon.FormContext, "normal", "Creepyon", AnyForm: true, Image: $"{ImageFolder}creepyon.png"),
    ];

    /// <summary>Formes du Complet et de l'Ultime que Cobblemon n'a pas (voir le résumé de la classe).</summary>
    private static bool InModpack(GlobalSlot slot) =>
        slot.FormKey is not ("Starter" or "Spiky" or "Legend" or "Lady")
        && !(slot.FormKey == "Large" && slot.Species is not (Pumpkaboo or Gourgeist)) // Dominants (pas la grande taille de Pitrouille)
        && !(slot.Species is Mothim or Scatterbug or Spewpa && slot.Form > 0);

    /// <summary>Formes du modpack absentes du global, ajoutées au Complet et à l'Ultime.</summary>
    private static IEnumerable<GlobalSlot> ModpackForms()
    {
        foreach (var mega in MegaSlots())
            yield return mega;
        yield return new(Staryu, 0, CobblemonPokemon.FormContext, "patrickyu", $"{GlobalLivingDex.SpeciesName(Staryu)} (Patrickyu)",
            Image: $"{ImageFolder}120-patrickyu.png");
        const byte eternal = 5;
        yield return new(Floette, eternal, EntityContext.Gen9a, "Eternal",
            GlobalLivingDex.UltimateName(Floette, eternal, EntityContext.Gen9a, "Eternal"));
        foreach (var custom in CustomSpecies)
            yield return custom;
    }

    /// <summary>
    /// Méga-Évolutions et Primo-Résurgences de Légendes Z-A (PKHeX, table de Légendes Z-A : celles de X / Y et de Rubis
    /// Oméga / Saphir Alpha, et les nouvelles, Méga-Dimension comprise) : une case chacune, un exemplaire de plus qui tient
    /// la gemme ; quand l'espèce a plusieurs formes, celle qu'il doit avoir (Méga-Mistigrix ♀ : une femelle ; Méga-Floette :
    /// Floette Éternelle ; Méga-Magearna Couleur du Passé). Celles déjà dans le global gardent leur case (même clé).
    /// </summary>
    private static IEnumerable<GlobalSlot> MegaSlots()
    {
        var table = PersonalTable.ZA;
        for (ushort species = 1; species <= Math.Min((int)table.MaxSpeciesID, GlobalLivingDex.MaxSpecies); species++)
        {
            var names = FormNames(species, EntityContext.Gen9a);
            var stones = Enumerable.Range(0, names.Length).Select(f => ItemStorage9ZA.GetExpectedMegaStoneOrPrimalOrb(species, (byte)f)).ToArray();
            for (byte form = 0; form < names.Length; form++)
            {
                if (!FormInfo.IsMegaForm(species, form) && !FormInfo.IsPrimalForm(species, form))
                    continue;
                // Formes jumelles (« F-Mega », « Original-Mega ») : PKHeX ne donne la gemme qu'à l'une d'elles.
                var stone = stones[form] != 0 ? stones[form] : stones.FirstOrDefault(s => s != 0);
                if (stone == 0)
                    continue; // Méga-Rayquaza, sans gemme : déjà dans le global (Draco-Ascension)
                var name = names[form];
                var dash = name.IndexOf("-Mega", StringComparison.Ordinal);
                string? baseKey = null;
                if (dash > 0)
                    baseKey = name[..dash];
                else if (species == Floette)
                    baseKey = "Eternal";
                yield return new GlobalSlot(species, form, EntityContext.Gen9a, name, MegaName(species, name, baseKey),
                    HeldItem: stone, BaseFormKey: baseKey);
            }
        }
    }

    /// <summary>« Méga-Raichu X », « Méga-Mistigrix ♀ », « Méga-Nigirigon (forme Affalée) », « Primo-Kyogre ».</summary>
    private static string MegaName(ushort species, string formName, string? baseKey)
    {
        var name = GlobalLivingDex.SpeciesName(species);
        var prefix = formName == "Primal" ? "Primo" : "Méga";
        string suffix;
        if (formName is "Mega X" or "Mega Y" or "Mega Z")
            suffix = $" {formName[^1]}";
        else if (baseKey is "M" or "F")
            suffix = baseKey == "M" ? " ♂" : " ♀";
        else if (baseKey is null or "Normal" || species == Floette)
            suffix = "";
        else if (baseKey == "Original")
            suffix = " (Couleur du Passé)"; // Magearna (PKHeX abrège en « Passé »)
        else
        {
            var names = FormNames(species, EntityContext.Gen9a);
            var baseForm = Array.IndexOf(names, baseKey);
            suffix = baseForm < 0 ? "" : $" ({GlobalLivingDex.FrenchForm(species, (byte)baseForm, EntityContext.Gen9a)})";
        }
        return $"{prefix}-{name}{suffix}";
    }

    /// <summary>
    /// Coupes de Couafarel : les neuf des jeux, puis les six de Cobblemon ; motifs de Prismillon propres à Cobblemon.
    /// Noms français des formes de Cobblemon : traduits pour le site (le mod n'est qu'en anglais).
    /// </summary>
    private static IEnumerable<GlobalSlot> UltimateExtras()
    {
        var furfrou = GlobalLivingDex.SpeciesName(Furfrou);
        var trims = FormNames(Furfrou, EntityContext.Gen9);
        for (byte form = 1; form < trims.Length; form++)
            yield return new(Furfrou, form, EntityContext.Gen9, trims[form],
                $"{furfrou} (coupe {GlobalLivingDex.FrenchForm(Furfrou, form, EntityContext.Gen9)})");
        foreach (var (id, french) in CobblemonTrims)
            yield return new(Furfrou, 0, EntityContext.Gen9, id, $"{furfrou} (coupe {french})", Image: $"{ImageFolder}676-{id}.png");
        var vivillon = GlobalLivingDex.SpeciesName(Vivillon);
        foreach (var (id, french) in CobblemonPatterns)
            yield return new(Vivillon, 0, EntityContext.Gen9, id, $"{vivillon} (motif {french})", Image: $"{ImageFolder}666-{id}.png");
    }

    /// <summary>Coupes de Couafarel propres à Cobblemon (identifiant de forme, nom français).</summary>
    private static readonly (string Id, string French)[] CobblemonTrims =
        [("matcha", "Matcha"), ("lavender", "Lavande"), ("cinnamon", "Cannelle"), ("crusader", "Croisé"), ("rocker", "Rockeur"), ("mourner", "Deuil")];

    /// <summary>Motifs de Prismillon propres à Cobblemon (identifiant de forme, nom français).</summary>
    private static readonly (string Id, string French)[] CobblemonPatterns = [("inferno", "Inferno"), ("void", "Néant"), ("forsaken", "Abandon")];

    private static readonly Lazy<IReadOnlyList<GlobalSlot>> NationalSlots =
        new(() => [.. GlobalLivingDex.Slots(LivingDexMode.National), .. CustomSpecies]);

    private static readonly Lazy<IReadOnlyList<GlobalSlot>> CompleteSlots =
        new(() => Merge(GlobalLivingDex.Slots(LivingDexMode.Complete).Where(InModpack).Concat(ModpackForms())));

    private static readonly Lazy<IReadOnlyList<GlobalSlot>> UltimateSlots =
        new(() => Merge(SingleFormNames(GlobalLivingDex.Slots(LivingDexMode.Ultimate).Where(InModpack).ToList())
            .Concat(ModpackForms()).Concat(UltimateExtras())));

    /// <summary>
    /// Espèce à qui il ne reste qu'une case (Papilord, Lépidonille, Pérégrain : une seule forme dans Cobblemon) : son nom
    /// seul, sans celui de la forme (« Papilord », pas « Papilord (cape Plante) »).
    /// </summary>
    private static IEnumerable<GlobalSlot> SingleFormNames(List<GlobalSlot> slots)
    {
        var alone = slots.Where(s => !s.ExtraCopy).GroupBy(s => s.Species).Where(g => g.Count() == 1).Select(g => g.Key).ToHashSet();
        var trimmed = new HashSet<ushort> { Mothim, Scatterbug, Spewpa };
        return slots.Select(s => !s.ExtraCopy && alone.Contains(s.Species) && trimmed.Contains(s.Species)
            ? s with { Name = GlobalLivingDex.SpeciesName(s.Species) } : s);
    }

    /// <summary>
    /// Cases réunies sans doublon (une même case : la première gardée, celle du global), dans l'ordre du national, puis
    /// formes, Méga, Gigamax ; mâle et femelle, décorations de Charmilly restent à part.
    /// </summary>
    private static List<GlobalSlot> Merge(IEnumerable<GlobalSlot> slots)
    {
        var seen = new HashSet<((ushort, int, string), byte?, byte?)>();
        return slots.Where(s => seen.Add((GlobalLivingDex.Key(s), s.Gender, s.Sweet)))
            .Select((s, i) => (Slot: s, Order: i))
            .OrderBy(s => s.Slot.Species).ThenBy(s => GlobalLivingDex.Key(s.Slot).Item2).ThenBy(s => s.Order)
            .Select(s => s.Slot).ToList();
    }

    private static readonly GameStrings English = GameInfo.GetStrings("en");

    private static string[] FormNames(ushort species, EntityContext context) =>
        FormConverter.GetFormList(species, English.types, English.forms, GameInfo.GenderSymbolASCII, context);

    /// <summary>Pokémon d'un monde vu par ce living dex (forme reconnue par son nom, voir <see cref="CobblemonPokemon.FormKey"/>).</summary>
    public static GlobalLivingDex.Copy Copy(int id, CobblemonPokemon mon) =>
        new(id, mon.DexNumber, mon.Form, CobblemonPokemon.FormContext, mon.Gender, mon.HeldItem, mon.Sweet, mon.CanGigantamax,
            mon.Species == LivingDexGames.Rayquaza && mon.ReadMoves().Any(m => m.Move == LivingDexGames.DragonAscent),
            Key: mon.FormKey, Base: mon.IsBaseForm && mon.FormKey == CobblemonPokemon.FormKeyOf(mon.Species, "normal")); // pas Zarbi « ? »
}
