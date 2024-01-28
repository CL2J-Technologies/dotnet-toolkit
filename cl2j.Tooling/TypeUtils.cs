using System.Text;

namespace cl2j.Tooling
{
    public static class TypeUtils
    {
        public static string GetPrettyName(this Type type)
        {
            var name = type.Name;
            if (!type.IsGenericType)
                return name;

            var sb = new StringBuilder();
            var index = name.IndexOf('`');
            if (index >= 0)
                sb.Append(name.AsSpan(0, index));
            else
                sb.Append(name);
            sb.Append('<');
            sb.Append(string.Join(", ", type.GetGenericArguments().Select(t => t.GetPrettyName())));
            sb.Append('>');

            return sb.ToString();
        }
    }
}
