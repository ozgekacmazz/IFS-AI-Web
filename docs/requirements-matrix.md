# Gereksinim Matrisi

## 1. Kaynak, kapsam ve durumlar

Bu matrisin birincil kaynağı depo kökündeki dört sayfalık **“2. Proje - AI-Web Uygulaması .pdf”** belgesidir. PDF bütünüyle incelenmiş; her madde zorunlu, öneri/opsiyonel, bonus veya teslim gereksinimi olarak kendi özgün sınıfında tutulmuştur. Uygulama kanıtı için kaynak kod, yapılandırma, EF Core migration'ları ve otomatik testler doğrudan incelenmiştir.

Durumlar:

- **Tamamlandı:** Depoda doğrudan uygulama kanıtı vardır. Gerekli gerçek servis/tarayıcı doğrulaması yapılmadıysa doğrulama notunda ayrıca belirtilir.
- **Kısmen tamamlandı:** Gereksinimin yalnız bir bölümü veya teslim kanıtı vardır.
- **Planlandı:** Gereksinime ait çıktı depoda yoktur.
- **Kapsam dışı:** Özgün PDF gereksinimi değildir veya bilinçli olarak bu teslim kapsamına alınmamıştır.
- **Doğrulanamadı:** Mevcut depo kanıtı güvenilir sınıflandırma için yeterli değildir.

PDF gereksinim sayıları: **21 zorunlu/teslim**, **6 öneri veya opsiyonel**, **4 bonus**; toplam **31**.

## 2. Zorunlu uygulama ve teslim gereksinimleri

