# Feature Specification: Contact Submission Service

**Feature Branch**: `001-contact-service`
**Created**: 2025-10-29
**Status**: Draft
**Input**: User description: "Create a Contact WebAPI service specification. This microservice handles all customer contact submissions from the website and other channels. It must collect user details (firstName, lastName, phoneNumber, email, countryId, and message), with optional fields (companyName, uploadIds, and metadata). It integrates with the Country Service (/countries/v1) for validation and the Upload Service (/uploads/v1) for file uploads to Google Cloud Storage. Quotation-related inquiries are excluded, as those are handled by the Quotation and Quotation Request Services."

## Clarifications

### Session 2025-10-29

- Q: When Country Service is unavailable during contact submission, should the system reject submissions or queue them for later validation? → A: Reject submission immediately with error message asking user to retry later
- Q: How should concurrent admin updates to the same inquiry be handled (two admins changing status simultaneously)? → A: Last write wins with optimistic concurrency warning (timestamp-based, notify if overwriting recent changes)
- Q: What happens if an admin tries to delete an inquiry that has already been marked as Resolved? → A: Allow deletion with audit log entry recording who deleted, when, and reason (require deletion reason in request)

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Basic Contact Submission (Priority: P1)

A customer visits the Maliev website and wants to submit a general inquiry or support request. They fill out a contact form with their personal information, a subject line, and message, then submit it to receive confirmation that their inquiry was received.

**Why this priority**: This is the core functionality - without the ability to submit basic contact forms, the service has no value. It represents the minimum viable product and delivers immediate business value by capturing customer inquiries.

**Independent Test**: Can be fully tested by submitting a contact form with required fields (fullName, email, countryId, subject, message) and optional phoneNumber, verifying the submission is stored and confirmation is provided. Delivers a working contact submission system without any additional features.

**Acceptance Scenarios**:

1. **Given** a customer is on the contact form, **When** they enter all required fields (fullName: "John Doe", email: "john@example.com", countryId: 1, subject: "Need help with order", message: "I need help with my order #12345") and submit, **Then** the system accepts the inquiry and returns a success confirmation with a unique inquiry ID
2. **Given** a customer submits a valid contact form with optional phoneNumber: "+66812345678", **When** the submission is processed, **Then** the system stores the phone number along with other details
3. **Given** a customer submits a valid contact form, **When** the submission is processed, **Then** the system validates the countryId exists in the Country Service before accepting the submission
4. **Given** a customer enters an invalid email format, **When** they attempt to submit, **Then** the system rejects the submission with a clear validation error message
5. **Given** a customer omits a required field (fullName, email, countryId, subject, or message), **When** they attempt to submit, **Then** the system rejects the submission and indicates which required fields are missing

---

### User Story 2 - Contact Submission with Company Information (Priority: P2)

A business customer wants to submit a contact inquiry on behalf of their company. They need to include their company name along with their personal contact information to establish a business relationship context.

**Why this priority**: While not essential for MVP, this adds significant value for B2B customers and helps categorize inquiries. It's a simple addition that enhances the basic functionality without adding complexity.

**Independent Test**: Can be tested by submitting a contact form with the optional companyName field populated, verifying it's stored correctly and doesn't interfere with required field validation.

**Acceptance Scenarios**:

1. **Given** a business customer is on the contact form, **When** they enter all required fields plus companyName: "Acme Corp", **Then** the system accepts the submission and stores the company information
2. **Given** a customer submits without a company name, **When** the submission is processed, **Then** the system accepts it without requiring the company name field
3. **Given** a customer enters a company name that exceeds reasonable length (over 200 characters), **When** they submit, **Then** the system validates the length and rejects with an appropriate error message

---

### User Story 3 - Contact Submission with File Attachments (Priority: P3)

A customer needs to submit supporting documentation or images with their inquiry (such as product photos, specifications, or order details). They attach files directly to the contact form submission, and the system handles uploading them to storage in the background.

**Why this priority**: This is an enhancement feature that enables richer inquiries but is not essential for basic contact functionality. Customers can still submit inquiries via text, and files can be requested via follow-up if needed.

