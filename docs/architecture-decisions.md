# Mimari Kararlar

Bu belge, **IFS-AI-Web** projesinin mimari kararlarını (ADR), tasarım ilkelerini ve evrilen sistem bileşenlerini belgelemektedir. Tüm mimari kararlar 100/100 backend ve 60/60 frontend otomatik testleri ile %100 doğrulanmış ve `0ffb2a4` commit hash'i ile kayıt altına alınmıştır.

---

## 1. Karar Sınıfları

- **[PDF gereksinimi]:** Resmî ödev PDF'inin açıkça istediği davranış veya kısıt.
- **[Teknik karar]:** Ekibin seçtiği uygulama yaklaşımı ve kütüphane seçimi.
- **[Evrilmiş karar]:** İlk kararın uygulama veya sprintler sırasında geliştirilmiş hali.

---

## 2. Mimari Karar Kayıtları (ADR)

### ADR-001 - Backend: ASP.NET Core 10
- **Karar:** **[Teknik karar]** API, ASP.NET Core 10 Minimal API mimarisiyle inşa edilmiştir.
- **Bağlam ve Gerekçe:** Yerleşik DI, JWT kimlik doğrulama, politika tabanlı yetkilendirme, rate limiting, Problem Details ve test sunucusu desteği projenin güvenlik ve test ihtiyaçlarına uygundur.
- **Uygulama Kanıtı:** `backend/src/IFS.AIWeb.Api/Program.cs`, `backend/IFS.AIWeb.slnx`.

### ADR-002 - Frontend: React 19, Vite ve TypeScript
- **Karar:** **[Teknik karar]** İstemci, Vite tabanlı React 19 ve TypeScript SPA uygulamasıdır; ek ağır UI kütüphaneleri yerine modüler Vanilla CSS (`styles.css`) kullanılmıştır.
- **Bağlam ve Gerekçe:** Yüksek performans, tam tip güvenliği, özelleştirilebilir tasarım ve hızlı Vite derleme süreci.
- **Uygulama Kanıtı:** `frontend/package.json`, `frontend/src/App.tsx`, `frontend/src/auth/Auth.tsx`, `frontend/src/pages/AppPage.tsx`.

### ADR-003 - Veritabanı: PostgreSQL ve EF Core
- **Karar:** **[Teknik karar]** Kullanıcılar, refresh token kayıtları ve özet denetim kayıtları PostgreSQL veritabanında EF Core Code-First yaklaşımıyla tutulur.
- **Bağlam ve Gerekçe:** Rol, kullanıcı, token ailesi ve özet kayıtları ilişkisel bütünlük, indeks ve işlemsel güncelleme gerektirir. `users`, `refresh_tokens` ve `summary_records` tabloları üzerinde benzersiz hash indeksleri bulunur.
- **Uygulama Kanıtı:** `backend/src/IFS.AIWeb.Infrastructure/AuthDbContext.cs`, `Migrations/`.

### ADR-004 - Clean Architecture Katmanları
- **Karar:** **[Teknik karar]** Backend 4 ayrık katmandan oluşur: `Domain`, `Application`, `Infrastructure` ve `Api`.
- **Bağlam ve Gerekçe:** Bağımlılıklar içe doğrudur (`Api -> Infrastructure -> Application -> Domain`). LLM, kalıcılık ve güvenlik bağımlılıkları arayüzler aracılığıyla tersine çevrilmiştir (DIP).
- **Uygulama Kanıtı:** `backend/src/IFS.AIWeb.Domain/`, `IFS.AIWeb.Application/`, `IFS.AIWeb.Infrastructure/`, `IFS.AIWeb.Api/`.

### ADR-005 - SOLID ilkeleri ve Dependency Injection
- **Karar:** **[Teknik karar]** Tüm dış bağımlılıklar (`ILlmSummarizer`, `IPdfReportGenerator`, `IUserRepository`, `IRefreshTokenRepository`, `IPasswordService`, `IClock`) küçük arayüzlerle soyutlanmış ve IoC container üzerinden injection yapılmıştır.
- **Uygulama Kanıtı:** `backend/src/IFS.AIWeb.Infrastructure/DependencyInjection.cs`.