| ID | Kategori / kaynak | Gereksinim | Durum | Somut uygulama kanıtı | İlgili testler | Doğrulama notu / kalan iş |
|---|---|---|---|---|---|---|
| PDF-M-01 | Tanım, s. 1 | Kullanıcının yazdığı metin LLM tarafından özetlenip döndürülmelidir. | **Tamamlandı** | `SummarizationService`, `ILlmSummarizer` ve `GroqSummarizer`; `POST /api/summaries`; `AppPage` özet formu ve sonuç görünümü (`backend/src/IFS.AIWeb.Application/Summarization.cs`, `backend/src/IFS.AIWeb.Infrastructure/SummarizationInfrastructure.cs`, `backend/src/IFS.AIWeb.Api/Program.cs`, `frontend/src/pages/AppPage.tsx`). | `SummarizationTests`, `SummaryApiTests`, `GroqProviderTests`, `Summarization.test.tsx`. | Kod ve sahte sağlayıcı testleri mevcut; bu görevde gerçek Groq çağrısı yapılmadı, runtime verification pending. |
| PDF-M-02 | Tanım, s. 1 | Metin girme → LLM'e iletme → özet döndürme prompt tabanlı akışı. | **Tamamlandı** | Sürümlü `SummarizationPromptBuilder` (`summary-v3`) kaynak metni sınırlar; sağlayıcıya system/user mesajları gönderilir; yapılandırılmış sonuç uygulamaya döner. | `SummarizationTests.Prompt_IsControlledVersionedDelimitedAndLanguageAware`, `GroqProviderTests.Request_UsesConfiguredModelTokenLimitAndBearerWithoutExposingValue`. | Prompt ve taşıma sözleşmesi sahte HTTP işleyicisiyle doğrulanmıştır; gerçek Groq doğrulaması bekliyor. |
| PDF-M-03 | Kapsam, s. 1 | Giriş ve kayıt ekranı; e-posta/kullanıcı adı ve şifre ile giriş. | **Tamamlandı** | Ürün seçimi olarak kullanıcı adı kullanılır. `AuthService`; register/login/refresh/logout/me endpointleri; `LoginPage`, `RegisterPage`, `AuthProvider`; JWT access token ve döndürülen hash'li refresh token (`AuthService.cs`, `AuthInfrastructure.cs`, `Program.cs`, `frontend/src/auth/Auth.tsx`). | `AuthServiceTests`, `AuthApiTests`, `AuthForms.test.tsx`, `App.test.tsx`. | Kod/test kanıtı var; bu görevde gerçek PostgreSQL ve tarayıcı oturum akışı çalıştırılmadı. |
| PDF-M-04 | Kapsam ve Kullanıcı Akışı, ss. 1-2 | Ana sayfada tek metin alanı, Özetle düğmesi ve sonuç alanı. | **Tamamlandı** | `AppPage` kontrollü textarea, dil seçimi, gönderim/bekleme/hata ve detay sonuç görünümü sağlar. | `Summarization.test.tsx`, `ProductExperience.test.tsx`. | Bileşen testleri mevcut; teslim ekran görüntüsü PDF-M-20 altında ayrıca bekliyor. |
| PDF-M-05 | Kapsam, s. 1 | Admin panelini yalnız Admin rolü görmelidir. | **Tamamlandı** | React `AdminRoute` ve role göre navigasyon; `/api/admin` grubu `AdminOnly` politikasıyla korunur (`frontend/src/App.tsx`, `frontend/src/auth/Auth.tsx`, `AdminLayout.tsx`, `AdminEndpoints.cs`, `Program.cs`). | `AdminFrontend.test.tsx`, `AdminApiTests`. | İstemci kontrolü UX içindir; sunucu politikası yetki sınırıdır. |
| PDF-M-06 | Kapsam ve Admin Akışı, ss. 1-2 | Admin log ekranında kullanıcı metinleri ve/veya AI özetleri listelenmelidir. | **Tamamlandı** | `GET /api/admin/logs`, `AdminService`, `AdminRepository` ve `AdminLogsPage` yalnız sınırlı önizleme/operasyon meta verisi gösterir; tam içerik Admin cevabına açılmaz. | `AdminApiTests` log gizlilik/sayfalama senaryoları, `AdminFrontend.test.tsx` log görünümü. | PDF “ve/veya” der; mahremiyet nedeniyle güvenli, kısaltılmış önizleme seçilmiştir. Runtime verification pending. |
| PDF-M-07 | Admin Akışı, s. 2 | İstek/yanıt tarih-kullanıcı-kısa giriş-kısa özet içeren tek satır kayıt olarak listelenmelidir. | **Tamamlandı** | `SummaryRecord` tarih, kullanıcı, giriş, özet, durum ve sağlayıcı meta verisini tutar; `AdminRepository.GetLogsAsync` sayfalar; `AdminService.Preview` 160 metin öğesiyle sınırlar; UI kompakt satır/kart sunar. | `AdminApiTests`, `AdminFrontend.test.tsx`. | Başarısız kayıtlarda kaynak/özet saklanmaz; süresi dolanlarda önizleme açılmaz. |
| PDF-M-08 | Kapsam ve Admin Akışı, ss. 1-2 | Admin username/email, şifre ve User/Admin rolüyle kullanıcı oluşturabilmelidir. | **Tamamlandı** | Kullanıcı adı seçeneği uygulanmıştır. `POST /api/admin/users`, `AdminService.CreateUserAsync`, `AdminUsersPage`; rol allowlist'i ve ortak doğrulama kullanılır. | `AdminApiTests` oluşturma/çakışma/geçersiz rol/yetki; `AdminFrontend.test.tsx` form ve hata eşleme. | E-posta desteklenmez; PDF'nin `username/email` alternatiflerinden kullanıcı adı seçilmiştir. |
| PDF-M-09 | Kapsam ve Admin Akışı, ss. 1-2 | Admin kullanıcı şifresini güncelleyebilmeli/sıfırlayabilmelidir. | **Tamamlandı** | `PUT /api/admin/users/{id}/password`, `AdminService.ResetPasswordAsync`, `AdminRepository.ResetPasswordAsync`, Admin parola diyaloğu; refresh oturumları iptal edilir. | `AdminApiTests` eski/yeni parola ve refresh iptali; `AdminFrontend.test.tsx`. | Mevcut access token doğal 15 dakikalık süresine kadar geçerli kalabilir; PDF self-service unutulan parola istemez. |
| PDF-M-10 | Kapsam ve Admin Akışı, ss. 1-2 | Admin kullanıcı durumunu Aktif/Pasif güncelleyebilmelidir. | **Tamamlandı** | `PATCH /api/admin/users/{id}/status`; `AdminService.SetStatusAsync`; eşzamanlı son-admin ve kendi hesabını pasifleştirme korumaları; Admin UI onayı. | `AdminApiTests` pasifleştirme/aktifleştirme/eski token/son admin; `AuthModelsTests`; `AdminFrontend.test.tsx`. | Kod ve veritabanı entegrasyon testleri mevcut; bu görevde runtime çalıştırılmadı. |
| PDF-M-11 | Yetkilendirme, s. 2 | User rolü yalnızca Ana Sayfa'ya erişmelidir. | **Tamamlandı** | `ProtectedRoute` uygulama alanını, `AdminRoute` Admin sayfalarını korur; `/api/admin` yalnız `AdminOnly`; normal User için admin navigasyonu gösterilmez. | `AdminFrontend.test.tsx` User yönlendirme/menü; `AdminApiTests` User için 403. | Admin normal özet alanını da kullanabilir; User yönetim alanına erişemez. |
| PDF-M-12 | Yetkilendirme, s. 2 | Admin menüyü görmeli; log ve kullanıcı yönetimine erişmelidir. | **Tamamlandı** | `/admin`, `/admin/users`, `/admin/logs`; `AdminLayout`; ilgili API endpointleri `AdminOnly` grubundadır. | `AdminFrontend.test.tsx`, `AdminApiTests`. | Kod/test kanıtı var; teslim görselleri PDF-M-21 altında bekliyor. |
| PDF-M-13 | AI Entegrasyonu, s. 3 | Sistem serbest seçilen bir LLM sağlayıcısıyla entegre edilmelidir. | **Tamamlandı** | Sağlayıcıdan bağımsız `ILlmSummarizer`; Groq Chat Completions bağdaştırıcısı; model mevcut `GroqOptions`/`Groq:Model` üzerinden `openai/gpt-oss-120b`. | `GroqProviderTests`, `SummarizationTests`, `SummaryApiTests`. | Gerçek sağlayıcı çağrısı bu görevde yapılmadı; runtime verification pending. |
| PDF-M-14 | AI Entegrasyonu, s. 3 | API hatası, boş metin vb. kullanıcıya yalın ve kibar mesajlarla gösterilmelidir. | **Tamamlandı** | `SummaryValidation`, merkezi `WriteError` Problem Details eşlemesi ve `summaryRequestFailure`; boş/geçersiz metin, 413, 422, 429, 502 ve diğer güvenli Türkçe mesajlar. | `SummarizationTests` doğrulama; `SummaryApiTests.InvalidAndProviderFailure_ReturnSafeProblemDetails`; `Summarization.test.tsx`. | Otomatik test kanıtı var; tüm hata türleri için gerçek tarayıcı/runtime kontrolü bekliyor. |
| PDF-M-15 | Güvenlik, s. 3 | Admin paneline doğrudan URL ile rol kontrolü aşılmadan erişilememelidir. | **Tamamlandı** | React `AdminRoute`; sunucuda `AdminOnly` = kimliği doğrulanmış + Admin rolü + aktif kullanıcı şartı; bütün admin endpointleri aynı route grubundadır. | `AdminFrontend.test.tsx`; `AdminApiTests` anonim 401, User 403 ve pasif Admin senaryoları. | Backend nihai yetki sınırıdır. |
| PDF-M-16 | Güvenlik, s. 3 | API anahtarları secrets/.env benzeri ortamda tutulmalı, koda gömülmemelidir. | **Tamamlandı** | İzlenen `appsettings.json` içinde `Groq:ApiKey` ve JWT/DB secret alanları boştur; `Program` eksik Groq anahtarı/JWT anahtarında üretim başlangıcını reddeder; `README.md` .NET User Secrets adımlarını verir; sağlayıcı logları anahtarı yazmaz. | `GroqProviderTests` istek gövdesi/log sızıntısı kontrolleri; yapılandırma başlangıç kontrolü kod incelemesi. | Gerçek secret değeri incelenmedi veya belgelenmedi. Temiz ortam başlangıç doğrulaması bekliyor. |
| PDF-M-17 | Teslim İçeriği, s. 4 | Çalışan uygulamanın kaynak kodu teslim edilmelidir. | **Kısmen tamamlandı** | Backend, frontend, migration ve test kaynakları depodadır; çözüm/proje dosyaları mevcuttur. | Depoda application/domain/frontend ve non-Docker test projeleri vardır. | “Çalışan teslim” için README'nin temiz ortamda uygulanması, PostgreSQL migration, gerçek Groq ve uçtan uca akış kanıtı henüz bu matriste doğrulanmamıştır. |
| PDF-M-18 | Teslim İçeriği, s. 4 | Kurulum ve çalıştırma adımlarını içeren README teslim edilmelidir. | **Tamamlandı** | `README.md` önkoşullar, PostgreSQL/Docker seçeneği, User Secrets, migration, backend/frontend başlatma ve test komutlarını içerir. | Doküman incelemesi. | İçerik mevcut; adımların sıfırdan temiz ortam yürütmesi runtime verification pending. |
| PDF-M-19 | Teslim İçeriği, s. 4 | Giriş/Kayıt ekranı görüntüsü teslim edilmelidir. | **Planlandı** | Giriş/kayıt UI kodu vardır; depoda bu teslim kanıtı için izlenen ekran görüntüsü yoktur. | `AuthForms.test.tsx` yalnız davranış kanıtıdır, ekran görüntüsünün yerini tutmaz. | Okunur giriş ve kayıt görselleri oluşturulup teslim paketine eklenmeli. |
| PDF-M-20 | Teslim İçeriği, s. 4 | “Metin gir → özet döndür” akışının ekran görüntüsü teslim edilmelidir. | **Planlandı** | Akış uygulanmıştır; depoda kaynak metin ve sonucu gösteren teslim ekran görüntüsü yoktur. | `Summarization.test.tsx` davranış kanıtıdır. | Hassas olmayan örnekle gerçek sonuç ekranı görüntüsü gerekli. |
| PDF-M-21 | Teslim İçeriği, s. 4 | Admin log listesi ve kullanıcı yönetimi ekran görüntüleri teslim edilmelidir. | **Planlandı** | Her iki Admin ekranı uygulanmıştır; depoda istenen teslim görselleri yoktur. | `AdminFrontend.test.tsx` davranış kanıtıdır. | Log ve kullanıcı yönetimi için mahremiyet kontrollü görseller gerekli. |

