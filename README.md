# IFS AI-Web

![License](https://img.shields.io/badge/license-MIT-blue.svg)
![NET](https://img.shields.io/badge/.NET-10.0-purple.svg)
![React](https://img.shields.io/badge/React-19.0-blue.svg)
![TypeScript](https://img.shields.io/badge/TypeScript-5.7-blue.svg)
![Tests](https://img.shields.io/badge/tests-192%20passed-brightgreen.svg)

**IFS AI-Web**, .NET 10 Clean Architecture mimarisi, React 19 / TypeScript Vite SPA ve PostgreSQL 18 veritabanı üzerinde inşa edilmiş, yapay zeka destekli Türkçe/İngilizce metin özetleme, kurumsal PDF raporlama, yönetici denetim paneli ve gelişmiş güvenlik mekanizmaları sunan web uygulamasıdır.

---

## 🌟 Öne Çıkan Özellikler

- **🤖 Adaptif Yapay Zeka Özetleme (`summary-v5`)**: Groq Chat Completions (`openai/gpt-oss-120b`, `max_completion_tokens: 500`) entegrasyonu ile kaynak metin uzunluğuna uyum sağlayan, kilit tarihleri, olayları ve olguları detaylıca özetleyen yapılandırılmış AI akışı.
- **📄 Birleşik PDF Rapor İndirme**: Permissive MIT lisanslı `PDFsharp 6.1.1` ve gömülü `NotoSans` TrueType font çözücü ile Unicode ve Türkçe glif desteğine sahip, şık ve indirilebilir PDF özet raporları.
- **🔊 Metinden Sese (TTS) Sesli Okuma**: Özet kaydındaki seçili dile göre Web Speech API için `tr-TR` veya `en-US` konuşma dili atanarak çalışan sesli okuma ve durdurma özelliği.
- **🎨 Vibrant Pink & Neon Violet AI Teması**: `Plus Jakarta Sans` tipografisi, yumuşatılmış kart hatları (`rounded-2xl`), mor-pembe soft gölgeler ve canlı gradyan butonlarla modern UI/UX deneyimi.
- **🔐 Güvenli Kimlik Doğrulama & Oturum Yönetimi**: JWT access token (bellekte, HMAC-SHA256), SHA-256 hash'li HttpOnly refresh cookie, aynı FamilyId ile token rotasyonu ve eşzamanlı yenileme koruması.
- **🚫 Pasif Kullanıcı Koruması**: Pasife alınan kullanıcı girişlerinde jenerik hata yerine açık ve kibar Türkçe uyarı: *"Hesabınız pasife alınmıştır. Lütfen yönetici ile iletişime geçin."* (HTTP 403 Forbidden).
- **🔒 Sıkı Sahiplik & Mahremiyet (Admin Bypass Engelleme)**: Kullanıcılar yalnız kendi özetlerinin detayına ve PDF'ine erişebilir. Yönetici (Admin) dahi başkasının özet ID'sini istediğinde 404 Not Found alır. Admin log ekranında kullanıcı metinleri en çok 160 karakterlik beyaz alanı temizlenmiş önizlemeyle gösterilir.
- **⚡ Kullanıcı Başına Rate Limiting**: `POST /api/summaries` endpoint'i kullanıcı bazlı Sliding Window algoritmasıyla (5 izin / kalan süre dinamik Retry-After) korunur. PDF indirme ve feedback işlemleri rate-limit kotasını tüketmez.

---

## 🛠️ Önkoşullar

- **.NET SDK 10.0+**
- **Node.js 20+ ve npm**
- **Docker Desktop** (PostgreSQL 18 konteyneri için)
- **EF Core CLI** (`dotnet tool install --global dotnet-ef --version 10.*`)

---

## 🚀 Adım Adım Kurulum ve Çalıştırma

### 1. Yerel Yapılandırma ve Secret Hazırlığı

Proje kökündeki `.env.example` dosyasını `.env` adıyla kopyalayın ve güçlü rastgele değerler atayın:

```powershell
Copy-Item .env.example .env
```

Groq API anahtarını API başlangıç projesinin .NET User Secrets deposuna ekleyin:

```powershell
dotnet user-secrets set "Groq:ApiKey" "<your-groq-api-key>" --project backend/src/IFS.AIWeb.Api
```

> **Not:** API anahtarı yalnız backend tarafından kullanılır; istemciye, loglara veya kaynak koda sızdırılmaz.

---

### 2. PostgreSQL 18 Veritabanı ve Migration

Docker ile PostgreSQL 18 konteynerini başlatın ve veritabanı migration'ını uygulayın:

```powershell
# PostgreSQL 18 konteynerini başlatın
docker compose up -d postgres

# Veritabanı şemasını güncelleyin
dotnet ef database update --project backend/src/IFS.AIWeb.Infrastructure --startup-project backend/src/IFS.AIWeb.Api
```

---

### 3. Backend Uygulamasını Çalıştırma

```powershell
# Bağımlılıkları yükleyin ve backend'i çalıştırın
dotnet restore backend/IFS.AIWeb.slnx
dotnet run --project backend/src/IFS.AIWeb.Api/IFS.AIWeb.Api.csproj
```

- **API Adresi:** `http://localhost:5099`
- **Sağlık Kontrolü (Health Check):** `http://localhost:5099/health`

---

### 4. Frontend Uygulamasını Çalıştırma

Ayrı bir terminal penceresinde frontend geliştirme sunucusunu başlatın:

```powershell
cd frontend
npm install
npm run dev
```

- **Uygulama Adresi:** `http://localhost:5173`

---

## 🧪 Test Çalıştırma ve Kalite Metrikleri

Projedeki çalıştırılan tanımlı test vakalarının tamamı başarılı durumdadır.

### Backend Testleri (128 Test Vakası - %100 Başarılı)
```powershell
dotnet test backend/IFS.AIWeb.slnx --configuration Release
```
- **Domain Tests:** 4 Passed
- **Application Tests:** 64 Passed
- **Integration Tests (PostgreSQL 18):** 60 Passed (50 test metodu / 60 çalıştırılan test vakası)

### Frontend Testleri (64 Test - %100 Başarılı)
```powershell
cd frontend
npm run lint
npm test -- --run
npm run build
```
- **ESLint:** 0 Hata, 0 Uyarı (%100 Temiz).
- **Vitest Unit/Component Tests:** 64 / 64 Passed.
- **Production Build:** Vite ve TypeScript (tsc -b) 0 hata ile derlenir.

---

## 🏗️ Proje Mimarısı (Clean Architecture)

```text
IFS-AI-Web/
├── backend/
│   ├── src/
│   │   ├── IFS.AIWeb.Domain/         # Varlıklar, Değer Nesneleri ve Domain Kuralları
│   │   ├── IFS.AIWeb.Application/    # Servisler, DTO'lar, PDF Üretici, Prompt Builder
│   │   ├── IFS.AIWeb.Infrastructure/ # EF Core, PostgreSQL 18, PDFsharp Font Resolver, Groq Client
│   │   └── IFS.AIWeb.Api/            # Minimal API Endpoint'leri, Middleware, Rate Limiter
│   └── tests/                        # Birim ve Entegrasyon Test Paketleri
├── frontend/
│   ├── src/
│   │   ├── admin/                    # Admin Yönetim Sayfaları, AbortController ve İstatistikler
│   │   ├── auth/                     # Kimlik Doğrulama Bileşenleri ve Guard'lar
│   │   ├── pages/                    # AppPage, Özetleme, Pano Kopyalama, TTS ve Detay Görünümleri
│   │   └── styles.css                # Vibrant Pink & Neon Violet AI Tema Stilleri
└── docs/                             # Mimari Kararlar (ADR), Gereksinim Matrisi ve Test Doğrulama Logları
```

---

## 📝 Lisans

Bu proje ödev ve portal gereksinimleri kapsamında geliştirilmiştir. Kullanılan tüm kütüphaneler (PDFsharp, EF Core, React vb.) permissive açık kaynak lisanslarına (MIT / Apache 2.0 / SIL OFL 1.1) sahiptir.
