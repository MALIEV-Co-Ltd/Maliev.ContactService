using Maliev.ContactService.Application.DTOs;

namespace Maliev.ContactService.Application.Interfaces;

/// <summary>
/// Publishes notifications when customer contact messages are submitted.
/// </summary>
public interface IContactNotificationPublisher
{
    /// <summary>
    /// Publishes a notification for a newly submitted contact message.
    /// </summary>
    /// <param name="contactMessage">The persisted contact message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task PublishContactMessageSubmittedAsync(ContactMessageDto contactMessage, CancellationToken cancellationToken = default);
}
