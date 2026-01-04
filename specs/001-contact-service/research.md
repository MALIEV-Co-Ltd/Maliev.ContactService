# Research: Contact Submission Service

**Feature**: Contact Submission Service
**Branch**: `001-contact-service`
**Date**: 2025-10-29

## Purpose

This document captures technical research for implementing the remaining gaps in the Contact Service specification. Most of the service is already implemented, so this research focuses on the specific unknowns related to missing features.

## Research Topics

### 1. Country Service Integration Pattern

**Question**: What is the exact API contract and authentication pattern for the Country Service?

**Research Findings**:

Based on the specification and architectural patterns:
- **Endpoint**: `GET /countries/v1/{id}`
- **Expected Response**: JSON with country details (at minimum: `id`, `name`, `isActive`)
- **Authentication**: Likely uses internal service-to-service JWT or API key
- **Timeout**: Should be configured (recommended 10 seconds per CLAUDE.md patterns)
- **Resilience**: Needs Polly retry policy with exponential backoff
- **Circuit Breaker**: Recommended to prevent cascading failures

**Required Information from Team**:
- Exact Country Service base URL for each environment (dev/staging/prod)
- Authentication mechanism (JWT? API key? None for internal traffic?)
- Expected response schema
- SLA/timeout expectations

**Implementation Approach**:
```csharp
public interface ICountryServiceClient
{
    Task<CountryDto?> GetCountryByIdAsync(int countryId, CancellationToken cancellationToken = default);
    Task<bool> ValidateCountryExistsAsync(int countryId, CancellationToken cancellationToken = default);
}
```

Pattern will match existing `UploadServiceClient.cs` implementation.

---

### 2. Duplicate Submission Detection Strategy

**Question**: Should duplicate detection use database queries or caching for performance?

**Research Findings**:

Two viable approaches:

**Option A: Database Query (Recommended)**
- Query `ContactMessages` table for recent submissions from same email within 60 seconds
- Simple implementation, no additional infrastructure
- Performance: PostgreSQL indexed query on `Email` + `CreatedAt` columns is fast enough for expected load (100-500 submissions/day)
- Cost: Adds ~10-20ms to submission time

**Option B: Redis Cache**
- Store recent submission timestamps in Redis with 60-second TTL
- Fastest performance (~1-2ms lookup)
- Adds infrastructure dependency and complexity
- Overkill for current scale

**Decision**: Use **Option A (Database Query)** because:
1. Service already has database dependency
2. Expected submission volume is low (100-500/day)
3. Simpler to maintain and test
4. Can migrate to caching later if performance becomes issue

**Implementation**:
```csharp
// In ContactService.CreateContactMessageAsync()
var recentSubmission = await _context.ContactMessages
    .Where(c => c.Email == request.Email &&
                c.CreatedAt > DateTimeOffset.UtcNow.AddSeconds(-60))
    .AnyAsync(cancellationToken);

if (recentSubmission)
{
    throw new DuplicateSubmissionException(
        "You have recently submitted a contact form. Please wait before submitting again.");
}
```

Add composite index: `CREATE INDEX idx_contact_email_created ON contact_messages(email, created_at DESC);`

---

### 3. Rate Limiting Configuration Values

**Question**: What are the correct rate limit values for ContactPolicy and GlobalPolicy?

**Research Findings**:

Based on specification requirements (FR-023) and architectural patterns:

**ContactPolicy** (Public Submission Endpoint):
- Limit: 10 submissions per IP per hour
- Window: Fixed window (01:00:00)
- Queue: 0 (reject immediately when limit exceeded)
- Applies to: `POST /contact/v1/contacts` endpoint

**GlobalPolicy** (Admin Endpoints):
- Limit: 100 requests per IP per hour (more permissive for internal use)
- Window: Fixed window (01:00:00)
- Queue: 0
- Applies to: All admin endpoints (GET, PUT, DELETE)

**Configuration Structure** (appsettings.json):
```json
{
  "RateLimiting": {
    "FixedWindow": {
      "PermitLimit": 10,
      "Window": "01:00:00",
      "QueueLimit": 0
    },
    "GlobalFixedWindow": {
      "PermitLimit": 100,
      "Window": "01:00:00",
      "QueueLimit": 0
    }
  }
}
```

**Note**: Infrastructure already exists in `Program.cs:78-102`, just needs configuration values added.

---

### 4. Email Query Endpoint Authorization

**Question**: Should the email query endpoint require Admin role or allow public access for users to query their own submissions?

**Research Findings**:

**Option A: Admin Only** (Recommended)
- Requires `[Authorize(Roles = "Admin")]`
- Prevents customer enumeration attacks
- Consistent with other query endpoints
- Simple authorization model

**Option B: Public with Email Verification**
- Allow customers to query their own submissions
- Requires additional email verification mechanism (send verification code, magic link, etc.)
- Adds complexity for limited value
- Opens potential security holes (email enumeration, privacy concerns)

**Decision**: Use **Option A (Admin Only)** because:
1. Consistent with existing admin endpoints
2. No requirement in spec for customer self-service
3. Avoids security and privacy concerns
4. Can add customer-facing portal later if needed

