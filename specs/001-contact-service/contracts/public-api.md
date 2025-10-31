# Public API Contract: Contact Submission

**Feature**: Contact Submission Service
**Branch**: `001-contact-service`
**Date**: 2025-10-29
**Base Path**: `/contacts/v1`

## Overview

This document defines the public customer-facing API for submitting contact inquiries. This endpoint is rate-limited and allows anonymous submissions without authentication.

---

## POST /contacts/v1

Submit a new contact inquiry with optional file attachments.

### Authentication

**None** - Public endpoint, no authentication required

### Rate Limiting

**Policy**: `ContactPolicy`
- **Limit**: 10 submissions per IP address per hour
- **Window**: Fixed (resets every hour at :00)
- **Response on limit exceeded**: `429 Too Many Requests`
- **Headers**:
  ```
  X-RateLimit-Limit: 10
  X-RateLimit-Remaining: 7
  X-RateLimit-Reset: 1698537600
  ```

### Request

**Content-Type**: `multipart/form-data` (when files attached) or `application/json` (no files)

**Body (JSON)**:
```json
{
  "fullName": "string",
  "email": "string",
  "phoneNumber": "string?",
  "company": "string?",
  "subject": "string",
  "message": "string",
  "countryId": "integer",
  "contactType": "integer",
  "files": [
    {
      "fileName": "string",
      "fileContent": "base64string",
      "contentType": "string"
    }
  ]
}
```

**Body (Multipart Form-Data)**:
```
POST /contacts/v1 HTTP/1.1
Content-Type: multipart/form-data; boundary=----WebKitFormBoundary7MA4YWxkTrZu0gW

------WebKitFormBoundary7MA4YWxkTrZu0gW
Content-Disposition: form-data; name="fullName"

John Doe
------WebKitFormBoundary7MA4YWxkTrZu0gW
Content-Disposition: form-data; name="email"

john@example.com
------WebKitFormBoundary7MA4YWxkTrZu0gW
Content-Disposition: form-data; name="countryId"

1
------WebKitFormBoundary7MA4YWxkTrZu0gW
Content-Disposition: form-data; name="subject"

Need help with order
------WebKitFormBoundary7MA4YWxkTrZu0gW
Content-Disposition: form-data; name="message"

I need assistance with order #12345
------WebKitFormBoundary7MA4YWxkTrZu0gW
Content-Disposition: form-data; name="contactType"

0
------WebKitFormBoundary7MA4YWxkTrZu0gW
Content-Disposition: form-data; name="files"; filename="invoice.pdf"
Content-Type: application/pdf

[binary file content]
------WebKitFormBoundary7MA4YWxkTrZu0gW--
```

**Field Specifications**:

| Field | Type | Required | Constraints | Description |
|-------|------|----------|-------------|-------------|
| `fullName` | string | ✅ Yes | 1-200 chars | Customer's full name |
| `email` | string | ✅ Yes | Valid email, max 254 chars | Contact email address (RFC 5322) |
| `phoneNumber` | string | ❌ No | 8-20 chars, pattern: `^[\d\s\-\(\)\+]{8,20}$` | Optional phone number (international format) |
| `company` | string | ❌ No | 1-200 chars | Optional company name |
| `subject` | string | ✅ Yes | 1-500 chars | Inquiry subject line |
| `message` | string | ✅ Yes | 1-10,000 chars | Inquiry message content |
| `countryId` | integer | ✅ Yes | Must exist in Country Service | Country reference |
| `contactType` | integer | ✅ Yes | 0 (General), 1 (Supplier), 3 (Business) | Inquiry type (NOT 2/Quotation) |
| `files` | array | ❌ No | Max 10 files, 25MB each | File attachments |
| `files[].fileName` | string | ✅ Yes | 1-255 chars | Original filename |
| `files[].fileContent` | bytes | ✅ Yes | Max 26,214,400 bytes (25MB) | File binary content |
| `files[].contentType` | string | ✅ Yes | Valid MIME type | File content type |

**Validation Rules**:
1. All required fields must be present and non-empty
2. Email must be valid format (RFC 5322)
3. Phone number (if provided) must match pattern: `^[\d\s\-\(\)\+]{8,20}$`
4. CountryId must exist in Country Service (validated via GET /countries/v1/{id})
5. ContactType MUST NOT be 2 (Quotation) - rejected with validation error
6. Files array limited to 10 items maximum
7. Each file size must not exceed 25MB
8. Total request size must not exceed 250MB (10 files × 25MB)
9. No duplicate submissions from same email within 60 seconds
10. Message length limited to 10,000 characters

### Response