### ADR-006 - LLM Sağlayıcı Soyutlaması ve Groq Yapılandırılmış Çıktısı
- **Karar:** **[Evrilmiş karar]** `ILlmSummarizer` soyutlaması üzerinden Groq Chat Completions HTTP API'si (`openai/gpt-oss-120b`) kullanılmıştır.
- **Evrim:** Max completion token sınırı **500'den 900'e** çıkarılmış; `response_format.type = json_schema`, `strict = true` ile kesin yapılandırılmış JSON çıktısı zorunlu kılınmıştır.
- **Uygulama Kanıtı:** `backend/src/IFS.AIWeb.Infrastructure/SummarizationInfrastructure.cs`.

### ADR-007 - Access/Refresh Token ve Pasif Kullanıcı Güvenlik Politikası
- **Karar:** **[Evrilmiş karar]** 15 dakikalık imzalı JWT access token istemci belleğinde; 7 günlük refresh token ise `HttpOnly`, `SameSite=Strict`, `Secure` cookie olarak saklanır. Veritabanında yalnız SHA-256 hash'i tutulur.
- **Pasif Kullanıcı Güncellemesi:** Yönetici tarafından pasife alınan bir kullanıcı giriş yapmaya çalıştığında jenerik hata yerine `AccountInactiveException` fırlatılır ve HTTP `403 Forbidden` cevabı ile `"Hesabınız pasife alınmıştır. Lütfen yönetici ile iletişime geçin."` mesajı döndürülür. Pasif kullanıcının var olan tüm refresh oturumları anında iptal edilir.
- **Uygulama Kanıtı:** `AuthContracts.cs`, `AuthService.cs`, `Program.cs`, `frontend/src/auth/Auth.tsx`.

### ADR-008 - Adaptif Özet Politikası ve Prompt Sürümü (`summary-v5`)
- **Karar:** **[Evrilmiş karar]** Metin uzunluğuna göre adaptif kısıtlar uygulayan `SummaryLengthPolicy` geliştirilmiştir.
- **Evrim:** Prompt sürümü `summary-v5` seviyesine yükseltilmiştir. Uzun kaynak metinlerde (2000+ karakter) yüzeysel 1-2 cümlelik özetler yerine, kilit tarihleri, olayları, aktörleri ve kararları kapsayan zengin, tutarlı ve yapılandırılmış özet üretilmesi sağlayan yönlendirmeler eklenmiştir.
- **Uygulama Kanıtı:** `SummaryLengthPolicy.cs`, `Summarization.cs` (`SummarizationPromptBuilder`).

### ADR-009 - Sıkı Structured Output Doğrulaması ve Kontrollü Retry
- **Karar:** **[Teknik karar]** Groq API'sinden dönen JSON yanıtının `quality` ve `summary` alanları şema kontrolünden geçirilir. Yalnızca kısıtlı ve kanıtlanmış JSON şema uyuşmazlığı durumlarında en çok 1 defa kontrollü retry yapılır.

### ADR-010 - Kullanıcı Başına Sliding Window Rate Limiting
- **Karar:** **[Bonus karar]** `POST /api/summaries` endpoint'i JWT `sub` doğrulamasına dayalı Sliding Window algoritmasıyla (5 izin / 60 saniye / 60 segment) korunur. Limiti aşan isteklere HTTP `429 Too Many Requests` ve `Retry-After` header'ı dönülür.
- **Uygulama Kanıtı:** `Program.cs` (`SummaryRateLimitPolicy`).

### ADR-011 - İçerik Kalıcılığı, Log Maskeleme ve 30 Günlük Saklama
- **Karar:** **[Bonus karar]** Başarılı özetler ve kaynak metinler 30 günlük `ExpiresAtUtc` süresiyle saklanır. Admin log ekranında kullanıcı mahremiyetini korumak amacıyla metinlerin yalnız ilk 160 karakterlik önizlemesi (beyaz alanları temizlenmiş olarak) gösterilir.

### ADR-012 - Merkezi ve Güvenli Hata Yönetimi
- **Karar:** **[Teknik karar]** API genelinde `WriteError` Problem Details handler'ı kullanılarak hassas veriler sızdırılmadan Türkçe ve anlaşılır hata yanıtları üretilir.

