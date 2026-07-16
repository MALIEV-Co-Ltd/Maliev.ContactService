using Maliev.Aspire.ServiceDefaults.Authorization;
using Maliev.Aspire.ServiceDefaults.IAM;
using Maliev.ContactService.Application.Interfaces;
using Maliev.ContactService.Infrastructure.BackgroundServices;
using Maliev.ContactService.Infrastructure.ExternalServices;
using Maliev.ContactService.Infrastructure.Metrics;
using Maliev.ContactService.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Maliev.ContactService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IContactDbContext>(provider => provider.GetRequiredService<ContactDbContext>());

        // Background Services
        services.AddHostedService<AuditLogBackgroundService>();

        // Metrics
        services.AddSingleton<IAuthMetrics, AuthMetricsService>();

        // External Service Clients
        services.AddHttpClient<IUploadServiceClient, UploadServiceClient>(client =>
        {
            client.BaseAddress = new Uri(
                configuration["ExternalServices:UploadService"]
                ?? configuration["Services:UploadService:BaseUrl"]
                ?? "https+http://UploadService");
            client.Timeout = TimeSpan.FromSeconds(5);
        })
        .AddServiceDiscovery()
        .AddHttpMessageHandler<ServiceAccountAuthenticationHandler>();

        services.AddHttpClient(UploadServiceClient.StorageHttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false
        });

        services.AddHttpClient<ICountryServiceClient, CountryServiceClient>(client =>
        {
            client.BaseAddress = new Uri(
                configuration["ExternalServices:CountryService"]
                ?? configuration["Services:CountryService:BaseUrl"]
                ?? "https+http://CountryService");
        })
        .AddServiceDiscovery()
        .AddHttpMessageHandler<ServiceAccountAuthenticationHandler>();

        return services;
    }
}
