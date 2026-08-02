# Sprint Raporu Şablonu

> Bu dosyayı her sprintte somut kanıtlarla doldurun. “Tamamlandı”, “çalışıyor” veya “test edildi” gibi tek başına doğrulanamayan ifadeler kullanmayın. Uygulanmayan bölüm için **“Uygulanmadı - gerekçe: ...”** yazın; bölümü silmeyin. Secret, şifre, token ve kişisel kullanıcı metni eklemeyin.

## 1. Sprint kimliği

- **Sprint başlığı:**
- **Tarih aralığı:** YYYY-AA-GG - YYYY-AA-GG
- **Rapor tarihi:** YYYY-AA-GG
- **Sorumlu(lar):**
- **Branch:**
- **Başlangıç commit'i:** `<sha>`
- **Bitiş commit'i:** `<sha>` veya “Henüz commit yok”

## 2. Sprint amacı

**Amaç:** Tek cümleyle ölçülebilir sonuç.

**Kabul ölçütleri:**

- [ ] Gözlenebilir davranış + beklenen sonuç + kanıt bağlantısı/komutu.
- [ ] Gözlenebilir davranış + beklenen sonuç + kanıt bağlantısı/komutu.

**Kapsam dışı:** Bu sprintte özellikle yapılmayacak işler ve gerekçeleri.

## 3. İlgili PDF gereksinimleri

| Gereksinim ID | Kısa açıklama | Bu sprintteki karşılığı | Kanıt | Sonuç |
|---|---|---|---|---|
| `PDF-M-..` | | Dosya/endpoint/test | Test adı, ekran görüntüsü veya komut çıktısı | Karşılandı / Kısmi / Karşılanmadı |

> PDF'de olmayan ürün/teknik kararları `URUN-..` veya ilgili ADR ile belirtin; PDF zorunluluğu gibi göstermeyin.

## 4. Tamamlanan kullanıcı senaryoları

| Senaryo | Önkoşul | Adımlar | Beklenen ve gözlenen sonuç | Kanıt |
|---|---|---|---|---|
| Örn. geçerli giriş | Aktif user mevcut | ... | HTTP 200; access token ve HttpOnly refresh cookie... | Test adı / görüntü |

## 5. Ne uygulandı?

Her madde için önce kullanıcıya/sisteme görünen sonucu yazın; “altyapı hazırlandı” gibi belirsiz ifadelerden kaçının.

- **Sonuç:**
  - **Kapsanan senaryolar:**
  - **Kapsanmayan kenar durumları:**
  - **Kanıt:**

## 6. Nasıl uygulandı?

Akışı girişten kalıcılığa/dış servise kadar kısa ve doğrulanabilir biçimde anlatın.

1. İstek ve doğrulama:
2. Application kullanım senaryosu:
3. Domain kuralı:
4. Infrastructure/veritabanı/dış servis:
5. Cevap ve UI davranışı:

## 7. Neden bu çözüm seçildi?

- **Karar verilen sorun:**
- **Seçilen çözüm:**
- **Ölçütler:** Güvenlik, sadelik, test edilebilirlik, maliyet, performans vb.
- **Değerlendirilen alternatifler:**
- **Reddedilme nedenleri:** Somut ödünleşimler.
- **Sonraki gözden geçirme koşulu:** Hangi ölçüm/olay kararı yeniden açar?

## 8. Mimari ve tasarım kararları

| ADR / karar | Sınıf | Sprint etkisi | Bağımlılık yönü | Ödünleşim | Durum |
|---|---|---|---|---|---|
| `ADR-...` | PDF / Ürün / Teknik / Açık | | | | Yeni / Onaylı / Değişti |

Clean Architecture sınırlarında sapma varsa dosya ve gerekçesiyle yazın.

## 9. Değişen dosyalar

| Dosya | Katman | Sorumluluğu | Değişiklik nedeni | İlgili gereksinim/test |
|---|---|---|---|---|
| `path/file` | Domain/Application/... | Tek ve açık sorumluluk | | |