**Success (201 Created)**:
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
      "uploadServiceFileId": "file_abc123xyz"
    }
  ]
}
```

**Error Responses**:

**400 Bad Request** - Validation failure
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Email": ["The Email field is not a valid e-mail address."],
    "CountryId": ["The CountryId field is required."],
    "Files": ["Maximum 10 files allowed per submission."]
  }
}
```

**429 Too Many Requests** - Rate limit exceeded
```json
{
  "type": "https://tools.ietf.org/html/rfc6585#section-4",
  "title": "Too Many Requests",
  "status": 429,
  "detail": "Rate limit exceeded. You have submitted 10 inquiries in the last hour. Please try again later."
}
```

**503 Service Unavailable** - Country Service unavailable
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.6.4",
  "title": "Service Unavailable",
  "status": 503,
  "detail": "Unable to validate country information. Please try again in a few moments."
}
```

**409 Conflict** - Duplicate submission detected
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Conflict",
  "status": 409,
  "detail": "You have recently submitted a contact form. Please wait before submitting again."
}
```

**422 Unprocessable Entity** - Quotation type rejected
```json
{
  "type": "https://tools.ietf.org/html/rfc4918#section-11.2",
  "title": "Unprocessable Entity",
  "status": 422,
  "detail": "Quotation inquiries should be submitted through the Quotation Service, not the Contact Service."
}
```

---

## Request Examples

### Example 1: Basic Submission (No Files)

**Request**:
```bash
curl -X POST https://api.maliev.com/contacts/v1 \
  -H "Content-Type: application/json" \
  -d '{
    "fullName": "Jane Smith",
    "email": "jane.smith@example.com",
    "phoneNumber": "+66812345678",
    "subject": "Question about 3D printing services",
    "message": "I would like to know more about your industrial 3D printing capabilities for automotive parts.",
    "countryId": 1,
    "contactType": 0
  }'
```

**Response** (201 Created):
```json
{
  "id": 67890,
  "fullName": "Jane Smith",
  "email": "jane.smith@example.com",
  "phoneNumber": "+66812345678",
  "company": null,
  "subject": "Question about 3D printing services",
  "message": "I would like to know more about your industrial 3D printing capabilities for automotive parts.",
  "countryId": 1,
  "contactType": 0,
  "priority": 1,
  "status": 0,
  "createdAt": "2025-10-29T10:45:00Z",
  "updatedAt": "2025-10-29T10:45:00Z",
  "resolvedAt": null,
  "files": []
}
```

---

### Example 2: Business Inquiry with Company and Files

**Request**:
```bash
curl -X POST https://api.maliev.com/contacts/v1 \
  -F "fullName=David Johnson" \
  -F "email=david@techcorp.com" \
  -F "company=TechCorp Industries" \
  -F "phoneNumber=+1 555 123 4567" \
  -F "subject=Partnership Opportunity" \
  -F "message=We are interested in discussing a potential manufacturing partnership for our new product line." \
  -F "countryId=2" \
  -F "contactType=3" \
  -F "files=@specifications.pdf" \
  -F "files=@company-profile.pdf"
```

**Response** (201 Created):
```json
{
  "id": 67891,
  "fullName": "David Johnson",
  "email": "david@techcorp.com",
  "phoneNumber": "+1 555 123 4567",
  "company": "TechCorp Industries",
  "subject": "Partnership Opportunity",
  "message": "We are interested in discussing a potential manufacturing partnership for our new product line.",
  "countryId": 2,
  "contactType": 3,
  "priority": 1,
  "status": 0,
  "createdAt": "2025-10-29T11:00:00Z",
  "updatedAt": "2025-10-29T11:00:00Z",
  "resolvedAt": null,
  "files": [
    {
      "id": 457,
      "fileName": "specifications.pdf",
      "contentType": "application/pdf",
      "fileSize": 1048576,
      "uploadServiceFileId": "file_def456uvw"
    },
    {
      "id": 458,
      "fileName": "company-profile.pdf",
      "contentType": "application/pdf",
      "fileSize": 2097152,
      "uploadServiceFileId": "file_ghi789rst"
    }
  ]
}
```

---

### Example 3: Validation Error - Invalid Email

**Request**:
```bash
curl -X POST https://api.maliev.com/contacts/v1 \
  -H "Content-Type: application/json" \
  -d '{
    "fullName": "Test User",
    "email": "not-an-email",
    "subject": "Test",
    "message": "Test message",
    "countryId": 1,
    "contactType": 0
  }'
```

