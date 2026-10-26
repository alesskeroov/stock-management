# Stok hərəkətləri auditi

Stok girişləri, çıxışları, düzəlişləri və geri qaytarmaları `stock_logs` cədvəlində ayrıca, dəyişdirilməz tarixçə kimi saxlanır. Bu cədvəl bildirişlərdən və mövcud `stock_movements` ledger-indən ayrıdır.

## `StockLog` qeydi

- `Id`, `ProductId` (məhsul silinsə `NULL` ola bilər), `ProductName` (məhsul adı snapshot-u)
- `Type`: `IN`, `OUT` və ya `ADJUST`
- `QuantityChange`, `QuantityBefore`, `QuantityAfter`
- İstəyə bağlı `Reason`, `CreatedBy`, `CreatedAt`

Stok yazan əməliyyatlar eyni `StockLogService.Record` metodunu çağırır; loq və stok eyni save/transaction daxilində yazılır. Sıfır dəyişiklik loq yaratmır. GRN geri qaytarması `OUT` kimi qeyd edilir. API məhsulu yumşaq silir; loqdakı məhsul adı snapshot-u əlaqə sonradan silinsə də qalır. `StockLog` üçün yeniləmə/silmə endpoint-i yoxdur, DbContext də dəyişiklik və silməni rədd edir.

## API və giriş

- `GET /api/stock-logs?page=1&size=10&product=...&type=IN&fromDate=YYYY-MM-DD&toDate=YYYY-MM-DD` — tarix azalan sıra ilə; `product` adın bir hissəsi və ya məhsul ID-si ola bilər.
- `GET /api/stock-logs/products/{productId}?page=1&size=10` — konkret məhsulun tarixçəsi.

Hər iki route yalnız `Admin` roluna açıqdır; frontend menyusunda “Loqlar” da yalnız Admin-ə görünür. Səhifə ölçüsü maksimum 10-dur.
