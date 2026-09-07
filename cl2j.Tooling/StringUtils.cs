using System.Text;

namespace cl2j.Tooling
{
    public static class StringUtils
    {
        private static readonly Dictionary<string, string> foreign_characters = new()
        {
            { "äæǽ", "ae" },
            { "öœ", "oe" },
            { "ü", "ue" },
            { "Ä", "Ae" },
            { "Ü", "Ue" },
            { "Ö", "Oe" },
            { "ÀÁÂÃÄÅǺĀĂĄǍΑΆẢẠẦẪẨẬẰẮẴẲẶА", "A" },
            { "àáâãåǻāăąǎªαάảạầấẫẩậằắẵẳặа", "a" },
            { "Б", "B" },
            { "б", "b" },
            { "ÇĆĈĊČ", "C" },
            { "çćĉċč", "c" },
            { "Д", "D" },
            { "д", "d" },
            { "ÐĎĐΔ", "Dj" },
            { "ðďđδ", "dj" },
            { "ÈÉÊËĒĔĖĘĚΕΈẼẺẸỀẾỄỂỆЕЭ", "E" },
            { "èéêëēĕėęěέεẽẻẹềếễểệеэ", "e" },
            { "Ф", "F" },
            { "ф", "f" },
            { "ĜĞĠĢΓГҐ", "G" },
            { "ĝğġģγгґ", "g" },
            { "ĤĦ", "H" },
            { "ĥħ", "h" },
            { "ÌÍÎÏĨĪĬǏĮİΗΉΊΙΪỈỊИЫ", "I" },
            { "ìíîïĩīĭǐįıηήίιϊỉịиыї", "i" },
            { "Ĵ", "J" },
            { "ĵ", "j" },
            { "ĶΚК", "K" },
            { "ķκк", "k" },
            { "ĹĻĽĿŁΛЛ", "L" },
            { "ĺļľŀłλл", "l" },
            { "М", "M" },
            { "м", "m" },
            { "ÑŃŅŇΝН", "N" },
            { "ñńņňŉνн", "n" },
            { "ÒÓÔÕŌŎǑŐƠØǾΟΌΩΏỎỌỒỐỖỔỘỜỚỠỞỢО", "O" },
            { "òóôõōŏǒőơøǿºοόωώỏọồốỗổộờớỡởợо", "o" },
            { "П", "P" },
            { "п", "p" },
            { "ŔŖŘΡР", "R" },
            { "ŕŗřρр", "r" },
            { "ŚŜŞȘŠΣС", "S" },
            { "śŝşșšſσςс", "s" },
            { "ȚŢŤŦτТ", "T" },
            { "țţťŧт", "t" },
            { "ÙÚÛŨŪŬŮŰŲƯǓǕǗǙǛŨỦỤỪỨỮỬỰУ", "U" },
            { "ùúûũūŭůűųưǔǖǘǚǜυύϋủụừứữửựу", "u" },
            { "ÝŸŶΥΎΫỲỸỶỴЙ", "Y" },
            { "ýÿŷỳỹỷỵй", "y" },
            { "В", "V" },
            { "в", "v" },
            { "Ŵ", "W" },
            { "ŵ", "w" },
            { "ŹŻŽΖЗ", "Z" },
            { "źżžζз", "z" },
            { "ÆǼ", "AE" },
            { "ß", "ss" },
            { "Ĳ", "IJ" },
            { "ĳ", "ij" },
            { "Œ", "OE" },
            { "ƒ", "f" },
            { "ξ", "ks" },
            { "π", "p" },
            { "β", "v" },
            { "μ", "m" },
            { "ψ", "ps" },
            { "Ё", "Yo" },
            { "ё", "yo" },
            { "Є", "Ye" },
            { "є", "ye" },
            { "Ї", "Yi" },
            { "Ж", "Zh" },
            { "ж", "zh" },
            { "Х", "Kh" },
            { "х", "kh" },
            { "Ц", "Ts" },
            { "ц", "ts" },
            { "Ч", "Ch" },
            { "ч", "ch" },
            { "Ш", "Sh" },
            { "ш", "sh" },
            { "Щ", "Shch" },
            { "щ", "shch" },
            { "ЪъЬь", "" },
            { "Ю", "Yu" },
            { "ю", "yu" },
            { "Я", "Ya" },
            { "я", "ya" },
        };

        //The table above is grouped by replacement, because that is the shape a human can read and
        //maintain. A lookup wants it the other way round, so it is inverted once here rather than
        //scanned — eighty-nine string searches per character was the cost before.
        //
        //TryAdd, not the indexer: a few characters appear in two entries ("Ä" has its own entry and
        //is also in the "A" group), and the scan this replaces returned the first match.
        private static readonly Dictionary<char, string> Transliterations = BuildTransliterations();

        private static Dictionary<char, string> BuildTransliterations()
        {
            var map = new Dictionary<char, string>();
            foreach (var (characters, replacement) in foreign_characters)
            {
                foreach (var c in characters)
                    map.TryAdd(c, replacement);
            }
            return map;
        }

