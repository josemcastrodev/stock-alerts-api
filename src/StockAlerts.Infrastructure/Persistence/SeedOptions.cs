namespace StockAlerts.Infrastructure.Persistence;

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    /// <summary>When both admin values are set, that admin account is created at startup if it does not exist.</summary>
    public string? AdminEmail { get; init; }

    public string? AdminPassword { get; init; }

    /// <summary>Adds a small demo catalog to an empty database.</summary>
    public bool DemoData { get; init; }
}
