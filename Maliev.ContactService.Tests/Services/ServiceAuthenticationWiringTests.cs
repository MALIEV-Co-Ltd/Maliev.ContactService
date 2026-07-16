using Maliev.Aspire.ServiceDefaults.IAM;
using Maliev.ContactService.Application.Interfaces;
using Maliev.ContactService.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;

namespace Maliev.ContactService.Tests.Services;

public sealed class ServiceAuthenticationWiringTests
{
    private const string ExpectedToken = "centrally-issued-contact-token";

    [Fact]
    public void Program_RegistersExactContactProcessIdentityWithoutLegacySigner()
    {
        var source = ReadRepositoryFile("Maliev.ContactService.Api", "Program.cs");

        Assert.Contains("builder.AddAuthServiceTokenExchange(\"ContactService\");", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AddIAMServiceClient", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AuthServiceExchange_UsesExactContactProcessIdentity()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Testing"
        });
        AddExchangeConfiguration(builder.Configuration);

        builder.AddAuthServiceTokenExchange("ContactService");

        using var provider = builder.Services.BuildServiceProvider();
        var identity = provider.GetRequiredService<ServiceProcessIdentity>();

        Assert.Equal("ContactService", identity.ServiceName);
        Assert.Null(provider.GetService<IServiceAccountTokenProvider>());
    }

    [Theory]
    [InlineData(typeof(IUploadServiceClient))]
    [InlineData(typeof(ICountryServiceClient))]
    public async Task DownstreamClient_UsesAuthServiceExchangeHandler(Type clientType)
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ExternalServices:UploadService"] = "https://upload.test",
                ["ExternalServices:CountryService"] = "https://country.test"
            })
            .Build();
        var capture = new AuthorizationCaptureHandler();

        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IAuthServiceTokenProvider>(new StubTokenProvider());
        services.AddTransient<AuthServiceTokenExchangeHandler>();
        services.AddSingleton<IHttpMessageHandlerBuilderFilter>(new CapturingPrimaryHandlerFilter(capture));
        services.AddInfrastructure(configuration);

        await using var provider = services.BuildServiceProvider();
        var typedClient = provider.GetRequiredService(clientType);
        var clientField = typedClient.GetType().GetField("_httpClient", BindingFlags.Instance | BindingFlags.NonPublic);
        var client = Assert.IsType<HttpClient>(clientField?.GetValue(typedClient));

        using var response = await client.GetAsync("https://boundary.test/authentication-probe", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new AuthenticationHeaderValue("Bearer", ExpectedToken), capture.Authorization);
    }

    private static void AddExchangeConfiguration(ConfigurationManager configuration)
    {
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        configuration["ServiceAuthentication:ClientId"] = "service-contact-service";
        configuration["ServiceAuthentication:ClientSecret"] = "contact-test-secret-with-at-least-32-bytes";
        configuration["Services:AuthService:BaseUrl"] = "http://127.0.0.1:5000";
        configuration["Jwt:PublicKey"] = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());
        configuration["Jwt:Issuer"] = "https://api.maliev.com";
        configuration["Jwt:Audience"] = "https://api.maliev.com";
    }

    private static string ReadRepositoryFile(params string[] segments)
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            Path.Combine(segments)));

        Assert.True(File.Exists(path), $"Could not find source file: {path}");
        return File.ReadAllText(path);
    }

    private sealed class StubTokenProvider : IAuthServiceTokenProvider
    {
        public Task<string> GetTokenAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(ExpectedToken);
    }

    private sealed class AuthorizationCaptureHandler : HttpMessageHandler
    {
        public AuthenticationHeaderValue? Authorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private sealed class CapturingPrimaryHandlerFilter(HttpMessageHandler primaryHandler)
        : IHttpMessageHandlerBuilderFilter
    {
        public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next) => builder =>
        {
            next(builder);
            for (var index = builder.AdditionalHandlers.Count - 1; index >= 0; index--)
            {
                if (builder.AdditionalHandlers[index].GetType().FullName?.Contains(
                        "ServiceDiscovery",
                        StringComparison.Ordinal) == true ||
                    builder.AdditionalHandlers[index].GetType().FullName?.Contains(
                        "ResolvingHttpDelegatingHandler",
                        StringComparison.Ordinal) == true)
                {
                    builder.AdditionalHandlers.RemoveAt(index);
                }
            }

            builder.PrimaryHandler = primaryHandler;
        };
    }
}
