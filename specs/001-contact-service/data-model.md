# Data Model: Contact Submission Service

**Feature**: Contact Submission Service
**Branch**: `001-contact-service`
**Date**: 2025-10-29

## Overview

The Contact Service data model consists of two core entities: `ContactMessage` (customer inquiries) and `ContactFile` (file attachments). The model supports the complete inquiry lifecycle from submission through admin resolution.

## Entity Relationship Diagram

```
┌─────────────────────────────────────────┐
│           ContactMessage                │
├─────────────────────────────────────────┤
│ PK  Id (int)                            │
│     FullName (string, 200)              │
│     Email (string, 254)                 │
│     PhoneNumber (string?, 20)           │
│     Company (string?, 200)              │
│     Subject (string, 500)               │
│     Message (text)                      │
│ FK  CountryId (int) [TO BE ADDED]       │
│     ContactType (enum)                  │
│     Priority (enum)                     │
│     Status (enum)                       │
│     CreatedAt (timestamptz)             │
│     UpdatedAt (timestamptz)             │
│     ResolvedAt (timestamptz?)           │
│     RowVersion (bytea) [TO BE ADDED]    │
└─────────────────────────────────────────┘
             │
             │ 1:N
             │
             ▼
┌─────────────────────────────────────────┐
│             ContactFile                 │
├─────────────────────────────────────────┤
│ PK  Id (int)                            │
│ FK  ContactMessageId (int)              │
│     FileName (string, 255)              │
│     ObjectName (string, 500)            │
│     FileSize (bigint?)                  │
│     ContentType (string?, 100)          │
│     UploadServiceFileId (string?, 100)  │
│     CreatedAt (timestamptz)             │
│     UpdatedAt (timestamptz)             │
└─────────────────────────────────────────┘

External References (not stored locally):
- CountryId → Country Service (/countries/v1/{id})
- UploadServiceFileId → Upload Service (/uploads/v1/{id})
```

## Entities

### ContactMessage

Represents a customer inquiry or support request submitted through the contact form.

**Table**: `contact_messages`

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| `id` | integer | NOT NULL | Auto | Primary key |
| `full_name` | varchar(200) | NOT NULL | - | Customer's full name |
| `email` | varchar(254) | NOT NULL | - | Customer's email address (RFC 5322 compliant) |
| `phone_number` | varchar(20) | NULL | - | Optional phone number (international formats supported) |
| `company` | varchar(200) | NULL | - | Optional company name (for B2B inquiries) |
| `subject` | varchar(500) | NOT NULL | - | Inquiry subject line |
| `message` | text | NOT NULL | - | Inquiry message content (max 10,000 characters) |
| `country_id` | integer | NOT NULL | - | **[TO BE ADDED]** Foreign key to Country Service |
| `contact_type` | integer | NOT NULL | 0 | Inquiry type: General(0), Supplier(1), Business(3) |
| `priority` | integer | NOT NULL | 1 | Priority level: Low(0), Medium(1), High(2), Urgent(3) |
| `status` | integer | NOT NULL | 0 | Workflow status: New(0), InProgress(1), Resolved(2), Closed(3) |
| `created_at` | timestamptz | NOT NULL | NOW() | Submission timestamp (UTC) |
| `updated_at` | timestamptz | NOT NULL | NOW() | Last update timestamp (UTC) |
| `resolved_at` | timestamptz | NULL | - | Resolution timestamp (set when status=Resolved) |
| `row_version` | bytea | NOT NULL | - | **[TO BE ADDED]** Optimistic concurrency token |

**Indexes**:
```sql
-- Primary key (auto-created)
PRIMARY KEY (id)

-- Email query and duplicate detection (TO BE ADDED)
CREATE INDEX idx_contact_email_created ON contact_messages(email, created_at DESC);

-- Admin filtering by status and type (TO BE ADDED)
CREATE INDEX idx_contact_status_type ON contact_messages(status, contact_type);

-- Priority triage queries (TO BE ADDED)
CREATE INDEX idx_contact_priority_status ON contact_messages(priority, status) WHERE status IN (0, 1);
```

