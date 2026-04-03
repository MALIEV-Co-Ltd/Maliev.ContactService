# Maliev.ContactService Development Guidelines

This document provides essential information for AI agents and developers working on the `Maliev.ContactService` repository.

> **Workspace root** `B:\maliev` contains **41 independent git repos**. Each `Maliev.*` folder and `maliev-gitops` is its own repo. There is no single repo at the workspace root. Always work within the target service directory.

---

## 1. Build, Lint, and Test Commands

All commands run from within `B:\maliev\Maliev.ContactService`.

### Build
```powershell
# Build (treats warnings as errors — all must be fixed)
dotnet build Maliev.ContactService.slnx

# Build in Release mode
dotnet build Maliev.ContactService.slnx -c Release
```

### Test
```powershell
# Run all tests
dotnet test Maliev.ContactService.slnx --verbosity normal

# Run a single test method
dotnet test --filter "FullyQualifiedName~ContactMessageTests.CreateContactMessage_Should_Return_Created_Result"

# Run all tests in a class
dotnet test --filter "FullyQualifiedName~ContactMessageTests"

# Run with code coverage
dotnet test Maliev.ContactService.slnx --collect:"XPlat Code Coverage"
```

### Linting & Formatting
```powershell
# Format check
dotnet format Maliev.ContactService.slnx

# Format fix
dotnet format Maliev.ContactService.slnx
```

**Note:** The project treats warnings as errors (`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`). Ensure code compiles without warnings.

### EF Core Migrations
```powershell
dotnet ef migrations add <Name> --project Maliev.ContactService.Infrastructure --startup-project Maliev.ContactService.Infrastructure
```

---

## 2. Code Style & Conventions

### Workspace Structure
```
Maliev.ContactService/
├── Maliev.ContactService.Api/              # Controllers, Consumers, Middleware
├── Maliev.ContactService.Application/      # Use cases, DTOs, Interfaces, Handlers
├── Maliev.ContactService.Domain/           # Entities, value objects, domain interfaces
├── Maliev.ContactService.Infrastructure/   # EF Core DbContext, repositories, HTTP clients
├── Maliev.ContactService.Tests/            # Unit + Integration tests (xUnit)
├── Directory.Build.props                   # Central package versioning
└── Maliev.ContactService.slnx             # Solution file (.slnx preferred over .sln)
```

### C# Naming & Formatting
- **Namespaces**: File-scoped (`namespace Maliev.ContactService.Domain.Entities;`)
- **Classes/Methods/Properties**: `PascalCase`
- **Private fields**: `_camelCase` (underscore prefix, e.g., `_contactService`)
- **Parameters/locals**: `camelCase`
- **Async methods**: Suffix with `Async` (e.g., `CreateContactMessageAsync`)
- **Interfaces**: Prefix with `I` (e.g., `IContactService`)
- **Permissions**: GCP-style `{domain}.{plural-resource}.{action}` as `public const string` in a `Permissions` static class
  - Valid: `contact.contact-messages.create`, `contact.contact-messages.read`
  - Invalid: `contact.contact-message.create` (singular), `contact.create` (missing resource)
- **XML docs**: Required on ALL public methods and properties
- **Nullable**: Enabled (`<Nullable>enable</Nullable>`). Use `?` explicitly
- **Imports**: System first, then third-party, then local. Alphabetize within groups. Remove unused `using`
- **Braces**: Allman style (new line) for methods and control structures. Expression-bodied for properties/accessors
- **Indentation**: 4 spaces, LF line endings, UTF-8, trim trailing whitespace

### C# Patterns
- **DI**: Constructor injection with `private readonly` fields
- **Controllers**: `[ApiController]`, `[ApiVersion("1")]`, `[Route("contact/v{version:apiVersion}")]`
- **Logging**: `ILogger<T>` with structured placeholders (never interpolate): `_logger.LogInformation("Processing {ContactId}", contactId)`
- **Error handling**: Global exception middleware. Return `ProblemDetails` / `ErrorResponse` DTOs. Never expose stack traces
- **JSON**: Check existing conventions in this service for naming policy
- **Manual mapping**: Static extension methods (`ToDto()`, `ToEntity()`). AutoMapper is banned
- **Validation**: `System.ComponentModel.DataAnnotations` on DTOs. FluentValidation is banned
- **DTOs**: Use `required` modifier for mandatory properties in DTOs
  ```csharp
  public class ContactMessageDto
  {
      public required string Email { get; set; }
  }
  ```

---

## 3. Banned Libraries (Build Will Fail)

| Banned | Use Instead |
|--------|-------------|
| AutoMapper | Manual mapping extensions |
| FluentValidation | DataAnnotations or manual validation |
| FluentAssertions | Standard xUnit `Assert.*` |
| Swashbuckle/Swagger | Scalar (at `/contact/scalar`) |
| InMemoryDatabase (EF Core) | Testcontainers with real PostgreSQL |

---

## 4. Testing Guidelines

