# IFS AI-Web

Phase 2, .NET 10 Clean Architecture API, React/TypeScript Vite SPA ve PostgreSQL üzerinde güvenli kayıt, giriş, oturum yenileme ve `User`/`Admin` yetkilendirme temelini sağlar. Özetleme ve LLM entegrasyonu henüz uygulanmamıştır.

## Gereksinimler

- .NET SDK 10
- Node.js ve npm
- Docker Desktop / Docker Compose
- EF Core CLI (`dotnet tool install --global dotnet-ef --version 10.*`)

## Güvenli yerel yapılandırma

`.env.example` dosyasını `.env` olarak kopyalayın ve tüm `replace-...` yer tutucularını yalnız yerel güçlü değerlerle değiştirin. `.env` Git tarafından yok sayılır. API için aynı değerler environment variable olarak verilmelidir:

```text
ConnectionStrings__PostgreSql
Jwt__SigningKey
InitialAdmin__Username       (isteğe bağlı)
InitialAdmin__Password       (isteğe bağlı)
```

JWT anahtarı en az 32 bayt olmalıdır. İlk yönetici yalnız iki seed değeri birlikte sağlandığında oluşturulur. Seed idempotenttir; var olan hesabı yükseltmez, şifreyi sıfırlamaz ve kullanıcı adı çakışmasında güvenli biçimde durur. Gerçek kimlik bilgilerini dosyaya yazmayın.

## PostgreSQL ve migration

```powershell
docker compose config
docker compose up -d postgres
dotnet ef database update --project backend/src/IFS.AIWeb.Infrastructure --startup-project backend/src/IFS.AIWeb.Api
docker compose stop postgres
```

Compose yalnız PostgreSQL çalıştırır; localhost `5432`, sağlık kontrolü ve `postgres_data` adlı kalıcı volume kullanılır. API normal başlangıçta migration uygulamaz.

## Backend

```powershell
dotnet restore backend/IFS.AIWeb.slnx
dotnet build backend/IFS.AIWeb.slnx --no-restore
dotnet test backend/IFS.AIWeb.slnx --no-build
dotnet run --project backend/src/IFS.AIWeb.Api
```

Yerel API URL'si launch profile ile `http://localhost:5099`; anonim sağlık kontrolü `/health` adresindedir.

## Frontend

```powershell
cd frontend
npm install
npm run lint
npm test
npm run build
npm run dev
```

SPA varsayılan olarak `http://localhost:5173` adresindedir. Kullanıcı adı 3-32 karakterdir; Unicode NFKC ile normalize edilir, büyük/küçük harfe duyarsızdır ve yalnız harf, rakam, `.`, `_`, `-` kabul eder. Ad/soyad 1-80, şifre 12-128 karakterdir; passphrase kullanımına izin verilir.

## Oturum ve güvenlik modeli

- Açık kayıt yalnız `User` oluşturur; istek rol veya e-posta kabul etmez.
- Access token JWT'dir, 15 dakika geçerlidir ve SPA'da yalnız bellekte tutulur.
- Refresh token 7 gün geçerlidir; yalnız `HttpOnly`, `SameSite=Strict`, üretimde `Secure`, `/api/auth` path cookie olarak taşınır ve veritabanında yalnız SHA-256 hash'i tutulur.
- Her refresh token'ı döndürür. Eski/iptal edilmiş token'ın tekrar kullanımı ilgili token ailesini iptal eder.
- Cookie kullanan refresh/logout çağrılarında yapılandırılmış kesin Origin listesi doğrulanır; CORS wildcard kullanmaz.
- Pasif kullanıcı giriş/refresh yapamaz ve eski JWT ile korumalı endpoint'lere erişemez.
- Yönetici kanıtı için seed ile oluşturulan Admin hesabıyla giriş yapıp `/app/admin-check` sayfası kullanılabilir; kimlik bilgileri kaynakta veya dokümantasyonda yer almaz.

API uçları: `POST /api/auth/register`, `login`, `refresh`, `logout`; `GET /api/auth/me`, `admin-check`.

## Bilinen bağımlılık bildirimi

`npm audit`, React Router 7.18.2 için GHSA-qwww-vcr4-c8h2 bildirimini gösterebilir. Proje yalnız Vite `BrowserRouter` SPA'dır; React Server Components, Server Actions, Framework Mode server actions veya unstable RSC API kullanmaz. Bu nedenle bildirimin etkilenen işlevi bu mimaride kullanılmamaktadır; bulgu bastırılmaz ve audit sonucu temizmiş gibi sunulmaz.

## Kapsam dışında

Özetleme/LLM, şablonlar, admin kullanıcı yönetimi, şifre sıfırlama/değiştirme, profil düzenleme, log ekranları, Docker ile API/SPA, CI/CD ve gerçek IFS entegrasyonu uygulanmamıştır.
