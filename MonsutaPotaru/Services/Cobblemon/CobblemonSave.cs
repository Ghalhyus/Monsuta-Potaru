using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MonsutaPotaru.Services;

/// <summary>
/// Les fichiers d'un monde Minecraft solo avec Cobblemon (modpack All the Mons) que le site lit : l'équipe
/// (<c>pokemon/playerpartystore/xx/&lt;uuid&gt;.dat</c>) et le PC (<c>pokemon/pcstore/xx/&lt;uuid&gt;.dat</c>) du joueur, en
/// NBT compressé (réglage <c>storageFormat: nbt</c> de Cobblemon, celui par défaut), <c>level.dat</c> (nom et graine du
/// monde : son identité) et <c>stats/&lt;uuid&gt;.json</c> (temps de jeu). Ils arrivent dans un zip, ou choisis dans le
/// dossier du monde par le navigateur (<see cref="ZipFromFolder"/>, qui n'envoie que ceux-là).
/// <para>
/// Seuls ces fichiers sont gardés (<see cref="Bundle"/>, ce que stocke « Mes sauvegardes ») ; le joueur ne reçoit que le
/// dossier <c>pokemon</c> (<see cref="Write"/>) : <c>level.dat</c> et <c>stats</c> ne sont jamais réécrits.
/// </para>
/// <para>« Pages » (comme les boîtes d'une sauvegarde de jeu) : 0 pour l'équipe, puis une par boîte du PC.</para>
/// </summary>
public sealed partial class CobblemonSave
{
    /// <summary>Nom affiché du jeu (badge de « Dépôt / Retrait », « Mes sauvegardes », jeu d'origine dans « Ma collection »).</summary>
    public const string GameName = "All the Mons";

    /// <summary>Début de l'identité d'un monde stocké (<see cref="Data.StoredSave.IdentityKey"/>) : le distingue des jeux.</summary>
    public const string IdentityPrefix = "cobblemon:";

    /// <summary>Cases d'une boîte du PC de Cobblemon (fixe dans le mod).</summary>
    public const int BoxSize = 30;

    /// <summary>Limites d'un zip envoyé (zip piégé qui se décompresse en gigaoctets) et des fichiers gardés.</summary>
    private const int MaxEntries = 20_000;
    private const long MaxUncompressed = 64L * 1024 * 1024;
    private const int MaxKeptFileBytes = 4 * 1024 * 1024;

    /// <summary>Fichiers venus du dossier choisi dans le navigateur : au plus ce nombre (un monde solo en a 4 ou 5).</summary>
    public const int MaxFolderFiles = 32;

    public const string NoLevelMessage =
        "Il manque le fichier level.dat du monde : choisis le dossier de ton monde (dans « saves »), ou mets dans le zip les dossiers « pokemon » et « stats » avec le fichier level.dat.";

    public const string NoStatsMessage =
        "Il manque le temps de jeu du monde (dossier « stats ») : choisis le dossier de ton monde, ou ajoute le dossier « stats » au zip.";

    private readonly Dictionary<string, byte[]> _files; // fichiers gardés, chemins depuis le dossier du monde
    private readonly string _partyPath, _pcPath;
    private readonly NbtCompound _party, _pc;
    private readonly CobblemonPokemon?[] _partySlots;
    private readonly CobblemonPokemon?[][] _boxes;

    /// <summary>UUID du joueur Minecraft (nom des fichiers de l'équipe, du PC et des statistiques).</summary>
    public string PlayerId { get; }

    /// <summary>Le zip avait le fichier level.dat du monde (exigé partout, sauf pour lire un vieux zip dans les tests).</summary>
    public bool HasLevel => WorldName is not null;

    /// <summary>Nom du monde dans Minecraft (« Kantalia »), lu dans level.dat ; null sans level.dat.</summary>
    public string? WorldName { get; }

    /// <summary>Graine du monde, lue dans level.dat.</summary>
    public long Seed { get; }

    /// <summary>Temps de jeu du joueur dans ce monde (stats), en secondes ; null sans le fichier.</summary>
    public int? PlayTimeSeconds { get; }

    /// <summary>
    /// Identité du monde : « cobblemon:&lt;UUID du joueur&gt;:&lt;graine&gt; ». Deux mondes du même joueur restent distincts ; un
    /// même monde est reconnu d'un envoi à l'autre. Null sans level.dat.
    /// </summary>
    public string? IdentityKey => HasLevel ? $"{IdentityPrefix}{PlayerId}:{Seed}" : null;

