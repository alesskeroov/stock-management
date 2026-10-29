# Stockroom — Anbar və satınalma idarəetməsi

Stockroom məhsul və kateqoriya kataloqu, anbar qalığı, satınalma sifarişləri və mal qəbulunu idarə edən veb və mobil tətbiqdir. Backend ASP.NET Core Web API və SQL Server, veb interfeys React, mobil tətbiq isə Expo/React Native üzərində qurulub.

Kod və əmr yoxlaması: **2026-10-29**. Təlimat junior developer üçün addım-addımdır.
Məhsul adı mövcud README və interfeysdən götürülüb; hüquqi/rəsmi ad: TODO: dəqiqləşdir.

## Sənədləşdirmə xəritəsi

- [AGENTS.md](AGENTS.md) — kodla işləmə qaydaları və yoxlama əmrləri.
- [PLAN.md](PLAN.md) — hazır işlər, məlum xətalar və növbəti addımlar.
- [Arxitektura](docs/ARCHITECTURE.md) — modullar, məlumat axını və diaqramlar.
- [Qərarlar](docs/DECISIONS.md) — seçimlər, səbəblər və açıq suallar.
- [RESEARCH.md](RESEARCH.md) — öyrənmə növbəsi, mövzu şablonu və jurnal.
- [Excel idxalı](docs/product-import.md), [audit](docs/audit-log.md), [bildirişlər](docs/notifications.md), [satınalma](docs/procurement-notes.md).

İlk dəfə başlayırsınızsa: əvvəl “Lokal başlatma”, sonra “Nümunə istifadə”, sonda “Yoxlama əmrləri” bölmələrini izləyin.

## Əsas imkanlar

- Məhsul və kateqoriyaların CRUD əməliyyatları, axtarış, pagination, barkod və QR skanı.
- Anbar, stok partiyaları, FIFO ilə stok çıxışı, stok düzəlişləri və inventar əməliyyatları.
- Təchizatçıların idarə edilməsi, satınalma sifarişlərinin yaradılması və təsdiq axını.
- Mal qəbulu (GRN), qismən qəbul, təkrar sorğulara qarşı idempotency və qəbulun səbəblə geri çevrilməsi.
- Stok hərəkətləri və audit loqları, aşağı stok/bitmiş stok bildirişləri.
- Satınalma hesabatları: açıq və gecikmiş sifarişlər, sifariş-qəbul müqayisəsi, təchizatçı üzrə qəbul məbləğləri.
- JWT əsaslı giriş, refresh token rotasiyası, rol və icazə əsaslı giriş nəzarəti.
- Mobil tətbiqdə giriş, məhsul/kateqoriya siyahısı, məhsul CRUD, axtarış və pagination.
- Vebdə Excel şablonu, anbar seçimi, toplu idxal və sətir üzrə xətaların göstərilməsi.

## Texnologiyalar

- **Backend:** .NET 9, ASP.NET Core Web API, EF Core, ASP.NET Core Identity, JWT
- **Verilənlər bazası:** Microsoft SQL Server
- **Veb:** React 19, TypeScript, Vite
- **Mobil:** Expo, React Native, TypeScript, `expo-secure-store`

Versiya mənbələri: backend `.csproj`, veb `client/package.json`, mobil `mobile/package.json`.
Excel idxalı ClosedXML 0.105.0, testlər xUnit və EF InMemory istifadə edir.

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

Node.js və pnpm üçün sabit komanda versiyası repoda təyin edilməyib: TODO: dəqiqləşdir.
Hər iki frontend qovluğunda `pnpm-lock.yaml` var; quraşdırmanı pnpm ilə edin.
`dotnet ef` əmri yoxdursa, layihənin EF Core versiyasına uyğun aləti quraşdırın:

```powershell
dotnet tool install --global dotnet-ef --version 9.0.21
```

Bu quraşdırma əmri sənədləşdirilib; bu tapşırıqda global alət quraşdırılmayıb.

## Lokal başlatma

