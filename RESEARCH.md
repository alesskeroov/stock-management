# Stockroom araşdırma dəftəri

Son baxış: 2026-10-29. Oxucu junior developer-dir.
Məqsəd yeni mövzunu sadə izah, qısa nümunə və yoxlanmış mənbə ilə öyrənməkdir.

## A) Araşdırma növbəsi

Statuslar: `[ ]` gözləyir, `[~]` davam edir, `[x]` bitdi. “Bitdi” yalnız göstərilən dar sual üçündür.

- [ ] .NET qatları və Dependency Injection: bir HTTP sorğusu hansı siniflərdən keçir?
- [ ] JWT, refresh token, role və permission policy: 401 və 403 nə vaxt gəlir?
- [ ] EF Core tracking və transaction: eyni entity niyə iki dəfə izlənməməlidir?
- [ ] SQL Server stok tutarlılığı: ledger, cache, FIFO partiyası və paralel qəbul necə uzlaşır?
- [ ] React state, props və formalar: modalın açıq vəziyyəti harada saxlanmalıdır?
- [ ] Expo konfiqurasiyası və SecureStore: API ünvanı və tokenlər necə idarə edilir?
- [~] Excel və Azərbaycan hərfləri: NFC/ordinal davranışı test olunub, real SQL collation yoxlanmalıdır.
- [x] Swagger-də `IFormFile` parametrini göstərmək: birbaşa `[FromForm]` problemi təkrarlanıb düzəldilib, regression test keçib.

### Rəsmi mənbələrlə başlanğıc xəritəsi

Aşağıdakı linklər 2026-10-29 tarixində yoxlanıb. Linkin açılması mövzunun tam öyrənildiyi demək deyil.

