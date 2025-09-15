
using Maliev.ContactService.Api.Services;
using Maliev.ContactService.Tests.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Maliev.ContactService.Tests.Integration;

public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove the real IUploadServiceClient registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IUploadServiceClient));

            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Add the mock implementation
            services.AddScoped<IUploadServiceClient, MockUploadServiceClient>();

            services.AddAuthentication(TestAuthHandler.TestScheme)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.TestScheme, options => {});
        });
    }
}
