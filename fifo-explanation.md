# FIFO anbar idarəetməsi və partiyaların izlənməsi

## FIFO nədir?

FIFO (*First In, First Out* — ilk daxil olan, ilk çıxır) satış zamanı məhsulun ən əvvəl anbara daxil olmuş qalıq partiyasından çıxılmasını tələb edir. Eyni vaxtda daxil olmuş partiyalar üçün sabit ikinci sıralama meyarı istifadə edilməlidir; bu layihədə `EntryDate`, sonra `Id` seçilir. FIFO fiziki məhsul hərəkətini dəstəkləyir və xüsusilə tez xarab olan mallarda köhnə ehtiyatın anbarda qalma riskini azaldır.

## Niyə hər girişi ayrıca partiya kimi saxlamaq lazımdır?

Təkcə `Product.StockQuantity = 100` saxlamaq ümumi sayını göstərir, amma bu 100 vahidin nə vaxt, hansı maya dəyəri ilə və hansı son istifadə tarixi ilə alındığını itirir. `ProductBatch` hər qəbul üçün ayrıca qeyddir:

- `InitialQuantity` qəbul edilən ilkin miqdarı saxlayır.
- `RemainingQuantity` satış və digər çıxışlardan sonra partiyada qalan miqdardır.
- `EntryDate` və istəyə bağlı `ExpiryDate` izlənə bilən tarixlər verir.
- `PurchasePrice` həmin partiyanın maya dəyərini, `SalePrice` təklif edilən satış qiymətini saxlayır.
- `BatchNumber` qəbul, inventar və satış qeydlərini bir-birinə bağlayır.

Beləliklə, stok qalığı partiyalardan hesablanır, maya dəyəri isə satışda həqiqətən çıxılan partiyalara əsasən müəyyən edilə bilər. Son istifadə tarixinə görə çıxarış tələb edilərsə, FIFO sıralamasını FEFO (*First Expired, First Out*) qaydasına dəyişmək mümkündür.

## Mal qəbulu

Qəbul əməliyyatı müsbət miqdar, alış və satış qiyməti, məhsul və anbar tələb edir. `EntryDate` ötürülməzsə UTC cari vaxt istifadə edilir. Backend unikal LOT kodu yaradır, ilkin və qalıq miqdarı bərabər edir, ümumi `Stock.Quantity`-ni artırır və `Purchase` tipli əməliyyat tarixçəsi yaradır. Bunların hamısı bir DB tranzaksiyasında saxlanılır.

```csharp
var received = await inventoryService.ReceiveAsync(
    new ReceiveStockRequest(
        ProductId: productId,
        WarehouseId: warehouseId,
        Quantity: 24,
        PurchasePrice: 1.20m,
        SalePrice: 1.75m,
        EntryDate: DateTime.UtcNow,
        ExpiryDate: DateTime.UtcNow.AddDays(10)),
    appUserId,
    cancellationToken);

// received.BatchNumber, received.Quantity, received.EntryDate
```

## Satış zamanı FIFO necə işləyir?

1. Məhsul, anbar və istifadəçinin mövcudluğu yoxlanır.
2. DB-də serializable tranzaksiya açılır; eyni məhsul/anbar qalığından paralel çıxarışlar ardıcıllığa salınır.
3. `RemainingQuantity > 0` olan partiyalar `EntryDate`, sonra `Id` artan sırası ilə oxunur.
4. Əlçatan miqdar tələbə çatmırsa domen xətası atılır və tranzaksiya rollback edilir.
5. Ən köhnə partiyadan tələb qədər çıxılır. Partiya bitdikdə növbəti partiyaya keçilir.
6. Partiyaların `RemainingQuantity` və anbarın ümumi `Stock.Quantity` qalıqları azaldılır, mənfi `Sale` əməliyyatı yazılır; hamısı birlikdə commit olunur.
7. Cavabda istifadə edilmiş partiyalar və hər birindən götürülən miqdar qaytarılır.

```csharp
var issue = await inventoryService.IssueFifoAsync(
    productId,
    warehouseId,
    quantity: 30,
    referenceNo: "INV-2026-0042",
    appUserId,
    cancellationToken);

foreach (var item in issue.Allocations)
    Console.WriteLine($"{item.BatchNumber}: {item.Quantity} vahid, maya dəyəri {item.PurchasePrice}");
```

## Həyat nümunəsi: süd məhsulları

| Partiya | Giriş | İlkin miqdar | Alış qiyməti | Qalan |
|---|---|---:|---:|---:|
| LOT-A | 1 oktyabr | 20 | 1.20 ₼ | 20 |
| LOT-B | 3 oktyabr | 30 | 1.30 ₼ | 30 |

Mağaza 25 vahid satdıqda backend əvvəl LOT-A-dan 20, sonra LOT-B-dən 5 çıxır. Nəticə LOT-A: 0, LOT-B: 25, ümumi stok: 25 vahiddir. Hər iki qəbul tarixi və qiyməti qorunur; sonradan hansı qəbulun satıldığı dəqiq görünür.

Elektron detal nümunəsində də eyni qayda maya dəyərini düzgün izləyir: 100 detalı 4.00 ₼, sonra 50 detalı 4.40 ₼ qiymətinə almışıqsa, 120 satışın ilk 100-ü birinci partiyadan, 20-si ikinci partiyadan götürülür. Satışın FIFO maya dəyəri `100 × 4.00 + 20 × 4.40` olur.

## Layihədəki tətbiq və istismar qeydləri

- `ProductBatch` domen entity-si partiya invariantlarını qoruyur.
- `InventoryService` giriş və çıxış üçün tətbiq servisi API-sidir.
- `EfInventoryRepository` EF Core/SQL Server əməliyyatlarını, tranzaksiyanı və tarixçə qeydlərini yerinə yetirir.
- Yeni `ProductBatches` cədvəli üçün EF migration yaradılıb tətbiq edilməlidir.
- Mövcud `/products/{id}/stock` endpoint-i köhnə ümumi stok düzəlişidir. FIFO partiyalarını yalnız bu yeni servis vasitəsilə dəyişin; köhnə endpoint ilə partiyalar arasında uyğunsuzluq yarana bilər. Mövcud başlanğıc stokunu batch-lərə çevirmək üçün inventar uzlaşdırma/migrasiya addımı lazımdır.
- Bu nümunə satış tranzaksiyası başına bir məhsul üçündür. Sifarişdə bir neçə məhsul olduqda bütün çıxarışları eyni xarici tranzaksiya və idempotent sifariş istinadı daxilində yerinə yetirmək məqsədəuyğundur.
