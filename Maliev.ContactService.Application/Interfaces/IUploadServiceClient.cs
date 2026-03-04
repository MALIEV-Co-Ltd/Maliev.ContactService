namespace Maliev.ContactService.Application.Interfaces;

public interface IUploadServiceClient
{
    Task<UploadResponse> UploadFileAsync(string objectName, byte[] content, string contentType, string fileName);
    Task DeleteFileAsync(string fileId);
    Task<DownloadResponse> DownloadFileAsync(string fileId);
}

public record UploadResponse(string FileId, long FileSize);
public record DownloadResponse(byte[] Content, string ContentType, string FileName);
