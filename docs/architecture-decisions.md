# Başlangıç Mimari Kararları

## 1. Karar sınıfları

- **[PDF gereksinimi]:** PDF'nin açıkça istediği davranış veya kısıt.
- **[Teknik karar]:** Ekibin seçtiği uygulama yaklaşımı; PDF zorunluluğu değildir.
- **[Teknik öneri]:** Uygulamadan önce doğrulanacak tercih.
- **[Açık karar]:** Henüz seçilmemiş konu.

PDF backend, frontend, veritabanı ve LLM sağlayıcısını yazılımcıya bırakır. Bu nedenle ASP.NET Core, React, PostgreSQL ve Clean Architecture seçimleri PDF gereksinimi olarak sunulmaz.

## 2. Kararlar

### ADR-001 - Backend: ASP.NET Core 10

- **Karar:** **[Teknik karar]** API, ASP.NET Core 10 ile geliştirilecektir.
- **Bağlam ve gerekçe:** Güçlü DI, kimlik/yetkilendirme, doğrulama (validation), rate limiting, OpenAPI ve test ekosistemi; ekibin .NET hedefiyle uyumludur.
- **Değerlendirilen alternatifler:** .NET 8 LTS, Node.js/NestJS, Python/FastAPI.
- **Sonuçlar ve ödünleşimler:** Modern platform özellikleri ve güçlü tip güvenliği sağlanır. Hedef çalışma ortamında .NET 10 desteği ve seçilen paketlerin uyumluluğu doğrulanmalıdır.
- **Mevcut durum:** Kabul edildi; kod oluşturulmadı.

### ADR-002 - Frontend: React ve TypeScript

- **Karar:** **[Teknik karar]** İstemci React ve TypeScript olacaktır.
- **Bağlam ve gerekçe:** Form ağırlıklı kullanıcı/admin ekranları, bileşen tekrar kullanımı ve tipli API sözleşmeleri için uygundur.
- **Değerlendirilen alternatifler:** Blazor, Angular, Vue.
- **Sonuçlar ve ödünleşimler:** Ayrı frontend derleme zinciri gerekir; istemci doğrulaması güvenlik sınırı sayılmaz. Küçük MVP için gereksiz global state kütüphanesi eklenmeyecektir.
- **Mevcut durum:** Kabul edildi; araç zinciri kararı geliştirme fazına bırakıldı.

### ADR-003 - Veritabanı: PostgreSQL

- **Karar:** **[Teknik karar]** Kullanıcılar, kimlik verileri, refresh token kayıtları ve kararlaştırılan denetim kaydı (audit log) verileri PostgreSQL'de tutulacaktır.
- **Bağlam ve gerekçe:** Rol, kullanıcı ve log ilişkileri ilişkisel modele uygundur; bütünlük kısıtları ve sorgulanabilirlik sağlar.
- **Değerlendirilen alternatifler:** SQL Server, SQLite, MongoDB; PDF SQL/NoSQL seçimini serbest bırakır.
- **Sonuçlar ve ödünleşimler:** Migration ve yerel PostgreSQL kurulumu gerekir. Ham metin saklama mahremiyet ve boyut maliyeti doğurur; saklama kapsamı ayrı karardır.
- **Mevcut durum:** Kabul edildi; şema/migration oluşturulmadı.

### ADR-004 - Clean Architecture ve katmanlar

- **Karar:** **[Teknik karar]** Backend dört proje sınırı kullanacaktır: Domain, Application, Infrastructure ve API.
- **Bağlam ve gerekçe:** LLM, veritabanı ve kimlik altyapısını kullanım senaryolarından ayırmak; test edilebilirliği korumak.
- **Değerlendirilen alternatifler:** Tek projeli katmanlı monolit; feature-folder dikey dilimler; mikroservisler.
- **Sonuçlar ve ödünleşimler:** Proje referansları `Application -> Domain`, `Infrastructure -> Application` ve yalnız gerektiğinde `Infrastructure -> Domain`, `API -> Application` ve composition-root kayıtları için `API -> Infrastructure` yönünde olacaktır. `Domain` hiçbir dış katmana referans vermez. API'nin bağımlılık kayıtları için Infrastructure'ı referanslaması, iş kurallarının Infrastructure'a bağımlı olduğu anlamına gelmez; iş kuralları Domain/Application katmanlarında tanımlanır ve dış hizmetlere Application'daki soyutlamalar üzerinden bağımlıdır. Dört proje küçük bir ek maliyettir; mikroservis, event bus, CQRS framework veya generic repository eklenmeyecektir.
- **Mevcut durum:** Kabul edildi.

