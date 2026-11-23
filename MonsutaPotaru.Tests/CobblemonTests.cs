using System.IO.Compression;
using Microsoft.Extensions.Logging.Abstractions;
using MonsutaPotaru.Data;
using MonsutaPotaru.Services;
using PKHeX.Core;

namespace MonsutaPotaru.Tests;

/// <summary>
/// Minecraft (Cobblemon, modpack All the Mons) : format NBT, lecture du zip du dossier « pokemon » d'un vrai monde
/// (<c>Sauvegardes/all-the-mons.zip</c> : un Feunnec dans l'équipe, 100 boîtes vides, level.dat et stats du monde
/// Kantalia), dépôts et retraits, validation, et mondes séparés (ni GTS, ni rubans, ni parc, ni living dex global, ni
/// retrait dans un jeu). Mondes stockés : <see cref="StoredWorldTests"/>.
/// </summary>
public sealed class CobblemonTests : IDisposable
{
    private readonly TestDatabase _db = new("sacha");

    public void Dispose() => _db.Dispose();

    /// <summary>Le zip du vrai monde (Kantalia).</summary>
    internal static byte[] RealZip() => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Sauvegardes", "all-the-mons.zip"));

    internal const string Player = "650fa532-fb2b-46d3-b055-b68008f9a895";

    internal static CobblemonSave RealWorld() => CobblemonSave.TryRead(RealZip(), out var error) ?? throw new InvalidOperationException(error);

    /// <summary>
    /// Pokémon de test : le Feunnec du vrai monde, avec une autre espèce et un UUID propre (<paramref name="n"/>), modifié
    /// par <paramref name="edit"/>.
    /// </summary>
    internal static CobblemonPokemon Mon(string species, int n, Action<NbtCompound>? edit = null)
    {
        var mon = Fennekin.Value.Clone();
        mon.Nbt["Species"] = Text($"cobblemon:{species}");
        mon.Nbt["UUID"] = new NbtArray(NbtType.IntArray, new[] { 0x11111111, 0x22222222, 0x33333333, n });
        edit?.Invoke(mon.Nbt);
        return mon;
    }

    /// <summary>Le Feunnec du vrai monde, lu une fois (relire tout le monde à chaque Pokémon de test serait lent).</summary>
    private static readonly Lazy<CobblemonPokemon> Fennekin = new(() => RealWorld().Get(0, 0)!);

    internal static NbtValue Text(string s) => new(NbtType.String, s);

    /// <summary>Identifiant de pension (« TetheringId ») d'un Pokémon de test.</summary>
    private static int[] TetheringId() => [1, 2, 3, 4];

    /// <summary>
    /// Le vrai monde (pokemon, level.dat, stats), avec ces Pokémon en plus dans la première boîte du PC (cases 0, 1...).
    /// </summary>
    internal static byte[] World(params CobblemonPokemon[] pc)
    {
        var world = RealWorld();
        for (int i = 0; i < pc.Length; i++)
            world.Set(1, i, pc[i]);
        return world.Bundle();
    }

    /// <summary>
    /// Le vrai monde modifié : graine (autre monde), temps de jeu en secondes, sans level.dat ou sans stats.
    /// </summary>
    internal static byte[] WorldVariant(long? seed = null, int? playTimeSeconds = null, bool withoutLevel = false, bool withoutStats = false,
        params CobblemonPokemon[] pc)
    {
        var files = Unzip(World(pc));
        if (seed is { } newSeed)
        {
            var level = Nbt.Read(files["level.dat"]);
            level.Compound("Data")!.Compound("WorldGenSettings")!["seed"] = new NbtValue(NbtType.Long, newSeed);
            files["level.dat"] = Nbt.Write(level, gzip: true);
        }
        if (playTimeSeconds is { } seconds)
            files[$"stats/{Player}.json"] = System.Text.Encoding.UTF8.GetBytes(
                $"{{\"stats\":{{\"minecraft:custom\":{{\"minecraft:play_time\":{seconds * 20}}}}},\"DataVersion\":3955}}");
        if (withoutLevel)
            files.Remove("level.dat");
        if (withoutStats)
            files.Remove($"stats/{Player}.json");
        return Zip([.. files.Select(f => (f.Key, f.Value))]);
    }

    /// <summary>
    /// Ce que fait le joueur : il décompresse le zip rendu (dossier pokemon seul) dans son monde, puis renvoie le monde
    /// (level.dat et stats du vrai monde).
    /// </summary>
    internal static byte[] InWorld(byte[] returned)
    {
        var files = Unzip(RealZip());
        foreach (var key in files.Keys.Where(k => k.StartsWith("pokemon/")).ToList())
            files.Remove(key);
        foreach (var (name, data) in Unzip(returned))
            files[name] = data;
        return Zip([.. files.Select(f => (f.Key, f.Value))]);
    }

