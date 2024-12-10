using System.Diagnostics;
using cl2j.SqlServerMaintenancePlan.IndexMaintenance.Repository;

namespace cl2j.SqlServerMaintenancePlan.IndexMaintenance
{
    public class SqlServerMaintenanceService(string connectionString)
    {
        private readonly SqlServerIndexRepository maintenancePlanRepository = new(connectionString);

        public async Task<OptimizeIndexResult> OptimizeIndexes(bool simulate)
        {
            var result = new OptimizeIndexResult();

            int no = 0;
            var fragmentations = await maintenancePlanRepository.GetIndexesFragmentation();
            foreach (var fragmentation in fragmentations)
            {
                ++no;
                if (string.IsNullOrEmpty(fragmentation.Index))
                    continue;

                var indexText = $"{fragmentation.Schema}.{fragmentation.Table} {fragmentation.Index}";

                try
                {
                    if (fragmentation.Fragmentation > 30)
                    {
                        AddMessage(result.Messages, $"[{no}/{fragmentations.Count}] Rebulding index {indexText}: {fragmentation.Fragmentation:0.0}%");
                        if (!simulate)
                            await maintenancePlanRepository.RebuildOnlineAsync(fragmentation.Schema, fragmentation.Table, fragmentation.Index);
                        ++result.RebuildCount;
                    }
                    else if (fragmentation.Fragmentation > 10)
                    {
                        AddMessage(result.Messages, $"[{no}/{fragmentations.Count}] Reorganizing index {indexText} : {fragmentation.Fragmentation:0.0}%");
                        if (!simulate)
                            await maintenancePlanRepository.ReorganizeAsync(fragmentation.Schema, fragmentation.Table, fragmentation.Index);
                        ++result.ReorganizeCount;
                    }
                    else
                        AddMessage(result.Messages, $"[{no}/{fragmentations.Count}] No action on index {indexText} : {fragmentation.Fragmentation:0.0}%");
                }
                catch (Exception ex)
                {
                    AddMessage(result.Messages, $"[{no}/{fragmentations.Count}] Error: Unexpected error {indexText}: {ex.Message}");
                }
            }

            return result;
        }

        private static void AddMessage(List<string> messages, string text)
        {
            messages.Add(text);
            Debug.WriteLine(text);
        }
    }
}
