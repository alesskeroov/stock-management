using StockManagement.Application.DTOs.Notifications;

namespace StockManagement.Application.Abstractions;

public interface IStockNotificationService
{
    Task RefreshForProductAsync(Guid productId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<StockNotificationDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default);
    Task<bool> MarkReadAsync(Guid id, CancellationToken cancellationToken = default);
    Task MarkAllReadAsync(CancellationToken cancellationToken = default);
}
