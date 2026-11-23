using System.IO.Compression;
using System.Text;

namespace MonsutaPotaru.Services;

/// <summary>Types des balises NBT de Minecraft (le numéro est celui écrit dans le fichier).</summary>
public enum NbtType : byte
{
    End = 0, Byte = 1, Short = 2, Int = 3, Long = 4, Float = 5, Double = 6, ByteArray = 7, String = 8, List = 9,
    Compound = 10, IntArray = 11, LongArray = 12,
}

/// <summary>Une valeur NBT : nombre, texte, tableau, liste ou ensemble nommé (<see cref="NbtCompound"/>).</summary>
public interface INbtTag
{
    NbtType Type { get; }

    /// <summary>Copie indépendante (un Pokémon déposé ne doit plus bouger avec la sauvegarde).</summary>
    INbtTag Clone();
}

/// <summary>Nombre ou texte : <see cref="Value"/> est un byte (signé), short, int, long, float, double ou string.</summary>
public sealed class NbtValue(NbtType type, object value) : INbtTag
{
    public NbtType Type => type;
    public object Value { get; } = value;
    public INbtTag Clone() => new NbtValue(type, Value);
}

/// <summary>Tableau d'octets, d'entiers ou d'entiers longs.</summary>
public sealed class NbtArray(NbtType type, Array values) : INbtTag
{
    public NbtType Type => type;
    public Array Values { get; } = values;
    public INbtTag Clone() => new NbtArray(type, (Array)Values.Clone());
}

/// <summary>Liste de valeurs du même type (<see cref="ElementType"/>, End pour une liste vide).</summary>
public sealed class NbtList(NbtType elementType, List<INbtTag> items) : INbtTag
{
    public NbtType Type => NbtType.List;
    public NbtType ElementType { get; } = elementType;
    public List<INbtTag> Items { get; } = items;
    public INbtTag Clone() => new NbtList(ElementType, Items.Select(i => i.Clone()).ToList());
}

/// <summary>Ensemble de valeurs nommées, dans l'ordre du fichier (gardé à la réécriture).</summary>
public sealed class NbtCompound : INbtTag
{
    private readonly List<KeyValuePair<string, INbtTag>> _entries = [];

    public NbtType Type => NbtType.Compound;
    public IEnumerable<KeyValuePair<string, INbtTag>> Entries => _entries;
    public IEnumerable<string> Keys => _entries.Select(e => e.Key);

    public INbtTag? this[string name]
    {
        get => _entries.FirstOrDefault(e => e.Key == name).Value;
        set
        {
            var index = _entries.FindIndex(e => e.Key == name);
            if (value is null)
            {
                if (index >= 0) _entries.RemoveAt(index);
            }
            else if (index >= 0) _entries[index] = new(name, value);
            else _entries.Add(new(name, value));
        }
    }

    public bool Contains(string name) => _entries.Exists(e => e.Key == name);

    public NbtCompound? Compound(string name) => this[name] as NbtCompound;
    public NbtList? List(string name) => this[name] as NbtList;
    public string? String(string name) => (this[name] as NbtValue)?.Value as string;

    /// <summary>Valeur entière, quel que soit son type de nombre (byte, short, int ou long) ; null si absente.</summary>
    public long? Integer(string name) => (this[name] as NbtValue)?.Value switch
    {
        sbyte b => b,
        short s => s,
        int i => i,
        long l => l,
        _ => null,
    };

    public bool Flag(string name) => Integer(name) is { } v && v != 0;

    public INbtTag Clone()
    {
        var copy = new NbtCompound();
        foreach (var (key, tag) in _entries)
            copy._entries.Add(new(key, tag.Clone()));
        return copy;
    }
}

/// <summary>
/// Lecture et écriture des fichiers NBT de Minecraft (gros-boutiste, textes en UTF-8 « modifié » de Java), compressés en
/// gzip ou non. Les fichiers de Cobblemon (<c>pokemon/pcstore</c>, <c>pokemon/playerpartystore</c>) sont compressés.
/// </summary>
public static class Nbt
{
    /// <summary>Profondeur maximale (comme Minecraft) : un fichier trafiqué ne fait pas déborder la pile.</summary>
    private const int MaxDepth = 512;

    public static bool IsGzip(ReadOnlySpan<byte> data) => data.Length >= 2 && data[0] == 0x1F && data[1] == 0x8B;

    /// <summary>Ensemble racine d'un fichier, compressé en gzip ou non ; une erreur si ce n'est pas du NBT.</summary>
    public static NbtCompound Read(byte[] data)
    {
        if (IsGzip(data))
        {
            using var gzip = new GZipStream(new MemoryStream(data), CompressionMode.Decompress);
            using var plain = new MemoryStream();
            gzip.CopyTo(plain);
            data = plain.ToArray();
        }
        var reader = new BinaryReaderBE(data);
        if ((NbtType)reader.Byte() != NbtType.Compound)
            throw new InvalidDataException("Le fichier ne commence pas par un ensemble NBT.");
        reader.String(); // nom de la racine (vide)
        var root = (NbtCompound)ReadPayload(reader, NbtType.Compound, 0);
        return root;
    }

