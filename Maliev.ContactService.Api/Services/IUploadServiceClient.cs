namespace Maliev.ContactService.Api.Services;

public interface IUploadServiceClient
{
    Task<UploadResponse> UploadFileAsync(string objectName, byte[] fileContent, string contentType, string fileName);
    Task<bool> DeleteFileAsync(string uploadServiceFileId);
    Task<FileDownloadResponse> DownloadFileAsync(string uploadServiceFileId);
}

public class UploadRequest
{
    public required string ObjectName { get; set; }
    public required byte[] FileContent { get; set; }
    public required string ContentType { get; set; }
    public required string FileName { get; set; }
}

public class UploadResponse
{
    public required string FileId { get; set; }
    public required string ObjectName { get; set; }
    public required string Bucket { get; set; }
    public long FileSize { get; set; }
    public DateTime UploadedAt { get; set; }
}

public class FileDownloadResponse
{
    public required byte[] Content { get; set; }
    public required string ContentType { get; set; }
    public required string FileName { get; set; }
    public long FileSize { get; set; }
}