using Maliev.ContactService.Api.Services;
using Maliev.ContactService.Application.DTOs;
using Maliev.ContactService.Domain.Entities;
using Maliev.MessagingContracts.Contracts.Shared;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Maliev.ContactService.Tests.Services;

public sealed class MassTransitContactNotificationPublisherTests
{
    [Fact]
    public async Task PublishContactMessageSubmittedAsync_ValidContact_PublishesInboxNotificationAndCustomerCopy()
    {
        var publishedEvents = new List<NotificationEvent>();
        var publishEndpoint = new Mock<IPublishEndpoint>();
        publishEndpoint
            .Setup(endpoint => endpoint.Publish(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationEvent, CancellationToken>((message, _) => publishedEvents.Add(message))
            .Returns(Task.CompletedTask);

        var publisher = new MassTransitContactNotificationPublisher(
            publishEndpoint.Object,
            NullLogger<MassTransitContactNotificationPublisher>.Instance);

        var contactMessage = new ContactMessageDto
        {
            Id = 42,
            FullName = "Website Customer",
            Email = "customer@example.com",
            PhoneNumber = "+66 81 000 0000",
            Company = "Customer Co",
            Subject = "Can MALIEV review this part?",
            Message = "Please review the attached drawing.",
            CountryId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            ContactType = ContactType.General,
            Priority = Priority.Urgent,
            Status = ContactStatus.New,
            CreatedAt = DateTimeOffset.Parse("2026-05-24T06:00:00Z"),
            UpdatedAt = DateTimeOffset.Parse("2026-05-24T06:00:00Z"),
            Files =
            [
                new ContactFileDto
                {
                    Id = 7,
                    FileName = "drawing.pdf",
                    ObjectName = "contacts/42/drawing.pdf",
                    ContentType = "application/pdf",
                    FileSize = 1280,
                    UploadServiceFileId = "upload-7",
                    CreatedAt = DateTimeOffset.Parse("2026-05-24T06:00:00Z")
                }
            ]
        };

        await publisher.PublishContactMessageSubmittedAsync(contactMessage);

        publishEndpoint.Verify(endpoint => endpoint.Publish(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        Assert.Equal(2, publishedEvents.Count);

        var inboxEvent = publishedEvents[0];
        Assert.Equal(nameof(NotificationEvent), inboxEvent.MessageName);
        Assert.Equal("ContactService", inboxEvent.PublishedBy);
        Assert.Contains("NotificationService", inboxEvent.ConsumedBy);
        Assert.Equal("ContactMessageSubmitted", inboxEvent.Payload.NotificationType);
        Assert.Equal("critical", inboxEvent.Payload.Priority);
        Assert.Equal("contact-message-submitted", inboxEvent.Payload.TemplateId);

        var target = Assert.Single(inboxEvent.Payload.TargetUsers);
        Assert.Equal("maliev-contact-inbox", target.UserId);
        Assert.Equal("staff", target.UserType);

        var parameters = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(inboxEvent.Payload.Parameters);
        Assert.Equal("42", parameters["contactId"]);
        Assert.Equal("Website Customer", parameters["fullName"]);
        Assert.Equal("customer@example.com", parameters["email"]);
        Assert.Equal("+66 81 000 0000", parameters["phoneNumber"]);
        Assert.Equal("Customer Co", parameters["company"]);
        Assert.Equal("Can MALIEV review this part?", parameters["subject"]);
        Assert.Equal("General", parameters["contactType"]);
        Assert.Equal("Urgent", parameters["priority"]);
        Assert.Equal("1", parameters["attachmentCount"]);
        Assert.Equal("drawing.pdf", parameters["attachmentNames"]);
        Assert.False(parameters.ContainsKey("message"));

        var customerEvent = publishedEvents[1];
        Assert.Equal("ContactMessageCustomerCopy", customerEvent.Payload.NotificationType);
        Assert.Equal("contact-message-customer-copy", customerEvent.Payload.TemplateId);

        var customerTarget = Assert.Single(customerEvent.Payload.TargetUsers);
        Assert.Equal("contact-message-42-customer", customerTarget.UserId);
        Assert.Equal("direct-email", customerTarget.UserType);

        var customerParameters = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(customerEvent.Payload.Parameters);
        Assert.Equal("customer@example.com", customerParameters["recipientEmail"]);
        Assert.Equal("Website Customer", customerParameters["recipientName"]);
        Assert.Equal("42", customerParameters["contactId"]);
        Assert.Equal("Please review the attached drawing.", customerParameters["message"]);
        Assert.Equal("drawing.pdf", customerParameters["attachmentNames"]);
    }
}
