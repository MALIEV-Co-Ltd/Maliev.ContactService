using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Maliev.ContactService.Api.Exceptions;
using Maliev.ContactService.Api.Models;
using Maliev.ContactService.Api.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Maliev.ContactService.Tests.Services;

public class ServiceClientTests
{
    private readonly Mock<HttpMessageHandler> _handlerMock;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<CountryServiceClient>> _countryLoggerMock;
    private readonly Mock<ILogger<UploadServiceClient>> _uploadLoggerMock;

    public ServiceClientTests()
    {
        _handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        _httpClient = new HttpClient(_handlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost")
        };
        _countryLoggerMock = new Mock<ILogger<CountryServiceClient>>();
        _uploadLoggerMock = new Mock<ILogger<UploadServiceClient>>();
    }

    [Fact]
    public async Task ValidateCountryExistsAsync_WhenCountryExistsAndIsActive_ReturnsTrue()
    {
        // Arrange
        var countryDto = new CountryDto { Id = 1, Name = "Test", Iso2 = "TS", IsActive = true };
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = JsonContent.Create(countryDto)
            });

        var client = new CountryServiceClient(_httpClient, _countryLoggerMock.Object);

        // Act
        var result = await client.ValidateCountryExistsAsync(1);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ValidateCountryExistsAsync_WhenCountryNotFound_ReturnsFalse()
    {
        // Arrange
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            });

        var client = new CountryServiceClient(_httpClient, _countryLoggerMock.Object);

        // Act
        var result = await client.ValidateCountryExistsAsync(1);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ValidateCountryExistsAsync_WhenNullResponse_ReturnsFalse()
    {
        // Arrange
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("null")
            });

        var client = new CountryServiceClient(_httpClient, _countryLoggerMock.Object);

        // Act
        var result = await client.ValidateCountryExistsAsync(1);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ValidateCountryExistsAsync_WhenHttpRequestException_ThrowsCountryServiceException()
    {
        // Arrange
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var client = new CountryServiceClient(_httpClient, _countryLoggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<CountryServiceException>(() => client.ValidateCountryExistsAsync(1));
    }

    [Fact]
    public async Task ValidateCountryExistsAsync_WhenTimeout_ThrowsCountryServiceException()
    {
        // Arrange
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Timeout", new TimeoutException()));

        var client = new CountryServiceClient(_httpClient, _countryLoggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<CountryServiceException>(() => client.ValidateCountryExistsAsync(1));
    }

    [Fact]
    public async Task UploadFileAsync_WhenSuccessful_ReturnsUploadResponse()
    {
        // Arrange
        var uploadResponse = new UploadResponse
        {
            FileId = "test-id",
            ObjectName = "obj-1",
            Bucket = "bucket-1"
        };
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = JsonContent.Create(uploadResponse, options: new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
            });

        var client = new UploadServiceClient(_httpClient, _uploadLoggerMock.Object);

        // Act
        var result = await client.UploadFileAsync("obj-1", new byte[] { 1, 2, 3 }, "text/plain", "test.txt");

        // Assert
        Assert.Equal("test-id", result.FileId);
    }

    [Fact]
    public async Task UploadFileAsync_WhenFailure_Throws()
    {
        // Arrange
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError
            });

        var client = new UploadServiceClient(_httpClient, _uploadLoggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => client.UploadFileAsync("obj-1", new byte[] { 1, 2, 3 }, "text/plain", "test.txt"));
    }

    [Fact]
    public async Task DeleteFileAsync_WhenSuccessful_ReturnsTrue()
    {
        // Arrange
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK
            });

        var client = new UploadServiceClient(_httpClient, _uploadLoggerMock.Object);

        // Act
        var result = await client.DeleteFileAsync("test-id");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task DeleteFileAsync_WhenNotFound_ReturnsTrue()
    {
        // Arrange
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            });

        var client = new UploadServiceClient(_httpClient, _uploadLoggerMock.Object);

        // Act
        var result = await client.DeleteFileAsync("test-id");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task DeleteFileAsync_WhenForbidden_ReturnsFalse()
    {
        // Arrange
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Forbidden
            });

        var client = new UploadServiceClient(_httpClient, _uploadLoggerMock.Object);

        // Act
        var result = await client.DeleteFileAsync("test-id");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DownloadFileAsync_WhenSuccessful_ReturnsResponse()
    {
        // Arrange
        var content = new byte[] { 1, 2, 3 };
        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(content)
        };
        httpResponse.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        httpResponse.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
        {
            FileName = "test.txt"
        };

        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        var client = new UploadServiceClient(_httpClient, _uploadLoggerMock.Object);

        // Act
        var result = await client.DownloadFileAsync("test-id");

        // Assert
        Assert.Equal(content, result.Content);
        Assert.Equal("text/plain", result.ContentType);
        Assert.Equal("test.txt", result.FileName);
    }

    [Fact]
    public async Task DownloadFileAsync_WhenNoFileName_ReturnsDefaultName()
    {
        // Arrange
        var content = new byte[] { 1, 2, 3 };
        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(content)
        };
        httpResponse.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");

        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        var client = new UploadServiceClient(_httpClient, _uploadLoggerMock.Object);

        // Act
        var result = await client.DownloadFileAsync("test-id");

        // Assert
        Assert.Equal("download", result.FileName);
    }

    [Fact]
    public async Task DownloadFileAsync_WhenException_Throws()
    {
        // Arrange
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new Exception("Network error"));

        var client = new UploadServiceClient(_httpClient, _uploadLoggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => client.DownloadFileAsync("test-id"));
    }
}