Üretilmiş dosyaları gruplayın; elle yazılmış kodla ayırın. İlgisiz değişiklik varsa açıklayın.

## 10. Veritabanı değişiklikleri

- **Değişen tablo/alan/kısıt/index:**
- **Veri bütünlüğü etkisi:**
- **Kişisel/hassas veri etkisi:**
- **Geriye uyumluluk:**
- **Rollback yaklaşımı:**
- **Yerel ve test verisi etkisi:**

### Migration bilgisi

| Migration | Oluşturma komutu | Uygulama komutu | Geri alma komutu | Temiz DB sonucu | Mevcut DB sonucu |
|---|---|---|---|---|---|
| | | | | Başarılı/Başarısız + çıktı | Başarılı/Başarısız + çıktı |

Migration yoksa “Yok; bu sprintte şema değişmedi” yazın.

## 11. Endpoint ve istek/cevap davranışı

| Method + rota | Yetki | İstek özeti | Başarılı cevap | Hata cevapları | Kanıt |
|---|---|---|---|---|---|
| `POST /api/...` | User/Admin/Anonim | Alanlar ve sınırlar | Status + şema + cookie/header | 400/401/403/409/429/5xx davranışı | Test/Swagger |

Her endpoint için örneklerin gerçek secret/token/kişisel metin içermediğini kontrol edin. API sözleşmesinde değişiklik varsa eski istemci etkisini yazın.

## 12. Doğrulama (validation) ve hata senaryoları

| Senaryo | Beklenen katman | Beklenen status/mesaj | Gözlenen sonuç | Otomatik test |
|---|---|---|---|---|
| Boş metin | Application/API | 400 + doğal Türkçe mesaj | | |
| Yetkisiz rol | API | 403; veri sızıntısı yok | | |
| Sağlayıcı timeout | Infrastructure/API | Güvenli 5xx/uygun eşleme | | |

- Teknik ayrıntı/stack trace kullanıcıya sızdı mı?
- Correlation ID log ve cevapta izlenebiliyor mu?
- UI hata sonrası kullanılabilir durumda mı?

## 13. Otomatik testler

| Test düzeyi | Proje/komut | Test edilen davranış | Geçen | Kalan | Süre | Sonuç kanıtı |
|---|---|---|---:|---:|---:|---|
| Unit | | | | | | |
| Integration | | | | | | |
| Contract/API | | | | | | |
| E2E | | | | | | |
| Prompt/evaluation | | | | | | |

Başarısız/atlanan/flaky testleri adları ve nedenleriyle listeleyin. Yalnız test sayısını değil, hangi kritik davranışların kanıtlandığını yazın.

## 14. Build sonuçları

| Bileşen | Tam komut | Ortam/sürüm | Warning | Error | Sonuç |
|---|---|---|---:|---:|---|
| Backend | `dotnet ...` | | | | |
| Frontend | `npm ...` | | | | |

Komutun son satırlarını veya CI bağlantısını ekleyin. Warning'leri yok saymayın; kabul edilenleri gerekçelendirin.

## 15. Swagger manuel doğrulama

- **Ortam / API sürümü:**
- **Kullanılan test hesabı rolü:** Gerçek kimlik bilgisi yazmayın.
- **Denenen endpointler ve status kodları:**
- **Request/response şeması gözlemi:**
- **Yetkilendirme (authorization) kontrolleri:** Anonim, User ve Admin ayrı.
- **Cookie/header gözlemi:** Değerleri maskeleyin; `HttpOnly`, `Secure`, `SameSite`, expiry gibi nitelikleri yazın.
- **Kanıt:** Tarihli ekran görüntüsü veya kayıt.

## 16. Tarayıcı ve F12 manuel doğrulama

| Tarayıcı/sürüm | Senaryo | Viewport | Console | Network | Application/Cookies | Sonuç/kanıt |
|---|---|---|---|---|---|---|
| | | | Hata sayısı | Status/timing | Güvenlik öznitelikleri | |

Kontrol listesi:

