# Color Stack Rush 0.3.0 — Web doğrulaması

Tarih: 4 Ekim 2026. Unity: 6000.3.3f1. Bu teslimde APK üretilmedi.

## Uygulanan davranış

Yalnız renk eşleştirme, ortak yuvarlatılmış bloklar, renkli oyuncu ve torus, Türkçe arayüz, yeni çevre dekorları ve havuzlu efektler. Ayrı sonsuz mod yoktur; biten bölümler 1 → 2 → 3… biçiminde devam eder. Tekrar denemede tohum ve parkur değişmez. İlk 18 bölümün uzunluğu korunur; sonrakiler 480–624 birimde kalır. Hız 10–16, mutlak sınır 18; yanlış renk iki ve engel üç blok götürür, engel koruması 0,8 saniyedir.

## Otomatik test kanıtı

- EditMode: **45/45 geçti**. Son XML: `verification/web-editmode.xml`.
- PlayMode regresyonları: **14/14 geçti**. Son XML: `verification/web-playmode.xml`.
- Gerçek girdi/fizik botu: **1/1 geçti**, 23 bölüm içerir. XML: `verification/web-physics-bot.xml`; bölüm ölçümleri: `previews-web/levels-physics.csv`.
- Toplam **60 farklı NUnit testi** geçti. Bot içindeki 23 bölüm ayrıca test sayısına eklenmemiştir.

Bot 1–20, 100, 1.000 ve 1.000.000 numaralı bölümleri bitirdi. Her birinde sıfır yanlış renk ve sıfır engel isabeti, Victory ve sonraki bölüm açılması doğrulandı. Bu kontrol sentetik Input System fare olaylarını ve gerçek FixedUpdate/Rigidbody fiziğini kullanır; oyuncu konumu test tarafından doğrudan yazılmaz. Hızlandırılmış bot koşusu fiziksel cihaz performans ölçümü değildir.

EditMode; en az 1.000 bölüm/tohumda rota/doğrulama, deterministik tekrar, renk geçişleri, büyük bölüm kimlikleri, v2 → v3 göçü, bozuk ana kayıt/yedek kurtarma, havuza çift bırakma ve asenkron kayıt kuyruğunu kapsar. Satın alma, günlük ödül, sıfırlama ve bölüm ilerlemesi başarısız yazımda geri alınır; aynı yazım onayı iki kez işlense de sonuç tekrarlanmaz. Devre dışı kalan eski GameManager yeni koşuya sonuç yayımlamaz.

Tema değiştirme testinde ısınmadan sonra 10.000 zemin güncellemesi sıfır managed allocation ile geçti. Bu sonuç bütün oyun için sıfır GC iddiası değildir; HUD metinleri ve bazı geri bildirim animasyonları tahsis yapabilir.

Normal hızdaki dikey ilk bölüm karesinde oyuncu viewport Y=0,422 (üstten yaklaşık %58), çap=0,950; görünür mesh üçgenleri=4.728 ve mesh renderer sayısı=25 ölçüldü. Renderer sayısı draw call sayısı değildir. İlk sahne ölçümü tüm bölümlerin en yüksek yükünü temsil etmez.

## Tarayıcı yükleme ve depolama

Başlangıç IndexedDB okuması tamamlanmadan oyun başlamaz. İlk okuma hatası Türkçe hata/yeniden yükle ekranında kalır. Yazım FS.syncfs onayını bekler; bölüm sonucu ve satın alma bu onaydan sonra kesinleşir. HTML şablonu tek Unity Oyna ekranı, yükleme yüzdesi, hata sonrası yeniden yükleme ve isteğe bağlı tam ekran içerir. Telefon DPR 1, masaüstü DPR en fazla 1,5'tir. Sekme gizlenince duraklatılır; ses ilk oynama etkileşiminden sonra başlar.

Bağımsız Node kontrolleri; başlangıç bağımlılığı, başarısız ilk okuma, çift yazım onayı, ses dokunuşu, visibility/pagehide ve şablon JavaScript sözdizimi için geçti.

Yerel tarayıcıda ilk bölüm gerçek oyun akışıyla tamamlandı: **3.175 toplam puan = 1.465 koşu puanı + 1.710 merdiven bonusu**, **15 para** ve **üç yıldız**. Sayfa yenilendiğinde ana ekranda **Bölüm 2** ve **15 para** korundu. Bu koşuda konsol hatası görülmedi; Unity'nin elle dosya senkronizasyonu API'si için kullanımdan kaldırma uyarısı kaydedildi. Bu gözlem yerel tarayıcıya aittir; fiziksel telefon testi değildir.

## WebGL derlemesi

Son, Brotli açma desteğini loader'a dahil eden Unity **6000.3.3f1**, sürüm **0.3.0**, WebGL/IL2CPP derlemesi **sıfır hata** ile tamamlandı. Derlemenin UTC zaman damgası `2026-10-04T14:14:17.0926988Z`; derleme süresi yaklaşık **145 saniye**.

