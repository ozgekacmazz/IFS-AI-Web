# Mimari Kararlar

Bu belge başlangıç kararlarının güncel uygulamayla nasıl evrildiğini kaydeder. Kaynak kodu ve otomatik testler uygulanmış mimari davranış için esas kanıttır; bu belge çalışma zamanı, temiz ortam, gerçek Groq veya dağıtım doğrulaması yapılmış gibi yorumlanmamalıdır.

## 1. Karar sınıfları

- **[PDF gereksinimi]:** PDF'nin açıkça istediği davranış veya kısıt.
- **[Teknik karar]:** Ekibin seçtiği uygulama yaklaşımı; PDF zorunluluğu değildir.
- **[Evrilmiş karar]:** İlk kararın uygulama sırasında daha kesin veya farklı bir biçime dönüşmüş hali.
- **[Açık/ertelenmiş]:** Henüz uygulanmamış işletim, ölçekleme veya sertleştirme konusu.

PDF backend, frontend, veritabanı ve LLM sağlayıcısını yazılımcıya bırakır. Bu nedenle ASP.NET Core, React, PostgreSQL ve Clean Architecture seçimleri PDF gereksinimi olarak sunulmaz.

## 2. Kararlar

### ADR-001 - Backend: ASP.NET Core 10

- **Karar:** **[Teknik karar]** API, ASP.NET Core 10 hedefleyen Minimal API uygulamasıdır.
- **Bağlam ve gerekçe:** Yerleşik DI, JWT kimlik doğrulama, politika tabanlı yetkilendirme, rate limiting, Problem Details ve test sunucusu desteği projenin güvenlik ve test ihtiyaçlarına uygundur.
- **Sonuçlar ve ödünleşimler:** Modern platform özellikleri ve güçlü tip güvenliği sağlanır; çalışma ve dağıtım ortamı .NET 10 desteklemelidir.
- **Uygulama kanıtı:** `backend/src/IFS.AIWeb.Api/IFS.AIWeb.Api.csproj`, `backend/src/IFS.AIWeb.Api/Program.cs`, `backend/IFS.AIWeb.slnx`.
- **Mevcut durum:** Uygulandı. Bu senkronizasyonda runtime doğrulaması yapılmadı.

### ADR-002 - Frontend: React ve TypeScript

- **Karar:** **[Teknik karar]** İstemci, backend'den ayrı Vite tabanlı React 19 ve TypeScript uygulamasıdır; ek bir global state veya tasarım sistemi bağımlılığı kullanılmaz.
- **Bağlam ve gerekçe:** Form ağırlıklı kullanıcı/Admin ekranları, bileşen tekrar kullanımı ve tipli istemci modelleri için uygundur.
- **Sonuçlar ve ödünleşimler:** Ayrı frontend derleme zinciri vardır. Route guard'lar kullanıcı deneyimini iyileştirir; güvenlik sınırı değildir, yetkilendirme API'de uygulanır.
- **Uygulama kanıtı:** `frontend/package.json`, `frontend/src/App.tsx`, `frontend/src/auth/Auth.tsx`, `frontend/src/pages/AppPage.tsx`, `frontend/src/admin/`.
- **Mevcut durum:** Uygulandı.

### ADR-003 - Veritabanı: PostgreSQL ve EF Core

- **Karar:** **[Teknik karar]** Kullanıcılar, refresh token kayıtları ve özet işlem kayıtları PostgreSQL'de EF Core ile tutulur.
- **Bağlam ve gerekçe:** Rol, kullanıcı, token ailesi ve özet kayıtları ilişkisel bütünlük, indeks ve işlemsel güncelleme gerektirir.
- **Sonuçlar ve ödünleşimler:** PostgreSQL kurulumu ve migration çalıştırılması gerekir. `users`, `refresh_tokens` ve `summary_records` tablolarında yabancı anahtarlar, benzersiz kullanıcı/token hash indeksleri ve geçmiş sorgularını destekleyen indeksler vardır. Refresh rotasyonu ile kritik Admin durum/parola değişiklikleri işlem içinde yürütülür; son aktif Admin kontrolü PostgreSQL advisory lock ve satır kilidi kullanır.
- **Uygulama kanıtı:** `backend/src/IFS.AIWeb.Infrastructure/AuthDbContext.cs`, `AuthInfrastructure.cs`, `AdminInfrastructure.cs`, `Migrations/20260802110648_InitialAuthentication.cs`, `Migrations/20260802121509_AddSummarizationRecords.cs`.
- **Mevcut durum:** Uygulandı. Gerçek PostgreSQL/temiz ortam migration doğrulaması bu belge güncellemesinin dışındadır.

