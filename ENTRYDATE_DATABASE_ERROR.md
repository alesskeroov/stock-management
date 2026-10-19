# `Invalid column name 'EntryDate'` xətası

## Səbəb

`EfRepository<TEntity>.GetAllAsync()` düzgün işləyir və dəyişdirilməyib:

```csharp
await Set.AsNoTracking().ToListAsync(cancellationToken);
```

`Product` üçün `ToListAsync()` modelə uyğun bütün sütunları oxuyur. Modeldə `EntryDate` var, amma API-nin hədəf bazasındakı `Products` cədvəlində bu sütun olmadığı üçün SQL Server Error 207 qaytarır. Sorğunu dəyişmək həll deyil; model və DB sxemi uyğunlaşdırılmalıdır.

## Connection string yoxlaması

Connection string-in məxfi hissələri göstərilmədən yoxlanılıb:

| Mənbə | Server | Database | Qeyd |
|---|---|---|---|
| `appsettings.json` | `Revan` | `StockManagmentDb` | Integrated Security |
| `StockManagementDbContextFactory` | `Revan` | `StockManagmentDb` | EF design-time bağlantısı |
| `appsettings.Development.json` | — | — | Override yoxdur |
| `launchSettings.json` | — | — | Yalnız `ASPNETCORE_ENVIRONMENT=Development` təyin edir |
| Cari shell environment / user-secrets | — | — | `StockManagementDb` override tapılmadı |

`StockManagmentDb` yazılışı appsettings və EF design-time factory-də eynidir. Visual Studio prosesi ayrıca environment variable təyin edə bilər; bunu Debug Environment bölməsində yoxlayın.

## Model və mövcud migration

- `Product.EntryDate`: CLR `DateTime`, nullable deyil.
- EF konfiqurasiyası: sütun adı `EntryDate`, default `SYSUTCDATETIME()`, ayrıca schema təyinatı yoxdur (SQL Server-də default schema `dbo`).
- `20261016150000_AddProductEntryDate` migration artıq mövcuddur. `Up()` `Products.EntryDate` sütununu `datetime2 NOT NULL` tipində, `SYSUTCDATETIME()` default-u ilə əlavə edir və mövcud sətirlərə UTC tarix təyin edir. Təkrar migration yaradılmayıb.
- `Program.cs`-də `Database.MigrateAsync()` yoxdur. Avtomatik migration əlavə etmək tələb olunmur; DB yeniləməsi ayrıca aparılmalıdır.

## Гүйцэтгэсэн команд ба үр дүн

Эхний parallel build амжилтгүй болсон. Infrastructure build-ийг дараах командаар амжилттай хийсэн:

```powershell
dotnet build src/StockManagement.Infrastructure --no-restore -m:1
```

`dotnet ef migrations list --project src/StockManagement.Infrastructure --startup-project src/StockManagement.Api --no-build` dörd migration sadaladı, lakin DB-yə qoşula bilmədiyindən hansının tətbiq olunduğunu təsdiqləmədi.

`dotnet ef database update` нь `Revan` сервер рүү Named Pipes / Error 5 `Access is denied` алдаагаар холбогдож чадаагүй. Иймээс database-д өөрчлөлт ороогүй.

API build хийх үед ажиллаж байсан процесс `StockManagement.Api/bin/Debug/net9.0/StockManagement.Infrastructure.dll` файлыг түгжсэн тул MSB3027/MSB3021 copy алдаа гарсан. API-г build хийх шаардлагатай бол эхлээд Visual Studio debugger/API процессыг зогсооно.

Migration шалгах үед гарсан `PendingModelChangesWarning` нь `StockManagementDbContextModelSnapshot`-д `ProductBatches` entity дутсантай холбоотой байв. Snapshot EF-in cari modeli ilə uyğunlaşdırıldı və müvəqqəti audit migration-ı silindi. `has-pending-model-changes` дараа нь “No changes” гэж мэдээлсэн. `GetAllAsync()` өөрчлөгдөөгүй.

EF CLI 9.0.8 нь runtime 9.0.21-ээс хуучин гэсэн анхааруулга гаргасан ч шалгалт болон SQL script үүсгэхэд саад болоогүй.

## Idempotent SQL ба DB-д хэрэглэх

Idempotent script амжилттай үүссэн: репозиторийн үндсэн хавтас дахь [migration.sql](migration.sql). Үүнийг SQL Server-т холбогдсон орчинд `Revan` серверийн `StockManagmentDb` базад SSMS-ээр ажиллуулж болно. Эсвэл холболт ажиллах terminal-аас:

```powershell
dotnet ef database update --project src/StockManagement.Infrastructure --startup-project src/StockManagement.Api
```

Холболт амжилтгүй байсан тул migration бодитоор хэрэгжсэн эсэх болон `GET /api/products`-ийн ажиллагааг батлах боломжгүй байв.

DB-də tətbiqdən sonra yoxlamaq üçün SQL:

```sql
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Products' AND COLUMN_NAME = 'EntryDate';

SELECT MigrationId
FROM dbo.__EFMigrationsHistory
WHERE MigrationId LIKE '%AddProductEntryDate%';
```

