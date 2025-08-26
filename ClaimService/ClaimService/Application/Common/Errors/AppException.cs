// ClaimService.Application/Common/Errors/AppException.cs
using System.Net;
namespace ClaimService.Application.Common.Errors;

public class AppException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string ErrorCode { get; }

    public AppException(string message,
                        HttpStatusCode statusCode = HttpStatusCode.BadRequest,
                        string errorCode = "AppError",
                        Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }
}

// 404 helper
public sealed class NotFoundException : AppException
{
    public NotFoundException(string message, string code = "NotFound")
        : base(message, HttpStatusCode.NotFound, code) { }
}

// Validation with field errors
public sealed class ValidationException : AppException
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException(IDictionary<string, string[]> errors,
                               string message = "One or more validation errors occurred.",
                               string code = "ValidationError")
        : base(message, HttpStatusCode.BadRequest, code)
    {
        Errors = errors;
    }
}
