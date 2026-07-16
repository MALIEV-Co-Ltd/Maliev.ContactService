using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Maliev.ContactService.Infrastructure.ExternalServices;
using Microsoft.Extensions.Configuration;

namespace Maliev.ContactService.Tests.Services;

public sealed class UploadServiceClientTests
{
    [Fact]
    public async Task UploadFileAsync_ValidFile_UsesCanonicalContractsAndIsolatedStorageClient()
    {
        var serviceCall = 0;
        var serviceHandler = new RecordingHandler(request =>
        {
            serviceCall++;
            if (serviceCall == 1)
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal("/upload/v1/uploads/resumable", request.RequestUri!.PathAndQuery);
                Assert.Equal(
                    "{\"path\":\"contacts/7/report.pdf\",\"fileName\":\"report.pdf\",\"serviceName\":\"contact-service\",\"contentType\":\"application/pdf\",\"totalSize\":3,\"overwrite\":true}",
                    request.Body);
                Assert.Equal("Bearer", request.Authorization?.Scheme);
                return JsonResponse("""
                    {"uploadId":"upload/42","sessionUri":"https://storage.example/upload/session","expiresAt":"2026-07-16T21:00:00Z","totalSize":3}
                    """);
            }

            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/upload/v1/uploads/resumable/upload%2F42/complete", request.RequestUri!.PathAndQuery);
            Assert.Equal("{}", request.Body);
            Assert.Equal("Bearer", request.Authorization?.Scheme);
            return JsonResponse("{" + "\"uploadId\":\"upload/42\",\"fileSize\":3}");
        });
        var storageHandler = new RecordingHandler(request =>
        {
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.Equal("https://storage.example/upload/session", request.RequestUri!.AbsoluteUri);
            Assert.Equal("abc", request.Body);
            Assert.Equal("application/pdf", request.ContentType);
            Assert.Equal("bytes 0-2/3", request.ContentRange);
            Assert.Null(request.Authorization);
            Assert.Empty(request.Cookies);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var client = CreateClient(serviceHandler, storageHandler);

        var result = await client.UploadFileAsync(
            "contacts/7/report.pdf",
            Encoding.UTF8.GetBytes("abc"),
            "application/pdf",
            "report.pdf",
            CancellationToken.None);

        Assert.Equal("upload/42", result.FileId);
        Assert.Equal(3, result.FileSize);
        Assert.Equal(2, serviceHandler.Requests.Count);
        Assert.Single(storageHandler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task UploadFileAsync_InitiateFails_ThrowsWithoutStorageOrCompletion(HttpStatusCode statusCode)
    {
        var serviceHandler = new RecordingHandler(_ => new HttpResponseMessage(statusCode));
        var storageHandler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(serviceHandler, storageHandler);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => UploadAsync(client));

        Assert.Equal(statusCode, exception.StatusCode);
        Assert.Single(serviceHandler.Requests);
        Assert.Empty(storageHandler.Requests);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-json")]
    [InlineData("null")]
    [InlineData("{}")]
    public async Task UploadFileAsync_InitiateMalformedOrEmpty_ThrowsStableContractFailure(string json)
    {
        var serviceHandler = new RecordingHandler(_ => JsonResponse(json));
        var storageHandler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(serviceHandler, storageHandler);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => UploadAsync(client));

        if (!string.IsNullOrEmpty(json))
        {
            Assert.DoesNotContain(json, exception.Message, StringComparison.Ordinal);
        }
        Assert.Null(exception.InnerException);
        Assert.Empty(storageHandler.Requests);
    }

    [Theory]
    [InlineData("not-a-url", false)]
    [InlineData("file:///secret", false)]
    [InlineData("https://user:password@storage.example/session", false)]
    [InlineData("http://storage.example/session", true)]
    [InlineData("http://127.0.0.1:4443/session", false)]
    public async Task UploadFileAsync_UnsafeSessionUri_EnforcesExactPolicy(
        string sessionUri,
        bool allowInsecureStorageUrls)
    {
        var serviceHandler = new RecordingHandler(_ => JsonResponse($$"""
            {"uploadId":"upload-42","sessionUri":"{{sessionUri}}","expiresAt":"2026-07-16T21:00:00Z","totalSize":3}
            """));
        var storageHandler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(serviceHandler, storageHandler, allowInsecureStorageUrls);

        await Assert.ThrowsAsync<InvalidOperationException>(() => UploadAsync(client));

        Assert.Empty(storageHandler.Requests);
    }

    [Fact]
    public async Task UploadFileAsync_ConfiguredLoopbackHttpSession_AllowsDevelopmentOverride()
    {
        var serviceCall = 0;
        var serviceHandler = new RecordingHandler(_ => ++serviceCall == 1
            ? JsonResponse("""
                {"uploadId":"upload-42","sessionUri":"http://127.0.0.1:4443/session","expiresAt":"2026-07-16T21:00:00Z","totalSize":3}
                """)
            : JsonResponse("{\"uploadId\":\"upload-42\",\"fileSize\":3}"));
        var storageHandler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(serviceHandler, storageHandler, allowInsecureStorageUrls: true);

        var result = await UploadAsync(client);

        Assert.Equal("upload-42", result.FileId);
        Assert.Single(storageHandler.Requests);
    }

    [Fact]
    public async Task UploadFileAsync_StoragePutFails_DoesNotComplete()
    {
        var serviceHandler = new RecordingHandler(_ => JsonResponse("""
            {"uploadId":"upload-42","sessionUri":"https://storage.example/session","expiresAt":"2026-07-16T21:00:00Z","totalSize":3}
            """));
        var storageHandler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway));
        var client = CreateClient(serviceHandler, storageHandler);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => UploadAsync(client));

