using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using StockManagement.Application.Abstractions;
using StockManagement.Application.DTOs.Auth;
using StockManagement.Application.Security;
using StockManagement.Infrastructure.Identity;
using StockManagement.Infrastructure.Persistence;

namespace StockManagement.Infrastructure.Authentication;

public sealed class AuthService(
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signInManager,
    RoleManager<IdentityRole<Guid>> roles,
    StockManagementDbContext db,
    JwtOptions jwtOptions,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<AuthResult<EmployeeCreatedDto>> RegisterAsync(RegisterEmployeeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Roles.All.Contains(request.Role, StringComparer.Ordinal))
            return AuthResult<EmployeeCreatedDto>.Failure(400, "İcazə verilməyən rol.");

        var role = await roles.FindByNameAsync(request.Role);
        if (role is null)
            return AuthResult<EmployeeCreatedDto>.Failure(500, "Rollar bazada yaradılmayıb.");

        var user = new ApplicationUser
        {
            UserName = request.UserName.Trim(),
            Email = request.Email.Trim(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            EmailConfirmed = true
        };

        var createResult = await users.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
            return AuthResult<EmployeeCreatedDto>.Failure(400, string.Join(" ", createResult.Errors.Select(x => x.Description)));

        var roleResult = await users.AddToRoleAsync(user, request.Role);
        if (!roleResult.Succeeded)
        {
            await users.DeleteAsync(user);
            logger.LogError("İşçi yaradıldı, lakin rol təyin edilə bilmədi. UserId: {UserId}", user.Id);
            return AuthResult<EmployeeCreatedDto>.Failure(500, "İşçi üçün rol təyin etmək mümkün olmadı.");
        }

        return AuthResult<EmployeeCreatedDto>.Success(
            new(user.Id, user.UserName!, user.Email!, user.FirstName, user.LastName, request.Role), 201);
    }

    public async Task<AuthResult<AuthTokenResponse>> LoginAsync(LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var identifier = request.Identifier.Trim();
        var user = await users.FindByNameAsync(identifier)
            ?? (identifier.Contains('@') ? await users.FindByEmailAsync(identifier) : null);

        if (user is null || !user.IsActive)
            return AuthResult<AuthTokenResponse>.Failure(401, "İstifadəçi adı/e-poçt və ya şifrə yanlışdır.");

        var signInResult = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (signInResult.IsLockedOut)
            return AuthResult<AuthTokenResponse>.Failure(423, "Çoxsaylı uğursuz cəhdə görə hesab müvəqqəti bloklanıb.");
        if (!signInResult.Succeeded)
            return AuthResult<AuthTokenResponse>.Failure(401, "İstifadəçi adı/e-poçt və ya şifrə yanlışdır.");

        return AuthResult<AuthTokenResponse>.Success(await CreateTokenPairAsync(user, cancellationToken));
    }

    public async Task<AuthResult<AuthTokenResponse>> RefreshAsync(string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var hash = HashToken(refreshToken);
        var stored = await db.RefreshTokens.Include(x => x.User)
            .SingleOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);
        if (stored is null)
            return AuthResult<AuthTokenResponse>.Failure(401, "Refresh token etibarsızdır.");

        var now = DateTime.UtcNow;
        if (stored.RevokedAtUtc is not null)
        {
            if (stored.ReplacedByTokenHash is not null)
            {
                var activeTokens = await db.RefreshTokens
                    .Where(x => x.UserId == stored.UserId && x.RevokedAtUtc == null && x.ExpiresAtUtc > now)
                    .ToListAsync(cancellationToken);
                foreach (var activeToken in activeTokens) activeToken.Revoke();
                await db.SaveChangesAsync(cancellationToken);
            }

            return AuthResult<AuthTokenResponse>.Failure(401, "Refresh token artıq istifadə olunub və ya ləğv edilib.");
        }

        if (stored.ExpiresAtUtc <= now || !stored.User.IsActive)
        {
            stored.Revoke();
            await db.SaveChangesAsync(cancellationToken);
            return AuthResult<AuthTokenResponse>.Failure(401, "Refresh token-in vaxtı bitib.");
        }

        var pair = await CreateTokenPairAsync(stored.User, cancellationToken, saveRefreshToken: false);
        stored.Revoke(HashToken(pair.RefreshToken));
        await db.SaveChangesAsync(cancellationToken);
        return AuthResult<AuthTokenResponse>.Success(pair);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var hash = HashToken(refreshToken);
        var stored = await db.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);
        if (stored is null || stored.RevokedAtUtc is not null) return;
        stored.Revoke();
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<AuthTokenResponse> CreateTokenPairAsync(ApplicationUser user,
        CancellationToken cancellationToken, bool saveRefreshToken = true)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(jwtOptions.AccessTokenMinutes);
        var userRoles = await users.GetRolesAsync(user);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName ?? string.Empty),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new("given_name", user.FirstName),
            new("family_name", user.LastName)
        };

        foreach (var roleName in userRoles)
        {
            claims.Add(new Claim("role", roleName));
            var role = await roles.FindByNameAsync(roleName);
            if (role is null) continue;
            var roleClaims = await roles.GetClaimsAsync(role);
            claims.AddRange(roleClaims.Where(x => x.Type == "permission"));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));
        var signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(jwtOptions.Issuer, jwtOptions.Audience, claims,
            now, expires, signingCredentials);
        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        var rawRefreshToken = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(64));
        var refreshEntity = new RefreshToken(user.Id, HashToken(rawRefreshToken), now.AddDays(jwtOptions.RefreshTokenDays));
        db.RefreshTokens.Add(refreshEntity);
        if (saveRefreshToken) await db.SaveChangesAsync(cancellationToken);

        var profile = new AuthenticatedUserDto(user.Id, user.UserName ?? string.Empty, user.Email ?? string.Empty,
            user.FirstName, user.LastName, userRoles.ToArray());
        return new(accessToken, rawRefreshToken, expires, profile);
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
