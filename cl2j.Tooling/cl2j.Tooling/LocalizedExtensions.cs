namespace cl2j.Tooling
{
    public class LocalizedExtensions
    {
        public static Localized<string> EmptyString()
        {
            return Localized<string>.CreateFrEn(string.Empty, string.Empty);
        }
    }
}
