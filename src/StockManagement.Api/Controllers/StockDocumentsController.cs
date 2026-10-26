using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockManagement.Application.DTOs.StockDocuments;
using StockManagement.Application.Security;
using StockManagement.Infrastructure.Services;

namespace StockManagement.Api.Controllers;

[ApiController]
[Route("api/documents")]
[Authorize]
public sealed class StockDocumentsController(StockDocumentService service) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.ProductsRead)]
    public async Task<ActionResult<StockDocumentPage>> GetPage([FromQuery] string? page, [FromQuery] string? size,
        [FromQuery] string? type, CancellationToken cancellationToken)
    {
        if (!TryPositiveInt(page, 1, out var currentPage) || !TryPositiveInt(size, StockDocumentService.DefaultPageSize, out var pageSize))
            return BadRequest(new { error = "page və size müsbət tam ədəd olmalıdır." });
        if (!string.IsNullOrWhiteSpace(type) && type is not ("in" or "out"))
            return BadRequest(new { error = "Sənəd növü yalnız in və ya out ola bilər." });
        return Ok(await service.GetPageAsync(currentPage, Math.Min(pageSize, StockDocumentService.MaximumPageSize), type, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<StockDocumentDto>> Create(CreateStockDocumentRequest request,
        CancellationToken cancellationToken)
    {
        var type = request.Type?.Trim().ToLowerInvariant();
        if (type is not ("in" or "out")) return BadRequest(new { error = "Sənəd növü yalnız in və ya out ola bilər." });
        var permission = type == "in" ? Permissions.StockIn : Permissions.StockOut;
        if (!User.HasClaim("permission", permission)) return Forbid();
        try { return Ok(await service.CreateAsync(request, cancellationToken)); }
        catch (ArgumentException exception) { return BadRequest(new { error = exception.Message }); }
        catch (KeyNotFoundException exception) { return NotFound(new { error = exception.Message }); }
    }

    private static bool TryPositiveInt(string? value, int fallback, out int parsed)
    {
        if (string.IsNullOrEmpty(value)) { parsed = fallback; return true; }
        return int.TryParse(value, out parsed) && parsed > 0;
    }
}
