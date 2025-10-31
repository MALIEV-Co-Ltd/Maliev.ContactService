# Maliev Contact Service

A comprehensive contact form management service for Maliev Co. Ltd., built with .NET 9.0 and designed to handle customer inquiries, quotation requests, and business communications through their website.

## Features

- **Contact Form Submission**: Anonymous contact form submission with file upload support and country selection
- **Admin Management**: Full CRUD operations for contact messages with JWT authentication
- **File Management**: Integration with UploadService for secure file storage and retrieval
- **Country Validation**: Real-time country verification via Country Service integration
- **Duplicate Prevention**: 60-second duplicate inquiry detection per email address
- **Rate Limiting**: Protective rate limiting for contact submissions (10 req/hour) and admin operations (100 req/hour)
- **Real-time Monitoring**: Prometheus metrics and health checks for observability
- **Comprehensive Caching**: Memory-based caching for improved performance
- **Database Integration**: PostgreSQL with Entity Framework Core and optimistic concurrency control
- **Concurrent Update Protection**: Row versioning to prevent conflicting updates

## Architecture

The service follows a clean architecture pattern with the following projects:

- **Maliev.ContactService.Api**: Web API controllers and HTTP endpoints
- **Maliev.ContactService.Data**: Database models, DbContext, and data access layer
- **Maliev.ContactService.Tests**: Comprehensive test suite with 80 test cases

## API Endpoints

### Public Endpoints

| Method | Endpoint | Description | Rate Limit |
|--------|----------|-------------|------------|
| POST | `/contacts/v1` | Submit contact form (requires countryId) | 10 req/hour per IP |

### Admin Endpoints (Requires JWT Authentication)

| Method | Endpoint | Description | Rate Limit |
|--------|----------|-------------|------------|
| GET | `/contacts/v1` | List contact messages with pagination | 100 req/hour per IP |
| GET | `/contacts/v1/{id}` | Get specific contact message | 100 req/hour per IP |
| PUT | `/contacts/v1/{id}/status` | Update contact status | 100 req/hour per IP |
| DELETE | `/contacts/v1/{id}` | Delete contact message | 100 req/hour per IP |
| GET | `/contacts/v1/{id}/files` | List contact files | 100 req/hour per IP |
| GET | `/contacts/v1/{id}/files/{fileId}/download` | Download file | 100 req/hour per IP |
| DELETE | `/contacts/v1/{id}/files/{fileId}` | Delete file | 100 req/hour per IP |

## Data Models

### Contact Types
- **General**: General inquiries
- **Business**: Business partnership inquiries
- **Supplier**: Supplier-related communications

**Note**: Quotation requests are handled by a separate Quotation Service and are not accepted through the Contact Service.

### Priority Levels
- **Low**: Non-urgent inquiries
- **Medium**: Standard priority (default)
- **High**: Important inquiries
- **Urgent**: Critical inquiries requiring immediate attention

### Contact Status
- **New**: Newly submitted contact (default)
- **InProgress**: Under review or processing
- **Resolved**: Successfully handled
- **Closed**: Completed and archived

## Getting Started

### Prerequisites

- .NET 9.0 SDK
- PostgreSQL database
- Docker (for containerized deployment)

