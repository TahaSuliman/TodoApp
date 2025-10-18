using Application.Services;
using Domain.Interfaces;
using HealthChecks.UI.Client;
using Infrastructure.Data;
using Infrastructure.HealthCheck;
using Infrastructure.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Polly;
using Serilog;
using Serilog.Context;
using Serilog.Events;
using Serilog.Sinks.Grafana.Loki;
using Web.HealthChecks;
using Web.Middleware;
using Web.Middleware.YourNamespace.Middleware;
using Web.Services;

var builder = WebApplication.CreateBuilder(args);

// === Environment Definition ===
var isDocker = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";

// 🔥 Correcting URLs
var healthEndpoint = isDocker ? "http://web:8080/health" : "http://localhost:8080/health";
var lokiUrl = isDocker ? "http://loki:3100" : "http://localhost:3100";  // ✅ Without /ready
var lokiHealthUrl = $"{lokiUrl}/ready";  // ✅ For health check only
var seqUrl = builder.Configuration["Seq:Url"] ?? "http://host.docker.internal:5341";

// === Serilog Configuration ===
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("MachineName", Environment.MachineName)
    .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
    .Enrich.WithProperty("Application", "TodoApp")
    .Enrich.WithProperty("ThreadId", Environment.CurrentManagedThreadId)
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.GrafanaLoki(
        lokiUrl,  // ✅ Using lokiUrl instead of lokiHealthUrl
        labels: new[]
        {
            new LokiLabel { Key = "app", Value = "TodoApp" },
            new LokiLabel { Key = "environment", Value = builder.Environment.EnvironmentName }
        })
    .WriteTo.Seq(seqUrl)
    .WriteTo.File("logs/log-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

Log.Information("📡 Loki URL: {LokiUrl}", lokiUrl);
Log.Information("🏥 Loki Health URL: {LokiHealthUrl}", lokiHealthUrl);
builder.Host.UseSerilog();
Log.Information("🚀 Starting TodoApp - Environment: {Environment}", builder.Environment.EnvironmentName);

// === Services ===
builder.Services.AddControllersWithViews();

// Database Configuration
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,  // ✅ Increased from 3 to 5
            maxRetryDelay: TimeSpan.FromSeconds(30),  // ✅ Increased from 10 to 30
            errorNumbersToAdd: null);
        sqlOptions.CommandTimeout(60);  // ✅ Increased from 30 to 60
    });

    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
    }
});

// 🔥 Health Checks
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database_health",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "database", "critical" })
    .AddDbContextCheck<AppDbContext>("database_context",
        tags: new[] { "database" })
    .AddSqlServer(
        connectionString: builder.Configuration.GetConnectionString("DefaultConnection")!,
        name: "sqlserver",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "database", "sqlserver" },
        timeout: TimeSpan.FromSeconds(10))  // ✅ Added timeout
    .AddUrlGroup(
        new Uri(lokiHealthUrl),
        name: "loki",
        failureStatus: HealthStatus.Degraded,
        tags: new[] { "monitoring", "loki" },
        timeout: TimeSpan.FromSeconds(5))  // ✅ Added timeout
    .AddCheck<SystemHealthCheck>("system_health",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "system", "performance" })
    .AddCheck("application_health", () =>
    {
        var uptime = DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime();
        if (uptime.TotalSeconds < 10)
            return HealthCheckResult.Degraded("Application is starting up");
        return HealthCheckResult.Healthy($"Application running for {uptime.TotalMinutes:F1} minutes");
    },
    tags: new[] { "application" });

// 🎨 Health Checks UI
builder.Services
    .AddHealthChecksUI(setup =>
    {
        setup.SetEvaluationTimeInSeconds(30);  // ✅ Increased from 10 to 30
        setup.MaximumHistoryEntriesPerEndpoint(50);
        setup.SetApiMaxActiveRequests(1);
        setup.AddHealthCheckEndpoint("TodoApp Health", healthEndpoint);

        Log.Information("🏥 Health Check UI endpoint: {HealthEndpoint}", healthEndpoint);
    })
    .AddInMemoryStorage();