### 1. Verilənlər bazası və API

Terminalı `StockManagement.sln` olan kök qovluqda açın. Əvvəl paketləri bərpa edin:

```powershell
dotnet restore StockManagement.sln
dotnet restore tests/StockManagement.Tests/StockManagement.Tests.csproj
```

SQL Server instance-i və həmin instance-ə qoşulma icazəsi əvvəlcədən mövcud olmalıdır.

`ConnectionStrings:StockManagementDb` dəyərini öz SQL Server-inizə uyğunlaşdırın. Push etməzdən əvvəl lokal connection string-i repoya yazmayın; mühit dəyişəni ilə vermək olar:

```powershell
$env:ConnectionStrings__StockManagementDb = "Server=localhost;Database=Stockroom;Trusted_Connection=True;TrustServerCertificate=True;"
```

Miqrasiyaları tətbiq edin:

Aşağıdakı əmr seçilən bazanın sxemini dəyişir. Lokal/test bazasını seçdiyinizi yoxlayın.
Bu sənədləşdirmə işi zamanı baza və migration dəyişdirilməyib.

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

`launchSettings.json` API-ni `0.0.0.0:5136` üzərində dinləməyə yönəldir.
`Program.cs` başlanğıcda `DemoDataSeeder` və `AuthSeeder` çağırır; seed məlumat yaza bilər.
Demo seed Production şərti ilə məhdudlaşdırılmayıb. İstehsal siyasəti: TODO: dəqiqləşdir.

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

İstisnalar var: auth-un anonim əməliyyatları, kök redirect və `ChatbotDataController` anonimdir.
Cari permission xəritəsi üçün `src/StockManagement.Application/Security/Roles.cs` əsas mənbədir.
Köhnə ADR qeydlərindəki rol təsvirləri ilə fərqlər [qərarlar jurnalında](docs/DECISIONS.md) göstərilib.

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
| Excel idxalı və şablon | `/api/products/import`, `/api/products/import/template` |
| Anbar və stok | `/api/inventory`, `/api/stock-logs` |
| Təchizatçılar | `/api/suppliers` |
| Satınalma sifarişləri | `/api/purchase-orders` |
| Mal qəbulları | `/api/goods-receipts` |
| Stok sənədləri | `/api/documents` |
| Bildirişlər | `/api/notifications` |
| Satınalma hesabatları | `/api/procurement-reports` |

Bütün qorunan API sorğularına `Authorization: Bearer <access-token>` başlığı əlavə edin.

## Nümunə istifadə

### Məhsul yaratmaq və qəbulu izləmək

1. API və veb tətbiqi başladın. Bootstrap ilə yaradılmış hesabla daxil olun.
2. Məhsullar bölməsində “Məhsul əlavə et” seçin.
3. Ad, unikal SKU, mövcud kateqoriya, qiymət və ölçü vahidini daxil edin.
4. Stok üçün uyğun anbarı və qəbul əməliyyatını seçin. Partiyalı qəbul FIFO üçündür.
5. Satınalma bölməsində təchizatçı və PO yaradın; icazənizə uyğun təsdiq/qəbul edin.

Düymə görünmürsə hesabın icazələrini yoxlayın. UI və API eyni səlahiyyətləri tələb etməlidir.

### Excel ilə toplu idxal

1. `products.manage` və `stock.in` icazələri olan hesabla Məhsullar bölməsini açın.
2. “Excel ilə idxal” → “Şablonu yüklə” seçin.
3. Nümunələri öz məhsullarınızla əvəz edin; kateqoriyanı bazadakı adı ilə yazın.
4. `.xlsx` faylını seçin (maksimum 5 MB) və stokun yazılacağı anbarı göstərin.
5. “Məhsulları idxal et” seçin. Xəta varsa sətir və sütunu düzəldib yenidən seçin.

Mövcud SKU-nun adı/qiyməti yenilənir, miqdar qalığa əlavə olunur.
Eyni faylı yenidən uğurla idxal etmək miqdarı təkrar artırır.
Excel idxalı FIFO partiyası yaratmır. Ətraflı: [Excel idxalı](docs/product-import.md).

