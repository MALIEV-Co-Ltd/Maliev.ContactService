using Maliev.ContactService.Data.DbContexts;
using Maliev.MessagingContracts.Generated;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Maliev.ContactService.Api.Consumers;

/// <summary>
/// Consumes FileDeletedEvent to clean up local contact file references.
/// </summary>
public class FileDeletedEventConsumer : IConsumer<FileDeletedEvent>
{
    private readonly ContactDbContext _dbContext;
    private readonly ILogger<FileDeletedEventConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileDeletedEventConsumer"/> class.
    /// </summary>
    public FileDeletedEventConsumer(ContactDbContext dbContext, ILogger<FileDeletedEventConsumer> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Consumes the FileDeletedEvent.
    /// </summary>
    public async Task Consume(ConsumeContext<FileDeletedEvent> context)
    {
        var message = context.Message;
        
        if (message.ServiceId != "contact-service")
        {
            // Possibly shared
        }

        _logger.LogInformation("Processing FileDeletedEvent for FileId: {FileId}, StoragePath: {StoragePath}", 
            message.FileId, message.StoragePath);

        // Find contact files associated with this file
        var files = await _dbContext.ContactFiles
            .Where(f => f.UploadServiceFileId == message.FileId || f.ObjectName == message.StoragePath)
            .ToListAsync();

        if (files.Any())
        {
            _dbContext.ContactFiles.RemoveRange(files);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Removed {Count} contact file references.", files.Count);
        }
    }
}
