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
    private const string ContactInboxTemplateId = "contact-message-submitted";
    private const string CustomerCopyTemplateId = "contact-message-customer-copy";

    /// <inheritdoc />
    public async Task PublishContactMessageSubmittedAsync(ContactMessageDto contactMessage, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contactMessage);

        var inboxNotification = new NotificationEvent(
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
                TemplateId: ContactInboxTemplateId,
                Parameters: BuildInboxParameters(contactMessage),
                Metadata: new NotificationEventPayloadMetadata("en", "ContactService")));

        var customerCopyNotification = new NotificationEvent(
            MessageId: Guid.NewGuid(),
            MessageName: nameof(NotificationEvent),
            MessageType: MessageType.Event,
            MessageVersion: "1.0",
            PublishedBy: "ContactService",
            ConsumedBy: ["NotificationService"],
            CorrelationId: inboxNotification.CorrelationId,
            CausationId: inboxNotification.MessageId,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: false,
            Payload: new NotificationEventPayload(
                NotificationType: "ContactMessageCustomerCopy",
                Priority: MapPriority(contactMessage.Priority),
                TargetUsers: [new NotificationEventPayloadTargetUsersItem($"contact-message-{contactMessage.Id}-customer", "direct-email")],
                TemplateId: CustomerCopyTemplateId,
                Parameters: BuildCustomerCopyParameters(contactMessage),
                Metadata: new NotificationEventPayloadMetadata("en", "ContactService")));

        await publishEndpoint.Publish(inboxNotification, cancellationToken);
        await publishEndpoint.Publish(customerCopyNotification, cancellationToken);
        logger.LogInformation(
            "Published contact message inbox and customer-copy notifications for ContactId={ContactId}",
            contactMessage.Id);
    }

    private static IReadOnlyDictionary<string, string> BuildInboxParameters(ContactMessageDto contactMessage)
    {
        return new Dictionary<string, string>
        {
            ["contactId"] = contactMessage.Id.ToString(CultureInfo.InvariantCulture),
            ["fullName"] = contactMessage.FullName,
            ["email"] = contactMessage.Email,
            ["phoneNumber"] = contactMessage.PhoneNumber ?? string.Empty,
            ["company"] = contactMessage.Company ?? string.Empty,
            ["subject"] = contactMessage.Subject,
            ["contactType"] = contactMessage.ContactType.ToString(),
            ["priority"] = contactMessage.Priority.ToString(),
            ["attachmentCount"] = contactMessage.Files.Count.ToString(CultureInfo.InvariantCulture),
            ["attachmentNames"] = string.Join(", ", contactMessage.Files.Select(file => file.FileName))
        };
    }

    private static IReadOnlyDictionary<string, string> BuildCustomerCopyParameters(ContactMessageDto contactMessage)
    {
        var parameters = new Dictionary<string, string>(BuildInboxParameters(contactMessage))
        {
            ["recipientEmail"] = contactMessage.Email,
            ["recipientName"] = contactMessage.FullName,
            ["message"] = contactMessage.Message
        };

        return parameters;
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
