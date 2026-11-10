using System.ComponentModel.DataAnnotations;

namespace StockAlerts.Api.Workers;

public sealed class StockCheckOptions
{
    public const string SectionName = "StockCheck";

    /// <summary>Set to false on instances that should serve HTTP only.</summary>
    public bool Enabled { get; init; } = true;

    [Range(1, 86_400)]
    public int IntervalSeconds { get; init; } = 60;
}
