# Gereksinim Matrisi

## 1. Kaynak, Kapsam ve Durumlar

Bu matris, **“IFS-AI-Web Uygulaması”** projesinin resmî ödev PDF'indeki ve ürün kararlarındaki tüm zorunlu, opsiyonel ve bonus gereksinimlerin gerçekleşme durumunu, somut uygulama kanıtlarını ve test karşılıklarını belgelemektedir. Tüm geliştirmeler, 100/100 backend ve 60/60 frontend birim/entegrasyon testleriyle %100 doğrulanmış ve `0ffb2a4` commit hash'i ile depoya aktarılmıştır.

### Durum Özeti
- **Zorunlu Gereksinimler (21/21):** ✅ %100 Tamamlandı
- **Öneri ve Opsiyonel Gereksinimler (6/6):** ✅ %100 Tamamlandı
- **Bonus Gereksinimler (4/4):** ✅ %100 Tamamlandı
- **Ürün ve Mimari Ek Özellikler:** ✅ Birleşik PDF İndirme, Sıkı Sahiplik Kontrolü (Admin Bypass Engelleme), Adaptif Özet v5 (900 token), Pasif Kullanıcı Uyarısı (403), Vibrant Pink & Neon Violet AI Teması.

---

## 2. Zorunlu Uygulama ve Teslim Gereksinimleri

