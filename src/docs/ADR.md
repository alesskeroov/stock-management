# Architecture Decision Records

## ADR-001: Login render zamanı null session-a icazə verilməməsi

### Kontekst

İlk səhifə açılışında session olmadıqda `auth` dəyəri `null` olur. Permission yoxlamaları login üçün şərti renderdən əvvəl `auth!.user` oxuyurdu. Bu, React render zamanı `TypeError` yaradır və login formasının əvəzinə ağ ekran göstərirdi. Session storage-dan oxunan zədələnmiş və ya köhnəlmiş JSON da gözlənilməz user formasına səbəb ola bilər.

### Qərar

Login üçün `if (!auth) return <LoginScreen ... />` şərti permission hesablamalarından əvvəl yerləşdirilir. Session JSON-u `unknown` kimi parse edilir, sessiyanın və user/roles sahələrinin forması yoxlandıqdan sonra qəbul edilir; etibarsız dəyər təmizlənir. Gözlənilməz React render xətaları üçün `AppErrorBoundary` istifadə edilir və istifadəçiyə yeniləmə təlimatı göstərilir.

### Səbəb

Null session autentifikasiya edilməmiş normal vəziyyətdir və login interfeysini bloklamamalıdır. Runtime yoxlaması storage-dan gələn məlumata güvənməməyə kömək edir. Error boundary istisnaları ağ ekran əvəzinə idarə olunan fallback ilə qarşılayır və xəta təfərrüatını konsola yazır.

## ADR-002: Rol əsaslı UI əməliyyatlarının mərkəzi permission xəritəsi

### Kontekst

API `products.manage`, `inventory.count` policy-ləri və Admin rol yoxlaması ilə yazma əməliyyatlarını qoruyur. Əvvəlki UI-da anbardar və menecer üçün bəzi idarəetmə əməliyyatları görünə bilirdi.

### Qərar

React-də `hasPermission` və vahid rol-permission xəritəsi istifadə olunur: Admin məhsul/kateqoriya və işçi idarəetməsi, Anbardar stok sayımı, Menecer oxu səlahiyyətləri alır. Məhsul redaktə və silmə ikonları yalnız `products.manage` səlahiyyəti olan Admin-ə göstərilir. Anbardara ayrıca stok yeniləmə əməliyyatı verilir; bu əməliyyat məhsul redaktəsi kimi işarələnmir. Kateqoriya və işçi əməliyyatları da icazəyə görə render olunur. Submit/delete handler-ləri əlavə yoxlama aparır.

### Səbəb

Mərkəzi xəritə təkrar rol müqayisələrini azaldır və interfeys davranışını API policy-ləri ilə uyğunlaşdırır. UI yoxlaması rahatlıq üçündür; API authorization əsas təhlükəsizlik nəzarəti olaraq qalır.

## ADR-003: Stok yeniləməsini məhsul redaktəsindən ayırmaq

### Kontekst

Anbardarın stok yeniləmə əməliyyatı məhsul redaktə callback-i ilə eyni modalı açırdı. Məhsul sahələri Anbardar üçün read-only olsa da, əməliyyatın görünüşü və davranışı məhsul redaktəsi təsiri bağışlayırdı.

### Qərar

Məhsul cədvəlində Anbardarın stok düyməsi ayrıca `stock` dialog-u və `saveStockQuantity` handler-inə bağlanır. Dialog yalnız yeni miqdarı qəbul edir və yalnız `PUT /api/products/{id}/stock` çağırır. Handler `inventory.count` icazəsini, tam ədəd və sıfırdan kiçik olmayan miqdarı yoxlayır. Məhsul redaktə modalı `products.manage` icazəli istifadəçilər üçün ayrıdır. API endpoint-i `[Authorize(Policy = Permissions.InventoryCount)]` ilə qorunmağa davam edir.

### Səbəb

İki əməliyyatın UI və handler səviyyəsində ayrılması səhv endpoint çağırılmasının, 403 cavabının və səlahiyyətsiz istifadəçiyə redaktə forması göstərilməsinin qarşısını alır. Backend policy UI-dan kənar sorğuları da qoruyur.

## ADR-004: Stok yeniləməsini yalnız Admin-ə vermək

Bu qərar ADR-003-dəki Anbardar üçün stok dialog-u qərarını əvəz edir.

### Kontekst

Anbardar üçün `inventory.count` permission-i frontend rol xəritəsində və Identity role claim-lərində aktiv idi. Bu səbəbdən Anbardar stok yeniləmə UI-sini görür və API policy-sindən keçə bilirdi.

### Qərar

`WarehouseKeeper` frontend permission xəritəsindən və backend `AuthSeeder` rol konfiqurasiyasından `inventory.count` çıxarılır. Seeder role-ın artıq saxlanmış, konfiqurasiyada olmayan permission claim-lərini də silir. Stock endpoint həm `Roles.Admin`, həm `Permissions.InventoryCount` tələb edir. Stok miqdarı validator qaydaları əvvəlki kimi tətbiq olunur.

### Səbəb

UI-da düyməni gizlətmək API qoruması deyil. Role və policy-ni birlikdə yoxlamaq köhnə JWT-də permission claim-i qalmış Anbardarın da endpoint-ə müraciətini bloklayır. Seeder yeni token-lərə yanlış permission claim-in yenidən əlavə olunmasının qarşısını alır.
