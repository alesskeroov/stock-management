using Microsoft.Extensions.DependencyInjection;
using FluentValidation;
using StockManagement.Application.Categories;
using StockManagement.Application.Products;
using StockManagement.Application.Suppliers;
using StockManagement.Application.Validation.Auth;

namespace StockManagement.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ProductService>();
        services.AddScoped<InventoryService>();
        services.AddScoped<StockService>();
        services.AddScoped<CategoryService>();
        services.AddScoped<SupplierService>();
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        return services;
    }
}
