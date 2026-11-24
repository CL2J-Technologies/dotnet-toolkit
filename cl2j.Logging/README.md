# cl2j.Logging

A `Microsoft.Extensions.Logging` provider that writes log lines to files through
[cl2j.FileStorage](../cl2j.FileStorage/README.md). Because it goes through FileStorage, the log can live on local disk,
Azure Blob Storage or S3-compatible storage, depending on how the storage named `Logger` is configured.

What it does:

- Buffers lines in memory and appends them to the log file at a fixed interval (and when the provider is disposed).
- Names files from a pattern (application name, date, file number) and starts a new file each day, or when the
  current one reaches a maximum size.
- Stamps every line in a time zone you choose.
- Optionally echoes lines to the console, and keeps the most recent lines in memory.
- Adds `LoggerBlock` (timed blocks with progress) and `Log*IfEnabled` extension methods.

# Getting started

```powershell
dotnet add package cl2j.Logging
```

The package depends on `cl2j.FileStorage`. The provider writes through the file storage named **`Logger`**, so that
storage must exist in the configuration.

## 1. Configure appsettings.json

```json
{
  "ApplicationName": "MyApp",
  "cl2j": {
    "FileStorage": {
      "Storages": {
        "Logger": {
          "Type": "Disk",
          "Path": "C:\\logs\\myapp"
        }
      }
    },
    "Logging": {
      "LogLevel": "Information",
      "TimeZoneName": "Eastern Standard Time",
      "FlushInterval": "00:00:05",
      "MaxSize": 3145728,
      "IncludeConsole": true,
      "ClearOnStartup": false,
      "MemoryLogs": false
    }
  }
}
```