**Enums**:

```csharp
public enum ContactType
{
    General = 0,    // General inquiries
    Supplier = 1,   // Supplier/vendor inquiries
    // Quotation = 2 [TO BE REMOVED] - handled by separate Quotation Service
    Business = 3    // Business partnership inquiries
}

public enum Priority
{
    Low = 0,        // Can wait several days
    Medium = 1,     // Default priority, respond within 2 business days
    High = 2,       // Urgent, respond within 24 hours
    Urgent = 3      // Critical, respond within 4 hours
}

public enum ContactStatus
{
    New = 0,        // Newly submitted, not yet reviewed
    InProgress = 1, // Admin is working on inquiry
    Resolved = 2,   // Inquiry resolved, response sent
    Closed = 3      // Inquiry closed (no further action needed)
}
```

**Business Rules**:
1. Email addresses must be unique within 60-second window (duplicate prevention)
2. Status change to `Resolved` automatically sets `resolved_at` timestamp
3. `country_id` must exist in Country Service before accepting submission
4. `ContactType.Quotation` must be rejected (separate service handles quotations)
5. `priority` defaults to `Medium` for new submissions
6. `status` defaults to `New` for new submissions
7. `updated_at` automatically updates on any modification
8. `row_version` enforces optimistic concurrency control for admin updates

---

### ContactFile

Represents a file attachment associated with a contact submission.

**Table**: `contact_files`

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| `id` | integer | NOT NULL | Auto | Primary key |
| `contact_message_id` | integer | NOT NULL | - | Foreign key to `contact_messages.id` |
| `file_name` | varchar(255) | NOT NULL | - | Original uploaded filename |
| `object_name` | varchar(500) | NOT NULL | - | GCS object name/path |
| `file_size` | bigint | NULL | - | File size in bytes (max 26,214,400 = 25MB) |
| `content_type` | varchar(100) | NULL | - | MIME type (e.g., "image/jpeg", "application/pdf") |
| `upload_service_file_id` | varchar(100) | NULL | - | Reference to Upload Service file record |
| `created_at` | timestamptz | NOT NULL | NOW() | Upload timestamp (UTC) |
| `updated_at` | timestamptz | NOT NULL | NOW() | Last update timestamp (UTC) |

**Indexes**:
```sql
-- Primary key (auto-created)
PRIMARY KEY (id)

-- Foreign key to contact messages
CREATE INDEX idx_contact_file_message_id ON contact_files(contact_message_id);

-- Foreign key constraint
ALTER TABLE contact_files
  ADD CONSTRAINT fk_contact_files_message
  FOREIGN KEY (contact_message_id)
  REFERENCES contact_messages(id)
  ON DELETE CASCADE;
```

**Business Rules**:
1. Maximum 10 files per `contact_message_id`
2. Each file size must not exceed 25MB (26,214,400 bytes)
3. Files are uploaded to Upload Service during submission transaction
4. Failed file uploads do not block submission (best-effort file attachment)
5. Deleting a `ContactMessage` cascades to delete associated `ContactFile` records
6. Deleting a `ContactFile` should also delete from Upload Service (cleanup)

---

## Database Migrations Required

### Migration 1: AddCountryIdAndRowVersion

**Purpose**: Add Country Service integration and optimistic concurrency support

**Changes**:
```csharp
public partial class AddCountryIdAndRowVersion : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Add CountryId column
        migrationBuilder.AddColumn<int>(
            name: "country_id",
            table: "contact_messages",
            type: "integer",
            nullable: false,
            defaultValue: 1); // Default to Thailand (or appropriate default)

        // Add RowVersion for optimistic concurrency
        migrationBuilder.AddColumn<byte[]>(
            name: "row_version",
            table: "contact_messages",
            type: "bytea",
            rowVersion: true,
            nullable: false);

        // Add indexes for performance
        migrationBuilder.CreateIndex(
            name: "idx_contact_email_created",
            table: "contact_messages",
            columns: new[] { "email", "created_at" });

        migrationBuilder.CreateIndex(
            name: "idx_contact_status_type",
            table: "contact_messages",
            columns: new[] { "status", "contact_type" });

        migrationBuilder.CreateIndex(
            name: "idx_contact_priority_status",
            table: "contact_messages",
            columns: new[] { "priority", "status" },
            filter: "status IN (0, 1)"); // Only index active inquiries
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "idx_contact_email_created", table: "contact_messages");
        migrationBuilder.DropIndex(name: "idx_contact_status_type", table: "contact_messages");
        migrationBuilder.DropIndex(name: "idx_contact_priority_status", table: "contact_messages");
        migrationBuilder.DropColumn(name: "country_id", table: "contact_messages");
        migrationBuilder.DropColumn(name: "row_version", table: "contact_messages");
    }
}
```

