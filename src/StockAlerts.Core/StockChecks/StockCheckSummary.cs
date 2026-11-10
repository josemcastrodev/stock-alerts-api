namespace StockAlerts.Core.StockChecks;

public sealed record StockCheckSummary(int Checked, int Failed, int Restocked, int AlertsCreated);