### API-dən oxuma nümunəsi

Swagger-də `POST /api/auth/login` üçün `identifier` və `password` göndərin.
Qayıdan `accessToken` ilə Authorize edin. Sonra:

```http
GET /api/products?page=1&size=10
Authorization: Bearer <access-token>
```

Həqiqi token və şifrələri sənədlərə və Git-ə yazmayın.

## Yoxlama əmrləri

Repo kökündə:

```powershell
dotnet build StockManagement.sln
dotnet test tests/StockManagement.Tests/StockManagement.Tests.csproj
```

Test layihəsi solution-a daxil deyil; test üçün birbaşa `.csproj` göstərilməlidir.
API işləyərkən DLL kilidi yaranarsa, API-ni özünüz dayandırın və ya ayrıca çıxış qovluğu ilə yoxlayın:

```powershell
dotnet test tests/StockManagement.Tests/StockManagement.Tests.csproj -m:1 -p:BuildInParallel=false -p:OutputPath=bin/Verification/
```

`client` daxilində:

```powershell
pnpm build
pnpm lint
```

`mobile` daxilində:

```powershell
pnpm typecheck
```

Bu script-lər `npm run build`, `npm run lint`, `npm run typecheck` ilə də çağırılır.
Backend üçün ayrıca lint əmri, frontend E2E və mobil test skripti tapılmayıb: TODO: dəqiqləşdir.
Cari nəticələr [PLAN.md](PLAN.md)-dədir: 70 xUnit testindən 6-sı uğursuzdur, mobil typecheck TS2580 verir.
InMemory testləri real SQL tranzaksiyalarının davranışını təsdiqləmir.

## Tez-tez rast gəlinən xətalar

| Əlamət | Yoxlanacaq yer və addım |
|---|---|
| `ClosedXML`, `XLWorkbook` tanınmır | Infrastructure csproj-də paket istinadını yoxlayın, `dotnet restore StockManagement.sln --force`, sonra build edin |
| İdxalda 404/405 | İşləyən API köhnə ola bilər; dayandırıb yenidən build/start edin. Swagger-də `/api/products/import` görünməlidir |
| Swagger JSON 500 verir | Server exception-ını oxuyun. `IFormFile` üzərində birbaşa `[FromForm]` əvvəl generator xətası yaradıb; `SwaggerTests` bunu yoxlayır |
| 401 | Sessiyanın vaxtı bitmiş və ya Development signing key dəyişmiş ola bilər; yenidən daxil olun |
| 403 | Tokenin permission-larını və controller policy-lərini yoxlayın |
| Veb API-yə qoşulmur | `client/vite.config.ts` proxy və lokal `VITE_API_URL` dəyərini yoxlayın; cari API portu 5136-dır |
| Chatbot cavab vermir | Widget `http://localhost:8000/api/chat` çağırır; serverin mənbə/quraşdırması repoda yoxdur: TODO: dəqiqləşdir |

`.env.example` və bəzi köhnə sənədlərdə 5135 portu qala bilər; cari kod konfiqurasiyası 5136-dır.
Frontend build-də verilən `VITE_*` dəyərlərinə secret qoymayın; onlar client koduna daxil ola bilər.
Bu davranış üçün [Vite rəsmi sənədi](https://vite.dev/guide/env-and-mode) yoxlanıb: 2026-10-29.

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

## Açıq təşkilati məsələlər

- TODO: dəqiqləşdir — layihə sahibi, əlaqə və rəsmi ad.
- TODO: dəqiqləşdir — lisenziya və istifadə şərtləri; repoda LICENSE faylı tapılmayıb.
- TODO: dəqiqləşdir — deployment ünvanı, CI/CD, backup/restore qaydası.
- TODO: dəqiqləşdir — məcburi commit/PR qaydaları və dəstəklənən Node.js/pnpm versiyaları.

