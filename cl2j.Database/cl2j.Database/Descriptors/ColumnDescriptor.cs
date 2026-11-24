using System.Reflection;
using cl2j.Database.DataAnnotations;

namespace cl2j.Database.Descriptors
{
    public class ColumnDescriptor
    {
        public string Name { get; set; } = null!;
        public string NameFormatted { get; set; } = null!;

        public PropertyInfo Property { get; set; } = null!;

        public ColumnAttribute ColumnAtribute { get; set; } = null!;

        /// <summary>
        ///     True when this column is one of several key columns of its table. Such a column is
        ///     always supplied by the caller: a server-generated key (identity, a Guid default) only
        ///     makes sense for a table with a single key column. Set once, by <see cref="TableDescriptorFactory"/>.
        /// </summary>
        public bool IsPartOfCompositeKey { get; set; }

        public override string ToString()
        {
            return $"{Name} Column[Name={ColumnAtribute.Name}, TypeName={ColumnAtribute.TypeName}, Key={ColumnAtribute.Key}, Required={ColumnAtribute.Required}, Json={ColumnAtribute.Json}, Length={ColumnAtribute.Length}, Decimals={ColumnAtribute.Decimals}, Default={ColumnAtribute.Default}]";
        }
    }
}
