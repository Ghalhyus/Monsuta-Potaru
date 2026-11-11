using PKHeX.Core;

namespace MonsutaPotaru.Services;

/// <summary>Famille d'un ruban, dans l'ordre de la page d'un suivi (titres de Pokébip, « Tous les rubans »).</summary>
public enum RibbonCategory { Champion, Contest, Battle, Commemorative, Colosseum }

/// <summary>
/// Un ruban compté par le suivi des rubans : <paramref name="Key"/> est le nom du ruban chez PKHeX sans « Ribbon »
/// (« ChampionG3 », celui de son image : <see cref="Ribbons.Image"/>) ; <paramref name="HowTo"/> dit comment l'obtenir.
/// </summary>
public sealed record RibbonInfo(string Key, string Name, RibbonCategory Category, string HowTo)
{
    /// <summary>Ruban « souvenir » : un compteur (rubans de 3e et 4e génération réunis), pas un simple oui / non.</summary>
    public int? MemoryMax => Key switch
    {
        Ribbons.ContestMemory => Ribbons.ContestMemoryMax,
        Ribbons.BattleMemory => Ribbons.BattleMemoryMax,
        _ => null,
    };

    /// <summary>Les rubans qu'un souvenir réunit (40 de concours, 8 de combat) ; null pour un ruban simple.</summary>
    public IReadOnlyList<RibbonPart>? Parts => Key switch
    {
        Ribbons.ContestMemory => Ribbons.ContestParts,
        Ribbons.BattleMemory => Ribbons.BattleParts,
        _ => null,
    };
}

/// <summary>
/// Un des rubans de 3e ou 4e génération réunis dans un souvenir (« Sang-Froid Super » de Hoenn, « Aptitude Duo ») :
/// <paramref name="Key"/> est son nom chez PKHeX sans « Ribbon » (« G3CoolSuper »), <paramref name="Group"/> sa région ou
/// sa génération dans la fenêtre du détail.
/// </summary>
public sealed record RibbonPart(string Key, string Name, string Group, string HowTo)
{
    public string Image => $"images/rubans/ribbon{Key.ToLowerInvariant()}.png";
}

/// <summary>Un ruban d'un souvenir sur un Pokémon : obtenu, manquant, ou null quand le jeu n'a gardé que le compteur.</summary>
public sealed record RibbonPartState(RibbonPart Part, bool? Owned);

/// <summary>
/// Un ruban sur un Pokémon : obtenu ou non ; pour un souvenir, le compteur (« 23 / 40 ») et le détail de ses rubans
/// (<see cref="Parts"/>).
/// </summary>
public sealed record RibbonState(RibbonInfo Ribbon, bool Owned, int? Count = null)
{
    /// <summary>Détail d'un souvenir, ruban par ruban ; null pour un ruban simple.</summary>
    public IReadOnlyList<RibbonPartState>? Parts { get; init; }

    /// <summary>
    /// Le détail ne dit pas lesquels : à partir de la 6e génération, le jeu ne garde que le compteur des souvenirs (sauf à
    /// 0 ou au complet, où il n'y a pas de doute).
    /// </summary>
    public bool PartsUnknown => Parts?.Any(p => p.Owned is null) == true;

    /// <summary>
    /// Ce que le ruban pèse dans la complétion : 1 pour un ruban simple, chacun de ses rubans pour un souvenir (40, 8),
    /// pour que les rubans de 3e et 4e génération comptent tous.
    /// </summary>
    public int Worth => Ribbon.Parts?.Count ?? 1;

    /// <summary>Rubans obtenus, comptés comme <see cref="Worth"/> : le compteur d'un souvenir (connu même quand le détail ne l'est pas).</summary>
    public int Earned => Ribbon.Parts is null ? (Owned ? 1 : 0) : Math.Min(Count ?? 0, Worth);

    /// <summary>Il en manque encore : ruban simple pas obtenu, ou souvenir pas au complet.</summary>
    public bool Missing => Earned < Worth;

    /// <summary>Souvenir au complet (40 rubans de concours, 8 de combat) : il a son image dorée.</summary>
    public bool Complete => Count is { } count && count >= Ribbon.MemoryMax;

    public string Image => Ribbons.Image(Ribbon, Complete);
}

