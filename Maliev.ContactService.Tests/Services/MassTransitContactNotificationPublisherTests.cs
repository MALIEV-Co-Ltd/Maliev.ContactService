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
    public async Task PublishContactMessageSubmittedAsync_ValidContact_PublishesNotificationEventForContactInbox()
    {
        NotificationEvent? publishedEvent = null;
        var publishEndpoint = new Mock<IPublishEndpoint>();
        publishEndpoint
            .Setup(endpoint => endpoint.Publish(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationEvent, CancellationToken>((message, _) => publishedEvent = message)
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

        publishEndpoint.Verify(endpoint => endpoint.Publish(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(publishedEvent);
        Assert.Equal(nameof(NotificationEvent), publishedEvent.MessageName);
        Assert.Equal("ContactService", publishedEvent.PublishedBy);
        Assert.Contains("NotificationService", publishedEvent.ConsumedBy);
        Assert.Equal("ContactMessageSubmitted", publishedEvent.Payload.NotificationType);
        Assert.Equal("critical", publishedEvent.Payload.Priority);
        Assert.Equal("contact-message-submitted", publishedEvent.Payload.TemplateId);

        var target = Assert.Single(publishedEvent.Payload.TargetUsers);
        Assert.Equal("maliev-contact-inbox", target.UserId);
        Assert.Equal("staff", target.UserType);

        var parameters = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(publishedEvent.Payload.Parameters);
        Assert.Equal("42", parameters["contactId"]);
        Assert.Equal("Website Customer", parameters["fullName"]);
        Assert.Equal("customer@example.com", parameters["email"]);
        Assert.Equal("+66 81 000 0000", parameters["phoneNumber"]);
        Assert.Equal("Customer Co", parameters["company"]);
        Assert.Equal("Can MALIEV review this part?", parameters["subject"]);
        Assert.Equal("Please review the attached drawing.", parameters["message"]);
        Assert.Equal("General", parameters["contactType"]);
        Assert.Equal("Urgent", parameters["priority"]);
        Assert.Equal("1", parameters["attachmentCount"]);
        Assert.Equal("drawing.pdf", parameters["attachmentNames"]);
    }
}
