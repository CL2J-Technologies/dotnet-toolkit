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

            return GetTypeNameWithNamespace(typeName, type.Namespace);
        }

        private static string GetTypeNameWithNamespace(string typeName, string? nameSpace)
        {
            if (nameSpace?.Length > 0)
                return $"{nameSpace}.{typeName}";
            return typeName;
        }

        private static string GetCSharpRepresentation(Type t, bool trimArgCount = true, bool addNamespace = false)
        {
            if (t.IsGenericType)
            {
                var genericArgs = t.GetGenericArguments().ToList();
                var representation = GetCSharpRepresentation(t, trimArgCount, genericArgs);

                //A generic argument used to fall through here without its namespace, because
                //addNamespace was only honoured on the non-generic branch below. Callers write
                //this straight into source they compile, so a bare name is only ever a compile
                //error inside generated code. See issue #31.
                if (!addNamespace)
                    return representation;

                //A generic nested in a plain type is written Outer.Inner<T>, like a plain nested type.
                if (t.DeclaringType is { IsGenericType: false })
                    return $"{GetCSharpRepresentation(t.DeclaringType, trimArgCount, true)}.{representation}";

                return GetTypeNameWithNamespace(representation, t.Namespace);
            }

            //A plain type nested in a plain one is written Outer.Inner. Without this the argument of a
            //generic, List<Inner>, lost its declaring type and the generated code did not compile.
            if (addNamespace && t.DeclaringType is { IsGenericType: false })
                return $"{GetCSharpRepresentation(t.DeclaringType, trimArgCount, true)}.{t.Name}";

            if (addNamespace)
                return GetTypeNameWithNamespace(t.Name, t.Namespace);

            return t.Name;
        }

        private static string GetCSharpRepresentation(Type t, bool trimArgCount, List<Type> availableArguments)
        {
            if (t.IsGenericType)
            {
                string value = t.Name;
                if (trimArgCount && value.IndexOf('`') > -1)
                    value = value[..value.IndexOf('`')];

                // A type nested in a generic one names the generic declaring type first, with its
                // arguments. A plain declaring type is left to the caller: it is already part of
                // ReflectedType.FullName, or is written in front by the generic branch above, and
                // repeating it here is what produced Outer.Outer+Inner<T>.
                if (t.DeclaringType is { IsGenericType: true })
                    value = GetCSharpRepresentation(t.DeclaringType, trimArgCount, availableArguments) + "+" + value;

                // Build the type arguments (if any)
                var argString = new StringBuilder();
                var thisTypeArgs = t.GetGenericArguments();
                for (int i = 0; i < thisTypeArgs.Length && availableArguments.Count > 0; i++)
                {
                    if (i != 0) argString.Append(", ");

                    argString.Append(GetCSharpRepresentation(availableArguments[0], trimArgCount, true));
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
