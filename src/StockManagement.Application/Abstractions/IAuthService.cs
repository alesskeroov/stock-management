using StockManagement.Application.DTOs.Auth;

namespace StockManagement.Application.Abstractions;

public interface IAuthService
{
    Task<AuthResult<EmployeeCreatedDto>> RegisterAsync(RegisterEmployeeRequest request,
        CancellationToken cancellationToken = default);
    Task<AuthResult<AuthTokenResponse>> LoginAsync(LoginRequest request,
        CancellationToken cancellationToken = default);
    Task<AuthResult<AuthTokenResponse>> RefreshAsync(string refreshToken,
        CancellationToken cancellationToken = default);
    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);
}
