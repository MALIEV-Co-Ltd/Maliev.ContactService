using Maliev.ContactService.Domain.Entities;
using System.Threading.Channels;
using Maliev.ContactService.Application.Interfaces;

namespace Maliev.ContactService.Application.Services;

/// <summary>
/// Implementation of the audit log service using a background queue.
/// </summary>
public class AuditLogService : IAuditLogService
{
    private readonly Channel<AuditLog> _channel;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuditLogService"/> class.
    /// </summary>
    /// <param name="channel">The audit log channel.</param>
    public AuditLogService(Channel<AuditLog> channel)
    {
        _channel = channel;
    }

    /// <inheritdoc/>
    public void LogDecision(string userId, string action, string resource, bool result, string? reason, string? clientIp)
    {
        try
        {
            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTimeOffset.UtcNow,
                UserId = userId,
                Action = action,
                Resource = resource,
                Result = result,
                Reason = reason,
                ClientIp = clientIp
            };

            // Non-blocking try write. If queue full, it may drop log but won't block request.
            _channel.Writer.TryWrite(auditLog);
        }
        catch
        {
            // Silent fail for logging to ensure API stability
        }
    }
}
