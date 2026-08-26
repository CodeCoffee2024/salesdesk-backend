namespace SalesDesk.Application.Auth;

public sealed class UserDto
{
    public Guid Id { get; init; }

    public string Email { get; init; } = string.Empty;

    public string FullName { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;

    public Guid WorkspaceId { get; init; }
}

public sealed class AuthResponseDto
{
    public string Token { get; init; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; init; }

    public UserDto User { get; init; } = new();
}