    public int BoxCount => _boxes.Length;
    public int PageCount => 1 + _boxes.Length;
    public int SlotCount(int page) => page == 0 ? _partySlots.Length : BoxSize;

    /// <summary>« Équipe », le nom donné à la boîte dans le jeu, ou « Boîte 3 ».</summary>
    public string PageName(int page) =>
        page == 0 ? "Équipe"
        : _pc.Compound($"Box{page - 1}")?.String("BoxName") is { Length: > 0 } name ? name
        : $"Boîte {page}";

    public CobblemonPokemon? Get(int page, int slot) =>
        page == 0 ? _partySlots[slot] : _boxes[page - 1][slot];

    public void Set(int page, int slot, CobblemonPokemon? pokemon)
    {
        if (page == 0) _partySlots[slot] = pokemon;
        else _boxes[page - 1][slot] = pokemon;
    }

    /// <summary>Tous les Pokémon de l'équipe et du PC (contrôle des doublons au chargement).</summary>
    public IEnumerable<CobblemonPokemon> All =>
        _partySlots.Concat(_boxes.SelectMany(b => b)).OfType<CobblemonPokemon>();

    /// <summary>Les Pokémon avec leur place (« Équipe 1 », « Boîte 3, case 12 »), dans l'ordre : équipe, puis boîtes.</summary>
    public IEnumerable<(CobblemonPokemon Pokemon, string Location)> AllWithLocation()
    {
        for (int i = 0; i < _partySlots.Length; i++)
            if (_partySlots[i] is { } mon)
                yield return (mon, $"Équipe {i + 1}");
        for (int b = 0; b < _boxes.Length; b++)
            for (int s = 0; s < BoxSize; s++)
                if (_boxes[b][s] is { } mon)
                    yield return (mon, $"{PageName(b + 1)}, case {s + 1}");
    }

    private CobblemonSave(Dictionary<string, byte[]> files, string playerId, string partyPath, NbtCompound party, string pcPath, NbtCompound pc,
        string? worldName, long seed, int? playTime)
    {
        _files = files;
        PlayerId = playerId;
        _partyPath = partyPath;
        _pcPath = pcPath;
        _party = party;
        _pc = pc;
        WorldName = worldName;
        Seed = seed;
        PlayTimeSeconds = playTime;

        var slotCount = (int)Math.Clamp(party.Integer("SlotCount") ?? 6, 1, 64);
        _partySlots = Enumerable.Range(0, slotCount).Select(i => Read(party, $"Slot{i}")).ToArray();

        var boxCount = (int)Math.Clamp(pc.Integer("BoxCount") ?? 0, 0, 10_000);
        _boxes = Enumerable.Range(0, boxCount)
            .Select(b => Enumerable.Range(0, BoxSize).Select(s => pc.Compound($"Box{b}") is { } box ? Read(box, $"Slot{s}") : null).ToArray())
            .ToArray();
    }

    private static CobblemonPokemon? Read(NbtCompound parent, string key) =>
        parent.Compound(key) is { } nbt && nbt.String("Species") is { Length: > 0 } ? new CobblemonPokemon(nbt) : null;

    /// <summary>Le fichier ressemble-t-il à un zip (« PK\3\4 ») ? Les pages l'aiguillent alors ici, PKHeX ne le lit jamais.</summary>
    public static bool IsZip(ReadOnlySpan<byte> data) =>
        data.Length >= 4 && data[0] == 'P' && data[1] == 'K' && data[2] == 3 && data[3] == 4;

    private const string Uuid = "[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}";

    /// <summary>Fichier de l'équipe ou du PC, sous le dossier du monde (<c>root</c>, éventuellement vide).</summary>
    [GeneratedRegex($@"^(?<root>(?:[^/]+/)*?)pokemon/(?<kind>pcstore|playerpartystore)/[0-9a-f]{{2}}/(?<player>{Uuid})\.(?<ext>dat|json)(?<old>\.old)?$",
        RegexOptions.IgnoreCase)]
    private static partial Regex StoreFile();

