# Procurement foundation notes (Phase 1)

This phase adds the stock ledger and procurement infrastructure only. It does not add purchase-order (PO) or goods-received-note (GRN) records or workflows.

## Current stock writers (legacy cache)

`Stock.Quantity` remains the current stock cache. Existing behavior is intentionally unchanged. These are the locations that currently write it directly:

1. `src/StockManagement.Infrastructure/Repositories/EfInventoryRepository.cs` — `ReceiveAsync` increases/creates the warehouse stock row when stock is received. It also creates a `ProductBatch` and legacy `StockTransaction`.
2. `src/StockManagement.Infrastructure/Repositories/EfInventoryRepository.cs` — `IssueFifoAsync` decreases `Stock.Quantity` and consumes batches; it also creates a legacy sale `StockTransaction`.
3. `src/StockManagement.Infrastructure/Repositories/EfProductRepository.cs` — `SetStockQuantityAsync` applies an absolute stock correction and creates legacy adjustment `StockTransaction` rows.
4. `src/StockManagement.Api/DemoDataSeeder.cs` — creates initial `Stock` cache rows for demo data without corresponding movement rows.

Reads of cached stock include `ChatbotDataController` and inventory/product services. `StockTransactions` is the previous event/history table; it is preserved. The new `stock_movements` table is not yet written by those legacy flows. `StockService` therefore reports ledger totals; run the one-time opening-balance command before relying on it for existing inventory. Future phases must move or dual-write the receipt, sale, and correction paths, then reconcile before removing the cache.

The separate `stock_logs` table records audit snapshots for each stock change; it does not populate or replace `stock_movements`.

## Opening balance

After applying the migration and confirming existing `Stocks` rows, run from the repository root with a valid user GUID:

```powershell
dotnet run --project src/StockManagement.Api -- --maintenance opening-balance --created-by 00000000-0000-0000-0000-000000000000
```

This snapshots each current `(product, warehouse)` cache value as one `OPENING_BALANCE` movement, including a zero value so that a zero row is still marked as initialized. The command runs in a serializable transaction on SQL Server and is safe to run again: existing pairs are skipped, and a filtered unique index is a final duplicate guard. It does not import the detailed historical `StockTransactions`/batch history. Use the ID of an existing `AspNetUsers` row for `--created-by`.

## Reconciliation (read-only)

```powershell
dotnet run --project src/StockManagement.Api -- --maintenance reconcile
```

The command compares `Stocks.Quantity` and the sum of `stock_movements.quantity` for every product/warehouse pair, prints differences, and exits with code 1 if any exist. It never changes the cache or ledger.

Replace the example `--created-by` value with the ID of a real `AspNetUsers` row. The commands use `src/StockManagement.Api/appsettings.json` for the connection string and honor `ConnectionStrings__StockManagementDb` from environment variables. They skip demo-data and user seeding. Do not run the opening-balance command before the new migration is applied or before the legacy cache has been reviewed.

## Supplier lifecycle note

Supplier delete is a soft delete, preserving identifiers and audit history. A supplier referenced by any PO cannot be deleted, but can be deactivated. The database foreign key also restricts physical deletion.

## Phase 2: Purchase orders

Phase 2 adds `purchase_orders` and `purchase_order_items`; it does not add a GRN flow and does not write to `Stocks`, `StockTransactions`, or `stock_movements`. PO create/edit only saves planned quantities and prices. The server ignores submitted line and order totals and recalculates them. Status rules are centralized in `PurchaseOrderStateMachine`.

Apply the new migration after Phase 1:

```powershell
dotnet ef database update --project src/StockManagement.Infrastructure --startup-project src/StockManagement.Api --configuration Release
```

The PO UI is under **Satınalma sifarişləri**. API routes are `GET /api/purchase-orders`, `GET /api/purchase-orders/{id}`, `GET /api/purchase-orders/options`, `POST /api/purchase-orders`, `PUT /api/purchase-orders/{id}`, and `POST /api/purchase-orders/{id}/{submit|approve|cancel}`. Listing supports `status`, `supplierId`, `fromDate`, `toDate`, `page`, and `size`. `po.approve.override` lets a creator approve their own PO; manager/admin defaults include this permission.

## Phase 3: Goods receipts

Phase 3 adds immutable `goods_receipts`/`goods_receipt_items` and `Products.average_cost`. Configure optional over-receipt tolerance with `Procurement:OverReceiptTolerancePercent` (percentage; default `0`). GRN creation requires `grn.create`; reads use `po.view`. Use `POST /api/goods-receipts`, `GET /api/goods-receipts?page=1&size=10`, or `GET /api/goods-receipts/{id}`. Send an optional `Idempotency-Key` header (or `idempotencyKey` in the body). The UI creates a receipt from an approved/partially received PO and shows linked GRNs in PO details.