### ADR-004 - Clean Architecture ve katmanlar

- **Karar:** **[Teknik karar]** Backend dört proje sınırı kullanır: Domain, Application, Infrastructure ve API. Frontend ayrı bir uygulamadır.
- **Bağlam ve gerekçe:** LLM, PostgreSQL, parola ve token altyapısını kullanım senaryolarından ayırmak; iş kurallarını test edilebilir tutmak.
- **Sonuçlar ve ödünleşimler:** `Domain` başka proje referansı içermez; `Application -> Domain`; `Infrastructure -> Application, Domain`; composition root olan `API -> Application, Infrastructure` yönündedir. LLM, saat, kalıcılık, parola ve token sınırları Application arayüzleriyle tersine çevrilir. API'nin Infrastructure referansı yalnız bileşim içindir. Mikroservis, event bus, CQRS framework veya generic repository eklenmemiştir.
- **Uygulama kanıtı:** Dört `backend/src/*/*.csproj` proje referansı; `backend/src/IFS.AIWeb.Application/AuthContracts.cs`, `Summarization.cs`; `backend/src/IFS.AIWeb.Infrastructure/DependencyInjection.cs`; `backend/src/IFS.AIWeb.Api/Program.cs`.
- **Mevcut durum:** Uygulandı.

### ADR-005 - SOLID ve Dependency Injection

- **Karar:** **[Teknik karar]** Yalnız gerçek dış sınırlar küçük arayüzlerle tanımlanır ve bağımlılıklar constructor injection ile verilir.
- **Bağlam ve gerekçe:** LLM sağlayıcısı, saat, token üretimi ve kalıcılık test doubles ile değiştirilebilmelidir.
- **Sonuçlar ve ödünleşimler:** Application kullanım senaryoları altyapı ayrıntılarını bilmez; anlamsız arayüz ve katman çoğalması önlenir.
- **Uygulama kanıtı:** `ILlmSummarizer`, `ISummaryRepository`, `IClock`, `IUserRepository`, `IRefreshTokenRepository`, `IPasswordService`, `IAccessTokenService` ve kayıtları `backend/src/IFS.AIWeb.Infrastructure/DependencyInjection.cs` içindedir.
- **Mevcut durum:** Uygulandı.

### ADR-006 - Değiştirilebilir LLM sağlayıcı soyutlaması ve hata sınırı

- **Karar:** **[Teknik karar]** Application sağlayıcıdan bağımsız `ILlmSummarizer` portunu kullanır; Infrastructure bu portu Groq Chat Completions HTTP API ile uygular.
- **Bağlam ve gerekçe:** Sağlayıcı PDF tarafından serbest bırakılmıştır. İş kuralları Groq taşıma biçiminden bağımsız kalmalı, fakat ilk sürüm tek aktif sağlayıcıyla basit tutulmalıdır.
- **Evrim:** İlk karardaki “otomatik retry yoktur” ifadesi artık geçerli değildir. Genel bir resilience politikası eklenmemiş; ADR-009'da tanımlanan dar kapsamlı, tek kontrollü retry uygulanmıştır.
- **Sonuçlar ve ödünleşimler:** Groq base URL, model ve timeout yapılandırılabilir; varsayılan model `openai/gpt-oss-120b`, timeout 30 saniye, çıktı sınırı 500 completion token'dır. İstemci iptali yayılır; timeout, ağ ve sağlayıcı hataları güvenli türlere çevrilir. Veri işleme bölgesi, üretim kotası/maliyeti ve gerçek sağlayıcı davranışı işletim doğrulaması gerektirir.
- **Uygulama kanıtı:** `backend/src/IFS.AIWeb.Application/Summarization.cs` (`ILlmSummarizer`, hata türleri), `backend/src/IFS.AIWeb.Infrastructure/SummarizationInfrastructure.cs` (`GroqOptions`, `GroqSummarizer`), `DependencyInjection.cs`, `backend/src/IFS.AIWeb.Api/appsettings.json`, `backend/tests/IFS.AIWeb.IntegrationTests/GroqProviderTests.cs`.
- **Mevcut durum:** Uygulandı; gerçek Groq çağrısı bu görevde yapılmadı.

