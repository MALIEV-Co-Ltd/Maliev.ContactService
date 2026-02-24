# Admin API Contract: Inquiry Management

**Feature**: Contact Submission Service
**Branch**: `001-contact-service`
**Date**: 2025-10-29
**Base Path**: `/contact/v1/contacts`

## Overview

This document defines the administrative API for managing customer contact inquiries. All endpoints require JWT authentication with the `Admin` role.

---

## Authentication & Authorization

**Authentication**: JWT Bearer Token (RSA asymmetric)

**Required Role**: `Admin`

**Request Header**:
```
Authorization: Bearer <JWT_TOKEN>
```

**JWT Claims Required**:
```json
{
  "sub": "admin@maliev.com",
  "role": "Admin",
  "exp": 1698537600
}
```

**Error Responses**:
- `401 Unauthorized` - Missing or invalid token
- `403 Forbidden` - Valid token but missing Admin role

---

## Endpoints

### 1. GET /contact/v1/contacts

List all contact inquiries with filtering and pagination.

**Authentication**: Required (Admin role)

**Rate Limiting**: `GlobalPolicy` (100 requests/IP/hour)

**Query Parameters**:

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `page` | integer | No | 1 | Page number (1-indexed) |
| `pageSize` | integer | No | 20 | Items per page (max 100) |
| `email` | string | No | - | Filter by customer email |
| `status` | integer | No | - | Filter by status: 0(New), 1(InProgress), 2(Resolved), 3(Closed) |
| `contactType` | integer | No | - | Filter by type: 0(General), 1(Supplier), 3(Business) |
| `priority` | integer | No | - | Filter by priority: 0(Low), 1(Medium), 2(High), 3(Urgent) |

**Request Example**:
```bash
curl -X GET "https://api.maliev.com/contact/v1/contacts?page=1&pageSize=20&status=0&contactType=0" \
  -H "Authorization: Bearer <JWT_TOKEN>"
```

**Response (200 OK)**:
```json
{
  "items": [
    {
      "id": 12345,
      "fullName": "John Doe",
      "email": "john@example.com",
      "phoneNumber": "+66812345678",
      "company": null,
      "subject": "Need help with order",
      "message": "I need assistance with order #12345",
      "countryId": 1,
      "contactType": 0,
      "priority": 1,
      "status": 0,
      "createdAt": "2025-10-29T10:30:00Z",
      "updatedAt": "2025-10-29T10:30:00Z",
      "resolvedAt": null,
      "fileCount": 2
    }
  ],
  "totalCount": 157,
  "page": 1,
  "pageSize": 20,
  "totalPages": 8
}
```

---

### 2. GET /contact/v1/contacts/{id}

Retrieve detailed information for a specific inquiry including all attached files.

**Authentication**: Required (Admin role)

**Rate Limiting**: `GlobalPolicy`

**Path Parameters**:
- `id` (integer, required): Contact submission ID

**Request Example**:
```bash
curl -X GET "https://api.maliev.com/contact/v1/12345" \
  -H "Authorization: Bearer <JWT_TOKEN>"
```

**Response (200 OK)**:
```json
{
  "id": 12345,
  "fullName": "John Doe",
  "email": "john@example.com",
  "phoneNumber": "+66812345678",
  "company": null,
  "subject": "Need help with order",
  "message": "I need assistance with order #12345",
  "countryId": 1,
  "contactType": 0,
  "priority": 1,
  "status": 0,
  "createdAt": "2025-10-29T10:30:00Z",
  "updatedAt": "2025-10-29T10:30:00Z",
  "resolvedAt": null,
  "files": [
    {
      "id": 456,
      "fileName": "invoice.pdf",
      "contentType": "application/pdf",
      "fileSize": 524288,
      "uploadServiceFileId": "file_abc123xyz",
      "createdAt": "2025-10-29T10:30:00Z"
    },
    {
      "id": 457,
      "fileName": "photo.jpg",
      "contentType": "image/jpeg",
      "fileSize": 1048576,
      "uploadServiceFileId": "file_def456uvw",
      "createdAt": "2025-10-29T10:30:00Z"
    }
  ]
}
```

