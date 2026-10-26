# İşə salma və yoxlama

## Backend

Repo kökündən:

```powershell
dotnet run --project src/StockManagement.Api
```

Hazırkı HTTP launch profile `http://localhost:5136` ünvanından dinləyir. Swagger `http://localhost:5136/swagger` ünvanındadır. Backend DB bağlantısını və login üçün Admin/istifadəçi hesabını ayrıca konfiqurasiya etmək lazımdır; `README.md` faylındakı user-secrets təlimatına baxın.

## Expo

`mobile` qovluğunda:

```powershell
Copy-Item .env.example .env
pnpm install
pnpm start
```

`pnpm start` terminalında `a` basmaq Android emulator-u açır; QR kodu Expo Go ilə oxutmaq fiziki cihazda tətbiqi işə salır. Cihazın API ünvanını dəyişmək üçün `.env` faylındakı `EXPO_PUBLIC_API_URL` dəyərini dəyişib Expo serverini yenidən başladın.

## API ünvanı

| İcra mühiti | API URL |
| --- | --- |
| Android emulator | `http://10.0.2.2:5136` |
| iOS Simulator | `http://localhost:5136` |
| Fiziki telefon | `http://<kompüterin-LAN-IP>:5136` |

Telefon və kompüter eyni şəbəkədə olmalıdır. Fiziki telefon üçün server kompüterin LAN interfeysində də dinləməlidir; məsələn `dotnet run --project src/StockManagement.Api --urls http://0.0.0.0:5136` işlədin və firewall-da porta girişə icazə verin. Mobil tətbiq native fetch istifadə etdiyindən brauzer CORS siyasəti tətbiq edilmir.

## Yoxlama ssenariləri

1. Backend və Expo-nu başladın, etibarlı hesabla daxil olun.
2. Məhsul/kateqoriya tablarını yoxlayın; məhsul kartında qiymət, stok və status görünməlidir.
3. 10-dan çox məhsulla əvvəlki/növbəti pagination-ı; son səhifədə disabled vəziyyəti yoxlayın.
4. Ad, SKU, barkod və kateqoriya ilə cari səhifədə axtarın.
5. Admin hesabında yeni məhsul yaradın, redaktə edin və silin. Dəyişikliklər backend-ə `POST`, `PUT`, `DELETE /api/products` ilə yazılır.
6. Token müddəti bitəndə refresh, logout və yanlış şifrə üçün xəta mesajını yoxlayın.
7. `pnpm typecheck` işlədib TypeScript səhvlərini yoxlayın.

Paketləri quraşdırıb build-i yoxlamaq üçün işə salmanı repo root-dan öz kompüterinizdə icra edin.