    internal static Dictionary<string, byte[]> Unzip(byte[] zip)
    {
        using var archive = new ZipArchive(new MemoryStream(zip));
        return archive.Entries.Where(e => !e.FullName.EndsWith('/')).ToDictionary(e => e.FullName, e =>
        {
            using var s = e.Open();
            using var m = new MemoryStream();
            s.CopyTo(m);
            return m.ToArray();
        });
    }

    private static byte[] Gunzip(byte[] data)
    {
        using var gzip = new GZipStream(new MemoryStream(data), CompressionMode.Decompress);
        using var plain = new MemoryStream();
        gzip.CopyTo(plain);
        return plain.ToArray();
    }

    internal static byte[] Zip(params (string Name, byte[] Data)[] files)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var (name, data) in files)
            {
                using var s = archive.CreateEntry(name).Open();
                s.Write(data);
            }
        return output.ToArray();
    }

    // ---------- NBT ----------

    [Fact]
    public void Nbt_RoundTrip_IsByteExact()
    {
        foreach (var (name, data) in Unzip(RealZip()).Where(f => f.Key.Contains(".dat"))) // pas les statistiques, succès et quêtes (JSON, SNBT)
        {
            var plain = Gunzip(data);
            var root = Nbt.Read(data);
            Assert.Equal(plain, Nbt.Write(root, gzip: false));
            Assert.Equal(plain, Gunzip(Nbt.Write(root, gzip: true)));
            Assert.Equal(plain, Nbt.Write(Nbt.Read(plain), gzip: false)); // aussi sans compression
            Assert.True(name.EndsWith(".dat") || name.EndsWith(".dat.old"), name);
        }
    }

    [Fact]
    public void Nbt_AllTypes_RoundTrip()
    {
        var root = new NbtCompound
        {
            ["b"] = new NbtValue(NbtType.Byte, (sbyte)-3),
            ["s"] = new NbtValue(NbtType.Short, (short)-300),
            ["l"] = new NbtValue(NbtType.Long, long.MinValue),
            ["f"] = new NbtValue(NbtType.Float, 1.5f),
            ["d"] = new NbtValue(NbtType.Double, -2.25),
            ["ba"] = new NbtArray(NbtType.ByteArray, new sbyte[] { -1, 0, 1 }),
            ["la"] = new NbtArray(NbtType.LongArray, new long[] { 1, -1 }),
            ["vide"] = new NbtList(NbtType.End, []),
            ["texte"] = Text("é\0😀"),
        };
        var copy = Nbt.Read(Nbt.Write(root, gzip: true));
        Assert.Equal(Nbt.Write(root, gzip: false), Nbt.Write(copy, gzip: false));
        Assert.Equal(-3, copy.Integer("b"));
        Assert.Equal(-300, copy.Integer("s"));
        Assert.Equal(long.MinValue, copy.Integer("l"));
        Assert.Null(copy.Integer("f"));
        Assert.Equal("é\0😀", copy.String("texte"));
        Assert.Equal(new sbyte[] { -1, 0, 1 }, (sbyte[])((NbtArray)copy["ba"]!).Values);

        copy["s"] = null; // retrait
        Assert.False(copy.Contains("s"));
        Assert.Equal(["b", "l", "f", "d", "ba", "la", "vide", "texte"], copy.Keys);
    }

    [Fact]
    public void ModifiedUtf8_LikeJava()
    {
        Assert.Equal(new byte[] { 0xC0, 0x80 }, ModifiedUtf8.Encode("\0")); // nul sur deux octets
        Assert.Equal(6, ModifiedUtf8.Encode("😀").Length); // deux demi-codets de trois octets
        Assert.Equal("a\0é😀", ModifiedUtf8.Decode(ModifiedUtf8.Encode("a\0é😀")));
        Assert.Throws<InvalidDataException>(() => ModifiedUtf8.Decode([0xFF]));
    }

    [Fact]
    public void Nbt_RejectsBrokenFiles()
    {
        var data = Nbt.Write(new NbtCompound { ["n"] = Text("abc") }, gzip: false);
        Assert.Throws<InvalidDataException>(() => Nbt.Read(data[..^3])); // tronqué
        Assert.Throws<InvalidDataException>(() => Nbt.Read([8, 0, 0])); // pas un ensemble
        Assert.Throws<InvalidDataException>(() => Nbt.Read([10, 0, 0, 13, 0, 1, (byte)'x'])); // type inconnu
        Assert.Throws<InvalidDataException>(() => Nbt.Read([10, 0, 0, 7, 0, 1, (byte)'x', 0x7F, 0, 0, 0])); // longueur folle
    }

    // ---------- Lecture du monde ----------

    [Fact]
    public void RealWorld_IsRead()
    {
        var world = RealWorld();
        Assert.Equal(Player, world.PlayerId);
        Assert.Equal(100, world.BoxCount);
        Assert.Equal(101, world.PageCount);
        Assert.Equal(6, world.SlotCount(0));
        Assert.Equal(30, world.SlotCount(1));
        Assert.Equal("Équipe", world.PageName(0));
        Assert.Equal("Boîte 1", world.PageName(1));
        Assert.Single(world.All);

        var fennekin = world.Get(0, 0)!;
        Assert.Equal((ushort)Species.Fennekin, fennekin.Species);
        Assert.Equal(0, fennekin.Form);
        Assert.Equal(10, fennekin.Level);
        Assert.Equal(0, fennekin.Gender);
        Assert.False(fennekin.IsShiny);
        Assert.Null(fennekin.Nickname);
        Assert.Equal((byte)PKHeX.Core.Nature.Quirky, fennekin.Nature);
        Assert.Equal((int)PKHeX.Core.Ability.Blaze, fennekin.Ability);
        Assert.Equal([(ushort)Move.Ember, (ushort)Move.Scratch, (ushort)Move.TailWhip], fennekin.ReadMoves().Select(m => m.Move));
        Assert.Equal(new byte[] { 26, 24, 11, 21, 20, 23 }, fennekin.ReadIvs());
        Assert.Null(fennekin.HeldItemId);
        Assert.Equal("3c671c09-4aa5-4db8-b2be-6fd169dadfe4", fennekin.Uuid);
        Assert.Equal("cobblemon:3c671c09-4aa5-4db8-b2be-6fd169dadfe4", fennekin.Fingerprint);
        Assert.Equal(Player, fennekin.OriginalTrainerId);
        Assert.False(fennekin.IsTethered);
        Assert.Null(world.Get(0, 1));

        // level.dat et stats : identité et temps de jeu
        Assert.True(world.HasLevel);
        Assert.Equal("Kantalia", world.WorldName);
        Assert.Equal(9031796605789880640, world.Seed);
        Assert.Equal(29598 / 20, world.PlayTimeSeconds);
        Assert.Equal($"cobblemon:{Player}:9031796605789880640", world.IdentityKey);
        Assert.Equal([("Feunnec", "Équipe 1")], world.AllWithLocation().Select(p => (new PokemonInfo().Describe(p.Pokemon, "").SpeciesName, p.Location)));
        var placed = RealWorld();
        placed.Set(3, 11, Mon("eevee", 1));
        Assert.Equal("Boîte 3, case 12", placed.AllWithLocation().Last().Location);
    }

    [Fact]
    public void Pokemon_Fields_FromCobblemonIds()
    {
        var vulpix = Mon("vulpix", 1, n =>
        {
            n["FormId"] = Text("alola");
            n["Shiny"] = new NbtValue(NbtType.Byte, (sbyte)1);
            n["Gender"] = Text("FEMALE");
            n["Nickname"] = Text("Neige");
            n["MintedNature"] = Text("cobblemon:timid");
            n["HeldItem"] = new NbtCompound { ["id"] = Text("cobblemon:leftovers"), ["count"] = new NbtValue(NbtType.Int, 1) };
            n["TetheringId"] = new NbtArray(NbtType.IntArray, TetheringId());
        });
        Assert.Equal((ushort)Species.Vulpix, vulpix.Species);
        Assert.Equal(1, vulpix.Form); // Alola
        Assert.True(vulpix.IsShiny);
        Assert.Equal(1, vulpix.Gender);
        Assert.Equal("Neige", vulpix.Nickname);
        Assert.Equal((byte)PKHeX.Core.Nature.Timid, vulpix.Nature); // la menthe avant la nature
        Assert.Equal(234, vulpix.HeldItem); // Restes
        Assert.True(vulpix.IsTethered);
        Assert.Equal(FormKind.Alola, PokemonTraits.KindOf(vulpix.Species, vulpix.Form, CobblemonPokemon.FormContext));

        // Noms à la Showdown : symboles, accents, ponctuation
        Assert.Equal((ushort)Species.NidoranF, Mon("nidoranf", 2).Species);
        Assert.Equal((ushort)Species.Flabébé, Mon("flabebe", 3).Species);
        Assert.Equal((ushort)Species.MrMime, Mon("mrmime", 4).Species);
        Assert.Equal((ushort)Species.TypeNull, Mon("typenull", 5).Species);

        // Inconnus de PKHeX : objet de Minecraft, espèce d'un autre mod, surnom en composant de texte
        var other = Mon("pokemonmaison", 6, n =>
        {
            n["HeldItem"] = new NbtCompound { ["id"] = Text("minecraft:stick") };
            n["Nickname"] = new NbtCompound { ["text"] = Text("Bâton") };
            n["Gender"] = Text("GENDERLESS");
        });
        Assert.Equal(0, other.Species);
        Assert.Equal(0, other.Form);
        Assert.Equal("minecraft:stick", other.HeldItemId);
        Assert.Equal(0, other.HeldItem);
        Assert.Equal("Bâton", other.Nickname);
        Assert.Equal(2, other.Gender);
        Assert.Equal("Mimi", Mon("eevee", 7, n => n["Nickname"] = Text("{\"text\":\"Mimi\"}")).Nickname);
        Assert.Equal("Lulu", Mon("eevee", 8, n => n["Nickname"] = Text("\"Lulu\"")).Nickname);

        // Fiche en français
        var info = new PokemonInfo();
        var view = info.Describe(vulpix, "sacha");
        Assert.Equal("Goupix", view.SpeciesName);
        Assert.Equal("Neige", view.Nickname);
        Assert.Equal("Restes", view.HeldItem);
        Assert.Equal("Timide", view.Nature);
        Assert.Equal(["Flammèche", "Griffe", "Mimi-Queue"], view.Moves);
        Assert.Equal("sacha", view.OriginalTrainer);
        Assert.Equal("", view.TrainerId);
        Assert.Equal(CobblemonSave.GameName, view.OriginGame);
        Assert.False(view.HasLegality);
        Assert.Contains("chromatique/", view.SpriteUrl);
        var otherView = info.Describe(other, "sacha");
        Assert.Equal("pokemonmaison", otherView.SpeciesName);
        Assert.Equal("stick", otherView.HeldItem);
    }

    [Fact]
    public void Write_KeepsEveryOtherFile_AndRewritesTheOldCopy()
    {
        var original = Unzip(RealZip());
        var world = RealWorld();
        // Rendu au joueur : seulement le dossier pokemon ; gardé par le site : tout, level.dat et stats intacts.
        Assert.Equal(original.Keys.Where(k => k.StartsWith("pokemon/")).Order(), Unzip(world.Write()).Keys.Order());
        var bundle = Unzip(world.Bundle());
        Assert.Equal(original.Keys.Order(), bundle.Keys.Order());
        Assert.Equal(original["level.dat"], bundle["level.dat"]);
        Assert.Equal(original[$"stats/{Player}.json"], bundle[$"stats/{Player}.json"]);

        var fennekin = world.Get(0, 0)!;
        world.Set(0, 0, null);
        world.Set(1, 4, fennekin);
        var written = Unzip(world.Bundle());
        Assert.Equal(original.Keys.Order(), written.Keys.Order());
        var party = $"pokemon/playerpartystore/65/{Player}.dat";
        Assert.Equal(written[party], written[party + ".old"]); // la copie de secours ne fait pas revenir le Feunnec

        var reread = CobblemonSave.TryRead(Zip([.. written.Select(e => (e.Key, e.Value))]), out var error);
        Assert.NotNull(reread);
        Assert.Equal("", error);
        Assert.Null(reread.Get(0, 0));
        Assert.Equal(fennekin.Uuid, reread.Get(1, 4)!.Uuid);
        Assert.Equal(fennekin.ToBytes(), reread.Get(1, 4)!.ToBytes()); // intact
        // Le reste du PC n'a pas bougé (fonds d'écran, nombre de boîtes...)
        var pc = Nbt.Read(written[$"pokemon/pcstore/65/{Player}.dat"]);
        Assert.Equal(100, pc.Integer("BoxCount"));
        Assert.Equal("cobblemon:textures/gui/pc/pc_screen_overlay.png", pc.Compound("Box99")!.String("BoxWallpaper"));
    }

    [Fact]
    public void Write_CreatesAMissingBox_OnlyWhenNeeded()
    {
        var world = RealWorld();
        var files = Unzip(world.Write());
        var pcPath = $"pokemon/pcstore/65/{Player}.dat";
        var pc = Nbt.Read(files[pcPath]);
        pc["Box3"] = null;
        files[pcPath] = Nbt.Write(pc, gzip: true);
        var trimmed = CobblemonSave.TryRead(Zip([.. files.Select(e => (e.Key, e.Value))]), out _)!;
        Assert.False(Nbt.Read(Unzip(trimmed.Write())[pcPath]).Contains("Box3"));
        trimmed.Set(4, 0, Mon("eevee", 1));
        Assert.Equal("cobblemon:eevee", Nbt.Read(Unzip(trimmed.Write())[pcPath]).Compound("Box3")!.Compound("Slot0")!.String("Species"));
    }

    [Fact]
    public void TryRead_ExplainsWhatIsWrong()
    {
        var files = Unzip(RealZip());
        string Error(byte[] zip)
        {
            Assert.Null(CobblemonSave.TryRead(zip, out var error));
            return error;
        }

        Assert.Contains("ne contient pas le dossier « pokemon »", Error(Zip(("level.dat", [1, 2]))));
        Assert.Contains("JSON", Error(Zip(($"pokemon/pcstore/65/{Player}.json", "{}"u8.ToArray()))));
        var other = "750fa532-fb2b-46d3-b055-b68008f9a895";
        Assert.Contains("plusieurs joueurs", Error(Zip([.. files.Select(e => (e.Key, e.Value)),
            ($"pokemon/pcstore/75/{other}.dat", files[$"pokemon/pcstore/65/{Player}.dat"])])));
        Assert.Contains("Il manque le PC", Error(Zip([.. files.Where(e => !e.Key.Contains("pcstore")).Select(e => (e.Key, e.Value))])));
        Assert.Contains("Il manque l'équipe", Error(Zip([.. files.Where(e => e.Key.Contains("pcstore")).Select(e => (e.Key, e.Value))])));
        Assert.Contains("illisible", Error(Zip(($"pokemon/pcstore/65/{Player}.dat", [1, 2, 3]), ($"pokemon/playerpartystore/65/{Player}.dat", [1, 2, 3]))));
        Assert.Contains("nombre de boîtes", Error(Zip(($"pokemon/pcstore/65/{Player}.dat", Nbt.Write(new NbtCompound(), true)),
            ($"pokemon/playerpartystore/65/{Player}.dat", Nbt.Write(new NbtCompound(), true)))));
        Assert.Contains("Zip illisible", Error([(byte)'P', (byte)'K', 3, 4, 0, 0]));

        // Zip du dossier du monde lui-même (« Kantalia/... »), avec des « \ » de Windows : rendu sans ce dossier
        var rooted = Zip([.. files.Select(e => ("Kantalia\\" + e.Key.Replace('/', '\\'), e.Value)), ("Kantalia/region/r.0.0.mca", [1, 2, 3])]);
        var world = CobblemonSave.TryRead(rooted, out _)!;
        Assert.Equal("Kantalia", world.WorldName);
        Assert.All(Unzip(world.Write()).Keys, k => Assert.StartsWith("pokemon/", k));
        Assert.DoesNotContain(Unzip(world.Bundle()).Keys, k => k.Contains("region")); // seuls les fichiers utiles sont gardés

        // Le contenu du dossier « pokemon » seul (sans le dossier) n'est plus reconnu
        Assert.Contains("ne contient pas le dossier « pokemon »", Error(Zip([.. files.Where(e => e.Key.StartsWith("pokemon/"))
            .Select(e => (e.Key["pokemon/".Length..], e.Value))])));
        // Deux mondes dans le même zip
        Assert.Contains("plusieurs mondes", Error(Zip([.. files.Select(e => ("a/" + e.Key, e.Value)), .. files.Select(e => ("b/" + e.Key, e.Value))])));
        // level.dat ou stats illisibles
        Assert.Contains("level.dat est illisible", Error(Zip([.. files.Where(e => e.Key != "level.dat").Select(e => (e.Key, e.Value)),
            ("level.dat", Nbt.Write(new NbtCompound { ["Data"] = new NbtCompound() }, true))])));
        Assert.Contains("statistiques du joueur est illisible", Error(Zip([.. files.Where(e => !e.Key.StartsWith("stats/")).Select(e => (e.Key, e.Value)),
            ($"stats/{Player}.json", "{\"stats\":{}}"u8.ToArray())])));
        Assert.Contains("statistiques du joueur est illisible", Error(Zip([.. files.Where(e => !e.Key.StartsWith("stats/")).Select(e => (e.Key, e.Value)),
            ($"stats/{Player}.json", "pas du json"u8.ToArray())])));
        Assert.Contains("trop volumineux", Error(Zip(("pokemon/gros.bin", new byte[65 * 1024 * 1024]))));
    }

    // ---------- Dossier choisi dans le navigateur ----------

    /// <summary>Ce qu'envoie <c>watchWorldFolder</c> (app.js) : « MPW1 », longueur de l'en-tête, en-tête JSON, contenus.</summary>
    internal static byte[] FolderPayload(params (string Name, byte[] Data)[] files) =>
        FolderPayload(System.Text.Json.JsonSerializer.Serialize(files.Select(f => new { n = f.Name, s = f.Data.Length })), files.Select(f => f.Data));

    private static byte[] FolderPayload(string header, IEnumerable<byte[]> contents)
    {
        var headerBytes = System.Text.Encoding.UTF8.GetBytes(header);
        var payload = new List<byte>("MPW1"u8.ToArray());
        payload.AddRange(BitConverter.GetBytes(headerBytes.Length)); // petit-boutiste sur x86 / x64 / ARM
        payload.AddRange(headerBytes);
        foreach (var c in contents)
            payload.AddRange(c);
        return [.. payload];
    }

    /// <summary>Le vrai monde tel que l'enverrait le navigateur (chemins depuis le dossier du monde).</summary>
    internal static byte[] RealFolderPayload() =>
        FolderPayload([.. Unzip(RealZip()).Select(f => (f.Key, f.Value))]);

    [Fact]
    public void ZipFromFolder_RebuildsTheWorld()
    {
        var zip = CobblemonSave.ZipFromFolder(RealFolderPayload(), out var error);
        Assert.Equal("", error);
        var world = CobblemonSave.TryRead(zip!, out _)!;
        Assert.Equal("Kantalia", world.WorldName);
        Assert.Equal((ushort)Species.Fennekin, world.Get(0, 0)!.Species);
    }

    [Fact]
    public void ZipFromFolder_RefusesAnythingElse()
    {
        var level = Unzip(RealZip())["level.dat"];
        void Refused(byte[] payload) => Assert.Null(CobblemonSave.ZipFromFolder(payload, out _));

        Refused([]);
        Refused("XXXX\0\0\0\0"u8.ToArray()); // pas le bon format
        Refused(FolderPayload(("region/r.0.0.mca", [1]))); // un fichier que le site ne demande pas
        Refused(FolderPayload(("../level.dat", level)));
        Refused(FolderPayload(("Kantalia/level.dat", level))); // chemins depuis le dossier du monde seulement
        Refused(FolderPayload(($"stats/{Player}.json/../../../secret.txt", new byte[] { 1 })));
        Refused(FolderPayload(("level.dat", level), ("level.dat", level))); // deux fois le même
        Refused(FolderPayload(Enumerable.Range(0, CobblemonSave.MaxFolderFiles + 1)
            .Select(i => ($"pokemon/pcstore/65/{Player[..^2]}{i:x2}.dat", new byte[] { 1 })).ToArray())); // trop de fichiers
        Refused(FolderPayload("""[{"n":"level.dat","s":100}]""", [level])); // taille annoncée fausse
        Refused(FolderPayload("""[{"n":"level.dat","s":-1}]""", [level]));
        Refused(FolderPayload("""[{"n":"level.dat","s":1}]""", [[1, 2]])); // octets en trop à la fin
        Refused(FolderPayload("pas du json", [level]));
        Refused(FolderPayload("[]", []));
        Refused([.. "MPW1"u8.ToArray(), 0xFF, 0xFF, 0xFF, 0x7F]); // en-tête plus long que l'envoi
        Refused(FolderPayload(("level.dat", new byte[4 * 1024 * 1024 + 1])));
    }

    [Fact]
    public void FolderFileName_KeepsOnlySafeCharacters()
    {
        Assert.Equal("Kantalia.zip", CobblemonSave.FolderFileName("Kantalia"));
        Assert.Equal("Mon monde é 2.zip", CobblemonSave.FolderFileName("Mon monde é 2"));
        Assert.Equal("etcpasswd.zip", CobblemonSave.FolderFileName("../../etc/passwd"));
        Assert.Equal("monde.zip", CobblemonSave.FolderFileName("..."));
        Assert.Equal("monde.zip", CobblemonSave.FolderFileName(null));
        Assert.Equal(64, CobblemonSave.FolderFileName(new string('a', 200)).Length);
    }

    // ---------- « Dépôt / Retrait » ----------

    [Fact]
    public void Session_DepositsAndWithdraws_InMinecraft()
    {
        var session = new SaveSession();
        Assert.True(session.TryLoad(World(Mon("eevee", 1), Mon("pikachu", 2, n => n["TetheringId"] = new NbtArray(NbtType.IntArray, TetheringId()))),
            "pokemon.zip", out var error), error);
        Assert.NotNull(session.Minecraft);
        Assert.Null(session.Save);
        Assert.Equal(101, session.BoxCount);
        Assert.Equal(6, session.SlotCount(0));
        Assert.Equal("Équipe", session.BoxName(0));
        Assert.Equal(3, session.Fingerprints().Count());

        // Enclos : verrouillé
        Assert.True(session.IsLocked(1, 1));
        Assert.False(session.TryDeposit(1, 1, out error));
        Assert.Contains("enclos", error);
        Assert.False(session.TryDeposit(1, 5, out error));
        Assert.Equal("Emplacement vide.", error);

        Assert.True(session.TryDeposit(0, 0, out error), error);
        var deposit = Assert.Single(session.Deposits);
        Assert.Null(deposit.Pokemon);
        Assert.Equal((ushort)Species.Fennekin, deposit.Cobblemon!.Species);
        Assert.True(session.IsEmpty(0, 0));
        Assert.True(session.UndoDeposit(deposit));
        Assert.False(session.IsEmpty(0, 0));
        Assert.True(session.TryDeposit(0, 0, out _));

        // Retrait d'un Pokémon de Minecraft : dans le PC d'abord, l'équipe en dernier recours
        var entry = BankService.CreateEntry(new PokemonInfo(), Mon("bulbasaur", 9), "sacha", "sacha", DateTime.UtcNow);
        entry.Id = 42;
        Assert.Null(session.WithdrawalProblem(entry));
        Assert.True(session.TryWithdraw(entry, -1, -1, out error), error);
        Assert.Equal(new PendingWithdrawal(42, 1, 2), Assert.Single(session.Withdrawals));
        Assert.False(session.TryWithdraw(entry, -1, -1, out error));
        Assert.Equal("Déjà retiré.", error);
        session.UndoWithdrawal(session.Withdrawals[0]);
        Assert.True(session.TryWithdraw(entry, 0, 5, out error), error); // glissé dans l'équipe
        Assert.Equal((ushort)Species.Bulbasaur, session.GetCobblemon(0, 5)!.Species);
        Assert.Equal((1, 2), session.FindEmptySlot(0, 5));

        // Mondes séparés
        var game = BankService.CreateEntry(new PokemonInfo(), SaveSessionTests.CreatePk5(25, 5), "sacha", DateTime.UtcNow);
        Assert.Equal(SaveSession.GameToMinecraftRefusal, session.WithdrawalProblem(game));
        Assert.False(session.TryWithdraw(game, -1, -1, out error));
        Assert.Equal(SaveSession.GameToMinecraftRefusal, error);
        Assert.Equal(SaveSession.GameToMinecraftRefusal, session.WithdrawalProblem(SaveSessionTests.CreatePk5(25, 5)));

        var written = new SaveSession();
        Assert.True(written.TryLoad(InWorld(session.Write()), "pokemon.zip", out _));
        Assert.True(written.IsEmpty(0, 0));
        Assert.Equal((ushort)Species.Bulbasaur, written.GetCobblemon(0, 5)!.Species);

        // Tout annuler recharge le zip d'origine
        session.Reset();
        Assert.Empty(session.Deposits);
        Assert.Equal((ushort)Species.Fennekin, session.GetCobblemon(0, 0)!.Species);
        session.Close();
        Assert.False(session.IsLoaded);
        Assert.Equal(0, session.BoxCount);
        Assert.Equal((-1, -1), session.FindEmptySlot(0, 0));
    }

    [Fact]
    public void Session_NeedsLevelDat()
    {
        var session = new SaveSession();
        Assert.False(session.TryLoad(WorldVariant(withoutLevel: true), "pokemon.zip", out var error));
        Assert.Equal(CobblemonSave.NoLevelMessage, error);
        Assert.True(session.TryLoad(WorldVariant(withoutStats: true), "pokemon.zip", out error), error); // stats facultatif ici
        Assert.Equal($"cobblemon:{Player}:9031796605789880640", session.IdentityKey);
        Assert.Null(new SaveSession().IdentityKey);
    }

    [Fact]
    public void Session_RefusesMinecraftPokemon_InAGame()
    {
        var session = new SaveSession();
        Assert.True(session.TryLoad(SaveSessionTests.CreateBw2Save(), "noir2.sav", out _));
        var entry = BankService.CreateEntry(new PokemonInfo(), Mon("eevee", 1), "sacha", "sacha", DateTime.UtcNow);
        Assert.Equal(SaveSession.MinecraftToGameRefusal, session.WithdrawalProblem(entry));
        Assert.False(session.TryWithdraw(entry, -1, -1, out var error));
        Assert.Equal(SaveSession.MinecraftToGameRefusal, error);
        Assert.Null(new SaveSession().WithdrawalProblem(entry)); // sans sauvegarde : rien à griser
        Assert.Null(BankService.ToPkm(entry));
    }

    [Fact]
    public void Session_FullWorld_HasNoRoom()
    {
        var world = RealWorld();
        var full = new SaveSession();
        Assert.True(full.TryLoad(world.Bundle(), "pokemon.zip", out _));
        for (int b = 0; b < full.BoxCount; b++)
            for (int s = 0; s < full.SlotCount(b); s++)
                full.Minecraft!.Set(b, s, Mon("eevee", b * 100 + s));
        var entry = BankService.CreateEntry(new PokemonInfo(), Mon("bulbasaur", 99_999), "sacha", "sacha", DateTime.UtcNow);
        Assert.False(full.TryWithdraw(entry, -1, -1, out var error));
        Assert.Equal("Aucun emplacement libre dans les boîtes.", error);
        Assert.False(full.TryWithdraw(entry, 0, 0, out error));
        Assert.Equal("Cet emplacement est déjà occupé.", error);
    }

    // ---------- Validation et collection ----------

    [Fact]
    public async Task Commit_StoresTheNbt_AndRefusesAnOldCopy()
    {
        var bank = _db.Bank();
        var zip = World(Mon("eevee", 1, n => n["PokemonOriginalTrainer"] = Text("someone-else")),
            Mon("pikachu", 2, n => n["PokemonOriginalTrainerName"] = Text("Pierre")));
        var session = new SaveSession();
        Assert.True(session.TryLoad(zip, "pokemon.zip", out _));
        Assert.True(session.TryDeposit(0, 0, out _));
        Assert.True(session.TryDeposit(1, 0, out _));
        Assert.True(session.TryDeposit(1, 1, out _));
        var written = await bank.CommitAsync("sacha", session);
        Assert.Empty(session.Deposits);

        var entries = await bank.ListAsync("sacha");
        Assert.Equal(3, entries.Count);
        var fennekin = entries.Single(e => e.Species == (ushort)Species.Fennekin);
        Assert.True(BankService.IsCobblemon(fennekin));
        Assert.Equal("Feunnec", fennekin.SpeciesName);
        Assert.Equal("sacha", fennekin.OriginalTrainer); // le joueur du monde : le pseudo du compte
        Assert.Equal(CobblemonSave.GameName, fennekin.OriginGame);
        Assert.Equal((byte)PKHeX.Core.Nature.Quirky, fennekin.Nature);
        Assert.Equal(26, fennekin.IvHp);
        Assert.Equal((byte)MoveType.Fire, fennekin.Type1);
        Assert.Equal(RealWorld().Get(0, 0)!.ToBytes(), fennekin.Data); // le NBT intact
        Assert.Equal("Joueur Minecraft", entries.Single(e => e.Species == (ushort)Species.Eevee).OriginalTrainer);
        Assert.Equal("Pierre", entries.Single(e => e.Species == (ushort)Species.Pikachu).OriginalTrainer);
        var view = BankService.Describe(new PokemonInfo(), fennekin)!;
        Assert.Equal("Feunnec", view.SpeciesName);
        Assert.False(view.HasLegality);
        Assert.True((await bank.GetRangeAsync("sacha", null, 0, 10)).All(i => i.IsMinecraft));

        // Le zip d'avant (vieille copie) : ses Pokémon sont déjà au stockage interne
        var duplicates = await bank.FindDuplicatesAsync("sacha", new SaveSession().Also(s => s.TryLoad(zip, "pokemon.zip", out _)).Fingerprints());
        Assert.Equal(3, duplicates.Count);

        // Retour dans Minecraft
        var back = new SaveSession();
        Assert.True(back.TryLoad(InWorld(written), "pokemon.zip", out _));
        Assert.Empty(back.Minecraft!.All);
        Assert.True(back.TryWithdraw(fennekin, -1, -1, out var error), error);
        var final = await bank.CommitAsync("sacha", back);
        Assert.Equal(2, (await bank.ListAsync("sacha")).Count);
        var world = CobblemonSave.TryRead(final, out _)!;
        Assert.Equal(RealWorld().Get(0, 0)!.ToBytes(), world.Get(1, 0)!.ToBytes());

        // Copies de sécurité avant / après, au nom du zip
        var backups = Directory.GetFiles(Path.Combine(_db.BackupRoot, "sacha"));
        Assert.Contains(backups, f => f.EndsWith("_pokemon_avant.zip"));
        Assert.Contains(backups, f => f.EndsWith("_pokemon_apres.zip"));
    }

    [Fact]
    public async Task MinecraftPokemon_StayOutOfGameFeatures()
    {
        var bank = _db.Bank();
        var session = new SaveSession();
        Assert.True(session.TryLoad(RealZip(), "pokemon.zip", out _));
        Assert.True(session.TryDeposit(0, 0, out _));
        await bank.CommitAsync("sacha", session);
        var entry = Assert.Single(await bank.ListAsync("sacha"));
        var item = Assert.Single(await bank.GetRangeAsync("sacha", null, 0, 10));

        // GTS
        Assert.Equal(GtsService.MinecraftRefusal, GtsService.PickProblem(item));
        Assert.Null(await _db.Gts().DepositChoiceAsync("sacha", entry.Id));

        // Rubans
        var ribbons = new RibbonService(_db.Factory, NullLogger<RibbonService>.Instance);
        Assert.Contains("Minecraft", (await ribbons.UnavailableAsync("sacha"))[entry.Id]);
        var added = await ribbons.AddAsync("sacha", entry.Id);
        Assert.False(added.Ok);
        Assert.Contains("Minecraft", added.Message);

        // Parc
        var park = new ParkService(_db.Factory, NullLogger<ParkService>.Instance);
        var parkId = (await park.ListParksAsync("sacha")).Single().Id;
        Assert.Empty(await park.ListCandidatesAsync("sacha", parkId));

        // Living dex global : rien ne compte
        var dex = new LivingDexService(_db.Factory, NullLogger<LivingDexService>.Instance);
        var global = await dex.CreateGlobalAsync("sacha");
        Assert.True(global.Ok, global.Message);
        Assert.Equal(0, (await dex.GetAsync("sacha", global.Id!.Value))!.Owned);
    }
}

internal static class TestExtensions
{
    /// <summary>Appelle <paramref name="action"/> sur l'objet et le renvoie (préparer en une expression).</summary>
    public static T Also<T>(this T value, Action<T> action)
    {
        action(value);
        return value;
    }
}
