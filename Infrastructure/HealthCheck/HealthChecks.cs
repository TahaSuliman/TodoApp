using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.HealthCheck
{
    /// <summary>
    /// فحص صحة النظام (الذاكرة، CPU، وقت التشغيل)
    /// </summary>
    public class SystemHealthCheck : IHealthCheck
    {
        private readonly ILogger<SystemHealthCheck> _logger;
        private const long MaxMemoryMB = 1024; // 1 GB

        public SystemHealthCheck(ILogger<SystemHealthCheck> logger)
        {
            _logger = logger;
        }

        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var process = Process.GetCurrentProcess();
                var memoryUsedMB = process.WorkingSet64 / 1024 / 1024;
                var uptime = DateTime.UtcNow - process.StartTime.ToUniversalTime();
                var threadCount = process.Threads.Count;

                var data = new Dictionary<string, object>
                {
                    { "MemoryUsed_MB", memoryUsedMB },
                    { "MemoryLimit_MB", MaxMemoryMB },
                    { "Uptime", uptime.ToString(@"dd\.hh\:mm\:ss") },
                    { "ThreadCount", threadCount },
                    { "ProcessId", process.Id },
                    { "MachineName", Environment.MachineName }
                };

                // تحديد الحالة بناءً على استخدام الذاكرة
                if (memoryUsedMB > MaxMemoryMB)
                {
                    _logger.LogWarning("High memory usage detected: {MemoryUsed} MB", memoryUsedMB);
                    return Task.FromResult(HealthCheckResult.Unhealthy(
                        $"Memory usage ({memoryUsedMB} MB) exceeds limit ({MaxMemoryMB} MB)",
                        data: data));
                }

                if (memoryUsedMB > MaxMemoryMB * 0.8)
                {
                    return Task.FromResult(HealthCheckResult.Degraded(
                        $"Memory usage is high: {memoryUsedMB} MB",
                        data: data));
                }

                return Task.FromResult(HealthCheckResult.Healthy(
                    $"System is healthy. Memory: {memoryUsedMB} MB, Uptime: {uptime.TotalHours:F1} hours",
                    data: data));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking system health");
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    "Failed to check system health",
                    exception: ex));
            }
        }
    }
}
