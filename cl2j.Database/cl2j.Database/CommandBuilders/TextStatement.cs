using cl2j.Database.Descriptors;

namespace cl2j.Database.CommandBuilders
{
    public class TextStatement
    {
        public TableDescriptor TableDescriptor { get; set; } = null!;

        public string Text { get; set; } = null!;

        /// <summary>
        ///     Les valeurs que <see cref="Text"/> reference par un espace reserve. Vide pour les
        ///     enonces dont tous les parametres se deduisent des colonnes du type.
        /// </summary>
        public IList<StatementParameter> Parameters { get; } = [];
    }
}
