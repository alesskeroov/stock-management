# Stockroom React UI

React + TypeScript + Vite frontend for the Stock Management API.

## Run locally

1. Start `StockManagement.Api` from Visual Studio or run `dotnet run --project src/StockManagement.Api` from the repository root.
2. In another terminal, run `pnpm install` and `pnpm dev` from this `client` folder.
3. Vite opens `http://localhost:5173`; API calls to `/api` are proxied to `http://localhost:5135`.

The UI currently provides category and product CRUD, search, form validation, API error feedback, and category/product summaries. Set `VITE_API_URL` in a local `.env` file only if the API is hosted at a different base URL.