### ADR-007 - Access ve refresh token oturumu

- **Karar:** **[Teknik karar]** 15 dakikalık imzalı JWT access token istemci belleğinde tutulur. 7 günlük refresh token kriptografik rastgele üretilir, yalnız SHA-256 hash'i PostgreSQL'de saklanır ve `HttpOnly`, `SameSite=Strict`, `/api/auth` path cookie ile taşınır; üretimde cookie `Secure` olur.
- **Bağlam ve gerekçe:** HttpOnly cookie refresh token'ın JavaScript tarafından okunmasını, hash saklama ise veritabanı sızıntısında düz token'ın elde edilmesini önler. Refresh ve logout isteklerinde izinli Origin doğrulanır.
- **Evrim:** Rotasyon, iptal ve token-family reuse detection ertelenmemiştir. Her yenileme aynı ailede yeni token oluşturur; döndürülmüş/iptal edilmiş token tekrar sunulursa ailedeki etkin token'lar iptal edilir. Eşzamanlı yenileme veritabanı satır kilidi ve işlem ile sınırlandırılır. Pasife alma ve Admin parola sıfırlama refresh oturumlarını iptal eder.
- **Yetkilendirme:** JWT issuer, audience, imza ve süre doğrulanır. Varsayılan politika veritabanından etkin kullanıcıyı, `AdminOnly` ayrıca `Admin` rolünü gerektirir. React `ProtectedRoute` ve `AdminRoute` yalnız UX katmanıdır; API politikaları nihai sınırdır.
- **Bilinen sınırlılık:** Merkezi access-token deny-list/security-stamp yoktur. Pasif kullanıcı aktif-kullanıcı kontrolüyle hemen engellenir; bunun dışındaki önceden verilmiş access token'lar süreleri dolana kadar kriptografik olarak geçerlidir. Parola değişikliği tek başına verilmiş access token'ı geri çağırmaz.
- **Uygulama kanıtı:** `backend/src/IFS.AIWeb.Application/AuthService.cs`, `AuthContracts.cs`; `backend/src/IFS.AIWeb.Infrastructure/AuthInfrastructure.cs`, `AuthDbContext.cs`; `backend/src/IFS.AIWeb.Api/Program.cs`; `frontend/src/auth/Auth.tsx`; `AuthServiceTests.cs`, `AuthApiTests.cs`, `AdminApiTests.cs`.
- **Mevcut durum:** Uygulandı.

### ADR-008 - Kontrollü prompt, güvenlik ve sürümleme

- **Karar:** **[Teknik karar]** Merkezi `SummarizationPromptBuilder`, sistem talimatını kullanıcı içeriğinden ayırır; kaynak metni `<source_text>` sınırları içine alır ve her kayda prompt sürümü ekler. Geçerli çıktı dilleri Turkish ve English'dir.
- **Bağlam ve gerekçe:** Kaynak metin güvenilmeyen veridir. Model kaynak içindeki talimatları izlememeli, olmayan bilgi üretmemeli ve anlamlı önerme yoksa güvenli biçimde kapanmalıdır.
- **Evrim:** Güncel sürüm `summary-v3`'tür. Sistem talimatı `sufficient`/`insufficient` ölçütlerini, yetersiz içerikte boş özet, yeterli içerikte yalnız kaynak olguları ve seçilen dil kurallarını tanımlar. Önceki prompt sürümleri tarihsel kayıtlarda korunabilir.
- **Sonuçlar ve ödünleşimler:** Prompt injection riski azaltılır fakat semantik doğruluk garanti edilmez. Backend en çok 12.000 karakter, en az bir Unicode harf ve iki harf içeren token gibi deterministik yapısal kurallar uygular; yalnız noktalama/sayı, tek kelime ve uzun tek-karakter tekrarlarını reddeder. Çok kelimeli içeriğin anlamsal anlamlılığını ek bir AI çağrısı olmadan kesin bildiğini iddia etmez.
- **Uygulama kanıtı:** `backend/src/IFS.AIWeb.Application/Summarization.cs` (`SummarizationPromptBuilder`, `Validate`), `SummarizationTests.cs`, `GroqProviderTests.cs`; frontend kolaylık doğrulaması `frontend/src/pages/AppPage.tsx`.
- **Mevcut durum:** Uygulandı; prompt kalite değerlendirmesi ve gerçek sağlayıcı doğrulaması ayrıca gereklidir.