/// <summary>
/// Les rubans que le suivi compte, et leur lecture dans un Pokémon de PKHeX (tous formats : 3e génération, Colosseum / XD,
/// jusqu'à la 9e).
/// <para>
/// Ne comptent que les rubans que n'importe quel Pokémon peut obtenir en jouant : pas ceux des distributions (Classique,
/// Premier, Évènement, Anniversaire, Souvenir, Souhait, Spécial, Champion du Monde, Vainqueur Championnat, Partenaire),
/// ni ceux des anciens tournois japonais (Marine, Ciel, Record, Pays, Monde, Champion Régional / National...), ni les
/// Marques, ni Exceptionnel (dû au hasard d'un Échange Miracle). Ceux de Colosseum / XD (National, Terre) comptent.
/// </para>
/// <para>
/// Les rubans de concours et de combat de 3e et 4e génération ne sont pas comptés un par un : comme dans les jeux à partir
/// de la 6e, ils deviennent « Souvenir Concours » (jusqu'à 40) et « Souvenir Combat » (jusqu'à 8), un ruban chacun, obtenu
/// dès le premier. Un Pokémon encore en 3e ou 4e génération est compté de la même façon (<see cref="MemoryCounts"/>).
/// </para>
/// La liste est la même pour tous les Pokémon, même ceux qui ne peuvent plus obtenir certains rubans (voulu : un futur
/// moyen de les renvoyer dans les anciens jeux).
/// </summary>
public static class Ribbons
{
    public const string ContestMemory = "CountMemoryContest";
    public const string BattleMemory = "CountMemoryBattle";
    public const int ContestMemoryMax = 40;
    public const int BattleMemoryMax = 8;

    private const string ContestGames = "Rubis Oméga, Saphir Alpha, Diamant Étincelant, Perle Scintillante";