**Independent Test**: Can be tested by submitting a contact form with attached files (fileName, fileContent bytes, contentType), verifying the files are uploaded to Upload Service successfully, and the submission stores file references. Delivers file attachment capability independently of other features.

**Acceptance Scenarios**:

1. **Given** a customer has selected files to attach (e.g., "product-photo.jpg" and "specifications.pdf"), **When** they submit the contact form with these files inline, **Then** the system uploads each file to Upload Service, stores the file references, and confirms successful submission
2. **Given** a customer submits files and one file upload fails, **When** the submission is processed, **Then** the system logs the file upload error but continues with the submission for other files that succeeded
3. **Given** a customer submits without any files attached, **When** the submission is processed, **Then** the system accepts it without requiring file attachments
4. **Given** a customer attaches more than 10 files, **When** they submit, **Then** the system rejects the submission indicating the maximum attachment limit of 10 files
5. **Given** a customer attaches a file exceeding reasonable size (over 25MB per file), **When** they submit, **Then** the system rejects the submission with a file size limit error

---

### User Story 4 - Admin Inquiry Management (Priority: P4)

Support staff and administrators need to track, prioritize, and manage customer contact submissions. They need to categorize inquiries by type (General, Supplier, Business), set priority levels, update status as they work through inquiries, and maintain a clear audit trail of when inquiries were resolved.

**Why this priority**: While customers can submit inquiries without admin features, effective inquiry management is essential for customer service operations. This is the foundational workflow that enables support teams to provide timely responses and maintain service quality metrics.

**Independent Test**: Can be tested by submitting contact forms with different contactType values, then using admin endpoints to update status and priority, filter inquiries by various criteria, and verify resolution timestamps. Delivers complete admin workflow independently of customer-facing features.

**Acceptance Scenarios**:

1. **Given** an admin views pending inquiries, **When** they update an inquiry status from "New" to "InProgress", **Then** the system records the status change with timestamp and allows admin to continue working
2. **Given** an admin is triaging urgent inquiries, **When** they change priority from "Medium" to "Urgent", **Then** the system updates the priority immediately and reflects this in filtered inquiry lists
3. **Given** an admin has resolved a customer inquiry, **When** they update status to "Resolved", **Then** the system automatically records the resolvedAt timestamp for SLA tracking
4. **Given** an admin needs to focus on specific inquiry types, **When** they filter by contactType="Supplier" and status="New", **Then** the system returns only supplier inquiries that haven't been addressed yet
5. **Given** an admin determines an inquiry was submitted in error, **When** they delete the inquiry, **Then** the system removes the inquiry and associated files from the database
6. **Given** a customer submitted inquiry files, **When** an admin views the inquiry, **Then** they can download attached files directly through the admin interface to review supporting documentation

---

### Edge Cases

- What happens when a customer submits with a countryId that exists in the Country Service but is marked as inactive or not accepting inquiries?
- How does the system handle duplicate submissions from the same email within a short time window (e.g., user clicks submit multiple times)?
- When Country Service is unavailable during submission validation, the system rejects the submission with HTTP 503 error and instructs user to retry
- How does the system handle phone numbers in various international formats (with/without country codes, different separator styles)?
- What happens if a file upload to Upload Service fails but the contact submission should continue?
- How are special characters, emojis, or non-Latin scripts handled in fullName, subject, and messages?
- What happens when a customer submits with an extremely long message (over 10,000 characters)?
- How does the system handle rate limiting to prevent spam or abuse from a single IP address or email?
- What validation prevents accepting ContactType=Quotation (which should go to separate Quotation Service)?
- How are priority levels determined when a submission is first created (default to Medium)?
- How does admin distinguish between different inquiry types when filtering (can they filter by multiple types)?
- Admins can delete resolved inquiries by providing deletion reason; system logs audit entry with admin identifier, timestamp, reason, and deleted inquiry summary for compliance
- How does the system handle very large file attachments (over 25MB per file)?
- What happens if Upload Service returns fileId but then the file is deleted before admin downloads it?
- Concurrent admin updates use last-write-wins strategy with timestamp-based conflict detection; admins receive warning when overwriting changes made within last 5 minutes

