using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Web.Services
{
    public class DatabaseHealthMonitorService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<DatabaseHealthMonitorService> _logger;

        public DatabaseHealthMonitorService(
            IServiceProvider services,
            ILogger<DatabaseHealthMonitorService> logger)
        {
            _services = services;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _services.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var canConnect = await dbContext.Database.CanConnectAsync(stoppingToken);
                    if (canConnect)
                    {
                        _logger.LogInformation("Database is connected and working correctly - {Time}", DateTimeOffset.Now);
                    }
                    else
                    {
                        _logger.LogWarning("Cannot connect to the database - {Time}", DateTimeOffset.Now);
                    }

                    // فحص كل 5 دقائق
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while monitoring database health");
                    // انتظر دقيقة واحدة قبل المحاولة مرة أخرى في حالة الخطأ
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
            }
        }
    }
}