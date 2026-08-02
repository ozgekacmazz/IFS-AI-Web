# Gereksinim Matrisi

## 1. Kaynak ve sınıflandırma

Bu matrisin birincil kaynağı, depo kökündeki dört sayfalık **“2. Proje - AI-Web Uygulaması .pdf”** belgesidir. Kaynak gösteriminde PDF bölüm adı ve sayfa numarası birlikte verilmiştir.

Durumlar bu Phase 0 çalışmasının sonucunu gösterir:

- **Planlandı:** Uygulama geliştirilmedi; gereksinim uygulama planına alındı.
- **Açık karar:** Uygulamadan önce netleştirilmesi gereken konu var.
- **İleri aşamaya bırakıldı:** MVP için zorunlu değildir.

PDF'de “öneri”, “opsiyonel” veya “bonus” olarak işaretlenen hiçbir madde zorunlu kabul edilmemiştir.

## 2. Zorunlu PDF gereksinimleri

| ID | Kaynak | Gereksinim | Planlanan uygulama | Katman / bileşen | Doğrulama | Durum |
|---|---|---|---|---|---|---|
| PDF-M-01 | Tanım, s. 1 | Kullanıcının yazdığı paragraf/metin, arka plandaki LLM tarafından özetlenerek döndürülmelidir. | Kimliği doğrulanmış kullanıcıdan metni alan özetleme kullanım senaryosu ve değiştirilebilir LLM bağdaştırıcısı. | React ana sayfa; Application; Infrastructure LLM; API | Geçerli metin gönderildiğinde LLM isteği ve ekranda özet sonucu entegrasyon testiyle doğrulanır. | Planlandı |
| PDF-M-02 | Tanım, s. 1 | Temel akış prompt tabanlı olmalıdır: kullanıcı metni girer, sistem LLM'e iletir, model özeti döndürür. | Kullanıcı girdisini kontrollü sistem talimatından ayıran, sürümlenmiş prompt oluşturucu. | Application; Infrastructure LLM | Sahte sağlayıcıyla oluşturulan isteğin metni içerdiği ve yanıtın geri aktarıldığı test edilir. | Planlandı |
| PDF-M-03 | Kapsam, s. 1 | Kullanıcı girişi ve kayıt ekranı bulunmalı; e-posta/kullanıcı adı ve şifre ile giriş yapılabilmelidir. | Kayıt, giriş ve oturum yenileme akışları; benzersiz kullanıcı kimliği. | React kimlik ekranları; Application Identity; API; PostgreSQL | Kayıt, doğru/yanlış kimlik bilgisi, tekrar eden kimlik ve oturum senaryoları test edilir. | Planlandı |
| PDF-M-04 | Kapsam ve Kullanıcı Akışı, ss. 1-2 | User ana sayfasında tek metin alanı, “Özetle” düğmesi ve sonuç alanı bulunmalıdır. | Erişilebilir form; gönderim, bekleme, başarı ve hata durumları. | React ana sayfa | Tarayıcıda metin girme, gönderme ve sonuç gösterme akışı doğrulanır. | Planlandı |
| PDF-M-05 | Kapsam, s. 1 | Admin panelini yalnızca admin rolü görmelidir. | Menü görünürlüğü ve sunucu tarafı rol politikası birlikte uygulanır. | React yönlendirme; API yetkilendirme (authorization) | User için menünün gizli ve admin URL/API çağrılarının 403 olduğu; admin erişiminin başarılı olduğu test edilir. | Planlandı |
| PDF-M-06 | Kapsam; Admin Akışı, ss. 1-2 | Admin log ekranında kullanıcı metinleri ve/veya AI özet yanıtları listelenmelidir. | Tarih, kullanıcı, kısa giriş ve kısa özet içeren sayfalı kayıt listesi; saklama kapsamı açık karara bağlıdır. | React admin log; Application; Infrastructure persistence; API | Yetkili listeleme, alan eşleşmesi, kısaltma ve yetkisiz erişim test edilir. | Açık karar |
| PDF-M-07 | Admin Akışı, s. 2 | Her istek/yanıt en basit hâliyle tarih-kullanıcı-kısa giriş-kısa özet içeren tek satırlık kayıt olarak listelenmelidir. | Her özetleme denemesi için denetim kaydı (audit log); hassas/uzun girdide güvenli önizleme. | Application denetim kaydı; PostgreSQL; admin UI | Başarılı istek sonrası satırın doğru kullanıcı, tarih ve önizlemelerle oluştuğu doğrulanır. | Planlandı |
| PDF-M-08 | Kapsam; Admin Akışı, ss. 1-2 | Admin yeni kullanıcı oluşturabilmelidir; kullanıcı adı/e-posta, şifre ve user/admin rolü atanabilmelidir. | Admin kullanıcı oluşturma formu ve yetkili API kullanım senaryosu. | React admin kullanıcılar; Application Identity; API; PostgreSQL | Admin başarı/çakışma/geçersiz rol; user için 403 senaryoları test edilir. | Planlandı |
| PDF-M-09 | Kapsam; Admin Akışı, ss. 1-2 | Admin kullanıcı şifresini güncelleyebilmeli veya sıfırlayabilmelidir. | Eski oturumları geçersizleştiren yönetici şifre sıfırlama akışı. | React admin kullanıcılar; Application Identity; API | Yeni şifreyle giriş, eski şifrenin reddi ve yetki kontrolü doğrulanır. | Planlandı |
| PDF-M-10 | Kapsam; Admin Akışı, ss. 1-2 | Admin kullanıcı durumunu Aktif/Pasif olarak güncelleyebilmelidir. | Durum değiştirme kullanım senaryosu; pasif kullanıcının yeni oturum/korumalı işlem erişimini engelleme. | React admin kullanıcılar; Application Identity; API; PostgreSQL | Pasife alma, giriş/erişim reddi ve tekrar aktifleştirme test edilir. | Planlandı |
| PDF-M-11 | Yetkilendirme, s. 2 | User rolü yalnızca Ana Sayfa'ya erişmelidir. | Rol tabanlı istemci rotaları ve sunucu politikaları. | React router; API yetkilendirme | User'ın admin rotası ve uç noktalarında reddedildiği test edilir. | Planlandı |
| PDF-M-12 | Yetkilendirme, s. 2 | Admin, Admin Paneli menüsünü görmeli; log ve kullanıcı yönetimi sayfalarına erişmelidir. | Admin navigasyonu ve rol politikaları. | React admin; API yetkilendirme | Admin rolüyle menü, sayfalar ve API çağrıları doğrulanır. | Planlandı |
| PDF-M-13 | AI Entegrasyonu, s. 3 | Sistem bir LLM ile entegre edilmelidir; kullanılacak LLM sağlayıcısı yazılımcıya bırakılmıştır. | Sağlayıcıdan bağımsız arayüz; ilk sağlayıcı/model daha sonra seçilecek. | Application port; Infrastructure provider | Sözleşme testleri ve seçilen sağlayıcıyla kontrollü entegrasyon testi. | Açık karar |
| PDF-M-14 | AI Entegrasyonu, s. 3 | API hatası, boş metin vb. hatalar kullanıcıya yalın ve kibar mesajlarla gösterilmelidir. | Girdi doğrulama, merkezi hata eşleme ve Türkçe kullanıcı mesajları. | React geri bildirim; Application doğrulama (validation); API middleware | Boş metin, zaman aşımı, sağlayıcı hatası ve beklenmeyen hata senaryoları doğrulanır. | Planlandı |
| PDF-M-15 | Güvenlik, s. 3 | Admin paneline doğrudan URL ile rol kontrolü aşılmadan erişilememelidir. | Her admin API uç noktasında sunucu tarafı admin politikası; istemci kontrolü yalnızca UX içindir. | API yetkilendirme; React router | User/anonim doğrudan URL ve doğrudan API çağrılarının reddedildiği test edilir. | Planlandı |
| PDF-M-16 | Güvenlik, s. 3 | API anahtarları `.env`/secrets benzeri ortamlarda tutulmalı, koda gömülmemelidir. | Yerel secret store ve ortam değişkenleri; yalnızca adları açıklayan örnek yapılandırma. | API configuration; deployment | Kaynak taraması, yapılandırmasız başlatma davranışı ve secret sızıntısı kontrol edilir. | Planlandı |
| PDF-M-17 | Teslim İçeriği, s. 4 | Çalışan uygulamanın kaynak kodu teslim edilmelidir. | Sonraki fazlarda çalışır backend ve frontend oluşturulacak. | Tüm uygulama | Temiz ortamda kurulum, build ve uçtan uca temel akış doğrulanır. | Planlandı |
| PDF-M-18 | Teslim İçeriği, s. 4 | Kurulum ve çalıştırma adımlarını içeren README teslim edilmelidir. | Önkoşullar, secrets, veritabanı, çalıştırma ve doğrulama adımları yazılacak. | Dokümantasyon | README adımları temiz ortamda uygulanır. | Planlandı |
| PDF-M-19 | Teslim İçeriği, s. 4 | Giriş/Kayıt ekranının ekran görüntüsü teslim edilmelidir. | Tamamlanan arayüzden kanıt görüntüsü üretilecek. | Teslim dokümanları | Görselde giriş ve kayıt durumlarının okunur olduğu kontrol edilir. | Planlandı |
| PDF-M-20 | Teslim İçeriği, s. 4 | Ana sayfadaki “metin gir -> özet döndür” akışının ekran görüntüsü teslim edilmelidir. | Girdi ve sonuç görünür olacak şekilde kanıt görüntüsü üretilecek. | Teslim dokümanları | Görsel, kaynak metni ve dönen özeti gösterir. | Planlandı |
| PDF-M-21 | Teslim İçeriği, s. 4 | Admin panelindeki log listesi ve kullanıcı yönetiminin ekran görüntüleri teslim edilmelidir. | İki admin yeteneğini gösteren kanıt görüntüleri üretilecek. | Teslim dokümanları | Görsellerde iki ekran ve admin bağlamı doğrulanır. | Planlandı |