## Requirements *(mandatory)*

### Functional Requirements

#### Data Validation (FR-001 to FR-015)

- **FR-001**: System MUST accept contact inquiries with all required fields: fullName, email, countryId, subject, message, and contactType
- **FR-002**: System MUST validate fullName is between 1-200 characters
- **FR-003**: System MUST validate email addresses conform to standard email format (RFC 5322) with maximum 254 characters
- **FR-004**: System SHOULD validate phoneNumber format when provided (optional field, 8-20 characters, allowing numbers, spaces, hyphens, parentheses, plus sign)
- **FR-005**: System MUST validate subject field is between 1-500 characters
- **FR-006**: System MUST validate message field is not empty and does not exceed 10,000 characters
- **FR-007**: System MUST validate countryId exists in the Country Service via GET /countries/v1/{id} before accepting inquiry
- **FR-008**: System MUST accept optional companyName field (1-200 characters) when provided
- **FR-009**: System MUST accept optional files array with inline file content (fileName, fileContent bytes, contentType)
- **FR-010**: System MUST limit files array to maximum 10 files per inquiry
- **FR-011**: System MUST validate each file size does not exceed 25MB
- **FR-012**: System MUST accept contactType enum with values: General, Supplier, Business
- **FR-013**: System MUST NOT accept ContactType=Quotation (quotation inquiries belong to separate Quotation Service)
- **FR-014**: System MUST set default Priority=Medium and Status=New for new inquiries
- **FR-015**: System MUST return appropriate error messages for validation failures without exposing system internals

#### File Upload Integration (FR-016 to FR-018)

- **FR-016**: System MUST upload files to Upload Service during inquiry transaction using multipart/form-data
- **FR-017**: System MUST store Upload Service fileId references in ContactFile table with metadata (fileName, contentType, fileSize, objectName)
- **FR-018**: System SHOULD continue with inquiry if individual file uploads fail (log errors, store successful files only)

#### Inquiry Processing (FR-019 to FR-026)

- **FR-019**: System MUST generate unique inquiry ID for each accepted contact inquiry
- **FR-020**: System MUST store all inquiry data with timestamps (createdAt, updatedAt)
- **FR-021**: System MUST return inquiry ID and confirmation message to submitter upon successful submission
- **FR-022**: System MUST prevent duplicate inquiries from same email address within 60-second window
- **FR-023**: System MUST implement rate limiting of 10 inquiries per IP address per hour to prevent abuse
- **FR-024**: System MUST reject inquiries when Country Service is unavailable, returning HTTP 503 Service Unavailable with error message: "Unable to validate country information. Please try again in a few moments."
- **FR-025**: System MUST handle Upload Service unavailability gracefully (log errors, allow inquiry without files)
- **FR-026**: System MUST log all inquiry attempts (successful and failed) for audit purposes

#### Admin Workflow Management (FR-027 to FR-032)

- **FR-027**: System MUST allow administrators to update inquiry status (New, InProgress, Resolved, Closed)
- **FR-028**: System MUST allow administrators to update inquiry priority (Low, Medium, High, Urgent)
- **FR-029**: System MUST automatically record resolvedAt timestamp when status changes to Resolved
- **FR-030**: System MUST support filtering inquiries by status, contactType, and priority with pagination
- **FR-031**: System MUST allow administrators to delete inquiries including resolved ones (removes inquiry and associated files), requiring deletion reason in request and logging audit entry with admin identifier, timestamp, reason, and deleted inquiry summary
- **FR-032**: System MUST allow administrators to manage attached files (list, view, download, delete)
- **FR-033**: System MUST use last-write-wins strategy for concurrent admin updates, checking updatedAt timestamp to detect conflicts
- **FR-034**: System SHOULD warn admins when update would overwrite changes made within last 5 minutes by another admin (include admin identifier and timestamp in warning)

#### Query and Retrieval (FR-035 to FR-037)

