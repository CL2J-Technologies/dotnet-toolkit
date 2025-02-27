namespace cl2j.Database.Helpers
{
    public static class Types
    {
#pragma warning disable CA2211 // Non-constant fields should not be visible
        public static Type TypeBool = typeof(bool);

        public static Type TypeShort = typeof(short);
        public static Type TypeInt = typeof(int);
        public static Type TypeLong = typeof(long);

        public static Type TypeDecimal = typeof(decimal);
        public static Type TypeFloat = typeof(float);
        public static Type TypeDouble = typeof(double);

        public static Type TypeString = typeof(string);

        public static Type TypeDateTime = typeof(DateTime);
        public static Type TypeDateTimeOffset = typeof(DateTimeOffset);

        public static Type TypeGuid = typeof(Guid);

#pragma warning restore CA2211 // Non-constant fields should not be visible
    }
}
