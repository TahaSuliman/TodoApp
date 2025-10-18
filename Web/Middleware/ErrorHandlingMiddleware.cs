using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

namespace Web.Middleware
{


    //using YourNamespace.Exceptions;

    namespace YourNamespace.Middleware
    {
        public class ErrorHandlingMiddleware
        {
            private readonly RequestDelegate _next;
            private readonly ILogger<ErrorHandlingMiddleware> _logger;
            private readonly IWebHostEnvironment _environment;

            public ErrorHandlingMiddleware(
                RequestDelegate next,
                ILogger<ErrorHandlingMiddleware> logger,
                IWebHostEnvironment environment)
            {
                _next = next;
                _logger = logger;
                _environment = environment;
            }

            public async Task InvokeAsync(HttpContext context)
            {
                try
                {
                    await _next(context);
                }
                catch (Exception ex)
                {
                    await HandleExceptionAsync(context,  ex);
                }
            }

            private async Task HandleExceptionAsync(HttpContext context, Exception exception)
            {
                var correlationId = GetOrCreateCorrelationId(context);

                // Log based on exception severity
                LogException(exception, correlationId, context);

                // Prepare response
                context.Response.ContentType = "application/json";

                var errorResponse = BuildErrorResponse(exception, correlationId, context);
                context.Response.StatusCode = errorResponse.StatusCode;

                var jsonResponse = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = _environment.IsDevelopment()
                });

                await context.Response.WriteAsync(jsonResponse);
            }

            private ErrorResponse BuildErrorResponse(Exception exception, string correlationId, HttpContext context)
            {
                var response = new ErrorResponse
                {
                    CorrelationId = correlationId,
                    Timestamp = DateTime.UtcNow,
                    Path = context.Request.Path,
                    Method = context.Request.Method
                };

                switch (exception)
                {
                    case BaseCustomException customEx:
                        response.StatusCode = (int)customEx.StatusCode;
                        response.Message = customEx.Message;
                        response.ErrorCode = customEx.ErrorCode;

                        if (customEx is ValidationException validationEx)
                        {
                            response.ValidationErrors = validationEx.Errors;
                        }
                        break;

                    case DbUpdateException dbEx when dbEx.InnerException is SqlException sqlEx:
                        response.StatusCode = (int)HttpStatusCode.BadRequest;
                        response.Message = GetSqlExceptionMessage(sqlEx);
                        response.ErrorCode = "DATABASE_ERROR";
                        break;

                    case DbUpdateConcurrencyException:
                        response.StatusCode = (int)HttpStatusCode.Conflict;
                        response.Message = "The record you attempted to update was modified by another user.";
                        response.ErrorCode = "CONCURRENCY_ERROR";
                        break;

                    case ArgumentNullException:
                    case ArgumentException:
                        response.StatusCode = (int)HttpStatusCode.BadRequest;
                        response.Message = "Invalid request data.";
                        response.ErrorCode = "INVALID_ARGUMENT";
                        break;

                    case KeyNotFoundException:
                        response.StatusCode = (int)HttpStatusCode.NotFound;
                        response.Message = "The requested resource was not found.";
                        response.ErrorCode = "NOT_FOUND";
                        break;

                    case UnauthorizedAccessException:
                        response.StatusCode = (int)HttpStatusCode.Unauthorized;
                        response.Message = "Unauthorized access.";
                        response.ErrorCode = "UNAUTHORIZED";
                        break;

                    case InvalidOperationException:
                        response.StatusCode = (int)HttpStatusCode.BadRequest;
                        response.Message = "Invalid operation.";
                        response.ErrorCode = "INVALID_OPERATION";
                        break;

                    case TimeoutException:
                        response.StatusCode = (int)HttpStatusCode.RequestTimeout;
                        response.Message = "The request timed out.";
                        response.ErrorCode = "TIMEOUT";
                        break;

                    case OperationCanceledException:
                        response.StatusCode = 499; // Client Closed Request
                        response.Message = "The operation was cancelled.";
                        response.ErrorCode = "OPERATION_CANCELLED";
                        break;

                    case NotImplementedException:
                        response.StatusCode = (int)HttpStatusCode.NotImplemented;
                        response.Message = "This feature is not implemented yet.";
                        response.ErrorCode = "NOT_IMPLEMENTED";
                        break;

                    default:
                        response.StatusCode = (int)HttpStatusCode.InternalServerError;
                        response.Message = "An unexpected error occurred. Please try again later.";
                        response.ErrorCode = "INTERNAL_SERVER_ERROR";
                        break;
                }

                // Include detailed error information only in development
                if (_environment.IsDevelopment())
                {
                    response.Details = exception.Message;
                    response.StackTrace = exception.StackTrace;
                    response.InnerException = exception.InnerException?.Message;
                }

                return response;
            }

