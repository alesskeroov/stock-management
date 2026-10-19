namespace StockManagement.Application.Abstractions;

public interface IQrCodeGenerator
{
    string ToSvg(string value);
}