**Error Responses**:
- `404 Not Found` - Inquiry does not exist

---

### 3. PUT /contact/v1/contacts/{id}/status

Update inquiry status and/or priority level.

**Authentication**: Required (Admin role)

**Rate Limiting**: `GlobalPolicy`

**Path Parameters**:
- `id` (integer, required): Contact submission ID

**Request Body**:
```json
{
  "status": 1,
  "priority": 2
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `status` | integer | Yes | New status: 0(New), 1(InProgress), 2(Resolved), 3(Closed) |
| `priority` | integer | No | New priority: 0(Low), 1(Medium), 2(High), 3(Urgent) |

**Request Example**:
```bash
curl -X PUT "https://api.maliev.com/contact/v1/12345/status" \
  -H "Authorization: Bearer <JWT_TOKEN>" \
  -H "Content-Type: application/json" \
  -d '{
    "status": 2,
    "priority": 1
  }'
```

**Response (200 OK)**:
```json
{
  "id": 12345,
  "status": 2,
  "priority": 1,
  "updatedAt": "2025-10-29T11:15:00Z",
  "resolvedAt": "2025-10-29T11:15:00Z"
}
```

**Business Rules**:
1. Changing status to `Resolved` (2) automatically sets `resolvedAt` timestamp
2. Optimistic concurrency: Returns 409 Conflict if record was modified by another admin
3. Warning logged if record was updated within last 5 minutes (concurrent admin activity)

**Error Responses**:
- `404 Not Found` - Inquiry does not exist
- `409 Conflict` - Concurrent modification detected

---

### 4. DELETE /contact/v1/contacts/{id}

Permanently delete an inquiry and all associated files (including resolved inquiries).

**Authentication**: Required (Admin role)

**Rate Limiting**: `GlobalPolicy`

**Path Parameters**:
- `id` (integer, required): Contact submission ID

**Request Body**:
```json
{
  "reason": "Spam submission - unrelated to business"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `reason` | string | Yes | Deletion reason for audit trail (1-500 chars) |

**Request Example**:
```bash
curl -X DELETE "https://api.maliev.com/contact/v1/12345" \
  -H "Authorization: Bearer <JWT_TOKEN>" \
  -H "Content-Type: application/json" \
  -d '{
    "reason": "Duplicate submission"
  }'
```

**Response (204 No Content)**:
No response body.

**Business Rules**:
1. Deletes inquiry and all associated files (CASCADE)
2. Audit log entry created with: admin identifier, timestamp, reason, deleted inquiry summary
3. Files also deleted from Upload Service
4. Resolved inquiries CAN be deleted (with audit trail)

**Error Responses**:
- `404 Not Found` - Inquiry does not exist
- `400 Bad Request` - Missing or invalid deletion reason

---

### 5. GET /contact/v1/contacts/{id}/files

Retrieve all files attached to a specific inquiry.

**Authentication**: Required (Admin role)

**Rate Limiting**: `GlobalPolicy`

**Path Parameters**:
- `id` (integer, required): Contact submission ID

**Request Example**:
```bash
curl -X GET "https://api.maliev.com/contact/v1/12345/files" \
  -H "Authorization: Bearer <JWT_TOKEN>"
```

**Response (200 OK)**:
```json
{
  "contactMessageId": 12345,
  "files": [
    {
      "id": 456,
      "fileName": "invoice.pdf",
      "contentType": "application/pdf",
      "fileSize": 524288,
      "uploadServiceFileId": "file_abc123xyz",
      "createdAt": "2025-10-29T10:30:00Z"
    },
    {
      "id": 457,
      "fileName": "photo.jpg",
      "contentType": "image/jpeg",
      "fileSize": 1048576,
      "uploadServiceFileId": "file_def456uvw",
      "createdAt": "2025-10-29T10:30:00Z"
    }
  ]
}
```

**Error Responses**:
- `404 Not Found` - Inquiry does not exist

---

### 6. GET /contact/v1/contacts/{id}/files/{fileId}/download

Download a specific file attached to an inquiry (proxies through Upload Service).

**Authentication**: Required (Admin role)

**Rate Limiting**: `GlobalPolicy`

**Path Parameters**:
- `id` (integer, required): Contact submission ID
- `fileId` (integer, required): ContactFile ID

**Request Example**:
```bash
curl -X GET "https://api.maliev.com/contact/v1/12345/files/456/download" \
  -H "Authorization: Bearer <JWT_TOKEN>" \
  -o downloaded_file.pdf
```

**Response (200 OK)**:
```
Content-Type: application/pdf
Content-Disposition: attachment; filename="invoice.pdf"
Content-Length: 524288

[binary file content]
```

**Business Rules**:
1. Contact Service proxies request to Upload Service
2. Uses admin JWT to authenticate with Upload Service
3. Returns file binary content with appropriate headers
4. Logs file access for audit purposes

**Error Responses**:
- `404 Not Found` - Inquiry or file does not exist
- `410 Gone` - File was deleted from Upload Service

**Performance**: Target < 10 seconds for files up to 25MB

---

### 7. DELETE /contact/v1/contacts/{id}/files/{fileId}

Delete a specific file from an inquiry (removes from both database and Upload Service).

**Authentication**: Required (Admin role)

**Rate Limiting**: `GlobalPolicy`

**Path Parameters**:
- `id` (integer, required): Contact submission ID
- `fileId` (integer, required): ContactFile ID

**Request Example**:
```bash
curl -X DELETE "https://api.maliev.com/contact/v1/12345/files/456" \
  -H "Authorization: Bearer <JWT_TOKEN>"
```

**Response (204 No Content)**:
No response body.

**Business Rules**:
1. Removes file record from database
2. Deletes file from Upload Service
3. Does NOT delete the parent inquiry
4. Audit log entry created

**Error Responses**:
- `404 Not Found` - Inquiry or file does not exist

---

## Common Response Codes

| Code | Description | When Used |
|------|-------------|-----------|
| 200 | OK | Successful GET or PUT request |
| 201 | Created | Successful POST request |
| 204 | No Content | Successful DELETE request |
| 400 | Bad Request | Invalid request parameters or body |
| 401 | Unauthorized | Missing or invalid JWT token |
| 403 | Forbidden | Valid token but insufficient permissions (not Admin) |
| 404 | Not Found | Resource does not exist |
| 409 | Conflict | Concurrent modification detected (optimistic concurrency) |
| 429 | Too Many Requests | Rate limit exceeded |
| 500 | Internal Server Error | Unexpected server error |
| 503 | Service Unavailable | Upload Service unavailable |

---

## Pagination Format

All list endpoints support pagination with the following format:

**Response Structure**:
```json
{
  "items": [],
  "totalCount": 157,
  "page": 1,
  "pageSize": 20,
  "totalPages": 8,
  "hasNextPage": true,
  "hasPreviousPage": false
}
```

**Query Parameters**:
- `page`: Page number (1-indexed, default: 1)
- `pageSize`: Items per page (default: 20, max: 100)

---

## Filtering Examples

### Example 1: New Inquiries (Triage)
```bash
GET /contact/v1/contacts?status=0&page=1&pageSize=50
```

### Example 2: Urgent Supplier Inquiries
```bash
GET /contact/v1/contacts?contactType=1&priority=3&status=0
```

### Example 3: Customer Email History
```bash
GET /contact/v1/contacts?email=john@example.com
```

### Example 4: Recently Resolved
```bash
GET /contact/v1/contacts?status=2&page=1&pageSize=10
```

---

## Optimistic Concurrency

Admin update endpoints (PUT /contacts/{id}/status) implement optimistic concurrency control:

**Scenario**: Two admins update the same inquiry simultaneously

**Behavior**:
1. First admin saves successfully (status changes)
2. Second admin receives 409 Conflict response
3. System logs warning if updates occur within 5 minutes of each other

**Conflict Response (409)**:
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Conflict",
  "status": 409,
  "detail": "This contact was modified by another admin. Please refresh and try again.",
  "lastUpdatedBy": "admin@maliev.com",
  "lastUpdatedAt": "2025-10-29T11:10:00Z"
}
```

**Warning Log Entry** (updates within 5 minutes):
```json
{
  "level": "Warning",
  "message": "Contact 12345 was recently updated by another admin",
  "contactId": 12345,
  "previousUpdateAt": "2025-10-29T11:10:00Z",
  "previousUpdatedBy": "admin@maliev.com",
  "currentAdmin": "admin2@maliev.com"
}
```

---

## Audit Logging

All admin actions are logged for compliance and audit purposes:

**Logged Events**:
- Inquiry status/priority changes
- Inquiry deletions (with reason)
- File deletions
- File downloads
- Concurrent modification attempts

**Log Entry Structure**:
```json
{
  "timestamp": "2025-10-29T11:15:00Z",
  "action": "UPDATE_STATUS",
  "contactId": 12345,
  "adminId": "admin@maliev.com",
  "changes": {
    "status": { "from": 0, "to": 2 },
    "priority": { "from": 1, "to": 1 }
  }
}
```

**Deletion Audit Entry**:
```json
{
  "timestamp": "2025-10-29T11:20:00Z",
  "action": "DELETE_CONTACT",
  "contactId": 12345,
  "adminId": "admin@maliev.com",
  "reason": "Duplicate submission",
  "deletedData": {
    "email": "john@example.com",
    "subject": "Need help with order",
    "createdAt": "2025-10-29T10:30:00Z",
    "fileCount": 2
  }
}
```

---

## Performance Expectations

| Endpoint | Target Response Time | Notes |
|----------|---------------------|-------|
| GET /contacts (list) | < 200ms | p95, with filters and pagination |
| GET /contacts/{id} | < 100ms | p95, single record |
| PUT /contacts/{id}/status | < 150ms | p95, simple update |
| DELETE /contacts/{id} | < 300ms | p95, includes file deletion |
| GET /files/{id}/download | < 10s | p95, up to 25MB files |

---

## Testing Checklist

**List Endpoint**:
- [ ] List all inquiries (no filters)
- [ ] Filter by status
- [ ] Filter by contactType
- [ ] Filter by email
- [ ] Combine multiple filters
- [ ] Pagination (page 1, 2, 3...)
- [ ] Invalid page number (should default to 1)
- [ ] PageSize > 100 (should cap at 100)

**Detail Endpoint**:
- [ ] Get existing inquiry (success)
- [ ] Get non-existent inquiry (404)
- [ ] Get inquiry with files
- [ ] Get inquiry without files

**Status Update**:
- [ ] Update status only
- [ ] Update priority only
- [ ] Update both status and priority
- [ ] Update to Resolved (should set resolvedAt)
- [ ] Concurrent update conflict (409)
- [ ] Update non-existent inquiry (404)

**Delete Endpoint**:
- [ ] Delete inquiry with reason (success)
- [ ] Delete without reason (400)
- [ ] Delete non-existent inquiry (404)
- [ ] Delete resolved inquiry (success with audit log)
- [ ] Verify files also deleted from Upload Service

**File Operations**:
- [ ] List files for inquiry
- [ ] Download file (success)
- [ ] Download non-existent file (404)
- [ ] Delete file (success)
- [ ] Verify file deleted from Upload Service

**Authentication**:
- [ ] Request without token (401)
- [ ] Request with invalid token (401)
- [ ] Request with non-Admin token (403)
- [ ] Request with expired token (401)

**Rate Limiting**:
- [ ] 101st request within hour (429)