### ADR-009 - Strict structured output, yerel doğrulama ve kontrollü retry

- **Karar:** **[Evrilmiş teknik karar]** Groq isteği API düzeyinde `response_format.type=json_schema`, `strict=true` kullanır. Şema tam olarak zorunlu `quality` (`sufficient|insufficient`) ve `summary` string alanlarını kabul eder; ek alanları reddeder. Provider sözleşmesi doğrudan UI sözleşmesi değildir: Infrastructure JSON'u ayrıştırıp şemayı, Application da yeterli sonuçta boş olmayan özeti doğrular.
- **Retry kararı:** En çok iki sağlayıcı denemesi vardır. Yalnız ilk yanıt HTTP 400 olduğunda ve sınırlı (en çok 16 KiB) güvenli hata metadatası bunun yapılandırılmış çıktı üretim hatası olduğunu pozitif olarak kanıtladığında bir kez yeniden denenir. Kanıt; `invalid_request_error` ile `failed_generation`, belgelenmiş tam schema-mismatch mesajı veya güvenli `json_validate_failed` / `structured_output_validation_failed` kodlarından biridir.
- **Retry dışı durumlar:** Olağan 400, 401/403, sağlayıcı 429, 5xx, timeout, ağ hatası, caller cancellation, taşıma JSON parse hatası, content JSON parse hatası, eksik/ek alan, geçersiz enum, boş yeterli özet ve dolu yetersiz özet yeniden denenmez. Dolayısıyla bu genel amaçlı retry/backoff değildir.
- **Sonuçlar ve ödünleşimler:** Geçici ve kanıtlanmış model schema-generation hatası bir ek denemeyle toparlanabilir; maliyet ve çift çağrı yalnız bu sınıfa sınırlandırılır. `insufficient` güvenli 422 ürün sonucuna, geçersiz sağlayıcı çıktısı güvenli sağlayıcı hatasına dönüşür.
- **Uygulama kanıtı:** `backend/src/IFS.AIWeb.Infrastructure/SummarizationInfrastructure.cs` (`CreateRequest`, `SafeErrorMetadataAsync`, iki denemeli döngü), `backend/src/IFS.AIWeb.Application/Summarization.cs`; `backend/tests/IFS.AIWeb.IntegrationTests/GroqProviderTests.cs`, `SummarizationTests.cs`, `SummaryApiTests.cs`.
- **Mevcut durum:** Uygulandı.

### ADR-010 - Kullanıcı başına Sliding Window rate limiting

- **Karar:** **[Evrilmiş teknik karar]** Yalnız `POST /api/summaries`, doğrulanmış JWT `sub` değerine göre bölümlenen, uygulama belleğindeki Sliding Window limiter ile korunur.
- **Evrim:** Önceki “sabit bir dakikalık pencere” ifadesi yanlıştı. Güncel politika 1 dakikalık pencere, 60 segment, 5 permit, kuyruk kapasitesi 0 ve otomatik yenilemedir. Aynı kullanıcının ilk beş POST isteği kabul edilir; altıncı istek kapasite açılana kadar reddedilir. `GET /api/summaries/recent` ve detay okumaları permit tüketmez.
- **Red cevabı:** 429 Problem Details, lease'ten yukarı yuvarlanan pozitif `Retry-After` header'ı ve `retryAfterSeconds` alanı döner. Frontend güvenli Türkçe mesaj ve geri sayım gösterir, metni korur ve süre boyunca gönderimi kapatır.
- **Sonuçlar ve ödünleşimler:** Maliyet/kötüye kullanım sınırı basittir. Sayaç backend yeniden başladığında sıfırlanır ve ayrı instance'lar sayaç paylaşmaz; yatay ölçeklemede dağıtık limiter gerekir. Rate-limit logundaki kullanıcı bölümü ham `sub` değil, kısaltılmış SHA-256 değeridir.
- **Uygulama kanıtı:** `backend/src/IFS.AIWeb.Api/Program.cs` (`SummaryRateLimitPolicy`, `SummaryPerUser`), `RateLimitPolicyTests.cs`, `RateLimitPipelineTests.cs`, `SummaryApiTests.cs`; `frontend/src/pages/AppPage.tsx`, `frontend/src/Summarization.test.tsx`.
- **Mevcut durum:** Uygulandı; çoklu instance dağıtık sayaç ertelendi.

