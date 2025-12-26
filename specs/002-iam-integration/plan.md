# Implementation Plan: Permission-Based Authorization Migration (IAM Integration)

**Branch**: `002-iam-integration` | **Date**: 2025-12-21 | **Spec**: [specs/002-iam-integration/spec.md](spec.md)
**Input**: Feature specification from `/specs/002-iam-integration/spec.md`

## Summary

Migrate the ContactService to a granular permission-based authorization model. The system will validate identities using an external OIDC/JWT provider, sync user-role assignments from JWT claims, and enforce authorization using locally stored role-permission mappings. All authorization decisions will be cached in Redis and logged for audit purposes.

## Technical Context

**Language/Version**: C# / .NET 10
**Primary Dependencies**: `Microsoft.AspNetCore.Authentication.JwtBearer`, `StackExchange.Redis`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `Maliev.Aspire.ServiceDefaults`, `Testcontainers`
**Storage**: PostgreSQL (for role-permission definitions and audit logs)
**Testing**: xUnit, Testcontainers (Real PostgreSQL and Redis)
**Target Platform**: Linux (Docker/Kubernetes)
**Project Type**: Web API (ASP.NET Core)
**Performance Goals**: Authorization overhead < 50ms (Spec SC-004)
**Constraints**: Distributed cache (Redis) mandatory, Audit log (Success + Failure) mandatory, No AutoMapper/FluentValidation/FluentAssertions
**Scale/Scope**: 13 permissions, 4 predefined roles, impact on all existing Contact/Communication/Group endpoints

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| Service Autonomy | PASS | Uses local DB for role-permission mappings. |
| Explicit Contracts | PASS | API contracts will be updated/added to `contracts/`. |
| Test-First Development | PASS | Integration tests using Testcontainers required. |
| Real Infrastructure Testing | PASS | Using real PostgreSQL and Redis via Testcontainers. |
| Auditability & Observability | PASS | Audit logs (success/failure) and business metrics required. |
| Secrets Management | PASS | Using Google Secret Manager (via Aspire/Environment). |
| Zero Warnings Policy | PASS | Mandatory. |
| Clean Project Artifacts | PASS | No src/ folder, flat project structure. |
| Docker Best Practices | PASS | Dockerfile in Api project, using 'app' user. |
| No AutoMapper/FluentValidation/FluentAssertions | PASS | Explicit mapping and xUnit Assert only. |

## Project Structure

### Documentation (this feature)

```text
specs/002-iam-integration/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
└── tasks.md             # Phase 2 output (generated later)
```

### Source Code (repository root)

```text
Maliev.ContactService.Api/
├── Controllers/         # Updated with [Authorize] and Permission attributes
├── Services/            # AuthorizationService, IAMRegistration
├── Models/              # DTOs
└── Program.cs           # Auth configuration

Maliev.ContactService.Data/
├── DbContexts/          # Migrated for Role/Permission entities
└── Models/              # Permission, Role, AuditLog entities

Maliev.ContactService.Tests/
├── Integration/         # Testcontainers-based auth tests
└── Services/            # Unit tests for mapping/logic
```

**Structure Decision**: Flat project structure as per MALIEV constitution.

## Complexity Tracking

*No violations identified.*