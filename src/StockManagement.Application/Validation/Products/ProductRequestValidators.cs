using FluentValidation;

namespace StockManagement.Application.DTOs.Products;

public sealed class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator() => ProductRules.Apply(this);
}

public sealed class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductRequestValidator() => ProductRules.Apply(this);
}

public sealed class UpdateProductStockRequestValidator : AbstractValidator<UpdateProductStockRequest>
{
    public UpdateProductStockRequestValidator()
    {
        RuleFor(x => x.Quantity).GreaterThanOrEqualTo(0).WithMessage("Stok miqdarı mənfi ola bilməz.")
            .Must(quantity => decimal.Truncate(quantity) == quantity).WithMessage("Stok miqdarı tam ədəd olmalıdır.")
            .LessThanOrEqualTo(999999999999m).WithMessage("Stok miqdarı icazə verilən həddi aşır.");
    }
}

internal static class ProductRules
{
    public static void Apply<T>(AbstractValidator<T> validator) where T : IProductRequest
    {
        validator.RuleFor(x => x.Name).Cascade(CascadeMode.Stop)
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithMessage("Məhsulun adı tələb olunur.")
            .MaximumLength(200).WithMessage("Məhsulun adı 200 simvoldan çox ola bilməz.");
        validator.RuleFor(x => x.Sku).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("SKU tələb olunur.")
            .MaximumLength(64).WithMessage("SKU 64 simvoldan çox ola bilməz.")
            .Matches("^[A-Za-z0-9][A-Za-z0-9._-]*$")
            .WithMessage("SKU yalnız hərf, rəqəm, nöqtə, alt xətt və defisdən ibarət olmalıdır.");
        validator.RuleFor(x => x.Barcode).MaximumLength(64)
            .WithMessage("Barkod 64 simvoldan çox ola bilməz.");
        validator.RuleFor(x => x.Description).MaximumLength(1000)
            .WithMessage("Təsvir 1000 simvoldan çox ola bilməz.");
        validator.RuleFor(x => x.CategoryId).NotEmpty().WithMessage("Kateqoriya seçilməlidir.");
        validator.RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0)
            .WithMessage("Satış qiyməti mənfi ola bilməz.");
        validator.RuleFor(x => x.UnitOfMeasure).Cascade(CascadeMode.Stop)
            .Must(value => !string.IsNullOrWhiteSpace(value)).WithMessage("Ölçü vahidi tələb olunur.")
            .MaximumLength(32).WithMessage("Ölçü vahidi 32 simvoldan çox ola bilməz.");
    }
}

public interface IProductRequest
{
    string Name { get; }
    string Sku { get; }
    string Barcode { get; }
    string Description { get; }
    Guid CategoryId { get; }
    decimal UnitPrice { get; }
    string UnitOfMeasure { get; }
}
