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
    public sealed class StatementParameter(string name, object value)
    {
        public string Name { get; } = name;

        public object Value { get; } = value;
    }
}
