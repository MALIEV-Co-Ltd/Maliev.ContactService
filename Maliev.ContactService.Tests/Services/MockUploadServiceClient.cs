using Maliev.ContactService.Api.Services;

namespace Maliev.ContactService.Tests.Services;

public class MockUploadServiceClient : IUploadServiceClient
{
    public Task<UploadResponse> UploadFileAsync(string objectName, byte[] fileContent, string contentType, string fileName)
    {
        return Task.FromResult(new UploadResponse
        {
            FileId = "dummy-file-id",
            ObjectName = objectName,
            Bucket = "test-bucket",
            FileSize = fileContent.Length,
            UploadedAt = DateTime.UtcNow
        });
    }

    public Task<bool> DeleteFileAsync(string uploadServiceFileId)
    {
        return Task.FromResult(true);
    }

    public Task<FileDownloadResponse> DownloadFileAsync(string uploadServiceFileId)
    {
        var stream = new MemoryStream();
        var writer = new StreamWriter(stream);
        writer.Write("This is a test file.");
        writer.Flush();
        stream.Position = 0;

        return Task.FromResult(new FileDownloadResponse
        {
            Content = stream.ToArray(),
            ContentType = "text/plain",
            FileName = "test.txt",
            FileSize = stream.Length
        });
    }
}
