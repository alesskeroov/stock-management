using QRCoder;
using StockManagement.Application.Abstractions;

namespace StockManagement.Infrastructure.Services;

public sealed class QrCodeGenerator : IQrCodeGenerator
{
    public string ToSvg(string value)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(value, QRCodeGenerator.ECCLevel.Q);
        return new SvgQRCode(data).GetGraphic(5);
    }
}
