namespace cl2j.SqlServerMaintenancePlan.IndexMaintenance.Repository
{
    public class IndexFragmentation
    {
        public string Schema { get; set; } = null!;
        public string Table { get; set; } = null!;
        public string Index { get; set; } = null!;
        public double Fragmentation { get; set; }

        public override string ToString()
        {
            return $"{Schema}.{Table} {Index} : {Fragmentation:0.0}% fragmented.";
        }
    }
}