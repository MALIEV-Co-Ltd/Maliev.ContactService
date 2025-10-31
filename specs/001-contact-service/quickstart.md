# Quickstart Guide: Contact Submission Service

**Feature**: Contact Submission Service
**Branch**: `001-contact-service`
**Date**: 2025-10-29

## Overview

This guide will help you set up, run, and develop the Contact Submission Service locally. The service is already partially implemented, and this guide covers both running the existing implementation and working on the remaining spec gaps.

---

## Prerequisites

Before you begin, ensure you have the following installed:

- **.NET 9.0 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/9.0)
- **Docker Desktop** - For running PostgreSQL locally
- **kubectl** - For accessing development database
- **Git** - For version control
- **Preferred IDE**: Visual Studio 2022, VS Code with C# extension, or JetBrains Rider

**Verify Installation**:
```bash
dotnet --version          # Should show 9.0.x
docker --version          # Should show 20.x or higher
kubectl version --client  # Should show 1.28 or higher
```

---

## Quick Start (5 Minutes)

### 1. Clone and Build

```bash
# Clone repository
git clone https://github.com/MALIEV-Co-Ltd/Maliev.ContactService.git
cd Maliev.ContactService

# Checkout feature branch
git checkout 001-contact-service

# Restore dependencies
dotnet restore Maliev.ContactService.sln

# Build solution
dotnet build Maliev.ContactService.sln

# Run tests
dotnet test Maliev.ContactService.sln --verbosity normal
```

### 2. Start Local Database

**Option A: Docker Compose (Recommended)**
```bash
# Create docker-compose.yml
cat > docker-compose.yml <<EOF
version: '3.8'
services:
  postgres:
    image: postgres:18
    container_name: contact-service-db
    environment:
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: devpassword123
      POSTGRES_DB: contact_app_db
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data

volumes:
  postgres_data:
EOF

# Start PostgreSQL
docker-compose up -d

# Verify running
docker ps | grep contact-service-db
```

**Option B: Use Development K8s Cluster**
```bash
# Port forward to development PostgreSQL (direct to pod)
kubectl port-forward -n maliev-dev postgres-cluster-1 5432:5432
```

### 3. Apply Database Migrations

```bash
# Set connection string
export ContactDbContext="Server=localhost;Port=5432;Database=contact_app_db;User Id=postgres;Password=devpassword123;"

# Apply migrations
dotnet ef database update --project Maliev.ContactService.Data
```

### 4. Run the Service

```bash
cd Maliev.ContactService.Api
dotnet run
```

**Service Endpoints**:
- API: `http://localhost:5000`
- API Documentation (Scalar): `http://localhost:5000/contacts/scalar`
- Health Check (Liveness): `http://localhost:5000/contacts/liveness`
- Health Check (Readiness): `http://localhost:5000/contacts/readiness`

### 5. Test the API

**Submit a Contact Form**:
```bash
curl -X POST http://localhost:5000/contacts/v1 \
  -H "Content-Type: application/json" \
  -d '{
    "fullName": "Test User",
    "email": "test@example.com",
    "phoneNumber": "+66812345678",
    "subject": "Test Inquiry",
    "message": "This is a test message for development",
    "contactType": 0
  }'
```

**Note**: This will fail with `countryId` validation error because Country Service integration is not yet implemented. This is expected per the implementation gaps documented in spec.md.

---

## Development Setup

### IDE Configuration

**Visual Studio 2022**:
1. Open `Maliev.ContactService.sln`
2. Set `Maliev.ContactService.Api` as startup project
3. Run with F5 (Debug) or Ctrl+F5 (Run)

**VS Code**:
1. Open folder: `Maliev.ContactService`
2. Install recommended extensions (C# Dev Kit, C# Extensions)
3. Press F5 to launch with debugger

**Rider**:
1. Open `Maliev.ContactService.sln`
2. Run configuration: `Maliev.ContactService.Api`
3. Run with Shift+F10

---

### Environment Configuration

**appsettings.Development.json**:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  },
  "ConnectionStrings": {
    "ContactDbContext": "Server=localhost;Port=5432;Database=contact_app_db;User Id=postgres;Password=devpassword123;Include Error Detail=true"
  },
  "UploadService": {
    "BaseUrl": "http://localhost:8080/api",
    "TimeoutSeconds": 60
  },
  "CountryService": {
    "BaseUrl": "http://localhost:8081/api",
    "TimeoutSeconds": 10
  },
  "RateLimiting": {
    "FixedWindow": {
      "PermitLimit": 100,
      "Window": "01:00:00",
      "QueueLimit": 0
    },
    "GlobalFixedWindow": {
      "PermitLimit": 1000,
      "Window": "01:00:00",
      "QueueLimit": 0
    }
  }
}
```

**User Secrets** (for sensitive data):
```bash
# Initialize user secrets
cd Maliev.ContactService.Api
dotnet user-secrets init

