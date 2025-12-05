using Maliev.ContactService.Data.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Maliev.ContactService.Api.HealthChecks;

/// <summary>
/// Health check for the Contact Service database.
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly ContactDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseHealthCheck"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    public DatabaseHealthCheck(ContactDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Checks the health of the database connection.
    /// </summary>
    /// <param name="context">A context object associated with the current health check.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the health check.</param>
    /// <returns>A <see cref="Task"/> that completes with a <see cref="HealthCheckResult"/> indicating the health of the database.</returns>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.Database.CanConnectAsync(cancellationToken);
            return HealthCheckResult.Healthy("Database connection is healthy");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database connection failed", ex);
        }
    }
}