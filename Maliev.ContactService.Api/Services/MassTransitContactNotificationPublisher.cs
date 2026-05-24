using System.Globalization;
using Maliev.ContactService.Application.DTOs;
using Maliev.ContactService.Application.Interfaces;
using Maliev.ContactService.Domain.Entities;
using Maliev.MessagingContracts;
using Maliev.MessagingContracts.Contracts.Shared;
using MassTransit;

namespace Maliev.ContactService.Api.Services;

/// <summary>
/// Publishes contact message notifications through the shared notification event contract.
/// </summary>
public sealed class MassTransitContactNotificationPublisher(
    IPublishEndpoint publishEndpoint,
    ILogger<MassTransitContactNotificationPublisher> logger) : IContactNotificationPublisher
{
    private const string ContactInboxUserId = "maliev-contact-inbox";
    private const string NotificationTemplateId = "contact-message-submitted";

    /// <inheritdoc />
    public async Task PublishContactMessageSubmittedAsync(ContactMessageDto contactMessage, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contactMessage);

        var notificationEvent = new NotificationEvent(
            MessageId: Guid.NewGuid(),
            MessageName: nameof(NotificationEvent),
            MessageType: MessageType.Event,
            MessageVersion: "1.0",
            PublishedBy: "ContactService",
            ConsumedBy: ["NotificationService"],
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: false,
            Payload: new NotificationEventPayload(
                NotificationType: "ContactMessageSubmitted",
                Priority: MapPriority(contactMessage.Priority),
                TargetUsers: [new NotificationEventPayloadTargetUsersItem(ContactInboxUserId, "staff")],
                TemplateId: NotificationTemplateId,
                Parameters: BuildParameters(contactMessage),
                Metadata: new NotificationEventPayloadMetadata("en", "ContactService")));

        await publishEndpoint.Publish(notificationEvent, cancellationToken);
        logger.LogInformation(
            "Published contact message notification for ContactId={ContactId}",
            contactMessage.Id);
    }

    private static IReadOnlyDictionary<string, string> BuildParameters(ContactMessageDto contactMessage)
    {
        return new Dictionary<string, string>
        {
            ["contactId"] = contactMessage.Id.ToString(CultureInfo.InvariantCulture),
            ["fullName"] = contactMessage.FullName,
            ["email"] = contactMessage.Email,
            ["phoneNumber"] = contactMessage.PhoneNumber ?? string.Empty,
            ["company"] = contactMessage.Company ?? string.Empty,
            ["subject"] = contactMessage.Subject,
            ["message"] = contactMessage.Message,
            ["countryId"] = contactMessage.CountryId.ToString(),
            ["contactType"] = contactMessage.ContactType.ToString(),
            ["priority"] = contactMessage.Priority.ToString(),
            ["attachmentCount"] = contactMessage.Files.Count.ToString(CultureInfo.InvariantCulture),
            ["attachmentNames"] = string.Join(", ", contactMessage.Files.Select(file => file.FileName))
        };
    }

    private static string MapPriority(Priority priority)
    {
        return priority switch
        {
            Priority.Urgent => "critical",
            Priority.High => "high",
            Priority.Low => "low",
            _ => "normal"
        };
    }
}
