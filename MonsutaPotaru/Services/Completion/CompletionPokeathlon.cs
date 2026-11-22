using PKHeX.Core;

namespace MonsutaPotaru.Services;

/// <summary>
/// PokéAthlon d'HeartGold / SoulSilver (choix de l'utilisateur, 2026-11-16) : dans les quêtes annexes, chaque parcours gagné,
/// chaque trophée et chaque objet exposé des quatre salles du musée du Dôme, et les 27 Cartes Mémo. Les ouvertures des
/// salles ne comptent pas en double (Confiance : les 5 parcours ; Potentiel : un Pokémon aux 5 médailles ; Amitié : les
/// 10 records du Potentiel), ni les parcours en connexion (une deuxième console) ni la Coupe des Cracks (aucune trace de
/// victoire). Tout se lit dans le bloc PokéAthlon de la sauvegarde (<see cref="Pokeathlon4"/> de PKHeX) ; le jeu ne garde
/// pas les trophées, ils sont recalculés d'après ses règles (seuils de Pokébip ; ceux de l'Amitié, de PKHeX).
/// Conditions « PA:… » : « M:n » parcours n gagné (une espèce a sa médaille), « C:n:v » meilleur score du parcours n arrivé
/// à v, « T:n » n espèces ont les 5 médailles, « G:compteur:v » compteur arrivé à v (« EventFirst » : dans chaque épreuve),
/// « R:n:v » record de l'épreuve n arrivé à v (Cours'Obstacles : temps en dessous), « F:n » n trophées de l'Amitié,
/// « D:n » Carte Mémo n reçue.
/// </summary>
internal static class CompletionPokeathlon
{
    private const string Dome = "Dôme Pokéathlon";

    /// <summary>Parcours, dans l'ordre de <see cref="PokeathlonStat4"/> (bits des médailles).</summary>
    private static readonly string[] Courses = ["Vitesse", "Force", "Habileté", "Endurance", "Saut"];

    /// <summary>
    /// Épreuves, dans l'ordre de <see cref="PokeathlonEvent4"/>, avec le record d'un trophée du Potentiel (Pokébip), en
    /// unités de la sauvegarde : Cours'Obstacles en images (30 par seconde : 80,0 s = 2 400 ; une première course de
    /// heartgold.sav, 3 018, fait 100,6 s, juste au-dessus du record de l'ordinateur, 100,0 s), Course de Relais en
    /// millièmes de tour (16,0 tours ; 14 222 dans heartgold.sav), les autres en points ou en nombre (drapeaux, rocs…).
    /// </summary>
    private static readonly (string Name, int Record, string Text)[] Events =
    [
        ("Cours'Obstacles", 2400, "en 80 s ou moins"),
        ("Capture Drapeau", 50, "50 drapeaux"),
        ("Cercle Musical", 60, "60 points"),
        ("Casse Roc", 130, "130 rocs"),
        ("Attrap'Disques", 70, "70 points"),
        ("Saute-Lumière", 500, "500 points"),
        ("Course de Relais", 16000, "16 tours"),
        ("Combat Ring-Out", 100, "100 points"),
        ("Bataille Neige", 55, "55 boules"),
        ("Balle au But", 9, "9 points"),
    ];

    /// <summary>Nombre d'espèces aux 5 médailles pour chaque trophée de la Confiance.</summary>
    private static readonly int[] TrustTrophies = [1, 2, 5, 10, 25, 50, 75, 100, 150, 200];

    /// <summary>Record vide : l'épreuve n'a pas encore de score.</summary>
    private const ushort NoRecord = ushort.MaxValue;