    public static readonly IReadOnlyList<RibbonInfo> All =
    [
        // Panthéon
        new("ChampionG3", "Maître", RibbonCategory.Champion, "Vaincre la Ligue Pokémon (Rubis, Saphir, Émeraude, Rouge Feu, Vert Feuille)."),
        new("ChampionSinnoh", "Maître de Sinnoh", RibbonCategory.Champion, "Vaincre la Ligue Pokémon (Diamant, Perle, Platine, Diamant Étincelant, Perle Scintillante)."),
        new("ChampionKalos", "Maître de Kalos", RibbonCategory.Champion, "Vaincre la Ligue Pokémon (X, Y)."),
        new("ChampionG6Hoenn", "Maître de Hoenn", RibbonCategory.Champion, "Vaincre la Ligue Pokémon (Rubis Oméga, Saphir Alpha)."),
        new("ChampionAlola", "Maître d'Alola", RibbonCategory.Champion, "Devenir Maître de la Ligue (Soleil, Lune, Ultra-Soleil, Ultra-Lune)."),
        new("ChampionGalar", "Maître de Galar", RibbonCategory.Champion, "Devenir Maître de Galar (Épée, Bouclier)."),
        new("ChampionPaldea", "Maître de Paldea", RibbonCategory.Champion, "Vaincre la Ligue Pokémon (Écarlate, Violet)."),

        // Concours
        new(ContestMemory, "Souvenir Concours", RibbonCategory.Contest,
            "Les rubans de concours de 3e et 4e génération (Rubis, Saphir, Émeraude ; Diamant, Perle, Platine), réunis en un seul à "
            + "partir de la 6e : 40 en tout, et le ruban devient doré."),
        new("MasterCoolness", "Maître Sang-Froid", RibbonCategory.Contest, $"Gagner le Concours Master Sang-Froid ({ContestGames})."),
        new("MasterBeauty", "Maître Beauté", RibbonCategory.Contest, $"Gagner le Concours Master Beauté ({ContestGames})."),
        new("MasterCuteness", "Maître Grâce", RibbonCategory.Contest, $"Gagner le Concours Master Grâce ({ContestGames})."),
        new("MasterCleverness", "Maître Intelligence", RibbonCategory.Contest, $"Gagner le Concours Master Intelligence ({ContestGames})."),
        new("MasterToughness", "Maître Robustesse", RibbonCategory.Contest, $"Gagner le Concours Master Robustesse ({ContestGames})."),
        new("ContestStar", "Star des Concours", RibbonCategory.Contest,
            "Gagner tous les Concours Master (Rubis Oméga, Saphir Alpha ; Diamant Étincelant, Perle Scintillante : sauf le Concours Étincelant / Scintillant)."),
        new("TwinklingStar", "Étoile Brillante", RibbonCategory.Contest, "Gagner le Concours Étincelant / Scintillant Master (Diamant Étincelant, Perle Scintillante)."),

        // Installations de combat
        new(BattleMemory, "Souvenir Combat", RibbonCategory.Battle,
            "Les rubans de la Tour de Combat de 3e et 4e génération (Gagnant, Victoire ; les six Aptitude), réunis en un seul à "
            + "partir de la 6e : 8 en tout, et le ruban devient doré."),
        new("BattlerSkillful", "Élite du Combat", RibbonCategory.Battle, "Vaincre une Châtelaine en mode Normal à la Maison de Combat (X, Y, Rubis Oméga, Saphir Alpha)."),
        new("BattlerExpert", "Génie du Combat", RibbonCategory.Battle, "Vaincre une Châtelaine en mode Super à la Maison de Combat (X, Y, Rubis Oméga, Saphir Alpha)."),
        new("BattleTreeGreat", "Élite de l'Arbre", RibbonCategory.Battle, "Vaincre un Dresseur Légende en mode Normal à l'Arbre de Combat (Soleil, Lune, Ultra-Soleil, Ultra-Lune)."),
        new("BattleTreeMaster", "Maître de l'Arbre", RibbonCategory.Battle, "Vaincre un Dresseur Légende en mode Super à l'Arbre de Combat (Soleil, Lune, Ultra-Soleil, Ultra-Lune)."),
        new("TowerMaster", "Maître de la Tour", RibbonCategory.Battle,
            "Vaincre le maître de la Tour de Combat avec toute l'équipe (Épée, Bouclier ; Diamant Étincelant, Perle Scintillante)."),
        new("MasterRank", "Top du Classement", RibbonCategory.Battle, "Gagner un combat classé au rang Master Ball avec toute l'équipe (Épée, Bouclier ; Écarlate, Violet)."),

        // Commémoratifs
        new("Artist", "Artiste", RibbonCategory.Commemorative, "Être exposé au Musée de Nénucrique après un Concours Master (Rubis, Saphir, Émeraude)."),
        new("Effort", "Effort", RibbonCategory.Commemorative, "Avoir 510 EV (de la 3e à la 9e génération, auprès d'un habitant de la région)."),
        new("Alert", "Alerte", RibbonCategory.Commemorative, "Offert le lundi (4e génération, X, Y, Rubis Oméga, Saphir Alpha, Diamant Étincelant, Perle Scintillante)."),
        new("Shock", "Choc", RibbonCategory.Commemorative, "Offert le mardi (mêmes jeux que le ruban Alerte)."),
        new("Downcast", "Déprime", RibbonCategory.Commemorative, "Offert le mercredi (mêmes jeux que le ruban Alerte)."),
        new("Careless", "Négligence", RibbonCategory.Commemorative, "Offert le jeudi (mêmes jeux que le ruban Alerte)."),
        new("Relax", "Détente", RibbonCategory.Commemorative, "Offert le vendredi (mêmes jeux que le ruban Alerte)."),
        new("Snooze", "Sieste", RibbonCategory.Commemorative, "Offert le samedi (mêmes jeux que le ruban Alerte)."),
        new("Smile", "Sourire", RibbonCategory.Commemorative, "Offert le dimanche (mêmes jeux que le ruban Alerte)."),
        new("Gorgeous", "Sublime", RibbonCategory.Commemorative, "Acheté 10 000 ₽ (Diamant, Perle, Platine, Rubis Oméga, Saphir Alpha, Diamant Étincelant, Perle Scintillante)."),
        new("Royal", "Royal", RibbonCategory.Commemorative, "Acheté 100 000 ₽ (mêmes jeux que le ruban Sublime)."),
        new("GorgeousRoyal", "Royal Sublime", RibbonCategory.Commemorative, "Acheté 999 999 ₽ (mêmes jeux que le ruban Sublime)."),
        new("Footprint", "Empreinte", RibbonCategory.Commemorative,
            "Amitié au maximum (Diamant, Perle, Platine), ou au moins 30 niveaux gagnés depuis la capture (X, Y, Rubis Oméga, Saphir Alpha, Soleil, Lune, Diamant Étincelant…)."),
        new("Legend", "Légende", RibbonCategory.Commemorative, "Vaincre Red au Mont Argenté (HeartGold, SoulSilver)."),
        new("BestFriends", "Affection", RibbonCategory.Commemorative,
            "Affection ou amitié au maximum (Poké-Récré, Poké Détente ; Épée, Bouclier, Diamant Étincelant, Perle Scintillante, Écarlate, Violet)."),
        new("Training", "Entraînement", RibbonCategory.Commemorative, "Réussir tous les défis du SPV (X, Y, Rubis Oméga, Saphir Alpha)."),
        new("BattleRoyale", "Champion Royal", RibbonCategory.Commemorative, "Triompher au Dôme Royal en rang Master (Soleil, Lune, Ultra-Soleil, Ultra-Lune)."),
        new("Hisui", "Hisui", RibbonCategory.Commemorative, "Être pris en photo au Studio Photo de Rusti-Cité (Légendes Arceus)."),

        // Colosseum / XD
        new("National", "National", RibbonCategory.Colosseum, "Purifier un Pokémon Obscur (Pokémon Colosseum, Pokémon XD)."),
        new("Earth", "Terre", RibbonCategory.Colosseum, "Enchaîner 100 victoires au Mont Bataille (Pokémon Colosseum, Pokémon XD)."),
    ];

