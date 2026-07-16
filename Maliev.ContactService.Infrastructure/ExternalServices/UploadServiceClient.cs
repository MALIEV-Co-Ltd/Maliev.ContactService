using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Maliev.ContactService.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Maliev.ContactService.Infrastructure.ExternalServices;

/// <summary>
/// Calls UploadService for file metadata operations and transfers bytes through signed storage URLs.
/// </summary>
public class UploadServiceClient : IUploadServiceClient
{
    /// <summary>
    /// The named client used only for unsigned storage transfers.
    /// </summary>
    public const string StorageHttpClientName = "ContactService.StorageTransfer";

    private const int SignedUrlExpirationMinutes = 5;
    private readonly bool _allowInsecureStorageUrls;
    private readonly HttpClient _httpClient;
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Initializes a new UploadService client.
    /// </summary>
    /// <param name="httpClient">The authenticated UploadService client.</param>
    /// <param name="httpClientFactory">Factory for the isolated storage-transfer client.</param>
    /// <param name="configuration">Application configuration.</param>
    public UploadServiceClient(
        HttpClient httpClient,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _httpClientFactory = httpClientFactory;
        _allowInsecureStorageUrls = configuration.GetValue<bool>(
            "ExternalServices:UploadService:AllowInsecureStorageUrls");
    }

    /// <inheritdoc/>
    public async Task<UploadResponse> UploadFileAsync(
        string objectName,
        byte[] content,
        string contentType,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var initiateRequest = new InitiateResumableUploadRequest(
            Path: objectName,
            FileName: fileName,
            ServiceName: "ContactService",
            ContentType: contentType,
            TotalSize: content.LongLength,
            Overwrite: true);

        using var initiateResponse = await _httpClient.PostAsJsonAsync(
            "/upload/v1/uploads/resumable",
            initiateRequest,
            cancellationToken);
        initiateResponse.EnsureSuccessStatusCode();

        var session = await initiateResponse.Content.ReadFromJsonAsync<InitiateResumableUploadResponse>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Upload service returned an empty resumable session.");
        var sessionUri = ValidateStorageUri(session.SessionUri, "resumable upload session");

        using var uploadContent = new ByteArrayContent(content);
        uploadContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        uploadContent.Headers.ContentLength = content.LongLength;
        uploadContent.Headers.ContentRange = new ContentRangeHeaderValue(
            0,
            content.LongLength - 1,
            content.LongLength);

        var storageClient = _httpClientFactory.CreateClient(StorageHttpClientName);
        using var storageResponse = await storageClient.PutAsync(sessionUri, uploadContent, cancellationToken);
        storageResponse.EnsureSuccessStatusCode();

        var escapedUploadId = Uri.EscapeDataString(session.UploadId);
        using var completeResponse = await _httpClient.PostAsJsonAsync(
            $"/upload/v1/uploads/resumable/{escapedUploadId}/complete",
            new { },
            cancellationToken);
        completeResponse.EnsureSuccessStatusCode();

        var result = await completeResponse.Content.ReadFromJsonAsync<UploadServiceResponse>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Upload service returned an empty completion response.");
        return new UploadResponse(result.UploadId, result.FileSize);
    }

    /// <inheritdoc/>
    public async Task DeleteFileAsync(string fileId, CancellationToken cancellationToken = default)
    {
        var escapedFileId = Uri.EscapeDataString(fileId);
        using var response = await _httpClient.DeleteAsync(
            $"/upload/v1/files/{escapedFileId}",
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <inheritdoc/>
    public async Task<DownloadResponse> DownloadFileAsync(
        string fileId,
        CancellationToken cancellationToken = default)
    {
        var escapedFileId = Uri.EscapeDataString(fileId);
        using var signedUrlResponse = await _httpClient.PostAsJsonAsync(
            $"/upload/v1/files/{escapedFileId}/signed-url",
            new GenerateSignedUrlRequest(SignedUrlExpirationMinutes),
            cancellationToken);
        signedUrlResponse.EnsureSuccessStatusCode();

        SignedUrlResponse signedUrl;
        try
        {
            signedUrl = await signedUrlResponse.Content.ReadFromJsonAsync<SignedUrlResponse>(
                cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException("Upload service returned an empty signed URL response.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Upload service returned a malformed signed URL response.", ex);
        }
        var downloadUri = ValidateStorageUri(signedUrl.SignedUrl, "signed download URL");

        var storageClient = _httpClientFactory.CreateClient(StorageHttpClientName);
        using var downloadResponse = await storageClient.GetAsync(
            downloadUri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        downloadResponse.EnsureSuccessStatusCode();

        var bytes = await downloadResponse.Content.ReadAsByteArrayAsync(cancellationToken);
        var contentType = downloadResponse.Content.Headers.ContentType?.MediaType
            ?? "application/octet-stream";
        var fileName = GetSafeFileName(downloadResponse, signedUrl.StoragePath);

        return new DownloadResponse(bytes, contentType, fileName);
    }

    private string ValidateStorageUri(string? value, string description)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps
                && !(_allowInsecureStorageUrls
                    && uri.Scheme == Uri.UriSchemeHttp
                    && uri.IsLoopback)))
        {
            throw new InvalidOperationException(
                $"Upload service returned an invalid or insecure {description}.");
        }

        return uri.AbsoluteUri;
    }

    private static string GetSafeFileName(HttpResponseMessage response, string? storagePath)
    {
        var headerFileName = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName;
        var candidate = string.IsNullOrWhiteSpace(headerFileName)
            ? Path.GetFileName((storagePath ?? string.Empty).Replace('\\', '/'))
            : headerFileName.Trim('"');
        candidate = Path.GetFileName(candidate.Replace('\\', '/'));

        return string.IsNullOrWhiteSpace(candidate) || IsUnsafeFileName(candidate)
            ? "file"
            : candidate;
    }

    private static bool IsUnsafeFileName(string value) =>
        value.Any(char.IsControl)
        || value.IndexOfAny(['/', '\\', ':', '*', '?', '"', '<', '>', '|']) >= 0;

    private sealed record UploadServiceResponse(string UploadId, long FileSize);

    private sealed record GenerateSignedUrlRequest(int ExpirationMinutes);

    private sealed record SignedUrlResponse(
        string? SignedUrl,
        DateTime ExpiresAt,
        string? UploadId,
        string? StoragePath);

    private sealed record InitiateResumableUploadRequest(
        string Path,
        string FileName,
        string ServiceName,
        string ContentType,
        long TotalSize,
        bool Overwrite);

    private sealed record InitiateResumableUploadResponse(
        string UploadId,
        string SessionUri,
        DateTime ExpiresAt,
        long TotalSize);
}