- **FR-035**: System MUST support querying inquiries by inquiry ID for retrieval
- **FR-036**: System MUST support querying inquiries by email address for customer history
- **FR-037**: System MUST support pagination when retrieving multiple inquiries (configurable page size with default 20, maximum 100). System MUST validate pageSize is between 1 and 100 (inclusive), returning HTTP 400 for invalid values. System MUST validate page number is ≥1, returning HTTP 400 for zero or negative values. System MUST sort inquiries by CreatedAt descending (newest first) by default to ensure consistent pagination across requests.

#### Edge Case Handling (FR-038 to FR-042)

- **FR-038**: System MUST validate country isActive=true via Country Service, rejecting inquiries for inactive countries with error message: "Country is not currently accepting contact inquiries"
- **FR-039**: System MUST support Unicode (UTF-8) characters in fullName, subject, and message fields including emojis and non-Latin scripts
- **FR-040**: System SHOULD implement email-based rate limiting (10 inquiries per email per day) in addition to IP-based limiting
- **FR-041**: System MUST return HTTP 410 Gone when admin attempts to download a file that was deleted from Upload Service
- **FR-042**: System MUST define "Country Service unavailable" as: connection timeout (>5 seconds per HTTP attempt, measured before Polly retries), HTTP 5xx response, connection refused, or circuit breaker open state. Total time including retries may exceed 5 seconds.

#### Performance and Caching (FR-043)

- **FR-043**: System SHOULD cache paginated lists of inquiries to improve performance for admin queries. Any operation that creates, updates, or deletes an inquiry MUST invalidate the cache to ensure data freshness.


### Key Entities

- **Contact Inquiry**: Represents a customer inquiry or support request submitted through the contact form. Contains customer identification (fullName 1-200 characters, email, phoneNumber optional), location context (countryId), inquiry categorization (subject 1-500 characters, contactType enum), the inquiry message, optional business context (companyName), optional file attachments (files array with inline content), admin workflow fields (priority enum, status enum, resolvedAt timestamp), system-generated unique identifier (inquiryId), and timestamps (createdAt, updatedAt). Establishes relationship to Country entity via countryId and to ContactFile entities for file attachments.

- **Contact File**: Represents a file attached to a contact inquiry. Contains file metadata (fileName, contentType, fileSize, objectName), reference to Upload Service file (uploadServiceFileId), and relationship to parent ContactMessage. Files are uploaded to Upload Service during submission and stored as references.

- **Country**: External entity from Country Service. Represents geographical regions and is used to validate and contextualize where contact inquiries originate from. Referenced by countryId in inquiries.

- **Upload Service File**: External entity from Upload Service. Represents files stored in Google Cloud Storage. Contact Service uploads files during submission and stores the returned fileId as uploadServiceFileId in ContactFile table.

## Success Criteria *(mandatory)*

### Measurable Outcomes

#### Customer Experience Metrics

- **SC-001**: Customers can submit a complete contact form in under 30 seconds from page load to confirmation
- **SC-002**: System successfully processes 95% of valid contact inquiries without database errors, Country Service timeouts (>5s), or HTTP 5xx errors. File upload failures to Upload Service do not count as processing failures per FR-018.
- **SC-003**: Contact inquiries are confirmed to users within 2 seconds of submission
- **SC-004**: System handles at least 100 concurrent contact inquiries without degradation in response time
- **SC-005**: Invalid inquiries provide error messages that include the field name and expected format (e.g., "Email: Must be a valid email address") in 100% of validation failures
- **SC-006**: Zero contact inquiries are lost to database errors after acceptance (100% durability). File attachments may be lost to Upload Service failures per FR-018, but inquiry metadata is always persisted.
- **SC-007**: 95% of customers successfully attach files inline to their inquiries on first attempt when needed. **Measurement**: Track ratio of successful POST /contacts/v1 requests with files array (HTTP 201) to total attempts with files array (all status codes). Exclude Upload Service failures per FR-018 (system allows partial file success).

#### Anti-Spam and Security Metrics