**Response** (400 Bad Request):
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Email": ["The Email field is not a valid e-mail address."]
  }
}
```

---

### Example 4: Rate Limit Exceeded

**Request** (11th submission within 1 hour):
```bash
curl -X POST https://api.maliev.com/contacts/v1 \
  -H "Content-Type: application/json" \
  -d '{ ... }'
```

**Response** (429 Too Many Requests):
```json
{
  "type": "https://tools.ietf.org/html/rfc6585#section-4",
  "title": "Too Many Requests",
  "status": 429,
  "detail": "Rate limit exceeded. You have submitted 10 inquiries in the last hour. Please try again later."
}
```

**Headers**:
```
HTTP/1.1 429 Too Many Requests
X-RateLimit-Limit: 10
X-RateLimit-Remaining: 0
X-RateLimit-Reset: 1698541200
Retry-After: 3600
```

---

## Integration Notes

### Frontend Integration

**JavaScript Example** (with Fetch API):
```javascript
async function submitContactForm(formData) {
  const payload = {
    fullName: formData.fullName,
    email: formData.email,
    phoneNumber: formData.phoneNumber || null,
    company: formData.company || null,
    subject: formData.subject,
    message: formData.message,
    countryId: parseInt(formData.countryId),
    contactType: parseInt(formData.contactType),
    files: formData.files.map(file => ({
      fileName: file.name,
      fileContent: file.base64Content,
      contentType: file.type
    }))
  };

  try {
    const response = await fetch('https://api.maliev.com/contacts/v1', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(payload)
    });

    if (response.status === 429) {
      const retryAfter = response.headers.get('Retry-After');
      throw new Error(`Rate limit exceeded. Try again in ${retryAfter} seconds.`);
    }

    if (!response.ok) {
      const error = await response.json();
      throw new Error(error.detail || 'Submission failed');
    }

    return await response.json();
  } catch (error) {
    console.error('Contact submission error:', error);
    throw error;
  }
}
```

**React Hook Example**:
```typescript
import { useState } from 'react';

interface ContactFormData {
  fullName: string;
  email: string;
  phoneNumber?: string;
  company?: string;
  subject: string;
  message: string;
  countryId: number;
  contactType: number;
  files?: File[];
}

export function useContactSubmission() {
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submitContact = async (data: ContactFormData) => {
    setIsSubmitting(true);
    setError(null);

    try {
      const response = await fetch('/contacts/v1', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(data)
      });

      if (response.status === 429) {
        throw new Error('You have submitted too many inquiries. Please try again later.');
      }

      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.detail || 'Submission failed');
      }

      return await response.json();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unknown error');
      throw err;
    } finally {
      setIsSubmitting(false);
    }
  };

  return { submitContact, isSubmitting, error };
}
```

---

## Performance Expectations

| Metric | Target | Notes |
|--------|--------|-------|
| Response time (no files) | < 500ms | p95 |
| Response time (with files) | < 2 seconds | p95, depends on file sizes |
| Throughput | 100 concurrent submissions | Without degradation |
| Rate limit | 10/IP/hour | Fixed window |
| Max request size | 250MB | 10 files × 25MB |

---

## Security Considerations

1. **No Authentication**: Public endpoint, protected by rate limiting only
2. **Rate Limiting**: Prevents abuse and spam (10 submissions/IP/hour)
3. **Duplicate Detection**: Prevents accidental double submissions (60-second window)
4. **Input Validation**: All fields validated server-side, client validation is UX only
5. **File Upload Safety**:
   - Size limits enforced (25MB per file, 250MB total)
   - Content type validation recommended (but not enforced)
   - Files uploaded to isolated GCS bucket via Upload Service
6. **Country Validation**: Validates countryId exists before accepting submission
7. **SQL Injection**: Parameterized queries (EF Core) prevent SQL injection
8. **XSS Prevention**: All user input HTML-encoded in admin interface

---

## Testing Checklist

- [ ] Submit with all required fields (success)
- [ ] Submit with optional fields (phoneNumber, company, files)
- [ ] Submit without required fields (validation error)
- [ ] Submit with invalid email format (validation error)
- [ ] Submit with Quotation contactType (rejection error)
- [ ] Submit with non-existent countryId (Country Service validation error)
- [ ] Submit twice within 60 seconds from same email (duplicate error)
- [ ] Submit 11 times within 1 hour from same IP (rate limit error)
- [ ] Submit with 11 files (validation error - max 10)
- [ ] Submit with file > 25MB (validation error)
- [ ] Submit while Country Service is down (503 error)
- [ ] Submit with special characters in message (success, proper encoding)
- [ ] Submit with international phone formats (success)
- [ ] Measure response time for various file sizes
