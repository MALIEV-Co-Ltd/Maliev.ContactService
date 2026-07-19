# Feature Specification: Permission-Based Authorization Migration

**Feature Branch**: `002-iam-integration`  
**Created**: 2025-12-21  
**Status**: Draft  
**Input**: User description: "Migrating the ContactService to use a permission-based authorization model, defining specific permissions for contacts, communications, and groups, and creating predefined roles like contact-admin, contact-manager, contact-user, and contact-viewer."

## Clarifications

### Session 2025-12-21
- Q: Where does the system retrieve the user's identity from? → A: External JWT/OIDC (e.g., Keycloak, Auth0)
- Q: Where are role-permission mappings stored? → A: Local Database (ContactService stores role-permission mappings)
- Q: What level of audit logging is required for authorization? → A: All Authorization Decisions (Success + Failure)
- Q: How are users assigned to roles? → A: Sync from JWT Claims (External IAM manages assignments)
- Q: What caching strategy should be used for authorization? → A: Distributed Cache (Redis)

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Full System Administration (Priority: P1)

As a system administrator, I want to have full control over all contact, communication, and group operations so that I can manage the entire service without restrictions.

**Why this priority**: Essential for system maintenance and oversight. It represents the highest level of access needed to verify all permissions work correctly.

**Independent Test**: Can be fully tested by attempting every operation (Create, Read, Update, Delete, Merge, Export, Log, Assign) across all modules and verifying they are all allowed.

**Acceptance Scenarios**:

1. **Given** a user with the `contact-admin` role, **When** they attempt to merge duplicate contacts, **Then** the operation is permitted.
2. **Given** a user with the `contact-admin` role, **When** they attempt to delete a contact group, **Then** the operation is permitted.

---

### User Story 2 - Daily Contact Management (Priority: P1)

As a contact user, I want to create, read, and update contacts, and log communications so that I can perform my core job of managing customer relationships.

**Why this priority**: This represents the primary workflow for the majority of service users.

**Independent Test**: Can be tested by verifying that `contact-user` can perform their assigned tasks but cannot perform restricted actions like merging contacts or deleting groups.

**Acceptance Scenarios**:

1. **Given** a user with the `contact-user` role, **When** they create a new contact, **Then** the operation is successful.
2. **Given** a user with the `contact-user` role, **When** they attempt to merge two contacts, **Then** the operation is denied with an "Unauthorized" response.

---

### User Story 3 - View-Only Access (Priority: P2)

As a viewer, I want to read contact details and communication history so that I can stay informed without accidentally modifying any data.

**Why this priority**: Important for stakeholders who need information but should not have write access.

**Independent Test**: Verify that all "Read" operations are allowed while all "Create", "Update", and "Delete" operations are blocked.

**Acceptance Scenarios**:

1. **Given** a user with the `contact-viewer` role, **When** they view a contact's communication history, **Then** the data is displayed.
2. **Given** a user with the `contact-viewer` role, **When** they attempt to update a contact's phone number, **Then** the operation is denied.

---

### Edge Cases

- **Resource Ownership**: Authorization is strictly role-based. A user with the `contact.contacts.read` permission can access all contact records in the system, regardless of who created them.
- **Missing Role**: How does the system handle a registered user who has not been assigned any of the 4 new roles? (Assumption: Access is denied by default).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-000**: System MUST validate user identity via standard OIDC/JWT tokens provided by an external IAM.
- **FR-001**: System MUST define and register 14 granular permissions covering Contacts, Communications, and Groups.
- **FR-002**: System MUST implement 4 predefined roles: `contact-admin`, `contact-manager`, `contact-user`, and `contact-viewer`.
- **FR-003**: System MUST enforce permission checks on all API endpoints related to contacts, communications, and groups.
- **FR-004**: System MUST map permissions to roles according to the defined specification (e.g., `contact-manager` has all permissions except `contacts.merge`).
- **FR-004.1**: Role-permission definitions MUST be persisted in the local ContactService database.
- **FR-004.2**: The system MUST treat permissions as additive when a user is assigned multiple roles.
- **FR-005**: System MUST resolve user-role assignments by extracting specific claims from the validated JWT.
- **FR-006**: System MUST ensure that role/permission changes take effect within a short-term window (e.g., 5 minutes) via a distributed cache (e.g., Redis).
- **FR-007**: System MUST provide clear "Unauthorized" feedback when a permission check fails.
- **FR-008**: System MUST log all authorization decisions (success and failure) for audit purposes.

### Key Entities *(include if feature involves data)*

- **Permission**: A unique string identifier representing a specific action (e.g., `contact.contacts.create`).
- **Role**: A named collection of permissions that can be assigned to users.
- **UserAssignment**: The mapping between a User and one or more Roles.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of the 14 defined permissions are correctly registered in the system.
- **SC-002**: All 4 predefined roles are correctly configured with their specified permission sets.
- **SC-003**: All API endpoints (Contacts, Communications, Groups) return a 403 Forbidden status when accessed by a user lacking the required permission.
- **SC-004**: Authorization overhead adds less than 50ms to the total API response time.