    /// <summary>Chemins acceptés depuis le dossier du monde (dossier choisi dans le navigateur) : rien d'autre ne passe.</summary>
    [GeneratedRegex($@"^(level\.dat|pokemon/(pcstore|playerpartystore)/[0-9a-f]{{2}}/{Uuid}\.dat(\.old)?|stats/{Uuid}\.json)$",
        RegexOptions.IgnoreCase)]
    private static partial Regex FolderFile();

    /// <summary>Lit le zip ; null avec un message prêt à afficher s'il ne convient pas.</summary>
    public static CobblemonSave? TryRead(byte[] zip, out string error)
    {
        List<(string Name, byte[]? Data)> entries;
        try
        {
            entries = Unzip(zip);
        }
        catch (Exception e) when (e is InvalidDataException or IOException)
        {
            error = $"Zip illisible : {e.Message}";
            return null;
        }
        return TryRead(entries, out error);
    }

    private static CobblemonSave? TryRead(List<(string Name, byte[]? Data)> entries, out string error)
    {
        error = "";
        var stores = entries
            .Where(e => e.Data is not null)
            .Select(e => (e.Name, Match: StoreFile().Match(e.Name)))
            .Where(e => e.Match.Success)
            .Select(e => (e.Name, Root: e.Match.Groups["root"].Value, Kind: e.Match.Groups["kind"].Value.ToLowerInvariant(),
                Player: e.Match.Groups["player"].Value.ToLowerInvariant(),
                Json: e.Match.Groups["ext"].Value.Equals("json", StringComparison.OrdinalIgnoreCase), Old: e.Match.Groups["old"].Success))
            .ToList();
        if (stores.Count == 0)
        {
            error = "Ce zip ne contient pas le dossier « pokemon » d'un monde Cobblemon (avec ses dossiers « pcstore » et « playerpartystore »).";
            return null;
        }
        if (stores.Select(s => s.Root).Distinct().Count() > 1)
        {
            error = "Ce zip contient plusieurs mondes : envoie-les un par un.";
            return null;
        }
        if (stores.All(s => s.Json))
        {
            error = "Ce monde enregistre ses Pokémon en JSON : seul le format NBT (le réglage par défaut de Cobblemon, celui d'All the Mons) est pris en charge.";
            return null;
        }
        var players = stores.Where(s => !s.Json).Select(s => s.Player).Distinct().ToList();
        if (players.Count > 1)
        {
            error = "Ce monde a plusieurs joueurs : seuls les mondes solo sont pris en charge.";
            return null;
        }

        var root = stores[0].Root;
        var player = players[0];
        var party = stores.FirstOrDefault(s => !s.Json && !s.Old && s.Kind == "playerpartystore" && s.Player == player).Name;
        var pc = stores.FirstOrDefault(s => !s.Json && !s.Old && s.Kind == "pcstore" && s.Player == player).Name;
        if (party is null || pc is null)
        {
            error = $"Il manque {(party is null ? "l'équipe (dossier « playerpartystore »)" : "le PC (dossier « pcstore »)")} du joueur : "
                + "envoie tout le dossier « pokemon » de ton monde.";
            return null;
        }

        // Seuls les fichiers utiles sont gardés, avec leur chemin depuis le dossier du monde.
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var (name, data) in entries)
        {
            if (data is null || !name.StartsWith(root, StringComparison.Ordinal))
                continue;
            var relative = name[root.Length..];
            var keep = stores.Any(s => s.Name == name && !s.Json && s.Player == player)
                || relative == "level.dat"
                || relative.Equals($"stats/{player}.json", StringComparison.OrdinalIgnoreCase);
            if (!keep)
                continue;
            if (data.Length > MaxKeptFileBytes)
            {
                error = $"Fichier trop gros dans le monde ({relative}).";
                return null;
            }
            files[relative] = data;
        }

