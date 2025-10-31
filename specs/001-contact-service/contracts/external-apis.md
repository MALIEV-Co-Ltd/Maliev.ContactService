# External API Contracts: Country Service & Upload Service

**Feature**: Contact Submission Service
**Branch**: `001-contact-service`
**Date**: 2025-10-29

## Overview

This document defines the external API contracts that the Contact Service depends on. The Contact Service integrates with two external microservices:

1. **Country Service** - Validates country IDs during contact submission
2. **Upload Service** - Handles file uploads to Google Cloud Storage

---

## Country Service Integration

### Base URL

**Environment Configuration**:
```json
{
  "CountryService": {
    "BaseUrl": "http://maliev-country-service.maliev-dev.svc.cluster.local/api",
    "TimeoutSeconds": 10
  }
}
```

| Environment | Base URL |
|-------------|----------|
| Development | `http://maliev-country-service.maliev-dev.svc.cluster.local/api` |
| Staging | `http://maliev-country-service.maliev-staging.svc.cluster.local/api` |
| Production | `http://maliev-country-service.maliev-prod.svc.cluster.local/api` |

---

### GET /countries/v1/{id}

Validate that a country ID exists and retrieve country details.

**Purpose**: Validate `countryId` during contact submission (FR-007)

**Authentication**: **[NEEDS CLARIFICATION]** - Likely internal service-to-service or none

**Request Example**:
```bash
curl -X GET "http://maliev-country-service/api/countries/v1/1"
```

**Response (200 OK)** - Country exists:
```json
{
  "id": 1,
  "name": "Thailand",
  "code": "TH",
  "isActive": true
}
```

**Expected Schema** (minimal):
```typescript
interface CountryDto {
  id: number;
  name: string;
  code: string;        // ISO 3166-1 alpha-2 code
  isActive: boolean;   // Whether country accepts submissions
}
```

**Response (404 Not Found)** - Country does not exist:
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Not Found",
  "status": 404,
  "detail": "Country with ID 999 does not exist."
}
```

**Response (503 Service Unavailable)** - Service down:
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.6.4",
  "title": "Service Unavailable",
  "status": 503,
  "detail": "Country Service is temporarily unavailable."
}
```

---

### Integration Requirements

**Implementation Pattern**:
```csharp
public interface ICountryServiceClient
{
    /// <summary>
    /// Validates that a country exists in the Country Service
    /// </summary>
    /// <param name="countryId">Country ID to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if country exists and is active, false otherwise</returns>
    Task<bool> ValidateCountryExistsAsync(int countryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves country details from Country Service
    /// </summary>
    /// <param name="countryId">Country ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Country DTO or null if not found</returns>
    Task<CountryDto?> GetCountryByIdAsync(int countryId, CancellationToken cancellationToken = default);
}
```

**HTTP Client Configuration**:
```csharp
// In Program.cs
builder.Services.AddHttpClient<ICountryServiceClient, CountryServiceClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<CountryServiceOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
})
.AddPolicyHandler(GetRetryPolicy())
.AddPolicyHandler(GetCircuitBreakerPolicy());

// Polly Retry Policy
static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.NotFound)
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (outcome, timespan, retryCount, context) =>
            {
                Log.Warning("Country Service retry {RetryCount} after {Delay}s due to {Result}",
                    retryCount, timespan.TotalSeconds, outcome.Result?.StatusCode);
            });
}

// Polly Circuit Breaker Policy
static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .CircuitBreakerAsync(
            handledEventsAllowedBeforeBreaking: 5,
            durationOfBreak: TimeSpan.FromSeconds(30),
            onBreak: (outcome, duration) =>
            {
                Log.Error("Country Service circuit breaker opened for {Duration}s", duration.TotalSeconds);
            },
            onReset: () =>
            {
                Log.Information("Country Service circuit breaker reset");
            });
}
```

**Error Handling Strategy**:

| Scenario | HTTP Status | Contact Service Behavior |
|----------|-------------|--------------------------|
| Country exists | 200 OK | Accept submission |
| Country not found | 404 Not Found | Reject submission with validation error |
| Service unavailable | 503 / Timeout | Reject submission with 503 error (per FR-024) |
| Transient error | 500, 502, 504 | Retry 3 times with exponential backoff, then 503 |
| Circuit breaker open | - | Immediately return 503 without calling service |

**Contact Service Response to User** (when Country Service unavailable):
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.6.4",
  "title": "Service Unavailable",
  "status": 503,
  "detail": "Unable to validate country information. Please try again in a few moments."
}
```

---

### Testing Strategy

**Unit Tests**:
```csharp
[Fact]
public async Task ValidateCountryExistsAsync_ValidCountry_ReturnsTrue()
{
    // Mock HTTP response
    var mockHttp = new MockHttpMessageHandler();
    mockHttp.When("http://*/countries/v1/1")
        .Respond("application/json", "{\"id\":1,\"name\":\"Thailand\",\"isActive\":true}");

    var client = new CountryServiceClient(mockHttp.ToHttpClient());
    var result = await client.ValidateCountryExistsAsync(1);

    result.Should().BeTrue();
}

[Fact]
public async Task ValidateCountryExistsAsync_InvalidCountry_ReturnsFalse()
{
    var mockHttp = new MockHttpMessageHandler();
    mockHttp.When("http://*/countries/v1/999")
        .Respond(HttpStatusCode.NotFound);

    var client = new CountryServiceClient(mockHttp.ToHttpClient());
    var result = await client.ValidateCountryExistsAsync(999);

    result.Should().BeFalse();
}

[Fact]
public async Task ValidateCountryExistsAsync_ServiceUnavailable_ThrowsException()
{
    var mockHttp = new MockHttpMessageHandler();
    mockHttp.When("http://*/countries/v1/1")
        .Respond(HttpStatusCode.ServiceUnavailable);

    var client = new CountryServiceClient(mockHttp.ToHttpClient());

    await Assert.ThrowsAsync<CountryServiceException>(
        () => client.ValidateCountryExistsAsync(1));
}
```

**Integration Tests** (with Testcontainers or WireMock):
- Country Service returns 200 for valid ID
- Country Service returns 404 for invalid ID
- Country Service timeout after 10 seconds
- Circuit breaker opens after 5 consecutive failures
- Circuit breaker resets after 30 seconds

---

## Upload Service Integration

### Base URL

**Environment Configuration**:
```json
{
  "UploadService": {
    "BaseUrl": "http://maliev-upload-service.maliev-dev.svc.cluster.local/api",
    "TimeoutSeconds": 60
  }
}
```

| Environment | Base URL |
|-------------|----------|
| Development | `http://maliev-upload-service.maliev-dev.svc.cluster.local/api` |
| Staging | `http://maliev-upload-service.maliev-staging.svc.cluster.local/api` |
| Production | `http://maliev-upload-service.maliev-prod.svc.cluster.local/api` |

---

### POST /uploads/v1

Upload a file to Google Cloud Storage.

**Purpose**: Upload contact submission files during form submission (FR-016)

**Authentication**: **[NEEDS CLARIFICATION]** - Likely internal service-to-service JWT

**Content-Type**: `multipart/form-data`

**Request Example**:
```bash
curl -X POST "http://maliev-upload-service/api/uploads/v1" \
  -F "file=@invoice.pdf" \
  -F "bucketName=contact-submissions" \
  -F "folder=2025/10"
```

