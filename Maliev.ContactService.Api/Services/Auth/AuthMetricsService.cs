using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Configuration;
using Maliev.Aspire.ServiceDefaults.Authorization;

namespace Maliev.ContactService.Api.Services.Auth;

/// <summary>
/// Service for tracking authorization-related business metrics.
/// </summary>
public class AuthMetricsService : IAuthMetrics
{
    private readonly Counter<long> _authSuccessCounter;
    private readonly Counter<long> _authFailureCounter;
    private readonly KeyValuePair<string, object?>[] _defaultTags;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthMetricsService"/> class.
    /// </summary>
    /// <param name="meterFactory">The meter factory.</param>
    /// <param name="configuration">The configuration.</param>
    public AuthMetricsService(IMeterFactory meterFactory, IConfiguration configuration)
    {
        var serviceName = configuration["Service:Name"] ?? "ContactService";
        var meter = meterFactory.Create($"{serviceName.ToLower()}-auth-meter");

        _defaultTags = new[]
        {
            new KeyValuePair<string, object?>("service_name", serviceName),
            new KeyValuePair<string, object?>("version", configuration["Service:Version"] ?? "1.0.0"),
            new KeyValuePair<string, object?>("region", configuration["Service:Region"] ?? "global"),
            new KeyValuePair<string, object?>("environment", configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production")
        };

        _authSuccessCounter = meter.CreateCounter<long>(
            "contact_auth_success_total",
            unit: "{decisions}",
            description: "Total number of successful authorization decisions");

        _authFailureCounter = meter.CreateCounter<long>(
            "contact_auth_failure_total",
            unit: "{decisions}",
            description: "Total number of failed authorization decisions");
    }

    /// <summary>
    /// Records a successful authorization decision.
    /// </summary>
    /// <param name="permission">The permission that was checked.</param>
    public void RecordSuccess(string permission)
    {
        var tags = new TagList();
        foreach (var tag in _defaultTags) tags.Add(tag);
        tags.Add("permission", permission);
        _authSuccessCounter.Add(1, tags);
    }

    /// <summary>
    /// Records a failed authorization decision.
    /// </summary>
    /// <param name="permission">The permission that was checked.</param>
    /// <param name="reason">The reason for failure.</param>
    public void RecordFailure(string permission, string reason)
    {
        var tags = new TagList();
        foreach (var tag in _defaultTags) tags.Add(tag);
        tags.Add("permission", permission);
        tags.Add("reason", reason);
        _authFailureCounter.Add(1, tags);
    }
}
