using cl2j.Database.Descriptors;

namespace cl2j.Database.CommandBuilders
{
    /// <summary>
    ///     A value the statement refers to through a placeholder, rather than carrying it as text.
    ///
    ///     <para>
    ///     Exists because a statement cannot always derive its parameters from the columns of a
    ///     type: an <c>IN</c> clause has as many placeholders as the list has entries, and that
    ///     list is only known at the call. The command builder names the placeholders, the code
    ///     that executes binds the values — no value becomes SQL text along the way.
    ///     </para>
    /// </summary>
    /// <param name="name">The bare name. The formatter prepends the <c>@</c> for the text.</param>
    /// <param name="value">The value, bound as-is.</param>
    /// <param name="column">
    ///     The column the value is compared against, when there is one. Carried so that binding
    ///     applies the same typing rules here as it does to a parameter derived from a column —
    ///     without it, the keys of an <c>IN</c> list would bind as Unicode and defeat the index on
    ///     an ANSI key column, which is the whole of issue #7 reappearing on a different path.
    /// </param>
    public sealed class StatementParameter(string name, object value, ColumnDescriptor? column = null)
    {
        public string Name { get; } = name;

        public object Value { get; } = value;

        public ColumnDescriptor? Column { get; } = column;
    }
}
