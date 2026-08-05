# IFS-AI-Web: Uçtan Uca Mimari, Güvenlik, Canlı Demo ve Ürün Rehberi

> **Sürüm / Temel Commit:** İncelemenin başladığı temel commit: `0bcac4b` (Sürüm: Final Teslim Çalışma Seti)  
> **Mimari:** ASP.NET Core 10 (Clean Architecture, Minimal API) + React 19 / TypeScript (Vite SPA)  
> **Veritabanı & Altyapı:** PostgreSQL 18 (`postgres:18-alpine`, EF Core 10), Docker, Groq LLM API (`openai/gpt-oss-120b`, `max_completion_tokens: 500`), PDFsharp 6.1.1, Web Speech API (Native TTS)  
> **Test Kapsamı:** 128 Backend Test Vakası (%100 Başarılı), 64 Frontend Test Vakası (%100 Başarılı), 0 ESLint Hatası, %100 Başarılı Prodüksiyon Derlemesi  

---

## 🏛️ BÖLÜM 1: KATMANLI MİMARİ (Clean Architecture)

Bir yazılım projesini baştan sona spagetti kod yazarak inşa etmek, duvarları, elektrik tesisatını ve su borularını birleştirip karmaşık bir yığın oluşturmaya benzer. Bir boru patladığında tüm binayı yıkmanız gerekir.

**IFS-AI-Web**, bu sorunu çözmek için dünya standartlarında kabul gören **Clean Architecture (Temiz Mimari)** prensipleriyle tasarlanmıştır. Sistemimizi 5 yıldızlı lüks bir restorana ve akıllı bir gökdelene benzetebiliriz.

```
                    ┌────────────────────────────────────────┐
                    │          İSTEMCİ (FRONTEND)            │
                    │   React 19 SPA (Restoran Salonu)       │
                    └───────────────────┬────────────────────┘
                                        │ (HTTP / JSON / Cookie)
                    ┌───────────────────▼────────────────────┐
                    │             API KATMANI                │
                    │ ASP.NET Core 10 (Karşılama & Garson)   │
                    └───────────────────┬────────────────────┘
                                        │
                    ┌───────────────────▼────────────────────┐
                    │         APPLICATION KATMANI            │
                    │   Kullanım Senaryoları (Aşçıbaşı)      │
                    └──────────┬──────────────────┬──────────┘
                               │                  │
         ┌─────────────────────▼──────┐     ┌─────▼──────────────────────┐
         │       DOMAIN KATMANI       │     │   INFRASTRUCTURE KATMANI   │
         │ Temel İş Kuralları (Reçete)│◄────┤  Teknik Ekipman & Malzeme  │
         └────────────────────────────┘     └────────────────────────────┘
```

---

### 1.1 Katmanlar ve Sorumluluk Dağılımı

#### 1. İstemci (Frontend - React 19 SPA) ➔ *Restoranın Müşteri Salonu*
Kullanıcının gördüğü masalar, menüler, ışıklar ve pembe-mor (Vibrant Pink & Violet) temadır. Mutfakta yemeğin nasıl pişirildiğini görmez; yalnızca garsona (API) siparişi verir ve sunulan yemeği (JSON / PDF / Sesli Okuma) tüketir.

#### 2. API Katmanı (`IFS.AIWeb.Api`) ➔ *Restoran Karşılama ve Garson Ekibi*
Müşteriden siparişi alan, müşterinin kimlik kartını (JWT) ve masadaki oturum iznini (Cookie) kontrol eden ilk temas noktasıdır. Garson yemeği kendi pişirmez; siparişi aşçıbaşına (Application) iletir ve gelen sonucu müşteriye sunar.

#### 3. Application Katmanı (`IFS.AIWeb.Application`) ➔ *Aşçıbaşı ve Mutfak Yönetimi*
Application katmanı kullanım senaryolarını, komut doğrulamalarını, DTO'ları ve servis portlarını yönetir. PDF üretimi mevcut sürümde Application projesindeki ayrı bir raporlama bileşeninde (`PdfReportGenerator`) bulunmaktadır.

