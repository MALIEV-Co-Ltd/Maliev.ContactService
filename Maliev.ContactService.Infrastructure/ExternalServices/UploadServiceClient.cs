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
        var multipartContent = new MultipartFormDataContent();
        multipartContent.Add(new ByteArrayContent(content), "file", fileName);
        multipartContent.Add(new StringContent(objectName), "objectName");
        multipartContent.Add(new StringContent(contentType), "contentType");

        var response = await _httpClient.PostAsync("/v1/files/upload", multipartContent);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<UploadServiceResponse>();
        return new UploadResponse(result!.FileId, result.FileSize);
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

    private record UploadServiceResponse(string FileId, long FileSize);
}
