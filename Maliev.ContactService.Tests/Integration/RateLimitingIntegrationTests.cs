using System.Net;
using System.Net.Http.Json;
using Maliev.ContactService.Api.Models;
using Maliev.ContactService.Data.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace Maliev.ContactService.Tests.Integration;

/// <summary>
/// Integration tests for rate limiting functionality.
/// Uses RateLimitingTestWebApplicationFactory which enables rate limiting
/// with faster windows (10 seconds) for quicker test execution.
/// </summary>
[Trait("Category", "Integration")]
public class RateLimitingIntegrationTests : IClassFixture<RateLimitingTestWebApplicationFactory>
{
    private readonly RateLimitingTestWebApplicationFactory _factory;

    public RateLimitingIntegrationTests(RateLimitingTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateContactMessage_Should_Be_Rate_Limited()
    {
        // Arrange
        var client = _factory.CreateClient();
        var testId = Guid.NewGuid().ToString("N");

        // Act - Make 5 requests (the limit is 5 per 10 seconds) - use unique emails to avoid duplicate detection
        for (int i = 0; i < 5; i++)
        {
            var request = new CreateContactMessageRequest
            {
                FullName = "John Doe",
                Email = $"john.doe.{testId}.{i}@example.com",
                Subject = "Test Subject",
                Message = "Test Message",
                CountryId = 1,
                ContactType = ContactType.General
            };
            var response = await client.PostAsJsonAsync("/contact/v1/contacts", request);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode); // Request {i + 1} should not be rate limited
        }

        // Make one more request that should be rate limited (use unique email)
        var rateLimitRequest = new CreateContactMessageRequest
        {
            FullName = "John Doe",
            Email = $"john.doe.{testId}.final@example.com",
            Subject = "Test Subject",
            Message = "Test Message",
            CountryId = 1,
            ContactType = ContactType.General
        };
        var rateLimitedResponse = await client.PostAsJsonAsync("/contact/v1/contacts", rateLimitRequest);

        // Assert
        Assert.Equal(HttpStatusCode.TooManyRequests, rateLimitedResponse.StatusCode);
    }

    [Fact]
    public async Task CreateContactMessage_Should_Allow_Requests_After_Rate_Limit_Period()
    {
        // Arrange
        var client = _factory.CreateClient();
        var testId = Guid.NewGuid().ToString("N");

        // Act - Make 5 requests to hit the rate limit (use unique emails to avoid duplicate detection)
        for (int i = 0; i < 5; i++)
        {
            var request = new CreateContactMessageRequest
            {
                FullName = "Jane Doe",
                Email = $"jane.doe.{testId}.{i}@example.com",
                Subject = "Test Subject",
                Message = "Test Message",
                CountryId = 1,
                ContactType = ContactType.General
            };
            await client.PostAsJsonAsync("/contact/v1/contacts", request);
        }

        // Wait for rate limit window to reset (10 seconds)
        await Task.Delay(TimeSpan.FromSeconds(11));

        // Make another request which should be allowed now (use unique email)
        var finalRequest = new CreateContactMessageRequest
        {
            FullName = "Jane Doe",
            Email = $"jane.doe.{testId}.final.{Guid.NewGuid():N}@example.com",
            Subject = "Test Subject",
            Message = "Test Message",
            CountryId = 1,
            ContactType = ContactType.General
        };
        var response = await client.PostAsJsonAsync("/contact/v1/contacts", finalRequest);

        // Assert
        Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