    private static readonly (string Key, string Name)[] ContestCategories =
        [("Cool", "Sang-Froid"), ("Beauty", "Beauté"), ("Cute", "Grâce"), ("Smart", "Intelligence"), ("Tough", "Robustesse")];

    private static readonly (string Key, string Suffix, string Rank)[] HoennRanks =
        [("", "", "Normal"), ("Super", " Super", "Super"), ("Hyper", " Hyper", "Hyper"), ("Master", " Master", "Master")];

    private static readonly (string Key, string Suffix, string Rank)[] SinnohRanks =
        [("", "", "Normal"), ("Great", " Méga", "Méga"), ("Ultra", " Ultra", "Ultra"), ("Master", " Master", "Master")];

    /// <summary>
    /// Les 40 rubans de Souvenir Concours : 5 catégories × 4 rangs à Hoenn (Normal, Super, Hyper, Master), autant à
    /// Sinnoh (Normal, Méga, Ultra, Master), dans l'ordre des catégories.
    /// </summary>
    public static readonly IReadOnlyList<RibbonPart> ContestParts =
    [
        .. ContestCategories.SelectMany(c => HoennRanks.Select(r => new RibbonPart($"G3{c.Key}{r.Key}", c.Name + r.Suffix, "Hoenn",
            $"Gagner un Concours {c.Name} de rang {r.Rank} (Rubis, Saphir, Émeraude)."))),
        .. ContestCategories.SelectMany(c => SinnohRanks.Select(r => new RibbonPart($"G4{c.Key}{r.Key}", c.Name + r.Suffix, "Sinnoh",
            $"Gagner un Super Concours {c.Name} de rang {r.Rank} (Diamant, Perle, Platine)."))),
    ];

    private const string Tower3 = "Tour de Combat, 3e génération";
    private const string Tower4 = "Tour de Combat, 4e génération";
    private const string Tower4Games = "Diamant, Perle, Platine, HeartGold, SoulSilver";

    /// <summary>Les 8 rubans de Souvenir Combat : Gagnant et Victoire de 3e génération, les six Aptitude de 4e.</summary>
    public static readonly IReadOnlyList<RibbonPart> BattleParts =
    [
        new("Winning", "Gagnant", Tower3, "Enchaîner 50 victoires à la Tour de Combat, au niveau 50 (Rubis, Saphir, Émeraude)."),
        new("Victory", "Victoire", Tower3, "Enchaîner 50 victoires à la Tour de Combat, au niveau 100 (Rubis, Saphir, Émeraude)."),
        new("Ability", "Aptitude", Tower4, $"Vaincre le maître de la Tour de Combat au 21e combat, en Simple ({Tower4Games})."),
        new("AbilityGreat", "Aptitude Méga", Tower4, $"Vaincre le maître de la Tour de Combat au 49e combat, en Simple ({Tower4Games})."),
        new("AbilityDouble", "Aptitude Duo", Tower4, $"Réussir le défi Duo de la Tour de Combat ({Tower4Games})."),
        new("AbilityMulti", "Aptitude Multi", Tower4, $"Réussir le défi Multi de la Tour de Combat, avec un partenaire du jeu ({Tower4Games})."),
        new("AbilityPair", "Aptitude Paire", Tower4, $"Réussir le défi Multi de la Tour de Combat avec un ami ({Tower4Games})."),
        new("AbilityWorld", "Aptitude Monde", Tower4, $"Atteindre le rang 5 du défi Wi-Fi de la Tour de Combat ({Tower4Games})."),
    ];

