# IFS AI-Web

IFS AI-Web, kullanıcı tarafından sağlanan metinleri ilerleyen fazlarda kontrollü bir LLM prompt akışıyla özetleyecek web uygulamasıdır. Bu depo ASP.NET Core ve React tabanlı uygulamanın Clean Architecture temellerini içerir.

## Phase 1 kapsamı

Bu faz yalnızca çalıştırılabilir proje temelini sağlar:

- .NET 10 üzerinde Domain, Application, Infrastructure ve API projeleri
- Anonim `GET /health` uç noktası ve entegrasyon testi
- React, TypeScript, Vite ve React Router tabanlı uygulama kabuğu
- Türkçe başlangıç ve bulunamadı sayfaları
- Yapılandırmadan okunan backend CORS origin listesi ve frontend API taban adresi

Kimlik doğrulama, PostgreSQL/veritabanı ve LLM entegrasyonu henüz uygulanmamıştır. Özetleme formu, kullanıcı/admin sayfaları ve kurumsal şablonlar da bu fazın kapsamında değildir.

## Önkoşullar

- .NET SDK 10.0 veya uyumlu daha yeni .NET 10 SDK
- Node.js 22.12 veya uyumlu daha yeni sürüm
- npm 11 veya uyumlu sürüm

## Backend

Depo kökünden:

```powershell
dotnet restore backend/IFS.AIWeb.slnx
dotnet build backend/IFS.AIWeb.slnx --no-restore
dotnet test backend/IFS.AIWeb.slnx --no-build
dotnet run --project backend/src/IFS.AIWeb.Api/IFS.AIWeb.Api.csproj
```

Backend geliştirme adresi: `http://localhost:5099`

Sağlık kontrolü: `GET http://localhost:5099/health`

## Frontend

```powershell
cd frontend
npm install
npm run lint
npm test
npm run build
npm run dev
```

Frontend geliştirme adresi: `http://localhost:5173`

## Ortam değişkenleri

Frontend için örnek dosyayı yerel `.env` dosyasına kopyalayın:

```powershell
Copy-Item frontend/.env.example frontend/.env
```

`VITE_API_BASE_URL`, backend taban adresini belirler. Örnek değer secret içermez:

```dotenv
VITE_API_BASE_URL=http://localhost:5099
```

Backend geliştirme CORS origin listesi `backend/src/IFS.AIWeb.Api/appsettings.Development.json` içindeki `Cors:AllowedOrigins` bölümünden yönetilir. Gerçek API anahtarı, şifre veya bağlantı dizesi kaynak dosyalarına eklenmemelidir.
