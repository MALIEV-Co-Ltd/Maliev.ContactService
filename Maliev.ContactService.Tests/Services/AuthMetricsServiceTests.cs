using Maliev.ContactService.Api.Services.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Moq;
using System.Diagnostics.Metrics;
using Xunit;

namespace Maliev.ContactService.Tests.Services;

public class AuthMetricsServiceTests
{
    private readonly Mock<IMeterFactory> _meterFactoryMock;
    private readonly IConfiguration _configuration;
    private readonly Meter _meter;

    public AuthMetricsServiceTests()
    {
        _meterFactoryMock = new Mock<IMeterFactory>();
        _meter = new Meter("contactservice-auth-meter");
        _meterFactoryMock.Setup(x => x.Create(It.IsAny<MeterOptions>())).Returns(_meter);

        var configDict = new Dictionary<string, string?>
        {
            ["Service:Name"] = "ContactService",
            ["Service:Version"] = "1.0.0",
            ["Service:Region"] = "global",
            ["ASPNETCORE_ENVIRONMENT"] = "Testing"
        };
        _configuration = new ConfigurationBuilder().AddInMemoryCollection(configDict).Build();
    }

    [Fact]
    public void RecordSuccess_IncrementsCounter()
    {
        // Arrange
        var service = new AuthMetricsService(_meterFactoryMock.Object, _configuration);
        var collector = new MetricCollector<long>(_meter, "contact_auth_success_total");

        // Act
        service.RecordSuccess("contact.read");

        // Assert
        var measurements = collector.GetMeasurementSnapshot();
        Assert.Single(measurements);
        Assert.Equal(1, measurements[0].Value);
        Assert.Equal("contact.read", measurements[0].Tags["permission"]);
        Assert.Equal("ContactService", measurements[0].Tags["service_name"]);
    }

    [Fact]
    public void RecordFailure_IncrementsCounter()
    {
        // Arrange
        var service = new AuthMetricsService(_meterFactoryMock.Object, _configuration);
        var collector = new MetricCollector<long>(_meter, "contact_auth_failure_total");

        // Act
        service.RecordFailure("contact.write", "Forbidden");

        // Assert
        var measurements = collector.GetMeasurementSnapshot();
        Assert.Single(measurements);
        Assert.Equal(1, measurements[0].Value);
        Assert.Equal("contact.write", measurements[0].Tags["permission"]);
        Assert.Equal("Forbidden", measurements[0].Tags["reason"]);
    }
}