    /// <summary>Titre de chaque famille sur la page d'un suivi.</summary>
    public static string CategoryName(RibbonCategory category) => category switch
    {
        RibbonCategory.Champion => "Panthéon",
        RibbonCategory.Contest => "Concours",
        RibbonCategory.Battle => "Installations de combat",
        RibbonCategory.Commemorative => "Commémoratifs",
        _ => "Pokémon Colosseum et XD",
    };

    /// <summary>Image du ruban (PKHeX, « ribbonchampiong3.png ») ; un souvenir au complet a son image dorée (« …2.png »).</summary>
    public static string Image(RibbonInfo ribbon, bool complete = false) =>
        $"images/rubans/ribbon{ribbon.Key.ToLowerInvariant()}{(complete && ribbon.MemoryMax is not null ? "2" : "")}.png";

    /// <summary>Les rubans comptés, dans l'ordre de <see cref="All"/>, lus dans ce Pokémon (aucun en 1re et 2e génération).</summary>
    public static List<RibbonState> Read(PKM? pk)
    {
        var (contest, battle) = pk is null ? (0, 0) : MemoryCounts(pk);
        return [.. All.Select(r => r.Key switch
        {
            ContestMemory => new RibbonState(r, contest > 0, contest) { Parts = PartsOf(pk, ContestParts, contest) },
            BattleMemory => new RibbonState(r, battle > 0, battle) { Parts = PartsOf(pk, BattleParts, battle) },
            _ => new RibbonState(r, pk is not null && Has(pk, r.Key)),
        })];
    }

    /// <summary>
    /// Détail d'un souvenir. Jusqu'à la 5e génération, chaque ruban est connu (en 3e génération, par le rang atteint dans
    /// chaque catégorie : rang 2, c'est Normal et Super). À partir de la 6e, le jeu ne garde que le compteur : inconnu,
    /// sauf à 0 (aucun) ou au complet (tous).
    /// </summary>
    private static List<RibbonPartState> PartsOf(PKM? pk, IReadOnlyList<RibbonPart> parts, int count)
    {
        if (pk is IRibbonSetMemory6)
        {
            bool? all = count == 0 ? false : count >= parts.Count ? true : null;
            return [.. parts.Select(p => new RibbonPartState(p, all))];
        }
        return [.. parts.Select(p => new RibbonPartState(p, pk is not null && HasPart(pk, p.Key)))];
    }

    /// <summary>Un ruban de 3e ou 4e génération (avant son passage dans un souvenir), par son nom chez PKHeX.</summary>
    private static bool HasPart(PKM pk, string key)
    {
        if (pk is IRibbonSetOnly3 g3 && key.StartsWith("G3", StringComparison.Ordinal))
        {
            // 3e génération et Colosseum / XD : le rang atteint dans la catégorie (0 à 4)
            var category = ContestCategories.First(c => key.AsSpan(2).StartsWith(c.Key, StringComparison.Ordinal)).Key;
            var rank = Array.FindIndex(HoennRanks, r => r.Key == key[(2 + category.Length)..]) + 1;
            var reached = category switch
            {
                "Cool" => g3.RibbonCountG3Cool,
                "Beauty" => g3.RibbonCountG3Beauty,
                "Cute" => g3.RibbonCountG3Cute,
                "Smart" => g3.RibbonCountG3Smart,
                _ => g3.RibbonCountG3Tough,
            };
            return reached >= rank;
        }
        // 4e et 5e génération (IRibbonSetUnique4), Gagnant et Victoire (IRibbonSetUnique3) : un booléen par ruban.
        if (pk is not (IRibbonSetUnique3 or IRibbonSetUnique4))
            return false;
        return pk.GetType().GetProperty("Ribbon" + key)?.GetValue(pk) is true;
    }

