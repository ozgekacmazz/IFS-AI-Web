# Ürün Kapsamı

## 1. Ürün amacı

**[PDF]** Ürün, kullanıcının yazdığı paragraf veya metni kontrollü bir prompt akışıyla LLM'e gönderip kısa ve anlaşılır bir özet döndüren web uygulamasıdır. Kimlik doğrulama, rol bazlı erişim, yönetici denetim kaydı (audit log) görünümü ve kullanıcı yönetimi temel teslim kapsamındadır.

**[Ürün kararı]** Ürün, genel amaçlı sohbet aracı değil; yalnızca kullanıcı tarafından sağlanan metni özetleyen, kaynağa sadakati öne çıkaran bir araçtır. Kaynakta olmayan sorumlu, tarih, risk veya başka ayrıntılar tahmin edilmez; gerekli alanda **“Belirtilmemiş”** denir. Yapay veya ölçülemeyen güven yüzdesi gösterilmez.

## 2. Hedef kullanıcılar ve roller

**[Ürün kararı] Hedef kullanıcılar:** Uzun veya kurumsal metinlerden hızlı, kaynağa sadık bir özet çıkarmak isteyen çalışanlar, yeni mezun/proje ekibi üyeleri ve uygulamanın kullanıcılarıyla kayıtlarını yöneten sistem yöneticileridir. Belirli bir sektör ya da gerçek IFS müşterisi varsayılmaz; bakım, servis ve proje örnekleri yalnız özet şablonlarıdır.

### User

**[PDF]** Kayıt olur/giriş yapar, Ana Sayfa'ya erişir, metnini gönderir ve özeti görür. Admin sayfalarına erişemez.

**[Ürün kararı]** Özet dili, uzunluğu, çıktı biçimi, hedef kitle ve kurumsal şablon seçebilir. Kaynak bağlantılı sonuçları ve uygun olduğunda yapılandırılmış bölümleri inceler.

### Admin

**[PDF]** Admin Paneli menüsünü görür; logları listeler, kullanıcı oluşturur, şifre günceller/sıfırlar ve kullanıcıyı aktif/pasif yapar.

**[Açık karar]** Admin rolünün Ana Sayfa'da özetleme yapıp yapamayacağı PDF'de belirtilmemiştir.

## 3. Temel kullanıcı senaryoları

1. **Kayıt ve giriş - [PDF]:** Ziyaretçi e-posta/kullanıcı adı ve şifreyle kaydolur; geçerli bilgilerle giriş yapar.
2. **Temel özetleme - [PDF]:** User tek metin alanına içerik girer, “Özetle”ye basar ve LLM özetini sonuç alanında görür.
3. **Özeti yapılandırma - [Ürün kararı]:** User Türkçe/İngilizce dilini, uzunluğu, çıktı biçimini, hedef kitleyi ve kurumsal şablonu seçer.
4. **Kaynağı izleme - [Ürün kararı]:** User bir özet cümlesinin/maddesinin dayandığı kaynak cümleleri açar.
5. **Kurumsal çıktı - [Ürün kararı]:** Uygun şablon seçildiğinde sonuç; eylemler, riskler ve eksik bilgiler gibi bölümler içerir. Yalnızca kaynakta bulunan bilgi olgu olarak sunulur.
6. **Log inceleme - [PDF]:** Admin tarih, kullanıcı, kısa giriş ve kısa özet bilgileriyle istek/yanıt kayıtlarını görür.
7. **Kullanıcı yönetimi - [PDF]:** Admin kullanıcı oluşturur, rol belirler, şifre sıfırlar ve durumu değiştirir.
8. **Hata geri bildirimi - [PDF]:** Boş girdi veya AI hizmet hatasında kullanıcı yalın ve kibar mesaj görür.

## 4. MVP kapsamı

### PDF'nin zorunlu kıldığı kapsam

- Kayıt olma ve e-posta/kullanıcı adı + şifreyle giriş.
- User için tek metin alanlı Ana Sayfa, “Özetle” eylemi ve sonuç alanı.
- Prompt tabanlı LLM özetleme akışı.
- Admin'e özel panel, log listesi ve kullanıcı yönetimi.
- Kullanıcı oluşturma, şifre güncelleme/sıfırlama, rol ve aktif/pasif durum yönetimi.
- Sunucu tarafında rol kontrolü; admin URL/API erişiminin korunması.
- Boş metin ve AI/API hatalarında anlaşılır mesajlar.
- API anahtarlarının kaynak koda gömülmemesi.
- Çalışan kaynak kod, kurulum/çalıştırma README'si ve istenen ekran görüntüleri.

### Bizim MVP ürün kararlarımız

