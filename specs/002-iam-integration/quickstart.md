# Quickstart: Permission-Based Authorization

## Setup Environment

1. **Infrastructure**: Ensure PostgreSQL and Redis are running.
   ```bash
   docker run --name contact-db -e POSTGRES_PASSWORD=password -p 5432:5432 -d postgres
   docker run --name contact-redis -p 6379:6379 -d redis
   ```

2. **Configuration**: Update `appsettings.json` with IAM and Redis settings.
   ```json
   {
     "Authentication": {
       "Jwt": {
         "Authority": "https://iam.maliev.io/realms/maliev",
         "Audience": "contact-service"
       }
     },
     "ConnectionStrings": {
       "Redis": "localhost:6379"
     }
   }
   ```

3. **Database Migration**:
   ```bash
   dotnet ef database update --project Maliev.ContactService.Data
   ```

## Development Workflow

1. **Permissions**: Define permissions in `ContactPermissions.cs`.
2. **Authorization**: Use the `[HasPermission(ContactPermissions.Contacts.Read)]` attribute on controller actions.
3. **Seeding**: Roles and permissions are automatically seeded on startup in Development environment.

## Verification

Run integration tests using Testcontainers:
```bash
dotnet test Maliev.ContactService.Tests
```
The tests will automatically spin up real PostgreSQL and Redis instances to verify the authorization logic.