**Request Body**:
```
POST /uploads/v1 HTTP/1.1
Content-Type: multipart/form-data; boundary=----WebKitFormBoundary7MA4YWxkTrZu0gW

------WebKitFormBoundary7MA4YWxkTrZu0gW
Content-Disposition: form-data; name="file"; filename="invoice.pdf"
Content-Type: application/pdf

[binary file content]
------WebKitFormBoundary7MA4YWxkTrZu0gW
Content-Disposition: form-data; name="bucketName"

contact-submissions
------WebKitFormBoundary7MA4YWxkTrZu0gW
Content-Disposition: form-data; name="folder"

2025/10
------WebKitFormBoundary7MA4YWxkTrZu0gW--
```

**Response (200 OK)**:
```json
{
  "fileId": "file_abc123xyz",
  "objectName": "contact-submissions/2025/10/abc123xyz_invoice.pdf",
  "fileName": "invoice.pdf",
  "contentType": "application/pdf",
  "fileSize": 524288,
  "uploadedAt": "2025-10-29T10:30:00Z",
  "publicUrl": null
}
```

**Expected Schema**:
```typescript
interface UploadResponseDto {
  fileId: string;          // Unique identifier in Upload Service
  objectName: string;      // GCS object path
  fileName: string;        // Original filename
  contentType: string;     // MIME type
  fileSize: number;        // Size in bytes
  uploadedAt: string;      // ISO 8601 timestamp
  publicUrl?: string;      // Public URL (if bucket is public)
}
```

**Response (400 Bad Request)** - Invalid file or size limit:
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "File size exceeds maximum allowed size of 25MB."
}
```

**Response (503 Service Unavailable)** - Service down or GCS unavailable:
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.6.4",
  "title": "Service Unavailable",
  "status": 503,
  "detail": "Upload Service is temporarily unavailable."
}
```

---

### GET /uploads/v1/{fileId}

Retrieve file metadata by file ID.

**Purpose**: Verify file exists before downloading (optional)

**Authentication**: Internal service-to-service

**Request Example**:
```bash
curl -X GET "http://maliev-upload-service/api/uploads/v1/file_abc123xyz"
```

**Response (200 OK)**:
```json
{
  "fileId": "file_abc123xyz",
  "objectName": "contact-submissions/2025/10/abc123xyz_invoice.pdf",
  "fileName": "invoice.pdf",
  "contentType": "application/pdf",
  "fileSize": 524288,
  "uploadedAt": "2025-10-29T10:30:00Z"
}
```

**Response (404 Not Found)**:
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Not Found",
  "status": 404,
  "detail": "File with ID 'file_invalid' does not exist."
}
```

---

### GET /uploads/v1/{fileId}/download

Download file binary content.

**Purpose**: Admin file download proxy (admin-api.md endpoint 6)

**Authentication**: Admin JWT

**Request Example**:
```bash
curl -X GET "http://maliev-upload-service/api/uploads/v1/file_abc123xyz/download" \
  -H "Authorization: Bearer <ADMIN_JWT>" \
  -o downloaded_file.pdf
```

**Response (200 OK)**:
```
HTTP/1.1 200 OK
Content-Type: application/pdf
Content-Disposition: attachment; filename="invoice.pdf"
Content-Length: 524288

[binary file content]
```

**Response (404 Not Found)** - File not found:
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Not Found",
  "status": 404,
  "detail": "File with ID 'file_invalid' does not exist."
}
```

**Response (410 Gone)** - File was deleted from GCS:
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.11",
  "title": "Gone",
  "status": 410,
  "detail": "File was deleted and is no longer available."
}
```

---

### DELETE /uploads/v1/{fileId}

Delete file from GCS.

**Purpose**: Cleanup when contact inquiry is deleted (admin-api.md endpoint 4, 7)

**Authentication**: Admin JWT or internal service-to-service

**Request Example**:
```bash
curl -X DELETE "http://maliev-upload-service/api/uploads/v1/file_abc123xyz" \
  -H "Authorization: Bearer <ADMIN_JWT>"
```

**Response (204 No Content)**:
No response body.

**Response (404 Not Found)**:
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Not Found",
  "status": 404,
  "detail": "File with ID 'file_invalid' does not exist."
}
```