    /// <summary>Les 77 étapes, à la fin des quêtes annexes d'HG / SS.</summary>
    public static IEnumerable<CompletionRow> Rows()
    {
        for (var i = 0; i < Courses.Length; i++)
            yield return Row("parcours", $"PA:M:{i}", $"Gagner le parcours {Courses[i]}");

        const string solidarity = "Salle de la Coopération";
        for (var i = 0; i < Courses.Length; i++)
        {
            yield return Row(solidarity, $"PA:C:{i}:420", $"Trophée : parcours {Courses[i]} à 420 points");
            yield return Row(solidarity, $"PA:C:{i}:450", $"Trophée : parcours {Courses[i]} à 450 points");
        }

        const string trust = "Salle de la Confiance";
        foreach (var count in TrustTrophies)
            yield return Row(trust, $"PA:T:{count}", count == 1
                ? "Trophée : un Pokémon aux 5 médailles"
                : $"Trophée : {count} Pokémon aux 5 médailles");
        yield return Row(trust, "PA:G:Dashed:5000", "Chaussures : 5 000 sprints");
        yield return Row(trust, "PA:G:SessionsJoined:50", "Jogging : 50 participations");
        yield return Row(trust, "PA:G:Switched:200", "Drapeau : 200 changements de Pokémon");

        const string potential = "Salle du Potentiel";
        for (var i = 0; i < Events.Length; i++)
            yield return Row(potential, $"PA:R:{i}:{Events[i].Record}", $"Trophée : {Events[i].Name} {Events[i].Text}");
        yield return Row(potential, "PA:G:PlacedFirst:100", "Pokématos : 100 victoires");
        yield return Row(potential, "PA:G:EventFirst:50", "Poké Ball : 50 premières places dans chaque épreuve");

        for (var n = 1; n <= Pokeathlon4.FriendshipTrophyThresholds.Length; n++)
        {
            var points = Pokeathlon4.FriendshipTrophyThresholds[n - 1]; // de 3 000 à 4 500
            yield return Row("Salle de l'Amitié", $"PA:F:{n}", $"Trophée : {points / 1000} {points % 1000:000} points au total");
        }

        for (var n = 0; n < (int)DataCard4.Count; n++)
            yield return Row("Cartes Mémo", $"PA:D:{n}", $"Obtenir la Carte Mémo {n + 1:00}");
    }

    private static CompletionRow Row(string detail, string done, string label) =>
        new(CompletionSection.Quests, Dome, detail, done, Label: label);

    /// <summary>Condition « PA:… » (<paramref name="parts"/> : le découpage de l'atome par « : », « PA » compris).</summary>
    public static bool Test(SaveFile sav, string[] parts)
    {
        if (sav is not SAV4HGSS hgss)
            throw new InvalidOperationException("Le PokéAthlon n'existe que dans HeartGold et SoulSilver.");
        var athlon = hgss.Pokeathlon;
        var n = parts[1] == "G" ? 0 : int.Parse(parts[2]); // « G » : un nom de compteur
        return parts[1] switch
        {
            "M" => Species().Any(s => (athlon.Medals.GetMedal(s) >> n & 1) != 0),
            "C" => athlon.GetCourseRecord((PokeathlonStat4)n).ScoreMax >= int.Parse(parts[3]),
            "T" => Species().Count(s => (athlon.Medals.GetMedal(s) & PokeathlonMedalManager4.MaxMedalBits)
                == PokeathlonMedalManager4.MaxMedalBits) >= n,
            "G" => Counter(athlon.GlobalCounters, parts[2]) >= uint.Parse(parts[3]),
            "R" => Record(athlon, (PokeathlonEvent4)n, int.Parse(parts[3])),
            "F" => Pokeathlon4.CalculateFriendshipTrophyCount(athlon.CalculateGlobalScore()) >= n,
            "D" => (athlon.FlagsDataCard >> n & 1) != 0,
            _ => throw new InvalidOperationException($"Condition du PokéAthlon inconnue : {string.Join(':', parts)}"),
        };
    }

    /// <summary>Les 493 espèces (les médailles sont rangées par espèce, Bulbizarre en premier).</summary>
    private static IEnumerable<ushort> Species() => Enumerable.Range(1, PokeathlonMedalManager4.SIZE).Select(s => (ushort)s);

    private static uint Counter(PokeathlonGlobalCounters4 counters, string name) => name switch
    {
        "Dashed" => counters.Dashed,
        "SessionsJoined" => counters.SessionsJoined,
        "Switched" => counters.Switched,
        "PlacedFirst" => counters.PlacedFirst,
        // L'objet demande 50 premières places dans chacune des 10 épreuves : la plus faible compte
        "EventFirst" => Enum.GetValues<PokeathlonEvent4>().Where(e => e < PokeathlonEvent4.Count).Min(e => counters[e]),
        _ => throw new InvalidOperationException($"Compteur du PokéAthlon inconnu : {name}"),
    };

    /// <summary>
    /// Meilleur des 5 records de l'épreuve arrivé à <paramref name="record"/> (Cours'Obstacles : un temps, en dessous) ;
    /// une épreuve jamais jouée n'a pas de record (65 535, ou zéro dans une sauvegarde vierge de PKHeX).
    /// </summary>
    private static bool Record(Pokeathlon4 athlon, PokeathlonEvent4 e, int record)
    {
        var data = athlon.GetEventSelf(e);
        if (data.Attempts == 0)
            return false;
        var records = Enumerable.Range(0, (int)PokeathlonEventData4.MaxRecord).Select(i => data.GetRecord(i).Record)
            .Where(r => r != NoRecord).ToList();
        if (records.Count == 0)
            return false;
        return e == PokeathlonEvent4.HurdleDash ? records.Min() <= record : records.Max() >= record;
    }
}
