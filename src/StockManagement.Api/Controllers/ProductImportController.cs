using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockManagement.Application.DTOs.Products;
using StockManagement.Application.Products;
using StockManagement.Application.Security;

namespace StockManagement.Api.Controllers;

[ApiController]
[Route("api/products/import")]
[Authorize(Policy = Permissions.ProductsManage)]
public sealed class ProductImportController(IProductImportService service, ILogger<ProductImportController> logger) : ControllerBase
{
    private const long MaximumFileBytes = 5 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    [HttpGet("template")]
    public async Task<IActionResult> Template(CancellationToken cancellationToken)
    {
        try
        {
            return File(await service.CreateTemplateAsync(cancellationToken),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "mal_sablon.xlsx");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception)
        {
            logger.LogError(exception, "Məhsul idxalı şablonu hazırlanmadı.");
            return Result(ProductImportResult.Failure(new ProductImportError(0, "file", "Şablon hazırlanmadı. Yenidən cəhd edin.")), 500);
        }
    }

    [HttpPost]
    [Authorize(Policy = Permissions.StockIn)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaximumFileBytes + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaximumFileBytes + 64 * 1024)]
    public async Task<IActionResult> Import(IFormFile? file, [FromQuery] Guid? warehouseId,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return Result(ProductImportResult.Failure(new ProductImportError(0, "file", "Fayl boşdur və ya seçilməyib.")), 400);
        if (file.Length > MaximumFileBytes)
            return Result(ProductImportResult.Failure(new ProductImportError(0, "file", "Faylın ölçüsü 5 MB-dan böyük ola bilməz.")), 400);
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var appUserId)) return Unauthorized();
        await using var content = file.OpenReadStream();
        var result = await service.ImportAsync(content, file.FileName, appUserId, warehouseId, cancellationToken);
        return Result(result, result.Success ? 200 : 400);
    }

    private static JsonResult Result(ProductImportResult result, int statusCode) => new(result, JsonOptions)
    {
        StatusCode = statusCode,
        ContentType = "application/json; charset=utf-8"
    };
}
