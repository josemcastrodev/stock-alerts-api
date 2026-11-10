using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StockAlerts.Api.Contracts;
using StockAlerts.Core.Entities;
using StockAlerts.Infrastructure.Auth;
using StockAlerts.Infrastructure.Persistence;

// Inside a controller, "User" is the ClaimsPrincipal, so the entity's NormalizeEmail is imported directly.
using static StockAlerts.Core.Entities.User;

namespace StockAlerts.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    AppDbContext db,
    IPasswordHasher<User> passwordHasher,
    JwtTokenService tokens,
    TimeProvider clock) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);

        // Friendly answer for the common case. The unique index on Email is what actually guarantees it.
        if (await db.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "That email is already registered.");
        }

        var user = new User(email, UserRole.User, clock.GetUtcNow());
        user.SetPasswordHash(passwordHasher.HashPassword(user, request.Password));

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created, ToResponse(tokens.CreateToken(user)));
    }

    [HttpPost("login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

        var verification = user is null
            ? PasswordVerificationResult.Failed
            : passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

        if (user is null || verification == PasswordVerificationResult.Failed)
        {
            // Same answer for "no such user" and "wrong password", so the endpoint does not reveal which emails exist.
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid email or password.");
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.SetPasswordHash(passwordHasher.HashPassword(user, request.Password));
            await db.SaveChangesAsync(cancellationToken);
        }

        return ToResponse(tokens.CreateToken(user));
    }

    private static AuthResponse ToResponse(AccessToken token) => new(token.Value, token.ExpiresAt);
}