# Add database password
dotnet user-secrets set "ConnectionStrings:ContactDbContext" "Server=localhost;Port=5432;Database=contact_app_db;User Id=postgres;Password=devpassword123;"

# Add JWT public key (for admin authentication)
dotnet user-secrets set "JwtSettings:PublicKey" "<RSA_PUBLIC_KEY>"
```

---

## Project Structure

```
Maliev.ContactService/
├── Maliev.ContactService.Api/          # Web API
│   ├── Controllers/
│   │   └── ContactsController.cs       # REST endpoints
│   ├── Services/
│   │   ├── ContactService.cs           # Business logic
│   │   └── UploadServiceClient.cs      # External service integration
│   ├── Models/
│   │   └── CreateContactMessageRequest.cs  # DTOs
│   └── Program.cs                      # DI & middleware configuration
│
├── Maliev.ContactService.Data/         # Data layer
│   ├── DbContexts/
│   │   └── ContactDbContext.cs         # EF Core context
│   ├── Models/
│   │   ├── ContactMessage.cs           # Entity models
│   │   └── ContactFile.cs
│   └── Migrations/                     # Database migrations
│
└── Maliev.ContactService.Tests/        # Tests
    ├── Controllers/                    # Controller unit tests
    ├── Services/                       # Service unit tests
    └── Integration/                    # Integration tests
```

---

## Common Development Tasks

### Create Database Migration

```bash
# Add new migration
cd Maliev.ContactService.Data
dotnet ef migrations add MigrationName --startup-project ../Maliev.ContactService.Api

# Apply migration
dotnet ef database update --startup-project ../Maliev.ContactService.Api

# Rollback migration
dotnet ef database update PreviousMigrationName --startup-project ../Maliev.ContactService.Api

# Remove last migration (if not applied)
dotnet ef migrations remove --startup-project ../Maliev.ContactService.Api
```

### Run Tests

```bash
# Run all tests
dotnet test Maliev.ContactService.sln --verbosity normal

# Run specific test project
dotnet test Maliev.ContactService.Tests/Maliev.ContactService.Tests.csproj

# Run tests with coverage
dotnet test Maliev.ContactService.sln --collect:"XPlat Code Coverage"

# Run specific test
dotnet test --filter "FullyQualifiedName~ContactServiceTests.CreateContactMessage_ValidRequest_ReturnsCreated"
```

### Debug Integration Tests

Integration tests use Testcontainers for PostgreSQL:

```csharp
// Tests automatically start PostgreSQL container
// No manual setup required
[Fact]
public async Task CreateContact_ValidRequest_SavesToDatabase()
{
    // Testcontainers handles database lifecycle
    var response = await _client.PostAsJsonAsync("/contacts/v1", request);
    response.StatusCode.Should().Be(HttpStatusCode.Created);
}
```

**Troubleshooting Testcontainers**:
- Ensure Docker Desktop is running
- Check Docker has sufficient resources (4GB+ RAM)
- View container logs: `docker logs $(docker ps -q --filter ancestor=postgres:18)`

### Format Code

```bash
# Format entire solution
dotnet format Maliev.ContactService.sln

# Check formatting without changes
dotnet format Maliev.ContactService.sln --verify-no-changes
```

---

## Working on Implementation Gaps

### Current Implementation Status

✅ **Completed Features**:
- Contact submission endpoint (POST /contacts/v1)
- Admin inquiry management (GET, PUT, DELETE)
- File upload integration with Upload Service
- Rate limiting infrastructure
- JWT authentication
- Health checks
- Database schema (partial)

❌ **Missing Features** (Priority order):
1. Country Service integration (P1)
2. Duplicate submission prevention (P1)
3. Rate limiting configuration (P1)
4. Email query endpoint (P2)
5. Quotation type rejection (P2)

### Task 1: Country Service Integration (P1)

**Steps**:

1. **Create Country Service Client**:
```bash
# Create files
touch Maliev.ContactService.Api/Services/ICountryServiceClient.cs
touch Maliev.ContactService.Api/Services/CountryServiceClient.cs
touch Maliev.ContactService.Api/Models/CountryServiceOptions.cs
```

2. **Implement Interface** (see contracts/external-apis.md for details):
```csharp
public interface ICountryServiceClient
{
    Task<bool> ValidateCountryExistsAsync(int countryId, CancellationToken cancellationToken = default);
}
```

3. **Register in DI** (Program.cs):
```csharp
builder.Services.Configure<CountryServiceOptions>(
    builder.Configuration.GetSection("CountryService"));

