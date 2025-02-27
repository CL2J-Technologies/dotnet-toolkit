using System.Reflection;
using cl2j.Database.DataAnnotations;

namespace cl2j.Database.CommandBuilders.Models
{
    public class ColumnDescriptor
    {
        public string Name { get; set; } = null!;
        public string NameFormatted { get; set; } = null!;

        public PropertyInfo Property { get; set; } = null!;

        public ColumnAttribute ColumnAtribute { get; set; } = null!;

        public override string ToString()
        {
            return $"{Name} Column[Name={ColumnAtribute.Name}, TypeName={ColumnAtribute.TypeName}, Key={ColumnAtribute.Key}, Required={ColumnAtribute.Required}, Json={ColumnAtribute.Json}, Length={ColumnAtribute.Length}, Decimals={ColumnAtribute.Decimals}, Default={ColumnAtribute.Default}]";
        }
    }
}
