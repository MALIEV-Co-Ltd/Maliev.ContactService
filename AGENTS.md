# Maliev.ContactService Development Guidelines

This document provides essential information for AI agents and developers working on the `Maliev.ContactService` repository.

## 1. Build, Lint, and Test Commands

### Build
- **Build Solution:** `dotnet build`
- **Build API only:** `dotnet build Maliev.ContactService.Api/Maliev.ContactService.Api.csproj`
- **Build in Release mode:** `dotnet build -c Release`

### Test
- **Run All Tests:** `dotnet test`
- **Run Specific Test Project:** `dotnet test Maliev.ContactService.Tests/Maliev.ContactService.Tests.csproj`
- **Run a Single Test:**
  ```bash
  dotnet test --filter "FullyQualifiedName~Maliev.ContactService.Tests.Controllers.ContactsControllerTests.CreateContactMessage_Should_Return_Created_Result"
  ```
  *Tip: You can usually match by method name if unique:*
  ```bash
  dotnet test --filter "Display=CreateContactMessage_Should_Return_Created_Result"
  ```
- **Watch Tests (Iterative Development):** `dotnet watch test`

### Linting & Formatting
- **Check Formatting:** `dotnet format --verify-no-changes`
- **Fix Formatting:** `dotnet format`
- **Note:** The project treats warnings as errors (`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`). ensure code compiles without warnings.

## 2. Code Style & Conventions

### General
- **Framework:** .NET 9.0 / .NET 10.0 (Check `.csproj` for `net10.0` or `net9.0`)
- **Language:** C# 12+ features are encouraged (Records, required properties, etc.)
- **Nullable Reference Types:** Enabled (`<Nullable>enable</Nullable>`). Handle nullability explicitly.

### Naming Conventions
- **Classes/Methods/Properties:** `PascalCase`
- **Local Variables/Parameters:** `camelCase`
- **Private Fields:** `_camelCase` (e.g., `_contactService`)
- **Interfaces:** `IPascalCase` (e.g., `IContactService`)
- **Async Methods:** Suffix with `Async` (e.g., `CreateContactMessageAsync`)

### Formatting
- **Braces:** Allman style (braces on new lines).
- **Indentation:** 4 spaces.
- **Using Directives:** Place at the top of the file. `ImplicitUsings` is enabled, so common `System` namespaces are not required.

### Project Structure & Layering
- **Api (`Maliev.ContactService.Api`):**
  - **Controllers:** Handle HTTP requests, input validation, and mapping to/from DTOs.
  - **Services:** Business logic.
  - **Models:** DTOs (Data Transfer Objects) used for API communication.
  - **Consumers:** MassTransit consumers for event handling.
- **Data (`Maliev.ContactService.Data`):**
  - **DbContexts:** EF Core database context.
  - **Models:** Database entities.
  - **Migrations:** EF Core migrations.
- **Tests (`Maliev.ContactService.Tests`):**
  - **Controllers:** Unit tests for controllers.
  - **Services:** Unit tests for services.

### Coding Patterns
- **Dependency Injection:** Use Constructor Injection.
- **DTOs:** Use `required` modifier for mandatory properties in DTOs.
  ```csharp
  public class ContactMessageDto
  {
      public required string Email { get; set; }
  }
  ```
- **Controller Actions:**
  - Return `Task<ActionResult<T>>`.
  - Use `[Produces("application/json")]`.
  - Use XML comments for Swagger documentation.
  - Wrap logic in `try-catch` blocks where appropriate, logging errors and returning 500 status codes for unhandled exceptions.
- **Entity Framework:**
  - Use `AsNoTracking()` for read-only queries to improve performance.
  - Use `Async` methods for all database operations (`ToListAsync`, `FirstOrDefaultAsync`).
  - **`Microsoft.EntityFrameworkCore.Design` package is PROHIBITED in Api project.** It must only be used in the Infrastructure project where migrations are located. Adding this package to Api will break the build.
  - When creating migrations: `dotnet ef migrations add <Name> --project Maliev.ContactService.Infrastructure --startup-project Maliev.ContactService.Infrastructure`