## 3. PDF'deki öneriler ve opsiyonel gereksinimler

| ID | Kaynak | Gereksinim | Planlanan uygulama | Katman / bileşen | Doğrulama | Durum |
|---|---|---|---|---|---|---|
| PDF-O-01 | Kullanıcı Akışı, s. 2 | Kullanıcı son üç özetini basit listede görebilir (**opsiyonel**). | MVP sonrası değerlendirilecek. Log saklama kararıyla birlikte ele alınır. | React ana sayfa; Application; PostgreSQL | Kullanıcı yalnızca kendi son üç kaydını, doğru sırada görür. | İleri aşamaya bırakıldı |
| PDF-O-02 | Veri Yapıları, ss. 2-3 | USERS için `id`, benzersiz `username_or_email`, `password_hash`, `role`, `status`, `created_at`, `updated_at` alanları minimum **öneridir**. | PostgreSQL modeli aynı kavramları taşıyacak; kesin şema migration tasarımında belirlenecek. | Domain; Infrastructure persistence | Şema ve kısıtların migration/integration testleri. | Planlandı |
| PDF-O-03 | Veri Yapıları, s. 3 | AI_LOGS için `id`, `user_id`, opsiyonel `input_text`, `output_summary`, `provider`, `created_at` alanları minimum **öneridir**. | Mahremiyet ve denetim kaydı gereksinimine göre şema belirlenecek. | Domain; Infrastructure persistence | Şema, ilişki, saklama ve maskeleme testleri. | Açık karar |
| PDF-O-04 | AI Entegrasyonu, s. 3 | “Aşağıdaki metni kısa ve anlaşılır bir özet olarak döndür: {metin}” basit prompt şablonu **önerilmiştir**. | Aynen kopyalamak yerine aynı amacı koruyan, kontrollü ve sürümlü prompt kullanılacak. | Application prompt builder | Prompt snapshot ve talimat enjeksiyonu testleri. | Planlandı |
| PDF-O-05 | Güvenlik, s. 3 | Şifrelerin salt+hash yöntemiyle saklanması **öneridir**. | Endüstri standardı uyarlanabilir şifre hasher'ı kullanılacak; düz metin saklanmayacak. | Infrastructure Identity | Hash'in şifreden farklı olması, salt davranışı ve doğrulama test edilir. | Planlandı |
| PDF-O-06 | Teslim İçeriği, s. 4 | Mimari, AI entegrasyonu ve sınırlılıkları anlatan kısa teknik rapor **opsiyoneldir**. | Sprint raporları ve son teknik raporla karşılanması planlanır. | Dokümantasyon | Üç başlığın somut kanıtlarla kapsandığı incelenir. | İleri aşamaya bırakıldı |

