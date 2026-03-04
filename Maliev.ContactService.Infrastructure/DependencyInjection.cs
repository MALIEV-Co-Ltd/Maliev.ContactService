using Maliev.Aspire.ServiceDefaults.Authorization;
using Maliev.ContactService.Application.Interfaces;
using Maliev.ContactService.Infrastructure.BackgroundServices;
using Maliev.ContactService.Infrastructure.ExternalServices;
using Maliev.ContactService.Infrastructure.Metrics;
using Maliev.ContactService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Maliev.ContactService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ContactDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IContactDbContext>(provider => provider.GetRequiredService<ContactDbContext>());

        // Background Services
        services.AddHostedService<AuditLogBackgroundService>();

        // Metrics
        services.AddSingleton<IAuthMetrics, AuthMetricsService>();

        // External Service Clients
        services.AddHttpClient<IUploadServiceClient, UploadServiceClient>(client =>
        {
            client.BaseAddress = new Uri(configuration["ExternalServices:UploadService"] ?? "http://upload-service");
        });

        services.AddHttpClient<ICountryServiceClient, CountryServiceClient>(client =>
        {
            client.BaseAddress = new Uri(configuration["ExternalServices:CountryService"] ?? "http://country-service");
        });

        return services;
    }
}