- **SC-008**: Rate limiting blocks >10 requests/IP/hour with <1% false positives (measured as blocked requests from IPs with previously successful submissions). Duplicate detection blocks resubmissions within 60 seconds from same email with 100% accuracy.
- **SC-009**: System maintains 99.9% uptime for contact inquiry acceptance

#### Admin Operations Metrics

- **SC-010**: Customer support team can retrieve inquiry history for any email address in under 3 seconds
- **SC-011**: Admin staff can update inquiry status or priority in under 5 seconds
- **SC-012**: Admin can filter and find specific inquiries by status, type, or priority in under 3 seconds
- **SC-013**: File downloads through admin proxy complete in under 10 seconds for files up to 25MB
- **SC-014**: Admin interface displays resolution timestamps accurately for SLA tracking and reporting

## Admin API Endpoints *(Implemented)*

The following endpoints are available for administrative inquiry management, providing complete workflow capabilities for support staff to track, prioritize, and resolve customer inquiries.

### Inquiry Management

#### List All Inquiries
- **Endpoint**: `GET /contacts/v1`
- **Description**: Retrieve all contact inquiries with filtering and pagination
- **Query Parameters**:
  - `page` (integer): Page number (default: 1)
  - `pageSize` (integer): Items per page (default: 20)
  - `status` (enum): Filter by ContactStatus (New, InProgress, Resolved, Closed)
  - `contactType` (enum): Filter by ContactType (General, Supplier, Business)
- **Authentication**: Requires `Admin` role
- **Rate Limiting**: Uses `GlobalPolicy` rate limiter

#### Get Specific Inquiry
- **Endpoint**: `GET /contacts/v1/{id}`
- **Description**: Retrieve detailed information for a specific inquiry including all attached files
- **Path Parameters**: `id` (integer): Contact submission ID
- **Authentication**: Requires `Admin` role
- **Rate Limiting**: Uses `GlobalPolicy` rate limiter

#### Update Inquiry Status/Priority
- **Endpoint**: `PUT /contacts/v1/{id}/status`
- **Description**: Update inquiry status and/or priority level
- **Path Parameters**: `id` (integer): Contact submission ID
- **Request Body**:
  - `status` (enum, required): New status (New, InProgress, Resolved, Closed)
  - `priority` (enum, optional): New priority (Low, Medium, High, Urgent)
- **Behavior**: Automatically sets `resolvedAt` timestamp when status changes to Resolved
- **Authentication**: Requires `Admin` role
- **Rate Limiting**: Uses `GlobalPolicy` rate limiter

#### Delete Inquiry
- **Endpoint**: `DELETE /contacts/v1/{id}`
- **Description**: Permanently delete an inquiry and all associated files (including resolved inquiries)
- **Path Parameters**: `id` (integer): Contact submission ID
- **Request Body**: `reason` (string, required): Deletion reason for audit trail
- **Authentication**: Requires `Admin` role
- **Rate Limiting**: Uses `GlobalPolicy` rate limiter
- **Behavior**: Logs audit entry with admin identifier, timestamp, deletion reason, and deleted inquiry summary before removal
- **Warning**: This is a destructive operation that removes data permanently

### File Management

#### List Inquiry Files
- **Endpoint**: `GET /contacts/v1/{id}/files`
- **Description**: Retrieve all files attached to a specific inquiry
- **Path Parameters**: `id` (integer): Contact submission ID
- **Response**: Array of file metadata (fileName, contentType, fileSize, uploadServiceFileId, createdAt)
- **Authentication**: Requires `Admin` role
- **Rate Limiting**: Uses `GlobalPolicy` rate limiter

#### Download File
- **Endpoint**: `GET /contacts/v1/{id}/files/{fileId}/download`
- **Description**: Download a specific file attached to an inquiry (proxies through Upload Service)
- **Path Parameters**:
  - `id` (integer): Contact submission ID
  - `fileId` (integer): ContactFile ID
- **Response**: File binary content with appropriate Content-Type and Content-Disposition headers
- **Authentication**: Requires `Admin` role
- **Rate Limiting**: Uses `GlobalPolicy` rate limiter

