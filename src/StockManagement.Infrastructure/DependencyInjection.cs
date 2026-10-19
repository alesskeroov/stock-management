using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Identity;
using StockManagement.Application.Abstractions;
using StockManagement.Application.Abstractions.Repositories;
using StockManagement.Application.Products.Repositories;
using StockManagement.Infrastructure.Authentication;
using StockManagement.Infrastructure.Identity;
using StockManagement.Infrastructure.Persistence;
using StockManagement.Infrastructure.Repositories;
using StockManagement.Infrastructure.Services;

namespace StockManagement.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("StockManagementDb")
            ?? throw new InvalidOperationException("Connection string 'StockManagementDb' tapılmadı.");
        services.AddDbContext<StockManagementDbContext>(options => options.UseSqlServer(connectionString));
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<StockManagementDbContext>()
            .AddSignInManager();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped(typeof(IGenericRepository<>), typeof(EfRepository<>));
        services.AddScoped<IProductRepository, EfProductRepository>();
        services.AddScoped<IInventoryRepository, EfInventoryRepository>();
        services.AddSingleton<IQrCodeGenerator, QrCodeGenerator>();
        return services;
    }
}
