using System.Globalization;
using System.Text;

namespace MonsutaPotaru.Services;

/// <summary>
/// Lecteur de SNBT (NBT écrit en texte), le format des fichiers de FTB Quests et FTB Teams dans un monde Minecraft
/// (<c>ftbquests/&lt;équipe&gt;.snbt</c>, <c>ftbteams/party/&lt;équipe&gt;.snbt</c>). Ensembles en
/// <see cref="Dictionary{TKey, TValue}"/>, listes en <see cref="List{T}"/>, nombres en <see cref="long"/> ou
/// <see cref="double"/> (suffixes L, b, s, d, f retirés), textes en <see cref="string"/>, true / false en <see cref="bool"/> ;
/// séparateurs : virgules ou retours à la ligne. Lecture seule ; profondeur limitée (fichier venu du joueur).
/// </summary>
public static class Snbt
{
    private const int MaxDepth = 64;

    /// <summary>La valeur lue ; null si le texte n'est pas du SNBT.</summary>
    public static object? TryParse(string text)
    {
        try
        {
            var reader = new Reader(text);
            var value = reader.Value(0);
            reader.SkipSpaces();
            return reader.AtEnd ? value : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>Le fichier (UTF-8) lu comme un ensemble ; null s'il ne l'est pas.</summary>
    public static Dictionary<string, object?>? TryParseCompound(byte[] data) =>
        TryParse(Encoding.UTF8.GetString(data)) as Dictionary<string, object?>;

    private sealed class Reader(string text)
    {
        private int _pos;

        public bool AtEnd => _pos >= text.Length;

        public void SkipSpaces()
        {
            while (_pos < text.Length && char.IsWhiteSpace(text[_pos]))
                _pos++;
        }

        private char Peek()
        {
            SkipSpaces();
            return _pos < text.Length ? text[_pos] : throw new FormatException("fin inattendue");
        }

        public object? Value(int depth)
        {
            if (depth > MaxDepth)
                throw new FormatException("trop imbriqué");
            return Peek() switch
            {
                '{' => Compound(depth),
                '[' => List(depth),
                '"' or '\'' => Quoted(),
                _ => Atom(Word()),
            };
        }

        private Dictionary<string, object?> Compound(int depth)
        {
            _pos++; // {
            var result = new Dictionary<string, object?>(StringComparer.Ordinal);
            while (true)
            {
                var c = Peek();
                if (c == '}')
                {
                    _pos++;
                    return result;
                }
                if (c == ',')
                {
                    _pos++;
                    continue;
                }
                var key = c is '"' or '\'' ? Quoted() : Word();
                if (Peek() != ':')
                    throw new FormatException("« : » attendu");
                _pos++;
                result[key] = Value(depth + 1);
            }
        }

        private List<object?> List(int depth)
        {
            _pos++; // [
            var result = new List<object?>();
            // Tableaux typés : [I; 1, 2], [L; ...], [B; ...]
            SkipSpaces();
            if (_pos + 1 < text.Length && text[_pos] is 'I' or 'L' or 'B' && text[_pos + 1] == ';')
                _pos += 2;
            while (true)
            {
                var c = Peek();
                if (c == ']')
                {
                    _pos++;
                    return result;
                }
                if (c == ',')
                {
                    _pos++;
                    continue;
                }
                result.Add(Value(depth + 1));
            }
        }

        private string Quoted()
        {
            var quote = text[_pos++];
            var result = new StringBuilder();
            while (true)
            {
                if (_pos >= text.Length)
                    throw new FormatException("texte non fermé");
                var c = text[_pos++];
                if (c == quote)
                    return result.ToString();
                if (c == '\\' && _pos < text.Length)
                {
                    var escaped = text[_pos++];
                    result.Append(escaped switch { 'n' => '\n', 't' => '\t', _ => escaped });
                }
                else
                    result.Append(c);
            }
        }

        private string Word()
        {
            SkipSpaces();
            var start = _pos;
            while (_pos < text.Length && !char.IsWhiteSpace(text[_pos]) && text[_pos] is not ('{' or '}' or '[' or ']' or ':' or ',' or ';' or '"'))
                _pos++;
            if (_pos == start)
                throw new FormatException("valeur attendue");
            return text[start.._pos];
        }

        private static object Atom(string word)
        {
            if (word == "true")
                return true;
            if (word == "false")
                return false;
            var number = word.TrimEnd('L', 'l', 'b', 'B', 's', 'S');
            if (number.Length > 0 && long.TryParse(number, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var integer))
                return integer;
            var real = word.TrimEnd('d', 'D', 'f', 'F');
            if (real.Length > 0 && double.TryParse(real, NumberStyles.Float, CultureInfo.InvariantCulture, out var floating))
                return floating;
            return word;
        }
    }
}
