# Architecture Decision Records

## ADR-001: Mərkəzi frontend icazə yoxlaması və UI pariteti

### Kontekst

API məhsul və kateqoriya yazma əməliyyatlarını `products.manage`, stok sayımını `inventory.count`, işçi qeydiyyatını isə Admin rolu ilə qoruyur. Əvvəlki React interfeysində kateqoriya yaratma/redaktə/silmə əməliyyatları, məhsul sətirlərinin idarəetmə düymələri və stokla bağlı formalar rollardan asılı olmayaraq görünə bilirdi.

### Qərar

Frontend-də `hasPermission` köməkçisi və rol-icazə xəritəsi saxlanılır. API-dəki icazələrə uyğun olaraq Admin məhsul/kateqoriya idarəetməsi, stok sayımı və işçi idarəetməsi; Anbardar yalnız stok sayımı; Menecer isə oxu icazəsi alır. İcazəsiz əməliyyat düymələri render edilmir. Anbardar üçün məhsul redaktə düyməsi stok yeniləmə kimi göstərilir və yalnız həmin əməliyyat təqdim olunur. API authorization əsas təhlükəsizlik sərhədi olaraq qalır.

### Səbəb

UI və API davranışını uyğunlaşdırmaq istifadəçini 403 xətaları ilə çaşdırmaqdan qoruyur. Mərkəzi rol-icazə xəritəsi frontend şərtlərində təkrarı azaldır; server tərəfindəki yoxlamalar isə UI manipulyasiyasına baxmayaraq məlumatı qoruyur.