#### Delete File
- **Endpoint**: `DELETE /contacts/v1/{id}/files/{fileId}`
- **Description**: Delete a specific file from an inquiry (removes from both database and Upload Service)
- **Path Parameters**:
  - `id` (integer): Contact submission ID
  - `fileId` (integer): ContactFile ID
- **Authentication**: Requires `Admin` role
- **Rate Limiting**: Uses `GlobalPolicy` rate limiter

### Security and Authorization

All admin endpoints require:
- **Authentication**: Valid JWT token with `Admin` role claim
- **HTTPS**: All requests must use HTTPS in production environments
- **Rate Limiting**: Admin endpoints use `GlobalPolicy` (less restrictive than public submission endpoint)
- **CORS**: Admin endpoints respect CORS configuration for allowed origins

### Error Responses

Admin endpoints return standard HTTP status codes:
- `200 OK`: Successful retrieval
- `201 Created`: Successfully created resource
- `204 No Content`: Successfully deleted resource
- `400 Bad Request`: Invalid request parameters or validation failure
- `401 Unauthorized`: Missing or invalid JWT token
- `403 Forbidden`: Valid token but insufficient permissions (not Admin role)
- `404 Not Found`: Inquiry or file not found
- `429 Too Many Requests`: Rate limit exceeded
- `500 Internal Server Error`: Server error occurred

## Implementation Gaps *(To Be Completed)*

While the Contact Service has been implemented with core functionality, the following features from this specification require completion to achieve full spec compliance.

### Critical Priority (P1 - Required for Production)

#### 0. Base Path Configuration (Routing)

**Current State**: ❌ Not configured
- Routes currently use `/v1/contacts` pattern
- Specification requires `/contacts/v1` pattern (service name first, then version)
- No base path middleware configured

**Required Work**:
- Add `app.UsePathBase("/contacts")` in `Program.cs` middleware pipeline
- Configure before routing middleware (before `UseRouting()` if explicit, or before `UseEndpoints()`/`MapControllers()`)
- Update all controller routes to use `[Route("v1")]` or `[Route("v1/[controller]")]` patterns
- Update health check routes to `/contacts/liveness` and `/contacts/readiness`
- Update Scalar route prefix to match base path

**Implementation Example** (`Program.cs`):
```csharp
var app = builder.Build();

// Configure base path BEFORE other middleware
app.UsePathBase("/contacts");

// Middleware pipeline
app.MapOpenApi(); // OpenAPI document generation
app.MapScalarApiReference(options =>
{
    options.WithTitle("Contact Service API")
           .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
}); // Scalar UI at /contacts/scalar
app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Health checks with base path
app.MapGet("/contacts/liveness", () => "Healthy").AllowAnonymous();
app.MapHealthChecks("/contacts/readiness", new HealthCheckOptions
{
    Predicate = healthCheck => healthCheck.Tags.Contains("readiness"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapControllers();
app.Run();
```

**Controller Route Updates**:
```csharp
// Current (incorrect):
[Route("v1/contacts")]
public class ContactsController : ControllerBase

// New (correct with UsePathBase):
[Route("v1")]  // Or [Route("v1/contacts")] if you want explicit path
public class ContactsController : ControllerBase
```

**Why UsePathBase**: This approach allows the service to be deployed behind API Gateway/reverse proxy that routes `/contacts/*` to this service, maintaining clean separation of concerns and enabling path-based routing at infrastructure level.

**Estimated Effort**: 30 minutes - 1 hour

#### 1. Country Service Integration (FR-007, FR-024)

**Current State**: ❌ Not implemented
- No Country Service client exists
- countryId field exists in spec but not in implementation database schema
- No validation of countryId against Country Service