builder.Services.AddHttpClient<ICountryServiceClient, CountryServiceClient>()
    .AddPolicyHandler(GetRetryPolicy())
    .AddPolicyHandler(GetCircuitBreakerPolicy());
```

4. **Add Database Migration**:
```bash
cd Maliev.ContactService.Data
dotnet ef migrations add AddCountryIdAndRowVersion --startup-project ../Maliev.ContactService.Api
dotnet ef database update --startup-project ../Maliev.ContactService.Api
```

5. **Update Request Model**:
```csharp
// In CreateContactMessageRequest.cs
[Required]
public int CountryId { get; set; }
```

6. **Add Validation in Service**:
```csharp
// In ContactService.CreateContactMessageAsync()
var countryExists = await _countryServiceClient.ValidateCountryExistsAsync(request.CountryId);
if (!countryExists)
{
    throw new ValidationException("Invalid country ID");
}
```

7. **Write Tests**:
```bash
touch Maliev.ContactService.Tests/Services/CountryServiceClientTests.cs
```

**Estimated Time**: 4-6 hours

---

### Task 2: Duplicate Submission Prevention (P1)

**Steps**:

1. **Add Duplicate Check**:
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

2. **Create Exception Class**:
```bash
touch Maliev.ContactService.Api/Exceptions/DuplicateSubmissionException.cs
```

3. **Add Index** (in migration from Task 1):
```csharp
migrationBuilder.CreateIndex(
    name: "idx_contact_email_created",
    table: "contact_messages",
    columns: new[] { "email", "created_at" });
```

4. **Write Tests**:
```csharp
[Fact]
public async Task CreateContact_DuplicateWithin60Seconds_ReturnsConflict()
{
    // First submission
    await CreateContactAsync(email: "test@example.com");

    // Second submission within 60 seconds
    var response = await CreateContactAsync(email: "test@example.com");

    response.StatusCode.Should().Be(HttpStatusCode.Conflict);
}
```

**Estimated Time**: 2-3 hours

---

### Task 3: Rate Limiting Configuration (P1)

**Steps**:

1. **Add Configuration** (appsettings.json):
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

2. **Update Program.cs** (use configuration values):
```csharp
builder.Services.AddRateLimiter(options =>
{
    var config = builder.Configuration.GetSection("RateLimiting:FixedWindow");
    options.AddFixedWindowLimiter("ContactPolicy", opt =>
    {
        opt.PermitLimit = config.GetValue<int>("PermitLimit");
        opt.Window = TimeSpan.Parse(config.GetValue<string>("Window"));
        opt.QueueLimit = config.GetValue<int>("QueueLimit");
    });
});
```

3. **Test Rate Limiting**:
```bash
# Submit 11 requests within 1 hour
for i in {1..11}; do
  curl -X POST http://localhost:5000/contacts/v1 -H "Content-Type: application/json" -d '{ ... }'
done

# 11th should return 429
```

**Estimated Time**: 30 minutes

---

## Testing Guidelines

### Unit Tests

**Pattern**:
```csharp
public class ContactServiceTests
{
    private readonly Mock<ContactDbContext> _mockContext;
    private readonly Mock<IUploadServiceClient> _mockUploadClient;
    private readonly Mock<ICountryServiceClient> _mockCountryClient;
    private readonly ContactService _sut;

    public ContactServiceTests()
    {
        _mockContext = new Mock<ContactDbContext>();
        _mockUploadClient = new Mock<IUploadServiceClient>();
        _mockCountryClient = new Mock<ICountryServiceClient>();
        _sut = new ContactService(_mockContext.Object, _mockUploadClient.Object, _mockCountryClient.Object);
    }

