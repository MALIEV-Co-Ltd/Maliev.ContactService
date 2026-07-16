using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Maliev.ContactService.Infrastructure.ExternalServices;
using Microsoft.Extensions.Configuration;

namespace Maliev.ContactService.Tests.Services;

public sealed class UploadServiceClientTests
{
    [Fact]
    public async Task DeleteFileAsync_ReservedUploadId_UsesEscapedCurrentRouteAndPropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var serviceHandler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = CreateClient(serviceHandler, new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));

        await client.DeleteFileAsync("folder/id with space", cancellation.Token);

        var request = Assert.Single(serviceHandler.Requests);
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal("/upload/v1/files/folder%2Fid%20with%20space", request.RequestUri!.PathAndQuery);
        Assert.True(request.CancellationToken.CanBeCanceled);
    }

    [Fact]
    public async Task DownloadFileAsync_ValidSignedUrl_PostsBoundedRequestThenDownloadsWithoutAuthorization()
    {
        var serviceHandler = new RecordingHandler(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/upload/v1/files/file%2F42/signed-url", request.RequestUri!.PathAndQuery);
            Assert.Equal("{\"expirationMinutes\":5}", request.Body);
            Assert.Equal("Bearer", request.Authorization?.Scheme);
            return JsonResponse("""
                {"signedUrl":"https://storage.example/files/file-42","expiresAt":"2026-07-16T01:00:00Z","uploadId":"file/42","storagePath":"contacts/7/report.pdf"}
                """);
        });
        var storageHandler = new RecordingHandler(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("https://storage.example/files/file-42", request.RequestUri!.AbsoluteUri);
            Assert.Null(request.Authorization);
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([1, 2, 3])
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            return response;
        });
        var client = CreateClient(serviceHandler, storageHandler);

        var result = await client.DownloadFileAsync("file/42", CancellationToken.None);

        Assert.Equal([1, 2, 3], result.Content);
        Assert.Equal("application/pdf", result.ContentType);
        Assert.Equal("report.pdf", result.FileName);
        Assert.Single(serviceHandler.Requests);
        Assert.Single(storageHandler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task DownloadFileAsync_SignedUrlRequestFails_ThrowsWithoutCallingStorage(HttpStatusCode statusCode)
    {
        var serviceHandler = new RecordingHandler(_ => new HttpResponseMessage(statusCode));
        var storageHandler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(serviceHandler, storageHandler);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.DownloadFileAsync("file-42", CancellationToken.None));

        Assert.Equal(statusCode, exception.StatusCode);
        Assert.Empty(storageHandler.Requests);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{}")]
    [InlineData("{\"signedUrl\":\"not-a-url\",\"storagePath\":\"contacts/file.txt\"}")]
    [InlineData("{\"signedUrl\":\"http://storage.example/file\",\"storagePath\":\"contacts/file.txt\"}")]
    [InlineData("{\"signedUrl\":\"file:///secrets.txt\",\"storagePath\":\"contacts/file.txt\"}")]
    public async Task DownloadFileAsync_MalformedOrUnsafeSignedUrl_RejectsWithoutCallingStorage(string json)
    {
        var serviceHandler = new RecordingHandler(_ => JsonResponse(json));
        var storageHandler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(serviceHandler, storageHandler);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.DownloadFileAsync("file-42", CancellationToken.None));

        Assert.Empty(storageHandler.Requests);
    }

    [Fact]
    public async Task DownloadFileAsync_StorageFailure_ThrowsStableHttpRequestException()
    {
        var serviceHandler = new RecordingHandler(_ => JsonResponse("""
            {"signedUrl":"https://storage.example/file","expiresAt":"2026-07-16T01:00:00Z","uploadId":"file-42","storagePath":"contacts/file.txt"}
            """));
        var storageHandler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway));
        var client = CreateClient(serviceHandler, storageHandler);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.DownloadFileAsync("file-42", CancellationToken.None));

        Assert.Equal(HttpStatusCode.BadGateway, exception.StatusCode);
    }

    [Fact]
    public async Task DownloadFileAsync_UnsafeContentDisposition_UsesSafeFallbackName()
    {
        var serviceHandler = new RecordingHandler(_ => JsonResponse("""
            {"signedUrl":"https://storage.example/file","expiresAt":"2026-07-16T01:00:00Z","uploadId":"file-42","storagePath":"contacts/safe-name.txt"}
            """));
        var storageHandler = new RecordingHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([1])
            };
            response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
            {
                FileName = "../unsafe:name.txt"
            };
            return response;
        });
        var client = CreateClient(serviceHandler, storageHandler);

        var result = await client.DownloadFileAsync("file-42", CancellationToken.None);

        Assert.Equal("file", result.FileName);
        Assert.Equal("application/octet-stream", result.ContentType);
    }

    [Fact]
    public async Task DownloadFileAsync_ConfiguredLocalHttpUrl_AllowsExplicitDevelopmentOverride()
    {
        var serviceHandler = new RecordingHandler(_ => JsonResponse("""
            {"signedUrl":"http://127.0.0.1:4443/file","expiresAt":"2026-07-16T01:00:00Z","uploadId":"file-42","storagePath":"contacts/file.txt"}
            """));
        var storageHandler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([1])
        });
        var client = CreateClient(serviceHandler, storageHandler, allowInsecureStorageUrls: true);

        var result = await client.DownloadFileAsync("file-42", CancellationToken.None);

        Assert.Equal([1], result.Content);
    }

    [Fact]
    public async Task DownloadFileAsync_CancelledRequest_PropagatesCancellationToken()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var serviceHandler = new RecordingHandler(_ => throw new InvalidOperationException("Handler should not execute."));
        var client = CreateClient(serviceHandler, new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.DownloadFileAsync("file-42", cancellation.Token));
    }

    private static UploadServiceClient CreateClient(
        RecordingHandler serviceHandler,
        RecordingHandler storageHandler,
        bool allowInsecureStorageUrls = false)
    {
        var serviceClient = new HttpClient(serviceHandler)
        {
            BaseAddress = new Uri("https://upload.example")
        };
        serviceClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "service-token");

        var storageClient = new HttpClient(storageHandler);
        var factory = new StubHttpClientFactory(storageClient);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ExternalServices:UploadService:AllowInsecureStorageUrls"] = allowInsecureStorageUrls.ToString()
            })
            .Build();

        return new UploadServiceClient(serviceClient, factory, configuration);
    }

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            Assert.Equal(UploadServiceClient.StorageHttpClientName, name);
            return client;
        }
    }

    private sealed class RecordingHandler(Func<RecordedRequest, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<HttpResponseMessage>(cancellationToken);
            }

            var recorded = new RecordedRequest(
                request.Method,
                request.RequestUri,
                request.Content?.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult(),
                request.Headers.Authorization,
                cancellationToken);
            Requests.Add(recorded);
            return Task.FromResult(responder(recorded));
        }
    }

    private sealed record RecordedRequest(
        HttpMethod Method,
        Uri? RequestUri,
        string? Body,
        AuthenticationHeaderValue? Authorization,
        CancellationToken CancellationToken);
}