        public static string Crop(this string text, int maxLength)
        {
            ArgumentNullException.ThrowIfNull(text);

            if (text.Length > maxLength)
                return text[..maxLength];
            return text;
        }

        public static string NormalizeForUri(this string text)
        {
            ArgumentNullException.ThrowIfNull(text);

            var dashInserted = false;
            var sb = new StringBuilder(text.Length);
            foreach (var c in text)
            {
                if (char.IsLetter(c))
                {
                    //A letter can transliterate to nothing, and a letter that produced nothing must
                    //not clear the flag: otherwise "a ь b" would emit two dashes in a row.
                    var replacement = char.ToLower(c).RemoveDiacritics();
                    if (replacement.Length > 0)
                    {
                        sb.Append(replacement);
                        dashInserted = false;
                    }
                }
                else if (char.IsDigit(c))
                {
                    sb.Append(c);
                    dashInserted = false;
                }
                else if (c == '/')
                {
                    if (dashInserted)
                        sb.Remove(sb.Length - 1, 1);    //Remove the dash before the slash
                    else
                        dashInserted = true;
                    sb.Append(c);
                }
                else
                {
                    if (!dashInserted)
                    {
                        sb.Append('-');
                        dashInserted = true;
                    }
                }
            }

            var uri = sb.ToString();
            if (uri.EndsWith('/'))
                return uri[0..^1];

            return uri;
        }

        public static string NormalizeComponentForUri(this string text)
        {
            ArgumentNullException.ThrowIfNull(text);

            var dashInserted = false;
            var sb = new StringBuilder(text.Length);
            foreach (var c in text)
            {
                if (char.IsLetter(c))
                {
                    var replacement = char.ToLower(c).RemoveDiacritics();
                    if (replacement.Length > 0)
                    {
                        sb.Append(replacement);
                        dashInserted = false;
                    }
                }
                else if (char.IsDigit(c))
                {
                    sb.Append(c);
                    dashInserted = false;
                }
                else
                {
                    if (!dashInserted)
                    {
                        sb.Append('-');
                        dashInserted = true;
                    }
                }
            }

            var uri = sb.ToString();
            if (uri.EndsWith('/') || uri.EndsWith('-'))
                return uri[0..^1];

            return uri;
        }

        /// <summary>
        ///     The transliteration of a single character, which is not always a single character:
        ///     <c>æ</c> becomes <c>ae</c>, <c>щ</c> becomes <c>shch</c>, and the Cyrillic hard and
        ///     soft signs become nothing at all.
        ///
        ///     <para>
        ///     This used to return a <see cref="char"/>, so it gave back the first letter of the
        ///     replacement and dropped the rest — <c>Ærøskøbing</c> came out as <c>Aroskobing</c> —
        ///     and it threw <see cref="IndexOutOfRangeException"/> on the four characters that
        ///     transliterate to nothing. Returning a string is what makes both unrepresentable.
        ///     See issue #31.
        ///     </para>
        /// </summary>
        public static string RemoveDiacritics(this char c)
        {
            return Transliterations.TryGetValue(c, out var replacement) ? replacement : c.ToString();
        }

        public static string RemoveDiacritics(this string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (var c in s)
                sb.Append(c.RemoveDiacritics());
            return sb.ToString();
        }

        public static string ToInvariant(string? text)
        {
            ArgumentNullException.ThrowIfNull(text);

            return text.ToLowerInvariant().RemoveDiacritics();
        }

        public static HashSet<string> ComputeInvariantValues<T>(Func<T, string> valueResolver, IList<T> list)
        {
            var set = new HashSet<string>();
            foreach (var item in list)
            {
                var value = valueResolver(item);
                if (!string.IsNullOrEmpty(value))
                    set.Add(ToInvariant(value));
            }
            return set;
        }

        public static HashSet<string> ComputeNormalizedValues<T>(Func<T, string> valueResolver, IEnumerable<T> list)
        {
            var set = new HashSet<string>();
            foreach (var item in list)
            {
                var value = valueResolver(item);
                if (!string.IsNullOrEmpty(value))
                    set.Add(NormalizeComponentForUri(value));
            }
            return set;
        }

        public static string RemoveIfStartWith(this string content, string startWith)
        {
            if (content.StartsWith(startWith))
                return content[startWith.Length..];
            return content;
        }

        public static string GenerateNormalizedKey(params string[] keys)
        {
            var temp = new string[keys.Length];
            for (var i = 0; i < temp.Length; i++)
                temp[i] = NormalizeComponentForUri(keys[i]);

            return GenerateKeyFromArray(temp);
        }
        public static string GenerateKeyFromArray(params string[] keys) => string.Join(".", keys);

        public static string SimpleRemoveHtml(string html)
        {
            if (string.IsNullOrEmpty(html))
                return string.Empty;

            var result = new StringBuilder();

            bool isInsideTag = false;
            foreach (char currentChar in html)
            {
                if (currentChar == '<')
                    isInsideTag = true;
                else if (currentChar == '>')
                    isInsideTag = false;
                else if (!isInsideTag)
                    result.Append(currentChar);
            }

            return result.ToString();
        }
    }
}