            private void LogException(Exception exception, string correlationId, HttpContext context)
            {
                var logMessage = "Exception occurred. CorrelationId: {CorrelationId}, Path: {Path}, Method: {Method}, User: {User}";
                var user = context.User?.Identity?.Name ?? "Anonymous";

                if (exception is BaseCustomException customEx && customEx.StatusCode < HttpStatusCode.InternalServerError)
                {
                    // Client errors (4xx) - log as warning
                    _logger.LogWarning("Middleware 😒😒" + exception.Message, logMessage, correlationId, context.Request.Path, context.Request.Method, user);
                }
                else
                {
                    // Server errors (5xx) - log as error
                    _logger.LogError("Middleware 😒😒" + exception, logMessage, correlationId, context.Request.Path, context.Request.Method, user);
                }
            }

            private string GetSqlExceptionMessage(SqlException sqlEx)
            {
                return sqlEx.Number switch
                {
                    2601 or 2627 => "A record with the same key already exists.",
                    547 => "The operation conflicts with existing data constraints.",
                    515 => "Required field is missing.",
                    -1 or -2 => "Database connection timeout. Please try again.",
                    _ => "A database error occurred."
                };
            }

            private string GetOrCreateCorrelationId(HttpContext context)
            {
                const string correlationIdHeader = "X-Correlation-ID";

                if (context.Request.Headers.TryGetValue(correlationIdHeader, out var correlationId))
                {
                    // Ensure we never return null, use ToString() which returns string.Empty if null
                    return correlationId.ToString();
                }

                var newCorrelationId = Guid.NewGuid().ToString();
                context.Response.Headers.Append(correlationIdHeader, newCorrelationId);
                return newCorrelationId;
            }
        }

        public class ErrorResponse
        {
            public int StatusCode { get; set; }
            public string Message { get; set; } = string.Empty;
            public string ErrorCode { get; set; } = string.Empty;
            public string CorrelationId { get; set; } = string.Empty;
            public DateTime Timestamp { get; set; }
            public string Path { get; set; } = string.Empty;
            public string Method { get; set; } = string.Empty;
            public string? Details { get; set; }
            public string? StackTrace { get; set; }
            public string? InnerException { get; set; }
            public IDictionary<string, string[]>? ValidationErrors { get; set; }
        }
    }

    //===================================//



    //public class ErrorHandlingMiddleware
    //{
    //    private readonly RequestDelegate _next;
    //    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    //    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    //    {
    //        _next = next;
    //        _logger = logger;
    //    }

    //    public async Task Invoke(HttpContext context)
    //    {
    //        try
    //        {
    //            await _next(context);
    //        }
    //        catch (Exception ex)
    //        {
    //            await HandleExceptionAsync(context, ex);
    //        }
    //    }

    //    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    //    {
    //        var code = HttpStatusCode.InternalServerError;
    //        var result = string.Empty;

    //        switch (exception)
    //        {
    //            case ValidationException validationException:
    //                code = HttpStatusCode.BadRequest;
    //                result = JsonSerializer.Serialize(new { error = "خطأ في البيانات المدخلة" });
    //                _logger.LogWarning(exception, "Validation error occurred");
    //                break;
    //            case NotFoundException notFoundException:
    //                code = HttpStatusCode.NotFound;
    //                result = JsonSerializer.Serialize(new { error = "العنصر غير موجود" });
    //                _logger.LogWarning(exception, "Resource not found");
    //                break;
    //            default:
    //                result = JsonSerializer.Serialize(new { error = "حدث خطأ في النظام" });
    //                _logger.LogError(exception, "An unexpected error occurred");
    //                break;
    //        }

    //        context.Response.ContentType = "application/json";
    //        context.Response.StatusCode = (int)code;

    //        await context.Response.WriteAsync(result);
    //    }
    //}




}