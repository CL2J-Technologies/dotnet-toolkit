using BenchmarkDotNet.Running;
using cl2j.Database.Benchmarks;

// The mapping suite needs no database, so the variable is only required when another suite is selected.
var mappingOnly = args.Any(a => a.Contains("Mapping", StringComparison.Ordinal));
if (!mappingOnly)
{
    try
    {
        // Fail here, in the host process, rather than once per benchmark in the child processes.
        BenchmarkSettings.GetConnectionString();
    }
    catch (InvalidOperationException ex)
    {
        await Console.Error.WriteLineAsync(ex.Message);
        return 1;
    }
}

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args.Length == 0 ? ["--filter", "*"] : args);
return 0;
