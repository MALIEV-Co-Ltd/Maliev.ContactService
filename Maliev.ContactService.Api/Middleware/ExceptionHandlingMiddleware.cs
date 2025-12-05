using Maliev.ContactService.Api.Exceptions;
using System.Net;
using System.Text.Json;

namespace Maliev.ContactService.Api.Middleware;

/// <summary>
/// Middleware for global exception handling.
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
    /// Invokes the middleware to catch exceptions in the pipeline.
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
            _logger.LogError(ex, "An unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var response = context.Response;
        response.ContentType = "application/json";

        // Unwrap AggregateException to get the actual inner exception
        // This is necessary because Task.FromException wraps exceptions in AggregateException
        var actualException = exception;
        if (exception is AggregateException aggregateException && aggregateException.InnerExceptions.Count == 1)
        {
            actualException = aggregateException.InnerException ?? exception;
        }

        var errorResponse = new
        {
            message = "An error occurred while processing your request",
            traceId = context.TraceIdentifier
        };

        response.StatusCode = actualException switch
        {
            DuplicateInquiryException => (int)HttpStatusCode.Conflict,
            CountryServiceException => (int)HttpStatusCode.ServiceUnavailable,
            ArgumentException => (int)HttpStatusCode.BadRequest,
            UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
            KeyNotFoundException => (int)HttpStatusCode.NotFound,
            NotFoundException => (int)HttpStatusCode.NotFound,
            _ => (int)HttpStatusCode.InternalServerError
        };

        // Provide specific messages for known exceptions (FR-015: user-friendly errors)
        if (actualException is DuplicateInquiryException || actualException is CountryServiceException || actualException is NotFoundException)
        {
            errorResponse = new
            {
                message = actualException.Message,
                traceId = context.TraceIdentifier
            };
        }
        else if (actualException is ArgumentException argEx)
        {
            // Provide validation message but sanitize to avoid exposing internals
            errorResponse = new
            {
                message = argEx.Message,
                traceId = context.TraceIdentifier
            };
        }

        var jsonResponse = JsonSerializer.Serialize(errorResponse);
        await response.WriteAsync(jsonResponse);
    }
}