### Local Development Setup

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd Maliev.ContactService
   ```

2. **Set up database connection**
   ```bash
   export ContactDbContext="Server=localhost;Port=5432;Database=contact_app_db;User Id=postgres;Password=your_password;"
   ```

3. **Apply database migrations**
   ```bash
   dotnet ef database update --project Maliev.ContactService.Data
   ```

4. **Run the application**
   ```bash
   dotnet run --project Maliev.ContactService.Api
   ```

5. **Access Swagger UI**
   ```
   http://localhost:8080/contacts/swagger
   ```

### Docker Deployment

```bash
docker build -f Maliev.ContactService.Api/Dockerfile -t maliev-contact-service .
docker run -p 8080:8080 -e ContactDbContext="<connection_string>" maliev-contact-service
```

### Kubernetes Deployment

The service is configured for GitOps deployment with:
- **Base Configuration**: `/maliev-gitops/3-apps/maliev-contact-service/base/`
- **Environment Overlays**: Development, Staging, Production
- **Secret Management**: Google Secret Manager integration
- **Monitoring**: Prometheus ServiceMonitor with `monitor: "true"` label

## Configuration

### Environment Variables

| Variable | Description | Required |
|----------|-------------|----------|
| `ContactDbContext` | PostgreSQL connection string | Yes |
| `ASPNETCORE_ENVIRONMENT` | Environment (Development/Staging/Production) | No |

### Application Settings

```json
{
  "RateLimiting": {
    "FixedWindow": {
      "PermitLimit": 10,
      "Window": "01:00:00"
    },
    "GlobalFixedWindow": {
      "PermitLimit": 100,
      "Window": "01:00:00"
    }
  },
  "UploadService": {
    "BaseUrl": "http://upload-service-url",
    "TimeoutSeconds": 30
  },
  "CountryService": {
    "BaseUrl": "http://country-service-url",
    "TimeoutSeconds": 10
  }
}
```

**Required Services**:
- **Country Service**: Must be running and accessible for contact form validation. The service validates that the selected country exists and is active before accepting contact inquiries.

## Testing

The service includes comprehensive test coverage with 86 test cases:

```bash
# Run all tests
dotnet test Maliev.ContactService.sln

# Run integration tests only (requires Docker)
dotnet test --filter "Purpose=LocalTesting"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Test Categories

- **Model Tests** (15 tests): DTO validation and enum testing
- **Service Tests** (39 tests): Business logic, caching, file operations, country validation, duplicate detection
- **Controller Tests** (19 tests): HTTP endpoints, error handling, authentication
- **Integration Tests** (7 tests): End-to-end testing with real PostgreSQL via Testcontainers

### Integration Testing with Testcontainers

Integration tests use **Testcontainers** to provide real PostgreSQL databases for testing. This ensures tests run against actual database behavior, not in-memory simulations.

**Prerequisites**: Docker Desktop must be running

**What is Testcontainers?**
- Automatically manages Docker containers for testing
- Spins up real PostgreSQL databases before tests
- Applies EF Core migrations automatically
- Cleans up containers after tests complete
- Works on any machine with Docker (local, CI/CD)

**How it works:**
1. Test run starts
2. Testcontainers pulls `postgres:17.5` image (first run only, ~30-40 seconds)
3. Starts PostgreSQL container with fresh database
4. Applies all EF Core migrations
5. Tests run against real PostgreSQL
6. Container automatically deleted after tests

**Subsequent test runs**: ~10-15 seconds (image cached)

**Key benefits:**
- ✅ Real PostgreSQL behavior (transactions, indexes, SQL functions)
- ✅ Tests verify migrations work correctly
- ✅ No manual database setup or port-forwarding
- ✅ Reproducible across all environments
- ✅ Automatic cleanup

### Key Test Scenarios
- Duplicate inquiry prevention (60-second window)
- Country Service integration and validation
- Rate limiting enforcement (10 req/hour public, 100 req/hour admin)
- Optimistic concurrency control with RowVersion
- File upload and download operations
- Cache invalidation strategies
- Health check endpoints (liveness and readiness)
- Exception handling with proper HTTP status codes

## Monitoring & Observability

### Health Checks
- **Liveness**: `/contacts/liveness` - Basic application health
- **Readiness**: `/contacts/readiness` - Database connectivity

### Metrics
- **Endpoint**: `/contacts/metrics`
- **Framework**: Prometheus metrics via `prometheus-net.AspNetCore`
- **Dashboards**: Available in Grafana monitoring stack