## 3. PDF'deki öneriler ve opsiyonel gereksinimler

| ID | Kategori / kaynak | Gereksinim | Durum | Somut uygulama kanıtı | İlgili testler | Doğrulama notu / kalan iş |
|---|---|---|---|---|---|---|
| PDF-O-01 | Opsiyonel, Kullanıcı Akışı, s. 2 | Kullanıcı son üç özetini basit listede görebilir. | **Tamamlandı** | Ürün kararıyla en yeni yedi uygun, başarılı ve süresi dolmamış özet gösterilir; `RecentAsync`, repository sorgusu ve `LibraryView`. | `SummarizationTests` recent senaryoları, `SummaryApiTests.Recent_ExcludesIneligibleAndOtherUsersBeforeTakingNewestSeven`, `Summarization.test.tsx`. | PDF minimumu aşılmıştır; yalnız mevcut kullanıcı kayıtlarına erişilir. Runtime verification pending. |
| PDF-O-02 | Öneri, Veri Yapıları, ss. 2-3 | USERS minimum alanları: id, benzersiz username/email, password hash, role, status, created/updated. | **Tamamlandı** | `User`, `AuthDbContext` ve `20260802110648_InitialAuthentication` migration'ı tüm kavramları taşır; benzersiz `normalized_username` indeksi vardır. | `AuthModelsTests`, `AuthServiceTests`, `AuthApiTests`. | Kullanıcı adı seçilmiştir; e-posta alanı yoktur. Temiz PostgreSQL migration doğrulaması bekliyor. |
| PDF-O-03 | Öneri, Veri Yapıları, s. 3 | AI_LOGS minimum alanları: id, user_id, opsiyonel input, output, provider, created_at. | **Tamamlandı** | `SummaryRecord`, `AuthDbContext` ve `20260802121509_AddSummarizationRecords` migration'ındaki `summary_records` bu alanları ve ek denetim meta verisini içerir. | `SummarizationTests`, `SummaryApiTests`, `AdminApiTests`. | Başarısız denemede giriş/özet saklanmaz; otomatik 30 günlük fiziksel silme worker'ı yoktur fakat PDF bunu istemez. |
| PDF-O-04 | Öneri, AI Entegrasyonu, s. 3 | Kısa ve anlaşılır özet isteyen basit prompt şablonu. | **Tamamlandı** | `SummarizationPromptBuilder` aynı amacı daha kontrollü, dil duyarlı, enjeksiyona dayanıklı ve sürümlü `summary-v3` talimatıyla uygular. | `SummarizationTests.Prompt_IsControlledVersionedDelimitedAndLanguageAware`, `GroqProviderTests`. | PDF metni birebir kopyalanmamış, anlamsal gereksinim güvenli biçimde karşılanmıştır. |
| PDF-O-05 | Öneri, Güvenlik, s. 3 | Şifreler salt+hash yöntemiyle saklanmalıdır. | **Tamamlandı** | `PasswordService`, ASP.NET Core `PasswordHasher<User>` kullanır; yalnız `PasswordHash` kalıcıdır. Refresh tokenlar ayrıca SHA-256 hash ile saklanır. | `AuthServiceTests`; `AuthApiTests`; Admin parola sıfırlama senaryoları `AdminApiTests`. | Gerçek veritabanında düz metin bulunmadığına ilişkin manuel DB incelemesi bu görevde yapılmadı. |
| PDF-O-06 | Opsiyonel, Teslim İçeriği, s. 4 | Mimari, AI entegrasyonu ve sınırlılıkları anlatan kısa teknik rapor. | **Kısmen tamamlandı** | `docs/architecture-decisions.md`, `docs/admin-backend.md`, `docs/admin-frontend.md` ve README ilgili teknik içeriğin önemli bölümünü taşır. | Doküman incelemesi. | Tek, güncel ve teslimata hazır kısa teknik rapor yok; `docs/sprint-report.md` hâlâ şablondur ve diğer dokümanların güncel senkronizasyonu ayrıca yapılmalıdır. |

