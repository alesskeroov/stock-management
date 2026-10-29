# Stockroom iş planı

Son kod və test yoxlaması: 2026-10-29.
Bu plan cari repoya əsaslanır. Biznes prioritetləri təsdiqlənmiş yol xəritəsi deyil.

## Cari məqsəd

Junior developer üçün layihəni qurmaq, işlətmək və dəyişmək addımlarını sənədləşdirmək.
Mövcud stok və Excel idxalı davranışını aydın göstərmək.
TODO: dəqiqləşdir — növbəti məhsul buraxılışının məqsədi və tarixi.

## Sənədləşdirmə addımları

- [x] Qovluqları, project reference-ləri, script-ləri və controller-ləri oxumaq.
- [x] Mövcud README, araşdırma və ADR qeydlərini nəzərdən keçirmək.
- [x] AGENTS.md və sənədlərin bir-birinə keçidlərini hazırlamaq.
- [x] README-ni mövcud məzmunu qoruyaraq genişləndirmək.
- [x] Arxitekturanı, qərarları və araşdırma şablonunu yazmaq.
- [x] Test, veb build/lint və mobil typecheck nəticələrini qeyd etmək.
- [ ] TODO: dəqiqləşdir — məhsul sahibi sənədlərdəki açıq sualları cavablandırsın.

## Kodda hazır olanlar

Checkbox funksiyanın kodda olduğunu göstərir; istehsal hazırlığı zəmanəti deyil.

- [x] Məhsul/kateqoriya kataloqu, JWT giriş və icazələr.
- [x] Anbar qalığı, partiyalar, FIFO çıxışı və stok düzəlişləri.
- [x] Təchizatçı, PO, təsdiq və GRN əməliyyatları.
- [x] Stok/audit qeydləri, bildiriş və satınalma hesabatları.
- [x] Excel idxalı API-si, şablon və veb idxal pəncərəsi.
- [x] Swagger-də multipart fayl sahəsi üçün regression testi.
- [x] Expo mobil giriş, məhsul/kateqoriya ekranları və məhsul formaları.

## Cari yoxlama nəticələri

| Yoxlama | 2026-10-29 nəticəsi |
|---|---|
| xUnit, birbaşa test csproj | 70 test: 64 keçdi, 6 uğursuz |
| Veb `npm run build` | Keçdi; 500 kB-dan böyük chunk xəbərdarlığı var |
| Veb `npm run lint` | Exit code 0; React purity, refs və effect xəbərdarlıqları var |
| Mobil `npm run typecheck` | Uğursuz: `App.tsx:8`, TS2580, `process` tipi tapılmır |

Testlər solution-a daxil olmadığı üçün test layihəsi ayrıca işə salınıb.
Yoxlama DLL kilidindən yayınmaq üçün `bin/DocumentationVerification/` ilə aparılıb.
Real SQL rollback, collation, paralel idxal və cihaz üzərində mobil smoke test aparılmayıb.

## Növbəti texniki işlər

Klaviatura/drag-and-drop işi üzrə əlavə yoxlama, 2026-10-29:
vebdə 22 Node helper/validation testi keçdi; mobil `process.env` tipi üçün lokal declaration əlavə edildikdən sonra typecheck keçdi.
Yuxarıdakı cədvəl sənədləşdirmə mərhələsindəki əvvəlki nəticəni saxlayır.
Yeni test əmri: `client` daxilində `pnpm test` (`npm test` də eyni script-i çağırır).
Testlər Node v24.22.0 ilə işə salınıb; real browser/cihaz E2E yoxlamasını əvəz etmir.

PO redaktəsi yoxlaması, 2026-10-29: yeni sifariş sətirləri açıq şəkildə `Added` kimi qeyd ediləndən sonra 7 PO testi keçdi.
Yadda saxlanan miqdar/qiymət və yekun məbləğ ChangeTracker təmizləndikdən sonra yenidən oxunaraq yoxlanıb; stok dəyişməyib.
Son ümumi backend yoxlaması: 70 testdən 65-i keçdi, əvvəlki 5 xəta qalır (səhifələmə və 4 GRN testi).

- [ ] `ProductPaginationTests.InvalidPageAndSizeUseSafeDefaultsAndSizeIsCapped`: test 50, kod 10 gözləyir; düzgün limiti razılaşdırmaq.
- [x] `PurchaseOrderTests.CreateAndEditDraftRecalculateTotalsWithoutChangingStock`: yeni sətirləri AddRange ilə qeyd edib concurrency xətasını düzəltmək; saxlanmış dəyərləri yenidən oxumaqla yoxlamaq.
- [ ] `GoodsReceiptTests.OverReceiptIsRejectedByDefaultAndAllowedWithinConfiguredTolerance` xətasını araşdırmaq.
- [ ] `GoodsReceiptTests.RejectedOnlyReceiptDoesNotIncreaseStockOrChangePoReceiptQuantity` xətasını araşdırmaq.
- [ ] `GoodsReceiptTests.IdempotencyReturnsExistingReceiptWithoutRepeatingMovement` xətasını araşdırmaq.
- [ ] `GoodsReceiptTests.DraftAndCancelledOrdersCannotReceiveAndAuditFailureLeavesNoBusinessWrites` xətasını araşdırmaq.
- [x] Mobil TS2580: istifadə edilən Expo public env dəyişəni üçün lokal TypeScript declaration əlavə etmək; typecheck keçdi.
- [ ] SQL test bazasında idxal rollback/unikal SKU/paralel sorğuları yoxlamaq.
- [ ] Stocks, Product.Stock, StockMovements və ProductBatches uyğunluğunu real məlumatla yoxlamaq.
- [ ] Veb lint xəbərdarlıqlarını və build chunk ölçüsünü ayrıca tapşırıqda həll etmək.
- [ ] TODO: dəqiqləşdir — test layihəsinin solution-a əlavə edilməsi və CI siyasəti.
- [ ] TODO: dəqiqləşdir — demo seed-in Production-da çağırılma siyasəti.
- [ ] TODO: dəqiqləşdir — anonim chatbot-data endpoint-lərinin giriş siyasəti.
- [ ] TODO: dəqiqləşdir — deployment, backup/restore, məsul şəxs və lisenziya.

Bu sənədləşdirmə tapşırığında biznes kodu və mövcud uğursuz testlər dəyişdirilmir.
