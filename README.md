# Stockroom — Anbar və satınalma idarəetməsi

Stockroom məhsul və kateqoriya kataloqu, anbar qalığı, satınalma sifarişləri və mal qəbulunu idarə edən veb və mobil tətbiqdir. Backend ASP.NET Core Web API və SQL Server, veb interfeys React, mobil tətbiq isə Expo/React Native üzərində qurulub.

## Əsas imkanlar

- Məhsul və kateqoriyaların CRUD əməliyyatları, axtarış, pagination, barkod və QR skanı.
- Anbar, stok partiyaları, FIFO ilə stok çıxışı, stok düzəlişləri və inventar əməliyyatları.
- Təchizatçıların idarə edilməsi, satınalma sifarişlərinin yaradılması və təsdiq axını.
- Mal qəbulu (GRN), qismən qəbul, təkrar sorğulara qarşı idempotency və qəbulun səbəblə geri çevrilməsi.
- Stok hərəkətləri və audit loqları, aşağı stok/bitmiş stok bildirişləri.
- Satınalma hesabatları: açıq və gecikmiş sifarişlər, sifariş-qəbul müqayisəsi, təchizatçı üzrə qəbul məbləğləri.
- JWT əsaslı giriş, refresh token rotasiyası, rol və icazə əsaslı giriş nəzarəti.
- Mobil tətbiqdə giriş, məhsul/kateqoriya siyahısı, məhsul CRUD, axtarış və pagination.

## Texnologiyalar

- **Backend:** .NET 9, ASP.NET Core Web API, EF Core, ASP.NET Core Identity, JWT
- **Verilənlər bazası:** Microsoft SQL Server
- **Veb:** React 19, TypeScript, Vite
- **Mobil:** Expo, React Native, TypeScript, `expo-secure-store`

## Layihə quruluşu

```text
src/
├── StockManagement.Api/             # API controller-ləri və tətbiqin başlanğıcı
├── StockManagement.Application/     # Biznes qaydaları, DTO-lar, interfeyslər
├── StockManagement.Domain/          # Entity, enum və domain qaydaları
└── StockManagement.Infrastructure/  # EF Core, Identity, repository və xidmətlər
client/                              # React + TypeScript veb tətbiqi
mobile/                              # Expo + React Native mobil tətbiqi
tests/                               # .NET test layihələri
docs/                                # Audit, bildiriş və procurement qeydləri
```

## Tələblər

- .NET 9 SDK
- SQL Server
- Node.js və pnpm
- Mobil tətbiqi işlətmək üçün Expo Go və ya uyğun development build

## Lokal başlatma

### 1. Verilənlər bazası və API

`ConnectionStrings:StockManagementDb` dəyərini öz SQL Server-inizə uyğunlaşdırın. Push etməzdən əvvəl lokal connection string-i repoya yazmayın; mühit dəyişəni ilə vermək olar:

```powershell
$env:ConnectionStrings__StockManagementDb = "Server=localhost;Database=Stockroom;Trusted_Connection=True;TrustServerCertificate=True;"
```

Miqrasiyaları tətbiq edin:

```powershell
dotnet ef database update --project src/StockManagement.Infrastructure --startup-project src/StockManagement.Api
```

İnkişaf rejimində JWT açarı və ilkin Admin-i User Secrets-də saxlayın:

```powershell
$project = "src/StockManagement.Api/StockManagement.Api.csproj"
dotnet user-secrets init --project $project
$jwtKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(64))
dotnet user-secrets set "Jwt:SigningKey" $jwtKey --project $project
dotnet user-secrets set "Auth:BootstrapAdmin:Email" "admin@company.az" --project $project
dotnet user-secrets set "Auth:BootstrapAdmin:UserName" "admin" --project $project
$adminPassword = Read-Host "Admin üçün güclü şifrə"
dotnet user-secrets set "Auth:BootstrapAdmin:Password" $adminPassword --project $project
```

API-ni başladın:

```powershell
dotnet run --project src/StockManagement.Api
```

API `http://localhost:5136` ünvanında, Swagger isə `http://localhost:5136/swagger` ünvanında açılır (Development mühitində).

