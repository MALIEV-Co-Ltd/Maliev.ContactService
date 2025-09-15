using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Maliev.ContactService.Api.Models;
using Maliev.ContactService.Data.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace Maliev.ContactService.Tests.Integration;

/// <summary>
/// Integration tests for rate limiting functionality
/// </summary>
[Trait("Category", "Integration")]
public class RateLimitingIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public RateLimitingIntegrationTests()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
            });
    }

    [Fact(Skip = "Rate limiting tests require specific configuration for testing environment")]
    public async Task CreateContactMessage_Should_Be_Rate_Limited()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new CreateContactMessageRequest
        {
            FullName = "John Doe",
            Email = "john.doe@example.com",
            Subject = "Test Subject",
            Message = "Test Message",
            ContactType = ContactType.General
        };

        // Act - Make 10 requests (the limit is 10 per minute)
        for (int i = 0; i < 10; i++)
        {
            var response = await client.PostAsJsonAsync("/v1/contacts", request);
            response.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests, 
                $"Request {i + 1} should not be rate limited");
        }

        // Make one more request that should be rate limited
        var rateLimitedResponse = await client.PostAsJsonAsync("/v1/contacts", request);

        // Assert
        rateLimitedResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact(Skip = "Rate limiting tests require specific configuration for testing environment")]
    public async Task CreateContactMessage_Should_Allow_Requests_After_Rate_Limit_Period()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new CreateContactMessageRequest
        {
            FullName = "Jane Doe",
            Email = "jane.doe@example.com",
            Subject = "Test Subject",
            Message = "Test Message",
            ContactType = ContactType.General
        };

        // Act - Make 10 requests to hit the rate limit
        for (int i = 0; i < 10; i++)
        {
            await client.PostAsJsonAsync("/v1/contacts", request);
        }

        // Wait for rate limit window to reset (1 minute)
        await Task.Delay(TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(1));

        // Make another request which should be allowed now
        var response = await client.PostAsJsonAsync("/v1/contacts", request);

        // Assert
        response.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}