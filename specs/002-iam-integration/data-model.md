# Data Model: Permission-Based Authorization

## Entities

### Permission
Represents a granular action allowed in the system.
- **Id**: `Guid` (Primary Key)
- **Name**: `string` (Unique, e.g., "contact.contacts.create")
- **Description**: `string`
- **Category**: `string` (e.g., "Contacts", "Communications", "Groups")

### Role
A collection of permissions.
- **Id**: `Guid` (Primary Key)
- **Name**: `string` (Unique, e.g., "contact-admin")
- **Description**: `string`

### RolePermission
Join table for many-to-many relationship between Roles and Permissions.
- **RoleId**: `Guid` (Foreign Key)
- **PermissionId**: `Guid` (Foreign Key)

### AuditLog
Records every authorization decision.
- **Id**: `Guid` (Primary Key)
- **Timestamp**: `DateTimeOffset`
- **UserId**: `string` (Subject from JWT)
- **Action**: `string` (Requested permission)
- **Resource**: `string` (API Path / Entity ID)
- **Result**: `bool` (Success/Failure)
- **Reason**: `string` (Optional, e.g., "Missing required permission")
- **ClientIp**: `string`

## Relationships
- **Role** 1:N **RolePermission** N:1 **Permission**
- **AuditLog** is a standalone entity (referenced to UserId via string)

## Validation Rules
- Permission names must follow the `contact.{module}.{action}` pattern.
- Role names must be unique.
- Audit logs are immutable (no Update/Delete).
