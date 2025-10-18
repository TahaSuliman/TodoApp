using System.Net;

namespace Web.Middleware
{
    // Base custom exception
    public abstract class BaseCustomException : Exception
    {
        public HttpStatusCode StatusCode { get; }
        public string ErrorCode { get; }

        protected BaseCustomException(string message, HttpStatusCode statusCode, string errorCode)
            : base(message)
        {
            StatusCode = statusCode;
            ErrorCode = errorCode;
        }

        protected BaseCustomException(string message, Exception innerException, HttpStatusCode statusCode, string errorCode)
            : base(message, innerException)
        {
            StatusCode = statusCode;
            ErrorCode = errorCode;
        }
    }

    // 400 Bad Request
    public class BadRequestException : BaseCustomException
    {
        public BadRequestException(string message)
            : base(message, HttpStatusCode.BadRequest, "BAD_REQUEST")
        {
        }
    }

    // 404 Not Found
    public class NotFoundException : BaseCustomException
    {
        public NotFoundException(string message)
            : base(message, HttpStatusCode.NotFound, "NOT_FOUND")
        {
        }

        public NotFoundException(string entityName, object key)
            : base($"{entityName} with id '{key}' was not found.", HttpStatusCode.NotFound, "NOT_FOUND")
        {
        }
    }

    // 401 Unauthorized
    public class UnauthorizedException : BaseCustomException
    {
        public UnauthorizedException(string message = "Unauthorized access.")
            : base(message, HttpStatusCode.Unauthorized, "UNAUTHORIZED")
        {
        }
    }

    // 403 Forbidden
    public class ForbiddenException : BaseCustomException
    {
        public ForbiddenException(string message = "Access to this resource is forbidden.")
            : base(message, HttpStatusCode.Forbidden, "FORBIDDEN")
        {
        }
    }

    // 409 Conflict
    public class ConflictException : BaseCustomException
    {
        public ConflictException(string message)
            : base(message, HttpStatusCode.Conflict, "CONFLICT")
        {
        }
    }

    // 422 Unprocessable Entity
    public class ValidationException : BaseCustomException
    {
        public IDictionary<string, string[]> Errors { get; }

        public ValidationException(IDictionary<string, string[]> errors)
            : base("One or more validation errors occurred.", HttpStatusCode.UnprocessableEntity, "VALIDATION_ERROR")
        {
            Errors = errors;
        }

        public ValidationException(string message)
            : base(message, HttpStatusCode.UnprocessableEntity, "VALIDATION_ERROR")
        {
            Errors = new Dictionary<string, string[]>();
        }
    }

    // 500 Internal Server Error
    public class InternalServerException : BaseCustomException
    {
        public InternalServerException(string message, Exception innerException = null)
            : base(message, innerException, HttpStatusCode.InternalServerError, "INTERNAL_SERVER_ERROR")
        {
        }
    }

    // 503 Service Unavailable
    public class ServiceUnavailableException : BaseCustomException
    {
        public ServiceUnavailableException(string message = "Service temporarily unavailable.")
            : base(message, HttpStatusCode.ServiceUnavailable, "SERVICE_UNAVAILABLE")
        {
        }
    }
}