**Required Work**:
- Create `ICountryServiceClient` interface and `CountryServiceClient` implementation
- Add HTTP client configuration for Country Service in `Program.cs`
- Add `CountryService` configuration section to `appsettings.json` with BaseUrl and timeout
- Add database migration to add `CountryId` column to `ContactMessages` table
- Update `CreateContactMessageRequest` model to include `countryId` (required field)
- Implement validation in `ContactService.CreateContactMessageAsync()` to call Country Service GET `/countries/v1/{id}`
- Handle Country Service unavailability gracefully (circuit breaker pattern recommended)
- Add integration tests for Country Service failures

**Files to Create/Modify**:
- `Maliev.ContactService.Api/Services/ICountryServiceClient.cs` (new)
- `Maliev.ContactService.Api/Services/CountryServiceClient.cs` (new)
- `Maliev.ContactService.Api/Models/CountryServiceOptions.cs` (new)
- `Maliev.ContactService.Api/Models/CreateContactMessageRequest.cs` (add countryId)
- `Maliev.ContactService.Data/Models/ContactMessage.cs` (add CountryId property)
- `Maliev.ContactService.Api/Services/ContactService.cs` (add validation)
- `Maliev.ContactService.Api/appsettings.json` (add CountryService section)
- `Maliev.ContactService.Data/Migrations/AddCountryId.cs` (new migration)

**Estimated Effort**: 1-2 days

#### 2. Duplicate Inquiry Prevention (FR-022)

**Current State**: ❌ Not implemented
- No check for duplicate inquiries from same email within 60-second window
- Users can accidentally submit multiple times by clicking submit button repeatedly

**Required Work**:
- Add duplicate check logic in `ContactService.CreateContactMessageAsync()` before creating inquiry
- Query `ContactMessages` table for recent inquiries (past 60 seconds) from same email
- Return user-friendly error message if duplicate detected
- Consider using cache for faster duplicate detection (optional optimization)
- Add unit tests for duplicate detection scenarios

**Code Location**: `Maliev.ContactService.Api/Services/ContactService.cs:27`

**Implementation Approach**:
```csharp
// In CreateContactMessageAsync, before creating new message:
var recentSubmission = await _context.ContactMessages
    .Where(c => c.Email == request.Email &&
                c.CreatedAt > DateTimeOffset.UtcNow.AddSeconds(-60))
    .AnyAsync();

if (recentInquiry)
{
    throw new DuplicateInquiryException(
        "You have recently submitted a contact form. Please wait before submitting again.");
}
```

**Estimated Effort**: 2-4 hours

#### 3. Rate Limiting Configuration (FR-023)

**Current State**: ⚠️ Partially implemented
- Rate limiting infrastructure exists in code (`Program.cs:78-102`)
- Rate limiter policies configured ("ContactPolicy" and "GlobalPolicy")
- ❌ Configuration values missing from `appsettings.json`
- Cannot verify if 10 inquiries per hour limit is enforced

**Required Work**:
- Add `RateLimiting` section to `appsettings.json` with FixedWindow configuration
- Configure ContactPolicy: 10 inquiries per hour per IP
- Configure GlobalPolicy: Higher limit for admin endpoints
- Test rate limiting with load testing tool
- Document rate limit headers in API response

**Configuration to Add** (`appsettings.json`):
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

**Estimated Effort**: 30 minutes

### Medium Priority (P2 - Required for Full Spec Compliance)

#### 4. Email-Based Query Endpoint (FR-036)

**Current State**: ❌ Not implemented
- Generic list endpoint exists (`GET /contacts/v1`) but no email-specific query
- Spec requires ability to query inquiries by email address

**Required Work**:
- Add endpoint method in `ContactsController`: `GET /contacts/v1?email={email}`
- Implement filtering logic in `IContactService.GetContactMessagesAsync()` to filter by email
- Consider authorization: should users be able to query their own submissions without Admin role?
- Add pagination support for email query results
- Add integration tests for email query

**Code Location**: `Maliev.ContactService.Api/Controllers/ContactsController.cs:59`

**Implementation Note**: Current `GetContactMessages` method supports status and contactType filters. Extend this to support email filter as well.

**Estimated Effort**: 2-3 hours

#### 5. Quotation Type Rejection (FR-013)

