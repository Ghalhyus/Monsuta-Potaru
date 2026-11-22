using PKHeX.Core;

namespace MonsutaPotaru.Services;

/// <summary>
/// Précise le jeu d'une sauvegarde quand PKHeX ne peut pas le lire dans le fichier. Rouge Feu et Vert Feuille ont le même
/// format : PKHeX répond toujours « Rouge Feu ». Mais les Pokémon attrapés par le dresseur gardent leur jeu d'origine :
/// s'ils viennent en majorité de Vert Feuille, la sauvegarde est celle de Vert Feuille. Sans Pokémon du dresseur (début de
/// partie), la réponse de PKHeX reste. Rien n'est écrit dans le fichier (le jeu n'y est pas).
/// </summary>
public static class SaveVersion
{
    public static void Refine(SaveFile sav)
    {
        if (sav is not SAV3FRLG frlg || !sav.HasBox)
            return;
        int fireRed = 0, leafGreen = 0;
        foreach (var pk in sav.BoxData.Concat(sav.PartyData))
        {
            if (pk.Species == 0 || !IsTrainers(sav, pk))
                continue;
            if (pk.Version == GameVersion.FR)
                fireRed++;
            else if (pk.Version == GameVersion.LG)
                leafGreen++;
        }
        if (leafGreen > fireRed)
            Use(frlg, GameVersion.LG);
    }

    /// <summary>Pokémon du dresseur de la sauvegarde (même nom, mêmes numéros), pas un Pokémon reçu par échange.</summary>
    private static bool IsTrainers(SaveFile sav, PKM pk) =>
        pk.TID16 == sav.TID16 && pk.SID16 == sav.SID16 && pk.OriginalTrainerName == sav.OT;

    /// <summary>Change le jeu et ses statistiques (Deoxys : forme Défense dans Vert Feuille).</summary>
    private static void Use(SAV3FRLG sav, GameVersion version)
    {
        sav.ResetPersonal(version);
        sav.Version = version;
    }
}