**Execution**:
```bash
# Generate migration
dotnet ef migrations add AddCountryIdAndRowVersion --project Maliev.ContactService.Data

# Apply to development database (after port-forward)
kubectl port-forward -n maliev-dev postgres-cluster-1 5432:5432
export ContactDbContext="Server=localhost;Port=5432;Database=contact_app_db;User Id=postgres;Password=<ACTUAL_PASSWORD>;"
dotnet ef database update --project Maliev.ContactService.Data
```

**Data Migration Strategy**:
- Set `country_id` default to Thailand (id=1) or prompt admins to update existing records
- Existing submissions without country can be handled retroactively by admin

---

### Migration 2: RemoveQuotationContactType (Optional)

**Purpose**: Remove Quotation enum value per spec requirement FR-013

**Changes**:
```csharp
public partial class RemoveQuotationContactType : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Update any existing Quotation records to General type
        migrationBuilder.Sql(@"
            UPDATE contact_messages
            SET contact_type = 0  -- General
            WHERE contact_type = 2;  -- Quotation
        ");

        // Note: Enum value removal happens in code, not database
        // Database stores integers, so no schema change needed
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Cannot restore Quotation records automatically
        // Manual data fix required if rollback needed
    }
}
```

**Code Change**:
```csharp
// In ContactMessage.cs, update enum:
public enum ContactType
{
    General = 0,
    Supplier = 1,
    // Quotation = 2,  // REMOVED - use Quotation Service
    Business = 3
}
```

---

## Entity Model Updates

### ContactMessage.cs Changes

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Maliev.ContactService.Data.Models;

public class ContactMessage : IAuditable
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public required string FullName { get; set; }

    [Required]
    [StringLength(254)]
    [EmailAddress]
    public required string Email { get; set; }

    [StringLength(20)]
    public string? PhoneNumber { get; set; }

    [StringLength(200)]
    public string? Company { get; set; }

    [Required]
    [StringLength(500)]
    public required string Subject { get; set; }

    [Required]
    public required string Message { get; set; }

    // NEW: Country Service integration
    [Required]
    public int CountryId { get; set; }

    [Required]
    public ContactType ContactType { get; set; } = ContactType.General;

    [Required]
    public Priority Priority { get; set; } = Priority.Medium;

    [Required]
    public ContactStatus Status { get; set; } = ContactStatus.New;

    [Required]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Required]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ResolvedAt { get; set; }

    // NEW: Optimistic concurrency control
    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // Navigation property
    public virtual ICollection<ContactFile> Files { get; set; } = new List<ContactFile>();
}

public enum ContactType
{
    General = 0,
    Supplier = 1,
    // Quotation = 2,  // REMOVED
    Business = 3
}

