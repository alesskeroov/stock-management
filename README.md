# Stock Management — N-layer starter

Visual Studio ilə `StockManagement.sln` faylını açın. Layihələr .NET 9 üçün hazırlanıb.

## Qatlar

- **StockManagement.Api (Presentation)** — HTTP controller-lər və tətbiqin başlanğıc nöqtəsi.
- **StockManagement.Application (BLL)** — biznes qaydaları, use-case-lər, DTO-lar və repository interfeysləri.
- **StockManagement.Infrastructure (DAL)** — EF Core repository implementasiyaları, `DbContext` və migration-lar.
- **StockManagement.Domain (Entities)** — məhsul modeli və stok qaydaları.

Project reference-lər: `Presentation (Api) → BLL (Application) + DAL (Infrastructure)`; `DAL → BLL + Entities`; `BLL → Entities`. Repository interfeysləri BLL-də, EF implementasiyaları DAL-dadır. Beləliklə, BLL verilənlər bazası implementasiyasına bağlı qalmır.

## Qovluq quruluşu

```text
src/
├── StockManagement.Api/                         # Presentation
│   └── Controllers/
├── StockManagement.Application/                 # Business Logic Layer
│   ├── Abstractions/Repositories/                # Generic repository contract
│   ├── Abstractions/IAuthService.cs               # Authentication contract
│   ├── Categories/                               # Category service
│   ├── DTOs/
│   │   ├── Categories/
│   │   ├── Auth/
│   │   └── Products/
│   ├── Security/                                 # RBAC roles and permission keys
│   └── Validation/                               # FluentValidation rules
│   └── Products/Repositories/                    # Product repository contract
├── StockManagement.Domain/                      # Entities
│   ├── Entities/
│   └── Enums/
└── StockManagement.Infrastructure/              # Data Access Layer
    ├── Authentication/                           # Identity auth, JWT and role seeding
    ├── Identity/                                  # User and hashed refresh-token entities
    ├── Persistence/                               # DbContext və migrations
    └── Repositories/                             # EF Core generic/specialized repositories
```

## Code First entities

`StockManagement.Domain/Entities` qovluğunda `Product`, `Category`, `Warehouse`, `Stock`, `Supplier` və `StockTransaction` modelləri, `Enums` qovluğunda əməliyyat növü var. FK və navigation property-lər EF Core Code First əlaqələri üçün qurulub. Məhsul miqdarı `Product`-da deyil, `Stock` entity-sində saxlanır. `ManagerId` və `AppUserId` üçün user/employee entity-si verilmədiyindən navigation property əlavə edilməyib.

Kateqoriya və məhsullar üçün asinxron CRUD endpoint-ləri müvafiq controller-lərdə, biznes qaydaları `CategoryService` və `ProductService`-də yerləşir. Generic CRUD müqaviləsi `IGenericRepository<TEntity>`, məhsula aid axtarış müqaviləsi `IProductRepository`-dir. EF Core implementasiyası `EfRepository<TEntity>` və `EfProductRepository` siniflərindədir. Kateqoriyanın mövcudluğu məhsul yaratma/güncəlləmədə yoxlanılır; əlaqəli məhsulu və ya alt kateqoriyası olan kateqoriya silinmir.

## Başlatmaq

Visual Studio-da solution-u açıb `StockManagement.Api` layihəsini startup project seçin və F5 basın. `http` launch profile brauzerdə birbaşa `http://localhost:5135/swagger` səhifəsini açır. Swagger UI Development mühitində aktivdir. Terminaldan:

```powershell
dotnet run --project src/StockManagement.Api
```

## React frontend

API-ni başladıqdan sonra ikinci terminalda `client` qovluğunda `pnpm install` və `pnpm dev` icra edin. Vite `http://localhost:5173` ünvanını brauzerdə açır və `/api` sorğularını API-nin `http://localhost:5135` ünvanına yönləndirir. Frontend hazırda backend-də mövcud olan məhsul və kateqoriya CRUD əməliyyatlarına qoşulub.

SQL Server connection string-i `src/StockManagement.Api/appsettings.json` faylındadır. `StockManagementDbContext` və `InitialCreate` migration-i Infrastructure qatındadır. `StockManagmentDb` yaradılıb və migration tətbiq edilib.

## JWT giriş və RBAC