For SQL Server, each receipt starts a transaction, locks the PO and its lines with `UPDLOCK, HOLDLOCK`, then reloads/validates remaining quantities. Product/cache rows are also locked in product-ID order before weighted-cost updates. A unique filtered idempotency-key index handles cross-process retries; an in-process key lock avoids duplicate work within one API process. GRN, PO item/status, cache, average cost, stock movement, numbering and audit writes commit together. Rejected quantities create no stock movement. If `Stocks.Quantity` disagrees with the ledger sum, receipt posting is rejected; reconcile or initialize opening balances first.

Apply Phase 3 after Phase 2:

```powershell
dotnet ef database update --project src/StockManagement.Infrastructure --startup-project src/StockManagement.Api --configuration Release
```

## Phase 4: reversal, reports, and deployment

GRNs remain immutable after posting. `POST /api/goods-receipts/{id}/reverse` requires `grn.reverse` and a non-empty JSON body such as `{"reason":"Səhv qəbul"}`. The operation locks the PO and its lines, checks ledger/cache stock, writes a negative `REVERSAL` movement for each original `PURCHASE_IN`, decreases the stock cache and PO received quantities, reopens the PO to `approved` or `partially_received`, and writes the reverse audit record in the same transaction. The reversal movement references the original movement ID (`reference_type=GRN_REVERSAL`). A GRN can be reversed once. If any received quantity has since been consumed, reversal is rejected. Rejected-only quantities have no stock movement to reverse.

The reversal intentionally leaves `Products.average_cost` unchanged. The receipt still records its original unit cost, and the reversal movement records the reversal amount/cost; changing a moving weighted average backwards would incorrectly rewrite the costing effect of later receipts and sales. This is the chosen safe accounting assumption for this phase.

Paged reports (all require `po.view`):

- `GET /api/procurement-reports/open-orders?page=1&size=10` — approved and partially received POs.
- `GET /api/procurement-reports/overdue-orders?page=1&size=10` — expected date passed, excluding received and cancelled orders, with days overdue.
- `GET /api/procurement-reports/receipt-comparison?page=1&size=10&purchaseOrderId={guid}` — ordered, net accepted, and not-reversed rejected quantities by PO product line.
- `GET /api/procurement-reports/supplier-receipt-amounts?page=1&size=10&fromDate=2026-01-01&toDate=2026-12-31` — accepted quantity multiplied by unit cost, grouped by supplier and currency; reversed GRNs are excluded.

Each report filters, counts, orders, and pages in SQL; it does not retrieve every PO/GRN to filter in application memory. Receipt and PO details are eager loaded by their existing read services. The repository has no PDF or dedicated print workflow, so no document generator or large PDF dependency was added.

Apply Phase 4 migration after Phase 3:

```powershell
dotnet ef database update --project src/StockManagement.Infrastructure --startup-project src/StockManagement.Api --configuration Release
```

### Deploy-dan əvvəl yoxlama siyahısı

- [ ] Database backup and a rollback window are ready; review the generated Phase 4 migration SQL before production.
- [ ] Apply migrations through `20261020132239_AddGoodsReceiptReversal`.
- [ ] Confirm role claims include `grn.reverse` only for manager/admin, and `po.view` for report readers.
- [ ] Run `dotnet run --project src/StockManagement.Api -- --maintenance reconcile`; resolve cache/ledger differences before using reverse or GRN posting.
- [ ] Verify API and frontend production builds and run the full test suite in an environment with restored NuGet packages.
- [ ] Smoke test an approved PO receipt, reversal, a duplicate reversal, and the four report routes using a test account/database.
- [ ] Reversal preserves the historical average cost by design; verify finance/accounting accepts this costing assumption.

The reports and reverse endpoint have not been applied to the configured database in this workspace. The reconciliation command was attempted but could not connect to SQL Server instance `Revan` (network/Named Pipes access denied), so no cache-to-ledger result is available yet. Run the checklist command from an environment with database access before deployment.

## Procurement UI

The React client exposes permission-gated **Satınalma sifarişləri**, **Mal qəbulları (GRN)**, **Təchizatçılar**, and **Satınalma hesabatları** screens. Supplier list supports server-side search/status filters and pagination. PO/GRN details include print-only documents styled with `@media print`; the browser print dialog can save to PDF without a PDF package. GRN receive keeps an idempotency key and disables repeat submission while posting; the reverse dialog requires a reason. Client role defaults only control visibility; API policies remain authoritative.

## Procurement UI demo data

After all procurement migrations are applied and the API connection string points to a reachable database, create 20 sample supplier/PO sets and their GRNs with:

```powershell
dotnet run -c Release --project src/StockManagement.Api -- --maintenance procurement-demo --count 20
```

`--count` accepts 1-50 and defaults to 20. The command ensures basic demo products/warehouse and the configured bootstrap account exist, writes an opening balance before posting GRNs, then creates varied draft, pending, approved, partially received, received, and cancelled orders. It uses the normal purchase-order and goods-receipt services, so receipts update the stock ledger/cache and appear in reports. Demo supplier tax IDs and PO notes are stable markers; re-running the command does not duplicate already-created demo PO sets. It does not apply migrations. The database connection used by this command is the same `StockManagementDb` application connection described above.