### 2. Veb tətbiqi

API işləyərkən ikinci terminalda:

```powershell
cd client
pnpm install
pnpm dev
```

Vite `http://localhost:5173` ünvanında açılır və `/api` sorğularını standart olaraq `http://localhost:5136` ünvanına yönləndirir. Başqa API ünvanı üçün `client/.env` faylında `VITE_API_URL` təyin edin. İstehsal build-i üçün `pnpm build` işlədin.

### 3. Mobil tətbiq

`mobile` qovluğunda `.env` faylı yaradın və `EXPO_PUBLIC_API_URL` dəyərini cihazınıza uyğun API ünvanı ilə doldurun (standart dəyər Android emulator üçündür). Sonra:

```powershell
cd mobile
pnpm install
pnpm start
```

Android emulator host kompüterə `http://10.0.2.2:5136`, iOS simulator `http://localhost:5136` ilə müraciət edir. Fiziki cihaz üçün kompüterin LAN IP ünvanından istifadə edin və cihazla kompüterin eyni şəbəkədə olduğuna əmin olun. Tokenlər `expo-secure-store` ilə saxlanır.

## Giriş və icazələr

API endpoint-ləri standart olaraq autentifikasiya tələb edir. Rollar və icazələr serverdə yoxlanılır; interfeysdəki görünürlük yoxlamaları təhlükəsizlik sərhədi deyil. İlkin Admin yalnız `Auth:BootstrapAdmin:*` konfiqurasiyası veriləndə yaradılır. Repoda standart istifadəçi və şifrə nəzərdə tutulmayıb.

Development-də signing key göstərilmədikdə müvəqqəti açar yaradılır və API yenidən başladıqda əvvəlki access token-lər işləməyəcək. Production-da `Jwt:SigningKey` ən azı 32 baytlıq güclü secret kimi secret manager və ya environment vasitəsilə verilməlidir. JWT açarını, Admin şifrəsini, API key-ləri və connection string-dəki parolları Git-ə commit etməyin.

Əsas rollar: `Admin`, `Manager`, `WarehouseKeeper`, `ProcurementStaff`. Giriş endpoint-ləri:

- `POST /api/auth/login`
- `POST /api/auth/refresh-token`
- `POST /api/auth/logout`
- `POST /api/auth/register` (Admin)

## API bölmələri

Development Swagger UI bütün controller endpoint-lərini göstərir. Əsas route-lar:

| Sahə | Route |
|---|---|
| Məhsullar və kateqoriyalar | `/api/products`, `/api/categories` |
| Anbar və stok | `/api/inventory`, `/api/stock-logs` |
| Təchizatçılar | `/api/suppliers` |
| Satınalma sifarişləri | `/api/purchase-orders` |
| Mal qəbulları | `/api/goods-receipts` |
| Stok sənədləri | `/api/documents` |
| Bildirişlər | `/api/notifications` |
| Satınalma hesabatları | `/api/procurement-reports` |

Bütün qorunan API sorğularına `Authorization: Bearer <access-token>` başlığı əlavə edin.

## Miqrasiyalar və texniki qeydlər

Yeni migration yaratmaq:

```powershell
dotnet ef migrations add MigrationName --project src/StockManagement.Infrastructure --startup-project src/StockManagement.Api
dotnet ef database update --project src/StockManagement.Infrastructure --startup-project src/StockManagement.Api
```

Mövcud verilənlər bazasında satınalma və stok migration-larını tətbiq etdikdən sonra uyğun ehtiyat nüsxə və sınaq mühitində yoxlama aparın. Stok ledger-i ilə köhnə cache arasında fərqləri yalnız verilənlər bazasına qoşulmuş mühitdə yoxlamaq olar:

```powershell
dotnet run --project src/StockManagement.Api -- --maintenance reconcile
```

Demo satınalma məlumatı yaratmaq üçün (1–50 arası, standart 20):

```powershell
dotnet run --project src/StockManagement.Api -- --maintenance procurement-demo --count 20
```

Əlavə dizayn və əməliyyat qeydləri `docs/`, `BARCODE_INTEGRATION.md`, `fifo-explanation.md` və `ADR.md` fayllarındadır.

