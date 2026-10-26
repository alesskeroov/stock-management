# Dizayn bələdçisi

Mobil UI web frontend-in Stockroom görünüşünü kiçik ekrana uyğunlaşdırır. Rəng palitrası `client/src/App.css`-dəki rənglərə əsaslanır; mobil tətbiqdə vahid `C` obyektində saxlanıb.

## Vizual dil

| İstifadə | Rəng |
| --- | --- |
| Tünd yaşıl başlıq və brend | `#153c30` |
| Əsas düymələr və aktiv state | `#287653` |
| Lime vurğu/loqo | `#c5f09a` |
| Səhifə fonu | `#f5f7f5` |
| Panel/kart | `#ffffff` |
| Xətt/ayırıcı | `#e7ede9` |
| Yumşaq yaşıl badge | `#eaf6ef` |

Kodda `C` rəng sabiti `mobile/App.tsx` faylının yuxarısındadır. Məsələn, `C.green` bir əsas yaşıl rəngi dəyişir. Dəyəri web CSS dəyişənləri ilə uyğun saxlayın.

## Ekran quruluşu

- **Giriş:** tünd yaşıl fon, mərkəzdə Stockroom loqosu və ağ kart formasında login.
- **Başlıq:** tünd yaşıl brend zolağı, istifadəçi baş hərfləri və çıxış düyməsi.
- **Xülasə:** kataloq üzrə məhsul sayı və cari səhifə.
- **Bölmələr:** Məhsullar və Kateqoriyalar tabları, web frontend-in tab stilinə uyğun.
- **Məhsul:** ağ kart, vəziyyət nişanı, qiymət/stok/barkod və Admin üçün redaktə/silmə düymələri.
- **Pagination:** “məhsul sayından aralıq” məlumatı və əvvəlki/növbəti oxları.
- **Forma:** tam ekran sheet, mobilə uyğun sahələr, seçim çipləri və aşağıda ləğv/yadda saxla düymələri.

## Mobilə uyğunlaşdırma

Web-dəki geniş cədvəl mobildə kart siyahısına çevrilib. Yan menyu alt tablarla əvəz olunub, alətlər dikey düzülür, bütün toxunma sahələri rahat basılacaq hündürlükdədir. `SafeAreaView`, `FlatList`, `ScrollView` və `KeyboardAvoidingView` ekran kənarları, uzun siyahılar və klaviatura üçün istifadə olunur.