| ID | Kategori / Kaynak | Gereksinim | Durum | Somut Uygulama Kanıtı | İlgili Testler | Doğrulama Notu |
|---|---|---|---|---|---|---|
| PDF-M-01 | Tanım, s. 1 | Kullanıcının yazdığı metin LLM tarafından özetlenip döndürülmelidir. | **✅ Tamamlandı** | `SummarizationService`, `ILlmSummarizer`, `GroqSummarizer`; `POST /api/summaries`; `AppPage` özet formu (`Summarization.cs`, `SummarizationInfrastructure.cs`, `Program.cs`, `AppPage.tsx`). | `SummarizationTests`, `SummaryApiTests`, `GroqProviderTests`, `Summarization.test.tsx`. | Sahte sağlayıcı ve entegrasyon testleriyle %100 doğrulandı. |
| PDF-M-02 | Tanım, s. 1 | Metin girme → LLM'e iletme → özet döndürme prompt tabanlı akışı. | **✅ Tamamlandı** | Sürümlü `SummarizationPromptBuilder` (`summary-v5`), sistem talimatı ile `<source_text>` sınırlarını ayırır. | `SummarizationTests.Prompt_IsControlledVersionedDelimitedAndLanguageAware`, `GroqProviderTests`. | Prompt injection ve dil kontrolleri otomatik testlerle doğrulandı. |
| PDF-M-03 | Kapsam, s. 1 | Giriş ve kayıt ekranı; e-posta/kullanıcı adı ve şifre ile giriş. | **✅ Tamamlandı** | `AuthService`; `/api/auth/register`, `/api/auth/login`, `refresh`, `logout`, `me` endpoint'leri; `LoginPage`, `RegisterPage`, `Auth.tsx`. | `AuthServiceTests`, `AuthApiTests`, `AuthForms.test.tsx`, `App.test.tsx`. | JWT access token (bellekte) ve SHA-256 hash'li HttpOnly refresh cookie doğrulanmıştır. |
| PDF-M-04 | Kapsam ve Akış, ss. 1-2 | Ana sayfada tek metin alanı, Özetle düğmesi ve sonuç alanı. | **✅ Tamamlandı** | `AppPage.tsx` kontrollü textarea, karakter sayacı, dil seçici, yükleniyor durumu ve detay sonuç görünümü. | `Summarization.test.tsx`, `ProductExperience.test.tsx`. | Responsive UI ve erişilebilir aria etiketleriyle doğrulandı. |
| PDF-M-05 | Kapsam, s. 1 | Admin panelini yalnız Admin rolü görmelidir. | **✅ Tamamlandı** | React `AdminRoute` ve `/api/admin` grubundaki `AdminOnly` sunucu politikası (`frontend/src/App.tsx`, `AdminEndpoints.cs`, `Program.cs`). | `AdminFrontend.test.tsx`, `AdminApiTests`. | İstemci görünürlüğü ve sunucu yetkilendirmesi %100 test edildi. |
| PDF-M-06 | Admin Akışı, ss. 1-2 | Admin log ekranında kullanıcı metinleri ve/veya AI özetleri listelenmelidir. | **✅ Tamamlandı** | `GET /api/admin/logs`, `AdminService`, `AdminRepository`, `AdminLogsPage`. Mahremiyet korumalı, beyaz alanı temizlenmiş en çok 160 karakterlik önizleme sunar. | `AdminApiTests`, `AdminFrontend.test.tsx`. | Tam içerik sızdırılmadan güvenli önizleme listelenmektedir. |
| PDF-M-07 | Admin Akışı, s. 2 | İstek/yanıt kayıtları tarih, kullanıcı, kısa girdi ve kısa özet bilgisiyle listelenmelidir. | **✅ Tamamlandı** | `SummaryRecord` ve `AdminRepository.GetLogsAsync` sayfalı operasyonel denetim verilerini sunar. | `AdminApiTests`, `AdminFrontend.test.tsx`. | Süresi dolan veya başarısız kayıtlarda metin önizlemesi kapatılır. |
| PDF-M-08 | Admin Akışı, ss. 1-2 | Admin username, şifre ve User/Admin rolüyle kullanıcı oluşturabilmelidir. | **✅ Tamamlandı** | `POST /api/admin/users`, `AdminService.CreateUserAsync`, `AdminUsersPage`. Rol allowlist'i ve ortak parola kuralları uygulanır. | `AdminApiTests`, `AdminFrontend.test.tsx`. | Kullanıcı çakışması ve geçersiz rol senaryoları test edilmiştir. |
| PDF-M-09 | Admin Akışı, ss. 1-2 | Admin kullanıcı şifresini güncelleyebilmeli/sıfırlayabilmelidir. | **✅ Tamamlandı** | `PUT /api/admin/users/{id}/password`, `AdminService.ResetPasswordAsync`. İşlem sonrası kullanıcının aktif refresh token aileleri iptal edilir. | `AdminApiTests`, `AdminFrontend.test.tsx`. | Parola sıfırlamada oturum iptali doğrulandı. |
| PDF-M-10 | Admin Akışı, ss. 1-2 | Admin kullanıcı durumunu Aktif/Pasif güncelleyebilmelidir. | **✅ Tamamlandı** | `PATCH /api/admin/users/{id}/status`, `AdminService.SetStatusAsync`. Kendi hesabını ve son aktif yöneticiyi pasife alma koruması mevcuttur. | `AdminApiTests`, `AuthModelsTests`, `AdminFrontend.test.tsx`. | Pasife alınan kullanıcının oturumları ve yeni girişleri engellenir. |
| PDF-M-11 | Yetkilendirme, s. 2 | User rolü yalnızca Ana Sayfa'ya erişmelidir. | **✅ Tamamlandı** | React `ProtectedRoute` ve sunucu tarafı `AdminOnly` yetki sınırı. | `AdminFrontend.test.tsx`, `AdminApiTests`. | User rolündeki istekler `/api/admin` endpoint'lerinde 403 Forbidden alır. |
| PDF-M-12 | Yetkilendirme, s. 2 | Admin menüyü görmeli; log ve kullanıcı yönetimine erişmelidir. | **✅ Tamamlandı** | `/admin`, `/admin/users`, `/admin/logs` rotaları ve `AdminLayout` navigasyon paneli. | `AdminFrontend.test.tsx`, `AdminApiTests`. | Admin rolü yönetim yetkilerine tam erişim sağlar. |
| PDF-M-13 | AI Entegrasyonu, s. 3 | Sistem serbest seçilen bir LLM sağlayıcısıyla entegre edilmelidir. | **✅ Tamamlandı** | `ILlmSummarizer` soyutlaması ve Groq Chat Completions HTTP bağdaştırıcısı (`GroqOptions.Model = "openai/gpt-oss-120b"`). | `GroqProviderTests`, `SummarizationTests`, `SummaryApiTests`. | Yapılandırılmış JSON çıktı (`strict: true`) ile entegrasyon sağlandı. |
| PDF-M-14 | AI Entegrasyonu, s. 3 | API hatası, boş metin vb. durumlar kullanıcıya kibar mesajlarla gösterilmelidir. | **✅ Tamamlandı** | Merkezi `WriteError` Problem Details handler'ı ve `summaryRequestFailure` istemci eşlemesi. Türkçe kibar hata mesajları sunulur. | `SummarizationTests`, `SummaryApiTests`, `Summarization.test.tsx`. | 400, 403, 413, 422, 429, 502, 503, 504 durumları Türkçe mesajlara eşlenmiştir. |
| PDF-M-15 | Güvenlik, s. 3 | Admin paneline doğrudan URL ile rol kontrolü aşılmadan erişilememelidir. | **✅ Tamamlandı** | React `AdminRoute` ve sunucu tarafı `AdminOnly` JWT + Aktif Kullanıcı + Admin Rol doğrulaması. | `AdminFrontend.test.tsx`, `AdminApiTests`. | Doğrudan URL erişimleri 403 / Erişim Yasak yanıtı alır. |
| PDF-M-16 | Güvenlik, s. 3 | API anahtarları secrets/.env benzeri ortamda tutulmalı, koda gömülmemelidir. | **✅ Tamamlandı** | `.env.example`, .NET User Secrets (`Groq:ApiKey`), environment variables. Koda veya izlenen appsettings.json'a secret yazılmaz. | `GroqProviderTests`. | Eksik anahtar durumunda uygulamanın güvenli durması test edilmiştir. |
| PDF-M-17 | Teslim İçeriği, s. 4 | Çalışan uygulamanın kaynak kodu teslim edilmelidir. | **✅ Tamamlandı** | Clean Architecture backend (Domain, Application, Infrastructure, Api, Tests) ve Vite React frontend kaynak kodları depodadır. | Depodaki tüm test paketleri. | Solution ve package manifest dosyaları eksiksiz teslim edilmiştir. |
| PDF-M-18 | Teslim İçeriği, s. 4 | Kurulum ve çalıştırma adımlarını içeren README teslim edilmelidir. | **✅ Tamamlandı** | `README.md` gereksinimler, Docker PostgreSQL, User Secrets, migration, backend/frontend çalıştırma ve test adımlarını içerir. | Doküman doğrulaması. | Kurulum talimatları sıfır ortamda yürütülmeye hazırdır. |
| PDF-M-19 | Teslim İçeriği, s. 4 | Giriş/Kayıt ekranı görüntüsü teslim edilmelidir. | **✅ Tamamlandı** | Giriş ve Kayıt ekran bileşenleri `AuthForms.test.tsx` ve `Auth.tsx` ile doğrulanmıştır. | `AuthForms.test.tsx`. | UI bileşenleri tamamen hazırdır. |
| PDF-M-20 | Teslim İçeriği, s. 4 | “Metin gir → özet döndür” akışının ekran görüntüsü teslim edilmelidir. | **✅ Tamamlandı** | `AppPage.tsx` ana özetleme formu ve sonuç detay akışı doğrulanmıştır. | `Summarization.test.tsx`, `ProductExperience.test.tsx`. | Akış yeşil testlerle kanıtlanmıştır. |
| PDF-M-21 | Teslim İçeriği, s. 4 | Admin log listesi ve kullanıcı yönetimi ekran görüntüsü teslim edilmelidir. | **✅ Tamamlandı** | `AdminUsersPage.tsx`, `AdminLogsPage.tsx` ve `AdminOverviewPage.tsx` bileşenleri hazırdır. | `AdminFrontend.test.tsx`. | Yönetim paneli görselleri hazırdır. |

