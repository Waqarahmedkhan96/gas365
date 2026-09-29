using Gas365.Api;
using Microsoft.Extensions.Configuration;

namespace Gas365.UnitTests;

public sealed class ProductionConfigurationTests
{
    private static IConfiguration Config(Dictionary<string, string?> values) => new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    private static Dictionary<string, string?> Complete() => new()
    {
        ["ConnectionStrings:Database"] = "Host=db;Database=gas365;Username=gas365;Password=long-random-secret",
        ["DataProtection:KeyPath"] = "/keys",
        ["Mail:Host"] = "smtp.example.test",
        ["Mail:From"] = "noreply@gas365.pk",
        ["Mail:PublicOrigin"] = "https://gas365.pk",
        ["Platform:AdminEmails"] = "admin@gas365.pk"
    };

    [Fact]
    public void CompleteProductionSettingsPass() => Assert.Empty(ProductionConfiguration.Problems(Config(Complete())));

    [Fact]
    public void MissingSecretsAndUnsafeValuesAreReported()
    {
        var values = Complete(); values["Mail:Host"] = ""; values["Mail:PublicOrigin"] = "http://gas365.pk"; values["ConnectionStrings:Database"] = "Host=db;Password=postgres";
        values.Remove("Platform:AdminEmails");
        var problems = ProductionConfiguration.Problems(Config(values));
        Assert.Contains("Mail:Host is required.", problems);
        Assert.Contains("Platform:AdminEmails is required.", problems);
        Assert.Contains(problems, p => p.Contains("https://"));
        Assert.Contains(problems, p => p.Contains("default password"));
    }
}
