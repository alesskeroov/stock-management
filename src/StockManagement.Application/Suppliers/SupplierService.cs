using StockManagement.Application.Abstractions;
using StockManagement.Application.DTOs.Suppliers;
using StockManagement.Application.Suppliers.Repositories;
using StockManagement.Domain.Entities;

namespace StockManagement.Application.Suppliers;

public sealed class SupplierService(ISupplierRepository repository, IAuditLogService auditLog)
{
    public const int DefaultPageSize = 10;
    public const int MaximumPageSize = 10;

    public async Task<SupplierPage> GetPageAsync(int page, int size, string? search = null, bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        size = size < 1 ? DefaultPageSize : Math.Min(size, MaximumPageSize);
        var (suppliers, totalCount) = await repository.GetPageAsync(page, size, search, isActive, cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)size);
        page = totalPages == 0 ? 1 : Math.Min(page, totalPages);
        return new(suppliers.Select(x => x.ToDto()).ToArray(), page, size, totalCount, totalPages,
            page < totalPages, page > 1);
    }

    public async Task<SupplierDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        (await repository.GetByIdAsync(id, cancellationToken))?.ToDto();

    public async Task<SupplierDto> CreateAsync(SaveSupplierRequest request, Guid userId, CancellationToken cancellationToken = default)
    {
        var supplier = new Supplier(request.Name, request.Phone, request.Email, request.Address,
            request.TaxId, request.ContactPerson);
        await repository.AddAsync(supplier, cancellationToken);
        await auditLog.WriteAsync(userId, "create", "supplier", supplier.Id, newValues: supplier.ToDto(), cancellationToken: cancellationToken);
        return supplier.ToDto();
    }

    public async Task<SupplierDto?> UpdateAsync(Guid id, SaveSupplierRequest request, Guid userId, CancellationToken cancellationToken = default)
    {
        var supplier = await repository.GetByIdAsync(id, cancellationToken);
        if (supplier is null) return null;
        var oldValues = supplier.ToDto();
        supplier.Update(request.Name, request.Phone, request.Email, request.Address, request.TaxId, request.ContactPerson);
        await repository.SaveChangesAsync(cancellationToken);
        await auditLog.WriteAsync(userId, "update", "supplier", id, oldValues, supplier.ToDto(), cancellationToken);
        return supplier.ToDto();
    }

    public async Task<SupplierDto?> SetActiveAsync(Guid id, bool active, Guid userId, CancellationToken cancellationToken = default)
    {
        var supplier = await repository.GetByIdAsync(id, cancellationToken);
        if (supplier is null) return null;
        var oldValues = supplier.ToDto();
        if (active) supplier.Activate(); else supplier.Deactivate();
        await repository.SaveChangesAsync(cancellationToken);
        await auditLog.WriteAsync(userId, active ? "activate" : "deactivate", "supplier", id,
            oldValues, supplier.ToDto(), cancellationToken);
        return supplier.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var supplier = await repository.GetByIdAsync(id, cancellationToken);
        if (supplier is null) return false;
        if (await repository.HasPurchaseOrdersAsync(id, cancellationToken))
            throw new InvalidOperationException("PO ilə əlaqəli təchizatçı silinə bilməz; onu deaktiv edin.");
        var oldValues = supplier.ToDto();
        supplier.MarkDeleted();
        await repository.SaveChangesAsync(cancellationToken);
        await auditLog.WriteAsync(userId, "soft-delete", "supplier", id, oldValues,
            new { supplier.IsDeleted, supplier.DeletedAt }, cancellationToken);
        return true;
    }
}
