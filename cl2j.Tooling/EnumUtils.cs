namespace cl2j.Tooling
{
    public static class EnumUtils
    {
        public static string[] ToList<T>() where T : struct
        {
            return Enum.GetNames(typeof(T));
        }
    }
}
