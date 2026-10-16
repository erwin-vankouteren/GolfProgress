namespace GolfProgress.Api.Security;

/// <summary>
/// The minimum password policy (#83), shared by registration and password changes,
/// following NIST SP 800-63B: length matters, composition rules don't. Any characters
/// are allowed, spaces and emoji included, so a passphrase works. Documented in
/// docs/architecture.md, "Passwords".
///
/// It applies when a password is set. Existing passwords keep working at login.
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;

    /// <summary>Long enough for any passphrase; bounds the cost of hashing.</summary>
    public const int MaxLength = 128;

    /// <summary>
    /// Why <paramref name="password"/> can't be used, or null when it can. Length counts
    /// characters as people see them (Unicode code points), not UTF-16 units.
    /// </summary>
    public static string? Check(string password, string? email)
    {
        var length = password.EnumerateRunes().Count();

        if (length < MinLength)
            return $"Use at least {MinLength} characters.";

        if (length > MaxLength)
            return $"Use at most {MaxLength} characters.";

        if (string.IsNullOrWhiteSpace(password))
            return "The password can't be only spaces.";

        if (email is not null && string.Equals(password.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase))
            return "The password can't be your email address.";

        return null;
    }
}