| Mövzu | Rəsmi mənbə |
|---|---|
| ASP.NET permission policy | [Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/policies?view=aspnetcore-9.0) |
| EF Core transaction | [Microsoft Learn](https://learn.microsoft.com/en-us/ef/core/saving/transactions) |
| React ortaq state | [React Learn](https://react.dev/learn/sharing-state-between-components) |
| Vite env dəyişənləri | [Vite](https://vite.dev/guide/env-and-mode) |
| Expo SecureStore | [Expo](https://docs.expo.dev/versions/latest/sdk/securestore/) |
| Excel oxuma/yazma | [ClosedXML](https://docs.closedxml.io/en/latest/) |

`latest` sənədləri layihədəki paket versiyasından fərqlənə bilər; tətbiqdən əvvəl uyğun versiya yoxlanmalıdır.

### Əvvəlki araşdırma sualları: Role-based authorization, policy security və UI rendering

- ASP.NET Core-da `[Authorize(Roles = "...")]` ilə `[Authorize(Policy = "...")]` arasındakı fərq nədir və permission claim-lər policy-lərə necə bağlanır?
- ASP.NET Core authorization handler-ləri policy qərarını necə verir? Bir istifadəçinin bir neçə rolu olduqda nəticə necə hesablanır?
- React-də mərkəzi `hasPermission` köməkçisi, Context/Provider və deklarativ şərti renderin üstünlükləri və çatışmazlıqları nələrdir?
- UI-da düyməni gizlətmək və deaktiv etmək istifadəçi təcrübəsi baxımından nə vaxt uyğundur? Bu yanaşma niyə server authorization-ı əvəz etmir?
- JWT-də role/permission claim-ləri nə zaman yenilənir və rol dəyişiklikləri köhnə access token-in qüvvədə olduğu müddətdə necə təsir göstərir?
- Hər rol üçün allow/deny cədvəlindən ASP.NET API inteqrasiya testləri necə qurulur?
- Frontend və backend arasında permission adlarının sinxron qalmasını OpenAPI-dən generasiya və ya ortaq müqavilə ilə necə təmin etmək olar?

Bu əvvəlki suallar saxlanılıb. Cavablandırılmamış suallar tamamlanmış araşdırma kimi işarələnmir.
Əlavə tarixi suallar: [src/docs/RESEARCH.md](src/docs/RESEARCH.md).

## B) Mövzu şablonu

Hər yeni mövzu üçün aşağıdakı bloku bu hissədə kopyalayın.
Şablondakı kod yeri placeholder-dir; tamamlanan qeyddə real, qısa və işləyən nümunə ilə əvəz edin.

````markdown
## Mövzu adı
- **Sual:** nəyi bilmək istəyirəm?
- **Sadə izah (5 cümləyə qədər):** uşağa izah edirmiş kimi.
- **Niyə lazımdır:** bu layihədə hansı faylda/əməliyyatda istifadə edilir?
- **Kiçik kod nümunəsi:** aşağıda işləyən kod və onu yoxlama əmri.
```text
TODO: dəqiqləşdir — mövzuya uyğun işləyən nümunə.
```
- **Tez-tez edilən səhvlər:** konkret nümunələr.
- **Mənbələr:** rəsmi sənəd linki, versiya və baxış tarixi (YYYY-MM-DD).
- **Nəticə / qərar:** nə istifadə edəcəyik, niyə? Qəbul edilməyibsə “təklif” yaz.
- **Növbəti addım:** nə yoxlanmalıdır və daha nə öyrənməliyik?
````

Araşdırma qaydaları:

1. Əvvəl lokal kod və paket versiyasını yoxla, sonra rəsmi sənədi oxu.
2. Məqalə istifadə etsən, onu rəsmi mənbədən ayrı göstər.
3. Mənbəsiz texniki iddia yazma. Əmin deyilsənsə “yoxlanmalıdır” yaz.
4. Nümunəni real əmrlə/testlə yoxla; yoxlanmayıbsa bunu açıq bildir.
5. Qərar çıxarsa [docs/DECISIONS.md](docs/DECISIONS.md)-yə də əlavə et.
6. Nəticəni A hissəsində statusla və C hissəsində tarixlə qeyd et.

### Nümunə mövzu: Unicode normallaşdırması və SKU müqayisəsi

- **Sual:** görünüşcə eyni mətnləri necə normallaşdıraq, `i`, `İ`, `ı` fərqini necə qoruyaq?
- **Sadə izah (5 cümləyə qədər):** Eyni görünən hərf müxtəlif Unicode ardıcıllığı ilə yazıla bilər. FormC uyğun ardıcıllıqları bir formaya gətirir. OrdinalIgnoreCase cari istifadəçi dilinə əsaslanmadan müqayisə edir. Layihənin testlərində `i` ilə `I` eyni, `İ` və `ı` isə ayrı açarlardır. SQL unikal indeksinin eyni davranacağı ayrıca yoxlanmalıdır.
- **Niyə lazımdır:** `ProductImportService` Excel mətnini normallaşdırır və SKU/kateqoriyanı uyğunlaşdırır.
- **Kiçik kod nümunəsi:** .NET console layihəsində `Program.cs` üçün:

```csharp
using System;
using System.Text;

var name = " S\u0327əkər ".Normalize(NormalizationForm.FormC).Trim();
Console.WriteLine(name); // Şəkər
Console.WriteLine(StringComparer.OrdinalIgnoreCase.Equals("i", "I")); // True
Console.WriteLine(StringComparer.OrdinalIgnoreCase.Equals("i", "ı")); // False
Console.WriteLine(StringComparer.OrdinalIgnoreCase.Equals("i", "İ")); // False
```

- **Tez-tez edilən səhvlər:** `ToUpper` nəticəsini müqayisə açarı saymaq; UTF-8 mətni yenidən başqa encoding ilə çevirmək; InMemory testindən SQL collation nəticəsi çıxarmaq.
- **Mənbələr:** [String.Normalize, .NET 9](https://learn.microsoft.com/en-us/dotnet/api/system.string.normalize?view=net-9.0), [OrdinalIgnoreCase, .NET 9](https://learn.microsoft.com/en-us/dotnet/api/system.stringcomparer.ordinalignorecase?view=net-9.0), baxış: 2026-10-29. Lokal sübut: `ProductImportTests.AzerbaijaniTextIsNormalizedAndDottedAndDotlessKeysRemainDistinct` və `CategoryMatchingDoesNotMergeDottedAndDotlessI`.
- **Nəticə / qərar:** idxalda FormC + OrdinalIgnoreCase saxlanır; qərar DECISIONS jurnalındadır. Console nümunəsi ayrıca işə salınmayıb, ekvivalent müqayisələr xUnit testlərində keçib.
- **Növbəti addım:** TODO: dəqiqləşdir — real SQL Server SKU collation-u və yanaşı saxlanma tələbi.

### Nümunə mövzu: Swagger və fayl parametri

- **Sual:** niyə Excel endpoint-i əlavə ediləndə bütün `swagger.json` 500 verdi?
- **Sadə izah (5 cümləyə qədər):** Swagger API metodlarını oxuyub sənəd hazırlayır. Bir metodun parametri səhv təsvir ediləndə bütün sənəd yaradıla bilməz. Bu layihədə Swashbuckle 8.1.4 birbaşa `[FromForm] IFormFile` parametrində xəta verdi. Atribut silinəndən sonra multipart sahəsi yarandı. Dəyişiklik testlə yoxlanıb.
- **Niyə lazımdır:** `ProductImportController.Import` fayl alır; Swagger bu fayl sahəsini göstərməlidir.
- **Kiçik kod nümunəsi:** ASP.NET Core Web API-də controller faylı; layihədəki metodu sadələşdirən nümunə:

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/example-upload")]
public sealed class ExampleUploadController : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    public IActionResult Upload(IFormFile file) => Ok(new { file.FileName });
}
```

- **Tez-tez edilən səhvlər:** birbaşa fayl parametrinə `[FromForm]` əlavə etmək; FormData üçün multipart boundary-ni browser əvəzinə əl ilə yazmaq; yalnız build yoxlayıb Swagger sənədini yoxlamamaq.
- **Mənbələr:** [Swashbuckle 8.1.4 rəsmi repo](https://github.com/domaindrivendev/Swashbuckle.AspNetCore/blob/v8.1.4/README.md), baxış: 2026-10-29. Konkret xəta lokal generator testi ilə təsdiqlənib: `SwaggerTests.ApiDefinitionIncludesExcelUploadAndTemplate`.
- **Nəticə / qərar:** real import metodunda fayl parametrinin birbaşa `[FromForm]` atributu silinib. Qərar DECISIONS jurnalına əlavə edilib. Nümunə ayrıca endpoint kimi əlavə edilməyib; real controller compile və generator testi ilə yoxlanıb.
- **Növbəti addım:** real HTTP multipart sorğusu və 401/403 davranışları üçün SQL-dən asılı olmayan inteqrasiya testi araşdırmaq.

### Nümunə mövzu: Enter ilə form göndərişi və Excel drag-and-drop

- **Sual:** Enter validation-ı keçmədən necə formu göndərsin və sürüklənən fayl necə eyni yoxlamadan keçsin?
- **Sadə izah (5 cümləyə qədər):** `requestSubmit` düyməyə basmaq kimi formu yoxlayıb submit hadisəsini çağırır. Textarea-da Enter yeni sətir yaratmalıdır. PO sətirinin miqdar/qiymət sahəsində Enter həmin sətri əlavə edir. Excel faylı həm seçiləndə, həm sürüklənəndə eyni validation funksiyasından keçir. Drop hadisəsində browser-in faylı səhifə kimi açması dayandırılır.
- **Niyə lazımdır:** `client/src/formKeyboard.ts`, `importFileValidation.ts`, `ProductImportDialog.tsx` və mobil `App.tsx`.
- **Kiçik kod nümunəsi:** layihədəki düymə və formu nəzərdə tutan browser kodu:

```js
const button = document.querySelector('button[type="submit"]')
if (button && !button.disabled) button.form.requestSubmit(button)
```

- **Tez-tez edilən səhvlər:** `form.submit()` ilə validation/submit handler-i keçmək; bütün səhifəyə global Enter bağlamaq; textarea və IME yazısını yarımçıq göndərmək; drop üçün ayrıca zəif validation yazmaq.
- **Mənbələr:** [MDN requestSubmit](https://developer.mozilla.org/en-US/docs/Web/API/HTMLFormElement/requestSubmit), [MDN file drag-and-drop](https://developer.mozilla.org/en-US/docs/Web/API/HTML_Drag_and_Drop_API/File_drag_and_drop), [React Native TextInput](https://reactnative.dev/docs/textinput#submitbehavior). Baxış: 2026-10-29.
- **Nəticə / qərar:** yalnız cari formda Enter işlənir, `textarea`, select və digər native kontrollerin davranışı qorunur. Fayl seçimi və drop eyni validatoru işlədir. Helper və validation üçün 22 Node testi keçib; real browser/cihaz E2E testi aparılmayıb.
- **Növbəti addım:** drag-and-drop və login autofill üçün real browser E2E yoxlaması əlavə etmək.

## C) Öyrəndiklərim jurnalı

- **2026-10-29:** Enter üçün düyməni göstərərək `requestSubmit` işlətmək validation-ı qoruyur. Çoxsətirli sahə və fayl seçimi ümumi klaviatura handler-indən kənarda saxlanmalıdır.

- **2026-10-29:** SKU müqayisəsi tətbiq yaddaşında və SQL indeksində ayrı qaydalarla işləyə bilər. Unicode testləri keçsə də, SQL collation ayrıca yoxlanmalıdır.
- **2026-10-29:** Swagger generatoru da ayrıca test edilə bilir. Fayl parametrindəki yanlış atribut bütün API sənədini dayandırmışdı; regression testi bu halı tutur.
- **2026-10-29:** Test csproj solution-a daxil deyil. Sənədlərdə test əmri birbaşa project yolunu göstərməlidir; yoxlamada 70 testdən 6-sı uğursuz qaldı.