## 4. Bonus PDF gereksinimleri

| ID | Kaynak | Gereksinim | Planlanan uygulama | Katman / bileşen | Doğrulama | Durum |
|---|---|---|---|---|---|---|
| PDF-B-01 | Bonus, s. 4 | Kullanıcı başına dakika/işlem oran sınırlaması. | Kimliği doğrulanmış kullanıcı anahtarıyla özetleme uç noktasında limit. | API rate limiting | Limit altı başarı, limit aşımında 429 ve pencere yenilenmesi test edilir. | Planlandı |
| PDF-B-02 | Bonus, s. 4 | Çok uzun metinlerde loga yalnızca ilk X karakteri yazan girdi maskeleme. | Yapılandırılabilir önizleme sınırı; hassas/uzun içerik için log politikası. | Application denetim kaydı; logging | Uzun girdinin tam metninin uygulama loglarına düşmediği doğrulanır. | Planlandı |
| PDF-B-03 | Bonus, s. 4 | Admin panelinde son yedi gün özet sayısını gösteren mini grafik. | Gün bazlı sayım uç noktası ve küçük grafik; MVP sonrası. | React admin; Application query; API | Tarih sınırları, saat dilimi ve grafik değerleri test edilir. | İleri aşamaya bırakıldı |
| PDF-B-04 | Bonus, s. 4 | Özet dilini (ör. Türkçe/İngilizce) seçebilme. | Ürün kararı gereği Türkçe/İngilizce seçimi MVP kapsamına alınır. | React ana sayfa; prompt builder | Her dil seçimi için yapılandırılmış yanıt dili doğrulanır. | Planlandı |