**Current State**: ⚠️ Contradicts spec
- `ContactType` enum includes "Quotation" value (`ContactMessage.cs:58`)
- Spec explicitly states quotations belong to separate Quotation Service
- No validation to reject quotation-related inquiries

**Required Work**:
- Remove `Quotation = 2` from `ContactType` enum
- Add validation in `CreateContactMessageRequest` to reject if contactType is invalid
- Update any existing database records with ContactType=Quotation (data migration)
- Update API documentation to reflect only: General, Supplier, Business
- Add validation tests

**Code Location**: `Maliev.ContactService.Data/Models/ContactMessage.cs:54-60`

**Estimated Effort**: 1-2 hours

#### 6. FullName Migration (Data Model Alignment)

**Current State**: ✅ Already implemented correctly
- Implementation uses `FullName` field
- Spec has been updated to match implementation
- No work required

### Low Priority (P3 - Nice to Have)

#### 7. File Size Validation (FR-011)

**Current State**: ⚠️ Not explicitly validated
- No file size limit enforced at application level
- IIS/Kestrel may have default limits
- Spec requires 25MB per file limit

**Required Work**:
- Add file size validation in `CreateContactMessageRequest` model or service layer
- Reject files exceeding 25MB with clear error message
- Add unit tests for file size validation
- Document file size limit in API documentation

**Estimated Effort**: 1 hour

#### 8. Phone Number Format Validation (FR-004 "SHOULD")

**Current State**: ⚠️ Partial validation
- Length validation exists (20 chars max)
- No format validation (only checks length, not content)
- Spec uses "SHOULD" (not "MUST") so lower priority

**Required Work**:
- Add regex validation for phone number format
- Allow: numbers, spaces, hyphens, parentheses, plus sign
- Validate length: 8-20 characters
- Make validation flexible for international formats
- Add validation tests

**Code Location**: `Maliev.ContactService.Api/Models/CreateContactMessageRequest.cs:17-18`

**Estimated Effort**: 1-2 hours

### Configuration Requirements

#### Country Service Configuration (appsettings.json)

```json
{
  "CountryService": {
    "BaseUrl": "http://maliev-country-service/api",
    "TimeoutSeconds": 10
  }
}
```

#### Rate Limiting Configuration (appsettings.json)

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

### Testing Requirements

To fully verify spec compliance, the following tests should be added:

#### Integration Tests
- Country Service integration (success and failure scenarios)
- Upload Service integration (already partially tested)
- Duplicate inquiry prevention (60-second window)
- Rate limiting (10 inquiries/hour enforcement)
- Email-based query with pagination

#### Load Tests
- 100 concurrent inquiries (SC-004)
- File upload performance with 25MB files
- Admin endpoint response times under load

#### End-to-End Tests
- Complete inquiry submission workflow with files
- Admin workflow (status updates, filtering, file downloads)
- Error scenarios (service unavailability, validation failures)

### Summary

**Total Functional Requirements**: 42 (FR-001 to FR-042)
- **MUST requirements**: 37 (88%)
- **SHOULD requirements**: 5 (12%)

**Total Implementation Gaps**: 9 items
- **P1 Critical**: 4 items (base path config, Country Service, duplicate prevention, rate limit config)
- **P2 Medium**: 3 items (email query, quotation rejection, data migration)
- **P3 Low**: 2 items (file size validation, phone format validation)

**Estimated Total Effort**: 3-5 days for P1 + P2 items

**Recommended Implementation Order**:
1. Base path configuration (30 min - 1 hour - quick win, enables correct routing)
2. Rate limiting configuration (30 min - quick win)
3. Duplicate inquiry prevention (4 hours)
4. Country Service integration (2 days - most complex)
5. Email query endpoint (3 hours)
6. Quotation type rejection (2 hours)
7. Optional: File size and phone validation (2-3 hours)

### Next Steps

1. Review this specification with the team
2. Create implementation tasks using `/speckit.tasks`
3. Prioritize Country Service integration (most critical gap)
4. Add rate limiting configuration immediately (easiest fix)
5. Implement duplicate inquiry prevention before production deployment
