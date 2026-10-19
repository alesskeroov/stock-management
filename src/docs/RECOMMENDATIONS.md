# Tövsiyələr

## Render sabitliyi və ağ ekranların qarşısı

- `null` və yüklənmə vəziyyətlərini renderdən əvvəl idarə edin; şərti qaytarışdan öncə nullable state-in iç sahələrini oxumayın.
- `sessionStorage` və `localStorage` məlumatlarını etibarlı hesab etməyin. JSON-u `unknown` kimi parse edib shape/type yoxlaması aparın; etibarsız sessiyanı silin.
- React render ağacını Error Boundary ilə qoruyun. Fallback istifadəçiyə növbəti addımı göstərməli, texniki xəta təfərrüatı isə konsola/log sisteminə yazılmalıdır.
- Error Boundary event handler və async xətalarını tutmur; bu xətaları ayrıca `try/catch` və görünən xəta vəziyyəti ilə idarə edin.
- React hook-larını şərti return-lardan əvvəl və hər renderdə eyni ardıcıllıqla çağırın.
- `index.html`-də mount elementinin mövcudluğunu yoxlayın və import/build xətalarını brauzer konsolunda araşdırın.

## Rol və icazə pariteti

- Bütün qorunan yazma əməliyyatlarında server-side policy/role authorization saxlayın; frontend render şərtləri təhlükəsizlik sərhədi deyil.
- UI permission xəritəsini API policy-ləri ilə eyni mənbədən və ya nəzərdən keçirilən müqavilədən sinxron saxlayın.
- İcazəsiz əməliyyatları vizual olaraq gizlədin; submit və handler səviyyəsində də icazəni yoxlayın.
- Məhsulun kataloq məlumatlarını redaktə etmək və stok sayını yeniləmək fərqli icazələrdirsə, ayrı düymə/etiket istifadə edin. Anbardara redaktə və silmə ikonlarını göstərməyin.
- UI-da düymənin kimə göründüyünü API endpoint-in authorization policy-si ilə dəqiq tutuşdurun; Manager rolunun adından məhsul idarəetmə icazəsi çıxarmayın.
- Hər rol üçün icazəli və rədd edilən API davranışını inteqrasiya testləri ilə yoxlayın, UI-da isə rol dəyişikliklərinə görə görünən əməliyyatları yoxlayın.
- Naməlum rollar üçün standart davranış icazəni rədd etmək olmalıdır.
- Fərqli permission tələb edən UI əməliyyatlarını eyni callback və ya modalda birləşdirməyin. Stok yeniləmə öz dialog-u və yalnız stock endpoint-ə gedən handler istifadə etməlidir.
- API controller-də stok endpoint-inə `inventory.count` policy tətbiq edin və validator ilə miqdarın tam ədəd, qeyri-mənfi və limit daxilində olduğunu yoxlayın.
- UI rol yoxlamasını istifadəçi təcrübəsi üçün saxlayın; icazənin son qərarını backend policy versin.
- Əməliyyat yalnız konkret rola aiddirsə, backend endpoint-də həmin rolu yoxlayın; köhnə JWT permission claim-ləri müddəti bitənədək qala bilər.
- Role claim-ləri dəyişdirən seeder-lər yalnız yeni claim əlavə etməməli, artıq konfiqurasiyaya uyğun olmayan icazələri də silib nəticəni yoxlamalıdır.
- Rol icazəsi dəyişəndə həm yeni tokenləri, həm mövcud tokenin qüvvədə qalma pəncərəsini nəzərə alın.
