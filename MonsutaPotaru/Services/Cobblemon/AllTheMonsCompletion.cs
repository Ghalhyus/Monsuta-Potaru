using System.Text.Json;

namespace MonsutaPotaru.Services;

/// <summary>Chapitre du livre de quêtes d'All the Mons : groupe (vide pour les premiers chapitres) et titre.</summary>
public sealed record AllTheMonsChapter(string Group, string Title)
{
    /// <summary>Titre d'un groupe d'étapes : « Cobblemon — Evolution », ou le chapitre seul s'il n'a pas de groupe.</summary>
    public string Zone => Group.Length == 0 ? Title : $"{Group} — {Title}";
}

/// <summary>Quête de FTB Quests : identifiant (16 chiffres hexadécimaux), chapitre, titre ; cachée tant qu'elle n'est pas faite.</summary>
public sealed record AllTheMonsQuest(string Id, int Chapter, string Title, bool Hidden = false);

/// <summary>Succès (advancement) : identifiant (« minecraft:story/mine_stone »), onglet, titre ; caché tant qu'il n'est pas obtenu.</summary>
public sealed record AllTheMonsAdvancement(string Id, int Tab, string Title, bool Hidden = false);

/// <summary>
/// Complétion All the Mons : toutes les quêtes du livre de quêtes (FTB Quests, optionnelles comprises) et tous les succès
/// affichés dans l'écran des progrès (cachés compris), relevés dans le modpack par <c>outils/completion_allthemons.py</c>
/// (<see cref="AllTheMonsCompletionData"/>). Deux sections, mais chaque quête et chaque succès compte autant dans le total
/// (<see cref="CompletionProgress.ByStep"/>, choix de l'utilisateur). Lu dans le monde stocké : la progression de l'équipe
/// FTB du joueur (<c>completed</c> : identifiants des quêtes faites) et ses succès (<c>done</c>). Une quête ou un succès
/// inconnu du monde (ajouté par une mise à jour du modpack) est ignoré ; un monde envoyé sans ces fichiers n'a rien de fait.
/// </summary>
public static class AllTheMonsCompletion
{
    /// <summary>Texte d'une quête ou d'un succès caché pas encore fait (le jeu ne le montre pas non plus).</summary>
    public const string HiddenQuest = "Quête cachée", HiddenAdvancement = "Succès caché";

    public static List<CompletionStep> Steps(CobblemonSave world)
    {
        var quests = DoneQuests(world.Quests);
        var advancements = DoneAdvancements(world.Advancements);
        var steps = new List<CompletionStep>(AllTheMonsCompletionData.Quests.Length + AllTheMonsCompletionData.Advancements.Length);
        foreach (var q in AllTheMonsCompletionData.Quests)
        {
            var done = quests.Contains(q.Id);
            steps.Add(new(CompletionSection.ModpackQuests, AllTheMonsCompletionData.Chapters[q.Chapter].Zone, null,
                q.Hidden && !done ? HiddenQuest : q.Title, done ? CompletionState.Done : CompletionState.Missing));
        }
        foreach (var a in AllTheMonsCompletionData.Advancements)
        {
            var done = advancements.Contains(a.Id);
            steps.Add(new(CompletionSection.Advancements, AllTheMonsCompletionData.Tabs[a.Tab], null,
                a.Hidden && !done ? HiddenAdvancement : a.Title, done ? CompletionState.Done : CompletionState.Missing));
        }
        return steps;
    }

    /// <summary>
    /// Identifiants faits dans la progression FTB de l'équipe (<c>completed</c> : quêtes, tâches et chapitres terminés,
    /// avec la date) ; aucun sans le fichier ou s'il est illisible.
    /// </summary>
    public static HashSet<string> DoneQuests(byte[]? data)
    {
        if (data is null || Snbt.TryParseCompound(data)?.GetValueOrDefault("completed") is not Dictionary<string, object?> completed)
            return [];
        return new(completed.Keys.Select(k => k.ToUpperInvariant()), StringComparer.Ordinal);
    }

    /// <summary>Succès obtenus (<c>"done": true</c>) dans le fichier des succès du joueur ; aucun sans le fichier ou s'il est illisible.</summary>
    public static HashSet<string> DoneAdvancements(byte[]? data)
    {
        var done = new HashSet<string>(StringComparer.Ordinal);
        if (data is null)
            return done;
        try
        {
            using var doc = JsonDocument.Parse(data, new JsonDocumentOptions { MaxDepth = 16 });
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return done;
            foreach (var p in doc.RootElement.EnumerateObject())
                if (p.Value.ValueKind == JsonValueKind.Object && p.Value.TryGetProperty("done", out var d) && d.ValueKind == JsonValueKind.True)
                    done.Add(p.Name);
        }
        catch (JsonException)
        {
            done.Clear();
        }
        return done;
    }
}