#### 4. Domain Katmanı (`IFS.AIWeb.Domain`) ➔ *Restoranın Asırlık Gizli Reçeteleri ve İş Kuralları*
Restoranın var olma sebebidir. `User`, `SummaryRecord`, `SummaryFeedback`, `UserRole`, `SummaryLanguage` varlıklarının ve iş kurallarının tanımlandığı katmandır. Dış kütüphanelere veya veritabanı sürücülerine bağımlı değildir.

#### 5. Infrastructure Katmanı (`IFS.AIWeb.Infrastructure`) ➔ *Mutfak Ekipmanları, Fırınlar ve Depo*
PostgreSQL 18 veritabanı adaptörleri, Groq HTTP istemcisi (`max_completion_tokens: 500`), EF Core DbContext ve kriptografik servisler bu katmandadır.

---

### 1.2 Mimaride Bağımlılık Yönü (Dependency Inversion)

Clean Architecture'da bağımlılık okları daima içe doğru bakar. Sağlayıcı veya veritabanı değişikliklerinde Domain ve temel Application kullanım senaryoları büyük ölçüde korunur; değişiklikler çoğunlukla Infrastructure adaptörleri, yapılandırma, sağlayıcıya özgü SQL/migration'lar ve entegrasyon testlerinde yoğunlaşır.

PostgreSQL'e özgü `FOR UPDATE` satır kilitleri, `COUNT(*) FILTER` sorguları ve Npgsql sürücüleri Infrastructure katmanında izole edilmiştir.

---

## 🗄️ BÖLÜM 2: VERİTABANI VE VERİ GÜVENLİĞİ ŞEMASI (PostgreSQL 18 & EF Core)

Uygulamanın kalıcı hafızası PostgreSQL 18 veritabanıdır (`postgres:18-alpine`). EF Core 10 Code-First yaklaşımıyla yönetilir.

```
       ┌─────────────────┐             ┌─────────────────┐
       │      users      │1           *│  refresh_tokens │
       ├─────────────────┼────────────┼─────────────────┤
       │ id (PK)         │             │ id (PK)         │
       │ username        │             │ user_id (FK)    │
       │ password_hash   │             │ token_hash      │
       │ role            │             │ family_id       │
       │ is_active       │             │ expires_at_utc  │
       └────────┬────────┘             └─────────────────┘
                │1
                │
                │*
       ┌────────┴────────┐
       │ summary_records │
       ├─────────────────┤
       │ id (PK)         │
       │ user_id (FK)    │
       │ input_text      │
       │ summary_text    │
       │ feedback        │
       │ expires_at_utc  │
       └─────────────────┘
```

---

### 2.1 Tablolar ve Veri Modeli

#### 1. `users` Tablosu
Kullanıcı hesaplarını saklar. `normalized_username` benzersiz indeksi (`IX_users_normalized_username`) üzerinden çakışma ve oturum açma doğrulaması yapılır. `is_active` pasif hesapların sistem erişimini engeller.

#### 2. `refresh_tokens` Tablosu
Uzun süreli oturum yenileme anahtarlarını tutar. Güvenlik nedeniyle düz metin token veritabanına asla yazılmaz; yalnız **SHA-256 hash'i** (`token_hash`) saklanır. `family_id` indeksi token rotasyonu ve hırsızlık tespitini takip eder.

#### 3. `summary_records` Tablosu
Özetleme geçmişini saklar. `feedback` alanı `SummaryFeedback` enum'u (`Useful` veya `NotUseful`) ile veritabanı seviyesinde `chk_summary_records_feedback` check constraint'i üzerinden korunur.

---

### 2.2 Parola Güvenliği (ASP.NET Identity PasswordHasher)

Sistemde şifreler **KESİNLİKLE DÜZ METİN SAKLANMAZ**. ASP.NET Core Identity `PasswordHasher<User>` ile tek yönlü PBKDF2 (HMAC-SHA512, 128-bit salt, 100.000+ iterasyon) kullanılarak şifrelenir.

---

## 🔒 BÖLÜM 3: TOKEN, COOKIE VE ÇOK KATMANLI GÜVENLİK MİMARİSİ

