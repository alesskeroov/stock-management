namespace StockManagement.Infrastructure.Authentication;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "StockManagement.Api";
    public string Audience { get; set; } = "StockManagement.Client";
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
}
