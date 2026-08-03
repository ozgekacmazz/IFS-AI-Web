# IFS AI-Web

Phase 3, .NET 10 Clean Architecture API, React/TypeScript Vite SPA ve PostgreSQL üzerinde güvenli kimlik doğrulama ile prompt tabanlı Türkçe/İngilizce metin özetleme akışını sağlar.

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

Groq anahtarını API başlangıç projesinin .NET User Secrets deposuna ekleyin:

```powershell
dotnet user-secrets set "Groq:ApiKey" "<your-groq-api-key>" --project backend/src/IFS.AIWeb.Api
```

İlk sağlayıcı Groq, değiştirilebilir varsayılan model `openai/gpt-oss-120b`'dir. Anahtar yalnız backend tarafından kullanılır; frontend'e, loglara veya izlenen yapılandırma dosyalarına yazılmaz.

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

SPA varsayılan olarak `http://localhost:5173` adresindedir. Kullanıcı adı 3-32 karakterdir; Unicode NFKC ile normalize edilir, büyük/küçük harfe duyarsızdır ve yalnız harf, rakam, `.`, `_`, `-` kabul eder. Ad/soyad 1-80 karakterdir. Şifre 8-128 Unicode karakter olmalı ve en az bir büyük harf, küçük harf, rakam ile noktalama/özel karakter içermelidir. Şifre trim veya normalize edilmez; boşluk kullanılabilir ancak özel karakter koşulunu tek başına karşılamaz.

Özet başlıkları backend tarafından saklanmaz; kaynak/özet metninden frontend'de deterministik olarak türetilir ve kayıt yeniden yüklendiğinde yeniden oluşturulur. API yalnız en yeni yedi uygun başarılı kaydı sağladığı için Kitaplık bu kapsamı dürüstçe gösterir. Sabitleme sunucuyla eşitlenmez; yalnız hassas olmayan özet kimlikleri `ifs-aiweb:pinned-summary-ids` anahtarı altında browser-local görünüm tercihi olarak saklanır. Access token hâlâ yalnız uygulama belleğindedir.

## Oturum ve güvenlik modeli

- Açık kayıt yalnız `User` oluşturur; istek rol veya e-posta kabul etmez.
- Access token JWT'dir, 15 dakika geçerlidir ve SPA'da yalnız bellekte tutulur.
- Refresh token 7 gün geçerlidir; yalnız `HttpOnly`, `SameSite=Strict`, üretimde `Secure`, `/api/auth` path cookie olarak taşınır ve veritabanında yalnız SHA-256 hash'i tutulur.
- Her refresh token'ı döndürür. Eski/iptal edilmiş token'ın tekrar kullanımı ilgili token ailesini iptal eder.
- Cookie kullanan refresh/logout çağrılarında yapılandırılmış kesin Origin listesi doğrulanır; CORS wildcard kullanmaz.
- Pasif kullanıcı giriş/refresh yapamaz ve eski JWT ile korumalı endpoint'lere erişemez.
- Yönetici kanıtı için seed ile oluşturulan Admin hesabıyla giriş yapıp `/app/admin-check` sayfası kullanılabilir; kimlik bilgileri kaynakta veya dokümantasyonda yer almaz.

API uçları: `POST /api/auth/register`, `login`, `refresh`, `logout`; `GET /api/auth/me`, `admin-check`.

## Admin backend

Admin backend ve frontend fazları tamamlanmıştır. `/api/admin` altındaki kullanıcı listeleme/oluşturma, aktiflik, şifre güncelleme, kompakt AI kayıtları, salt okunur prompt bilgisi ve yedi günlük UTC istatistik uçlarının tamamı mevcut `AdminOnly` politikasıyla korunur. Yönetim arayüzü `/admin`, `/admin/users` ve `/admin/logs` rotalarındadır; yalnızca Admin rolüyle görüntülenir. Sözleşmeler, oturum iptali, son aktif yönetici eşzamanlılık koruması ve kayıt mahremiyeti [docs/admin-backend.md](docs/admin-backend.md), arayüz davranışları ise [docs/admin-frontend.md](docs/admin-frontend.md) belgesinde açıklanır. Bu faz mevcut şemayla uygulanmış, migration eklenmemiştir.

## Özetleme modeli