    [Fact]
    public async Task CreateContact_ValidRequest_ReturnsContactMessage()
    {
        // Arrange
        var request = new CreateContactMessageRequest { /* ... */ };
        _mockCountryClient.Setup(x => x.ValidateCountryExistsAsync(It.IsAny<int>(), default))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.CreateContactMessageAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Email.Should().Be(request.Email);
    }
}
```

### Integration Tests

**Pattern**:
```csharp
public class ContactSubmissionTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly PostgreSqlContainer _postgres;

    public ContactSubmissionTests(WebApplicationFactory<Program> factory)
    {
        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:18")
            .Build();

        _postgres.StartAsync().Wait();

        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace with test database
                var connectionString = _postgres.GetConnectionString();
                services.AddDbContext<ContactDbContext>(options =>
                    options.UseNpgsql(connectionString));
            });
        }).CreateClient();
    }

    [Fact]
    public async Task POST_Contacts_ValidRequest_Returns201()
    {
        // Arrange
        var request = new CreateContactMessageRequest { /* ... */ };

        // Act
        var response = await _client.PostAsJsonAsync("/contacts/v1", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
```

---

## Troubleshooting

### Issue: Cannot connect to PostgreSQL

**Solution**:
```bash
# Check if PostgreSQL is running
docker ps | grep postgres

# Check connection
psql -h localhost -p 5432 -U postgres -d contact_app_db

# Restart container
docker-compose restart postgres
```

### Issue: Migration fails with "relation already exists"

**Solution**:
```bash
# Drop database and recreate
docker-compose down -v
docker-compose up -d
dotnet ef database update
```

### Issue: Tests fail with "Docker not available"

**Solution**:
- Ensure Docker Desktop is running
- Check Docker socket permissions: `docker ps`
- Increase Docker resource limits (Settings → Resources)

### Issue: Rate limiting not working

**Solution**:
- Verify `RateLimiting` section exists in appsettings.json
- Check `UseRateLimiter()` is called in middleware pipeline (Program.cs)
- Verify `[EnableRateLimiting("ContactPolicy")]` attribute on controller

---

## Useful Commands

### Database Management

```bash
# View current migration status
dotnet ef migrations list --project Maliev.ContactService.Data

# Generate SQL script for migration
dotnet ef migrations script --project Maliev.ContactService.Data --output migration.sql

# Reset database to specific migration
dotnet ef database update MigrationName --project Maliev.ContactService.Data
```

### Docker Commands

```bash
# View logs
docker logs contact-service-db -f

# Connect to PostgreSQL
docker exec -it contact-service-db psql -U postgres -d contact_app_db

# Check database size
docker exec contact-service-db psql -U postgres -c "SELECT pg_size_pretty(pg_database_size('contact_app_db'));"
```

### Kubernetes Development

```bash
# Get pods
kubectl get pods -n maliev-dev

# View logs
kubectl logs -f deployment/maliev-contact-service -n maliev-dev

# Port forward to service
kubectl port-forward -n maliev-dev svc/maliev-contact-service 5000:80

# Port forward to database (direct to pod)
kubectl port-forward -n maliev-dev postgres-cluster-1 5432:5432
```

---

## Next Steps

1. **Read Specifications**: Review [spec.md](./spec.md) for full feature requirements
2. **Review Architecture**: Check [plan.md](./plan.md) for technical decisions
3. **Study Contracts**: Understand API contracts in [contracts/](./contracts/)
4. **Implement Gaps**: Follow task breakdown in [tasks.md](./tasks.md) (generated by `/speckit.tasks`)
5. **Submit PR**: Follow [CLAUDE.md](../../CLAUDE.md) for PR guidelines

---

## Resources

- **Specification**: [spec.md](./spec.md)
- **Implementation Plan**: [plan.md](./plan.md)
- **API Contracts**: [contracts/](./contracts/)
- **Architecture Guidelines**: [CLAUDE.md](../../CLAUDE.md)
- **.NET 9 Documentation**: https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-9
- **EF Core Documentation**: https://learn.microsoft.com/en-us/ef/core/
- **ASP.NET Core Documentation**: https://learn.microsoft.com/en-us/aspnet/core/

---

## Getting Help

- **GitHub Issues**: Report bugs or request features
- **Team Chat**: Maliev engineering Slack/Teams channel
- **Documentation**: Check CLAUDE.md for project conventions
- **Code Review**: Tag appropriate team members in PR
