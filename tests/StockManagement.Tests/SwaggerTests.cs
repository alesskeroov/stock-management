using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using StockManagement.Api.Controllers;
using Swashbuckle.AspNetCore.Swagger;
using Xunit;

namespace StockManagement.Tests;

public sealed class SwaggerTests
{
    [Fact]
    public void ApiDefinitionIncludesExcelUploadAndTemplate()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(ProductImportController).Assembly.GetName().Name,
            EnvironmentName = "Development",
            Args = []
        });
        var services = builder.Services;
        services.AddControllers().AddApplicationPart(typeof(ProductImportController).Assembly);
        services.AddSwaggerGen();
        using var application = builder.Build();
        var document = application.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");

        var upload = document.Paths["/api/products/import"].Operations[Microsoft.OpenApi.Models.OperationType.Post];
        var schema = upload.RequestBody.Content["multipart/form-data"].Schema;
        Assert.Equal("binary", schema.Properties["file"].Format);
        Assert.Contains(upload.Parameters, x => x.Name == "warehouseId"
            && x.In == Microsoft.OpenApi.Models.ParameterLocation.Query);
        Assert.Contains(Microsoft.OpenApi.Models.OperationType.Get,
            document.Paths["/api/products/import/template"].Operations.Keys);
    }
}