### Error Handling
- Use custom exceptions for domain errors (e.g., `NotFoundException`).
- Controllers should catch specific exceptions to return appropriate HTTP status codes (e.g., `NotFoundException` -> 404).
- Log exceptions with relevant context parameters.

## 3. Testing Guidelines

### Unit Tests
- **Framework:** xUnit
- **Mocking:** Moq (`Mock<T>`)
- **Structure:** `Arrange`, `Act`, `Assert` comments are common but not strictly enforced if the code is clear.
- **Naming:** `MethodName_Condition_ExpectedResult` (e.g., `GetContactMessage_Should_Return_NotFound_When_Not_Exists`).

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

#### Key Rules
- Use `BaseIntegrationTestFactory<TProgram, TDbContext>` for integration tests (real Testcontainers, never InMemoryDatabase)
- Test naming: `MethodName_StateUnderTest_ExpectedBehavior`
- Minimum 80% code coverage
- Use `[Fact]` for single cases, `[Theory]` for parameterized tests

> Full ecosystem test strategy: `Maliev.Aspire.Tests/TEST_PLAN.md`

## 4. Environment & Configuration
- **Secrets:** Managed via `UserSecrets` in development (`maliev-contactservice-api`).
- **Database:** PostgreSQL (via `Npgsql`).
- **Messaging:** RabbitMQ (via `MassTransit`).
- **Caching:** Redis.
- **Aspire:** The project is part of a .NET Aspire orchestration.

## 5. File System Operations (Agent Specific)
- **Paths:** ALWAYS use absolute paths when reading/writing files.
- **Verification:** Always verify file existence before reading.
- **Safety:** Do not delete files unless explicitly instructed.

## 6. Cursor / Copilot Rules
- **Proactiveness:** Always verify changes with a successful build.
- **Assumptions:** Never assume changes will not break the build.
- **Context:** Read surrounding code to understand existing patterns before modifying.


## Git & Version Control — Mandatory Rules

### 🚨 CRITICAL: Always Commit Code Changes (Non-Negotiable)
- **You MUST commit your changes to the local repository after completing any meaningful unit of work.**
- **Never accumulate uncommitted changes.** Do not wait until end of session or until something breaks.
- **Commit early and often** — if a change is meaningful (even a small fix or refactor), commit it.
- **You do NOT need to push to remote** — local commits are sufficient to protect against accidental loss.
- **If you are unsure whether to commit, commit anyway.** Extra commits are harmless; lost work is irreversible.
- This rule applies even if you are just "testing" or "exploring" — use git branches to isolate experimental work and commit those changes too.

### 🚨 CRITICAL: Never Use `git checkout` to Restore Broken Files
- **NEVER use `git checkout` to restore or recover files.** This operation discards uncommitted changes permanently and will result in data loss.
- **To undo/recover from broken files: first commit your current changes, then use `git revert` or `git reset --soft` to safely undo.**

## Database & EF Core — Mandatory Rules

### EF Core Design Package
- ❌ `Microsoft.EntityFrameworkCore.Design` MUST NOT be in Api projects
- ✅ It belongs ONLY in the Infrastructure (or Data) project where migrations live
- Migration commands must target Infrastructure as both project and startup-project (since EF Core Design package is in Infrastructure):
  ```
  dotnet ef migrations add <Name> --project Maliev.<Domain>Service.Infrastructure --startup-project Maliev.<Domain>Service.Infrastructure
  ```

### PostgreSQL xmin Concurrency — Mandatory Pattern
Use shadow property ONLY. Never add a Xmin/xmin property to domain entities.
```csharp
entity.Property<uint>("xmin").HasColumnType("xid").IsRowVersion();
```
- ❌ Never use `UseXminAsConcurrencyToken()` (removed in Npgsql EF v7)
- ❌ Never use entity property `public uint Xmin { get; set; }` or `public uint xmin { get; set; }`
- ❌ Never use `.Ignore(e => e.Xmin)` — remove the entity property instead
