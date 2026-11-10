namespace StockAlerts.Core.StockChecks;

public enum StockCheckOutcome
{
    InStock,
    OutOfStock,

    /// <summary>The check could not produce a reliable answer (timeout, HTTP error, unexpected page...).</summary>
    Failed,
}

/// <summary>
/// What a stock source reports for one listing. <see cref="StockCheckOutcome.Failed"/> is an outcome of
/// a check, never a state of the listing: that is why this is a separate enum from <c>StockStatus</c>.
/// </summary>
public sealed record StockCheckResult
{
    private StockCheckResult(StockCheckOutcome outcome, decimal? price = null, string? error = null)
    {
        Outcome = outcome;
        Price = price;
        Error = error;
    }

    public StockCheckOutcome Outcome { get; }
    public decimal? Price { get; }
    public string? Error { get; }

    public static StockCheckResult InStock(decimal price) => new(StockCheckOutcome.InStock, price);

    public static StockCheckResult OutOfStock() => new(StockCheckOutcome.OutOfStock);

    public static StockCheckResult Failed(string error) => new(StockCheckOutcome.Failed, error: error);
}
