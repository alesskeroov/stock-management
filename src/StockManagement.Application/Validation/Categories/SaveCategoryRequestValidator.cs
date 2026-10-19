using FluentValidation;

namespace StockManagement.Application.DTOs.Categories;

public sealed class SaveCategoryRequestValidator : AbstractValidator<SaveCategoryRequest>
{
    public SaveCategoryRequestValidator()
    {
        RuleFor(x => x.Name).Cascade(CascadeMode.Stop)
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithMessage("Kateqoriyanın adı tələb olunur.")
            .MaximumLength(150).WithMessage("Kateqoriyanın adı 150 simvoldan çox ola bilməz.");
        RuleFor(x => x.Description).MaximumLength(1000)
            .WithMessage("Təsvir 1000 simvoldan çox ola bilməz.");
    }
}