- [ ] Türkçe varsayılan arayüz, doğal ve kısa metinler.
- [ ] Klavye kullanımı, label/focus ve temel erişilebilirlik.
- [ ] Loading, boş, başarı ve hata durumları.
- [ ] User/Admin menü ve doğrudan URL davranışı.
- [ ] Console'da hata veya hassas veri yok.
- [ ] Network cevabında stack trace, secret veya gereksiz kişisel veri yok.
- [ ] Refresh cookie JavaScript tarafından okunamıyor; access token kalıcı web storage'a yazılmıyor.
- [ ] Mobil ve masaüstü görünümde taşma/kırılma yok.

## 17. Güvenlik ve mahremiyet kontrolleri

| Kontrol | Yöntem | Beklenen | Sonuç | Kanıt / bulgu |
|---|---|---|---|---|
| Şifre saklama | DB/test inceleme | Salt'lı güçlü hash; düz metin yok | | |
| Refresh token | DB/cookie inceleme | DB'de hash, cookie HttpOnly/Secure | | |
| Rol kontrolü | Doğrudan API/URL | User admin verisine erişemez | | |
| Secret taraması | Komut/CI | Kaynakta anahtar yok | | |
| Log maskeleme | Uzun/hassas örnek | Tam içerik/token logda yok | | |
| Rate limit | Tekrarlı istek | Sınırda 429 | | |
| Prompt güvenliği | Enjeksiyon örnekleri | Sistem kuralları ve şema korunur | | |
| Kaynak sadakati | Değerlendirme örnekleri | Olmayan bilgi üretilmez | | |

Bulgu varsa önem derecesi, etkilenen sürüm ve çözüm/defer kararını yazın.

## 18. Karşılaşılan problemler, kök nedenler ve çözümler

| Problem / belirti | Etki | Kök neden kanıtı | Uygulanan çözüm | Çözüm doğrulaması | Tekrar önleme |
|---|---|---|---|---|---|
| | | Log/test/commit | | Test/ölçüm | |

“Paket sorunu vardı” yerine paket/sürüm, hata mesajının güvenli özeti, kök neden ve doğrulama komutunu yazın.

## 19. Öğrenilen kavramlar

| Kavram | Kendi cümlemizle açıklama | Bu sprintteki somut örnek | Yanlış anlaşılma / dikkat noktası |
|---|---|---|---|
| | | Dosya/test/akış | |

## 20. Bilinen sınırlılıklar ve ertelenen işler

| Konu | Tür | Neden şimdi yapılmadı? | Risk/etki | Takip işi / hedef sprint | Kabul eden |
|---|---|---|---|---|---|
| | Bilinen sınırlılık / Teknik borç / Kapsam dışı | | | | |

Erteleme, gereksinimi “tamamlandı” saymak için kullanılmamalıdır.

## 21. Commit bilgisi

| SHA | Mesaj | Kapsam | İlgili gereksinim | Build/test durumu |
|---|---|---|---|---|
| | | | | |

- **Çalışma ağacı durumu:** `git status --short` çıktısı
- **Diff özeti:** `git --no-pager diff --stat` çıktısı
- **Commit/push yapılmadıysa nedeni:**

## 22. Sonraki sprint

- **Önerilen hedef:**
- **Öncelikli PDF gereksinimleri:**
- **Önkoşullar / açık kararlar:**
- **Ölçülebilir kabul ölçütleri:**
- **Riskler ve azaltma yaklaşımı:**

## 23. Kanıta dayalı ifade örneği

**Kaçınılacak:** “Authentication tamamlandı.”

**Tercih edilecek:** “`<kararlaştırılan süre>` ömürlü access token ve `HttpOnly; Secure; SameSite=...` refresh-token cookie uygulandı. Refresh token veritabanında yalnız hash olarak tutuluyor. Login, refresh, logout, süresi dolmuş token, iptal edilmiş token, pasif kullanıcı ve geçersiz kimlik bilgisi senaryoları `<test adları>` ile doğrulandı; komut sonucu `<N> geçti, 0 başarısız` oldu.”
