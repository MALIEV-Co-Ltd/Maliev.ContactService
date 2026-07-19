using System.Collections.Concurrent;
using Maliev.ContactService.Application.DTOs;
using Maliev.ContactService.Application.Interfaces;

namespace Maliev.ContactService.Tests.Services;

public sealed class CapturingContactNotificationPublisher : IContactNotificationPublisher
{
    private readonly ConcurrentQueue<ContactMessageDto> _publishedMessages = new();

    public IReadOnlyCollection<ContactMessageDto> PublishedMessages => _publishedMessages.ToArray();

    public Task PublishContactMessageSubmittedAsync(ContactMessageDto contactMessage, CancellationToken cancellationToken = default)
    {
        _publishedMessages.Enqueue(contactMessage);
        return Task.CompletedTask;
    }

    public void Reset()
    {
        while (_publishedMessages.TryDequeue(out _))
        {
        }
    }
}
