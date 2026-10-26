# Arxitektura və API

## Layihənin hissələri

- `App.tsx` — ekran vəziyyətləri, API helper, login/sessiya, məhsul və kateqoriya siyahısı, form və kart komponentləri.
- `app.json` — Expo adı, Android/iOS identifikatorları və SecureStore plugin-i.
- `package.json` — Expo, React Native, SecureStore və əmrlər.
- `.env` — cihazın görə biləcəyi API ünvanı (`.env.example` nümunədir; `.env` Git-ə əlavə etməyin).
- `docs/` — izahlı layihə və öyrənmə qeydləri.

## Sorğu axını

```text
Giriş formu ── POST /api/auth/login ──> access + refresh token
      │                                  │
      └── SecureStore-də sessiya saxla <─┘

App ── Bearer access token ──> /api/products?page=1&size=10
                           ├─> /api/categories
                           └─> məhsul POST/PUT/DELETE (Admin)
```

`api()` helper bütün sorğulara JSON header və sessiya varsa Bearer token əlavə edir. `401` gələndə `/api/auth/refresh-token` çağırıb yeni sessiyanı saxlayır və sorğunu bir dəfə təkrar edir. Login/logout API müqaviləsi `src/StockManagement.Api/Controllers/AuthController.cs`-də, məhsullar `ProductsController.cs`-də, kateqoriyalar `CategoriesController.cs`-də yerləşir.

`ProductPage` cavabı `items`, `currentPage`, `pageSize`, `totalCount`, `totalPages`, `hasNext`, `hasPrevious` sahələrini ehtiva edir. Pagination backend-də işləyir; ekranda hər səhifədə 10 məhsul alınır. Axtarış yüklənmiş cari səhifənin məhsul adı, SKU, barkod və kateqoriya adı üzərində yerli aparılır.

## Yeni API sahəsi əlavə etmək

1. `type` yaradın və API JSON cavabı ilə uyğun sahələr yazın.
2. `api<YourType>('route')` çağırın; `api()` Bearer token və refresh qaydasını tətbiq edir.
3. Nəticəni React state-də saxlayın (`useState`).
4. Ekran komponentində state-i kart və ya form kimi göstərin.
5. Loading, boş nəticə və xəta halını da göstərin.

## Təhlükəsizlik qeydi

Access və refresh token-lər `expo-secure-store` ilə qorunan OS yaddaşında saxlanır. Şifrə persist edilmir. Development API HTTP istifadə edir; production-da HTTPS və məhdud CORS/API giriş konfiqurasiyası tətbiq edin. Token və şəxsi məlumatları log-a yazmayın.