## 5. Bizim ürün kararlarımız (PDF gereksinimi değildir)

| ID | Karar | Planlanan uygulama | Doğrulama | Durum |
|---|---|---|---|---|
| URUN-01 | Arayüz varsayılan olarak Türkçe açılır. | Türkçe varsayılan metinler ve doğal, kısa mesajlar. | İlk açılışta dil ve metin incelemesi. | Kararlaştırıldı |
| URUN-02 | Özet Türkçe veya İngilizce üretilebilir. | Dil seçici; bu karar PDF-B-04 bonusunu MVP'ye taşır. | İki dilde uçtan uca test. | Kararlaştırıldı |
| URUN-03 | Kullanıcı özet uzunluğunu seçebilir. | Sınırlandırılmış kısa/orta/uzun seçenekleri prompta kontrollü aktarılır. | Seçeneğin istek şemasına ve çıktıya yansıması. | Kararlaştırıldı |
| URUN-04 | Kullanıcı çıktı biçimini seçebilir. | Paragraf veya madde listesi gibi izinli değerler. | Şema ve UI testleri. | Kararlaştırıldı |
| URUN-05 | Kullanıcı hedef kitleyi seçebilir. | İzinli hedef kitle seçenekleri prompta kontrollü aktarılır. | Prompt ve yanıt testleri. | Kararlaştırıldı |
| URUN-06 | Kullanıcı kurumsal özet şablonu seçebilir. | Toplantı notu, bakım/servis kaydı, proje durumu ve yönetici özeti şablonları. | Her şablonun yapılandırılmış alan sözleşmesi test edilir. | Kararlaştırıldı |
| URUN-07 | Kaynak bağlantılı özet desteklenir. | Özet maddeleri kaynak cümle kimliklerine bağlanır; LLM eşleşmesi doğrulanır ve kullanıcıya açılır. | Her referansın var olan kaynak cümlesine işaret ettiği test edilir. | Planlandı |
| URUN-08 | Uygun şablonlarda eylemler, riskler ve kaynakta eksik bilgiler ayrı gösterilebilir. | Yalnızca metinden çıkarılan yapılandırılmış bölümler; bulunmayan değer “Belirtilmemiş” olur. | Uydurma sorumlu/tarih/risk içeren çıktının reddi veya düzeltilmesi. | Planlandı |
| URUN-09 | Sistem kaynakta olmayan bilgiyi üretmez ve yapay güven yüzdesi göstermez. | Prompt kuralı, şema doğrulama ve doğrulanabilir kaynak bağlantıları. | Eksik bilgi ve bilgi uydurma (hallucination) odaklı değerlendirme seti. | Kararlaştırıldı |

