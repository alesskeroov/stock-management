# React Native öyrənmə bələdçisi

React Native-də HTML elementlərinin əvəzinə React Native komponentləri işlədilir. Dizayn `StyleSheet` JavaScript obyektləri ilə yazılır; CSS faylı yoxdur.

## Web-dən mobilə uyğunluq

| React web | React Native | Bu tətbiqdə |
| --- | --- | --- |
| `div` | `View` | panel, sətir və konteyner |
| `span`, `p`, `h1` | `Text` | başlıq və mətn |
| `button` | `Pressable` | düymə və tab |
| `input` | `TextInput` | login, axtarış, form sahəsi |
| scroll olan uzun siyahı | `FlatList` | məhsul/kateqoriya siyahısı |
| CSS | `StyleSheet.create` | `styles` obyekti |
| `useState` / `useEffect` | eyni React hook-ları | giriş, yükləmə və ekran state-i |

## `App.tsx`-i oxuma qaydası

1. Əvvəldəki `type`-lar API-dən gələn məlumatın quruluşudur.
2. `API_URL`, `PAGE_SIZE` və `C` konfiqurasiya sabitləridir.
3. `readSession` və `saveSession` tokenləri SecureStore-dan oxuyur/yazır.
4. `App` komponentində `useState` ekrandakı cari məlumatları saxlayır.
5. `api` serverlə əlaqəni və token yeniləməsini idarə edir.
6. `loadData` məhsul səhifəsini və kateqoriyaları backend-dən yükləyir.
7. `LoginScreen`, `ProductCard`, `Pagination`, `ProductFormModal` kiçik UI hissələridir.
8. Faylın sonundakı `styles` cədvəlində komponentlərin rəng, boşluq və ölçüləri verilir.

## Nümunə: düyməyə basanda səhifəni dəyişmək

`Pagination` komponenti əvvəlki/növbəti `Pressable` düymələrini göstərir. Basılanda `onChange(2)` çağrılır. Valideyn bunu `loadData(2)` ilə `/api/products?page=2&size=10` sorğusuna çevirir. Serverin qaytardığı cavab `setPage` və `setProducts` ilə UI state-ə yerləşir; `FlatList` yeni kartları göstərir.

## Dizaynı dəyişmək

Məsələn, əsas yaşıl düymələri dəyişmək üçün `C.green` dəyərini yeniləyin. Təkcə rəngi deyil, `disabled`, loading və error hallarını da yoxlayın. Yeni sətirlərdə `View` istifadə edin, mətnləri `Text` daxilində saxlayın. React Native `Text` xaricində düz mətn child qəbul etmir.
