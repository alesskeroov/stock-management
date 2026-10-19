using FluentValidation.Results;

namespace StockManagement.Api;

internal static class ValidationResultExtensions
{
    public static IDictionary<string, string[]> ToErrorsDictionary(this ValidationResult result) =>
        result.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray());
}
