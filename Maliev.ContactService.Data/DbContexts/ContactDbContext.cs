using Maliev.ContactService.Data.Models;
using Microsoft.EntityFrameworkCore;
using Maliev.Aspire.ServiceDefaults.Database;

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
    public DbSet<ContactMessage> ContactMessages { get; set; }
    
    /// <summary>
    /// Gets or sets the DbSet for ContactFile entities.
    /// </summary>
    public DbSet<ContactFile> ContactFiles { get; set; }

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

            if (entity.State == EntityState.Added)
            {
                // Only set CreatedAt if it hasn't been explicitly set (is default DateTimeOffset)
                if (auditableEntity.CreatedAt == DateTimeOffset.MinValue)
                {
                    auditableEntity.CreatedAt = now;
                }
            }

            // Only set UpdatedAt if it hasn't been explicitly set (is default DateTimeOffset)
            if (auditableEntity.UpdatedAt == DateTimeOffset.MinValue)
            {
                auditableEntity.UpdatedAt = now;
            }
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

            entity.Property(e => e.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("NOW()");

            entity.Property(e => e.UpdatedAt)
                .IsRequired()
                .HasDefaultValueSql("NOW()");

            // Ignore the RowVersion property (PostgreSQL doesn't need it for concurrency)
            entity.Ignore(e => e.RowVersion);

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

        // Apply PostgreSQL snake_case naming convention globally
        SnakeCaseNamingHelper.ApplySnakeCaseNaming(modelBuilder);
    }
}