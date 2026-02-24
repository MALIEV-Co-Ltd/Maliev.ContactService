# Research: Permission-Based Authorization Migration

## Decision: JWT Claim Mapping
**Rationale**: To sync user roles from an external IAM, we must identify the specific claim name (e.g., `roles`, `groups`, or `realm_access.roles` for Keycloak).
**Alternatives considered**: 
- Custom assignment in ContactService DB (Rejected: violates "Sync from JWT" requirement).
- LDAP integration (Rejected: out of scope for OIDC/JWT focus).

## Decision: Permission Checking Mechanism
**Rationale**: Using a custom `AuthorizationPolicyProvider` and `IAuthorizationRequirement` is the standard .NET approach for dynamic, permission-based auth.
**Alternatives considered**: 
- Hardcoded `[Authorize(Roles = "...")]` (Rejected: not granular enough for 13 permissions).
- Custom Action Filter (Rejected: less integrated with .NET identity pipeline).

## Decision: Redis Caching Strategy
**Rationale**: Cache the list of permissions for a given `user_id` in Redis using a key like `auth:permissions:{user_id}` with a 5-minute TTL.
**Alternatives considered**: 
- In-memory cache (Rejected: doesn't support horizontal scaling).
- Database query every time (Rejected: might exceed 50ms overhead goal).

## Decision: Audit Logging
**Rationale**: Use a dedicated `AuditLog` entity in PostgreSQL and a background service or `Middleware` to record results without blocking API response.
**Alternatives considered**: 
- Structured logs only (Rejected: harder to query for compliance reports).
- Synchronous DB writes (Rejected: impacts performance).

## Decision: Initial Seeding (IAM Registration)
**Rationale**: Use EF Core Migrations or a `DataSeeder` on startup to ensure the 13 permissions and 4 roles are registered in the local DB.
**Alternatives considered**: 
- Manual SQL scripts (Rejected: error-prone across environments).
