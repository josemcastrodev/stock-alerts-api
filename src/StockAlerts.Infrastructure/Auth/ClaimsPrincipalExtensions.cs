using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace StockAlerts.Infrastructure.Auth;

public static class ClaimsPrincipalExtensions
{
    /// <summary>Id of the authenticated user. Only call it on endpoints that require authentication.</summary>
    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new InvalidOperationException("The token has no 'sub' claim."));
}
