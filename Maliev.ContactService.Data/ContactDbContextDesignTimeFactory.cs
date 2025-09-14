using Maliev.ContactService.Data.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Maliev.ContactService.Data;

public class ContactDbContextDesignTimeFactory : IDesignTimeDbContextFactory<ContactDbContext>
{
    public ContactDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ContactDbContext>();
        optionsBuilder.UseNpgsql("Server=localhost;Port=5432;Database=contact_app_db;User Id=postgres;Password=password;");

        return new ContactDbContext(optionsBuilder.Options);
    }
}