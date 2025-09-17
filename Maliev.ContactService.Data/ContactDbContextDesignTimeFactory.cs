using Maliev.ContactService.Data.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Maliev.ContactService.Data;

public class ContactDbContextDesignTimeFactory : IDesignTimeDbContextFactory<ContactDbContext>
{
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