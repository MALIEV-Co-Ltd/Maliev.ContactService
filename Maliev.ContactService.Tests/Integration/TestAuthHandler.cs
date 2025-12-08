using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Maliev.ContactService.Tests.Integration;

public class TestAuthHandlerOptions : AuthenticationSchemeOptions
{
    public RSA RsaKey { get; set; } = null!;
}

public class TestAuthHandler : AuthenticationHandler<TestAuthHandlerOptions>
{
    public const string TestScheme = "Test";

    public TestAuthHandler(IOptionsMonitor<TestAuthHandlerOptions> options,
        ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = GenerateTestToken();
        Context.Request.Headers.Authorization = $"Bearer {token}";

        var claims = new[] {
            new Claim(ClaimTypes.Name, "Test User"),
            new Claim(ClaimTypes.Role, "Admin")
        };
        var identity = new ClaimsIdentity(claims, TestScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        var result = AuthenticateResult.Success(ticket);

        return Task.FromResult(result);
    }

    private string GenerateTestToken()
    {
        var rsaKey = Options.RsaKey;
        if (rsaKey == null)
        {
            throw new InvalidOperationException("RsaKey is not configured in TestAuthHandlerOptions.");
        }

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, "testuser@example.com"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, "Admin")
        };

        var securityKey = new RsaSecurityKey(rsaKey);
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256);

        var token = new JwtSecurityToken(
            issuer: "test_issuer",
            audience: "test_audience",
            claims: claims,
            expires: DateTime.Now.AddMinutes(30),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
