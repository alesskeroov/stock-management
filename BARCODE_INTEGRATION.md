# Barkod və QR inteqrasiyası

## Ümumi iş prinsipi

Sistem hər məhsul qəbulu zamanı yeni `ProductBatch` (lot) yaradır. Lot nömrəsi (`BatchNumber`) serverdə `LOT-` prefiksi və GUID əsasında avtomatik yaradılır. Bu nömrə həmin lotun sabit skan kodudur. Qəbul əməliyyatı uğurla yadda saxlandıqdan sonra API eyni koddan QR kodun SVG təsvirini generasiya edib cavaba əlavə edir. QR kod URL və ya həssas məlumat saxlamır; yalnız lot nömrəsini saxlayır.

Lot nömrəsi `ProductBatches.BatchNumber` üzərində unikal indekslə qorunur. Məhsulun `Barcode` sahəsində boş olmayan dəyərlər üçün filtrli UNIQUE indeks var; boş barkodlar indeksə daxil edilmir ki, barkodu olmayan çoxsaylı məhsullara icazə verilsin. Məhsul yaradılması və redaktəsi zamanı tətbiq təkrarlanan barkodu yoxlayır, verilənlər bazası indeksi isə paralel sorğulara qarşı son müdafiədir. Barkod sahəsi əl ilə təyin oluna bilər; QR lot qəbulu üçün avtomatik yaradılır.

## Arxitektura və kitabxanalar

- **QRCoder 1.8.1** `StockManagement.Infrastructure` layihəsində istifadə edilir. `IQrCodeGenerator` Application qatında müəyyən olunur, `QrCodeGenerator` infrastruktura aid implementasiyadır və QR kodu SVG formatında yaradır. SVG platformadan asılı olmayan vektor təsviridir; `System.Drawing` tələb etmir.
- **html5-qrcode 2.3.8** React klientində kamera ilə QR və seçilmiş barkod formatlarını oxuyur. Kamera dəstəklənməyəndə və ya icazə verilməyəndə kod USB skanerindən və ya əl ilə daxil edilə bilər.
- QR şəkli DB-də ayrıca saxlanmır. `BatchNumber` QR məzmunu üçün mənbə olduğuna görə təsvir istənilən vaxt eyni koddan təkrar yaradıla bilir.
- Skan endpoint-ləri autentifikasiya tələb edir və `products.read` icazəsi ilə qorunur.

## API istifadəsi

### Anbara mal qəbulu

`POST /api/inventory/receipts` mövcud qəbul əməliyyatıdır. Müvəffəqiyyətli cavab partiya nömrəsi ilə yanaşı `qrCodeSvg` sahəsini də qaytarır:

```json
{
  "batchNumber": "LOT-0123456789abcdef",
  "quantity": 4,
  "entryDate": "2026-10-19T12:00:00Z",
  "qrCodeSvg": "<svg ...>...</svg>"
}
```

Klient bu SVG-ni `image/svg+xml` kimi göstərərək çap edə bilər. Tətbiqdə qəbuldakı uğur bildirişindən “QR kodu göstər” seçilir.

### Barkod/QR dəyəri ilə məlumat tapmaq

`GET /api/inventory/scan/{code}` həm lot nömrəsini, həm də boş olmayan məhsul barkodunu qəbul edir. Tapılmadıqda `404` qaytarır. Lot skanının nümunə cavabı:

```json
{
  "code": "LOT-0123456789abcdef",
  "codeType": "batch",
  "productId": "00000000-0000-0000-0000-000000000001",
  "productName": "Nümunə məhsul",
  "sku": "SAMPLE-1",
  "barcode": "1234567890123",
  "batchNumber": "LOT-0123456789abcdef",
  "warehouseName": "Əsas anbar",
  "remainingQuantity": 4,
  "entryDate": "2026-10-19T12:00:00Z",
  "expiryDate": null
}
```

Məhsul barkodu üçün `codeType` `product` olur; lot və qalıq sahələri `null` qaytarılır.

### QR təsvirini ayrıca almaq

`GET /api/inventory/scan/{code}/qr` kod üçün `image/svg+xml` qaytarır. Məsələn, partiya nömrəsi `LOT-0123456789abcdef` olduqda:

```text
GET /api/inventory/scan/LOT-0123456789abcdef/qr
```

## Frontend-də skan

Məhsullar səhifəsində **QR / barkod skan et** düyməsinə basın. Kamera icazəsi veriləndə kod oxunan kimi klient `/api/inventory/scan/{code}` çağırır, məhsul siyahısında uyğun SKU üzrə filtrləyir və lot qalığını bildirişdə göstərir. Kodu əl ilə və ya klaviatura kimi işləyən USB skanerlə də daxil etmək olar; skaner adətən Enter göndərdiyindən forma birbaşa axtarış edir.

Brauzerlər kamera istifadəsini təhlükəsiz kontekstlə məhdudlaşdırır. Yerli `localhost` ünvanında və HTTPS üzərindən işlədin, brauzerə kamera icazəsi verin. Kamera işləmirsə USB skaner və ya əl ilə kod daxiletməsi mövcuddur.

## Database migration

`20261019190628_AddProductBarcodeUniqueIndex` migration-ı `Products.Barcode` sahəsinə yalnız filtrli UNIQUE indeks əlavə edir. Mövcud dolu barkodların təkrarlanmadığı migration-dan əvvəl yoxlanılıb. Mövcud `ProductBatches.BatchNumber` UNIQUE indeksinə dəyişiklik etmir, çünki həmin indeks artıq `AddProductBatches` migration-ında var.

Migration-ı yaratmaq və tətbiq etmək:

```powershell
dotnet ef migrations add AddProductBarcodeUniqueIndex --project src/StockManagement.Infrastructure --startup-project src/StockManagement.Api
dotnet ef database update --project src/StockManagement.Infrastructure --startup-project src/StockManagement.Api
```
