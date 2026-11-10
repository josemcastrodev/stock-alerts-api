namespace StockAlerts.Core.Entities;

public enum StockStatus
{
    /// <summary>No check has succeeded yet. This is not the same as out of stock.</summary>
    Unknown,
    InStock,
    OutOfStock,
}
