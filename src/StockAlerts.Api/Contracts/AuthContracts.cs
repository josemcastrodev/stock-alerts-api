using System.ComponentModel.DataAnnotations;

namespace StockAlerts.Api.Contracts;

public sealed record RegisterRequest(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MinLength(8), MaxLength(128)] string Password);

public sealed record LoginRequest(
    [Required] string Email,
    [Required] string Password);

public sealed record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt);
