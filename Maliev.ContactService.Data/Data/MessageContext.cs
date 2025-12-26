namespace Maliev.MessageService.Data.Data
{
    using System;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Maliev.MessageService.Data.Models;

    /// <summary>
    /// Database context for legacy message data.
    /// </summary>
    public partial class MessageContext : DbContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MessageContext"/> class.
        /// </summary>
        public MessageContext()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageContext"/> class with the specified options.
        /// </summary>
        /// <param name="options">The options to configure the context.</param>
        public MessageContext(DbContextOptions<MessageContext> options)
            : base(options)
        {
        }

        /// <summary>
        /// Gets or sets the DbSet for Message entities.
        /// </summary>
        public virtual DbSet<Message> Message { get; set; }

        

        /// <summary>
        /// Configures the schema needed for the message context.
        /// </summary>
        /// <param name="modelBuilder">The builder being used to construct the model for this context.</param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Message>(entity =>
            {
                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.Company).HasMaxLength(50);

                entity.Property(e => e.Country).HasMaxLength(50);

                entity.Property(e => e.CreatedDate)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getutcdate())");

                entity.Property(e => e.Email).HasMaxLength(50);

                entity.Property(e => e.FirstName).HasMaxLength(50);

                entity.Property(e => e.LastName).HasMaxLength(50);

                entity.Property(e => e.ModifiedDate)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getutcdate())");

                entity.Property(e => e.Telephone).HasMaxLength(50);
            });
        }
    }
}
