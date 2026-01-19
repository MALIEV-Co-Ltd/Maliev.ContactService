using System.Net;
using Maliev.ContactService.Api.Services;
using Maliev.ContactService.Data.DbContexts;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Maliev.ContactService.Tests.Integration;

public class ProgramTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;

    public ProgramTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public void Root_Services_Are_Registered()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        // Act & Assert
        Assert.NotNull(services.GetService<IContactService>());
        Assert.NotNull(services.GetService<IUploadServiceClient>());
        Assert.NotNull(services.GetService<ICountryServiceClient>());
        Assert.NotNull(services.GetService<ContactDbContext>());
    }

    [Fact]
    public async Task App_Starts_And_Responds_To_Liveness()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/contact/liveness");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task App_Starts_And_Responds_To_Readiness()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/contact/readiness");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OpenAPI_Endpoint_Available_In_Development()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/contact/openapi/v1.json");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
