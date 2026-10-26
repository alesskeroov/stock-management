using StockManagement.Domain.Enums;
using System.Text.Json.Serialization;

namespace StockManagement.Application.DTOs.Notifications;

public sealed record StockNotificationDto(Guid Id, Guid ProductId, string ProductName,
    decimal RemainingQuantity, string Message,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] StockNotificationType Type,
    bool IsRead, DateTime CreatedAt);
