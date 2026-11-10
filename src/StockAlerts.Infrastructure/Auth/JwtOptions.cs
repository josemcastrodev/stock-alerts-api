using System.ComponentModel.DataAnnotations;

namespace StockAlerts.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; init; } = "stock-alerts-api";

    [Required]
    public string Audience { get; init; } = "stock-alerts-api";

    /// <summary>
    /// Symmetric signing key. It has no default on purpose: only appsettings.Development.json carries a
    /// throwaway one, so any other environment fails at startup until a real key is provided.
    /// </summary>
    [Required(ErrorMessage = "Jwt:Key is required. Outside Development, set the Jwt__Key environment variable.")]
    [MinLength(32, ErrorMessage = "Jwt:Key must be at least 32 characters (256 bits for HMAC-SHA256).")]
    public string Key { get; init; } = string.Empty;

    [Range(1, 1440)]
    public int ExpirationMinutes { get; init; } = 60;
}
