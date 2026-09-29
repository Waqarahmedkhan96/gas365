namespace Gas365.Api;

// Outside Development the API refuses to start with missing or unsafe settings instead of failing on first use.
public static class ProductionConfiguration
{
    public static string[] Problems(IConfiguration configuration)
    {
        var problems = new List<string>();
        void Required(string key) { if (string.IsNullOrWhiteSpace(configuration[key])) problems.Add($"{key} is required."); }
        Required("ConnectionStrings:Database");
        Required("DataProtection:KeyPath");
        Required("Mail:Host");
        Required("Mail:From");
        Required("Mail:PublicOrigin");
        Required("Platform:AdminEmails");
        var origin = configuration["Mail:PublicOrigin"];
        if (!string.IsNullOrWhiteSpace(origin) && !origin.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            problems.Add("Mail:PublicOrigin must use https:// so email links are not sent over plain HTTP.");
        if (configuration.GetConnectionString("Database") is { } database && database.Contains("Password=postgres", StringComparison.OrdinalIgnoreCase))
            problems.Add("ConnectionStrings:Database uses a default password.");
        return [.. problems];
    }

    public static void Validate(IConfiguration configuration, IHostEnvironment environment)
    {
        if (environment.IsDevelopment()) return;
        var problems = Problems(configuration);
        if (problems.Length > 0) throw new InvalidOperationException("Gas365 configuration is incomplete: " + string.Join(" ", problems));
    }
}
