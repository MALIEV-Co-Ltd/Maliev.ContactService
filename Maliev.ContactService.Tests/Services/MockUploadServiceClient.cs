using Maliev.ContactService.Application.Interfaces;

namespace Maliev.ContactService.Tests.Services;

public class MockUploadServiceClient : IUploadServiceClient
{
    public static bool ThrowOnUpload { get; set; }

    public Task<UploadResponse> UploadFileAsync(string objectName, byte[] content, string contentType, string fileName)
    {
        if (ThrowOnUpload)
        {
            throw new InvalidOperationException("Mock upload failure.");
        }

        return Task.FromResult(new UploadResponse("mock-file-id", content.Length));
    }

    public Task DeleteFileAsync(string fileId)
    {
        return Task.CompletedTask;
    }

    public Task<DownloadResponse> DownloadFileAsync(string fileId)
    {
        var content = System.Text.Encoding.UTF8.GetBytes("Mock file content");
        return Task.FromResult(new DownloadResponse(content, "text/plain", "mock.txt"));
    }
}