builder.Services.Configure<HealthCheckPublisherOptions>(options =>
{
    options.Delay = TimeSpan.FromSeconds(10);  // ✅ Increased from 5 to 10
    options.Period = TimeSpan.FromMinutes(1);
});
builder.Services.AddSingleton<IHealthCheckPublisher, HealthCheckPublisher>();

// Repositories & Services
builder.Services.AddScoped<ITodoRepository, TodoRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ITodoService, TodoService>();
builder.Services.AddScoped<IUserService, UserService>();

// Hosted Services
builder.Services.AddHostedService<DatabaseHealthMonitorService>();
builder.Services.AddHttpClient("default");

var app = builder.Build();

// === Middleware Pipeline ===
app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
                        ?? Guid.NewGuid().ToString();
    context.Response.Headers.Append("X-Correlation-ID", correlationId);
    context.Items["CorrelationId"] = correlationId;

    using (LogContext.PushProperty("CorrelationId", correlationId))
    {
        await next();
    }
});

app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    options.GetLevel = (httpContext, elapsed, ex) => ex != null
        ? LogEventLevel.Error
        : elapsed > 5000 ? LogEventLevel.Warning : LogEventLevel.Information;

    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers["User-Agent"].ToString());
        diagnosticContext.Set("ClientIP", httpContext.Connection.RemoteIpAddress?.ToString());
        diagnosticContext.Set("CorrelationId", httpContext.Items["CorrelationId"]);
    };
});

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<ErrorHandlingMiddleware>();

// === Health Check Endpoints ===
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
    AllowCachingResponses = false,
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status200OK,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
    }
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
    AllowCachingResponses = false,
    Predicate = check => check.Tags.Contains("critical")
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync("{\"status\":\"alive\"}");
    },
    AllowCachingResponses = false,
    Predicate = _ => false
});

// 🎨 Health Checks UI Dashboard
app.MapHealthChecksUI(setup =>
{
    setup.UIPath = "/health-ui";
    setup.ApiPath = "/health-ui-api";
    setup.UseRelativeApiPath = false;
    setup.UseRelativeResourcesPath = false;
});

