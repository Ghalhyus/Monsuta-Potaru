namespace MonsutaPotaru.Services;

/// <summary>
/// Musiques de l'animation d'échange (<c>Shared/TradeAnimation.razor</c>) : pistes de la bande originale de
/// HeartGold / SoulSilver, fournies par l'utilisateur. Non redistribuables : hors du dépôt Git, dans
/// <c>wwwroot/images/sons/</c> (dossier des images, monté depuis le serveur), envoyées par <c>deploy\sons-serveur.cmd</c>.
/// Un fichier absent ne joue rien (pas de requête vers un fichier manquant) ; sans aucun, l'animation est muette et n'a
/// pas de bouton du son. Présence regardée à chaque animation : des fichiers envoyés plus tard servent sans redémarrage.
/// </summary>
public static class TradeSounds
{
    public const string Folder = "images/sons/";
    public const string TradeFile = "echange.mp3";
    public const string EvolutionFile = "evolution.mp3";
    public const string CongratulationsFile = "felicitations.mp3";

    private static string? _webRoot;

    /// <summary>Dossier du site où chercher les fichiers (au démarrage, comme <see cref="PokemonSprites.Load"/>).</summary>
    public static void Load(string? webRoot) => _webRoot = webRoot;

    /// <summary>Musique de l'échange (envoi, voyage, arrivée) ; null si le fichier manque.</summary>
    public static string? TradeUrl => Url(TradeFile);

    /// <summary>Musique de l'évolution (silhouettes qui alternent) ; null si le fichier manque.</summary>
    public static string? EvolutionUrl => Url(EvolutionFile);

    /// <summary>Fanfare de la nouvelle forme ; null si le fichier manque.</summary>
    public static string? CongratulationsUrl => Url(CongratulationsFile);

    private static string? Url(string file) =>
        _webRoot is not null && File.Exists(Path.Combine(_webRoot, "images", "sons", file)) ? Folder + file : null;
}
