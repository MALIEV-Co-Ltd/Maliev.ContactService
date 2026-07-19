using Maliev.ContactService.Domain.Constants;
using Maliev.ContactService.Application.Interfaces;
using System.Security.Claims;

namespace Maliev.ContactService.Api.Middleware;

/// <summary>
/// Middleware for logging authorization results that were not captured by the PermissionHandler.
/// </summary>
public class AuditLogMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditLogMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuditLogMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">The logger instance.</param>
    public AuditLogMiddleware(RequestDelegate next, ILogger<AuditLogMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Invokes the middleware to process the request.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="auditLog">The audit log service.</param>
    public async Task InvokeAsync(HttpContext context, IAuditLogService auditLog)
    {
        await _next(context);

        // Capture 401 and 403 responses that might not have been logged by PermissionHandler
        if (context.Response.StatusCode == StatusCodes.Status401Unauthorized ||
            context.Response.StatusCode == StatusCodes.Status403Forbidden)
        {
            // Avoid double logging if PermissionHandler already logged it (though logging twice is better than not at all)
            // We could use context.Items to flag if it was already logged.

            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
            var action = context.Request.Method + " " + context.Request.Path;
            var resource = context.Request.Path;
            var result = false;
            var reason = context.Response.StatusCode == StatusCodes.Status401Unauthorized ? "Unauthorized (Missing or invalid token)" : "Forbidden";
            var clientIp = context.Connection.RemoteIpAddress?.ToString();

            // Only log if it's a security-related failure that wasn't already explicitly logged by our granular handler
            // For now, simple logging is safer.
            _logger.LogWarning("Security event detected in middleware: {StatusCode} for {UserId} at {Path}",
                context.Response.StatusCode, userId, context.Request.Path);

            auditLog.LogDecision(userId, action, resource, result, reason, clientIp);
        }
    }
}