## 4. Bonus PDF gereksinimleri

| ID | Kategori / kaynak | Gereksinim | Durum | Somut uygulama kanıtı | İlgili testler | Doğrulama notu / kalan iş |
|---|---|---|---|---|---|---|
| PDF-B-01 | Bonus, s. 4 | Kullanıcı başına dakika/işlem oran sınırlaması. | **Tamamlandı** | `SummaryPerUser` adlı 5 izin/60 saniye/60 segment Sliding Window politikası yalnız `POST /api/summaries` endpointine, JWT `sub` bölümüyle uygulanır; 429, `Retry-After` ve `retryAfterSeconds` döner. | `RateLimitPolicyTests`, gerçek middleware kullanan `RateLimitPipelineTests`, `SummaryApiTests`, frontend geri sayım testi `Summarization.test.tsx`. | Uygulama içi limiter süreç yeniden başladığında sıfırlanır ve çok instance arasında paylaşılmaz; gerçek tarayıcı kanıtı bu görevde üretilmedi. |
| PDF-B-02 | Bonus, s. 4 | Çok uzun metinde loga yalnız ilk X karakteri yazan girdi maskeleme. | **Tamamlandı** | Admin log sorgusu en çok 512 karakter projekte eder; `AdminService.Preview` beyaz alanı normalize edip en çok 160 Unicode metin öğesi ve üç nokta döndürür; başarısız/süresi dolmuş içerik önizlenmez. Sağlayıcı logları ham içerik içermez. | `AdminApiTests` önizleme/mahremiyet; `AdminFrontend.test.tsx`; `GroqProviderTests.FailureLog_ContainsSafeCategoryWithoutSensitiveProviderData`. | Tam kaynak/özet Admin API cevabına veya provider hata loguna eklenmez. |
| PDF-B-03 | Bonus, s. 4 | Admin panelinde son yedi gün özet sayısı mini grafiği. | **Tamamlandı** | `GET /api/admin/statistics/seven-days`, `AdminService.GetSevenDayStatisticsAsync`, PostgreSQL toplulaştırması ve `AdminOverviewPage` grafik + semantik tablo. | `AdminApiTests` yedi UTC gün/sınır/kovalar; `AdminFrontend.test.tsx` overview. | Kod/test kanıtı var; teslim ekran görüntüsü yok. |
| PDF-B-04 | Bonus, s. 4 | Özet dilini seçebilme. | **Tamamlandı** | `SummaryLanguage` Turkish/English; `AppPage` dil seçici; request doğrulaması ve dil duyarlı prompt. | `SummarizationTests`, `SummaryApiTests.ShortInformativeContent_RemainsSuccessful`, `Summarization.test.tsx`. | Gerçek sağlayıcıyla iki dilde runtime verification pending. |

