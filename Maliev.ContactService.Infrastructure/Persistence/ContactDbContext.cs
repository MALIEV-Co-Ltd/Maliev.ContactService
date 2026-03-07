using Maliev.ContactService.Application.Interfaces;
using Maliev.ContactService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Maliev.ContactService.Infrastructure.Persistence;

public class ContactDbContext : DbContext, IContactDbContext
{
    public ContactDbContext(DbContextOptions<ContactDbContext> options) : base(options)
    {
    }

    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();
    public DbSet<ContactFile> ContactFiles => Set<ContactFile>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    DatabaseFacade IContactDbContext.Database => base.Database;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ContactMessage>(entity =>
        {
            entity.ToTable("ContactMessages");
            entity.HasKey(e => e.Id);
            entity.Property<uint>("Xmin")
                .HasColumnType("xid");
            entity.HasIndex(e => e.Email);
            entity.HasMany(e => e.Files)
                  .WithOne(e => e.ContactMessage)
                  .HasForeignKey(e => e.ContactMessageId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ContactFile>(entity =>
        {
            entity.ToTable("ContactFiles");
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs");
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.HasKey(rp => new { rp.RoleId, rp.PermissionId });
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.ToTable("Permissions");
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Roles");
            entity.HasKey(e => e.Id);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        AddTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void AddTimestamps()
    {
        var entities = ChangeTracker.Entries<IAuditable>()
            .Where(x => x.State == EntityState.Added || x.State == EntityState.Modified);

        foreach (var entity in entities)
        {
            var now = DateTimeOffset.UtcNow;

            if (entity.State == EntityState.Added)
            {
                entity.Entity.CreatedAt = now;
            }

            entity.Entity.UpdatedAt = now;
        }
    }
}