// Priority and ContactStatus enums remain unchanged
```

### ContactFile.cs Changes

No changes required to `ContactFile.cs`. Current implementation is spec-compliant.

---

## Database Configuration

### Connection String (Secret Manager)

```
Server=postgres-cluster-rw.maliev-dev.svc.cluster.local;
Port=5432;
Database=contact_app_db;
User Id=contact_service;
Password=<SECRET_FROM_GSM>;
Include Error Detail=true;
```

**Secret Name**: `contact-db-connection-string`
**Mount Path**: `/mnt/secrets/ContactDbContext`

### DbContext Configuration

```csharp
// In ContactDbContext.cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    // Configure table names (snake_case)
    modelBuilder.Entity<ContactMessage>().ToTable("contact_messages");
    modelBuilder.Entity<ContactFile>().ToTable("contact_files");

    // Configure cascade delete
    modelBuilder.Entity<ContactMessage>()
        .HasMany(c => c.Files)
        .WithOne(f => f.ContactMessage)
        .HasForeignKey(f => f.ContactMessageId)
        .OnDelete(DeleteBehavior.Cascade);

    // Configure indexes (handled by migrations)

    // Configure row version (auto-increment on PostgreSQL)
    modelBuilder.Entity<ContactMessage>()
        .Property(c => c.RowVersion)
        .IsRowVersion()
        .IsConcurrencyToken();
}
```

---

## Data Validation Rules

### ContactMessage Validation

- `FullName`: 1-200 characters, required
- `Email`: RFC 5322 format, max 254 characters, required
- `PhoneNumber`: 8-20 characters, optional, pattern: `^[\d\s\-\(\)\+]{8,20}$`
- `Company`: 1-200 characters, optional
- `Subject`: 1-500 characters, required
- `Message`: 1-10,000 characters, required
- `CountryId`: Must exist in Country Service, required
- `ContactType`: Must be General(0), Supplier(1), or Business(3) - NOT Quotation(2)
- No duplicate submissions from same email within 60 seconds

### ContactFile Validation

- `FileName`: 1-255 characters, required
- `FileSize`: Max 26,214,400 bytes (25MB), required
- `ContentType`: MIME type validation recommended
- Max 10 files per ContactMessage

---

## Seed Data

### Development Environment

```csharp
// In ContactDbContext.cs or separate seed configuration
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // Seed test data for development
    if (_environment.IsDevelopment())
    {
        modelBuilder.Entity<ContactMessage>().HasData(
            new ContactMessage
            {
                Id = 1,
                FullName = "Test Customer",
                Email = "test@example.com",
                Subject = "Test Inquiry",
                Message = "This is a test message for development",
                CountryId = 1, // Thailand
                ContactType = ContactType.General,
                Priority = Priority.Medium,
                Status = ContactStatus.New,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            }
        );
    }
}
```

---

## Performance Considerations

### Query Optimization

**High-Traffic Queries**:
1. Admin list view: `SELECT * FROM contact_messages WHERE status = ? AND contact_type = ? ORDER BY created_at DESC LIMIT 20`
   - Uses: `idx_contact_status_type` index
   - Expected performance: < 10ms

2. Duplicate detection: `SELECT 1 FROM contact_messages WHERE email = ? AND created_at > NOW() - INTERVAL '60 seconds'`
   - Uses: `idx_contact_email_created` index
   - Expected performance: < 5ms

3. Email history query: `SELECT * FROM contact_messages WHERE email = ? ORDER BY created_at DESC`
   - Uses: `idx_contact_email_created` index
   - Expected performance: < 10ms

**Expected Load**:
- Write operations: ~100-500 inserts/day (~0.005-0.02 writes/second)
- Read operations: ~1000-5000 reads/day (~0.05-0.2 reads/second)
- Database size estimate: ~10MB/year (with 500 submissions/day, 10KB avg size)

**Scaling Strategy**:
- Current single-instance PostgreSQL sufficient for 5+ years
- Read replicas not needed at current scale
- Connection pooling configured in EF Core (default: 100 connections)

---

## Summary

The Contact Service data model is simple and well-structured for its domain. The required changes are minimal:

1. **Add CountryId column** to support Country Service integration
2. **Add RowVersion column** for optimistic concurrency control
3. **Remove Quotation enum value** from ContactType
4. **Add indexes** for duplicate detection and admin filtering

All changes can be applied in a single migration with zero downtime using standard PostgreSQL online DDL operations.

---

## Migration Deployment Guide

### Prerequisites

- Migration file: `20251030030605_AddCountryIdAndRowVersion.cs`
- PostgreSQL 17.5+ (current: 17.5)
- EF Core 9.0.9
- Access to target environment's PostgreSQL cluster

### Development Environment (Applied ✅)

**Status**: Migration successfully applied on 2025-10-30

**Verification**:
```sql
-- Check migration history
SELECT * FROM "__EFMigrationsHistory"
WHERE "MigrationId" = '20251030030605_AddCountryIdAndRowVersion';