### ADR-005 - SOLID ve Dependency Injection

- **Karar:** **[Teknik karar]** Sınırlar küçük arayüzlerle kurulacak; dış bağımlılıklar constructor injection ile verilecektir.
- **Bağlam ve gerekçe:** LLM sağlayıcısı, saat, token üretimi ve kalıcılık testlerde değiştirilebilmelidir.
- **Değerlendirilen alternatifler:** Statik servisler, service locator, tüm sınıflar için arayüz üretmek.
- **Sonuçlar ve ödünleşimler:** Bağımlılık tersine çevirme ve tek sorumluluk desteklenir. Yalnız gerçek sınırlar soyutlanır; anlamsız arayüz/katman çoğalması önlenir.
- **Mevcut durum:** Kabul edildi.

### ADR-006 - Değiştirilebilir LLM sağlayıcı soyutlaması

- **Karar:** **[Teknik karar]** Application katmanı sağlayıcıdan bağımsız bir özetleme portu ve istek/yanıt modelleri tanımlar; Infrastructure seçilen sağlayıcıyı uygular.
- **Bağlam ve gerekçe:** **[PDF]** Sağlayıcı serbesttir. Henüz sağlayıcı/model seçilmemiştir.
- **Değerlendirilen alternatifler:** Sağlayıcı SDK'sını doğrudan kullanım senaryosunda çağırmak; birden çok sağlayıcıyı ilk günden çalıştırmak.
- **Sonuçlar ve ödünleşimler:** Sağlayıcı değişimi ve sahte test kolaylaşır. Soyutlama yalnız ihtiyaç duyulan ortak yetenekleri kapsar; ilk sürümde tek aktif sağlayıcı yeterlidir.
- **Mevcut durum:** Soyutlama kabul edildi; **[Açık karar]** sağlayıcı, model, SDK, veri bölgesi, kota, maliyet ve timeout değerleri seçilmedi.

### ADR-007 - Access ve refresh token oturumu

- **Karar:** **[Teknik karar]** Kısa ömürlü access token istemci belleğinde kullanılacak; refresh token `Secure`, `HttpOnly`, uygun `SameSite` öznitelikli cookie ile taşınacaktır. Refresh token düz metin yerine yalnız hash olarak, süre ve iptal bilgisiyle saklanacaktır. Refresh-token rotasyonu ve iptal kayıtları MVP kapsamındadır.
- **Bağlam ve gerekçe:** **[PDF]** Giriş ve rol kontrolü ister; token biçimini belirtmez. HttpOnly cookie refresh token'ın JavaScript tarafından okunmasını önler, hash saklama veritabanı sızıntısının etkisini azaltır.
- **Değerlendirilen alternatifler:** İki token'ı localStorage'da tutmak; sunucu tarafı cookie session; düz metin refresh token.
- **Sonuçlar ve ödünleşimler:** Refresh endpoint'i için CSRF önlemi, güvenli cookie ayarı, rotasyon ve iptal kaydı gerekir. Logout, şifre değişimi ve pasife alma refresh token'ları iptal etmelidir. Token ailesine dayalı tekrar kullanım tespiti (reuse detection) MVP sonrasındaki güvenlik sıkılaştırma aşamasına bırakılmıştır. Bu erteleme, ele geçirilmiş ve döndürülmüş bir token'ın tekrar kullanıldığı bazı saldırıların MVP'de otomatik olarak saptanamayacağı bilinen güvenlik ödünleşimidir. Access token ömrü ve SameSite değeri dağıtım topolojisine göre seçilir.
- **Mevcut durum:** MVP için hash saklama, rotasyon ve iptal kayıtları kabul edildi. **[Açık karar]** Kesin token ömürleri ile cookie domain/path ayarlarıdır; token ailesine dayalı tekrar kullanım tespitinin tasarımı gelecek güvenlik aşamasında kararlaştırılacaktır.

### ADR-008 - Kontrollü prompt ve sürümleme

- **Karar:** **[Teknik karar]** Sistem talimatı, kullanıcı metni ve izinli seçenekler ayrı yapılandırılacak; prompt sürüm kimliği her AI kaydına eklenecektir.
- **Bağlam ve gerekçe:** **[PDF]** Prompt tabanlı akış ve basit bir şablon önerir. Ürün, dil/uzunluk/biçim/hedef kitle/şablon seçenekleri ve bilgi uydurmama kuralı getirir.
- **Değerlendirilen alternatifler:** Kullanıcı metnini string interpolation ile doğrudan talimata eklemek; promptları admin panelinden düzenlemek.
- **Sonuçlar ve ödünleşimler:** Tekrarlanabilirlik ve denetlenebilirlik artar; prompt injection tamamen ortadan kalkmaz. MVP'de promptlar kod/yapılandırma içinde sürümlü ve gözden geçirilmiş olur; prompt yönetim sistemi kurulmaz.
- **Mevcut durum:** Kabul edildi; ilk prompt metni ve değerlendirme seti açık karardır.