        try
        {
            var partyNbt = Nbt.Read(files[party[root.Length..]]);
            var pcNbt = Nbt.Read(files[pc[root.Length..]]);
            if (!pcNbt.Contains("BoxCount"))
            {
                error = "Le PC de ce monde est illisible (nombre de boîtes absent).";
                return null;
            }

            string? worldName = null;
            long seed = 0;
            if (files.TryGetValue("level.dat", out var level))
            {
                var data = Nbt.Read(level).Compound("Data");
                worldName = data?.String("LevelName");
                var worldSeed = data?.Compound("WorldGenSettings")?.Integer("seed") ?? data?.Integer("RandomSeed");
                if (worldName is null || worldSeed is null)
                {
                    error = "Le fichier level.dat est illisible (nom ou graine du monde absents).";
                    return null;
                }
                seed = worldSeed.Value;
            }

            int? playTime = null;
            var statsKey = files.Keys.FirstOrDefault(k => k.StartsWith("stats/", StringComparison.Ordinal));
            if (statsKey is not null)
            {
                playTime = PlayTimeOf(files[statsKey]);
                if (playTime is null)
                {
                    error = "Le fichier des statistiques du joueur est illisible (temps de jeu absent).";
                    return null;
                }
            }

            return new CobblemonSave(files, player, party[root.Length..], partyNbt, pc[root.Length..], pcNbt, worldName, seed, playTime);
        }
        catch (Exception e) when (e is InvalidDataException or IOException)
        {
            error = $"Fichier de Cobblemon illisible : {e.Message}";
            return null;
        }
    }

    /// <summary>Temps de jeu (secondes) lu dans les statistiques du joueur : <c>minecraft:play_time</c>, en ticks (20 par seconde).</summary>
    private static int? PlayTimeOf(byte[] json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("stats", out var stats) && stats.ValueKind == JsonValueKind.Object
                && stats.TryGetProperty("minecraft:custom", out var custom) && custom.ValueKind == JsonValueKind.Object
                && custom.TryGetProperty("minecraft:play_time", out var ticks) && ticks.TryGetInt64(out var value) && value >= 0)
                return (int)Math.Min(value / 20, int.MaxValue);
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Remet l'équipe et le PC dans leurs ensembles NBT, puis les compresse.</summary>
    private (byte[] Party, byte[] Pc) WriteStores()
    {
        for (int i = 0; i < _partySlots.Length; i++)
            _party[$"Slot{i}"] = _partySlots[i]?.Nbt;
        for (int b = 0; b < _boxes.Length; b++)
        {
            if (_pc.Compound($"Box{b}") is not { } box)
            {
                if (_boxes[b].All(p => p is null))
                    continue;
                _pc[$"Box{b}"] = box = new NbtCompound();
            }
            for (int s = 0; s < BoxSize; s++)
                box[$"Slot{s}"] = _boxes[b][s]?.Nbt;
        }
        return (Nbt.Write(_party, gzip: true), Nbt.Write(_pc, gzip: true));
    }

    /// <summary>
    /// Le zip rendu au joueur, à décompresser dans le dossier de son monde : seulement le dossier <c>pokemon</c>, l'équipe
    /// et le PC réécrits avec leurs copies <c>.dat.old</c> (sinon Cobblemon pourrait relire une ancienne version, où un
    /// Pokémon déposé serait encore là). Jamais level.dat ni stats : ils restent ceux du jeu.
    /// </summary>
    public byte[] Write()
    {
        var (party, pc) = WriteStores();
        return ToZip(_files.Where(f => f.Key.StartsWith("pokemon/", StringComparison.Ordinal)), party, pc);
    }

    /// <summary>Tous les fichiers gardés (pokemon, level.dat, stats), équipe et PC à jour : ce que stocke « Mes sauvegardes ».</summary>
    public byte[] Bundle()
    {
        var (party, pc) = WriteStores();
        return ToZip(_files, party, pc);
    }

    private byte[] ToZip(IEnumerable<KeyValuePair<string, byte[]>> files, byte[] party, byte[] pc)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, data) in files.OrderBy(f => f.Key, StringComparer.Ordinal))
            {
                var content = name == _partyPath || name == _partyPath + ".old" ? party
                    : name == _pcPath || name == _pcPath + ".old" ? pc
                    : data;
                using var stream = archive.CreateEntry(name, CompressionLevel.Optimal).Open();
                stream.Write(content);
            }
        }
        return output.ToArray();
    }

    // ---------- Dossier choisi dans le navigateur ----------

    /// <summary>
    /// Les fichiers envoyés par le navigateur depuis le dossier du monde (<c>wwwroot/js/app.js</c>, <c>worldFolder</c>),
    /// remis en zip. Format : « MPW1 », longueur de l'en-tête (4 octets, petit-boutiste), en-tête JSON
    /// <c>[{"n": chemin, "s": taille}]</c>, puis les contenus à la suite. Le navigateur ne doit envoyer que level.dat, le
    /// dossier pokemon et les statistiques ; tout est revérifié ici (une page trafiquée peut envoyer n'importe quoi) :
    /// chemins exacts, nombre et tailles, longueur totale. Null avec un message sinon.
    /// </summary>
    public static byte[]? ZipFromFolder(byte[] payload, out string error)
    {
        error = "Envoi du dossier illisible : recommence.";
        if (payload.Length < 8 || !payload.AsSpan(0, 4).SequenceEqual("MPW1"u8))
            return null;
        var headerLength = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(4, 4));
        if (headerLength <= 0 || headerLength > 64 * 1024 || 8 + headerLength > payload.Length)
            return null;

        List<FolderEntry>? header;
        try
        {
            header = JsonSerializer.Deserialize<List<FolderEntry>>(payload.AsSpan(8, headerLength), new JsonSerializerOptions { MaxDepth = 4 });
        }
        catch (JsonException)
        {
            return null;
        }
        if (header is null || header.Count == 0 || header.Count > MaxFolderFiles)
            return null;

        var files = new List<(string Name, byte[] Data)>();
        var offset = 8 + headerLength;
        foreach (var entry in header)
        {
            if (entry.N is null || !FolderFile().IsMatch(entry.N) || entry.S < 0 || entry.S > MaxKeptFileBytes
                || entry.S > payload.Length - offset || files.Exists(f => f.Name == entry.N))
                return null;
            files.Add((entry.N, payload.AsSpan(offset, entry.S).ToArray()));
            offset += entry.S;
        }
        if (offset != payload.Length)
            return null;

        error = "";
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var (name, data) in files)
            {
                using var stream = archive.CreateEntry(name, CompressionLevel.Fastest).Open();
                stream.Write(data);
            }
        return output.ToArray();
    }

    private sealed record FolderEntry(
        [property: System.Text.Json.Serialization.JsonPropertyName("n")] string? N,
        [property: System.Text.Json.Serialization.JsonPropertyName("s")] int S);

    /// <summary>
    /// Nom de fichier d'un monde envoyé par dossier (« Kantalia.zip ») : le nom du dossier, venu du navigateur, réduit
    /// aux lettres, chiffres, espaces, tirets et points.
    /// </summary>
    public static string FolderFileName(string? folder)
    {
        var name = new StringBuilder();
        foreach (var c in (folder ?? "").Normalize(NormalizationForm.FormC))
            if (char.IsLetterOrDigit(c) || c is ' ' or '-' or '_' or '.')
                name.Append(c);
        var clean = name.ToString().Trim(' ', '.');
        if (clean.Length > 60)
            clean = clean[..60].Trim(' ', '.');
        return (clean.Length == 0 ? "monde" : clean) + ".zip";
    }

    /// <summary>Entrées du zip dans leur ordre (null pour un dossier), avec les limites contre les zips piégés.</summary>
    private static List<(string, byte[]?)> Unzip(byte[] zip)
    {
        using var archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        if (archive.Entries.Count > MaxEntries)
            throw new InvalidDataException("trop de fichiers.");
        var entries = new List<(string, byte[]?)>();
        long total = 0;
        foreach (var entry in archive.Entries)
        {
            var name = entry.FullName.Replace('\\', '/');
            if (name.EndsWith('/'))
            {
                entries.Add((name, null));
                continue;
            }
            total += entry.Length;
            if (total > MaxUncompressed)
                throw new InvalidDataException("contenu trop volumineux (choisis plutôt le dossier du monde).");
            using var stream = entry.Open();
            using var copy = new MemoryStream((int)Math.Min(entry.Length, MaxUncompressed));
            CopyAtMost(stream, copy, entry.Length);
            entries.Add((name, copy.ToArray()));
        }
        return entries;
    }

    /// <summary>Copie au plus la taille annoncée par le zip : un zip qui ment sur sa taille ne fait pas déborder la mémoire.</summary>
    private static void CopyAtMost(Stream source, Stream target, long limit)
    {
        var buffer = new byte[81920];
        long copied = 0;
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            copied += read;
            if (copied > limit)
                throw new InvalidDataException("fichier plus gros qu'annoncé.");
            target.Write(buffer, 0, read);
        }
    }
}
