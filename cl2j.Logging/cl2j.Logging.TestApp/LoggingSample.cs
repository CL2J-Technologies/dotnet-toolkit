using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace cl2j.Logging.TestApp
{
    internal class LoggingSample
    {
        private readonly ILogger<LoggingSample> logger;

        public LoggingSample(ILogger<LoggingSample> logger)
        {
            this.logger = logger;
        }

        public async Task ExecuteAsync()
        {
            using (logger.CreateBlockDebug("Debug block"))
            {
                logger.LogTrace("This is a trace");
                logger.LogInformation("This is an information");
                logger.LogDebug("This is a debug");

                await Task.Delay(500);
            }
        }
    }
}