using Gas365.Api;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Gas365.UnitTests;

public sealed class HealthResponseTests
{
    [Fact]
    public void ResponseDoesNotExposeFailureDetails()
    {
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>
        {
            ["postgres"] = new(HealthStatus.Unhealthy, "private-host", TimeSpan.Zero,
                new Exception("sensitive connection details"), null)
        }, TimeSpan.Zero);

        var response = HealthResponse.From(report);

        Assert.Equal("Unhealthy", response.Status);
        Assert.Equal("Unhealthy", response.Database);
        Assert.DoesNotContain("private-host", response.ToString());
        Assert.DoesNotContain("sensitive", response.ToString());
    }

    [Fact]
    public void MissingDatabaseCheckIsNotReportedAsHealthy()
    {
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(), TimeSpan.Zero);
        Assert.Equal("Unknown", HealthResponse.From(report).Database);
    }
}
