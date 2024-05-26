using System.Text;

namespace cl2j.DataStore.Database
{
    public static class DatabaseUtils
    {
        private static readonly Random random = new();

        public static string CreateGuid()
        {
            return Guid.NewGuid().ToString();
        }

        public static string CreateShortGuid()
        {
            return random.Next(int.MaxValue).ToString("x"); ;
        }

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
