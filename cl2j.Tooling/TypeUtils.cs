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

        public static string? GetTypeName<T>() => GetTypeName(typeof(T));

        public static string? GetTypeName(Type type)
        {
            var typeName = GetCSharpRepresentation(type);

            if (type.ReflectedType is not null)
                return $"{type.ReflectedType!.FullName}.{typeName}";
            return typeName;
        }

        private static string GetCSharpRepresentation(Type t, bool trimArgCount = true)
        {
            if (t.IsGenericType)
            {
                var genericArgs = t.GetGenericArguments().ToList();
                return GetCSharpRepresentation(t, trimArgCount, genericArgs);
            }

            return t.Name;
        }

        private static string GetCSharpRepresentation(Type t, bool trimArgCount, List<Type> availableArguments)
        {
            if (t.IsGenericType)
            {
                string value = t.Name;
                if (trimArgCount && value.IndexOf('`') > -1)
                    value = value[..value.IndexOf('`')];

                // This is a nested type, build the nesting type first
                if (t.DeclaringType != null)
                    value = GetCSharpRepresentation(t.DeclaringType, trimArgCount, availableArguments) + "+" + value;

                // Build the type arguments (if any)
                string argString = string.Empty;
                var thisTypeArgs = t.GetGenericArguments();
                for (int i = 0; i < thisTypeArgs.Length && availableArguments.Count > 0; i++)
                {
                    if (i != 0) argString += ", ";

                    argString += GetCSharpRepresentation(availableArguments[0], trimArgCount);
                    availableArguments.RemoveAt(0);
                }

                // If there are type arguments, add them with < >
                if (argString.Length > 0)
                    value += "<" + argString + ">";

                return value;
            }

            return t.Name;
        }
    }
}
