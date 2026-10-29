# Excel ilə toplu məhsul idxalı

## Arxitektura və dəyişikliklər

Layihə .NET 9 / ASP.NET Core Web API / EF Core 9 / SQL Server istifadə edir.
İdxal mövcud `Product`, `Stock`, `StockTransaction`, `StockLog` və `StockNotification` modellərindən istifadə edir.
İlkin backend addımında entity, DbContext, migration, Program.cs və frontend dəyişdirilməyib.
Sonra veb idxal pəncərəsi ayrıca əlavə edilib: `client/src/ProductImportDialog.tsx`, uyğun CSS və `App.tsx` inteqrasiyası.
Məhsullar bölməsində “Excel ilə idxal” düyməsi ilə şablon, fayl və anbar seçimi, xəta cədvəli və nəticə sayları göstərilir.
Faylı seçim sahəsinə sürükləyib buraxmaq da mümkündür. Hər dəfə bir fayl qəbul edilir; drop və fayl seçimi eyni format/ölçü yoxlamasını işlədir.
İlkin backend qeydiyyatı üçün iki mövcud fayla bir sətir əlavə edilib:

`src/StockManagement.Infrastructure/StockManagement.Infrastructure.csproj`, paketlər bölməsi:

```xml
<PackageReference Include="ClosedXML" Version="0.105.0" />
```

`src/StockManagement.Infrastructure/DependencyInjection.cs`, `AddInfrastructure` metodu:

```csharp
services.AddScoped<StockManagement.Application.Products.IProductImportService, ProductImportService>();
```

`Program.cs` artıq `AddInfrastructure` çağırdığı üçün oraya əlavə qeydiyyat lazım deyil.
ClosedXML yeganə yeni birbaşa paketdir; onun NuGet asılılıqları avtomatik bərpa edilir.
Əvvəlki tapşırıqlara aid frontend və repository dəyişiklikləri bu idxalın dəyişikliklərinə daxil deyil.

Tam yeni mənbə faylları:

- `src/StockManagement.Application/Products/IProductImportService.cs`
- `src/StockManagement.Application/DTOs/Products/ProductImportContracts.cs`
- `src/StockManagement.Infrastructure/Services/ProductImportService.cs`
- `src/StockManagement.Api/Controllers/ProductImportController.cs`
- `tests/StockManagement.Tests/ProductImportTests.cs`
- `tests/StockManagement.Tests/SwaggerTests.cs` — fayl sahəsi daxil olmaqla API təsvirinin yaradılmasını yoxlayır.

## Endpoint-lər

`GET /api/products/import/template`

- JWT və `products.manage` icazəsi tələb edir.
- `mal_sablon.xlsx` qaytarır. İlk vərəqin başlıqları: `sku`, `name`, `category`, `price`, `quantity`, `unit`.
- İki nümunə sətir: Süd və Şəkər. Nümunə kateqoriyaları öz bazanızdakı kateqoriya adları ilə əvəz edin; nümunə sətirlər idxalda adi məhsul sətri kimi işlənir.
- SKU sütunu mətn formatındadır: məsələn, `00123` daxil edərkən başlanğıc sıfırlar qorunur.

`POST /api/products/import?warehouseId=<anbar-guid>`

- JWT, `products.manage` və `stock.in` icazələri tələb edir.
- `multipart/form-data` daxilində fayl sahəsinin adı `file` olmalıdır.
- `warehouseId` verilmədikdə tək mövcud anbar avtomatik seçilir. Sıfır və ya bir neçə anbar olduqda aydın xəta qaytarılır.
- Əməliyyatı edən istifadəçi JWT-nin `sub` claim-indən götürülür.
- Fayl maksimum 5 MiB (5 × 1024 × 1024 bayt), açılmış ZIP məzmunu maksimum 50 MiB ola bilər.
- Yalnız ilk iş vərəqi oxunur. Başlıqlar birinci sətirdə olmalıdır. Sütunların yeri sərbəstdir; altı başlığın hamısı tələb olunur.
- Başlıqların kənar boşluqları təmizlənir, müqayisə `OrdinalIgnoreCase` ilə aparılır.
- Tam boş məhsul sətirləri ötürülür. Yalnız başlıqları olan fayl rədd edilir.
- SKU, ad, kateqoriya, qiymət və miqdar məcburidir. Boş `unit` yeni məhsul üçün `Ədəd` olur.
- Formulalar əvəzinə sabit dəyərlər daxil edilməlidir.
- `12,50` və `12.50` qəbul edilir. Minlik ayırıcı dəstəklənmir.
- Mövcud SQL sütunlarına uyğun qiymət maksimum 2, miqdar maksimum 3 onluq rəqəm qəbul edir; artıq dəqiqlik səssiz yuvarlaqlaşdırılmır.

PowerShell nümunəsi (`$token` mövcud giriş tokenidir):

```powershell
curl.exe -H "Authorization: Bearer $token" `
  -o mal_sablon.xlsx http://localhost:5135/api/products/import/template

curl.exe -X POST -H "Authorization: Bearer $token" `
  -F "file=@mal_sablon.xlsx" `
  "http://localhost:5135/api/products/import?warehouseId=<anbar-guid>"