- `POST /api/summaries`, kimliği doğrulanmış kullanıcının en fazla 12.000 karakterlik metnini Türkçe veya İngilizce özetler. Arayüzdeki “Kaynak metin” alanı ödevdeki prompt/metin girdisidir; ikinci bir düzenlenebilir prompt alanı yoktur. Kullanıcı kimliği JWT claim'inden alınır.
- `GET /api/summaries/recent`, yalnız mevcut kullanıcının en yeni yedi uygun başarılı özetini kaynak metni içermeyen kompakt liste verisi olarak döndürür. `GET /api/summaries/{id}`, yalnız kayıt sahibi için başarılı ve süresi dolmamış kaydın tam kaynak metnini ve üretilen özeti talep üzerine döndürür; diğer bütün durumlar aynı güvenli 404 cevabını üretir.
- Application katmanındaki sağlayıcıdan bağımsız port, backend'in kontrol ettiği sürümlü `summary-v3` prompt oluşturucuyu Groq HTTP bağdaştırıcısından ayırır. Aynı sıfır sıcaklıklı sağlayıcı isteği, zorunlu yapılandırılmış çıktıda içeriği `sufficient` veya `insufficient` olarak sınıflandırır. Yalnız tanımlanabilir bir olgu, olay, durum, talimat, açıklama, iddia veya ilişki içeren kaynak yeterlidir; selamlaşmalar, bağlantısız/rastgele içerik ve belirsiz durumlar yetersiz kabul edilir. Yetersiz sonuçlar başarılı özet olarak saklanmaz ve güvenli 422 cevabı üretir. Kullanıcı sistem talimatını göremez veya değiştiremez.
- Özet girdisi için yetkili backend doğrulaması; boş metni, 12.000 karakter sınırının aşılmasını, hiç Unicode harf içermeyen girdiyi, en az sekiz harften/rakamdan oluşan tek karakter tekrarını ve harf içeren ikiden az Unicode kelimeyi reddeder. Bu deterministik kontroller çok kelimeli anlamsız dizileri güvenilir biçimde sınıflandırdığını iddia etmez; yapılandırılmış çıktı ve açık prompt rehberliği semantik sınıflandırma hatalarını azaltır ancak matematiksel olarak ortadan kaldırmaz. Kısa ama bilgi taşıyan Türkçe/İngilizce ifadeler kabul edilir.
- Sağlayıcı çıktısı en fazla 500 token, HTTP zaman aşımı 30 saniyedir. İptal iletilir; belirsiz veya ücret doğurabilecek işlemler otomatik yeniden denenmez.
- Özetleme POST isteği kullanıcı kimliğine göre sabit bir dakikalık pencerede beş istekle sınırlıdır. Limitleyici bellek içidir ve her API örneği için ayrıdır.
- Başarılı tam kaynak metni ve tam özet PostgreSQL'de tutulur; kullanıcı saklama süresi içinde kendi kaydının ikisini de ayrıntı görünümünde okuyabilir. Başarısız sağlayıcı denemelerinde kaynak metin veya özet yerine yalnız güvenli operasyonel üst veri ve hata kategorisi saklanır. Her kayıt `ExpiresAtUtc = CreatedAtUtc + 30 gün` değerini taşır.
- Uygulama loglarına tam girdi, prompt, özet, token, API anahtarı veya ham sağlayıcı hata gövdesi yazılmaz.
- Zamanlanmış 30 günlük silme görevi sonraki faza bırakılmıştır; Admin kayıt ekranı süresi dolan içeriği dürüstçe belirtir ve tam metni istemez.

## Bilinen bağımlılık bildirimi

`npm audit`, React Router 7.18.2 için GHSA-qwww-vcr4-c8h2 bildirimini gösterebilir. Proje yalnız Vite `BrowserRouter` SPA'dır; React Server Components, Server Actions, Framework Mode server actions veya unstable RSC API kullanmaz. Bu nedenle bildirimin etkilenen işlevi bu mimaride kullanılmamaktadır; bulgu bastırılmaz ve audit sonucu temizmiş gibi sunulmaz.

## Kapsam dışında

Kurumsal şablonlar, kaynak bağlantılı özetler, profil düzenleme, zamanlanmış saklama temizliği, Docker ile API/SPA, CI/CD ve gerçek IFS entegrasyonu uygulanmamıştır.