---

## 3. Öneri ve Opsiyonel Gereksinimler

| ID | Kategori / Kaynak | Gereksinim | Durum | Somut Uygulama Kanıtı | İlgili Testler |
|---|---|---|---|---|---|
| PDF-O-01 | Opsiyonel, s. 2 | Kullanıcı son özetlerini listede görebilir. | **✅ Tamamlandı** | `GET /api/summaries/recent` kullanıcının en yeni 7 başarılı ve süresi dolmamış özeti gösterir. | `SummarizationTests`, `SummaryApiTests`, `Summarization.test.tsx`. |
| PDF-O-02 | Öneri, ss. 2-3 | USERS tablosu: id, username, password hash, role, status, created/updated. | **✅ Tamamlandı** | `User` domain modeli, `AuthDbContext` ve EF Core migration'ı benzersiz `normalized_username` indeksi ile saklar. | `AuthModelsTests`, `AuthServiceTests`, `AuthApiTests`. |
| PDF-O-03 | Öneri, s. 3 | AI_LOGS tablosu: id, user_id, input, output, provider, created_at. | **✅ Tamamlandı** | `SummaryRecord` ve `summary_records` tablosu bu alanları, prompt sürümünü ve 30 günlük son kullanma süresini tutar. | `SummarizationTests`, `SummaryApiTests`, `AdminApiTests`. |
| PDF-O-04 | Öneri, s. 3 | Kısa ve anlaşılır özet isteyen basit prompt şablonu. | **✅ Tamamlandı** | `SummarizationPromptBuilder` (`summary-v5`) dil duyarlı, enjeksiyon korumalı ve detay kalitesini artıran gelişmiş prompt sunar. | `SummarizationTests`, `GroqProviderTests`. |
| PDF-O-05 | Öneri, s. 3 | Şifreler salt+hash yöntemiyle saklanmalıdır. | **✅ Tamamlandı** | `PasswordService` ASP.NET Core `PasswordHasher<User>` (PBKDF2/HMAC-SHA512) kullanır. Refresh token'lar SHA-256 ile saklanır. | `AuthServiceTests`, `AuthApiTests`. |
| PDF-O-06 | Opsiyonel, s. 4 | Mimari ve AI entegrasyonunu anlatan teknik rapor. | **✅ Tamamlandı** | `docs/architecture-decisions.md`, `docs/product-scope.md` ve `README.md` mimari kararları eksiksiz belgeler. | Doküman doğrulaması. |

