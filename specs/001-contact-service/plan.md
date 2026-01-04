# Implementation Plan: Contact Submission Service

**Branch**: `001-contact-service` | **Date**: 2025-10-29 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-contact-service/spec.md`

**Note**: This plan outlines the implementation strategy for completing the Contact Service specification gaps and establishing the complete technical architecture.

## Summary

The Contact Submission Service is a .NET 9 microservice that handles customer contact inquiries from web and other channels. It provides REST API endpoints for submitting contact forms with optional file attachments, integrates with Country Service for validation and Upload Service for file storage, and provides administrative endpoints for inquiry management.

The service is already partially implemented but requires completion of Country Service integration, duplicate inquiry prevention, rate limiting configuration, and other gaps identified in the specification. The implementation follows Clean Architecture principles with API, Data, and Tests projects, deployed via GitOps to Kubernetes (GKE) with PostgreSQL persistence.

## Technical Context

**Language/Version**: C# / .NET 10.0
**Primary Dependencies**:
  - ASP.NET Core 9.0 (Web API framework)
  - Entity Framework Core 9.0.10 (ORM)
  - Npgsql.EntityFrameworkCore.PostgreSQL 9.0.4 (PostgreSQL provider)
  - FluentValidation 11.5.1 (Request validation)
  - Serilog 8.0.2 (Structured logging to JSON stdout)
  - Microsoft.AspNetCore.Authentication.JwtBearer 9.0.8 (JWT RSA authentication)
  - Scalar (OpenAPI documentation UI)
  - Polly (HTTP resilience and retry policies)

**Storage**: PostgreSQL 18 (via CloudNativePG on GKE)
**Testing**:
  - xUnit (unit and integration testing framework)
  - FluentAssertions 8.6.0 (assertion library)
  - Moq 4.20.72 (mocking framework)
  - Testcontainers.PostgreSQL (integration test containers)
  - Testcontainers.Kafka (for future event-driven features)

**Target Platform**: Kubernetes (Google Kubernetes Engine) - Linux containers
**Project Type**: Microservice WebAPI (3-project solution: Api, Data, Tests)
**Performance Goals**:
  - < 2 seconds response time for contact inquiry submission (including file uploads)
  - Support 100 concurrent inquiries without degradation
  - < 3 seconds for admin query operations
  - < 10 seconds for file downloads up to 25MB

**Constraints**:
  - Stateless service design (no in-memory state, scale horizontally)
  - All secrets via Google Secret Manager (/mnt/secrets mount)
  - JWT authentication with RSA public key validation (asymmetric)
  - Rate limiting: 10 inquiries per IP per hour (fixed window)
  - File size limit: 25MB per file, max 10 files per inquiry
  - Database migrations via dotnet ef CLI (not automatic on startup)
  - Health checks: /contact/liveness (startup), /contact/readiness (database connectivity)

**Scale/Scope**:
  - Expected volume: 100-500 inquiries/day initially
  - Admin users: 5-10 support staff
  - Database: Single PostgreSQL database (contact_app_db)
  - API versioning: v1 prefix for all endpoints
  - Deployment: Development, Staging, Production environments via ArgoCD

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Note**: No formal constitution file exists for this project. The following checks are based on the architectural standards documented in CLAUDE.md and the technical requirements provided.

### Architectural Standards Compliance

✅ **Clean Architecture**: Service follows 3-layer separation (Api → Data → Tests)
✅ **Microservice Pattern**: Stateless, independently deployable, single responsibility
✅ **Security**: JWT authentication, secrets via Google Secret Manager (no hardcoded credentials)
✅ **Testing**: Unit tests (xUnit), integration tests (Testcontainers), no test-first violations
✅ **Observability**: Serilog structured logging, health checks, Prometheus metrics ready
✅ **CI/CD**: GitOps deployment via ArgoCD, three-branch workflow (develop/staging/main)
✅ **Database Migrations**: EF Core migrations, manual application (not automatic on startup)
✅ **Caching**: Simple AddMemoryCache() without SizeLimit (avoids common configuration error)
✅ **Dependencies**: Standard package versions from CLAUDE.md (no conflicting versions)
✅ **Zero Warnings**: Build must complete with zero warnings (treat warnings as errors)

### No Constitution Violations

No complexity violations to justify. The implementation follows standard microservice patterns without introducing unnecessary abstractions or architectural complexity.

**GATE STATUS**: ✅ PASS - Proceed to Phase 0 (Research)

## Project Structure

### Documentation (this feature)

```text
specs/001-contact-service/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output - technical unknowns and research
├── data-model.md        # Phase 1 output - entity models, relationships, migrations
├── quickstart.md        # Phase 1 output - developer onboarding guide
├── contracts/           # Phase 1 output - API request/response contracts
│   ├── public-api.md    # Customer-facing submission endpoint
│   ├── admin-api.md     # Admin management endpoints
│   └── external-apis.md # Country Service & Upload Service integration
├── checklists/          # Quality validation checklists
│   └── requirements.md  # Spec completeness checklist (already exists)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