// === Database Initialization (Non-Blocking) ===
// === Database Initialization (Non-Blocking) ===
var databaseInitTask = Task.Run(async () =>
{
    Log.Information("🔄 Attempting to connect to database in background...");

    var retryPolicy = Policy
        .Handle<Exception>()
        .WaitAndRetryAsync(
            retryCount: 10,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (exception, timeSpan, retryCount, context) =>
            {
                Log.Warning(exception,
                    "⚠️ Database connection attempt {RetryCount} failed. Retrying in {RetryDelay:0.0} seconds. Error: {ErrorMessage}",
                    retryCount,
                    timeSpan.TotalSeconds,
                    exception.Message);
            });

    try
    {
        await retryPolicy.ExecuteAsync(async () =>
        {
            using var scope = app.Services.CreateScope();
            var services = scope.ServiceProvider;
            var context = services.GetRequiredService<AppDbContext>();

            var connectionString = context.Database.GetConnectionString();
            if (!string.IsNullOrEmpty(connectionString))
            {
                var maskedConnectionString = System.Text.RegularExpressions.Regex.Replace(
                    connectionString,
                    @"Password=.*?;|password=.*?;",
                    "Password=*****;",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                Log.Information("🔌 Connection string: {ConnectionString}", maskedConnectionString);
            }

            // ✅ FIXED: Direct migration - this creates DB if not exists
            Log.Information("🔄 Applying database migrations...");
            await context.Database.MigrateAsync();
            Log.Information("✅ Database migration completed successfully");
        });
    }
    catch (Exception ex)
    {
        var errorMessage = ex.InnerException != null
            ? $"{ex.Message} | Inner: {ex.InnerException.Message}"
            : ex.Message;

        Log.Error(ex,
            "❌ Failed to initialize database after all retry attempts. Error: {ErrorMessage}. " +
            "Application will continue running with degraded functionality.",
            errorMessage);
    }
});

//---------------old db init task---------------
//var databaseInitTask = Task.Run(async () =>
//{
//    Log.Information("🔄 Attempting to connect to database in background...");

//    var retryPolicy = Policy
//        .Handle<Exception>()
//        .WaitAndRetryAsync(
//            retryCount: 10,  // ✅ زيادة من 5 إلى 10
//            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
//            onRetry: (exception, timeSpan, retryCount, context) =>
//            {
//                Log.Warning(exception,
//                    "⚠️ Database connection attempt {RetryCount} failed. Retrying in {RetryDelay:0.0} seconds. Error: {ErrorMessage}",
//                    retryCount,
//                    timeSpan.TotalSeconds,
//                    exception.Message);  // ✅ إضافة رسالة الخطأ
//            });

//    try
//    {
//        await retryPolicy.ExecuteAsync(async () =>
//        {
//            using var scope = app.Services.CreateScope();
//            var services = scope.ServiceProvider;
//            var context = services.GetRequiredService<AppDbContext>();

//            var connectionString = context.Database.GetConnectionString();
//            if (!string.IsNullOrEmpty(connectionString))
//            {
//                var maskedConnectionString = System.Text.RegularExpressions.Regex.Replace(
//                    connectionString,
//                    @"Password=.*?;|password=.*?;",
//                    "Password=*****;",
//                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
//                Log.Information("🔌 Connection string: {ConnectionString}", maskedConnectionString);
//            }

//            // ✅ اختبار الاتصال أولاً
//            var canConnect = await context.Database.CanConnectAsync();
//            if (!canConnect)
//            {
//                throw new Exception("Database connection test failed");
//            }
//            Log.Information("✅ Database connection verified successfully");

//            // ثم تطبيق المايقريشنز
//            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
//            if (pendingMigrations.Any())
//            {
//                Log.Information("📊 Applying {MigrationCount} pending migrations", pendingMigrations.Count());
//                await context.Database.MigrateAsync();
//                Log.Information("✅ Database migrations completed successfully");
//            }
//            else
//            {
//                Log.Information("✅ Database is up to date - no pending migrations");
//            }
//        });
//    }
//    catch (Exception ex)
//    {
//        // ✅ تصحيح الـ logging
//        var errorMessage = ex.InnerException != null
//            ? $"{ex.Message} | Inner: {ex.InnerException.Message}"
//            : ex.Message;

//        Log.Error(ex,
//            "❌ Failed to initialize database after all retry attempts. Error: {ErrorMessage}. " +
//            "Application will continue running with degraded functionality.",
//            errorMessage);
//    }
//});

// === Routes ===
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// === Graceful Shutdown ===
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();

lifetime.ApplicationStarted.Register(() =>
{
    Log.Information("✅ Application started successfully");
    Log.Information("🎨 Health Checks UI available at: /health-ui");
    Log.Information("🏥 Health endpoint: /health");
    Log.Information("🔍 Environment: {Environment}", builder.Environment.EnvironmentName);
    Log.Information("🐳 Running in Docker: {IsDocker}", isDocker);

    _ = Task.Run(async () =>
    {
        await databaseInitTask;
        if (databaseInitTask.IsCompletedSuccessfully)
        {
            Log.Information("🎉 Database is ready!");
        }
        else
        {
            Log.Warning("⚠️ Application running without database connection.");
        }
    });
});

lifetime.ApplicationStopping.Register(() =>
{
    Log.Information("⚠️ Application is stopping...");
});

lifetime.ApplicationStopped.Register(() =>
{
    Log.Information("🛑 Application stopped");
});

// === Run Application ===
try
{
    Log.Information("🎯 Application is ready to handle requests");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "💥 Application terminated unexpectedly");
    throw;
}
finally
{
    Log.Information("🔄 Flushing logs...");
    await Log.CloseAndFlushAsync();
}






//======================//


