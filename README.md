# Maliev Contact Service

A comprehensive contact form management service for Maliev Co. Ltd., built with .NET 9.0 and designed to handle customer inquiries, quotation requests, and business communications through their website.

## Features

- **Contact Form Submission**: Anonymous contact form submission with file upload support
- **Admin Management**: Full CRUD operations for contact messages with JWT authentication
- **File Management**: Integration with UploadService for secure file storage and retrieval
- **Rate Limiting**: Protective rate limiting for contact submissions and admin operations
- **Real-time Monitoring**: Prometheus metrics and health checks for observability
- **Comprehensive Caching**: Memory-based caching for improved performance
- **Database Integration**: PostgreSQL with Entity Framework Core for data persistence

## Architecture

The service follows a clean architecture pattern with the following projects:

- **Maliev.ContactService.Api**: Web API controllers and HTTP endpoints
- **Maliev.ContactService.Data**: Database models, DbContext, and data access layer
- **Maliev.ContactService.Tests**: Comprehensive test suite with 43 test cases

## API Endpoints

### Public Endpoints

| Method | Endpoint | Description | Rate Limit |
|--------|----------|-------------|------------|
| POST | `/v1/contacts` | Submit contact form | 10 req/min per IP |

### Admin Endpoints (Requires JWT Authentication)

| Method | Endpoint | Description | Rate Limit |
|--------|----------|-------------|------------|
| GET | `/v1/contacts` | List contact messages with pagination | 1000 req/min per IP |
| GET | `/v1/contacts/{id}` | Get specific contact message | 1000 req/min per IP |
| PUT | `/v1/contacts/{id}/status` | Update contact status | 1000 req/min per IP |
| DELETE | `/v1/contacts/{id}` | Delete contact message | 1000 req/min per IP |
| GET | `/v1/contacts/{id}/files` | List contact files | 1000 req/min per IP |
| GET | `/v1/contacts/{id}/files/{fileId}/download` | Download file | 1000 req/min per IP |
| DELETE | `/v1/contacts/{id}/files/{fileId}` | Delete file | 1000 req/min per IP |

## Data Models

### Contact Types
- **General**: General inquiries
- **Business**: Business partnership inquiries
- **Quotation**: Quotation requests
- **Supplier**: Supplier-related communications

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
  "Cache": {
    "MaxCacheSize": 1000,
    "DefaultExpirationMinutes": 5
  },
  "UploadService": {
    "BaseUrl": "http://upload-service-url",
    "TimeoutSeconds": 30
  }
}
```

## Testing

The service includes comprehensive test coverage with 43 test cases:

```bash
# Run all tests
dotnet test Maliev.ContactService.sln

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Test Categories

- **Model Tests** (7 tests): DTO validation and enum testing
- **Service Tests** (18 tests): Business logic, caching, file operations
- **Controller Tests** (18 tests): HTTP endpoints, error handling, authentication

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

- **Rate Limiting**: Sliding window rate limiter with IP-based partitioning
- **Authentication**: JWT Bearer token authentication for admin endpoints
- **Authorization**: Role-based access control
- **Input Validation**: Comprehensive data annotation validation
- **CORS**: Configured for `*.maliev.com` domains
- **File Security**: Virus scanning and type validation through UploadService

## Database Schema

### ContactMessages Table
```sql
- Id (Primary Key)
- FullName (Required, Max 200 chars)
- Email (Required, Valid email format, Max 254 chars)
- PhoneNumber (Optional, Max 20 chars)
- Company (Optional, Max 200 chars)
- Subject (Required, Max 500 chars)
- Message (Required, Text)
- ContactType (Enum: General, Business, Quotation, Supplier)
- Priority (Enum: Low, Medium, High, Urgent)
- Status (Enum: New, InProgress, Resolved, Closed)
- CreatedAt, UpdatedAt, ResolvedAt (Timestamps)
```

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