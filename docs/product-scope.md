# Ürün Kapsamı ve Teslim Durumu

## 1. Ürün Amacı ve Özeti

**IFS-AI-Web**, kullanıcının yazdığı paragraf veya metinleri güvenli ve kontrollü bir yapay zeka prompt akışıyla (`summary-v5`) LLM'e (Groq `openai/gpt-oss-120b`, `max_completion_tokens: 500`) göndererek kaynağa sadık, Türkçe veya İngilizce detaylı özetler üreten; özetleri indirilebilir kurumsal PDF raporlarına dönüştüren; kimlik doğrulama, rol tabanlı yetkilendirme, yönetici denetim kaydı (audit log) ve kullanıcı yönetimi sunan modern bir web uygulamasıdır.

Tüm ürün gereksinimleri, 128 backend test vakası ve 64 frontend test vakasıyla doğrulanmış olup incelemenin başladığı temel commit `0bcac4b`'dir.

---

## 2. Hedef Kullanıcılar ve Roller

### User (Standart Kullanıcı)
- Kayıt olur, giriş yapar, metin özetler (Türkçe/İngilizce dil seçeneği ile).
- Son özetlerini Kitaplık ekranında görüntüler ve başlık/özet üzerinde arama yapabilir.
- İstediği özetin detay ekranından Unicode ve Türkçe glif destekli Noto Sans fontlu **Birleşik PDF Raporu** indirebilir.
- Özet kaydındaki seçili dile göre Web Speech API için `tr-TR` veya `en-US` konuşma dili atanarak çalışan sesli okuma özelliğini kullanabilir.
- Özetlere `Useful` veya `NotUseful` geri bildirimi verebilir, pano kopyalama yapabilir, özetleri sabitleyebilir.
- Admin sayfalarına erişemez.

### Admin (Yönetici)
- Yönetici paneline erişir (`/admin`, `/admin/users`, `/admin/logs`).
- Yeni kullanıcı oluşturabilir (User veya Admin rolünde), kullanıcı şifrelerini sıfırlayabilir, kullanıcı durumunu Aktif/Pasif yapabilir.
- Kullanıcı mahremiyetini koruyan (en çok 160 karakterlik önizleme) özet denetim loglarını süzüp listeleyebilir.
- Son 7 günde oluşturulan özetler için mevcut kullanıcı memnuniyeti dağılımını (`👍 %88`) ve özetleme yapan benzersiz kullanıcı sayısını inceleyebilir.
- Devam eden ağ istekleri bileşen unmount olduğunda `AbortController` ile otomatik olarak iptal edilir.
- Güvenlik Kararı: Admin dahi olsa başkasının özel özet detayını veya PDF'ini indiremez (404 Not Found alır).

---

## 3. Tamamlanan Temel Senaryolar ve Ürün Kapsamı

1. **Kayıt ve Giriş:** Kullanıcı adı ve parola ile güvenli kimlik doğrulama. SHA-256 hash'li HttpOnly refresh cookie (aynı `FamilyId` ailesine bağlı yenileme rotasyonu) ve bellekte tutulan JWT access token (HMAC-SHA256).
2. **Pasif Kullanıcı Giriş Uyarısı:** Pasife alınan kullanıcılara giriş sırasında jenerik hata yerine açık uyarı: *"Hesabınız pasife alınmıştır. Lütfen yönetici ile iletişime geçin."* (HTTP 403 Forbidden).
3. **Adaptif Özetleme (`summary-v5`):** Metin uzunluğuna göre otomatik adaptif kısıtlar. Uzun kaynak metinlerde tarih, olay ve kilit noktaları içeren zengin özet üretimi (`max_completion_tokens: 500`).
4. **Birleşik PDF Rapor İndirme:** `PDFsharp 6.1.1` ve gömülü `NotoSans` font çözücü ile Türkçe glif destekli PDF indirme (`GET /api/summaries/{id}/pdf`).
5. **Kullanıcı Başına Rate Limiting:** `POST /api/summaries` endpoint'inde 5 izin / Sliding Window sınırlaması. Backend dinamik kalan süreyi `Retry-After` header'ında ve JSON `retryAfterSeconds` alanında iletir. PDF indirme ve feedback kotadan düşmez.
6. **Yönetici Log ve Kullanıcı Paneli:** Filtrelenebilir kullanıcı listesi, şifre sıfırlama, pasife alma, mahremiyet korumalı loglar ve 7 günlük kullanıcı memnuniyeti dağılımı.
7. **Vibrant Pink & Neon Violet AI UI Teması:** Modern `Plus Jakarta Sans` tipografisi, yumuşatılmış kart yapıları ve pembe-mor gradyan aksiyon butonları.

---

## 4. Test Kapsamı ve Kalite Metrikleri

- **Backend Test Kapsamı:** 128 Test Vakası BAŞARILI (%100).
  - `IFS.AIWeb.Domain.Tests`: 4 Passed
  - `IFS.AIWeb.Application.Tests`: 64 Passed
  - `IFS.AIWeb.IntegrationTests` (PostgreSQL 18): 60 Passed (50 test metodu / 60 çalıştırılan test vakası)
- **Frontend Test Kapsamı:** 64 / 64 Test BAŞARILI (%100).
- **Frontend Lint (ESLint):** 0 Hata, 0 Uyarı (%100 Temiz).
- **Frontend Production Build:** Vite & TypeScript 0 hata ile derlenir.

---

## 5. Çözülen Ürün Kararları

- **[Çözüldü]** Giriş ve kimlik doğrulama kullanıcı adı ile yapılır.
- **[Çözüldü]** Türkçe ve İngilizce özetleme desteği mevcuttur.
- **[Çözüldü]** En yeni 7 uygun özet Kitaplıkta listelenir.
- **[Çözüldü]** PDF indirme işlemi strict ownership kuralına tabidir (Admin bypass engellenmiştir).
- **[Çözüldü]** Pasif kullanıcı için 403 Forbidden ve özel Türkçe uyarı mesajı eklenmiştir.
- **[Çözüldü]** Proje yayın ve teslim belgeleri tamamlanmıştır.
