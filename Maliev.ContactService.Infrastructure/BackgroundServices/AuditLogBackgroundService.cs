using Maliev.ContactService.Infrastructure.Persistence;
using Maliev.ContactService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Maliev.ContactService.Infrastructure.BackgroundServices;

/// <summary>
/// Background service for processing audit log entries from a queue.
/// </summary>
public class AuditLogBackgroundService : BackgroundService
{
    private readonly Channel<AuditLog> _channel;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AuditLogBackgroundService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuditLogBackgroundService"/> class.
    /// </summary>
    /// <param name="channel">The audit log channel.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="logger">The logger instance.</param>
    public AuditLogBackgroundService(
        Channel<AuditLog> channel,
        IServiceProvider serviceProvider,
        ILogger<AuditLogBackgroundService> logger)
    {
        _channel = channel;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Audit Log Background Service is starting.");

        stoppingToken.Register(() => _channel.Writer.TryComplete());

        try
        {
            await foreach (var logEntry in _channel.Reader.ReadAllAsync(CancellationToken.None))
            {
                bool success = false;
                int retries = 0;
                const int maxRetries = 3;

                while (!success && retries < maxRetries)
                {
                    try
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var dbContext = scope.ServiceProvider.GetRequiredService<ContactDbContext>();

                        var strategy = dbContext.Database.CreateExecutionStrategy();
                        await strategy.ExecuteAsync(async () =>
                        {
                            dbContext.AuditLogs.Add(logEntry);
                            await dbContext.SaveChangesAsync(CancellationToken.None);
                        });
                        success = true;
                    }
                    catch (Exception ex)
                    {
                        retries++;
                        _logger.LogWarning(ex, "Retry {Count} for audit log entry for user {UserId}", retries, logEntry.UserId);
                        if (retries < maxRetries)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retries)), CancellationToken.None);
                        }
                        else
                        {
                            _logger.LogError(ex, "Failed to process audit log entry for user {UserId} after {Max} retries", logEntry.UserId, maxRetries);
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Audit Log Background Service is stopping.");
        }
    }
}
