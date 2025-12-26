# Tasks: Permission-Based Authorization Migration

**Input**: Design documents from `/specs/002-iam-integration/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [x] T001 Configure `appsettings.json` with JWT Authority and Redis connection strings
- [x] T002 Add `Microsoft.AspNetCore.Authentication.JwtBearer` and `StackExchange.Redis` NuGet packages to `Maliev.ContactService.Api/Maliev.ContactService.Api.csproj`
- [x] T003 [P] Add `Npgsql.EntityFrameworkCore.PostgreSQL` to `Maliev.ContactService.Data/Maliev.ContactService.Data.csproj` if not already present

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure for permission-based authorization and audit logging

- [x] T004 Create `Permission`, `Role`, and `RolePermission` entities in `Maliev.ContactService.Data/Models/`
- [x] T005 Create `AuditLog` entity in `Maliev.ContactService.Data/Models/AuditLog.cs`
- [x] T006 Update `ContactDbContext` in `Maliev.ContactService.Data/DbContexts/ContactDbContext.cs` with new Auth entities and relationships
- [x] T007 Create and apply EF Core Migration for new authorization schema
- [x] T008 [P] Define 14 permissions in `Maliev.ContactService.Api/Services/Auth/ContactPermissions.cs`
- [x] T009 [P] Define 4 predefined roles and their permission mappings in `Maliev.ContactService.Api/Services/Auth/ContactPredefinedRoles.cs`
- [x] T010 Implement `PermissionRequirement` and `PermissionHandler` in `Maliev.ContactService.Api/Services/Auth/`
- [x] T011 Implement custom `AuthorizationPolicyProvider` in `Maliev.ContactService.Api/Services/Auth/PermissionPolicyProvider.cs`
- [x] T012 Implement `AuditLogMiddleware` for recording authorization decisions in `Maliev.ContactService.Api/Middleware/AuditLogMiddleware.cs`
- [x] T013 Implement Redis-backed permission caching in `Maliev.ContactService.Api/Services/Auth/PermissionCacheService.cs`
- [x] T014 Implement role-sync logic from JWT claims in `Maliev.ContactService.Api/Services/Auth/UserRoleResolver.cs`
- [x] T015 Configure JWT Authentication and custom Authorization Policy Provider in `Maliev.ContactService.Api/Program.cs`
- [x] T016 Implement `DataSeeder` to register 14 permissions and 4 roles in `Maliev.ContactService.Api/Services/Auth/DataSeeder.cs`

**Checkpoint**: Authorization foundation ready - user stories can now be implemented.

---

## Phase 3: User Story 1 - Full System Administration (Priority: P1) 🎯 MVP

**Goal**: Enable `contact-admin` role with full access to all operations including merge and delete.

**Independent Test**: Verify `contact-admin` can successfully call `DELETE` and `PUT (status)` endpoints.

### Tests for User Story 1

- [x] T017 [P] [US1] Create integration test verifying `contact-admin` can access `DELETE /contacts/{id}` in `Maliev.ContactService.Tests/Integration/Auth/AdminAccessTests.cs`
- [x] T018 [P] [US1] Create integration test verifying `contact-admin` can access `GET /contacts` in `Maliev.ContactService.Tests/Integration/Auth/AdminAccessTests.cs`

### Implementation for User Story 1

- [x] T019 [US1] Apply `[HasPermission(ContactPermissions.Contacts.Delete)]` to `DeleteContactMessage` in `Maliev.ContactService.Api/Controllers/ContactsController.cs`
- [x] T020 [US1] Apply `[HasPermission(ContactPermissions.Contacts.Update)]` to `UpdateContactStatus` in `Maliev.ContactService.Api/Controllers/ContactsController.cs`
- [x] T021 [US1] Update `PermissionHandler` to correctly resolve `contact-admin` role from JWT claims

**Checkpoint**: User Story 1 fully functional and testable independently.

---

## Phase 4: User Story 2 - Daily Contact Management (Priority: P1)

**Goal**: Enable `contact-user` role to manage contacts but restrict administrative actions like delete/merge.

**Independent Test**: Verify `contact-user` can create/update contacts but receives 403 on delete.

### Tests for User Story 2

- [x] T022 [P] [US2] Create integration test verifying `contact-user` can access `POST /contacts` in `Maliev.ContactService.Tests/Integration/Auth/UserAccessTests.cs`
- [x] T023 [P] [US2] Create integration test verifying `contact-user` receives 403 on `DELETE /contacts/{id}` in `Maliev.ContactService.Tests/Integration/Auth/UserAccessTests.cs`

### Implementation for User Story 2

- [x] T024 [US2] Update `ContactsController` actions with appropriate `[HasPermission]` attributes for `contact-user` permissions
- [x] T025 [US2] Ensure `PermissionHandler` correctly denies restricted permissions for the `contact-user` role

**Checkpoint**: User Story 2 functional and correctly restricted.

---

## Phase 5: User Story 3 - View-Only Access (Priority: P2)

**Goal**: Enable `contact-viewer` role with read-only access.

**Independent Test**: Verify `contact-viewer` can call GET endpoints but receives 403 on any write operations.

### Tests for User Story 3

- [x] T026 [P] [US3] Create integration test verifying `contact-viewer` can access `GET /contacts/{id}` in `Maliev.ContactService.Tests/Integration/Auth/ViewerAccessTests.cs`
- [x] T027 [P] [US3] Create integration test verifying `contact-viewer` receives 403 on `PUT /contacts/{id}/status` in `Maliev.ContactService.Tests/Integration/Auth/ViewerAccessTests.cs`

### Implementation for User Story 3

- [x] T028 [US3] Apply `[HasPermission(ContactPermissions.Contacts.Read)]` to all GET endpoints in `Maliev.ContactService.Api/Controllers/ContactsController.cs`

**Checkpoint**: All 3 user stories functional and independently testable.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final audit logging, performance, and documentation.

- [x] T029 [P] Implement business metrics for authorization success/failure rates in `Maliev.ContactService.Api/Services/Auth/AuthMetricsService.cs`
- [x] T030 Ensure `AuditLogMiddleware` correctly captures `ClientIp` and `Reason` for all requests
- [x] T031 [P] Run all integration tests using Testcontainers (Postgres + Redis) to confirm < 50ms overhead
- [x] T032 Update `README.md` with new authorization requirements and role definitions
- [x] T033 [P] Verify/Create mandatory `.github/CODEOWNERS` file as per constitution Principle IX

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies.
- **Foundational (Phase 2)**: Depends on Setup (Phase 1). BLOCKS all user stories.
- **User Stories (Phase 3-5)**: All depend on Foundational (Phase 2). Can be implemented in parallel if needed.
- **Polish (Phase 6)**: Depends on completion of all User Stories.

### Parallel Opportunities

- T002 and T003 (NuGet setup)
- T008 and T009 (Defining constants)
- All test tasks marked [P] (T017, T018, T022, T023, T026, T027)
- US1, US2, and US3 implementation phases (Phases 3, 4, and 5) can run in parallel once Phase 2 is complete.

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (Critical)
3. Complete Phase 3: User Story 1 (Admin Access)
4. Validate with `AdminAccessTests.cs`.

### Incremental Delivery

1. Deploy Foundation + US1 (MVP)
2. Add US2 (Daily Management)
3. Add US3 (View-Only)
4. Final Polish and Audit review.
