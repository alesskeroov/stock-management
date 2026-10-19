# Araşdırma Mövzuları

## Role-based authorization, policy security və UI rendering

- ASP.NET Core-da `[Authorize(Roles = "...")]` ilə `[Authorize(Policy = "...")]` arasındakı fərq nədir və permission claim-lər policy-lərə necə bağlanır?
- ASP.NET Core authorization handler-ləri policy qərarını necə verir? Bir istifadəçinin bir neçə rolu olduqda nəticə necə hesablanır?
- React-də mərkəzi `hasPermission` köməkçisi, Context/Provider və deklarativ şərti renderin üstünlükləri və çatışmazlıqları nələrdir?
- UI-da düyməni gizlətmək və deaktiv etmək istifadəçi təcrübəsi baxımından nə vaxt uyğundur? Bu yanaşma niyə server authorization-ı əvəz etmir?
- JWT-də role/permission claim-ləri nə zaman yenilənir və rol dəyişiklikləri köhnə access token-in qüvvədə olduğu müddətdə necə təsir göstərir?
- Hər rol üçün allow/deny cədvəlindən ASP.NET API inteqrasiya testləri necə qurulur?
- Frontend və backend arasında permission adlarının sinxron qalmasını OpenAPI-dən generasiya və ya ortaq müqavilə ilə necə təmin etmək olar?