    /// <summary>
    /// Total de la complétion : chaque ruban simple, et chacun des rubans réunis dans les souvenirs (88 : 40 rubans
    /// simples, 40 de concours, 8 de combat).
    /// </summary>
    public static int Total => All.Sum(r => r.Parts?.Count ?? 1);

    /// <summary>Rubans que ce Pokémon a, sur <see cref="Total"/> (ceux des souvenirs un par un).</summary>
    public static int CountOwned(PKM? pk) => Read(pk).Sum(r => r.Earned);

    /// <summary>
    /// Compteurs des souvenirs : ceux du Pokémon à partir de la 6e génération ; avant, ses rubans de concours (5 catégories
    /// × 4 rangs en Hoenn, autant en Sinnoh) et de la Tour de Combat (Gagnant, Victoire, les six Aptitude) comptés comme le
    /// fait le jeu à son arrivée en 6e génération.
    /// </summary>
    public static (int Contest, int Battle) MemoryCounts(PKM pk)
    {
        if (pk is IRibbonSetMemory6 memory)
            return (Math.Min((int)memory.RibbonCountMemoryContest, ContestMemoryMax), Math.Min((int)memory.RibbonCountMemoryBattle, BattleMemoryMax));
        var contest = 0;
        var battle = 0;
        if (pk is IRibbonSetOnly3 g3) // 3e génération et Colosseum / XD : un rang (0 à 4) par catégorie
            contest += Math.Min((int)g3.RibbonCountG3Cool, 4) + Math.Min((int)g3.RibbonCountG3Beauty, 4) + Math.Min((int)g3.RibbonCountG3Cute, 4)
                + Math.Min((int)g3.RibbonCountG3Smart, 4) + Math.Min((int)g3.RibbonCountG3Tough, 4);
        if (pk is IRibbonSetUnique4 g4) // 4e et 5e génération : chaque ruban de Hoenn et de Sinnoh à part
        {
            contest += Count(g4.RibbonG3Cool, g4.RibbonG3CoolSuper, g4.RibbonG3CoolHyper, g4.RibbonG3CoolMaster,
                g4.RibbonG3Beauty, g4.RibbonG3BeautySuper, g4.RibbonG3BeautyHyper, g4.RibbonG3BeautyMaster,
                g4.RibbonG3Cute, g4.RibbonG3CuteSuper, g4.RibbonG3CuteHyper, g4.RibbonG3CuteMaster,
                g4.RibbonG3Smart, g4.RibbonG3SmartSuper, g4.RibbonG3SmartHyper, g4.RibbonG3SmartMaster,
                g4.RibbonG3Tough, g4.RibbonG3ToughSuper, g4.RibbonG3ToughHyper, g4.RibbonG3ToughMaster,
                g4.RibbonG4Cool, g4.RibbonG4CoolGreat, g4.RibbonG4CoolUltra, g4.RibbonG4CoolMaster,
                g4.RibbonG4Beauty, g4.RibbonG4BeautyGreat, g4.RibbonG4BeautyUltra, g4.RibbonG4BeautyMaster,
                g4.RibbonG4Cute, g4.RibbonG4CuteGreat, g4.RibbonG4CuteUltra, g4.RibbonG4CuteMaster,
                g4.RibbonG4Smart, g4.RibbonG4SmartGreat, g4.RibbonG4SmartUltra, g4.RibbonG4SmartMaster,
                g4.RibbonG4Tough, g4.RibbonG4ToughGreat, g4.RibbonG4ToughUltra, g4.RibbonG4ToughMaster);
            battle += Count(g4.RibbonAbility, g4.RibbonAbilityGreat, g4.RibbonAbilityDouble, g4.RibbonAbilityMulti,
                g4.RibbonAbilityPair, g4.RibbonAbilityWorld);
        }
        if (pk is IRibbonSetUnique3 u3)
            battle += Count(u3.RibbonWinning, u3.RibbonVictory);
        return (Math.Min(contest, ContestMemoryMax), Math.Min(battle, BattleMemoryMax));
    }

