using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace cl2j.FileStorage.Extensions
{
    /// <summary>
    ///     Resolves a <c>$type</c> only to a type on a fixed list, and refuses everything else.
    ///
    ///     <para>
    ///     Json.NET asks its binder for the type to instantiate before it looks at anything else, so
    ///     this is the one place where a stored file could pick a type. Nothing here loads a type
    ///     by name: the answer is always one of the <see cref="Type"/> objects the caller handed in.
    ///     </para>
    /// </summary>
    internal sealed partial class AllowListSerializationBinder : ISerializationBinder
    {
        private readonly DefaultSerializationBinder defaultBinder = new();
        private readonly Dictionary<string, Type> allowed = new(StringComparer.Ordinal);

        public AllowListSerializationBinder(IEnumerable<Type> allowedTypes)
        {
            foreach (var type in allowedTypes)
                allowed[Normalize(type.FullName ?? type.Name)] = type;
        }

        public Type BindToType(string? assemblyName, string typeName)
        {
            //The assembly is deliberately not part of the key. It is the type's full name that has to
            //match, and a version or a token in the stored assembly name must not make an allowed
            //type unreachable. A different assembly defining the same full name still resolves to
            //the type the caller listed, never to a type chosen by the file.
            if (allowed.TryGetValue(Normalize(typeName), out var type))
                return type;

            throw new JsonSerializationException($"The type '{typeName}' named by a $type is not on the allow-list of types that may be read. Add it to JsonStorageOptions.AllowedTypes if it is expected.");
        }

        public void BindToName(Type serializedType, out string? assemblyName, out string? typeName)
        {
            //Refuse on the way out too: a file written with a type that cannot be read back is a
            //file nobody can read.
            if (!allowed.ContainsKey(Normalize(serializedType.FullName ?? serializedType.Name)))
                throw new JsonSerializationException($"The type '{serializedType.FullName}' is not on the allow-list of types that may be written with a $type. Add it to JsonStorageOptions.AllowedTypes.");

            defaultBinder.BindToName(serializedType, out assemblyName, out typeName);
        }

        //Drops the version, culture and token of every assembly named inside a closed generic type, so
        //List`1[[A, Asm, Version=1.0.0.0, ...]] and List`1[[A, Asm]] are the same type.
        private static string Normalize(string typeName) => AssemblyDetails().Replace(typeName, string.Empty);

        [GeneratedRegex(@", Version=[^,\]]+, Culture=[^,\]]+, PublicKeyToken=[^,\]]+")]
        private static partial Regex AssemblyDetails();
    }
}
