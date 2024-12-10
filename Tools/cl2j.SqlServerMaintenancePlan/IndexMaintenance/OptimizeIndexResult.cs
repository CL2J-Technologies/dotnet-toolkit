namespace cl2j.SqlServerMaintenancePlan.IndexMaintenance
{
    public class OptimizeIndexResult
    {
        public List<string> Messages { get; set; } = [];

        public int RebuildCount { get; set; }
        public int ReorganizeCount { get; set; }
    }
}