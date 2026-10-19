using FluentValidation;
using StockManagement.Application.DTOs.Auth;
using StockManagement.Application.Security;

namespace StockManagement.Application.Validation.Auth;

public sealed class RegisterEmployeeRequestValidator : AbstractValidator<RegisterEmployeeRequest>
{
    public RegisterEmployeeRequestValidator()
    {
        RuleFor(x => x.UserName).NotEmpty().MaximumLength(64)
            .Matches("^[A-Za-z0-9._-]+$").WithMessage("İstifadəçi adı hərf, rəqəm, nöqtə, alt xətt və defisdən ibarət olmalıdır.");
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(12)
            .WithMessage("Şifrə ən azı 12 simvoldan ibarət olmalıdır.")
            .Matches("[A-Z]").WithMessage("Şifrədə ən azı bir böyük hərf olmalıdır.")
            .Matches("[a-z]").WithMessage("Şifrədə ən azı bir kiçik hərf olmalıdır.")
            .Matches("[0-9]").WithMessage("Şifrədə ən azı bir rəqəm olmalıdır.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Şifrədə ən azı bir xüsusi simvol olmalıdır.");
        RuleFor(x => x.Role).Must(role => Roles.All.Contains(role, StringComparer.Ordinal))
            .WithMessage("Rol Admin, WarehouseKeeper və ya Manager olmalıdır.");
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Identifier).NotEmpty().MaximumLength(320);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}

public sealed class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator() => RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(512);
}

public sealed class LogoutRequestValidator : AbstractValidator<LogoutRequest>
{
    public LogoutRequestValidator() => RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(512);
}
