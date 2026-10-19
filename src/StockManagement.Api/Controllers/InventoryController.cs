using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockManagement.Application.Products;
using StockManagement.Application.Security;
using StockManagement.Application.Abstractions;

namespace StockManagement.Api.Controllers;

[ApiController]
[Route("api/inventory")]
[Authorize]
public sealed class InventoryController(InventoryService service, IQrCodeGenerator qrCodeGenerator) : ControllerBase
{
    [HttpGet("warehouses")]
    [Authorize(Policy = Permissions.StockIn)]
    public async Task<ActionResult<IReadOnlyList<WarehouseOption>>> GetWarehouses(CancellationToken cancellationToken) =>
        Ok(await service.GetWarehousesAsync(cancellationToken));

    [HttpGet("products/{productId:guid}/batches")]
    [Authorize(Policy = Permissions.ProductsRead)]
    public async Task<ActionResult<IReadOnlyList<BatchInventoryItem>>> GetBatches(Guid productId, CancellationToken cancellationToken) =>
        Ok(await service.GetActiveBatchesAsync(productId, cancellationToken));

    [HttpGet("scan/{code}")]
    [Authorize(Policy = Permissions.ProductsRead)]
    public async Task<ActionResult<InventoryScanResult>> Scan(string code, CancellationToken cancellationToken)
    {
        var result = await service.FindByScanCodeAsync(code, cancellationToken);
        return result is null ? NotFound(new { error = "Barkod və ya partiya kodu tapılmadı." }) : Ok(result);
    }

    [HttpGet("scan/{code}/qr")]
    [Authorize(Policy = Permissions.ProductsRead)]
    public IActionResult GetQrCode(string code) =>
        string.IsNullOrWhiteSpace(code) ? BadRequest() : Content(qrCodeGenerator.ToSvg(code), "image/svg+xml");

    [HttpPost("receipts")]
    [Authorize(Policy = Permissions.StockIn)]
    public async Task<ActionResult<StockReceiptResult>> Receive(ReceiveStockRequest request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var appUserId)) return Unauthorized();
        try { return Ok(await service.ReceiveAsync(request, appUserId, cancellationToken)); }
        catch (KeyNotFoundException exception) { return NotFound(new { error = exception.Message }); }
        catch (ArgumentException exception) { return BadRequest(new { error = exception.Message }); }
        catch (InvalidOperationException exception) { return Conflict(new { error = exception.Message }); }
    }
}
