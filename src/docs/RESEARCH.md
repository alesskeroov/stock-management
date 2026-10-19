# Junior Developer üçün Araşdırma Tapşırıqları

## Render xətaları və ağ ekran

- React render zamanı `null.user` kimi xəta necə yaranır və stack trace-dən səhv sətir necə tapılır?
- React Error Boundary hansı render xətalarını tutur, hansıları tutmur? Event handler və Promise xətaları ayrıca necə işlənir?
- Browser `sessionStorage`-dan parse olunan JSON-u runtime type guard ilə necə yoxlamaq olar? TypeScript cast (`as`) niyə runtime validation etmir?
- React hook qaydaları nədir və şərti `return`-dan sonra hook çağırmaq niyə problem yaradır?
- Vite/React tətbiqində `index.html` mount elementi, modul importları, browser console və source maps necə əlaqələnir?

## Rol əsaslı təhlükəsizlik və UI

- ASP.NET Core `[Authorize(Roles = ...)]` və policy-based authorization necə işləyir?
- Role claim ilə permission claim arasında nə fərq var və policy handler icazəni necə hesablayır?
- UI-da idarəetmə düyməsini gizlətmək niyə API authorization-ı əvəz etmir?
- Admin, Manager və WarehouseKeeper üçün allow/deny cədvəlindən API authorization inteqrasiya testləri necə hazırlanır?
- Frontend və backend permission adlarını OpenAPI və ya ortaq müqavilə vasitəsilə necə sinxron saxlamaq olar?

## UI action və authorization sərhədi

- Niyə “məhsul redaktəsi” və “stok sayımı” fərqli domen əməliyyatları, permission-lar və endpoint-lər olmalıdır?
- React-də ayrı action callback və modal component istifadə etmək səhv route/endpoint çağırılması riskini necə azaldır?
- `[Authorize(Policy = Permissions.InventoryCount)]` qərarı JWT permission claim-lərindən necə çıxarılır və Anbardar rolu bu claim-i necə alır?
- UI-dan düyməni gizlətməkdən əlavə, API endpoint-in 401/403 davranışını necə yoxlamaq olar?
- Stok miqdarı üçün integer və non-negative validation niyə həm frontend, həm backend-də olmalıdır?
- ASP.NET Core-da eyni `[Authorize]` atributunda `Roles` və `Policy` tətbiq olunanda yoxlamalar necə birləşdirilir?
- JWT claim-ləri token yaradıldıqdan sonra niyə dəyişmir və köhnə tokenlə gələn sorğunu role check necə məhdudlaşdırır?
- `RoleManager.RemoveClaimAsync` ilə permission claim-i silmək və tokeni yeniləmək hansı fərqli təsirlərə malikdir?