## 6. Belirsizlikler, eksikler ve olası gerilimler

| ID | Konu | Tespit | İzlenecek karar |
|---|---|---|---|
| ACIK-01 | Giriş kimliği | PDF “e-posta/kullanıcı adı” ve tek `username_or_email` alanı diyor; ikisinin ayrı ayrı mı yoksa tek kimlik olarak mı destekleneceği açık değil. | Veri modeli öncesi tek alan veya ayrı benzersiz alanlar kararlaştırılacak. |
| ACIK-02 | Kayıt yetkisi | Hem herkese açık “Kayıt Ol” hem adminin kullanıcı oluşturması isteniyor; açık kayıtta admin rolünün nasıl engelleneceği belirtilmiyor. | Açık kayıt yalnızca `user`; `admin` ataması yalnızca admin işlemi olarak önerilir. |
| ACIK-03 | Log içeriği | “Metinler ve/veya yanıtlar”, yalnız AI yanıtı ya da girdi+çıktı seçenekleri verilmiş; zorunlu saklama kapsamı ve süresi yok. Tek satırda kısa giriş istenmesi, hiç girdi saklamama seçeneğiyle gerilimli. | Mahremiyet, teslim kanıtı ve kaynak bağlantısı dikkate alınarak kapsam/saklama süresi seçilecek. |
| ACIK-04 | Şifre işlemi | “Güncelleme/yenileme/sıfırlama” var; geçici şifre, kullanıcıya bildirim veya self-service unutulan şifre akışı tanımlı değil. | MVP'de admin tarafından yeni şifre atama ve oturum iptali önerilir; bildirim kapsam dışı kalabilir. |
| ACIK-05 | Pasif kullanıcı | Pasife almanın mevcut erişim/refresh token'larına etkisi belirtilmiyor. | Her yenileme ve korumalı kritik işlemde aktiflik kontrolü, pasife almada token iptali önerilir. |
| ACIK-06 | LLM seçimi | Sağlayıcı/model, maliyet, veri yerleşimi, kota ve kabul edilebilir gecikme tanımlı değil. | Ölçütlerle ayrı sağlayıcı/model kararı alınacak; mimari sağlayıcıdan bağımsız kalacak. |
| ACIK-07 | Girdi sınırı | Boş metin hatası anılıyor fakat minimum/maksimum uzunluk, desteklenen dil ve dosya girişi belirtilmiyor. | MVP yalnız düz metin; karakter/token sınırı sağlayıcı seçimiyle ölçülebilir biçimde belirlenecek. |
| ACIK-08 | Özet kalitesi | “Kısa ve anlaşılır” dışında ölçülebilir kalite ölçütü bulunmuyor. | Gerçekçi örneklerden kabul veri seti ve kaynakta olmayan bilgi yasağı kullanılacak. |
| ACIK-09 | Teknoloji | PDF teknoloji ve veritabanı türünü serbest bırakıyor. | ASP.NET Core 10, React TypeScript ve PostgreSQL bizim teknik seçimimizdir. |
| ACIK-10 | Yetki modeli | Admin'in Ana Sayfa'ya erişip erişemeyeceği açık değil; yalnız admin paneli erişimi belirtiliyor. | Admin'in de özetleme yapıp yapamayacağı ürün kararı olarak netleştirilecek. |
| ACIK-11 | Kaynak bağlantısı | Kaynak cümle eşlemesinin kesinliği LLM çıktısına bağlıdır. | Cümle kimlikleri, şema doğrulaması ve geçersiz referansı reddetme kullanılacak; semantik doğruluk ayrıca değerlendirilecek. |
