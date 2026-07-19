using Maliev.ContactService.Application.Interfaces;
using Maliev.ContactService.Application.Services;
using Maliev.ContactService.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Channels;

namespace Maliev.ContactService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IContactService, Services.ContactService>();
        services.AddSingleton<IAuditLogService, AuditLogService>();

        // Audit Logging Channel
        services.AddSingleton(Channel.CreateUnbounded<AuditLog>());

        return services;
    }
}