```

Uğurlu cavab (`200`):

```json
{"success":true,"addedCount":2,"updatedCount":1,"errors":[]}
```

Validasiya xətası (`400`), `row` Excel-də görünən sətir nömrəsidir:

```json
{"success":false,"addedCount":0,"updatedCount":0,"errors":[{"row":5,"column":"price","message":"Sətir 5: price düzgün deyil; mənfi olmayan, ən çox 2 onluq rəqəmli ədəd yazın."}]}
```

Fayla və ümumi şərtlərə aid xətaların `row` dəyəri `0`, başlıq xətalarınınkı `1` olur.
Bu endpoint-lərin tətbiq cavabları `application/json; charset=utf-8` və lokal Unicode encoder ilə yaradılır; qlobal JSON konfiqurasiyası dəyişmir.
Server/proksi limitini aşan sorğular tətbiqə çatmadan `413`, autentifikasiya/icazə xətaları isə `401`/`403` qaytara bilər.

## Saxlama qaydaları

Yeni SKU yeni məhsul yaradır. Mövcud SKU yalnız ad və satış qiymətini yeniləyir; barkod, təsvir, kateqoriya, ölçü vahidi və aktivlik qorunur.
Kateqoriya mövcud SKU üçün də yoxlanır. Kateqoriyalar avtomatik yaradılmır.
Silinmiş məhsulun SKU-su təkrar istifadə edilmir; uyğun sətir rədd edilir.
Fayldakı təkrar SKU-lar və bazadakı birmənalı olmayan kateqoriya/SKU adları xəta sayılır.

Miqdar mövcud ümumi stoka əlavə edilir, üzərinə yazılmır. Artım seçilmiş anbarda saxlanır.
Sıfır miqdar məhsul əlavə etməyə/yeniləməyə imkan verir, sıfır stok hərəkəti yaratmır.
Müsbət miqdar üçün stok jurnalı və `Adjustment` əməliyyatı yazılır; bildirişlər toplu yenilənir.
Bu, mövcud admin stok düzəlişinə uyğun qalıq artımıdır: FIFO partiyası, satınalma sənədi və alış maya dəyəri yaratmır.
FIFO ilə çıxarılacaq malları partiya və maya dəyəri ilə mövcud mal qəbulu prosesindən daxil edin.

SQL Server-də bütün yoxlamalar və yazılar bir `Serializable` tranzaksiyasındadır, bir `SaveChangesAsync` çağırışı ilə saxlanır.
Validasiya xətası varsa heç bir entity dəyişdirilmir və heç nə yazılmır.
SQL/commit xətaları serverdə loglanır, istifadəçiyə daxili exception mətni göstərilmir.
SKU-lar silinmiş qeydlərlə birlikdə bir sorğu ilə alınır. Dəqiq ordinal müqayisə SQL collation-dan asılı olmasın deyə uyğunlaşdırma yaddaşda aparılır.
Kateqoriyalar, stoklar və bildirişlər də toplu sorğularla alınır; hər məhsul üçün ayrıca verilənlər bazası sorğusu yoxdur.

## Unicode və məhdudiyyətlər

Mətnlər ClosedXML-dən Unicode kimi oxunur, `Normalize(FormC).Trim()` tətbiq edilir.
Müqayisələr `StringComparer.OrdinalIgnoreCase` ilə aparılır: `i` və `I` eynidir; `İ` və `ı` ayrı açarlardır.
İdxal kodu `ToLower`/`ToUpper` çağırmır.
Mövcud `Product` konstruktoru və `Update` SKU-nu böyük hərfə çevirdiyi üçün yalnız idxal yolunda EF property dəyəri ilə yeni SKU-nun orijinal forması, yenilənən SKU-nun isə bazadakı forması bərpa edilir.
Mövcud domain davranışı başqa yollarda dəyişdirilmir.

Məhsul/kateqoriya mətnləri EF modelində Unicode-dur, migration snapshot-da `nvarchar` kimi saxlanır.
Mövcud `varchar` sahələr status, valyuta və texniki kodlara aiddir; bu idxal onlara Azərbaycan mətnini yazmır.
Layihədə `InvariantGlobalization` parametrinə rast gəlinməyib; idxalın rəqəm və müqayisə qaydaları cari culture-dan asılı deyil.

Canlı bazanın collation-u bu işdə dəyişdirilməyib və yoxlanmayıb.
SQL unikal SKU indeksi ordinal müqayisənin ayrı saydığı `i`, `İ`, `ı` dəyərlərini eyni saya bilər.
Belə unikal indeks toqquşması bütün idxalı geri qaytarır. Bu açarların SQL-də yanaşı saxlanması tələb olunursa, mövcud məlumat və indekslər ayrıca yoxlanaraq collation dəyişikliyi planlaşdırılmalıdır; avtomatik migration yazılmayıb.

## Yoxlama

```powershell
dotnet restore tests/StockManagement.Tests/StockManagement.Tests.csproj
dotnet test tests/StockManagement.Tests/StockManagement.Tests.csproj --filter FullyQualifiedName~ProductImportTests
```

İşləyən API DLL-ləri kilidləyirsə, ayrıca çıxış qovluğu istifadə etmək olar:

```powershell
dotnet test tests/StockManagement.Tests/StockManagement.Tests.csproj --no-restore -m:1 `
  -p:BuildInParallel=false -p:OutputPath=bin/ImportVerification/ `
  --filter FullyQualifiedName~ProductImportTests
```

Testlər mövcud EF InMemory paketindən istifadə edir; real SQL tranzaksiyası, rollback, kilid və collation davranışını sübut etmir.
Son yoxlamada idxala aid 23 testin hamısı keçib, layihə uğurla yığılıb.
İstehsaldan əvvəl SQL Server test bazasında paralel idxal, unikal indeks toqquşması və yazma zamanı xəta ilə rollback yoxlanmalıdır.
Ümumi test dəstində idxaldan əvvəl də olan 6 xəta əvvəlki yığım ilə müqayisə edilərək təsdiqlənib:
səhifə ölçüsü testi, satınalma qaralamasının redaktəsi və 4 mal qəbulu testi. Bu tapşırıqda həmin testlər və funksiyalar dəyişdirilməyib.
