using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StockManagement.Application.Abstractions;
using StockManagement.Application.DTOs.Auth;
using StockManagement.Application.Security;

namespace StockManagement.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IAuthService authService,
    IValidator<RegisterEmployeeRequest> registerValidator,
    IValidator<LoginRequest> loginValidator,
    IValidator<RefreshTokenRequest> refreshValidator,
    IValidator<LogoutRequest> logoutValidator) : ControllerBase
{
    [HttpPost("register")]
    [Authorize(Roles = Roles.Admin)]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<EmployeeCreatedDto>> Register(RegisterEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await registerValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return ValidationProblem(new ValidationProblemDetails(validation.ToErrorsDictionary()));

        var result = await authService.RegisterAsync(request, cancellationToken);
        return result.Succeeded
            ? StatusCode(result.StatusCode, result.Value)
            : StatusCode(result.StatusCode, new { error = result.Error });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthTokenResponse>> Login(LoginRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await loginValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return ValidationProblem(new ValidationProblemDetails(validation.ToErrorsDictionary()));

        var result = await authService.LoginAsync(request, cancellationToken);
        return result.Succeeded
            ? Ok(result.Value)
            : StatusCode(result.StatusCode, new { error = result.Error });
    }

    [HttpPost("refresh-token")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthTokenResponse>> RefreshToken(RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await refreshValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return ValidationProblem(new ValidationProblemDetails(validation.ToErrorsDictionary()));

        var result = await authService.RefreshAsync(request.RefreshToken, cancellationToken);
        return result.Succeeded
            ? Ok(result.Value)
            : StatusCode(result.StatusCode, new { error = result.Error });
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Logout(LogoutRequest request, CancellationToken cancellationToken)
    {
        var validation = await logoutValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return ValidationProblem(new ValidationProblemDetails(validation.ToErrorsDictionary()));

        await authService.LogoutAsync(request.RefreshToken, cancellationToken);
        return NoContent();
    }
}
