using System.Net;
using System.Text.Json;
using Maliev.ContactService.Api.Exceptions;

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

        var (statusCode, title, detail) = exception switch
        {
            DuplicateInquiryException => (
                (int)HttpStatusCode.Conflict,
                "Conflict",
                exception.Message
            ),
            CountryServiceException => (
                (int)HttpStatusCode.ServiceUnavailable,
                "Service Unavailable",
                exception.Message
            ),
            NotFoundException => (
                (int)HttpStatusCode.NotFound,
                "Not Found",
                exception.Message
            ),
            ArgumentException => (
                (int)HttpStatusCode.BadRequest,
                "Bad Request",
                exception.Message
            ),
            InvalidOperationException => (
                (int)HttpStatusCode.Conflict,
                "Conflict",
                exception.Message
            ),
            _ => (
                (int)HttpStatusCode.InternalServerError,
                "Internal Server Error",
                "An unexpected error occurred while processing your request."
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
            type = $"https://tools.ietf.org/html/rfc7231#section-6.6.1", // Generic for now
            title = title,
            status = statusCode,
            detail = detail,
            instance = context.Request.Path
        });

        await response.WriteAsync(result);
    }
}
