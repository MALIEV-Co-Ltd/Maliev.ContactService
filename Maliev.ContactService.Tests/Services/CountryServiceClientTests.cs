using System.Net;
using System.Text.Json;
using Maliev.ContactService.Api.Exceptions;
using Maliev.ContactService.Api.Models;
using Maliev.ContactService.Api.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Maliev.ContactService.Tests.Services;

public class CountryServiceClientTests
{
    private readonly Mock<ILogger<CountryServiceClient>> _loggerMock;
    private readonly JsonSerializerOptions _jsonOptions;

    public CountryServiceClientTests()
    {
        _loggerMock = new Mock<ILogger<CountryServiceClient>>();
        _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    }

    [Fact]
    public async Task ValidateCountryExistsAsync_ReturnsTrue_WhenCountryIsActive()
    {
        // Arrange
        var countryId = 1;
        var countryDto = new CountryDto { Id = countryId, Name = "Test Country", Iso2 = "TC", IsActive = true };
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(countryDto))
            });

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://localhost") };
        var client = new CountryServiceClient(httpClient, _loggerMock.Object);

        // Act
        var result = await client.ValidateCountryExistsAsync(countryId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ValidateCountryExistsAsync_ReturnsFalse_WhenCountryNotFound()
    {
        // Arrange
        var countryId = 1;
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            });

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://localhost") };
        var client = new CountryServiceClient(httpClient, _loggerMock.Object);

        // Act
        var result = await client.ValidateCountryExistsAsync(countryId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ValidateCountryExistsAsync_ReturnsFalse_WhenCountryIsNotActive()
    {
        // Arrange
        var countryId = 1;
        var countryDto = new CountryDto { Id = countryId, Name = "Test Country", Iso2 = "TC", IsActive = false };
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(countryDto))
            });

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://localhost") };
        var client = new CountryServiceClient(httpClient, _loggerMock.Object);

        // Act
        var result = await client.ValidateCountryExistsAsync(countryId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ValidateCountryExistsAsync_ThrowsCountryServiceException_WhenApiReturnsError()
    {
        // Arrange
        var countryId = 1;
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError
            });

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://localhost") };
        var client = new CountryServiceClient(httpClient, _loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<CountryServiceException>(() => client.ValidateCountryExistsAsync(countryId));
    }

    [Fact]
    public async Task ValidateCountryExistsAsync_ThrowsCountryServiceException_WhenTimeoutOccurs()
    {
        // Arrange
        var countryId = 1;
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new TaskCanceledException("", new TimeoutException()));

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://localhost") };
        var client = new CountryServiceClient(httpClient, _loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<CountryServiceException>(() => client.ValidateCountryExistsAsync(countryId));
    }
}