## 5. PDF gereksinimi olmayan ürün kararları

Bu bölüm PDF sayımlarına ve teslim uygunluğu sonucuna dahil edilmez.

| ID | Ürün kararı / fikir | Mevcut durum | Not |
|---|---|---|---|
| URUN-01 | Arayüzün varsayılan dili Türkçedir. | **Tamamlandı** | React metinleri ve API hata metinleri Türkçedir. |
| URUN-02 | Türkçe veya İngilizce özet üretimi. | **Tamamlandı** | Aynı zamanda PDF-B-04 bonusunu karşılar. |
| URUN-03 | Uyarlanabilir kısa/orta/uzun özet uzunluğu. | **Kapsam dışı** | Uygulanmadı; özgün PDF gereksinimi değildir. |
| URUN-04 | Paragraf/madde listesi gibi çıktı biçimi seçimi. | **Kapsam dışı** | Uygulanmadı; özgün PDF gereksinimi değildir. |
| URUN-05 | Hedef kitle seçimi. | **Kapsam dışı** | Uygulanmadı; özgün PDF gereksinimi değildir. |
| URUN-06 | Kurumsal özet şablonları. | **Kapsam dışı** | Uygulanmadı; özgün PDF gereksinimi değildir. |
| URUN-07 | Kaynak cümle bağlantılı özet. | **Kapsam dışı** | Uygulanmadı; özgün PDF gereksinimi değildir. |
| URUN-08 | Eylem/risk/eksik bilgi bölümleri. | **Kapsam dışı** | Uygulanmadı; özgün PDF gereksinimi değildir. |
| URUN-09 | Kaynakta olmayan bilgi üretmeme ve yapay güven yüzdesi göstermeme. | **Kısmen tamamlandı** | `summary-v3` promptu bunu yasaklar; sistematik gerçek-model değerlendirme seti yoktur. |

