namespace GolfProgress.Api.Contracts.Auth;

public sealed class RegisterRequest
{
    public string Email { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Password { get; init; } = "";
}