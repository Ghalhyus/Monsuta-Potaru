using PKHeX.Core;

namespace MonsutaPotaru.Services;

/// <summary>
/// Identité d'une sauvegarde, tirée de son dresseur. <see cref="Key"/> sert d'empreinte unique sur le serveur
/// pour empêcher de stocker deux fois la même partie. Dissuasif seulement : PKHeX permet de modifier ces valeurs.
/// </summary>
public sealed record SaveIdentity(string Key, string TrainerName, string TrainerId, byte Generation, int PlayTimeSeconds)
{
    public static SaveIdentity Of(SaveFile sav)
    {
        var ot = sav.OT.Trim();
        // Génération + jeu (valeurs numériques stables de PKHeX) + ID dresseur et ID secret (ID32) + nom.
        // Les jeux de 1re et 2e génération n'ont pas d'ID secret : ID32 se limite alors à l'ID dresseur.
        // Rouge Feu / Vert Feuille : toujours Rouge Feu (réponse de PKHeX), même quand SaveVersion a reconnu Vert Feuille
        // d'après ses Pokémon : la clé ne doit pas changer au fil de la partie, ni pour les sauvegardes déjà stockées.
        var version = sav is SAV3FRLG ? GameVersion.FR : sav.Version;
        var key = $"G{sav.Generation}|V{(int)version}|{sav.ID32}|{ot}";
        var trainerId = sav.Generation <= 2 ? $"{sav.DisplayTID:00000}" : $"{sav.DisplayTID} / {sav.DisplaySID}";
        var playTime = sav.PlayedHours * 3600 + sav.PlayedMinutes * 60 + sav.PlayedSeconds;
        return new SaveIdentity(key, ot, trainerId, sav.Generation, playTime);
    }

    public static string FormatPlayTime(int seconds) => $"{seconds / 3600} h {seconds / 60 % 60:00}";
}