### ADR-011 - İçerik kalıcılığı, log maskeleme ve mahremiyet

- **Karar:** **[Evrilmiş teknik karar]** Başarılı işlemde özgün girdi ve tam özet, dil, sağlayıcı/model, prompt sürümü, süre, karakter sayıları, durum ve oluşturma/sona erme zamanı ile saklanır. Kayıt için `ExpiresAtUtc = CreatedAtUtc + 30 gün` uygulanır. Başarısız veya `insufficient` işlemde kaynak ve özet saklanmaz; yalnız güvenli hata/audit metadatası tutulur.
- **Okuma sınırları:** Kullanıcı son yedi başarılı, kendisine ait ve süresi dolmamış özeti görür; detay endpoint'i de aynı sahiplik/başarı/süre koşullarını uygular. Admin logları sayfalıdır. Başarılı ve süresi dolmamış içerikten repository en çok 512 karakter projekte eder; Application beyaz alanı normalize edip en çok 160 Unicode text element ve üç nokta döndürür. Başarısız ve süresi dolmuş kayıtta önizleme yoktur. Admin API tam kaynak veya tam özeti döndürmez.
- **Operasyonel loglar:** Kaynak, özet, token, credential ve ham provider response body yazılmaz. Provider hatasında yalnız güvenli kategori/status, sınırlandırılmış type/code, retry/attempt, süre, prompt sürümü, trace ve doğrulanmış provider request ID tutulur.
- **Bilinen sınırlılık:** 30 günlük süre erişim sorgularında uygulanır; fiziksel kayıtları zamanlanmış silen cleanup worker yoktur. Bu nedenle “30 gün saklama” erişilebilirlik süresidir, fiziksel silme garantisi değildir.
- **Uygulama kanıtı:** `backend/src/IFS.AIWeb.Domain/AuthModels.cs` (`SummaryRecord`), `backend/src/IFS.AIWeb.Application/Summarization.cs`, `AdminAdministration.cs`; `backend/src/IFS.AIWeb.Infrastructure/SummarizationInfrastructure.cs`, `AdminInfrastructure.cs`, `AuthDbContext.cs`; `SummaryApiTests.cs`, `AdminApiTests.cs`, `GroqProviderTests.cs`.
- **Mevcut durum:** Erişim ve maskeleme uygulandı; fiziksel cleanup ertelendi.

### ADR-012 - Merkezi ve güvenli hata yönetimi

- **Karar:** **[Teknik karar]** API merkezi exception handler ile tutarlı Problem Details cevapları üretir; iç exception, sağlayıcı cevabı ve hassas veri istemciye sızdırılmaz.
- **Bağlam ve gerekçe:** Doğrulama, kimlik, yetki, yetersiz içerik, provider, timeout, request-size ve rate-limit hatalarının kullanıcıya yalın Türkçe karşılıkları gerekir.
- **Sonuçlar ve ödünleşimler:** İstemci 400/413/422/429/502 gibi durumları güvenli ürün mesajlarına eşler. Provider 429 dış istemciye 503, timeout 504, diğer özetleme başarısızlıkları 502 olarak çevrilir; uygulamanın kendi limiter 429'u ayrı kalır. Beklenmeyen hatada güvenli exception type/path/trace kaydı tutulur, ham hata cevabı dönülmez.
- **Uygulama kanıtı:** `backend/src/IFS.AIWeb.Api/Program.cs` (`WriteError`, limiter rejection); `frontend/src/pages/AppPage.tsx` (`summaryRequestFailure`); `AuthApiTests.cs`, `SummaryApiTests.cs`, `Summarization.test.tsx`.
- **Mevcut durum:** Uygulandı.

### ADR-013 - Yetkili backend doğrulaması ve istemci kolaylığı