-- Verify schema changes
\d "ContactMessages"  -- Should show CountryId and RowVersion columns
\di "ContactMessages"  -- Should show 3 new indexes
```

### Staging Environment

**Before Deployment**:
1. Backup database:
   ```bash
   kubectl exec -n maliev-staging postgres-cluster-1 -- \
     pg_dump -U postgres contact_app_db > contact_app_db_backup_$(date +%Y%m%d_%H%M%S).sql
   ```

2. Verify database connectivity:
   ```bash
   kubectl exec -n maliev-staging postgres-cluster-1 -- \
     psql -U postgres -d contact_app_db -c "SELECT version();"
   ```

**Apply Migration**:

**Option A: Using dotnet ef (Recommended)**
```bash
# Port forward to PostgreSQL
kubectl port-forward -n maliev-staging postgres-cluster-1 5433:5432 &

# Set connection string (get password from secret)
PGPASSWORD=$(kubectl get secret -n maliev-staging postgres-superuser-credentials -o jsonpath='{.data.password}' | base64 -d)
export ContactDbContext="Server=localhost;Port=5433;Database=contact_app_db;User Id=postgres;Password=$PGPASSWORD;"

# Apply migration
cd Maliev.ContactService
dotnet ef database update --project Maliev.ContactService.Data --startup-project Maliev.ContactService.Api --context ContactDbContext

# Stop port forward
pkill -f "port-forward.*postgres-cluster-1"
```

**Option B: Using SQL Script (If dotnet ef fails)**
```bash
# Generate idempotent SQL script
export ASPNETCORE_ENVIRONMENT="Testing"
dotnet ef migrations script 20250915061014_UpdateTimestampsToDateTimeOffset 20251030030605_AddCountryIdAndRowVersion \
  --project Maliev.ContactService.Data --startup-project Maliev.ContactService.Api \
  --context ContactDbContext --idempotent --output migration_staging.sql

# Apply script directly in pod
cat migration_staging.sql | kubectl exec -i -n maliev-staging postgres-cluster-1 -- \
  psql -U postgres -d contact_app_db

# Verify
kubectl exec -n maliev-staging postgres-cluster-1 -- \
  psql -U postgres -d contact_app_db -c "SELECT * FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\";"
```

**Post-Migration Verification**:
```bash
# Verify columns exist
kubectl exec -n maliev-staging postgres-cluster-1 -- \
  psql -U postgres -d contact_app_db -c "\d \"ContactMessages\""

# Verify indexes exist
kubectl exec -n maliev-staging postgres-cluster-1 -- \
  psql -U postgres -d contact_app_db -c "
    SELECT indexname, indexdef
    FROM pg_indexes
    WHERE tablename = 'ContactMessages'
    AND indexname LIKE 'IX_%';"

# Test index usage (explain plan should show index scan)
kubectl exec -n maliev-staging postgres-cluster-1 -- \
  psql -U postgres -d contact_app_db -c "
    EXPLAIN SELECT 1 FROM \"ContactMessages\"
    WHERE \"Email\" = 'test@example.com' AND \"CreatedAt\" > NOW() - INTERVAL '60 seconds';"
```

### Production Environment

⚠️ **Critical**: Perform during maintenance window or low-traffic period

**Before Deployment**:
1. Schedule maintenance window
2. Create database backup (retain for 30 days):
   ```bash
   kubectl exec -n maliev-prod postgres-cluster-1 -- \
     pg_dump -U postgres contact_app_db | gzip > \
     contact_app_db_prod_backup_$(date +%Y%m%d_%H%M%S).sql.gz
   ```

3. Notify stakeholders of deployment

**Apply Migration** (same process as staging, use `-n maliev-prod`):
```bash
# Same steps as staging, but with maliev-prod namespace
kubectl port-forward -n maliev-prod postgres-cluster-1 5433:5432 &