//using Application.Services;
//using Domain.Interfaces;
//using HealthChecks.UI.Client;
//using Infrastructure.Data;
//using Infrastructure.HealthCheck;
//using Infrastructure.Repositories;
//using Microsoft.AspNetCore.Builder;
//using Microsoft.AspNetCore.Diagnostics.HealthChecks;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.Extensions.Diagnostics.HealthChecks;
//using Polly;
//using Serilog;
//using Serilog.Context;
//using Serilog.Events;
//using Serilog.Sinks.Grafana.Loki;
//using Web.HealthChecks;
//using Web.Middleware;
//using Web.Middleware.YourNamespace.Middleware;
//using Web.Services;

//var builder = WebApplication.CreateBuilder(args);

//// === Serilog Configuration ===
//var seqUrl = builder.Configuration["Seq:Url"] ?? "http://host.docker.internal:5341";


////=======================
//// 🔍 تحديد البيئة
//var isDocker = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";
//var healthEndpoint = isDocker ? "http://web:8080/health" : "http://localhost:8080/health";
//var lokiHealthUrl = isDocker ? "http://loki:3100/ready" : "http://localhost:3100/ready";


////=======================

//Log.Logger = new LoggerConfiguration()
//    .ReadFrom.Configuration(builder.Configuration)
//    .Enrich.FromLogContext()
//    .Enrich.WithProperty("MachineName", Environment.MachineName)
//    .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
//    .Enrich.WithProperty("Application", "TodoApp")
//    .Enrich.WithProperty("ThreadId", Environment.CurrentManagedThreadId)
//    .MinimumLevel.Information()
//    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
//    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
//    .MinimumLevel.Override("System", LogEventLevel.Warning)
//    .Enrich.FromLogContext()
//    .WriteTo.Console(
//        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
//    .WriteTo.GrafanaLoki(lokiHealthUrl, labels: new[]
//    {
//        new Serilog.Sinks.Grafana.Loki.LokiLabel { Key = "app", Value = "TodoApp" },
//        new Serilog.Sinks.Grafana.Loki.LokiLabel { Key = "environment", Value = builder.Environment.EnvironmentName }
//    })
//    .WriteTo.Seq(seqUrl)
//    .WriteTo.File("logs/log-.txt",
//        rollingInterval: RollingInterval.Day,
//        retainedFileCountLimit: 30,
//        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
//    .CreateLogger();

//Log.Information("📡 Loki URL: {LokiUrl}", lokiHealthUrl);
//builder.Host.UseSerilog();
//Log.Information("🚀 Starting TodoApp - Environment: {Environment}", builder.Environment.EnvironmentName);

//// === Services ===
//builder.Services.AddControllersWithViews();

//// Database Configuration
//builder.Services.AddDbContext<AppDbContext>(options =>
//{
//    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
//    options.UseSqlServer(connectionString, sqlOptions =>
//    {
//        sqlOptions.EnableRetryOnFailure(
//            maxRetryCount: 3,
//            maxRetryDelay: TimeSpan.FromSeconds(10),
//            errorNumbersToAdd: null);
//        sqlOptions.CommandTimeout(30);
//    });

//    if (builder.Environment.IsDevelopment())
//    {
//        options.EnableSensitiveDataLogging();
//        options.EnableDetailedErrors();
//    }
//});

////====================

//// ... (باقي الكود كما هو)


//// 🔥 Health Checks
//builder.Services.AddHealthChecks()
//    .AddCheck<DatabaseHealthCheck>("database_health",
//        failureStatus: HealthStatus.Unhealthy,
//        tags: new[] { "database", "critical" })
//    .AddDbContextCheck<AppDbContext>("database_context",
//        tags: new[] { "database" })
//    .AddSqlServer(
//        connectionString: builder.Configuration.GetConnectionString("DefaultConnection")!,
//        name: "sqlserver",
//        failureStatus: HealthStatus.Unhealthy,
//        tags: new[] { "database", "sqlserver" })
//    .AddUrlGroup(
//        new Uri(lokiHealthUrl),
//        name: "loki",
//        failureStatus: HealthStatus.Degraded,
//        tags: new[] { "monitoring", "loki" })
//    .AddCheck<SystemHealthCheck>("system_health",
//        failureStatus: HealthStatus.Unhealthy,
//        tags: new[] { "system", "performance" })
//    .AddCheck("application_health", () =>
//    {
//        var uptime = DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime();
//        if (uptime.TotalSeconds < 10)
//            return HealthCheckResult.Degraded("Application is starting up");
//        return HealthCheckResult.Healthy($"Application running for {uptime.TotalMinutes:F1} minutes");
//    },
//    tags: new[] { "application" });