        Assert.Equal(HttpStatusCode.BadGateway, exception.StatusCode);
        Assert.Single(serviceHandler.Requests);
        Assert.Single(storageHandler.Requests);
    }

    [Fact]
    public async Task UploadFileAsync_CompleteFails_ThrowsStableHttpRequestException()
    {
        var serviceCall = 0;
        var serviceHandler = new RecordingHandler(_ => ++serviceCall == 1
            ? JsonResponse("""
                {"uploadId":"upload-42","sessionUri":"https://storage.example/session","expiresAt":"2026-07-16T21:00:00Z","totalSize":3}
                """)
            : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var client = CreateClient(serviceHandler, new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => UploadAsync(client));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
        Assert.Equal(2, serviceHandler.Requests.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-json")]
    [InlineData("null")]
    [InlineData("{}")]
    public async Task UploadFileAsync_CompleteMalformedOrEmpty_ThrowsStableContractFailure(string json)
    {
        var serviceCall = 0;
        var serviceHandler = new RecordingHandler(_ => ++serviceCall == 1
            ? JsonResponse("""
                {"uploadId":"upload-42","sessionUri":"https://storage.example/session","expiresAt":"2026-07-16T21:00:00Z","totalSize":3}
                """)
            : JsonResponse(json));
        var client = CreateClient(serviceHandler, new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => UploadAsync(client));

        if (!string.IsNullOrEmpty(json))
        {
            Assert.DoesNotContain(json, exception.Message, StringComparison.Ordinal);
        }
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public async Task UploadFileAsync_CancelledRequest_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var serviceHandler = new RecordingHandler(_ => throw new InvalidOperationException("Handler should not execute."));
        var client = CreateClient(serviceHandler, new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => UploadAsync(client, cancellation.Token));
    }

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
        serviceClient.DefaultRequestHeaders.Add("Cookie", "contact-session=secret");

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

    private static Task<Maliev.ContactService.Application.Interfaces.UploadResponse> UploadAsync(
        UploadServiceClient client,
        CancellationToken cancellationToken = default) =>
        client.UploadFileAsync(
            "contacts/file.txt",
            Encoding.UTF8.GetBytes("abc"),
            "text/plain",
            "file.txt",
            cancellationToken);

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
                request.Headers.TryGetValues("Cookie", out var cookieValues) ? cookieValues.ToArray() : [],
                request.Content?.Headers.ContentType?.MediaType,
                request.Content?.Headers.ContentRange?.ToString(),
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
        IReadOnlyList<string> Cookies,
        string? ContentType,
        string? ContentRange,
        CancellationToken CancellationToken);
}
