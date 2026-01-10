using Maliev.ContactService.Data.DbContexts;
using Maliev.ContactService.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Maliev.ContactService.Api.Services.Auth;

/// <summary>
/// Service for seeding authorization data.
/// </summary>
public static class DataSeeder
{
    private static readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>
    /// Seeds permissions and roles into the database.
    /// </summary>
    public static async Task SeedAuthDataAsync(ContactDbContext dbContext)
    {
        await _semaphore.WaitAsync();
        try
        {
            // 1. Seed Permissions
            var allPermissions = ContactPermissions.GetAll().ToList();
            var existingPermissions = await dbContext.Permissions.AsNoTracking().ToListAsync();

            foreach (var permName in allPermissions)
            {
                if (!existingPermissions.Any(p => p.Name == permName))
                {
                    var category = permName.Split('.')[1];
                    category = char.ToUpper(category[0]) + category.Substring(1);

                    dbContext.Permissions.Add(new Permission
                    {
                        Id = Guid.NewGuid(),
                        Name = permName,
                        Category = category,
                        Description = $"Permission to {permName.Split('.')[2]} {permName.Split('.')[1]}"
                    });
                }
            }

            await dbContext.SaveChangesAsync();

            // 2. Seed Roles
            var existingRoles = await dbContext.Roles.Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission).ToListAsync();
            var permissions = await dbContext.Permissions.ToListAsync();

            foreach (var roleDef in ContactPredefinedRoles.All)
            {
                var roleName = roleDef.RoleId;
                var role = existingRoles.FirstOrDefault(r => r.Name == roleName);
                if (role == null)
                {
                    role = new Role
                    {
                        Id = Guid.NewGuid(),
                        Name = roleName,
                        Description = roleDef.Description
                    };
                    dbContext.Roles.Add(role);
                    await dbContext.SaveChangesAsync();
                }

                // Sync permissions for the role
                var rolePermissionNames = roleDef.Permissions;

                // Remove permissions no longer in the role definition
                var toRemove = role.RolePermissions
                    .Where(rp => !rolePermissionNames.Contains(rp.Permission.Name))
                    .ToList();

                foreach (var rp in toRemove)
                {
                    role.RolePermissions.Remove(rp);
                }

                // Add missing permissions
                foreach (var permName in rolePermissionNames)
                {
                    var targetPerm = permissions.First(p => p.Name == permName);
                    if (!role.RolePermissions.Any(rp => rp.PermissionId == targetPerm.Id))
                    {
                        role.RolePermissions.Add(new RolePermission
                        {
                            RoleId = role.Id,
                            PermissionId = targetPerm.Id
                        });
                    }
                }
            }

            await dbContext.SaveChangesAsync();
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
