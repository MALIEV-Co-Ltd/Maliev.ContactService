using Maliev.ContactService.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Maliev.ContactService.Data.DbContexts;

public class ContactDbContext : DbContext
{
    public ContactDbContext(DbContextOptions<ContactDbContext> options) : base(options)
    {
    }

    public DbSet<ContactMessage> ContactMessages { get; set; }
    public DbSet<ContactFile> ContactFiles { get; set; }

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
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.UpdatedAt)
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

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
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Foreign key relationship
            entity.HasOne(cf => cf.ContactMessage)
                .WithMany(cm => cm.Files)
                .HasForeignKey(cf => cf.ContactMessageId)
                .OnDelete(DeleteBehavior.Cascade);

            // Index for foreign key
            entity.HasIndex(e => e.ContactMessageId);
        });
    }
}