    /// <summary>Écrit l'ensemble racine (nom vide), compressé en gzip si <paramref name="gzip"/>.</summary>
    public static byte[] Write(NbtCompound root, bool gzip)
    {
        using var plain = new MemoryStream();
        var writer = new BinaryWriterBE(plain);
        writer.Byte((byte)NbtType.Compound);
        writer.String("");
        WritePayload(writer, root);
        if (!gzip)
            return plain.ToArray();
        using var packed = new MemoryStream();
        using (var stream = new GZipStream(packed, CompressionLevel.Optimal, leaveOpen: true))
            stream.Write(plain.GetBuffer(), 0, (int)plain.Length);
        return packed.ToArray();
    }

    private static INbtTag ReadPayload(BinaryReaderBE r, NbtType type, int depth)
    {
        if (depth > MaxDepth)
            throw new InvalidDataException("Fichier NBT trop imbriqué.");
        return type switch
        {
            NbtType.Byte => new NbtValue(type, (sbyte)r.Byte()),
            NbtType.Short => new NbtValue(type, r.Short()),
            NbtType.Int => new NbtValue(type, r.Int()),
            NbtType.Long => new NbtValue(type, r.Long()),
            NbtType.Float => new NbtValue(type, BitConverter.Int32BitsToSingle(r.Int())),
            NbtType.Double => new NbtValue(type, BitConverter.Int64BitsToDouble(r.Long())),
            NbtType.String => new NbtValue(type, r.String()),
            NbtType.ByteArray => new NbtArray(type, System.Runtime.InteropServices.MemoryMarshal.Cast<byte, sbyte>(r.Bytes(r.Length())).ToArray()),
            NbtType.IntArray => new NbtArray(type, ReadArray(r.Length(4), r.Int)),
            NbtType.LongArray => new NbtArray(type, ReadArray(r.Length(8), r.Long)),
            NbtType.List => ReadList(r, depth),
            NbtType.Compound => ReadCompound(r, depth),
            _ => throw new InvalidDataException($"Type NBT inconnu ({(byte)type})."),
        };
    }

    /// <summary>count valeurs lues une à une, dans l'ordre.</summary>
    private static T[] ReadArray<T>(int count, Func<T> read)
    {
        var values = new T[count];
        for (int i = 0; i < values.Length; i++)
            values[i] = read();
        return values;
    }

    private static NbtList ReadList(BinaryReaderBE r, int depth)
    {
        var elementType = (NbtType)r.Byte();
        var count = r.Int();
        if (count < 0 || count > r.Remaining)
            throw new InvalidDataException("Liste NBT de longueur invalide.");
        var items = new List<INbtTag>(count);
        for (int i = 0; i < count; i++)
            items.Add(ReadPayload(r, elementType, depth + 1));
        return new NbtList(elementType, items);
    }

    private static NbtCompound ReadCompound(BinaryReaderBE r, int depth)
    {
        var compound = new NbtCompound();
        for (var childType = (NbtType)r.Byte(); childType != NbtType.End; childType = (NbtType)r.Byte())
        {
            var name = r.String();
            compound[name] = ReadPayload(r, childType, depth + 1);
        }
        return compound;
    }

    private static void WritePayload(BinaryWriterBE w, INbtTag tag)
    {
        switch (tag)
        {
            case NbtValue v:
                WriteValue(w, v.Value);
                break;
            case NbtArray a:
                WriteArray(w, a.Values);
                break;
            case NbtList list:
                w.Byte((byte)(list.Items.Count == 0 ? list.ElementType : list.Items[0].Type));
                w.Int(list.Items.Count);
                foreach (var item in list.Items)
                    WritePayload(w, item);
                break;
            case NbtCompound compound:
                foreach (var (name, child) in compound.Entries)
                {
                    w.Byte((byte)child.Type);
                    w.String(name);
                    WritePayload(w, child);
                }
                w.Byte((byte)NbtType.End);
                break;
        }
    }

    private static void WriteValue(BinaryWriterBE w, object value)
    {
        switch (value)
        {
            case sbyte b: w.Byte((byte)b); break;
            case short s: w.Short(s); break;
            case int i: w.Int(i); break;
            case long l: w.Long(l); break;
            case float f: w.Int(BitConverter.SingleToInt32Bits(f)); break;
            case double d: w.Long(BitConverter.DoubleToInt64Bits(d)); break;
            case string s: w.String(s); break;
            default: throw new InvalidOperationException($"Valeur NBT inattendue : {value.GetType().Name}.");
        }
    }