## 6. Çözülen seçimler ve bilinen sınırlılıklar

- PDF'nin kullanıcı adı/e-posta seçeneğinden yalnız **kullanıcı adı** seçilmiştir; açık kayıt her zaman `User`, Admin oluşturma `User` veya `Admin` üretir.
- Access token 15 dakika; refresh token 7 gün, döndürmeli ve tekrar kullanımda aile iptallidir. Refresh cookie `HttpOnly`, `SameSite=Strict`, üretimde `Secure` ve `/api/auth` path kullanır; access token yalnız bellektedir.
- Başarılı tam kaynak ve özet 30 gün süre bilgisiyle saklanır. Süresi dolmuş kayıtlar kullanıcı listesi/detayında gösterilmez; zamanlanmış fiziksel temizleme worker'ı henüz yoktur.
- Groq sağlayıcısı ve model mevcut yapılandırma sistemiyle değiştirilebilir. `openai/gpt-oss-120b`, `json_schema`, `strict: true`, düşük reasoning effort ve en çok 500 completion token kullanılır. Yalnız pozitif tanımlanan yapılandırılmış çıktı HTTP 400 hatası için tek kontrollü retry vardır.
- Rate limiter uygulama belleğindedir; backend yeniden başlatma sayacı sıfırlar ve yatay ölçek için dağıtık sayaç yoktur.
- Girdi yalnız düz metindir; uygulama limiti 12.000 karakter, request body limiti 128 KiB'dir.
- Admin tam kaynak veya tam özet okuyamaz; yalnız mahremiyet kontrollü önizleme ve operasyon meta verisi görür.

## 7. Final delivery verification still pending

### Uygulanmış uygulama gereksinimleri

- PDF-M-01 ile PDF-M-16 arasındaki uygulama/güvenlik gereksinimleri için doğrudan kod ve otomatik test kanıtı vardır.
- PDF-M-18 README içeriği depodadır.
- PDF-O-01 ile PDF-O-05 ve dört özgün bonus gereksinimi uygulanmıştır.

### Dokümantasyon senkronizasyonu gerekenler

- `README.md`, `docs/architecture-decisions.md`, `docs/product-scope.md`, `docs/sprint-report.md`, `docs/admin-backend.md` ve `docs/admin-frontend.md` bu görevde değiştirilmemiştir; aralarındaki eski faz ifadeleri ayrı bir dokümantasyon görevinde gözden geçirilmelidir.
- PDF-O-06 için tek ve güncel kısa teknik teslim raporu hazırlanmalıdır.

### Eksik teslim ekran görüntüleri

- PDF-M-19: Giriş ve Kayıt ekranları.
- PDF-M-20: Kaynak metin ve dönen özeti gösteren ana sayfa akışı.
- PDF-M-21: Admin log listesi ve kullanıcı yönetimi ekranları.

### Temiz ortam ve runtime doğrulaması

- README adımlarının temiz makine/temiz klonda uygulanması.
- Temiz PostgreSQL üzerinde migration ve uygulama başlangıcı.
- Secret değerlerini açığa çıkarmadan gerçek Groq ile Türkçe/İngilizce özet ve hata davranışı.
- Kayıt, login, refresh, logout, User/Admin yetkisi ve pasif kullanıcı akışlarının gerçek tarayıcıda kanıtlanması.
- Teslim görsellerinin hassas veri içermediğinin kontrolü.

Bu matris uygulama kanıtını günceller; projenin bütün teslimatının tamamlandığını iddia etmez.
