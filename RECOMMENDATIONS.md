# Tövsiyələr

## İcazələr və UI/API pariteti

- API-də hər yazma əməliyyatını policy və ya rol əsaslı authorization ilə qoruyun; UI yoxlamalarını təhlükəsizlik sərhədi hesab etməyin.
- Frontend rol-icazə xəritəsini API policy-ləri ilə birlikdə nəzərdən keçirin. Mümkün olduqda icazə adlarını ortaq OpenAPI/konfiqurasiya mənbəyindən yaradın ki, dəyişikliklər iki tərəfdə yayınmasın.
- İcazəsiz əməliyyatları gizlədin; yalnız qeyri-mümkün vəziyyətlərdə görünən, deaktiv edilmiş düymə saxlayın. Anbardarın məhsul məlumatlarını deyil, yalnız stok sayını dəyişə bildiyini aydın göstərin.
- UI-dan əlavə, API üçün hər rolun icazəli və rədd edilən əməliyyatlarını yoxlayan inteqrasiya testləri saxlayın.
- Permission yoxlamalarını təkrar inline rol müqayisələri ilə yazmayın; mərkəzi köməkçidən istifadə edin və naməlum rolları standart olaraq rədd edin.
- Frontend icazələri istifadəçi sessiyasındakı rola əsaslanır; rol və permission məlumatlarının token yenilənməsi və logout zamanı yenilənməsini nəzərə alın.