# ... follow Option A or Option B from staging section ...
```

**Post-Migration Monitoring**:
```bash
# Monitor query performance
kubectl exec -n maliev-prod postgres-cluster-1 -- \
  psql -U postgres -d contact_app_db -c "
    SELECT schemaname, tablename, indexname, idx_scan, idx_tup_read, idx_tup_fetch
    FROM pg_stat_user_indexes
    WHERE tablename = 'ContactMessages'
    ORDER BY idx_scan DESC;"

# Check for slow queries (> 100ms)
kubectl logs -f -n maliev-prod deployment/maliev-contact-service | grep "slow query"
```

### Rollback Procedure (If Needed)

⚠️ **Only if migration causes critical issues**

```bash
# Connect to database
kubectl exec -it -n <namespace> postgres-cluster-1 -- psql -U postgres -d contact_app_db

# Remove migration from history
DELETE FROM "__EFMigrationsHistory"
WHERE "MigrationId" = '20251030030605_AddCountryIdAndRowVersion';

# Drop indexes
DROP INDEX IF EXISTS "IX_ContactMessages_Email_CreatedAt";
DROP INDEX IF EXISTS "IX_ContactMessages_Status_ContactType";
DROP INDEX IF EXISTS "IX_ContactMessages_Priority_Status_Filtered";

# Drop columns
ALTER TABLE "ContactMessages" DROP COLUMN IF EXISTS "CountryId";
ALTER TABLE "ContactMessages" DROP COLUMN IF EXISTS "RowVersion";

# Verify rollback
SELECT * FROM "__EFMigrationsHistory" ORDER BY "MigrationId";
\d "ContactMessages"
```

### Configuration Updates (Post-Migration)

After migration is applied, update GitOps configuration for Country Service integration:

**File**: `maliev-gitops/3-apps/maliev-contact-service/base/deployment.yaml`

Ensure `envFrom` includes Country Service secret:
```yaml
spec:
  template:
    spec:
      containers:
      - name: maliev-contact-service
        envFrom:
        - secretRef:
            name: maliev-contact-secrets  # Should include CountryService__BaseUrl
```

**Create/Update Secret** (if not exists):
```bash
kubectl create secret generic maliev-contact-secrets -n <namespace> \
  --from-literal=CountryService__BaseUrl=http://maliev-country-service/countries \
  --from-literal=CountryService__TimeoutSeconds=10 \
  --dry-run=client -o yaml | kubectl apply -f -
```

### Troubleshooting

**Issue**: Migration fails with "column already exists"
- **Cause**: Migration partially applied or manually added column
- **Solution**: Check current schema with `\d "ContactMessages"`, then run idempotent SQL script

**Issue**: Permission denied
- **Cause**: Using app_user instead of postgres superuser
- **Solution**: Use postgres superuser credentials from `postgres-superuser-credentials` secret

**Issue**: Connection timeout
- **Cause**: Port forward failed or database not accessible
- **Solution**: Verify pod is running (`kubectl get pods`), check port forward is active (`netstat -an | grep 5433`)

**Issue**: EF Tools version mismatch warning
- **Cause**: EF Core tools (9.0.8) older than runtime (9.0.9)
- **Impact**: Warning only, migration still works
- **Solution**: Update tools with `dotnet tool update --global dotnet-ef`

### Success Criteria

✅ Migration listed in `__EFMigrationsHistory`
✅ CountryId and RowVersion columns exist in ContactMessages table
✅ 3 new indexes created (Email_CreatedAt, Status_ContactType, Priority_Status_Filtered)
✅ No errors in application logs after deployment
✅ Health check endpoints return 200 OK
✅ Sample query execution time < 10ms using new indexes

### Estimated Downtime

- **Development**: 0 seconds (online DDL)
- **Staging**: 0 seconds (online DDL)
- **Production**: 0 seconds (online DDL)

**Note**: PostgreSQL supports online DDL for ADD COLUMN and CREATE INDEX operations. No application downtime required.
