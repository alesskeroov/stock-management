using StockManagement.Api;
using StockManagement.Application;
using StockManagement.Application.Security;
using StockManagement.Infrastructure.Authentication;
using StockManagement.Infrastructure;
using StockManagement.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
var configuredSigningKey = builder.Configuration["Jwt:SigningKey"];
if (string.IsNullOrWhiteSpace(configuredSigningKey))
{
    if (!builder.Environment.IsDevelopment())
        throw new InvalidOperationException("Production mühitində Jwt:SigningKey secret-i təyin edilməlidir.");
    configuredSigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    Console.Error.WriteLine("Development üçün müvəqqəti JWT açarı yaradıldı; restart zamanı tokenlər etibarsız olacaq.");
}
if (Encoding.UTF8.GetByteCount(configuredSigningKey) < 32)
    throw new InvalidOperationException("Jwt:SigningKey ən azı 32 bayt olmalıdır.");
jwtOptions.SigningKey = configuredSigningKey;
builder.Services.AddSingleton(jwtOptions);

builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
{
    var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
        ?? ["http://localhost:5173", "http://127.0.0.1:5173"];
    policy.WithOrigins(origins.Append("http://localhost:8000").Distinct().ToArray())
        .AllowAnyHeader().AllowAnyMethod();
}));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "unique_name",
            RoleClaimType = "role"
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    foreach (var permission in Permissions.All)
        options.AddPolicy(permission, policy => policy.RequireClaim("permission", permission));
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "JWT access token daxil edin."
    });
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            }, Array.Empty<string>()
        }
    });
});
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

var maintenanceFlag = Array.IndexOf(args, "--maintenance");
if (maintenanceFlag >= 0)
{
    var command = maintenanceFlag + 1 < args.Length ? args[maintenanceFlag + 1] : string.Empty;
    await using var scope = app.Services.CreateAsyncScope();
    if (command.Equals("opening-balance", StringComparison.OrdinalIgnoreCase))
    {
        var userFlag = Array.IndexOf(args, "--created-by");
        if (userFlag < 0 || userFlag + 1 >= args.Length || !Guid.TryParse(args[userFlag + 1], out var createdBy))
        {
            Console.Error.WriteLine("opening-balance üçün --created-by <mövcud istifadəçi GUID-i> tələb olunur.");
            return 2;
        }
        var count = await scope.ServiceProvider.GetRequiredService<OpeningBalanceCommand>().ExecuteAsync(createdBy);
        Console.WriteLine(count == 0
            ? "Açılış qalığı artıq var; heç bir hərəkət əlavə edilmədi."
            : $"{count} OPENING_BALANCE hərəkəti yaradıldı.");
        return 0;
    }

    if (command.Equals("reconcile", StringComparison.OrdinalIgnoreCase))
    {
        var differences = await scope.ServiceProvider.GetRequiredService<StockReconciliationCommand>().ExecuteAsync();
        if (differences.Count == 0) Console.WriteLine("Cache və ledger uyğun gəlir.");
        else
        {
            foreach (var difference in differences)
                Console.WriteLine($"product={difference.ProductId} warehouse={difference.WarehouseId} cache={difference.CachedQuantity} ledger={difference.MovementQuantity}");
            Console.WriteLine($"{differences.Count} fərq tapıldı; heç bir məlumat dəyişdirilmədi.");
        }
        return differences.Count == 0 ? 0 : 1;
    }

    if (command.Equals("procurement-demo", StringComparison.OrdinalIgnoreCase))
    {
        var count = 20;
        var countFlag = Array.IndexOf(args, "--count");
        if (countFlag >= 0 && (countFlag + 1 >= args.Length || !int.TryParse(args[countFlag + 1], out count) || count is < 1 or > 50))
        {
            Console.Error.WriteLine("procurement-demo üçün --count 1-50 aralığında olmalıdır (defolt: 20).");
            return 2;
        }

        await DemoDataSeeder.SeedAsync(app.Services);
        await AuthSeeder.SeedAsync(app.Services, app.Configuration);
        var services = scope.ServiceProvider;
        var seeded = await new ProcurementDemoDataCommand(
            services.GetRequiredService<StockManagement.Infrastructure.Persistence.StockManagementDbContext>(),
            services.GetRequiredService<PurchaseOrderService>(),
            services.GetRequiredService<GoodsReceiptService>(),
            services.GetRequiredService<OpeningBalanceCommand>()).ExecuteAsync(count);
        Console.WriteLine($"Demo seed tamamlandı. Yeni təchizatçı: {seeded.Suppliers}; yeni PO: {seeded.PurchaseOrders}; yeni GRN: {seeded.GoodsReceipts}.");
        Console.WriteLine("Əmr təkrar işlədikdə mövcud demo PO-lar təkrarlanmır.");
        return 0;
    }

    Console.Error.WriteLine("Əmr: --maintenance opening-balance --created-by <user-guid> | --maintenance reconcile | --maintenance procurement-demo [--count 20]");
    return 2;
}

await DemoDataSeeder.SeedAsync(app.Services);
await AuthSeeder.SeedAsync(app.Services, app.Configuration);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/", () => Results.Redirect("/swagger")).AllowAnonymous();

app.Run();
return 0;
