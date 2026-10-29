# Stockroom üçün iş qaydaları

## Layihə
Stockroom məhsul, anbar qalığı, satınalma sifarişi və mal qəbulunu idarə edir.
Backend .NET 9 / EF Core / SQL Server, veb React / TypeScript / Vite,
mobil tətbiq Expo / React Native üzərindədir. Əsas giriş: README.md.

## Quraşdırma və işə salma
Əmrləri ayrıca terminallarda işlət. SQL və bootstrap konfiqurasiyası README-dədir.
Repo kökündə:
```powershell
dotnet restore StockManagement.sln
dotnet restore tests/StockManagement.Tests/StockManagement.Tests.csproj
dotnet build StockManagement.sln --no-restore
dotnet run --project src/StockManagement.Api
```
Veb üçün `client` qovluğunda:
```powershell
pnpm install
pnpm dev
pnpm build
pnpm lint
pnpm test
```
Mobil üçün `mobile` qovluğunda:
```powershell
pnpm install
pnpm start
pnpm typecheck
```
`pnpm android` və `pnpm ios` skriptləri də mövcuddur.
`npm run build`, `npm run lint`, `npm run typecheck` eyni package.json skriptlərini çağırır.
Lockfile pnpm üçündür; asılılıq quraşdırarkən ikinci lockfile yaratma.

## Testlər
**Tapşırığı bitirməmişdən əvvəl testləri işlət.** Nəticəni olduğu kimi bildir.
Test layihəsi solution-a daxil deyil; yalnız `dotnet test StockManagement.sln` kifayət etmir.
```powershell
dotnet test tests/StockManagement.Tests/StockManagement.Tests.csproj
```
Veb dəyişikliyində `client` daxilində `pnpm build` və `pnpm lint` işlət.
Mobil dəyişikliyində `mobile` daxilində `pnpm typecheck` işlət.
API işləyərkən DLL kilidlənirsə, yoxlama üçün ayrıca çıxış qovluğu seç:
```powershell
dotnet test tests/StockManagement.Tests/StockManagement.Tests.csproj -m:1 -p:BuildInParallel=false -p:OutputPath=bin/Verification/
```
InMemory testini SQL tranzaksiyası və kilidləmə sübutu kimi təqdim etmə.
Hazırkı məlum xətalar və yoxlama tarixi PLAN.md-dədir.
Backend üçün ayrıca lint skripti tapılmayıb: TODO: dəqiqləşdir.

## Qovluqlar
- `src/StockManagement.Api`: controller, HTTP, auth və DI başlanğıcı.
- `src/StockManagement.Application`: DTO, interfeys, validation, tətbiq xidmətləri.
- `src/StockManagement.Domain`: entity, enum və domain qaydaları.
- `src/StockManagement.Infrastructure`: EF Core, repository, Identity, inteqrasiyalar.
- `client`, `mobile`: veb və mobil interfeyslər.
- `tests/StockManagement.Tests`: xUnit testləri; `docs`: mövzu sənədləri.

## Kod stili
- Mövcud faylın stilini saxla; əlaqəsiz refactor və formatlama etmə.
- C#: 4 boşluq, file-scoped namespace, PascalCase tip/metod, camelCase lokal dəyişən.
- Async IO-da `async/await` və `CancellationToken` ötür.
- TypeScript: mövcud 2 boşluq, single quote, React hook qaydalarına əməl et.
- DTO və API davranışı dəyişirsə, frontend və mobil uyğunluğunu yoxla.
- İstifadəçi mətnləri Azərbaycanca, UTF-8 və düzgün hərflərlə olsun.
- UI icazə yoxlaması API authorization-ını əvəz etmir.

## Toxunma və qorunan məlumatlar
- `bin`, `obj`, `dist`, `node_modules`, `.vs`, `.expo` generated fayllarını əl ilə dəyişmə.
- `.env`, secrets və lokal appsettings dəyərlərini oxuyub sənədlərə köçürmə, commit etmə.
- Mövcud migration-ları və model snapshot-ı əl ilə dəyişmə.
- Schema dəyişikliyi zəruridirsə, səbəbi izah et və ayrıca yeni migration hazırla.
- DB update və demo/maintenance yazma əmrlərini sənədləşdirmə yoxlaması üçün işə salma.
- Mövcud istifadəçi dəyişikliklərini silmə; mövcud sənədləri məzmunu qoruyaraq birləşdir.

## Commit və PR
- Commit/PR şablonu və məcburi adlandırma qaydası tapılmayıb: TODO: dəqiqləşdir.
- Tövsiyə: bir məqsədli commit, dəyişiklik və səbəbi göstərən qısa başlıq.
- PR-də problem, nəticə, yoxlama əmrləri, nəticələr və məlum məhdudiyyətləri yaz.
- İstifadəçi istəmədən commit, push və merge etmə.

## Yeni mövzu araşdırılanda RESEARCH.md qaydalarına bax
Əvvəl rəsmi sənədi oxu. Mənbə və yoxlama tarixini yaz.
Əmin olmadığın yerə `TODO: dəqiqləşdir` və ya `yoxlanmalıdır` əlavə et.
Qərar çıxarsa `docs/DECISIONS.md`, iş addımı çıxarsa `PLAN.md` yenilə.