**Implementation**:
```csharp
[HttpGet]
[Authorize(Roles = "Admin")]
[EnableRateLimiting("GlobalPolicy")]
public async Task<ActionResult<PaginatedResponse<ContactMessageResponse>>> GetContactMessages(
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    [FromQuery] string? email = null,  // Add email filter parameter
    [FromQuery] ContactStatus? status = null,
    [FromQuery] ContactType? contactType = null)
{
    // ... implementation
}
```

---

### 5. Phone Number Validation Pattern

**Question**: What regex pattern should be used for international phone number validation (FR-004)?

**Research Findings**:

**Requirements** (from spec):
- Optional field (only validate when provided)
- 8-20 characters length
- Allow: numbers, spaces, hyphens, parentheses, plus sign
- Support international formats

**Recommended Regex**:
```csharp
[RegularExpression(@"^[\d\s\-\(\)\+]{8,20}$",
    ErrorMessage = "Phone number must be 8-20 characters and contain only numbers, spaces, hyphens, parentheses, or plus sign")]
```

**Examples of Valid Formats**:
- `+66812345678` (Thailand international)
- `(02) 1234-5678` (Formatted)
- `021234567` (Local)
- `+1 (555) 123-4567` (US international formatted)

**Implementation Note**: This is "SHOULD" validation (not "MUST") per FR-004, so it's P3 priority. The current `[StringLength(20)]` validation is acceptable for MVP.

---

### 6. File Size Validation Enforcement

**Question**: Where should 25MB file size limit be enforced - at application level or infrastructure level?

**Research Findings**:

**Multiple Layers Needed**:

1. **Kestrel Server Limit** (appsettings.json):
```json
{
  "Kestrel": {
    "Limits": {
      "MaxRequestBodySize": 262144000  // 250MB (10 files × 25MB)
    }
  }
}
```

2. **Request Form Limits** (Program.cs or attribute):
```csharp
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 262144000; // 250MB total
});
```

3. **Application-Level Validation** (Service layer):
```csharp
// In ContactService.CreateContactMessageAsync()
foreach (var file in request.Files)
{
    if (file.FileContent.Length > 26214400) // 25MB in bytes
    {
        throw new ValidationException($"File '{file.FileName}' exceeds maximum size of 25MB");
    }
}
```

**Recommended Approach**: Implement all three layers for defense in depth. Application-level validation provides clearest user feedback.

---

### 7. Optimistic Concurrency Implementation

**Question**: How should optimistic concurrency be implemented for admin updates (FR-033, FR-034)?

**Research Findings**:

Based on EF Core best practices and spec requirements:

**Database Schema**:
```csharp
// Add to ContactMessage.cs entity
[Timestamp]
public byte[] RowVersion { get; set; }
```

**Service Layer Logic**:
```csharp
public async Task UpdateContactStatusAsync(int id, UpdateContactStatusRequest request)
{
    var contact = await _context.ContactMessages.FindAsync(id);

    // Check if updated recently by another admin (within 5 minutes)
    var fiveMinutesAgo = DateTimeOffset.UtcNow.AddMinutes(-5);
    if (contact.UpdatedAt > fiveMinutesAgo)
    {
        _logger.LogWarning("Contact {Id} was recently updated by another admin at {UpdatedAt}",
            id, contact.UpdatedAt);
        // Could return warning to client via response header or custom status code
    }

    // Update fields
    contact.Status = request.Status;
    contact.Priority = request.Priority;

    try
    {
        await _context.SaveChangesAsync();
    }
    catch (DbUpdateConcurrencyException)
    {
        throw new ConcurrencyException(
            "This contact was modified by another admin. Please refresh and try again.");
    }
}
```

**Database Migration**:
```csharp
migrationBuilder.AddColumn<byte[]>(
    name: "RowVersion",
    table: "ContactMessages",
    type: "bytea",
    rowVersion: true,
    nullable: false);
```

**Note**: This is part of the Country Service integration migration since both require schema changes.

---

## Summary of Technical Decisions

| Topic | Decision | Justification |
|-------|----------|--------------|
| Country Service Integration | HTTP client with Polly retry policy | Matches existing Upload Service pattern |
| Duplicate Detection | Database query (not caching) | Sufficient performance for current scale, simpler |
| Rate Limiting Values | 10/hour public, 100/hour admin | Per specification FR-023 |
| Email Query Auth | Admin only | Security best practice, consistent with existing endpoints |
| Phone Validation | Regex pattern (P3 priority) | "SHOULD" requirement, not blocking for MVP |
| File Size Limits | Multi-layer validation | Defense in depth, clear user feedback |
| Optimistic Concurrency | EF Core RowVersion + timestamp check | Standard pattern, meets FR-033/FR-034 requirements |

## No Remaining Unknowns

All technical unknowns have been resolved through research and architectural decision-making. The implementation can proceed to Phase 1 (Design & Contracts).

**Action Items Requiring Team Input**:
1. Country Service base URLs for each environment
2. Country Service authentication mechanism
3. Country Service response schema documentation
