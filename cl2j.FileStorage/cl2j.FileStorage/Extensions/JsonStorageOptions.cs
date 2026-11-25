namespace cl2j.FileStorage.Extensions
{
    /// <summary>
    ///     Opts a JSON read or write into polymorphism.
    ///
    ///     <para>
    ///     A <c>$type</c> property names the .NET type to instantiate, so whoever can write a file
    ///     to the storage must not be able to choose it. The JSON helpers therefore refuse every
    ///     type that is not listed here, and the list is empty by default. Reading a file that holds
    ///     a <c>$type</c>, or writing a value whose runtime type differs from its declared type
    ///     (the only case that needs one), throws a
    ///     <see cref="Newtonsoft.Json.JsonSerializationException"/> rather than following the name
    ///     or silently dropping the derived properties. A file with no <c>$type</c> reads as it
    ///     always did. A caller whose model has polymorphic members lists the types it expects.
    ///     </para>
    /// </summary>
    public sealed class JsonStorageOptions
    {
        /// <summary>
        ///     The types that may be written with, and resolved from, a <c>$type</c>. A closed
        ///     generic type is matched as a whole, with its arguments: list
        ///     <c>typeof(List&lt;Shape&gt;)</c> for a member that holds a <c>List&lt;Shape&gt;</c>
        ///     where the declared type is not that. Empty by default, which refuses every
        ///     <c>$type</c>.
        /// </summary>
        public IReadOnlyCollection<Type> AllowedTypes { get; init; } = [];
    }
}
