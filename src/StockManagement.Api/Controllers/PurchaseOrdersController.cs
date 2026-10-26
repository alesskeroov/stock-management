using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockManagement.Application.DTOs.PurchaseOrders;
using StockManagement.Application.Security;
using StockManagement.Infrastructure.Services;

namespace StockManagement.Api.Controllers;

[ApiController]
[Route("api/purchase-orders")]
public sealed class PurchaseOrdersController(PurchaseOrderService service) : ControllerBase
{
    [HttpGet("options")]
    [Authorize(Policy = Permissions.PurchaseOrderCreate)]
    public async Task<ActionResult<PurchaseOrderOptions>> Options(CancellationToken cancellationToken) =>
        Ok(await service.GetOptionsAsync(cancellationToken));

    [HttpGet("options/suppliers")]
    [Authorize(Policy = Permissions.PurchaseOrderView)]
    public async Task<ActionResult<IReadOnlyCollection<PurchaseOrderOption>>> SupplierOptions(CancellationToken cancellationToken) =>
        Ok(await service.GetSupplierFiltersAsync(cancellationToken));

    [HttpGet]
    [Authorize(Policy = Permissions.PurchaseOrderView)]
    public async Task<ActionResult<PurchaseOrderPage>> GetPage([FromQuery] string? page, [FromQuery] string? size,
        [FromQuery] string? status, [FromQuery] Guid? supplierId, [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate, CancellationToken cancellationToken)
    {
        if (!TryPage(page, out var currentPage) || !TrySize(size, out var pageSize))
            return BadRequest(new { error = "page və size müsbət tam ədəd olmalıdır." });
        if (!string.IsNullOrWhiteSpace(status) && !new[] { "draft", "pending_approval", "approved", "partially_received", "received", "cancelled" }.Contains(status, StringComparer.OrdinalIgnoreCase))
            return BadRequest(new { error = "PO status filtri yanlışdır." });
        if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
            return BadRequest(new { error = "fromDate toDate tarixindən sonra ola bilməz." });
        return Ok(await service.GetPageAsync(currentPage, pageSize, status, supplierId, fromDate, toDate, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.PurchaseOrderView)]
    public async Task<ActionResult<PurchaseOrderDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var order = await service.GetAsync(id, cancellationToken);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPost]
    [Authorize(Policy = Permissions.PurchaseOrderCreate)]
    public async Task<ActionResult<PurchaseOrderDto>> Create(SavePurchaseOrderRequest request, CancellationToken cancellationToken)
    {
        if (!TryUserId(out var userId)) return Unauthorized();
        try { return Ok(await service.CreateAsync(request, userId, cancellationToken)); }
        catch (ArgumentException exception) { return BadRequest(new { error = exception.Message }); }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.PurchaseOrderCreate)]
    public async Task<ActionResult<PurchaseOrderDto>> Update(Guid id, SavePurchaseOrderRequest request, CancellationToken cancellationToken)
    {
        if (!TryUserId(out var userId)) return Unauthorized();
        try { return Ok(await service.UpdateAsync(id, request, userId, cancellationToken)); }
        catch (KeyNotFoundException exception) { return NotFound(new { error = exception.Message }); }
        catch (ArgumentException exception) { return BadRequest(new { error = exception.Message }); }
        catch (InvalidOperationException exception) { return Conflict(new { error = exception.Message }); }
    }

    [HttpPost("{id:guid}/submit")]
    [Authorize(Policy = Permissions.PurchaseOrderCreate)]
    public Task<ActionResult<PurchaseOrderDto>> Submit(Guid id, CancellationToken cancellationToken) =>
        ChangeStatus(() => service.SubmitAsync(id, GetUserId(), cancellationToken));

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = Permissions.PurchaseOrderApprove)]
    public Task<ActionResult<PurchaseOrderDto>> Approve(Guid id, CancellationToken cancellationToken) =>
        ChangeStatus(() => service.ApproveAsync(id, GetUserId(), User.HasClaim("permission", Permissions.PurchaseOrderApproveOverride), cancellationToken));

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = Permissions.PurchaseOrderCancel)]
    public async Task<ActionResult<PurchaseOrderDto>> Cancel(Guid id, CancelPurchaseOrderRequest request, CancellationToken cancellationToken) =>
        await ChangeStatus(() => service.CancelAsync(id, GetUserId(), request.Reason, cancellationToken));

    private async Task<ActionResult<PurchaseOrderDto>> ChangeStatus(Func<Task<PurchaseOrderDto>> action)
    {
        if (!TryUserId(out _)) return Unauthorized();
        try { return Ok(await action()); }
        catch (KeyNotFoundException exception) { return NotFound(new { error = exception.Message }); }
        catch (ArgumentException exception) { return BadRequest(new { error = exception.Message }); }
        catch (InvalidOperationException exception) { return Conflict(new { error = exception.Message }); }
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirst("sub")!.Value);
    private bool TryUserId(out Guid id) => Guid.TryParse(User.FindFirst("sub")?.Value, out id);
    private static bool TryPage(string? value, out int parsed)
    {
        if (string.IsNullOrEmpty(value)) { parsed = 1; return true; }
        return int.TryParse(value, out parsed) && parsed > 0;
    }

    private static bool TrySize(string? value, out int parsed)
    {
        if (string.IsNullOrEmpty(value)) { parsed = PurchaseOrderService.DefaultPageSize; return true; }
        if (!int.TryParse(value, out parsed) || parsed < 1) return false;
        parsed = Math.Min(parsed, PurchaseOrderService.MaximumPageSize);
        return true;
    }
}