### Testing Rules
- **Framework**: xUnit with standard `Assert` (`Assert.Equal`, `Assert.NotNull`, etc.)
- **Naming**: `MethodName_StateUnderTest_ExpectedBehavior` or `HTTP_METHOD_Path_Scenario_ExpectedStatus`
- **Coverage**: Minimum 80% per service
- **Integration tests**: `BaseIntegrationTestFactory<TProgram, TDbContext>` with Testcontainers (PostgreSQL, Redis, RabbitMQ). Never InMemoryDatabase
- **System tests** (Tier 3): `AspireTestFixture` with `[Collection("AspireDomainTests")]` — shared AppHost, never one per class
- **Eventual consistency**: Use `TestHelpers.WaitForAsync`. Never `Task.Delay`
- **MassTransit consumers**: Must have consumer tests using `AddMassTransitTestHarness()`
- **Mocking**: Moq (`Mock<T>`) for unit tests
- **Structure**: `Arrange`, `Act`, `Assert` comments are common but not strictly enforced if the code is clear
- **Use `[Fact]`** for single cases, `[Theory]` for parameterized tests

### Example Test
```csharp
[Fact]
public async Task GetContactMessage_Should_Return_Contact_When_Exists()
{
    // Arrange
    var contactId = 1;
    var expectedContact = new ContactMessageDto { Id = contactId, ... };
    _contactServiceMock.Setup(x => x.GetContactMessageByIdAsync(contactId))
        .ReturnsAsync(expectedContact);

    // Act
    var result = await _controller.GetContactMessage(contactId);

    // Assert
    var okResult = Assert.IsType<OkObjectResult>(result.Result);
    Assert.Equal(expectedContact, okResult.Value);
}
```

### Testing Strategy (4-Tier Pyramid Context)

This service's tests cover **Tier 1 (Unit)** and **Tier 2 (Service Integration)** of the Maliev testing pyramid:

| Tier | What to Test | Infrastructure |
|------|-------------|---------------|
| **Unit** | Business logic, domain models, service methods with mocked dependencies | None (mocks only) |
| **Service Integration** | API endpoints, database persistence, permission enforcement, input validation | `BaseIntegrationTestFactory` + Testcontainers (Postgres/Redis/RabbitMQ) |

**Tier 3 (System Integration)** — cross-service workflows and event chains — is tested in `Maliev.Aspire.Tests/`.

> Full ecosystem test strategy: `Maliev.Aspire.Tests/TEST_PLAN.md`

---

## 5. Environment & Configuration
- **Secrets:** Managed via `UserSecrets` in development (`maliev-contactservice-api`). Never hardcoded. Use GCP Secret Manager or environment variables.
- **Database:** PostgreSQL (via `Npgsql`).
- **Messaging:** RabbitMQ (via `MassTransit`).
- **Caching:** Redis.
- **Aspire:** The project is part of a .NET Aspire orchestration.

---

## 6. Mandatory Rules

- **`TreatWarningsAsErrors = true`**: Zero warnings allowed. No suppression
- **`[RequirePermission("contact.contact-messages.action")]`**: On all endpoints, not plain `[Authorize]`
- **API versioning**: All routes versioned (`v1/`)
- **Service prefix**: Routes prefixed with service domain (`/contact`)
- **Scalar docs**: Configured at `/contact/scalar`
- **Secrets**: Never hardcoded. Use GCP Secret Manager or environment variables
- **Async/await**: All the way down. Pass `CancellationToken`
- **EF Core Design package**: Only in Infrastructure project, never in Api
- **PostgreSQL xmin**: Shadow property only — `entity.Property<uint>("xmin").HasColumnType("xid").IsRowVersion()`. Never add entity property
- **Temporary files**: Generate in `/temp` folder, clean up afterwards

---

## 7. File System Operations (Agent Specific)
- **Paths:** ALWAYS use absolute paths when reading/writing files.
- **Verification:** Always verify file existence before reading.
- **Safety:** Do not delete files unless explicitly instructed.

---

## 8. Cursor / Copilot Rules
- **Proactiveness:** Always verify changes with a successful build.
- **Assumptions:** Never assume changes will not break the build.
- **Context:** Read surrounding code to understand existing patterns before modifying.

---

## 9. Git Rules

- Each `Maliev.*` folder is an independent git repo. `cd` into it before git commands
- **Commit early and often** after every meaningful unit of work. Do not accumulate changes
- **Never use `git checkout` to restore files** — commit first, then `git revert` or `git reset --soft`
- Feature branches merged to `develop` via PR. Do not push without being asked

---

## 10. Database & EF Core — Mandatory Rules

### EF Core Design Package
- **`Microsoft.EntityFrameworkCore.Design`** MUST NOT be in Api projects
- It belongs ONLY in the Infrastructure project where migrations live
- Migration commands must target Infrastructure as both project and startup-project:
  ```
  dotnet ef migrations add <Name> --project Maliev.ContactService.Infrastructure --startup-project Maliev.ContactService.Infrastructure
  ```

### PostgreSQL xmin Concurrency — Mandatory Pattern
Use shadow property ONLY. Never add a Xmin/xmin property to domain entities.
```csharp
entity.Property<uint>("xmin").HasColumnType("xid").IsRowVersion();
```
- Never use `UseXminAsConcurrencyToken()` (removed in Npgsql EF v7)
- Never use entity property `public uint Xmin { get; set; }` or `public uint xmin { get; set; }`
- Never use `.Ignore(e => e.Xmin)` — remove the entity property instead