- Arayüz Türkçe açılır; metinler kısa, doğal ve profesyoneldir.
- Özet Türkçe veya İngilizce üretilebilir. Bu, PDF'de bonusken ürün kararıyla MVP'ye alınmıştır.
- Kullanıcı özet uzunluğu, çıktı biçimi, hedef kitle ve kurumsal şablon seçebilir.
- İlk şablon kümesi: toplantı notu, bakım kaydı, servis kaydı, proje durumu ve yönetici özeti. Kesin alan sözleşmeleri geliştirme öncesinde daraltılır.
- Yapılandırılmış AI cevabı şema doğrulamasından geçer.
- Kaynakta olmayan bilgi üretilmez; eksik ayrıntı “Belirtilmemiş” olarak gösterilir.
- Kaynak bağlantılı özet temel farklılaştırıcıdır. Teknik risk nedeniyle kabul ölçütü, referansların geçerli kaynak cümlelerine bağlanmasıdır; LLM'in anlamsal dayanak doğruluğu ayrıca örnek veri setiyle değerlendirilir.
- Güvenlik gereği kısa ömürlü access token, HttpOnly refresh cookie ve hash'lenmiş refresh token kullanılır.

## 5. Gelişmiş veya isteğe bağlı özellikler

### PDF'deki opsiyonel/bonus özellikler

- Kullanıcının son üç özetini görmesi (**opsiyonel**).
- Mimari, AI entegrasyonu ve sınırlılıkları içeren kısa teknik rapor (**opsiyonel**).
- Kullanıcı başına oran sınırlama (**bonus; güvenlik nedeniyle erken uygulanması önerilir**).
- Uzun girdide yalnız ilk X karakteri loglama (**bonus; mahremiyet nedeniyle erken uygulanması önerilir**).
- Admin için son yedi gün özet sayısı mini grafiği (**bonus; MVP sonrası**).
- Özet dili seçimi (**bonus; ürün kararıyla MVP'ye alınmıştır**).

### Bizim ileri aşama seçeneklerimiz

- Şablonların yönetilebilir hâle getirilmesi; ilk sürümde kod/yapılandırma ile sınırlı sabit şablonlar yeterlidir.
- Kaynak bağlantılarının kullanıcı geri bildirimiyle kalite değerlendirmesi.
- Saklama politikası tanımlandıktan sonra kullanıcının son üç özet geçmişi.

## 6. Kapsam dışı

Bu ürün aşağıdakilere dönüşmeyecektir:

- Görev yönetim sistemi; eylem maddeleri atanmaz, takip edilmez veya tamamlandı olarak işaretlenmez.
- Bağımsız risk analiz sistemi; yalnız kaynak metinde bulunan risk ifadeleri özetlenir.
- Workflow motoru; süreç adımı, otomasyon veya durum makinesi çalıştırılmaz.
- Gerçek IFS entegrasyonu; IFS'e veri okunmaz/yazılmaz.
- Onay yönetimi platformu; onay talebi, yetki zinciri veya elektronik onay yoktur.
- Genel amaçlı sohbet, metin üretimi, belge yazma ya da bilgi tamamlama aracı.
- Dosya yükleme, OCR, ses/video özetleme ve URL'den içerik çekme (PDF yalnız metin alanı ister).
- E-posta/SMS ile şifre bildirme; ayrıca kararlaştırılmadıkça admin şifre sıfırlaması arayüz içi kalır.
- Ölçülemeyen güven puanı veya yüzdesi.

## 7. Kabul sınırları

MVP aşağıdaki sınırlar sağlandığında ürün açısından kabul edilebilir:

- Kimliği doğrulanmış User geçerli düz metni gönderir ve seçilen ayarlara uygun, şeması geçerli bir özet görür.
- Boş/geçersiz girdi LLM'e gönderilmez; sağlayıcı hataları teknik ayrıntı sızdırmayan Türkçe mesajla gösterilir.
- Özet, kaynakta olmayan kişiyi, tarihi, sorumluyu veya riski olgu gibi sunmaz. Zorunlu şablon alanında bilgi yoksa “Belirtilmemiş” döner.
- Kaynak referansı verilen her özet öğesi, gönderilen metindeki var olan bir cümle kimliğine bağlanır. Geçersiz referans kullanıcıya sunulmaz.
- Türkçe ve İngilizce seçimleri çıktı dilini belirler; arayüz ilk açılışta Türkçedir.
- User doğrudan URL veya API çağrısıyla admin işlevlerine erişemez; Admin log ve kullanıcı yönetimi işlemlerini yapabilir.
- Şifreler ve refresh token'lar düz metin saklanmaz; API anahtarları kaynak kodda bulunmaz.
- Logların tam girdi/çıktı saklama kapsamı ve saklama süresi canlı kullanımdan önce yazılı karara bağlanır.
- Teslim README'si temiz ortam adımlarını, ekran görüntüleri PDF'deki üç kanıt grubunu kapsar.

## 8. Açık ürün kararları

- Tek bir `username_or_email` değeri mi, ayrı kullanıcı adı ve e-posta mı kullanılacak?
- Admin Ana Sayfa'yı kullanabilecek mi?
- Logda tam girdi, kısaltılmış girdi veya yalnız özet mi saklanacak; saklama süresi ne olacak?
- Metin için karakter/token sınırı ve desteklenen kaynak diller neler olacak?
- Şablonların MVP'deki kesin alanları ve “uygun olduğunda” gösterilecek bölümlerin kuralları neler olacak?
- Son üç özet özelliği MVP'ye alınacak mı?
- LLM sağlayıcı/model, maliyet, gecikme, veri işleme ve kota ölçütlerine göre hangisi olacak?
