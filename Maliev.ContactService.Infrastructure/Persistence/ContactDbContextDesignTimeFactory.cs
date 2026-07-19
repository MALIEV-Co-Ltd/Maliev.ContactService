using Maliev.ContactService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Maliev.ContactService.Infrastructure.Persistence;

/// <summary>
/// Factory for creating <see cref="ContactDbContext"/> instances at design time for migrations and tooling.
/// </summary>
public class ContactDbContextDesignTimeFactory : IDesignTimeDbContextFactory<ContactDbContext>
{
    /// <summary>
    /// Creates a new instance of <see cref="ContactDbContext"/> with the configured database connection.
    /// </summary>
    /// <param name="args">Command-line arguments passed to the factory.</param>
    /// <returns>A configured <see cref="ContactDbContext"/> instance.</returns>
    public ContactDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ContactDbContext>();

        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__ContactDbContext");

        if (string.IsNullOrEmpty(connectionString))
        {
            connectionString = "Host=localhost;Port=5433;Database=contact_app_db;Username=postgres;Password=temp;SslMode=Disable";
        }

        optionsBuilder.UseNpgsql(connectionString);

        return new ContactDbContext(optionsBuilder.Options);
    }
}
