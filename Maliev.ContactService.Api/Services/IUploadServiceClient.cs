namespace Maliev.ContactService.Api.Services;
/// <summary>
/// Client interface for UploadService service
/// </summary>

public interface IUploadServiceClient
{
    /// <summary>
    /// Uploads a file to the upload service.
    /// </summary>
    /// <param name="objectName">The object name for the file in storage.</param>
    /// <param name="fileContent">The binary content of the file.</param>
    /// <param name="contentType">The MIME content type of the file.</param>
    /// <param name="fileName">The original file name.</param>
    /// <returns>The upload response containing file metadata.</returns>
    Task<UploadResponse> UploadFileAsync(string objectName, byte[] fileContent, string contentType, string fileName);

    /// <summary>
    /// Deletes a file from the upload service.
    /// </summary>
    /// <param name="uploadServiceFileId">The file identifier in the upload service.</param>
    /// <returns>True if the file was deleted successfully, false otherwise.</returns>
    Task<bool> DeleteFileAsync(string uploadServiceFileId);

    /// <summary>
    /// Downloads a file from the upload service.
    /// </summary>
    /// <param name="uploadServiceFileId">The file identifier in the upload service.</param>
    /// <returns>The file download response containing file content and metadata.</returns>
    Task<FileDownloadResponse> DownloadFileAsync(string uploadServiceFileId);
}
/// <summary>
/// Request model for upload
/// </summary>

public class UploadRequest
{
    /// <summary>
    /// Gets or sets the object name for the file in storage.
    /// </summary>
    public required string ObjectName { get; set; }

    /// <summary>
    /// Gets or sets the binary content of the file.
    /// </summary>
    public required byte[] FileContent { get; set; }

    /// <summary>
    /// Gets or sets the MIME content type of the file.
    /// </summary>
    public required string ContentType { get; set; }

    /// <summary>
    /// Gets or sets the original file name.
    /// </summary>
    public required string FileName { get; set; }
}
/// <summary>
/// Response model for upload
/// </summary>

public class UploadResponse
{
    /// <summary>
    /// Gets or sets the unique file identifier from the upload service.
    /// </summary>
    public required string FileId { get; set; }

    /// <summary>
    /// Gets or sets the object name in storage.
    /// </summary>
    public required string ObjectName { get; set; }

    /// <summary>
    /// Gets or sets the storage bucket name.
    /// </summary>
    public required string Bucket { get; set; }

    /// <summary>
    /// Gets or sets the size of the file in bytes.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the file was uploaded.
    /// </summary>
    public DateTime UploadedAt { get; set; }
}
/// <summary>
/// Response model for filedownload
/// </summary>

public class FileDownloadResponse
{
    /// <summary>
    /// Gets or sets the binary content of the file.
    /// </summary>
    public required byte[] Content { get; set; }

    /// <summary>
    /// Gets or sets the MIME content type of the file.
    /// </summary>
    public required string ContentType { get; set; }

    /// <summary>
    /// Gets or sets the file name.
    /// </summary>
    public required string FileName { get; set; }

    /// <summary>
    /// Gets or sets the size of the file in bytes.
    /// </summary>
    public long FileSize { get; set; }
}