//// 🎨 Health Checks UI
//builder.Services
//    .AddHealthChecksUI(setup =>
//    {
//        setup.SetEvaluationTimeInSeconds(10);
//        setup.MaximumHistoryEntriesPerEndpoint(50);
//        setup.SetApiMaxActiveRequests(1);

//        setup.AddHealthCheckEndpoint("TodoApp Health", healthEndpoint); // 🔥

//        Log.Information("🏥 Health Check UI endpoint: {HealthEndpoint}", healthEndpoint);
//    })
//    .AddInMemoryStorage();
////====================



////============================




//builder.Services.Configure<HealthCheckPublisherOptions>(options =>
//{
//    options.Delay = TimeSpan.FromSeconds(5);
//    options.Period = TimeSpan.FromMinutes(1);
//});
//builder.Services.AddSingleton<IHealthCheckPublisher, HealthCheckPublisher>();

//// Repositories & Services
//builder.Services.AddScoped<ITodoRepository, TodoRepository>();
//builder.Services.AddScoped<IUserRepository, UserRepository>();
//builder.Services.AddScoped<ITodoService, TodoService>();
//builder.Services.AddScoped<IUserService, UserService>();

//// Hosted Services
//builder.Services.AddHostedService<DatabaseHealthMonitorService>();
//builder.Services.AddHttpClient("default");



//var app = builder.Build();

//// === Middleware Pipeline ===
//app.Use(async (context, next) =>
//{
//    var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
//                        ?? Guid.NewGuid().ToString();
//    context.Response.Headers.Append("X-Correlation-ID", correlationId);
//    context.Items["CorrelationId"] = correlationId;

//    using (LogContext.PushProperty("CorrelationId", correlationId))
//    {
//        await next();
//    }
//});

//app.UseSerilogRequestLogging(options =>
//{
//    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
//    options.GetLevel = (httpContext, elapsed, ex) => ex != null
//        ? LogEventLevel.Error
//        : elapsed > 5000 ? LogEventLevel.Warning : LogEventLevel.Information;

//    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
//    {
//        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
//        diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
//        diagnosticContext.Set("UserAgent", httpContext.Request.Headers["User-Agent"].ToString());
//        diagnosticContext.Set("ClientIP", httpContext.Connection.RemoteIpAddress?.ToString());
//        diagnosticContext.Set("CorrelationId", httpContext.Items["CorrelationId"]);
//    };
//});

//if (!app.Environment.IsDevelopment())
//{
//    app.UseHsts();
//}
//else
//{
//    app.UseDeveloperExceptionPage();
//}

//app.UseHttpsRedirection();
//app.UseStaticFiles();
//app.UseRouting();
//app.UseAuthentication();
//app.UseAuthorization();
//app.UseMiddleware<ErrorHandlingMiddleware>();

//// === Health Check Endpoints ===
//// 📊 Health Checks API (JSON Format)



//app.MapHealthChecks("/health", new HealthCheckOptions
//{
//    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse, // 🔥 استخدام UI Response Writer
//    AllowCachingResponses = false,
//    ResultStatusCodes =
//    {
//        [HealthStatus.Healthy] = StatusCodes.Status200OK,
//        [HealthStatus.Degraded] = StatusCodes.Status200OK,
//        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
//    }
//});

//app.MapHealthChecks("/health/ready", new HealthCheckOptions
//{
//    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
//    AllowCachingResponses = false,
//    Predicate = check => check.Tags.Contains("critical")
//});

