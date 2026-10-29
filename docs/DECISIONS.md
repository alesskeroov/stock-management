# Qərarlar jurnalı

Yoxlama tarixi: 2026-10-29. Bu sənəd cari kodu və bu sessiyada təsdiqlənən qərarları qeyd edir.
Stack seçiminin ilkin tarixi və müzakirəsi məlum deyil. Onlar uydurulmur.
Alternativlər sütunundakı təkliflər tarixi komanda müzakirəsi kimi təqdim edilmir.

| Tarix | Qərar | Niyə | Alternativlər |
|---|---|---|---|
| TODO: dəqiqləşdir; 2026-10-29 kodda yoxlanıb | .NET 9, EF Core, SQL Server istifadə edilir | csproj və `AddInfrastructure` bunu göstərir. İlkin seçim səbəbi: TODO: dəqiqləşdir | Hansı alternativlərin qiymətləndirildiyi məlum deyil |
| TODO: dəqiqləşdir; 2026-10-29 kodda yoxlanıb | Domain/Application/Infrastructure/API qatları saxlanır | Project reference-ləri qatları ayırır. İlkin seçim səbəbi: TODO: dəqiqləşdir | Tarixi alternativ: TODO: dəqiqləşdir |
| TODO: dəqiqləşdir; 2026-10-29 kodda yoxlanıb | Veb React/Vite, mobil Expo/React Native-dir | package.json fayllarında göstərilir. Stack seçim səbəbi sənədləşdirilməyib | Tarixi alternativ: TODO: dəqiqləşdir |
| TODO: dəqiqləşdir; 2026-10-29 kodda yoxlanıb | API-də permission policy, UI-da `hasPermission` işləyir | API yazma icazəsini yoxlayır, UI uyğun əməliyyatı göstərir; kod: `Program.cs`, `Roles.cs`, `App.tsx` | Yalnız UI-da gizlətmə server yoxlamasını əvəz etmir; tarixi müzakirə məlum deyil |
| 2026-10-29 | Excel idxalı ayrıca interfeys, xidmət, DTO və controller-dədir | Mövcud entity/schema dəyişmədən funksiyanı əlavə etmək; istifadəçinin minimum dəyişiklik tələbi | Mövcud controller-i böyütmək və schema dəyişmək seçilmədi |
| 2026-10-29 | Excel üçün yeganə yeni birbaşa paket ClosedXML 0.105.0-dır | İstifadəçinin paket məhdudiyyəti və `.xlsx` tələbi | Başqa birbaşa paket əlavə edilmədi |
| 2026-10-29 | İdxal bir Serializable SQL tranzaksiyası ilə saxlanır | Sətirlərdən biri səhvdirsə heç nə yazmamaq və read/add stok artımını qorumaq | Qismən uğurlu idxal tələbə uyğun deyildi; real SQL davranışı ayrıca yoxlanmalıdır |
| 2026-10-29 | İdxalda FormC və OrdinalIgnoreCase müqayisəsi işlənir | Unicode mətnini və `i/İ/ı` fərqlərini qorumaq; `ProductImportTests` ilə yoxlanıb | Culture-a bağlı böyük/kiçik hərf çevirməsi idxal müqayisəsində istifadə edilmir; SQL collation ayrıca mövzudur |
| 2026-10-29 | Bir neçə anbarda `warehouseId` tələb edilir | Excel-də anbar sütunu yoxdur; təsadüfi anbara yazmağın qarşısını almaq | Yalnız bir anbar olduqda avtomatik seçim var |
| 2026-10-29 | İdxal FIFO partiyası yaratmır | Şablonda partiya və alış maya dəyəri yoxdur; mövcud qalıq düzəlişi yolunu izləyir | FIFO üçün mövcud partiyalı mal qəbulu istifadə edilir; vahid stok modeli hələ açıq məsələdir |
| 2026-10-29 | `IFormFile` parametrində birbaşa `[FromForm]` silindi | Swashbuckle 8.1.4 sənəd yarada bilmirdi; xəta regression testdə təkrarlanıb düzəldildi | Form DTO yanaşması da mümkündür, bu düzəlişdə əlavə model tələb olunmadı |
| 2026-10-29 | Bu sənədləşdirmədə mətnlər birləşdirilir, biznes kodu dəyişdirilmir | İstifadəçinin mövcud faylları qoruma tələbi | Əvvəlki sənədləri silmək seçilmədi |
| 2026-10-29 | Enter cari formda `requestSubmit` ilə, PO sətrində yerli əlavə düyməsi ilə işlənir | Validation və submit handler-i qorumaq, bütün səhifəni təsadüfən göndərməmək | Global key listener və validation-ı keçən `form.submit()` istifadə edilmir |
| 2026-10-29 | Excel drop və fayl seçimi eyni validatorla işləyir | Hər iki girişdə bir fayl, .xlsx və 5 MB qaydalarını eyni saxlamaq | Ayrı validation yolları yaradılmır |

## Köhnə qeydlərlə əlaqə

[Kök ADR](../ADR.md) və [src/docs ADR](../src/docs/ADR.md) saxlanılıb.
Onların bəzi rol izahları cari `RolePermissionDefaults` ilə uyğun deyil.
Məsələn, cari Manager PO əməliyyatları edə bilir; WarehouseKeeper default xəritəsində `inventory.count` yoxdur.
Bu jurnaldakı qeyd köhnə qərarın nə vaxt dəyişdiyini sübut etmir: TODO: dəqiqləşdir.

## Yeni qərar necə əlavə edilir?

1. Əvvəl [RESEARCH.md](../RESEARCH.md) şablonu ilə sualı araşdır.
2. Seçimi və səbəbini kod, test və ya rəsmi mənbə ilə əsaslandır.
3. Tarix, qərar, səbəb və real qiymətləndirilmiş alternativi cədvələ əlavə et.
4. Hələ qəbul edilməmiş təklifi “təklif” kimi yaz; tamamlanmış qərar kimi göstərmə.

## Açıq qərarlar

- TODO: dəqiqləşdir — SKU indeksinin SQL collation-u və Unicode açar siyasəti.
- TODO: dəqiqləşdir — stok ledger/cache/partiya modelləri arasında əsas mənbə.
- TODO: dəqiqləşdir — istehsal demo seed və chatbot endpoint icazələri.
- TODO: dəqiqləşdir — dəqiq Node.js/pnpm versiyaları, CI, deployment və commit/PR standartı.
