namespace MonsutaPotaru.Services;

/// <summary>
/// Rubans de la complétion de 3e et 4e génération (choix de l'utilisateur, 2026-11-15) : ceux des concours, de la Tour de
/// Combat et le Ruban Effort, dans les quêtes annexes. Les rubans sont sur les Pokémon, pas dans la sauvegarde : l'étape
/// est faite si un Pokémon de la sauvegarde (équipe ou boîtes) a le ruban (« RB:clé », clés de <see cref="Ribbons"/>) ;
/// elle redevient à faire si ce Pokémon part. La même liste pour toutes les parties d'un jeu : écrite ici, pas dans les
/// tables générées.
/// </summary>
internal static class CompletionRibbons
{
    private const string Contest = "Salle de Concours";
    private const string Tower = "Tour de Combat";

    /// <summary>
    /// Rubis / Saphir : un concours par rang dans quatre villes ; Émeraude : tous à Nénucrique (les autres salles sont
    /// fermées). Tour de Combat : Gagnant au niveau 50, Victoire au niveau libre ; Ruban Effort au marché de Poivressel.
    /// </summary>
    public static IEnumerable<CompletionRow> Hoenn(bool emerald)
    {
        const string Nenucrique = "Nénucrique";
        string[] halls = emerald ? [Nenucrique, Nenucrique, Nenucrique, Nenucrique] : ["Vergazon", "Autéquia", "Poivressel", Nenucrique];
        var contests = Ribbons.ContestParts.Where(p => p.Key.StartsWith("G3", StringComparison.Ordinal)).ToList();
        foreach (var (part, i) in contests.Select((p, i) => (p, i)).OrderBy(x => x.i % 4))
            yield return Row(halls[i % 4], Contest, part.Key, part.Name);
        var (zone, detail) = emerald ? ("Zone de Combat", Tower) : (Tower, (string?)null);
        yield return Row(zone, detail, "Winning", "Gagnant (niveau 50)");
        yield return Row(zone, detail, "Victory", "Victoire (niveau libre)");
        yield return Row("Poivressel", "marché", "Effort", "Effort (510 EV)");
    }

    /// <summary>
    /// Diamant / Perle / Platine : les Super Concours d'Unionpolis aux rangs Normal, Méga et Ultra (le rang Master est
    /// déjà compté par son drapeau, « Gagner le concours … de rang Master ») ; HeartGold / SoulSilver n'ont pas de
    /// concours. Les six Aptitude de la Tour de Combat (Aptitude Monde : défi Wi-Fi) ; Ruban Effort au marché de Rivamar
    /// ou dans une maison d'Ébènelle.
    /// </summary>
    public static IEnumerable<CompletionRow> Sinnoh(bool platinum, bool heartGold)
    {
        if (!heartGold)
        {
            var contests = Ribbons.ContestParts.Where(p => p.Key.StartsWith("G4", StringComparison.Ordinal) && !p.Key.EndsWith("Master", StringComparison.Ordinal));
            foreach (var part in contests.Select((p, i) => (p, i)).OrderBy(x => x.i % 3).Select(x => x.p))
                yield return Row("Unionpolis", "Salle Concours", part.Key, part.Name);
        }
        var zone = platinum || heartGold ? "Zone de Combat" : "Aire de Combat";
        foreach (var part in Ribbons.BattleParts.Where(p => p.Key.StartsWith("Ability", StringComparison.Ordinal)))
            yield return Row(zone, Tower, part.Key, part.Name);
        yield return heartGold ? Row("Ébènelle", "maison", "Effort", "Effort (510 EV)") : Row("Rivamar", "marché", "Effort", "Effort (510 EV)");
    }

    private static CompletionRow Row(string zone, string? detail, string key, string name) =>
        new(CompletionSection.Quests, zone, detail, $"RB:{key}", Label: $"Obtenir le Ruban {name}");
}
