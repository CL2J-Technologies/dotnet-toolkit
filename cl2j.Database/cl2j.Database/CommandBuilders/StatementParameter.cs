namespace cl2j.Database.CommandBuilders
{
    /// <summary>
    ///     Une valeur que l enonce reference par un espace reserve plutot que de la porter en clair
    ///     dans son texte.
    ///
    ///     <para>
    ///     Existe parce qu un enonce ne peut pas toujours se contenter des colonnes du type : un
    ///     <c>IN</c> a autant d espaces reserves que la liste a d elements, et cette liste n est
    ///     connue qu a l appel. Le constructeur d enonce nomme les espaces reserves, le code qui
    ///     execute lie les valeurs — aucune valeur ne devient du texte SQL en chemin.
    ///     </para>
    /// </summary>
    /// <param name="name">Le nom sans prefixe. Le formateur y ajoute le <c>@</c> pour le texte.</param>
    /// <param name="value">La valeur liee telle quelle.</param>
    public sealed class StatementParameter(string name, object value)
    {
        public string Name { get; } = name;

        public object Value { get; } = value;
    }
}