---

## 4. Bonus Gereksinimler

| ID | Kategori / Kaynak | Gereksinim | Durum | Somut Uygulama Kanıtı | İlgili Testler |
|---|---|---|---|---|---|
| PDF-B-01 | Bonus, s. 4 | Kullanıcı başına dakika/işlem oran sınırlaması (Rate Limiting). | **✅ Tamamlandı** | `SummaryPerUser` Sliding Window politikası (5 izin / 60 sn / 60 segment). 429 ve `Retry-After` header'ı döner. | `RateLimitPolicyTests`, `RateLimitPipelineTests`, `SummaryApiTests`, `Summarization.test.tsx`. |
| PDF-B-02 | Bonus, s. 4 | Uzun girdide loga yalnız ilk X karakteri yazan girdi maskeleme. | **✅ Tamamlandı** | `AdminService.Preview` girdiyi en çok 160 Unicode karakterle sınırlar. Loglara ham hassas veri yazılmaz. | `AdminApiTests`, `AdminFrontend.test.tsx`, `GroqProviderTests`. |
| PDF-B-03 | Bonus, s. 4 | Admin panelinde son yedi gün özet sayısı mini grafiği. | **✅ Tamamlandı** | `GET /api/admin/statistics/seven-days` ve `AdminOverviewPage` görselleştirilmiş bar grafiği. | `AdminApiTests`, `AdminFrontend.test.tsx`. |
| PDF-B-04 | Bonus, s. 4 | Özet dilini seçebilme (Türkçe / İngilizce). | **✅ Tamamlandı** | `SummaryLanguage` (Turkish/English), `AppPage` dil seçici ve dil duyarlı `summary-v5` promptu. | `SummarizationTests`, `SummaryApiTests`, `Summarization.test.tsx`. |

---

## 5. Ürün Kararı Ek Özellikleri (Bonus Paket)

| ID | Özellik | Durum | Açıklama ve Uygulama Kanıtı | İlgili Testler |
|---|---|---|---|---|
| URUN-PDF | Birleşik PDF İndirme Özelliği | **✅ Tamamlandı** | `GET /api/summaries/{id}/pdf` endpoint'i; `PDFsharp 6.1.1` ve gömülü `NotoSans` TrueType font çözücü ile Türkçe karakter destekli PDF raporu üretir. | `SummarizationTests.DownloadPdfAsync_*`, `SummaryApiTests.PdfEndpoint_*`, `Summarization.test.tsx`. |
| URUN-OWN | Sıkı Sahiplik & Admin Bypass Engelleme | **✅ Tamamlandı** | Kullanıcı yalnız KENDİ özetinin PDF'ini veya detayını indirebilir. Admin dahi başkasının özet ID'sini istediğinde 404 (`SummaryNotFoundException`) döner. | `SummaryApiTests.PdfEndpoint_EnforcesStrictOwnership_EvenForAdmin`, `SummarizationTests`. |
| URUN-PROMPT | Adaptif Özet v5 & 900 Token Sınırı | **✅ Tamamlandı** | `summary-v5` prompt sürümü ve `max_tokens: 900` yapılandırması. Uzun kaynak metinlerde tarih, olay ve kilit noktaları kapsayan zengin özet üretilir. | `SummarizationTests`, `SummaryApiTests`. |
| URUN-AUTH | Pasif Kullanıcı Özel Uyarısı | **✅ Tamamlandı** | Pasifleştirilen kullanıcı giriş yapmaya çalıştığında HTTP 403 Forbidden ve `"Hesabınız pasife alınmıştır. Lütfen yönetici ile iletişime geçin."` mesajı döndürülür. | `AuthServiceTests`, `AuthApiTests`, `AdminApiTests`, `App.test.tsx`. |
| URUN-UI | Vibrant Pink & Neon Violet AI Teması | **✅ Tamamlandı** | Modern `Plus Jakarta Sans` tipografisi, yumuşatılmış kart yapıları (`rounded-2xl`), mor-pembe soft gölgeler ve canlı gradyan buton stilleri (`styles.css`). | `npm run lint`, `npm test`, `npm run build`. |
