namespace cl2j.Database.CommandBuilders
{
    /// <summary>
    ///     Decoupe une liste de cles en autant d enonces que necessaire.
    ///
    ///     <para>
    ///     Une requete par cles lie un parametre par cle, et SQL Server refuse une commande au-dela
    ///     de 2100 parametres. Sans decoupage, la methode marche jusqu au jour ou un appelant passe
    ///     une liste plus longue que celles qu on a essayees — et l echec arrive alors loin de la
    ///     ligne qui l a cause. Le plafond retenu laisse de la marge sous la limite du serveur.
    ///     </para>
    /// </summary>
    internal static class QueryKeyBatches
    {
        internal const int MaxKeysPerStatement = 2000;

        internal static IEnumerable<TextStatement> Build(ICommandBuilder commandBuilder, Type type, IEnumerable<object> keys)
        {
            //Chunk ne rend aucun bloc pour une source vide, ce qui est exactement ce qu il faut :
            //un IN vide n est pas du SQL valide, donc l absence de cles doit produire zero enonce
            //plutot qu un enonce qui echoue.
            foreach (var batch in keys.Chunk(MaxKeysPerStatement))
                yield return commandBuilder.GetQueryByKeysStatement(type, batch);
        }
    }
}
