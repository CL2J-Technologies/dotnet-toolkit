namespace cl2j.FileStorage.Extensions
{
    /// <summary>
    ///     Opts a JSON read or write into polymorphism.
    ///
    ///     <para>
    ///     By default the JSON helpers neither write nor follow a <c>$type</c> property: whoever can
    ///     write a file to the storage must not be able to choose which .NET type the reader
    ///     instantiates. A caller whose model needs polymorphic members lists the types it expects
    ///     here. Only those types are written with, and resolved from, a <c>$type</c>; any other
    ///     <c>$type</c> is refused with a <see cref="Newtonsoft.Json.JsonSerializationException"/>.
    ///     </para>
    /// </summary>
    public sealed class JsonStorageOptions
    {
        /// <summary>
        ///     The types that may be named by a <c>$type</c>. A closed generic type is matched as a
        ///     whole, with its arguments: list <c>typeof(List&lt;Shape&gt;)</c> for a member that
        ///     holds a <c>List&lt;Shape&gt;</c> where the declared type is not that. An empty list, the
        ///     default, leaves type names off.
        /// </summary>
        public IReadOnlyCollection<Type> AllowedTypes { get; init; } = [];
    }
}
