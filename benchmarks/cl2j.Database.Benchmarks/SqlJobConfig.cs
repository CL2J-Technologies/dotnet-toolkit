using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;

namespace cl2j.Database.Benchmarks;

/// <summary>
/// Job of the end-to-end suites: <c>ShortRun</c> to keep a full run short, or BenchmarkDotNet's
/// default job (tight error bars) when <c>CL2J_BENCH_JOB=default</c> is set, which is what the
/// version comparison uses to decide anything.
/// </summary>
public sealed class SqlJobConfig : ManualConfig
{
    public SqlJobConfig()
    {
        var useDefault = string.Equals(Environment.GetEnvironmentVariable("CL2J_BENCH_JOB"), "default", StringComparison.OrdinalIgnoreCase);
        AddJob(useDefault ? Job.Default : Job.ShortRun);
    }
}
