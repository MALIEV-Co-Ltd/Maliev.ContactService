using Maliev.ContactService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Maliev.ContactService.Application.Interfaces;

public interface IContactDbContext
{
    DbSet<ContactMessage> ContactMessages { get; }
    DbSet<ContactFile> ContactFiles { get; }
    DbSet<AuditLog> AuditLogs { get; }

    DatabaseFacade Database { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
