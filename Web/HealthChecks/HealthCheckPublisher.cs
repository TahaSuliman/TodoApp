using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;




namespace Web.HealthChecks
{
    public class HealthCheckPublisher : IHealthCheckPublisher
    {
        private readonly ILogger<HealthCheckPublisher> _logger;
        private readonly Dictionary<HealthStatus, LogLevel> _logLevelMap;

        public HealthCheckPublisher(ILogger<HealthCheckPublisher> logger)
        {
            _logger = logger;
            _logLevelMap = new Dictionary<HealthStatus, LogLevel>
            {
                { HealthStatus.Healthy, LogLevel.Information },
                { HealthStatus.Degraded, LogLevel.Warning },
                { HealthStatus.Unhealthy, LogLevel.Error }
            };
        }

        public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
        {
            var logLevel = _logLevelMap[report.Status];

            // Log the overall status
            _logger.Log(logLevel,
                "System is in {Status} state. Duration: {Duration}ms",
                report.Status,
                report.TotalDuration.TotalMilliseconds);

            // Log details of each individual check
            foreach (var entry in report.Entries)
            {
                var entryLogLevel = _logLevelMap[entry.Value.Status];

                if (entry.Value.Exception != null)
                {
                    _logger.Log(entryLogLevel,
                        entry.Value.Exception,
                        "Health check {Name} is in {Status} state: {Description}",
                        entry.Key,
                        entry.Value.Status,
                        entry.Value.Description);
                }
                else
                {
                    _logger.Log(entryLogLevel,
                        "Health check {Name} is in {Status} state: {Description}",
                        entry.Key,
                        entry.Value.Status,
                        entry.Value.Description);
                }

                // Log additional data if present
                if (entry.Value.Data.Count > 0)
                {
                    _logger.Log(entryLogLevel,
                        "Additional data for health check {Name}: {@Data}",
                        entry.Key,
                        entry.Value.Data);
                }
            }

            return Task.CompletedTask;
        }
    }
}