The Contact Service follows the standard Maliev microservice 3-project structure:

```text
Maliev.ContactService/
├── .github/
│   └── workflows/               # CI/CD pipelines
│       ├── ci-develop.yml       # Development environment deployment
│       ├── ci-staging.yml       # Staging environment deployment
│       └── ci-main.yml          # Production environment deployment
│
├── Maliev.ContactService.Api/   # Web API project (entry point)
│   ├── Configurations/          # Configuration models (appsettings binding)
│   │   └── UploadServiceOptions.cs
│   ├── Controllers/             # REST API controllers
│   │   └── ContactsController.cs
│   ├── Exceptions/              # Custom exception types
│   │   ├── CountryServiceException.cs (TO BE CREATED)
│   │   ├── DuplicateSubmissionException.cs (TO BE CREATED)
│   │   └── UploadServiceException.cs
│   ├── HealthChecks/            # Custom health check implementations
│   ├── Middleware/              # Custom middleware (logging, errors)
│   ├── Models/                  # DTOs and request/response models
│   │   ├── CreateContactMessageRequest.cs
│   │   ├── CreateContactFileRequest.cs
│   │   ├── ContactMessageResponse.cs
│   │   ├── UpdateContactStatusRequest.cs
│   │   └── CountryServiceOptions.cs (TO BE CREATED)
│   ├── Services/                # Business logic services
│   │   ├── IContactService.cs
│   │   ├── ContactService.cs
│   │   ├── IUploadServiceClient.cs
│   │   ├── UploadServiceClient.cs
│   │   ├── ICountryServiceClient.cs (TO BE CREATED)
│   │   └── CountryServiceClient.cs (TO BE CREATED)
│   ├── Program.cs               # Application entry point, DI configuration
│   ├── appsettings.json         # Configuration (non-secret values)
│   └── appsettings.Development.json
│
├── Maliev.ContactService.Data/  # Data access layer
│   ├── DbContexts/              # EF Core database contexts
│   │   └── ContactDbContext.cs
│   ├── Migrations/              # EF Core migrations
│   │   ├── 20240913_InitialCreate.cs
│   │   └── AddCountryId.cs (TO BE CREATED)
│   ├── Models/                  # Entity models (database schema)
│   │   ├── ContactMessage.cs
│   │   └── ContactFile.cs
│   └── Data/                    # Seed data, configurations
│
├── Maliev.ContactService.Tests/ # Test project
│   ├── Controllers/             # Controller unit tests
│   │   └── ContactsControllerTests.cs
│   ├── Services/                # Service unit tests
│   │   ├── ContactServiceTests.cs
│   │   ├── UploadServiceClientTests.cs
│   │   └── CountryServiceClientTests.cs (TO BE CREATED)
│   ├── Integration/             # Integration tests with Testcontainers
│   │   ├── ContactSubmissionTests.cs
│   │   └── AdminWorkflowTests.cs
│   └── Models/                  # Model/DTO validation tests
│
├── .specify/                    # Speckit configuration
│   ├── memory/                  # Specification memory
│   │   └── constitution.md
│   ├── scripts/                 # Specification scripts
│   └── templates/               # Specification templates
│
├── specs/                       # Feature specifications
│   └── 001-contact-service/    # This feature
│
├── Dockerfile                   # Multi-stage Docker build
├── .dockerignore
├── Maliev.ContactService.sln    # Solution file
├── README.md                    # Service documentation
├── CLAUDE.md                    # Architectural guidelines
└── .gitignore
```

**Structure Decision**:

This service uses the standard Maliev microservice pattern with three projects:

1. **Api Project**: Contains all HTTP-facing code (controllers, middleware, DTOs, service interfaces). This is the entry point and orchestration layer.

2. **Data Project**: Contains all database-facing code (EF Core contexts, entity models, migrations). This is the persistence layer with no business logic.

3. **Tests Project**: Contains all test code (unit, integration, contract tests) using xUnit, FluentAssertions, Moq, and Testcontainers.

This structure enforces Clean Architecture principles where the Api layer depends on Data, but Data has no knowledge of Api. The Tests project references both Api and Data to test the full stack.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

No complexity violations detected. The implementation follows standard patterns without unnecessary abstractions.
