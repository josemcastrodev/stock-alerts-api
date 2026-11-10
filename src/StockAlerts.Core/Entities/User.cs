namespace StockAlerts.Core.Entities;

public sealed class User
{
    public User(string email, UserRole role, DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        Email = NormalizeEmail(email);
        Role = role;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public string Email { get; private set; }
    public string PasswordHash { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public void SetPasswordHash(string passwordHash) => PasswordHash = passwordHash;

    /// <summary>Emails are stored and looked up in this form, so matching does not depend on database collation.</summary>
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