    private static int Count(params bool[] ribbons) => ribbons.Count(r => r);

    /// <summary>Le Pokémon a-t-il ce ruban (hors souvenirs) ? Un format qui ne connaît pas le ruban répond non.</summary>
    private static bool Has(PKM pk, string key) => key switch
    {
        "ChampionG3" => pk is IRibbonSetCommon3 { RibbonChampionG3: true },
        "Artist" => pk is IRibbonSetCommon3 { RibbonArtist: true },
        "Effort" => pk is IRibbonSetCommon3 { RibbonEffort: true },
        "ChampionSinnoh" => pk is IRibbonSetCommon4 { RibbonChampionSinnoh: true },
        "Alert" => pk is IRibbonSetCommon4 { RibbonAlert: true },
        "Shock" => pk is IRibbonSetCommon4 { RibbonShock: true },
        "Downcast" => pk is IRibbonSetCommon4 { RibbonDowncast: true },
        "Careless" => pk is IRibbonSetCommon4 { RibbonCareless: true },
        "Relax" => pk is IRibbonSetCommon4 { RibbonRelax: true },
        "Snooze" => pk is IRibbonSetCommon4 { RibbonSnooze: true },
        "Smile" => pk is IRibbonSetCommon4 { RibbonSmile: true },
        "Gorgeous" => pk is IRibbonSetCommon4 { RibbonGorgeous: true },
        "Royal" => pk is IRibbonSetCommon4 { RibbonRoyal: true },
        "GorgeousRoyal" => pk is IRibbonSetCommon4 { RibbonGorgeousRoyal: true },
        "Footprint" => pk is IRibbonSetCommon4 { RibbonFootprint: true },
        "Legend" => pk is IRibbonSetCommon4 { RibbonLegend: true },
        "ChampionKalos" => pk is IRibbonSetCommon6 { RibbonChampionKalos: true },
        "ChampionG6Hoenn" => pk is IRibbonSetCommon6 { RibbonChampionG6Hoenn: true },
        "BestFriends" => pk is IRibbonSetCommon6 { RibbonBestFriends: true },
        "Training" => pk is IRibbonSetCommon6 { RibbonTraining: true },
        "BattlerSkillful" => pk is IRibbonSetCommon6 { RibbonBattlerSkillful: true },
        "BattlerExpert" => pk is IRibbonSetCommon6 { RibbonBattlerExpert: true },
        "ContestStar" => pk is IRibbonSetCommon6 { RibbonContestStar: true },
        "MasterCoolness" => pk is IRibbonSetCommon6 { RibbonMasterCoolness: true },
        "MasterBeauty" => pk is IRibbonSetCommon6 { RibbonMasterBeauty: true },
        "MasterCuteness" => pk is IRibbonSetCommon6 { RibbonMasterCuteness: true },
        "MasterCleverness" => pk is IRibbonSetCommon6 { RibbonMasterCleverness: true },
        "MasterToughness" => pk is IRibbonSetCommon6 { RibbonMasterToughness: true },
        "ChampionAlola" => pk is IRibbonSetCommon7 { RibbonChampionAlola: true },
        "BattleRoyale" => pk is IRibbonSetCommon7 { RibbonBattleRoyale: true },
        "BattleTreeGreat" => pk is IRibbonSetCommon7 { RibbonBattleTreeGreat: true },
        "BattleTreeMaster" => pk is IRibbonSetCommon7 { RibbonBattleTreeMaster: true },
        "ChampionGalar" => pk is IRibbonSetCommon8 { RibbonChampionGalar: true },
        "TowerMaster" => pk is IRibbonSetCommon8 { RibbonTowerMaster: true },
        "MasterRank" => pk is IRibbonSetCommon8 { RibbonMasterRank: true },
        "TwinklingStar" => pk is IRibbonSetCommon8 { RibbonTwinklingStar: true },
        "Hisui" => pk is IRibbonSetCommon8 { RibbonHisui: true },
        "ChampionPaldea" => pk is IRibbonSetCommon9 { RibbonChampionPaldea: true },
        "National" => pk is IRibbonSetEvent3 { RibbonNational: true },
        "Earth" => pk is IRibbonSetEvent3 { RibbonEarth: true },
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Ruban inconnu"),
    };
}
