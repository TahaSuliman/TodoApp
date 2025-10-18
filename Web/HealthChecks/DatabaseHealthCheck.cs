using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;



namespace Web.HealthChecks
{
    public class DatabaseHealthCheck : IHealthCheck
    {
        private readonly AppDbContext _context;
        private readonly ILogger<DatabaseHealthCheck> _logger;

        public DatabaseHealthCheck(AppDbContext context, ILogger<DatabaseHealthCheck> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var startTime = DateTime.UtcNow;
                _logger.LogDebug("Starting database health check");

                // Attempt to connect to the database
                var canConnect = await _context.Database.CanConnectAsync(cancellationToken);
                var responseTime = DateTime.UtcNow - startTime;

                var data = new Dictionary<string, object>
                {
                    { "responseTime", responseTime.TotalMilliseconds },
                    { "checkTime", DateTime.UtcNow }
                };

                if (canConnect)
                {
                    try
                    {
                        // Add additional database information
                        var databaseName = _context.Database.GetDbConnection().Database;
                        data.Add("databaseName", databaseName);

                        // Log entity counts in the database
                        var todoCount = await _context.Set<Domain.Entities.Todo>().CountAsync(cancellationToken);
                        var userCount = await _context.Set<Domain.Entities.User>().CountAsync(cancellationToken);
                        data.Add("todoCount", todoCount);
                        data.Add("userCount", userCount);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to retrieve additional database information");
                    }

                    _logger.LogInformation("Successfully connected to the database. Response time: {ResponseTime}ms", responseTime.TotalMilliseconds);
                    return HealthCheckResult.Healthy(
                        "Database is connected and functioning properly",
                        data);
                }

                _logger.LogError(
                    "Failed to connect to the database. Response time: {ResponseTime}ms",
                    responseTime.TotalMilliseconds);

                return HealthCheckResult.Unhealthy(
                    "Unable to connect to the database",
                    data: data);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "An unexpected error occurred while checking database health");

                return HealthCheckResult.Unhealthy(
                    "An error occurred while checking database health",
                    ex,
                    new Dictionary<string, object>
                    {
                        { "errorType", ex.GetType().Name },
                        { "errorMessage", ex.Message },
                        { "checkTime", DateTime.UtcNow }
                    });
            }
        }
    }
}

//namespace Web.HealthChecks
//{
//    public class DatabaseHealthCheck : IHealthCheck
//    {
//        private readonly AppDbContext _context;
//        private readonly ILogger<DatabaseHealthCheck> _logger;

//        public DatabaseHealthCheck(AppDbContext context, ILogger<DatabaseHealthCheck> logger)
//        {
//            _context = context;
//            _logger = logger;
//        }

//        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
//        {
//            try
//            {
//                var startTime = DateTime.UtcNow;
//                _logger.LogDebug("بدء فحص قاعدة البيانات");

//                // محاولة الاتصال بقاعدة البيانات
//                var canConnect = await _context.Database.CanConnectAsync(cancellationToken);
//                var responseTime = DateTime.UtcNow - startTime;

//                var data = new Dictionary<string, object>
//                {
//                    { "responseTime", responseTime.TotalMilliseconds },
//                    { "checkTime", DateTime.UtcNow }
//                };

//                if (canConnect)
//                {
//                    try
//                    {
//                        // إضافة معلومات إضافية عن قاعدة البيانات
//                        var databaseName = _context.Database.GetDbConnection().Database;
//                        data.Add("databaseName", databaseName);

//                        // تسجيل عدد الكيانات في قاعدة البيانات
//                        var todoCount = await _context.Set<Domain.Entities.Todo>().CountAsync(cancellationToken);
//                        var userCount = await _context.Set<Domain.Entities.User>().CountAsync(cancellationToken);
//                        data.Add("todoCount", todoCount);
//                        data.Add("userCount", userCount);
//                    }
//                    catch (Exception ex)
//                    {
//                        _logger.LogWarning(ex, "فشل في الحصول على معلومات إضافية عن قاعدة البيانات");
//                    }

//                    _logger.LogInformation("تم الاتصال بقاعدة البيانات بنجاح. وقت الاستجابة: {ResponseTime}ms", responseTime.TotalMilliseconds);
//                    return HealthCheckResult.Healthy(
//                        "قاعدة البيانات متصلة وتعمل بشكل صحيح",
//                        data);
//                }

//                _logger.LogError(
//                    "فشل الاتصال بقاعدة البيانات. وقت الاستجابة: {ResponseTime}ms",
//                    responseTime.TotalMilliseconds);

//                return HealthCheckResult.Unhealthy(
//                    "لا يمكن الاتصال بقاعدة البيانات",
//                    data: data);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(
//                    ex,
//                    "حدث خطأ غير متوقع أثناء فحص حالة قاعدة البيانات");

//                return HealthCheckResult.Unhealthy(
//                    "حدث خطأ أثناء فحص حالة قاعدة البيانات",
//                    ex,
//                    new Dictionary<string, object>
//                    {
//                        { "errorType", ex.GetType().Name },
//                        { "errorMessage", ex.Message },
//                        { "checkTime", DateTime.UtcNow }
//                    });
//            }
//        }
//    }
//}