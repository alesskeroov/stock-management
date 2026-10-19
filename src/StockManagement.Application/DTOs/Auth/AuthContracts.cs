namespace StockManagement.Application.DTOs.Auth;

public sealed record RegisterEmployeeRequest(
    string UserName,
    string Email,
    string FirstName,
    string LastName,
    string Password,
    string Role);

public sealed record LoginRequest(string Identifier, string Password);
public sealed record RefreshTokenRequest(string RefreshToken);
public sealed record LogoutRequest(string RefreshToken);

public sealed record AuthenticatedUserDto(Guid Id, string UserName, string Email,
    string FirstName, string LastName, IReadOnlyCollection<string> Roles);

public sealed record AuthTokenResponse(string AccessToken, string RefreshToken,
    DateTime AccessTokenExpiresAtUtc, AuthenticatedUserDto User);

public sealed record EmployeeCreatedDto(Guid Id, string UserName, string Email,
    string FirstName, string LastName, string Role);

public sealed record AuthResult<T>(bool Succeeded, T? Value, int StatusCode, string? Error)
{
    public static AuthResult<T> Success(T value, int statusCode = 200) => new(true, value, statusCode, null);
    public static AuthResult<T> Failure(int statusCode, string error) => new(false, default, statusCode, error);
}