Identity ilə `AspNetUsers`, `AspNetRoles`, user/role əlaqələri və `RefreshTokens` cədvəlləri yaradılır. Refresh token-in özü yox, SHA-256 hash-i bazada saxlanır; yeniləmə zamanı token rotasiya edilir. Identity PasswordHasher şifrələri PBKDF2 ilə hash-ləyir. Beş uğursuz girişdən sonra hesab 15 dəqiqəlik bloklanır, auth endpointləri isə IP üzrə dəqiqədə 10 sorğu ilə məhdudlaşır.

İlk Admin istifadəçisi yalnız konfiqurasiya edildikdə yaradılır; repoda standart şifrə saxlanmır. Lokal development üçün API user-secrets-də signing açarını və bootstrap Admin məlumatlarını yazın:

```powershell
$project = "src/StockManagement.Api/StockManagement.Api.csproj"
dotnet user-secrets init --project $project
$jwtKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(64))
dotnet user-secrets set "Jwt:SigningKey" $jwtKey --project $project
dotnet user-secrets set "Auth:BootstrapAdmin:Email" "admin@company.az" --project $project
dotnet user-secrets set "Auth:BootstrapAdmin:UserName" "admin" --project $project
$adminPassword = Read-Host "Bootstrap Admin üçün 12+ simvollu güclü şifrə"
dotnet user-secrets set "Auth:BootstrapAdmin:Password" $adminPassword --project $project
```

API başladıqda Admin, WarehouseKeeper və Manager rolları və icazə claim-ləri yaradılır. Bootstrap məlumatları varsa ilk Admin də yaradılır. Signing key development-də verilməzsə müvəqqəti açar yaranır və server restartında access token-lər etibarsız olur; production-da `Jwt:SigningKey` mütləq secret manager/environment vasitəsilə verilməlidir.

Auth endpointləri:

- `POST /api/auth/login` — `{ "identifier": "admin@company.az", "password": "..." }`; email və ya istifadəçi adı ilə giriş edir, access və refresh token qaytarır.
- `POST /api/auth/refresh-token` — `{ "refreshToken": "..." }`; refresh token-i rotasiya edir.
- `POST /api/auth/logout` — `{ "refreshToken": "..." }`; refresh token-i ləğv edir.
- `POST /api/auth/register` — yalnız Admin; işçi əlavə edir. Rol dəyərləri `Admin`, `WarehouseKeeper`, `Manager`-dir.

Məhsul və kateqoriya oxumaq üçün bu rolların hamısına `products.read` icazəsi verilir; yazma əməliyyatları Admin-in `products.manage` icazəsi ilə qorunur. WarehouseKeeper üçün `stock.in`, `stock.out`, `inventory.count`; Manager üçün `stock.read`, `reports.read`, `approvals.manage` icazələri rol claim-i kimi verilir. Hələ həmin anbar/hesabat API endpointləri olmadığından bu icazələr endpointlər əlavə ediləndə istifadə olunacaq. React tətbiqində login, logout, access token istifadəsi və refresh token ilə sessiya yenilənməsi qoşulub.

Auth modeli və user foreign key-ləri üçün `AddIdentityAndRefreshTokens` migration-i `StockManagmentDb` bazasına tətbiq olunub.

Model dəyişikliklərindən sonra Visual Studio Package Manager Console-da:

```powershell
Add-Migration MigrationName -Project StockManagement.Infrastructure -StartupProject StockManagement.Api
Update-Database -Project StockManagement.Infrastructure -StartupProject StockManagement.Api
```

CLI alternativi:

```powershell
dotnet ef migrations add MigrationName --project src/StockManagement.Infrastructure --startup-project src/StockManagement.Api
dotnet ef database update --project src/StockManagement.Infrastructure --startup-project src/StockManagement.Api
```

`Scaffold-DbContext` mövcud bazadan entity/context kodunu tərsinə yaradır; Code First yanaşmasında bazanı entity-lərdən yaratmaq üçün migration istifadə edilir.

## API nümunələri

- `GET /api/products`
- `GET /api/products/{id}`
- `POST /api/products` — `{ "name": "Keyboard", "sku": "KB-001", "barcode": "123456", "description": "USB keyboard", "categoryId": "<guid>", "unitPrice": 45.90, "unitOfMeasure": "Piece", "isActive": true }`
- `PUT /api/products/{id}` — eyni sahələrlə tam yeniləmə
- `DELETE /api/products/{id}`
- `GET /api/categories`, `GET /api/categories/{id}`
- `POST /api/categories` — `{ "name": "Electronics", "description": "Electronic goods" }`
- `PUT /api/categories/{id}`, `DELETE /api/categories/{id}`

