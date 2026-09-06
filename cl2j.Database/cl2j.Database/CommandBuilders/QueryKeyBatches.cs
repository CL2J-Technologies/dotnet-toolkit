namespace cl2j.Database.CommandBuilders
{
    /// <summary>
    ///     Splits a key list into as many statements as it takes.
    ///
    ///     <para>
    ///     A query by keys binds one parameter per key, and SQL Server refuses a command beyond
    ///     2100 parameters. Without splitting, the method works until the day a caller passes a
    ///     longer list than anyone tried — and the failure then lands a long way from the line that
    ///     caused it. The ceiling below leaves room under the server's limit.
    ///     </para>
    /// </summary>
    internal static class QueryKeyBatches
    {
        internal const int MaxKeysPerStatement = 2000;

        internal static IEnumerable<TextStatement> Build(ICommandBuilder commandBuilder, Type type, IEnumerable<object> keys)
        {
            //Chunk yields no block for an empty source, which is exactly right: an empty IN is not
            //valid SQL, so no keys must produce no statement rather than one that fails.
            foreach (var batch in keys.Chunk(MaxKeysPerStatement))
                yield return commandBuilder.GetQueryByKeysStatement(type, batch);
        }
    }
}
