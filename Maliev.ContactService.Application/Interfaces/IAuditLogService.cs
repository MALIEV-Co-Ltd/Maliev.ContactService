namespace Maliev.ContactService.Application.Interfaces;

/// <summary>
/// Interface for logging authorization decisions.
/// </summary>
public interface IAuditLogService
{
    /// <summary>
    /// Logs an authorization decision.
    /// </summary>
    void LogDecision(string userId, string action, string resource, bool result, string? reason, string? clientIp);
}
