using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockManagement.Application.DTOs.Suppliers;
using StockManagement.Application.Security;
using StockManagement.Application.Suppliers;

namespace StockManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = Permissions.SupplierManage)]
public sealed class SuppliersController(SupplierService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SupplierPage>> GetAll([FromQuery] string? page, [FromQuery] string? size,
        [FromQuery] string? q, [FromQuery] bool? isActive,
        CancellationToken cancellationToken)
    {
        var parsedPage = int.TryParse(page, out var pageValue) && pageValue > 0 ? pageValue : 1;
        var parsedSize = int.TryParse(size, out var sizeValue) && sizeValue > 0
            ? sizeValue : SupplierService.DefaultPageSize;
        return Ok(await service.GetPageAsync(parsedPage, parsedSize, q, isActive, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SupplierDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var supplier = await service.GetByIdAsync(id, cancellationToken);
        return supplier is null ? NotFound() : Ok(supplier);
    }

    [HttpPost]
    public async Task<ActionResult<SupplierDto>> Create(SaveSupplierRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try { return Ok(await service.CreateAsync(request, userId, cancellationToken)); }
        catch (ArgumentException exception) { return BadRequest(new { error = exception.Message }); }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SupplierDto>> Update(Guid id, SaveSupplierRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try
        {
            var supplier = await service.UpdateAsync(id, request, userId, cancellationToken);
            return supplier is null ? NotFound() : Ok(supplier);
        }
        catch (ArgumentException exception) { return BadRequest(new { error = exception.Message }); }
    }

    [HttpPost("{id:guid}/deactivate")]
    public Task<ActionResult<SupplierDto>> Deactivate(Guid id, CancellationToken cancellationToken) => SetActive(id, false, cancellationToken);

    [HttpPost("{id:guid}/activate")]
    public Task<ActionResult<SupplierDto>> Activate(Guid id, CancellationToken cancellationToken) => SetActive(id, true, cancellationToken);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try { return await service.DeleteAsync(id, userId, cancellationToken) ? NoContent() : NotFound(); }
        catch (InvalidOperationException exception) { return Conflict(new { error = exception.Message }); }
    }

    private async Task<ActionResult<SupplierDto>> SetActive(Guid id, bool active, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try
        {
            var supplier = await service.SetActiveAsync(id, active, userId, cancellationToken);
            return supplier is null ? NotFound() : Ok(supplier);
        }
        catch (InvalidOperationException exception) { return Conflict(new { error = exception.Message }); }
    }

    private bool TryGetUserId(out Guid userId) => Guid.TryParse(User.FindFirst("sub")?.Value, out userId);
}