```
       ┌─────────────────────────────────────────────────────────────┐
       │                        TARAYICI                             │
       │                                                             │
       │  ┌──────────────────────┐     ┌──────────────────────────┐  │
       │  │  React Bellek (RAM)  │     │   HttpOnly Cookie Store  │  │
       │  │                      │     │                          │  │
       │  │  Access Token (JWT)  │     │  Refresh Token (Cookie)  │  │
       │  │  Süre: 15 Dakika     │     │  Süre: 7 Gün             │  │
       │  │  JS Okuyabilir: EVET │     │  JS Okuyabilir: HAYIR!   │  │
       │  └──────────┬───────────┘     └────────────┬─────────────┘  │
       └─────────────┼──────────────────────────────┼────────────────┘
                     │ (Authorization Header)       │ (Otomatik Cookie)
                     ▼                              ▼
       ┌─────────────────────────────────────────────────────────────┐
       │                      ASP.NET CORE API                       │
       └─────────────────────────────────────────────────────────────┘
```

---

### 3.1 Güvenlik Önlemleri ve Risk Değerlendirmesi

JWT access token, sunucu tarafındaki simetrik imzalama anahtarıyla **HMAC-SHA256** algoritması kullanılarak imzalanır.

> **Güvenlik Beyanı:** Bellek içi access token, HttpOnly ve SameSite=Strict refresh cookie, kısa token ömrü, Origin doğrulaması ve backend yetkilendirme kontrolleri; XSS ve CSRF kaynaklı token çalma ve yetkisiz işlem risklerini önemli ölçüde azaltan çok katmanlı güvenlik önlemleridir. Bu önlemler riski azaltır; bütün XSS veya CSRF ihtimallerini mutlak olarak ortadan kaldırdığı iddia edilmez.

---

### 3.2 Token Family Rotation ve PostgreSQL `SELECT ... FOR UPDATE`

Refresh token yenilendiğinde eski token iptal edilir ve yeni token ailesi oluşturulur. Önceden kullanılmış bir token sunulduğunda hırsızlık tespiti tetiklenir ve aileye ait tüm token'lar iptal edilir.

Yenileme isteğinde yarış durumlarını (race condition) önlemek için PostgreSQL satır düzeyinde kilitleme kullanılır:

```sql
SELECT * FROM refresh_tokens 
WHERE token_hash = @hash 
FOR UPDATE;
```

---

## 🧭 BÖLÜM 4: ADIM ADIM İSTEK DÖNGÜLERİ VE F12 NETWORK REHBERİ

### 4.1 Özet Oluşturma (`POST /api/summaries`)

1. İstemci `POST /api/summaries` isteği gönderir.
2. `SummaryPerUser` Sliding Window rate limiter isteği kontrol eder.
3. Prompt Builder (`summary-v5`) ve `STRICT LANGUAGE RULE` hazırlanır.
4. Groq API'sine `openai/gpt-oss-120b` modeli, `strict: true` JSON şeması ve `max_completion_tokens: 500` parametresi ile istek atılır.
5. Sonuç veritabanına kaydedilir ve istemciye döndürülür.
6. Kota aşıldığında backend Sliding Window ile hesaplanan gerçek kalan süreyi pozitif tam saniye olarak hem `Retry-After` header'ında hem de `retryAfterSeconds` JSON alanında döndürür.

---

### 4.2 Geri Bildirim API Sözleşmesi (`PUT /api/summaries/{id}/feedback`)

- **HTTP Endpoint:** `PUT /api/summaries/{id}/feedback`
- **Request Body:**
  ```json
  {
    "value": "Useful"
  }
  ```
- **Response Body (200 OK):**
  ```json
  {
    "value": "Useful",
    "updatedAtUtc": "2026-08-05T10:30:00Z"
  }
  ```
- **Özellikler:** Sıkı sahiplik kontrolü yapılır (`WHERE id = @id AND user_id = @userId`). Admin dahil başkasının özetine feedback verilemez (404 Not Found). İşlem idempotenttir; değer değişmezse `FeedbackUpdatedAtUtc` güncellenmez. Geri bildirim isteği Groq API çağırmaz ve rate limit kotasını tüketmez.

---

### 4.3 PDF Rapor İndirme (`GET /api/summaries/{id}/pdf`)

- PDF üretimi bellek içi (`MemoryStream`) çalışır.
- PDF içerisine gömülü Unicode ve Türkçe glif desteğine sahip Noto Sans TrueType fontları (`NotoSans-Regular.ttf` ve `NotoSans-Bold.ttf`, SIL Open Font License 1.1) kullanılmaktadır.
- Sıkı sahiplik kontrolü uygulanır. İndirme işlemi rate limit kotasını tüketmez.

