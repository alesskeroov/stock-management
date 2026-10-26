using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockManagement.Application.DTOs.PurchaseOrders;
using StockManagement.Application.Security;
using StockManagement.Infrastructure.Services;

namespace StockManagement.Api.Controllers;

[ApiController]
[Route("api/procurement-reports")]
[Authorize(Policy = Permissions.PurchaseOrderView)]
public sealed class ProcurementReportsController(ProcurementReportService reports) : ControllerBase
{
    [HttpGet("open-orders")]
    public async Task<ActionResult<ProcurementReportPage<OpenPurchaseOrderReport>>> OpenOrders(
        [FromQuery] int page = 1, [FromQuery] int size = 10, CancellationToken ct = default) =>
        Valid(page, size) ? Ok(await reports.GetOpenOrdersAsync(page, size, ct)) : BadRequest(new { error = "page/size müsbət olmalıdır." });

    [HttpGet("overdue-orders")]
    public async Task<ActionResult<ProcurementReportPage<OverduePurchaseOrderReport>>> OverdueOrders(
        [FromQuery] int page = 1, [FromQuery] int size = 10, CancellationToken ct = default) =>
        Valid(page, size) ? Ok(await reports.GetOverdueOrdersAsync(page, size, DateOnly.FromDateTime(DateTime.Today), ct)) : BadRequest(new { error = "page/size müsbət olmalıdır." });

    [HttpGet("receipt-comparison")]
    public async Task<ActionResult<ProcurementReportPage<PurchaseOrderReceiptComparison>>> ReceiptComparison(
        [FromQuery] int page = 1, [FromQuery] int size = 10, [FromQuery] Guid? purchaseOrderId = null,
        CancellationToken ct = default) =>
        Valid(page, size) ? Ok(await reports.GetReceiptComparisonAsync(page, size, purchaseOrderId, ct)) : BadRequest(new { error = "page/size müsbət olmalıdır." });

    [HttpGet("supplier-receipt-amounts")]
    public async Task<ActionResult<ProcurementReportPage<SupplierReceiptAmount>>> SupplierReceiptAmounts(
        [FromQuery] int page = 1, [FromQuery] int size = 10, [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null, CancellationToken ct = default)
    {
        if (!Valid(page, size) || (fromDate.HasValue && toDate.HasValue && fromDate > toDate))
            return BadRequest(new { error = "page/size və tarix aralığı yanlışdır." });
        return Ok(await reports.GetSupplierReceiptAmountsAsync(page, size, fromDate, toDate, ct));
    }

    private static bool Valid(int page, int size) => page > 0 && size > 0;
}