### Logging
- **Framework**: Serilog with structured logging
- **Output**: Console (containerized) and structured JSON format
- **Levels**: Information, Warning, Error with configurable levels

## Security Features

- **Rate Limiting**: Fixed window rate limiter with IP-based partitioning (10 req/hour public, 100 req/hour admin)
- **Duplicate Prevention**: 60-second duplicate inquiry detection per email to prevent spam
- **Authentication**: JWT Bearer token authentication for admin endpoints
- **Authorization**: Role-based access control
- **Input Validation**: Comprehensive data annotation validation with country verification
- **CORS**: Configured for `*.maliev.com` domains with HTTPS-only in production
- **File Security**: Virus scanning and type validation through UploadService
- **Concurrency Control**: Optimistic locking with RowVersion to prevent conflicting updates

## Database Schema

### ContactMessages Table
```sql
- Id (Primary Key)
- FullName (Required, Max 200 chars)
- Email (Required, Valid email format, Max 254 chars)
- PhoneNumber (Optional, Max 20 chars)
- Company (Optional, Max 200 chars)
- CountryId (Required, Foreign Key to Country Service)
- Subject (Required, Max 500 chars)
- Message (Required, Text, Max 10,000 chars)
- ContactType (Enum: General=0, Business=3, Supplier=1)
- Priority (Enum: Low, Medium, High, Urgent)
- Status (Enum: New, InProgress, Resolved, Closed)
- RowVersion (Concurrency token for optimistic locking)
- CreatedAt, UpdatedAt, ResolvedAt (Timestamps)
```

**Database Indexes**:
- `IX_ContactMessages_Email_CreatedAt`: Composite index for duplicate detection
- `IX_ContactMessages_Status_ContactType`: Composite index for admin filtering
- `IX_ContactMessages_Priority_Status_Filtered`: Filtered index for priority triage (Status IN (New, InProgress))

### ContactFiles Table
```sql
- Id (Primary Key)
- ContactMessageId (Foreign Key)
- FileName, ObjectName (File identifiers)
- FileSize, ContentType (File metadata)
- UploadServiceFileId (Integration with UploadService)
- CreatedAt (Timestamp)
```

## Performance Optimizations

- **Database Indexing**: Optimized indexes on Email, CreatedAt, Status, ContactType
- **Memory Caching**: 5-minute TTL for frequently accessed contact messages
- **Connection Pooling**: Entity Framework connection pooling
- **Async Operations**: Full async/await pattern implementation
- **Resource Limits**: Configured memory and CPU limits in Kubernetes

## Error Handling

- **Global Exception Handler**: Centralized exception handling middleware
- **Structured Logging**: Detailed error tracking with correlation IDs
- **Graceful Degradation**: Service continues operating during UploadService outages
- **Validation**: Comprehensive input validation with detailed error responses
- **User-Friendly Errors**: Specific error messages for duplicate inquiries (409), country service issues (503), and invalid data (400)
- **Audit Logging**: Comprehensive audit trail for all contact inquiry operations

## CI/CD Pipeline

### GitHub Actions Workflows
- **Development**: Auto-deploy on push to `develop` branch
- **Staging**: Auto-deploy on push to `staging` branch
- **Production**: Auto-deploy on push to `main` branch

### Pipeline Steps
1. Build and test .NET application
2. Docker image creation and push to GCR
3. GitOps repository update with new image tag
4. ArgoCD automatic deployment to Kubernetes

## Contributing

1. Follow the existing code patterns and CLAUDE.md conventions
2. Ensure all tests pass: `dotnet test`
3. Verify build succeeds: `dotnet build`
4. Update documentation for API changes
5. Test in development environment before promoting

## Support

For technical support or questions:
- **Documentation**: Internal Confluence pages
- **Monitoring**: Grafana dashboards at monitoring stack
- **Logs**: Kubernetes logs via `kubectl logs`

## License

Internal use only - Maliev Co. Ltd.