- **Karar:** **[Teknik karar]** Frontend hızlı ve erişilebilir geri bildirim sağlar; API/Application doğrulaması bütün istemciler için yetkili kaynaktır. Veritabanı uzunluk, zorunluluk, ilişki ve benzersizlik kısıtları son savunma katmanıdır.
- **Bağlam ve gerekçe:** İstemci kontrolleri atlanabilir. Kimlik alanları, parola, özet metni, izinli dil/rol/status değerleri ve AI şeması sunucuda tekrar doğrulanmalıdır.
- **Sonuçlar ve ödünleşimler:** UX için sınırlı kural tekrarı vardır; frontend sunucu Problem Details içindeki yalnız beklenen, sınırlandırılmış alan mesajlarını gösterir. Özet isteği 12.000 Unicode/emoji karakter sözleşmesini taşıyabilmek için 128 KiB body sınırına sahiptir; uygulama karakter sınırı değişmez.
- **Uygulama kanıtı:** `backend/src/IFS.AIWeb.Application/AuthService.cs`, `Summarization.cs`, `AdminAdministration.cs`; `backend/src/IFS.AIWeb.Api/Program.cs`; `frontend/src/auth/Auth.tsx`, `frontend/src/pages/AppPage.tsx`; ilgili Application ve API testleri.
- **Mevcut durum:** Uygulandı.

### ADR-014 - Yapılandırma ve secret yönetimi

- **Karar:** **[PDF gereksinimi + teknik karar]** Secret değerleri kaynak koda/commit'e girmez. Yerelde .NET User Secrets veya environment, dağıtımda platform secret store kullanılmalıdır. İzlenen yapılandırma yalnız anahtar adları, boş secret alanları ve güvenli varsayılanlar içerir.
- **Bağlam ve gerekçe:** Groq API anahtarı, JWT imzalama anahtarı ve PostgreSQL bağlantı bilgisi credential niteliğindedir.
- **Sonuçlar ve ödünleşimler:** Üretim dışı Testing ortamı haricinde kısa JWT anahtarı veya boş Groq anahtarı başlangıcı durdurur; PostgreSQL bağlantısı ve Groq option aralıkları DI kaydında doğrulanır. Sağlayıcı Authorization header'ı yalnız dış isteğe eklenir ve loglanmaz. Dağıtım secret platformu repo kapsamında seçilmemiştir.
- **Uygulama kanıtı:** `backend/src/IFS.AIWeb.Api/Program.cs`, `appsettings.json`; `backend/src/IFS.AIWeb.Infrastructure/DependencyInjection.cs`, `SummarizationInfrastructure.cs`; `GroqProviderTests.cs`.
- **Mevcut durum:** Kod ve izlenen yapılandırma uygulandı; gerçek secret değerleri incelenmedi ve dağıtım platformu açıktır.

### ADR-015 - Test stratejisi

- **Karar:** **[Teknik karar]** Domain/Application birim testleri, sahte HTTP/provider kullanan sağlayıcı ve API testleri, gerçek PostgreSQL için Testcontainers entegrasyon testleri ve React Testing Library bileşen/akış testleri birlikte kullanılır. Normal deterministik testler gerçek Groq'a çağrı yapmaz.
- **Bağlam ve gerekçe:** Rol sınırı, token rotasyonu/reuse, schema sözleşmesi, dar retry, sliding-window pipeline, kalıcılık ve istemci hata davranışı farklı test seviyeleri gerektirir.
- **Sonuçlar ve ödünleşimler:** Hızlı kaynak-içi testler geniş hata dallarını kapsar; Testcontainers Docker gerektirir. Canlı LLM kalite/smoke testleri maliyetli ve değişken olduğundan ayrı kontrollü doğrulama olmalıdır. Kritik tarayıcı akışları manuel doğrulanmış olabilir, fakat repo içinde tam bir E2E/CI kanıtı bulunmaz.
- **Uygulama kanıtı:** `backend/tests/IFS.AIWeb.Domain.Tests/`, `IFS.AIWeb.Application.Tests/`, `IFS.AIWeb.IntegrationTests/`; `frontend/src/*.test.tsx`, `frontend/src/test/`.
- **Mevcut durum:** Test katmanları uygulanmıştır; CI/CD, temiz ortam, Docker-backed PostgreSQL, gerçek Groq ve kapsamlı tarayıcı doğrulaması bu senkronizasyonda çalıştırılmadı.

