using System.Net.Http.Json;
using Maliev.ContactService.Application.Interfaces;

namespace Maliev.ContactService.Infrastructure.ExternalServices;

public class UploadServiceClient : IUploadServiceClient
{
    private readonly HttpClient _httpClient;

    public UploadServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<UploadResponse> UploadFileAsync(string objectName, byte[] content, string contentType, string fileName)
    {
        var initiateRequest = new InitiateResumableUploadRequest(
            Path: objectName,
            FileName: fileName,
            ServiceName: "ContactService",
            ContentType: contentType,
            TotalSize: content.LongLength,
            Overwrite: true);

        var initiateResponse = await _httpClient.PostAsJsonAsync("/upload/v1/uploads/resumable", initiateRequest);
        initiateResponse.EnsureSuccessStatusCode();

        var session = await initiateResponse.Content.ReadFromJsonAsync<InitiateResumableUploadResponse>()
            ?? throw new InvalidOperationException("Upload service returned null resumable session");

        using var gcsClient = new HttpClient();
        using var uploadContent = new ByteArrayContent(content);
        uploadContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        uploadContent.Headers.ContentLength = content.LongLength;
        uploadContent.Headers.ContentRange = new System.Net.Http.Headers.ContentRangeHeaderValue(0, content.LongLength - 1, content.LongLength);

        var gcsResponse = await gcsClient.PutAsync(session.SessionUri, uploadContent);
        gcsResponse.EnsureSuccessStatusCode();

        var response = await _httpClient.PostAsJsonAsync($"/upload/v1/uploads/resumable/{session.UploadId}/complete", new { });
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<UploadServiceResponse>();
        return new UploadResponse(result!.UploadId, result.FileSize);
    }

    public async Task DeleteFileAsync(string fileId)
    {
        var response = await _httpClient.DeleteAsync($"/v1/files/{fileId}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<DownloadResponse> DownloadFileAsync(string fileId)
    {
        var response = await _httpClient.GetAsync($"/v1/files/{fileId}/download");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsByteArrayAsync();
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        var fileName = response.Content.Headers.ContentDisposition?.FileName ?? "file";

        return new DownloadResponse(content, contentType, fileName);
    }

    private record UploadServiceResponse(string UploadId, long FileSize);

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