    private static void WriteArray(BinaryWriterBE w, Array values)
    {
        w.Int(values.Length);
        switch (values)
        {
            case sbyte[] bytes:
                foreach (var b in bytes)
                    w.Byte((byte)b);
                break;
            case int[] ints:
                foreach (var i in ints)
                    w.Int(i);
                break;
            case long[] longs:
                foreach (var l in longs)
                    w.Long(l);
                break;
        }
    }

    /// <summary>Lecture gros-boutiste, avec contrôle des longueurs (un fichier tronqué donne une erreur claire).</summary>
    private sealed class BinaryReaderBE(byte[] data)
    {
        private int _pos;

        public int Remaining => data.Length - _pos;

        public ReadOnlySpan<byte> Bytes(int count)
        {
            if (count < 0 || count > Remaining)
                throw new InvalidDataException("Fichier NBT tronqué.");
            var span = data.AsSpan(_pos, count);
            _pos += count;
            return span;
        }

        /// <summary>Longueur d'un tableau, vérifiée contre ce qui reste à lire.</summary>
        public int Length(int elementSize = 1)
        {
            var count = Int();
            if (count < 0 || (long)count * elementSize > Remaining)
                throw new InvalidDataException("Tableau NBT de longueur invalide.");
            return count;
        }

        public byte Byte() => Bytes(1)[0];
        public short Short() => System.Buffers.Binary.BinaryPrimitives.ReadInt16BigEndian(Bytes(2));
        public int Int() => System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(Bytes(4));
        public long Long() => System.Buffers.Binary.BinaryPrimitives.ReadInt64BigEndian(Bytes(8));

        public string String()
        {
            var length = (ushort)Short();
            return ModifiedUtf8.Decode(Bytes(length));
        }
    }

    private sealed class BinaryWriterBE(Stream stream)
    {
        public void Byte(byte b) => stream.WriteByte(b);

        public void Short(short v)
        {
            Span<byte> buffer = stackalloc byte[2];
            System.Buffers.Binary.BinaryPrimitives.WriteInt16BigEndian(buffer, v);
            stream.Write(buffer);
        }

        public void Int(int v)
        {
            Span<byte> buffer = stackalloc byte[4];
            System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(buffer, v);
            stream.Write(buffer);
        }

        public void Long(long v)
        {
            Span<byte> buffer = stackalloc byte[8];
            System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(buffer, v);
            stream.Write(buffer);
        }

        public void String(string s)
        {
            var bytes = ModifiedUtf8.Encode(s);
            if (bytes.Length > ushort.MaxValue)
                throw new InvalidOperationException("Texte NBT trop long.");
            Short((short)(ushort)bytes.Length);
            stream.Write(bytes);
        }
    }
}

/// <summary>
/// UTF-8 « modifié » de Java (DataOutput.writeUTF) : le caractère nul sur deux octets (C0 80), les caractères hors du plan
/// de base en deux demi-codets de trois octets chacun (et non sur quatre octets comme en UTF-8).
/// </summary>
public static class ModifiedUtf8
{
    public static byte[] Encode(string s)
    {
        var bytes = new List<byte>(s.Length);
        foreach (var c in s)
        {
            if (c != 0 && c < 0x80)
                bytes.Add((byte)c);
            else if (c < 0x800)
            {
                bytes.Add((byte)(0xC0 | (c >> 6)));
                bytes.Add((byte)(0x80 | (c & 0x3F)));
            }
            else
            {
                bytes.Add((byte)(0xE0 | (c >> 12)));
                bytes.Add((byte)(0x80 | ((c >> 6) & 0x3F)));
                bytes.Add((byte)(0x80 | (c & 0x3F)));
            }
        }
        return [.. bytes];
    }

    public static string Decode(ReadOnlySpan<byte> bytes)
    {
        var text = new StringBuilder(bytes.Length);
        for (int i = 0; i < bytes.Length;)
        {
            int b = bytes[i];
            if (b < 0x80)
            {
                text.Append((char)b);
                i++;
            }
            else if ((b & 0xE0) == 0xC0 && i + 1 < bytes.Length)
            {
                text.Append((char)(((b & 0x1F) << 6) | (bytes[i + 1] & 0x3F)));
                i += 2;
            }
            else if ((b & 0xF0) == 0xE0 && i + 2 < bytes.Length)
            {
                text.Append((char)(((b & 0x0F) << 12) | ((bytes[i + 1] & 0x3F) << 6) | (bytes[i + 2] & 0x3F)));
                i += 3;
            }
            else
                throw new InvalidDataException("Texte NBT mal encodé.");
        }
        return text.ToString();
    }
}