### ADR-013 - İstemci Kolaylığı ve Sunucu Tarafı Güvenlik
- **Karar:** **[Teknik karar]** Frontend metin doğrulama kontrolleri (karakter sayısı, min kelime sayısı) UX içindir; sunucu tarafı `AuthValidation` ve `SummaryValidation` aşılmaz yetkili sınırdır.

### ADR-014 - Yapılandırma ve Secret Yönetimi
- **Karar:** **[PDF gereksinimi]** API anahtarları ve veritabanı şifreleri koda yazılmaz; `.env` ve .NET User Secrets üzerinden yönetilir.

### ADR-015 - Bütüncül Test Stratejisi
- **Karar:** **[Teknik karar]** Backend tarafında Domain, Application, Integration (Testcontainers PostgreSQL), Frontend tarafında Vitest ve React Testing Library ile kapsayıcı test paketi kurulmuştur. Toplam 100 backend ve 60 frontend testi %100 başarılıdır.

### ADR-016 - Admin Denetlenebilirliği ve Yönetim Sınırları
- **Karar:** **[Teknik karar]** `/api/admin` endpoint'leri yalnız `AdminOnly` yetkisiyle korunur. Admin kullanıcı oluşturabilir, şifre sıfırlayabilir, kullanıcıyı aktif/pasif yapabilir ve mahremiyet korumalı logları inceleyebilir.

### ADR-017 - Birleşik PDF İndirme Mimarisi (PDFsharp & Embedded Font Resolver)
- **Karar:** **[Ürün kararı]** Özet detay sayfasından indirilebilen kurumsal PDF raporları için permissive MIT lisanslı `PDFsharp 6.1.1` kütüphanesi seçilmiştir.
- **Gerekçe & Font Çözümü:** Cross-platform Docker/Linux ortamlarında Türkçe karakter sorunu (UTF-8) yaşamamak için `NotoSans-Regular.ttf` ve `NotoSans-Bold.ttf` font dosyaları `IFS.AIWeb.Application` derlemesine gömülü kaynak (`EmbeddedResource`) olarak eklenmiş ve özel `EmbeddedFontResolver` ile tescil edilmiştir.
- **Performans & Rate Limit:** `GET /api/summaries/{id}/pdf` endpoint'i POST rate-limit kotasını tüketmez.
- **Uygulama Kanıtı:** `EmbeddedFontResolver.cs`, `PdfReportGenerator.cs`, `IPdfReportGenerator.cs`, `Program.cs`.

### ADR-018 - Sıkı Sahiplik Kontrolü & Admin Bypass Engelleme (PDF ve Detay)
- **Karar:** **[Güvenlik kararı]** Özet detayına erişim ve PDF indirme işlemlerinde katı sahiplik kontrolü (`UserId == CurrentUserId`) uygulanır.
- **Gerekçe:** Yönetici (Admin) rolündeki bir kullanıcı dahi olsa, başka bir kullanıcının özet ID'si ile PDF indirmeye çalıştığında sistem `SummaryNotFoundException` fırlatarak HTTP `404 Not Found` yanıtı döndürür. Böylece Admin yetkisi kötüye kullanılarak başkalarının özel özetlerinin indirilmesi engellenir.
- **Uygulama Kanıtı:** `SummarizationService.cs` (`DownloadPdfAsync`, `DetailAsync`), `SummaryApiTests.cs`.

### ADR-019 - Vibrant Pink & Neon Violet AI Tema Mimarisi
- **Karar:** **[UI/UX Kararı]** İstemci arayüzü kasvetli tonlardan çıkarılarak modern, enerjik ve şık **"Vibrant Pink & Neon Violet AI"** tasarım diline dönüştürülmüştür.
- **Özellikler:** `Plus Jakarta Sans` tipografisi, ferah lavanta zemin (`#FAF5FF`), yumuşatılmış kart hatları (`rounded-2xl`), mor-pembe şeffaf gölgeler ve pembeden mora uzanan gradyan butonlar (`linear-gradient(135deg, #EC4899 0%, #8B5CF6 100%)`).
- **Uygulama Kanıtı:** `frontend/src/styles.css`.
