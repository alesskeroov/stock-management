using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockManagement.Application.DTOs.GoodsReceipts;
using StockManagement.Application.Security;
using StockManagement.Infrastructure.Services;

namespace StockManagement.Api.Controllers;

[ApiController]
[Route("api/goods-receipts")]
public sealed class GoodsReceiptsController(GoodsReceiptService service) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.PurchaseOrderView)]
    public async Task<ActionResult<GoodsReceiptPage>> GetPage([FromQuery] string? page, [FromQuery] string? size,
        [FromQuery] Guid? purchaseOrderId, [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate,
        CancellationToken cancellationToken)
    {
        if (!TryPositiveInt(page, 1, out var currentPage) || !TryPositiveInt(size, GoodsReceiptService.DefaultPageSize, out var pageSize))
            return BadRequest(new { error = "page və size müsbət tam ədəd olmalıdır." });
        if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
            return BadRequest(new { error = "fromDate toDate tarixindən sonra ola bilməz." });
        pageSize = Math.Min(pageSize, GoodsReceiptService.MaximumPageSize);
        return Ok(await service.GetPageAsync(currentPage, pageSize, purchaseOrderId, fromDate, toDate, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.PurchaseOrderView)]
    public async Task<ActionResult<GoodsReceiptDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var receipt = await service.GetAsync(id, cancellationToken);
        return receipt is null ? NotFound() : Ok(receipt);
    }

    [HttpPost]
    [Authorize(Policy = Permissions.GoodsReceiptCreate)]
    [ProducesResponseType(typeof(GoodsReceiptDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<GoodsReceiptDto>> Create(CreateGoodsReceiptRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var receivedBy)) return Unauthorized();
        try
        {
            var receipt = await service.CreateAsync(request, receivedBy, idempotencyKey, cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = receipt.Id }, receipt);
        }
        catch (GoodsReceiptValidationException exception) { return UnprocessableEntity(new { error = exception.Message }); }
        catch (GoodsReceiptConflictException exception) { return Conflict(new { error = exception.Message }); }
        catch (KeyNotFoundException exception) { return NotFound(new { error = exception.Message }); }
    }

    [HttpPost("{id:guid}/reverse")]
    [Authorize(Policy = Permissions.GoodsReceiptReverse)]
    [ProducesResponseType(typeof(GoodsReceiptDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<GoodsReceiptDto>> Reverse(Guid id, ReverseGoodsReceiptRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var userId)) return Unauthorized();
        try { return Ok(await service.ReverseAsync(id, userId, request.Reason, cancellationToken)); }
        catch (GoodsReceiptValidationException exception) { return UnprocessableEntity(new { error = exception.Message }); }
        catch (GoodsReceiptConflictException exception) { return Conflict(new { error = exception.Message }); }
        catch (KeyNotFoundException exception) { return NotFound(new { error = exception.Message }); }
    }

    private static bool TryPositiveInt(string? value, int fallback, out int parsed)
    {
        if (string.IsNullOrWhiteSpace(value)) { parsed = fallback; return true; }
        return int.TryParse(value, out parsed) && parsed > 0;
    }
}