### ADR-016 - Admin denetlenebilirliği ve yönetim sınırları

- **Karar:** **[Evrilmiş teknik karar]** `/api/admin` grubu `AdminOnly` politikasıyla korunur. Admin; kullanıcıları arayıp filtreleyebilir, kullanıcı oluşturabilir, etkinliği değiştirebilir, parola sıfırlayabilir, özet loglarını filtreleyip sayfalayabilir, prompt bilgisini ve yedi günlük istatistikleri görebilir.
- **Bağlam ve gerekçe:** PDF Admin logu ve rol ayrımını ister. Operasyonel inceleme, kullanıcı içeriğini gereksiz yere açmadan yapılmalıdır.
- **Güvenlik ve mahremiyet:** Kendi hesabını pasife alma ve son aktif Admin'i pasife alma reddedilir. Pasife alma/parola sıfırlama refresh oturumlarını iptal eder. Admin logu ADR-011'deki bounded preview ve güvenli metadata ile sınırlıdır; tam AI içeriği veya provider body içermez. React Admin guard'ı UX sağlar, API rol ve aktif-kullanıcı kontrolü yetkili sınırdır.
- **Evrim ve sınırlılık:** Özet işlem denetimi `summary_records` ile uygulanmıştır. Ancak yönetici eylemlerini aktör/zaman/önce-sonra bilgisiyle kalıcılaştıran ayrı bir admin-action audit tablosu veya event store yoktur; ilk ADR'deki bu hedef ertelenmiş hardening olarak kalır.
- **Uygulama kanıtı:** `backend/src/IFS.AIWeb.Api/AdminEndpoints.cs`, `Program.cs`; `backend/src/IFS.AIWeb.Application/AdminAdministration.cs`; `backend/src/IFS.AIWeb.Infrastructure/AdminInfrastructure.cs`; `frontend/src/admin/`, `frontend/src/App.tsx`; `AdminApiTests.cs`, `AdminFrontend.test.tsx`.
- **Mevcut durum:** Admin yönetimi ve mahremiyetli özet audit görünümü uygulandı; ayrı Admin eylem audit kalıcılığı ertelendi.

## 3. Bilinen sınırlılıklar ve ertelenmiş hardening

- Rate-limit sayaçları bellek içi ve API instance'ına özeldir; restart sayaçları sıfırlar, yatay instance'lar sayaç paylaşmaz.
- Merkezi access-token geri çağırma/security-stamp yoktur; aktif-kullanıcı kontrolünün kapsamadığı verilmiş token'lar sona erene kadar geçerlidir.
- Refresh-token rotasyonu, iptali ve aile reuse detection uygulanmıştır; ertelenmiş değildir.
- `ExpiresAtUtc` okuma sınırlarında uygulanır; süresi dolmuş özetleri fiziksel olarak silen zamanlanmış cleanup worker yoktur.
- Özet işlem audit kayıtları vardır; ayrı ve kalıcı Admin eylem audit kaydı yoktur.
- Gerçek Groq kalite/smoke, temiz ortam, PostgreSQL migration, Docker-backed test, tarayıcı yetkilendirme ve final teslim ekran görüntüsü doğrulamaları koddan çıkarılamaz; ayrıca çalıştırılmalıdır.
- Üretim veri bölgesi, kota/maliyet, dağıtık limiter ve secret/deployment platformu işletim kararlarıdır.
- CI/CD ve gerçek IFS sistem entegrasyonu mevcut repo mimarisinin dışında veya henüz uygulanmamıştır.
- Adaptif özet uzunluğu, kurumsal özet modları, kullanıcı geri bildirimi ve PDF dışa aktarma mevcut mimari olarak sunulmaz.

## 4. Karar kaydı kapsamı

ADR-001–ADR-016 kimlikleri korunmuştur; yeni ADR eklenmemiştir. Önceden açık görünen structured output, Turkish/English dil seçimi, 12.000 karakter sınırı, Admin preview/access ve rate-limit değerleri ilgili mevcut ADR'lerde güncel uygulama kararı olarak kapatılmıştır. Bu belge dosya envanteri değil, bu davranışların mimari gerekçesi ve sınırlarını kaydeder.
