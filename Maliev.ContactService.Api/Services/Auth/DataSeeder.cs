using Maliev.ContactService.Data.DbContexts;
using Maliev.ContactService.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Maliev.ContactService.Api.Services.Auth;

/// <summary>
/// Provides seeding functionality for authorization data in the Contact Service.
/// </summary>
public static class DataSeeder
{
    /// <summary>
    /// Seeds authorization data including permissions and predefined roles.
    /// </summary>
    /// <param name="context">The database context to seed.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task SeedAuthDataAsync(ContactDbContext context)
    {
        // Seed Permissions
        foreach (var p in ContactPermissions.AllWithDescriptions)
        {
            var exists = await context.Permissions.AnyAsync(x => x.Name == p.Key);
            if (!exists)
            {
                context.Permissions.Add(new Permission
                {
                    Name = p.Key,
                    Description = p.Value,
                    Category = p.Key.Split('.')[1]
                });
            }
        }
        await context.SaveChangesAsync();

        // Seed Roles
        foreach (var r in ContactPredefinedRoles.All)
        {
            var role = await context.Roles
                .Include(x => x.RolePermissions)
                .FirstOrDefaultAsync(x => x.Name == r.RoleId);

            if (role == null)
            {
                role = new Role
                {
                    Name = r.RoleId,
                    Description = r.Description
                };
                context.Roles.Add(role);
                await context.SaveChangesAsync();
            }

            // Sync permissions for role
            foreach (var permName in r.Permissions)
            {
                var permission = await context.Permissions.FirstAsync(p => p.Name == permName);
                if (!role.RolePermissions.Any(rp => rp.PermissionId == permission.Id))
                {
                    context.RolePermissions.Add(new RolePermission
                    {
                        RoleId = role.Id,
                        PermissionId = permission.Id
                    });
                }
            }
        }
        await context.SaveChangesAsync();
    }
}