---

### Integration Requirements

**Implementation Pattern** (already exists in codebase):
```csharp
public interface IUploadServiceClient
{
    Task<UploadResponseDto> UploadFileAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        string bucketName,
        string? folder = null,
        CancellationToken cancellationToken = default);

    Task<FileDownloadDto> DownloadFileAsync(
        string fileId,
        CancellationToken cancellationToken = default);

    Task DeleteFileAsync(
        string fileId,
        CancellationToken cancellationToken = default);
}
```

**HTTP Client Configuration** (similar to Country Service):
```csharp
builder.Services.AddHttpClient<IUploadServiceClient, UploadServiceClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<UploadServiceOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds); // 60 seconds for large files
})
.AddPolicyHandler(GetRetryPolicy())
.AddPolicyHandler(GetCircuitBreakerPolicy());
```

**Error Handling Strategy**:

| Scenario | HTTP Status | Contact Service Behavior |
|----------|-------------|--------------------------|
| Upload success | 200 OK | Store fileId reference in database |
| Upload failure (transient) | 500, 502, 504 | Retry 3 times, then continue without file (best-effort) |
| Upload failure (permanent) | 400 Bad Request | Log error, continue without file (per FR-018) |
| Service unavailable | 503 / Timeout | Continue without files (per FR-025) |
| Download failure | 404, 410 | Return appropriate error to admin |

**Best-Effort File Upload Logic**:
```csharp
public async Task<List<ContactFile>> UploadFilesAsync(
    List<CreateContactFileRequest> files,
    CancellationToken cancellationToken)
{
    var uploadedFiles = new List<ContactFile>();

    foreach (var file in files)
    {
        try
        {
            var result = await _uploadServiceClient.UploadFileAsync(
                file.FileContent,
                file.FileName,
                file.ContentType,
                bucketName: "contact-submissions",
                folder: $"{DateTime.UtcNow:yyyy/MM}",
                cancellationToken);

            uploadedFiles.Add(new ContactFile
            {
                FileName = file.FileName,
                ObjectName = result.ObjectName,
                FileSize = result.FileSize,
                ContentType = result.ContentType,
                UploadServiceFileId = result.FileId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload file {FileName}, continuing without it", file.FileName);
            // Continue with next file (best-effort per FR-018)
        }
    }

    return uploadedFiles;
}
```

---

## Service Dependencies Summary

| Service | Purpose | Critical Path | Fallback Strategy |
|---------|---------|---------------|-------------------|
| Country Service | Validate countryId | ✅ Yes | Reject submission with 503 error |
| Upload Service | Store file attachments | ❌ No | Continue without files (best-effort) |

**Country Service is critical**: Contact submissions MUST be rejected if Country Service is unavailable (per FR-024).

**Upload Service is non-critical**: Contact submissions SHOULD continue even if Upload Service fails (per FR-025).

---

## Configuration Summary

**appsettings.json**:
```json
{
  "CountryService": {
    "BaseUrl": "http://maliev-country-service.maliev-dev.svc.cluster.local/api",
    "TimeoutSeconds": 10
  },
  "UploadService": {
    "BaseUrl": "http://maliev-upload-service.maliev-dev.svc.cluster.local/api",
    "TimeoutSeconds": 60
  }
}
```

**Google Secret Manager** (if authentication required):
- `/mnt/secrets/CountryServiceApiKey` (if needed)
- `/mnt/secrets/UploadServiceJwt` (if needed)

---

## Action Items for Team

1. **Country Service**: Confirm authentication mechanism (JWT? API key? None?)
2. **Country Service**: Provide exact response schema (JSON structure)
3. **Country Service**: Confirm base URLs for each environment
4. **Upload Service**: Confirm authentication mechanism for admin downloads
5. **Upload Service**: Confirm bucket name for contact submissions
6. **Both Services**: Provide SLA/availability metrics for circuit breaker tuning
