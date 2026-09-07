using System.Text;

namespace cl2j.DataStore.Database
{
    /// <summary>
    ///     Three helpers that ended up here for want of anywhere better. They are marked one by
    ///     one rather than as a class, because they do not deserve the same message: one has an
    ///     exact replacement in the framework, and the other two should not be replaced at all.
    /// </summary>
    public static class DatabaseUtils
    {
        private static readonly Random random = new();

        [Obsolete("Call Guid.NewGuid().ToString() directly — that is the entire body of this method, and it does not need a package.")]
        public static string CreateGuid()
        {
            return Guid.NewGuid().ToString();
        }

        /// <summary>
        ///     Not a GUID, and not safe to use as an identifier.
        /// </summary>
        [Obsolete("Do not use this for identifiers. It is not a GUID: it is 31 bits from a shared System.Random, so collisions arrive at around sixty-five thousand values by the birthday bound, and Random is not thread-safe — concurrent calls can corrupt its state and return zeros. Use Guid.NewGuid(), or a database identity column.")]
        public static string CreateShortGuid()
        {
            return random.Next(int.MaxValue).ToString("x"); ;
        }

        /// <summary>
        ///     Builds a quoted, comma-separated list by concatenation, with no escaping.
        /// </summary>
        [Obsolete("This concatenates values into SQL with no escaping, so a value containing an apostrophe ends the literal and everything after it is read as SQL. Pass the values as parameters instead — every provider supports a parameterised IN list, and cl2j.Database builds one for you in QueryKeys.")]
        public static string GenerateStringList(IEnumerable<string> values)
        {
            var sb = new StringBuilder();

            foreach (var value in values)
            {
                if (sb.Length > 0)
                    sb.Append(", ");
                sb.Append($"'{value}'");
            }

            return sb.ToString();
        }
    }
}
