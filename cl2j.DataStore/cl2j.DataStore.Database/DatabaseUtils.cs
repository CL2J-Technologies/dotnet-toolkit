namespace cl2j.DataStore.Database
{
    public static class DatabaseUtils
    {
        private static readonly Random random = new Random();

        public static string CreateGuid()
        {
            return Guid.NewGuid().ToString();
        }

        public static string CreateShortGuid()
        {
            return random.Next(int.MaxValue).ToString("x"); ;
        }
    }
}
