# Stockroom Mobile

Bu qovluq Stock Management API-yə qoşulan Android/iOS mobil tətbiqidir. Tətbiq Expo və React Native + TypeScript ilə yazılıb. `App.tsx` giriş, JWT sessiyası, məhsul/kateqoriya ekranları, məhsul CRUD formu, axtarış və pagination-ı bir yerdə göstərir.

## Tez başlat

1. Backend-i başladın: repo kökündən `dotnet run --project src/StockManagement.Api`.
2. `mobile` qovluğunda `pnpm install` işlədin.
3. `.env.example` faylını `.env` adı ilə kopyalayın. API ünvanını cihazınıza uyğun seçin.
4. `pnpm start` işlədin, QR kodu Expo Go ilə skan edin və ya Android emulator/iOS simulator seçin.

Expo Go və SDK uyğun gəlmirsə, həmin cihaz üçün Expo development build yaradın. Android emulator API-ni kompüterdən `http://10.0.2.2:5136` ilə görür; iOS simulator `http://localhost:5136` istifadə edir. Fiziki telefonda kompüter və telefon eyni Wi-Fi şəbəkəsində olmalı və `.env` daxilində kompüterin LAN IP-si yazılmalıdır. API firewall-da həmin porta giriş açıq olmalıdır.

Login üçün backend-də yaradılmış istifadəçi məlumatlarından istifadə edin. Standart istifadəçi/şifrə repoda yoxdur; Admin bootstrap məlumatı user-secrets ilə konfiqurasiya edilir. Sessiya tokenləri `expo-secure-store` ilə cihazın təhlükəsiz yaddaşında saxlanır; access token vaxtı bitəndə refresh endpoint-i çağırılır.

## Əmrlər

- `pnpm start` — Expo bundler-i başladır.
- `pnpm android` / `pnpm ios` — uyğun emulator/simulator-da açır.
- `pnpm typecheck` — TypeScript tip yoxlamasıdır.

Ətraflı bələdçilər [docs](./docs/README.md) qovluğundadır.
