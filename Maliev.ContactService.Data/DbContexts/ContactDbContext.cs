using Maliev.Aspire.ServiceDefaults.Database;
using Maliev.ContactService.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Maliev.ContactService.Data.DbContexts;

/// <summary>
/// Database context for contact service data.
/// </summary>
public class ContactDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactDbContext"/> class with the specified options.
    /// </summary>
    /// <param name="options">The options to configure the context.</param>
    public ContactDbContext(DbContextOptions<ContactDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Gets or sets the DbSet for ContactMessage entities.
    /// </summary>
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();

    /// <summary>
    /// Gets or sets the DbSet for ContactFile entities.
    /// </summary>
    public DbSet<ContactFile> ContactFiles => Set<ContactFile>();

    /// <summary>
    /// Gets or sets the DbSet for Permission entities.
    /// </summary>
    public DbSet<Permission> Permissions => Set<Permission>();

    /// <summary>
    /// Gets or sets the DbSet for Role entities.
    /// </summary>
    public DbSet<Role> Roles => Set<Role>();

    /// <summary>
    /// Gets or sets the DbSet for RolePermission entities.
    /// </summary>
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    /// <summary>
    /// Gets or sets the DbSet for AuditLog entities.
    /// </summary>
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    /// <summary>
    /// Saves all changes made in this context to the database.
    /// </summary>
    /// <returns>The number of state entries written to the database.</returns>
    public override int SaveChanges()
    {
        AddTimestamps();
        return base.SaveChanges();
    }

    /// <summary>
    /// Asynchronously saves all changes made in this context to the database.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous save operation. The task result contains the number of state entries written to the database.</returns>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        AddTimestamps();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void AddTimestamps()
    {
        var entities = ChangeTracker.Entries()
            .Where(x => x.Entity is IAuditable && (x.State == EntityState.Added || x.State == EntityState.Modified));

        foreach (var entity in entities)
        {
            var auditableEntity = (IAuditable)entity.Entity;
            var now = DateTimeOffset.UtcNow;

            if (entity.State == EntityState.Added && auditableEntity.CreatedAt == DateTimeOffset.MinValue)
            {
                auditableEntity.CreatedAt = now;
            }

            auditableEntity.UpdatedAt = now;
        }
    }

    /// <summary>
    /// Configures the schema needed for the contact context.
    /// </summary>
    /// <param name="modelBuilder">The builder being used to construct the model for this context.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure ContactMessage entity
        modelBuilder.Entity<ContactMessage>(entity =>
        {
            entity.ToTable("ContactMessages");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd();

            entity.Property(e => e.FullName)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.Email)
                .IsRequired()
                .HasMaxLength(254);

            entity.Property(e => e.PhoneNumber)
                .HasMaxLength(20);

            entity.Property(e => e.Company)
                .HasMaxLength(200);

            entity.Property(e => e.Subject)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.Message)
                .IsRequired();

            entity.Property(e => e.ContactType)
                .IsRequired()
                .HasConversion<int>();

            entity.Property(e => e.Priority)
                .IsRequired()
                .HasConversion<int>();

            entity.Property(e => e.Status)
                .IsRequired()
                .HasConversion<int>();

            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .HasDefaultValueSql("'\\x0000000000000001'::bytea")
                .ValueGeneratedOnAddOrUpdate();

            entity.Property(e => e.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("NOW()");

            entity.Property(e => e.UpdatedAt)
                .IsRequired()
                .HasDefaultValueSql("NOW()");

            // Indexes for performance
            entity.HasIndex(e => e.Email);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.ContactType);
        });

        // Configure ContactFile entity
        modelBuilder.Entity<ContactFile>(entity =>
        {
            entity.ToTable("ContactFiles");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd();

            entity.Property(e => e.FileName)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(e => e.ObjectName)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.ContentType)
                .HasMaxLength(100);

            entity.Property(e => e.UploadServiceFileId)
                .HasMaxLength(100);

            entity.Property(e => e.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("NOW()");

            entity.Property(e => e.UpdatedAt)
                .IsRequired()
                .HasDefaultValueSql("NOW()");

            // Foreign key relationship
            entity.HasOne(cf => cf.ContactMessage)
                .WithMany(cm => cm.Files)
                .HasForeignKey(cf => cf.ContactMessageId)
                .OnDelete(DeleteBehavior.Cascade);

            // Index for foreign key
            entity.HasIndex(e => e.ContactMessageId);
        });

        // Configure Permission entity
        modelBuilder.Entity<Permission>(entity =>
        {
            entity.ToTable("Permissions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Category).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.HasIndex(e => e.Name).IsUnique();
        });

        // Configure Role entity
        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Roles");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.HasIndex(e => e.Name).IsUnique();
        });

        // Configure RolePermission entity
        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.ToTable("RolePermissions");
            entity.HasKey(rp => new { rp.RoleId, rp.PermissionId });

            entity.HasOne(rp => rp.Role)
                .WithMany(r => r.RolePermissions)
                .HasForeignKey(rp => rp.RoleId);

            entity.HasOne(rp => rp.Permission)
                .WithMany(p => p.RolePermissions)
                .HasForeignKey(rp => rp.PermissionId);
        });

        // Configure AuditLog entity
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Resource).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ClientIp).HasMaxLength(50);
            entity.Property(e => e.Reason).HasMaxLength(500);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Timestamp);
        });

        // Apply PostgreSQL snake_case naming convention globally
        SnakeCaseNamingHelper.ApplySnakeCaseNaming(modelBuilder);
    }
}