//app.MapHealthChecks("/health/live", new HealthCheckOptions
//{
//    ResponseWriter = async (context, report) =>
//    {
//        context.Response.ContentType = "application/json";
//        await context.Response.WriteAsync("{\"status\":\"alive\"}");
//    },
//    AllowCachingResponses = false,
//    Predicate = _ => false
//});

//// 🎨 Health Checks UI Dashboard
//app.MapHealthChecksUI(setup =>
//{
//    setup.UIPath = "/health-ui"; // 🔥 المسار: /health-ui
//    setup.ApiPath = "/health-ui-api";
//    setup.UseRelativeApiPath = false;
//    setup.UseRelativeResourcesPath = false;
//});

//// === Database Initialization (Non-Blocking) ===
//var databaseInitTask = Task.Run(async () =>
//{
//    Log.Information("🔄 Attempting to connect to database in background...");

//    var retryPolicy = Policy
//        .Handle<Exception>()
//        .WaitAndRetryAsync(
//            retryCount: 5,
//            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
//            onRetry: (exception, timeSpan, retryCount, context) =>
//            {
//                Log.Warning(exception,
//                    "⚠️ Database connection attempt {RetryCount} failed. Retrying in {RetryDelay:0.0} seconds",
//                    retryCount,
//                    timeSpan.TotalSeconds);
//            });

//    try
//    {
//        await retryPolicy.ExecuteAsync(async () =>
//        {
//            using var scope = app.Services.CreateScope();
//            var services = scope.ServiceProvider;
//            var context = services.GetRequiredService<AppDbContext>();

//            var connectionString = context.Database.GetConnectionString();
//            if (!string.IsNullOrEmpty(connectionString))
//            {
//                var maskedConnectionString = System.Text.RegularExpressions.Regex.Replace(
//                    connectionString,
//                    @"Password=.*?;|password=.*?;",
//                    "Password=*****;",
//                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
//                Log.Information("🔌 Connection string: {ConnectionString}", maskedConnectionString);
//            }

//            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
//            if (pendingMigrations.Any())
//            {
//                Log.Information("📊 Applying {MigrationCount} pending migrations", pendingMigrations.Count());
//                await context.Database.MigrateAsync();
//                Log.Information("✅ Database migrations completed successfully");
//            }
//            else
//            {
//                Log.Information("✅ Database is up to date - no pending migrations");
//            }

//            var canConnect = await context.Database.CanConnectAsync();
//            if (!canConnect)
//            {
//                throw new Exception("Database connection test failed");
//            }

//            Log.Information("✅ Database connection verified successfully");
//        });
//    }
//    catch (Exception ex)
//    {
//        Log.Error(ex.Message + ex.InnerException,
//            "❌ Failed to initialize database after all retry attempts. " +
//            "Application will continue running with degraded functionality.");
//    }
//});

//// === Routes ===
//app.MapControllerRoute(
//    name: "default",
//    pattern: "{controller=Home}/{action=Index}/{id?}");

//// === Graceful Shutdown ===
//var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();

//lifetime.ApplicationStarted.Register(() =>
//{
//    Log.Information("✅ Application started successfully");
//    Log.Information("🎨 Health Checks UI available at: /health-ui");

//    _ = Task.Run(async () =>
//    {
//        await databaseInitTask;
//        if (databaseInitTask.IsCompletedSuccessfully)
//        {
//            Log.Information("🎉 Database is ready!");
//        }
//        else
//        {
//            Log.Warning("⚠️ Application running without database connection.");
//        }
//    });
//});

//lifetime.ApplicationStopping.Register(() =>
//{
//    Log.Information("⚠️ Application is stopping...");
//});

//lifetime.ApplicationStopped.Register(() =>
//{
//    Log.Information("🛑 Application stopped");
//});

//// === Run Application ===
//try
//{
//    Log.Information("🎯 Application is ready to handle requests");
//    await app.RunAsync();
//}
//catch (Exception ex)
//{
//    Log.Fatal(ex, "💥 Application terminated unexpectedly");
//    throw;
//}
//finally
//{
//    Log.Information("🔄 Flushing logs...");
//    await Log.CloseAndFlushAsync();
//}





