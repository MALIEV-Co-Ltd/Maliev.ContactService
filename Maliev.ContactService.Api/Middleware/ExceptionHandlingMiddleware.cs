using Maliev.ContactService.Api.Exceptions;
using System.Net;
using System.Text.Json;

namespace Maliev.ContactService.Api.Middleware;

/// <summary>
/// Middleware for handling exceptions and returning standardized error responses.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExceptionHandlingMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">The logger instance.</param>
    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Invokes the middleware to process the request.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // Unwrap AggregateException if it comes from TPL
        if (exception is AggregateException ae)
        {
            exception = ae.Flatten().InnerException ?? exception;
        }

        var response = context.Response;
        response.ContentType = "application/problem+json";

        var (statusCode, title, detail, type) = exception switch
        {
            DuplicateInquiryException => (
                (int)HttpStatusCode.Conflict,
                "Conflict",
                exception.Message,
                "https://tools.ietf.org/html/rfc7231#section-6.5.8"
            ),
            CountryServiceException => (
                (int)HttpStatusCode.ServiceUnavailable,
                "Service Unavailable",
                exception.Message,
                "https://tools.ietf.org/html/rfc7231#section-6.6.4"
            ),
            NotFoundException => (
                (int)HttpStatusCode.NotFound,
                "Not Found",
                exception.Message,
                "https://tools.ietf.org/html/rfc7231#section-6.5.4"
            ),
            ArgumentException => (
                (int)HttpStatusCode.BadRequest,
                "Bad Request",
                exception.Message,
                "https://tools.ietf.org/html/rfc7231#section-6.5.1"
            ),
            InvalidOperationException => (
                (int)HttpStatusCode.Conflict,
                "Conflict",
                exception.Message,
                "https://tools.ietf.org/html/rfc7231#section-6.5.8"
            ),
            _ => (
                (int)HttpStatusCode.InternalServerError,
                "Internal Server Error",
                "An unexpected error occurred while processing your request.",
                "https://tools.ietf.org/html/rfc7231#section-6.6.1"
            )
        };

        if (statusCode == (int)HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception occurred.");
        }
        else
        {
            _logger.LogWarning("Handled exception: {ExceptionType} - {Message}", exception.GetType().Name, exception.Message);
        }

        response.StatusCode = statusCode;

        var result = JsonSerializer.Serialize(new
        {
            type = type,
            title = title,
            status = statusCode,
            detail = detail,
            instance = context.Request.Path
        });

        await response.WriteAsync(result);
    }
}
