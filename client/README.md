# Stockroom React UI

React + TypeScript + Vite frontend for the Stock Management API.

## Run locally

1. Start `StockManagement.Api` from Visual Studio or run `dotnet run --project src/StockManagement.Api` from the repository root.
2. In another terminal, run `pnpm install` and `pnpm dev` from this `client` folder.
3. Vite opens `http://localhost:5173`; API calls to `/api` are proxied to `http://localhost:5136`.

The UI currently provides category and product CRUD, search, form validation, API error feedback, and category/product summaries. Set `VITE_API_URL` in a local `.env` file only if the API is hosted at a different base URL.

## Əlavə imkanlar və sənədlər

Vebdə satınalma sifarişləri, mal qəbulları, təchizatçılar, stok sənədləri, bildirişlər və hesabatlar da var.
Məhsullar → “Excel ilə idxal” pəncərəsi şablon endirir, fayl/anbar seçir və sətir xətalarını göstərir.
`products.manage` və `stock.in` icazələri tələb olunur.
İdxal qalığı artırır; FIFO partiyası yaratmır.

- Build: `pnpm build`; lint: `pnpm lint`; klaviatura/fayl validation testləri: `pnpm test`.
- Node testləri `.ts` helper-ləri birbaşa import edir; Node v24.22.0 ilə yoxlanıb.
- [Əsas quraşdırma təlimatı](../README.md).
- [Excel idxalı](../docs/product-import.md).
- [Cari test və məlum xəta vəziyyəti](../PLAN.md).

ChatWidget ayrıca `http://localhost:8000/api/chat` çağırır.
Chatbot serverinin mənbə və başlatma qaydası bu repoda yoxdur: TODO: dəqiqləşdir.
