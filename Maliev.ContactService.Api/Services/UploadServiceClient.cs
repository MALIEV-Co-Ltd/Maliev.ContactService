using System.Text;
using System.Text.Json;

namespace Maliev.ContactService.Api.Services;

public class UploadServiceClient : IUploadServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UploadServiceClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public UploadServiceClient(HttpClient httpClient, ILogger<UploadServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<UploadResponse> UploadFileAsync(string objectName, byte[] fileContent, string contentType, string fileName)
    {
        try
        {
            using var content = new MultipartFormDataContent();

            // Add file content
            var fileContentPart = new ByteArrayContent(fileContent);
            fileContentPart.Headers.Add("Content-Type", contentType);
            content.Add(fileContentPart, "file", fileName);

            // Add metadata
            content.Add(new StringContent(objectName), "objectName");
            content.Add(new StringContent(contentType), "contentType");

            var response = await _httpClient.PostAsync("/api/v1/upload", content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var uploadResponse = JsonSerializer.Deserialize<UploadResponse>(responseJson, _jsonOptions);

            if (uploadResponse == null)
                throw new InvalidOperationException("Invalid response from upload service");

            _logger.LogInformation("File uploaded successfully: {ObjectName} -> {FileId}", objectName, uploadResponse.FileId);
            return uploadResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload file: {ObjectName}", objectName);
            throw;
        }
    }

    public async Task<bool> DeleteFileAsync(string uploadServiceFileId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"/api/v1/files/{uploadServiceFileId}");

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("File deleted successfully: {FileId}", uploadServiceFileId);
                return true;
            }

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("File not found for deletion: {FileId}", uploadServiceFileId);
                return true; // Consider not found as successfully deleted
            }

            _logger.LogWarning("Failed to delete file {FileId}: {StatusCode}", uploadServiceFileId, response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file: {FileId}", uploadServiceFileId);
            return false;
        }
    }

    public async Task<FileDownloadResponse> DownloadFileAsync(string uploadServiceFileId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/v1/files/{uploadServiceFileId}/download");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsByteArrayAsync();
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
            var fileName = ExtractFileNameFromHeaders(response) ?? "download";

            _logger.LogInformation("File downloaded successfully: {FileId}", uploadServiceFileId);

            return new FileDownloadResponse
            {
                Content = content,
                ContentType = contentType,
                FileName = fileName,
                FileSize = content.Length
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download file: {FileId}", uploadServiceFileId);
            throw;
        }
    }

    private static string? ExtractFileNameFromHeaders(HttpResponseMessage response)
    {
        if (response.Content.Headers.ContentDisposition?.FileName != null)
        {
            return response.Content.Headers.ContentDisposition.FileName.Trim('"');
        }

        return null;
    }
}