### ADR-009 - Yapılandırılmış AI yanıtı ve şema doğrulama

- **Karar:** **[Teknik karar]** Sağlayıcıdan mümkünse JSON schema/structured output istenecek; cevap Application katmanında şemaya ve iş kurallarına göre doğrulanmadan UI'a gönderilmeyecektir.
- **Bağlam ve gerekçe:** Kaynak bağlantıları, kurumsal bölümler ve “Belirtilmemiş” davranışı serbest metinden güvenilir ayrıştırılamaz.
- **Değerlendirilen alternatifler:** Tamamen serbest metin; regex/Markdown ayrıştırma.
- **Sonuçlar ve ödünleşimler:** UI sözleşmesi kararlı olur. Sağlayıcı şema yetenekleri değişebilir; geçersiz yanıt için sınırlı yeniden deneme veya güvenli hata gerekir. Referans kimliklerinin varlığı deterministik doğrulanabilir, anlamsal doğruluk yalnız değerlendirme testleriyle ölçülebilir.
- **Mevcut durum:** Kabul edildi; kesin şema şablon alanları netleşince belirlenecek.

### ADR-010 - Rate limiting

- **Karar:** **[Teknik öneri]** Özetleme uç noktası kullanıcı kimliğine göre dakika/işlem limiti uygulayacaktır.
- **Bağlam ve gerekçe:** **[PDF bonusu]** Rate limit bonus olarak belirtilmiştir. Ayrıca maliyet ve kötüye kullanım kontrolü sağlar.
- **Değerlendirilen alternatifler:** IP limiti; yalnız sağlayıcı kotasına güvenmek; ilk sürümde limit koymamak.
- **Sonuçlar ve ödünleşimler:** 429 cevabı ve anlaşılır retry bilgisi gerekir. Çoklu instance durumunda dağıtık sayaç gerekebilir; MVP tek instance ise yerleşik limiter yeterlidir.
- **Mevcut durum:** Uygulanması önerildi; limit değeri ve dağıtım topolojisi açık.

### ADR-011 - Log maskeleme, mahremiyet ve denetim kaydı

- **Karar:** **[Teknik karar]** Operasyonel loglara tam kullanıcı metni, AI yanıtı, şifre, token veya API anahtarı yazılmayacaktır. Uzun girdiler yapılandırılabilir sınırda kısaltılır; denetim kaydı ile teknik log ayrılır.
- **Bağlam ve gerekçe:** **[PDF]** Admin logu ister, girdi/çıktı saklama seçeneklerini açık bırakır; uzun girdi maskelemesini bonus sayar. Kullanıcı metni hassas olabilir.
- **Değerlendirilen alternatifler:** Her şeyi tam saklamak; yalnız yanıtı saklamak; hiç içerik saklamamak.
- **Sonuçlar ve ödünleşimler:** Mahremiyet iyileşir, hata inceleme ayrıntısı azalabilir. Kim, ne zaman, hangi prompt/provider sürümüyle işlem yaptı ve sonuç durumu gibi üst veriler denetlenebilirlik sağlar. İçerik saklama kapsamı, erişim ve silme/saklama süresi kararlaştırılmalıdır.
- **Mevcut durum:** Maskeleme ilkesi kabul edildi; **[Açık karar]** admin log içeriği ve saklama süresi (retention).

### ADR-012 - Merkezi hata yönetimi

- **Karar:** **[Teknik karar]** API, merkezi exception handler ile tutarlı Problem Details cevapları üretecek; correlation ID korunacak, iç ayrıntılar kullanıcıya sızdırılmayacaktır.
- **Bağlam ve gerekçe:** **[PDF]** API ve boş metin hatalarının yalın ve kibar gösterilmesini ister.
- **Değerlendirilen alternatifler:** Her controller'da try/catch; ham exception cevabı.
- **Sonuçlar ve ödünleşimler:** İstemci davranışı ve gözlemlenebilirlik tutarlı olur. Beklenen domain/doğrulama hataları exception yerine açık sonuçlarla modellenebilir.
- **Mevcut durum:** Kabul edildi.