---

### 4.4 Metinden Sese (TTS) Sesli Okuma

- Özet kaydındaki seçili dile göre Web Speech API için `tr-TR` veya `en-US` konuşma dili atanır.
- Sayfa değişiminde veya özet geçişinde `speechSynthesis.cancel()` ile kaynaklar temizlenir.
- Tarayıcı desteği olmadığında sesli okuma butonu pasif konuma geçer.

---

### 4.5 Kitaplık Arama Filtresi

- Kitaplıkta türetilen başlık ve özet metni üzerinde büyük/küçük harf duyarsız istemci tarafı arama yapılır.
- Arama sırasında detay endpoint'leri otomatik olarak çağrılmaz.

---

### 4.6 Admin İstatistikleri ve Ağ Yönetimi

- Admin paneli istatistik uç noktası (`GET /api/admin/statistics/seven-days`) yanıtından alınan verilerle **"Son 7 günde oluşturulan özetler için mevcut kullanıcı memnuniyeti dağılımı"** rozeti (`👍 %88`) sunulur.
- Özetleme yapan benzersiz kullanıcı sayısı gösterilir.
- Admin sayfalarında devam eden veri istekleri bileşen unmount olduğunda `AbortController` ile iptal edilir; iptal kaynaklı `AbortError` kullanıcı hatası olarak gösterilmez.

---

## 📊 BÖLÜM 5: HATA KODLARI MAP’İ

| HTTP Kodu | Anlamı | Sunucu Mesajı | İstemci Davranışı |
|---|---|---|---|
| **400 Bad Request** | Validation Hatası | "Doğrulama hatası" | Form alanlarının altında Türkçe uyarılar gösterilir. |
| **401 Unauthorized** | Oturum Yok/Süresi Dolmuş | "Kimlik doğrulama başarısız" | Arka planda sessizce refresh denenir; başarısızsa `/login`'e yönlendirir. |
| **403 Forbidden** | Yetkisiz Rol / Pasif Kullanıcı | "Erişim engellendi" | Kırmızı Türkçe pasif kullanıcı uyarısı gösterilir. |
| **404 Not Found** | Kayıt Yok / Başkasının Özeti | "Özet bulunamadı" | Güvenli "Bulunamadı" uyarısı sunulur. |
| **409 Conflict** | Mükerrer Kullanıcı Adı | "Kullanıcı adı kullanılıyor" | İnput altında çakışma uyarısı verilir. |
| **429 Too Many Req.**| Rate Limit Kotası Aşıldı | "İstek sınırı aşıldı" | Backend'in bildirdiği kalan süreye göre canlı geri sayım başlatılır. |
| **502 Bad Gateway** | Groq API Ulaşılamıyor | "Özetleme hizmeti kullanılamıyor" | "AI servisine ulaşılamıyor" uyarısı sunulur. |

---

## 🧪 BÖLÜM 6: TEST VE KALİTE GÜVENCE DEĞERLERİ

Projedeki çalıştırılan tanımlı test vakalarının tamamı başarılıdır:

- **Domain Tests:** 4 Passed
- **Application Tests:** 64 Passed
- **Integration Tests (PostgreSQL 18):** 60 Passed (32 test metodu / 60 teori vakası)
- **Frontend Vitest Suite:** 64 Passed
- **ESLint:** 0 Hata, 0 Uyarı
- **Production Build:** 0 Hata

---

## 📸 BÖLÜM 7: EKRAN GÖRÜNTÜLERİ VE DEMO REHBERİ

- **Ekran Görüntüleri Durumu**: Manuel teslim bekliyor (Canlı demo sırasında güncellenecektir).

---

## 🎯 SONUÇ

Proje; katmanlı mimari, güvenli authentication, sahiplik kontrollü veri erişimi, provider hata yönetimi, PDF raporlama ve otomatik testleriyle production-oriented mühendislik pratikleri uygulanarak geliştirilmiştir. Dağıtık rate limiting, fiziksel retention worker, gözlemlenebilirlik ve CI/CD gibi başlıklar canlı üretim ortamı için sonraki geliştirme alanlarıdır.
