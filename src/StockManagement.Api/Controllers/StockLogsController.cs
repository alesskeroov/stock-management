using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockManagement.Application.DTOs.StockLogs;
using StockManagement.Application.Security;
using StockManagement.Domain.Enums;
using StockManagement.Infrastructure.Services;

namespace StockManagement.Api.Controllers;

[ApiController]
[Route("api/stock-logs")]
[Authorize(Roles = Roles.Admin)]
public sealed class StockLogsController(StockLogService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<StockLogPage>> GetPage([FromQuery] int page = 1, [FromQuery] int size = 10,
        [FromQuery] string? product = null, [FromQuery] string? type = null,
        [FromQuery] DateOnly? fromDate = null, [FromQuery] DateOnly? toDate = null,
        CancellationToken cancellationToken = default)
    {
        if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
            return BadRequest(new { error = "Başlanğıc tarixi son tarixdən sonra ola bilməz." });
        if (!string.IsNullOrWhiteSpace(type) &&
            (!Enum.TryParse<StockLogType>(type, true, out var parsedType) || !Enum.IsDefined(parsedType)))
            return BadRequest(new { error = "Növ yalnız IN, OUT və ya ADJUST ola bilər." });

        return Ok(await service.GetPageAsync(page, size, product, type, fromDate, toDate, cancellationToken));
    }

    [HttpGet("products/{productId:guid}")]
    public async Task<ActionResult<StockLogPage>> GetProductPage(Guid productId, [FromQuery] int page = 1,
        [FromQuery] int size = 10, CancellationToken cancellationToken = default) =>
        Ok(await service.GetProductPageAsync(productId, page, size, cancellationToken));
}