Derleme kaydındaki toplam çıktı **10.712.548 bayt**; `Build` klasöründeki Brotli `.unityweb` veri, WASM, framework ve loader dosyalarının toplamı **10.705.680 bayt** (yaklaşık **10,7 MB / 10,2 MiB**). Sıkıştırılmış başlangıç dosyaları 25 MB indirme hedefinin altında. Bu değerler indirme boyutudur; çalışma sırasında kullanılan bellek veya cihaz performansı ölçümü değildir.

Gerçek derlemedeki Brotli dosyaları bağımsız olarak açıldı: WASM dosyasının başlığı ve `UnityWebData1.0` veri başlığı doğru; framework ve tamamlanmış HTML şablonunun JavaScript sözdizimi geçerli. Şablonda çözülmemiş Unity makrosu kalmadı. IndexedDB başlangıç kancası, yazım onayı ve tarayıcı olayları derlemede korunmuş; başlangıç kancası `preRun` çalışmadan önce kuruluyor. Loader özel depolama hata callback'ini ve senkronizasyon ayarını Module'a aktarıyor. Bu dosya kontrolleri gerçek tarayıcı oynanışının yerine geçmez.

Son loader'da Brotli decoder, Worker ile açma ve `Module.wasmBinary` yolu bulundu. Kurulu Unity SDK'sının JavaScript Brotli decoder'ı üç `.unityweb` dosyasını başarıyla açtı; her çıktı Node'un bağımsız Brotli açma sonucu ile bayt düzeyinde aynıydı. Son derlemenin WASM ve framework yükleri ilk derlemedekilerle bayt düzeyinde aynı kaldı; bu yayın uyarlaması oynanış kodunu değiştirmedi.

İlk herkese açık yükleme başarısız oldu: Sites, çıktının `_headers` kurallarını uygulamadı ve sıkıştırılmış dosyaları gereken `Content-Encoding: br` başlığı olmadan sundu. Yayın için derleme ayarı Unity'nin desteklediği **Brotli + `decompressionFallback=true`** yöntemine çevrildi. Bu yöntem `.unityweb` dosyalarını başlangıç sırasında JavaScript ile açar; sunucunun sıkıştırma başlıklarına bağımlılığı kaldırır. Oynanış kodu değişmez. Native WASM streaming derlemesi kullanılamaz; JavaScript ile açma ilk yüklemeyi yavaşlatabilir. `_headers` kuralları destekleyen başka sunucular için kaynakta korunur; Sites'ta uygulandıkları varsayılmaz.

## Herkese açık yayın kontrolü

Oynanabilir adres: [Color Stack Rush](https://color-stack-rush.tnaylsl1327.chatgpt.site). Sites erişimi **public**; oyun hesap girişi istemeden açılır. Son yayın **sürüm 2**, Site kaynağı `4f823968d2fd6840ee1adf53a5768e43c3015c0c`, başarılı yayın zamanı `2026-10-04T14:17:14.432121+00:00`.

Windows üzerindeki Chromium tabanlı uygulama tarayıcısında gerçek HTTPS yüklemesi tamamlandı. Tek Oyna eylemi koşuyu başlattı; fare sürüklemesi oyuncuyu yönlendirdi. Duraklatma menüsü açıldı ve devamda koşu aynı ilerlemeden sürdü. Birinci bölüm 3.175 puan, üç yıldız ve 15 altınla tamamlandı. Yayın sayfası yenilendiğinde Bölüm 2 ve 15 altın korundu. Son yükleme ve yenilemede konsol hatası bulunmadı; Unity elle senkronizasyon API'si için kullanımdan kaldırma uyarısı verdi.

390 × 844 viewport'ta oyun alanı 390 × 693,3 piksel ve dikey olarak ortalanmıştı. 1280 × 720 viewport'ta alan 405 × 720 piksel ve yatay olarak ortalanmıştı; HUD ve duraklatma düğmeleri alanın içinde kaldı. Tam ekran isteği çalıştı; canvas 405 × 720 oranını korudu, Escape ile çıkış denendi ve geçici viewport ayarı sıfırlandı. Bu ölçümler ekran benzetimidir; Android/iOS dokunma, çentik veya cihaz performansı kanıtı değildir. Sonuç ekranı kanıtı: `previews-web/public-victory.jpg`.

## Kaynaklar ve lisans

CC0 Kenney Particle Pack, UI Pack ve sekiz Mini Forest çevre modeli kullanılır. Quaternius paketinin resmi indirmesi kota hatası verdiği için yerine Mini Forest alındı. Orijinal lisanslar ve kaynak kayıtları kaynak arşivindeki `Assets/Art/Licenses` ile `Assets/Art/SOURCES.md` içindedir.

## Yapılmamış ölçümler

Fiziksel Android ve iPhone/Safari testi, düşük/orta sınıf telefon FPS ve ısınma ölçümleri, 90 draw call doğrulaması, gerçek zamanlı 30 dakikalık bellek testi ve beş yeni oyuncuyla kabul testi yapılmadı. 60 FPS masaüstü / 30 FPS telefon hedefleri cihaz ölçümü olarak sunulmaz. Bu kontrollerin adımları kaynak projedeki `WEB_QA.md` içindedir.
