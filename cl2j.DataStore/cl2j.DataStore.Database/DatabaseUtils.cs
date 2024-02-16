namespace cl2j.DataStore.Database
{
    public static class DatabaseUtils
    {
        public static string CreateGuid()
        {
            return Guid.NewGuid().ToString();
        }
    }
}