### ADR-013 - Doğrulama

- **Karar:** **[Teknik karar]** İstemci hızlı geri bildirim verir; güvenlik ve iş kuralı doğrulamasının yetkili kaynağı Application/API'dir.
- **Bağlam ve gerekçe:** Boş metin, kimlik alanları, izinli enum değerleri ve AI şeması sınırlandırılmalıdır.
- **Değerlendirilen alternatifler:** Yalnız frontend veya yalnız veritabanı doğrulaması.
- **Sonuçlar ve ödünleşimler:** Bir miktar kural tekrarı olur; API istemciden bağımsız güvenli kalır. Maksimum metin uzunluğu sağlayıcı seçimi sonrası belirlenir.
- **Mevcut durum:** Kabul edildi; doğrulama kütüphanesi açık karardır.

### ADR-014 - Yapılandırma ve secret yönetimi

- **Karar:** **[PDF gereksinimi + teknik karar]** Secret'lar koda/commit'e girmez; yerelde .NET user-secrets veya environment, dağıtımda platform secret store kullanılır. Seçenekler başlangıçta doğrulanır.
- **Bağlam ve gerekçe:** PDF API anahtarlarının `.env`/secrets ortamında tutulmasını zorunlu güvenlik kuralı olarak verir.
- **Değerlendirilen alternatifler:** `appsettings.json` içinde gerçek anahtar; veritabanında şifresiz saklama.
- **Sonuçlar ve ödünleşimler:** Ortam kurulumu gerekir; örnek yapılandırma yalnız anahtar adlarını ve güvenli varsayılanları içerir.
- **Mevcut durum:** Kabul edildi; dağıtım platformu açık.

### ADR-015 - Test stratejisi

- **Karar:** **[Teknik karar]** Test piramidi: Domain/Application birim testleri; PostgreSQL ve kimlik/LLM bağdaştırıcı entegrasyon testleri; API yetkilendirme/sözleşme testleri; az sayıda kritik tarayıcı uçtan uca testi; prompt/özet kalite değerlendirme seti.
- **Bağlam ve gerekçe:** Rol sınırı, token rotasyonu, yapılandırılmış AI cevabı ve kaynak sadakati farklı test türleri gerektirir.
- **Değerlendirilen alternatifler:** Yalnız manuel test; her şeyi uçtan uca test etmek.
- **Sonuçlar ve ödünleşimler:** Hızlı deterministik testlerle kritik gerçek entegrasyonlar dengelenir. Canlı LLM testleri değişken ve maliyetli olduğundan normal CI'da sahte sağlayıcı kullanılır; kontrollü sağlayıcı smoke/evaluation ayrı çalışır.
- **Mevcut durum:** Kabul edildi; araçlar ve kalite veri seti açık.

### ADR-016 - Denetlenebilirlik

- **Karar:** **[Teknik karar]** Özet işlemi; kullanıcı, zaman, başarı durumu, provider/model (seçilince), prompt sürümü ve güvenli içerik önizlemesi/kimliğiyle izlenebilir olacaktır. Yönetici kullanıcı değişiklikleri de aktör ve zamanla kaydedilecektir.
- **Bağlam ve gerekçe:** **[PDF]** Log listesi ister; ürün kaynak bağlantısı ve prompt sürümleme getirir.
- **Değerlendirilen alternatifler:** Yalnız uygulama metin logları; tam event sourcing.
- **Sonuçlar ve ödünleşimler:** İnceleme ve hata ayıklama güçlenir; depolama ve kişisel veri yükümlülüğü doğar. Event sourcing MVP için gereksizdir.
- **Mevcut durum:** İlke kabul edildi; denetim alanları ve saklama süresi açık.

## 3. Çözülmemiş kararlar özeti

- LLM sağlayıcısı, modeli, veri işleme bölgesi, maliyet/kota, timeout ve structured-output desteği.
- Access token ve refresh token süreleri ile cookie dağıtım ayarları; token ailesine dayalı tekrar kullanım tespiti MVP sonrasına bırakılmıştır.
- Admin logunda saklanacak içerik, maskeleme sınırı, saklama süresi ve silme politikası.
- Kullanıcı adı/e-posta veri modeli ve self-registration ayrıntıları.
- Girdi karakter/token sınırı ve kabul edilen kaynak dilleri.
- Kurumsal şablonların kesin JSON şemaları ve ilk prompt sürümü.
- Rate limit değerleri ve tek/çok instance dağıtım biçimi.
- Dağıtım/secret platformu, test kütüphaneleri ve CI ortamı.
