namespace Maliev.ContactService.Application.Interfaces;

public interface IUploadServiceClient
{
    Task<UploadResponse> UploadFileAsync(
        string objectName,
        byte[] content,
        string contentType,
        string fileName,
        CancellationToken cancellationToken = default);

    Task DeleteFileAsync(string fileId, CancellationToken cancellationToken = default);

    Task<DownloadResponse> DownloadFileAsync(string fileId, CancellationToken cancellationToken = default);
}

public record UploadResponse(string FileId, long FileSize);
public record DownloadResponse(byte[] Content, string ContentType, string FileName);