Everything under `cl2j:Logging` is optional. See [Configuration](#configuration) for each key.

## 2. Register the services

```cs
using cl2j.FileStorage;
using cl2j.Logging;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var services = new ServiceCollection();

// FileStorage reads its storages from an IConfigurationRoot resolved from the container.
services.AddSingleton<IConfigurationRoot>(configuration);

services.AddFileStorage();           // creates the storages declared in the configuration (Disk is built in)
services.AddLogging(configuration);  // cl2j.Logging.LoggingExtensions.AddLogging
```

`AddLogging(IConfiguration, string? applicationName = null)` (and an `IConfigurationRoot` overload) registers
`LoggerProvider` as an `ILoggerProvider` and binds `LoggerOptions` from the `cl2j:Logging` section.

The application name used in the file name is the `applicationName` argument if you pass one, otherwise the
`ApplicationName` configuration value. Set one of them: without it the file name starts with `_`.

For Azure Blob Storage or S3, add the matching provider registration (`AddAzureBlobFileStorage()`,
`AddS3FileStorage()`) and set `"Type"` on the `Logger` storage accordingly. See the
[cl2j.FileStorage README](../cl2j.FileStorage/README.md).

## 3. Log as usual

```cs
public class OrderService(ILogger<OrderService> logger)
{
    public void Accept(int id)
    {
        logger.LogInformation("order {Id} accepted", id);
    }
}
```

Flush on shutdown. The provider writes its buffer every `FlushInterval`; disposing it writes what is left, so
dispose the service provider (or the `ILoggerProvider`) before the process exits. A host built with
`Host.CreateDefaultBuilder` / `WebApplication` does that for you.

# Configuration

`LoggerOptions`, bound from `cl2j:Logging`:

| Key | Default | Meaning |
| --- | --- | --- |
| `LogLevel` | none | Minimum level written by this provider (`Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, `None`). Applied as a filter on `LoggerProvider`. When absent, the provider adds no filter of its own and the usual `Logging` filters apply. |
| `MainFileNamePattern` | `<app>[_<instance>]_{0:yyyyMMdd}_{1:00}.log` | The file name pattern. See [File names and rollover](#file-names-and-rollover). |
| `TimeZoneName` | none (UTC) | A time zone id as understood by `TimeZoneInfo.FindSystemTimeZoneById` (for example `Eastern Standard Time` on Windows, `America/Toronto` on Linux). Used for the line timestamps, the date in the file name, and the daily rollover. A missing or unknown name falls back to UTC and prints a message to the console. |
| `MaxSize` | `3145728` (3 MB) | File size, in bytes, after which the next flush starts a new file. |
| `FlushInterval` | `00:00:05` | How often the buffer is written. A `TimeSpan`: `"00:00:30"` or `"0.00:00:00.500"`. |
| `IncludeConsole` | `false` | Also write each line to the console, colored by level. |
| `ClearOnStartup` | `false` | Delete the current log file when the provider starts, instead of appending to it. |
| `MemoryLogs` | `false` | Keep the most recent lines in memory. See [MemoryLogger](#memorylogger). |

# Output format

Each entry is one line, followed by the exception (full `ToString()`) on the next lines when there is one:

```
2026-09-06 19:59:12.417 INF [MyApp.OrderService] order 42 accepted
2026-09-06 19:59:12.502 ERR [MyApp.OrderService] payment failed
System.InvalidOperationException: card declined
   at ...
```

`yyyy-MM-dd HH:mm:ss.fff`, a three-letter level, the category name in brackets, then the formatted message.

| Level | Tag | Console color |
| --- | --- | --- |
| Critical | `CRI` | Red |
| Error | `ERR` | Red |
| Warning | `WRN` | Dark yellow |
| Information | `INF` | Cyan |
| Debug | `DBG` | White |
| Trace | `TRC` | Dark gray |

`LogLevel.None` writes nothing. Logging scopes (`BeginScope`) are not recorded.

# File names and rollover

The pattern is a `string.Format` pattern with two arguments: `{0}` is the current date and time (in the configured
time zone) and `{1}` is the file number, starting at 1. The result is converted to lower case.

The default pattern is built from the application name:

```
MyApp_{0:yyyyMMdd}_{1:00}.log        ->  myapp_20260906_01.log
```

When the `WEBSITE_INSTANCE_ID` configuration value is set (Azure App Service), the first characters of it are inserted
after the application name, so that several instances of a scaled-out site do not write to the same file. The
instance part is the underscore plus the first seven characters of the id.

A new file is chosen, at the next flush, when:

- the date (in the configured time zone) is not the date of the last write, or
- the file written so far has grown past `MaxSize`.

The new name uses the first file number whose file does not exist yet or is smaller than `MaxSize`:
`myapp_20260906_01.log` is followed by `myapp_20260906_02.log`. The size is checked before each flush is written, so
a file can end up larger than `MaxSize` by up to one flush.

To use your own naming, set `MainFileNamePattern` under `cl2j:Logging`:

```json
"MainFileNamePattern": "web_{0:yyyy-MM-dd}_{1:000}.log"
```

# MemoryLogger

With `"MemoryLogs": true`, the provider also keeps the last 1000 lines in memory, which is handy to show recent
activity in a diagnostics page. Older lines are dropped first.

```cs
var provider = serviceProvider.GetServices<ILoggerProvider>().OfType<LoggerProvider>().Single();

foreach (var log in provider.MemoryLogger.GetLogs())   // LogData: Stamp, Level, Text, Exception
    Console.WriteLine($"{log.Stamp:HH:mm:ss} {log.Level} {log.Text}");

provider.ClearMemoryLogs();
```

`MemoryLogger` is `null` when `MemoryLogs` is not enabled.

# LoggerBlock

`LoggerBlock` logs the start of an operation, then its end with the elapsed time. Create one from any `ILogger` with
`CreateBlockTrace`, `CreateBlockDebug` or `CreateBlockInfo` (the level the block logs at), and dispose it to close it:

```cs
using (var block = logger.CreateBlockInfo("Import customers"))
{
    var rows = Import();
    block.EndState = $"{rows} rows";
}
```

```
2026-09-06 19:59:12.417 INF [Importer] Import customers
2026-09-06 19:59:15.829 INF [Importer] Import customers --> 1200 rows [00:00:03.412]
```

- `BlockOutput.End` as the second argument skips the start line and only logs the closing one.
- `EndState` is the text after the arrow in the closing line. The elapsed time is `[hh:mm:ss.fff]`.
- `TraceTime(text)` and `DebugTime(text)` log `text [elapsed]` at that level (when enabled) and restart the stopwatch,
  to time the steps of a longer block.
- `ElapsedMilliseconds` and `Step` expose the current stopwatch and progress count.

For a long loop, pass `ProgressOptions` and call `Increment`. A progress line (`<step>/<count>`, plus the optional
text) is written at most once per `WriteFrequency` (2 seconds with `ProgressOptions.Create(count)`):

```cs
using var block = logger.CreateBlockInfo("Export", BlockOutput.StartAndEnd, ProgressOptions.Create(items.Count));
foreach (var item in items)
{
    Export(item);
    block.Increment(item.Name);
}
```

# `*IfEnabled` extensions

`LogTraceIfEnabled`, `LogDebugIfEnabled`, `LogInformationIfEnabled`, `LogWarningIfEnabled` and
`LogErrorIfEnabled` check `logger.IsEnabled(level)` and call the matching `Log*` method only when the level is on.
They take a message template and `params object[] args`, and live in the `Microsoft.Extensions.Logging` namespace,
so no extra `using` is needed.

```cs
logger.LogDebugIfEnabled("loaded {Count} items in {Ms} ms", items.Count, elapsed);
```

# More

The unit tests in `cl2j.Logging.Tests` show each behavior above, and `cl2j.Logging.TestApp` is a runnable console
sample.
