namespace StockManagement.Application.DTOs.Suppliers;

public sealed record SupplierDto(Guid Id, string Name, string Phone, string Email, string Address,
    string TaxId, string ContactPerson, bool IsActive, DateTime CreatedAt, DateTime UpdatedAt);
public sealed record SaveSupplierRequest(string Name, string Phone, string Email, string Address,
    string TaxId, string ContactPerson = "");
public sealed record SupplierPage(IReadOnlyCollection<SupplierDto> Items, int CurrentPage, int PageSize,
    int TotalCount, int TotalPages, bool HasNext, bool HasPrevious);

public static class SupplierMapping
{
    public static SupplierDto ToDto(this StockManagement.Domain.Entities.Supplier supplier) =>
        new(supplier.Id, supplier.Name, supplier.Phone, supplier.Email, supplier.Address, supplier.TaxId,
            supplier.ContactPerson, supplier.IsActive, supplier.CreatedAt, supplier.UpdatedAt);
}
