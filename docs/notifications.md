# Low-stock notifications

The stock notification service checks total stock across warehouses after inventory receipts, FIFO issues, stock adjustments, goods receipts, and goods receipt reversals. A positive total of `5` or less is low stock; zero is out of stock. `ReorderLevel` does not affect notification eligibility.

- A quantity of `0` creates an `OUT_OF_STOCK` notification.
- A positive quantity at or below the reorder level creates a `LOW_STOCK` notification.
- One unread notification is kept per product. Its type and message are updated as stock changes.
- Stock above the reorder level marks the product's unread notifications as read.

The authenticated API is available at `GET /api/notifications`, `GET /api/notifications/unread-count`, `PUT /api/notifications/{id}/read`, and `PUT /api/notifications/read-all`. The web header shows the unread count and provides the notification list.

Audit history is stored separately in `stock_logs`; stock log records do not replace or change notification behavior.

Apply the `AddStockNotifications` EF Core migration before using the notification API.
