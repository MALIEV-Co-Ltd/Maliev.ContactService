using System.Net;
using System.Net.Http.Json;
using Maliev.ContactService.Api.Models;
using Maliev.ContactService.Data.DbContexts;
using Maliev.ContactService.Data.Models;
using Maliev.ContactService.Api.Services;
using Maliev.ContactService.Tests.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Testcontainers.PostgreSql;
using Xunit;

namespace Maliev.ContactService.Tests.Integration;

public class T055_RateLimitingTests : IClassFixture<RateLimitingTestWebApplicationFactory>, IAsyncLifetime
{
    private readonly RateLimitingTestWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public T055_RateLimitingTests(RateLimitingTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.InitializeAsync();
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task T055_Rate_Limiting_Should_Block_11th_Request_With_429()
    {
        var testEmail = $"ratelimit.{Guid.NewGuid():N}@example.com";
        var requests = new List<Task<HttpResponseMessage>>();

        for (int i = 0; i < 11; i++)
        {
            var request = new CreateContactMessageRequest
            {
                FullName = $"Rate Limit Test {i}",
                Email = $"{i}.{testEmail}",
                Subject = "Rate Limit Test",
                Message = "Testing rate limiting",
                CountryId = Guid.Empty,
                ContactType = ContactType.General,
                Priority = Priority.Medium,
                Files = new List<CreateContactFileRequest>()
            };
            requests.Add(_client.PostAsJsonAsync("/contact/v1/contacts", request));
        }

        var responses = await Task.WhenAll(requests);

        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.TooManyRequests);
        Assert.True(responses.Count(r => r.StatusCode == HttpStatusCode.Created) <= 10);
    }
}
