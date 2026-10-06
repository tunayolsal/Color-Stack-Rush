# Color Stack Rush 0.4.0 — tarayıcı, zorluk ve oyuncu kabulü

Her sonuçta commit, yayın sürümü, tarayıcı, cihaz ve kalite ayarını kaydet. Yapılmamış ölçümler bekliyor olarak kalır; mobil ekran simülasyonu fiziksel telefon testi değildir.

## Parkur ve otomatik kontroller

- Unity Test Runner veya [README'deki batch komutları](README.md#testler) ile EditMode ve PlayMode çalıştır; XML, günlük ve kullanılan komutu sakla. Önceki sürümün başarılı raporunu yeni sürüm için kullanma.
- 1.000 bölüm ile 18, 19, 20, 100, 1.000 ve 1.000.000 kimliklerinde bütün sonlu `CoursePlan` doğrulanmalı; aynı bölüm ve içerik sürümü aynı geometri, renk sırası ve hareket fazlarını üretmeli.
- Dört açılış kapısı dönüşümlü olmalı; her biri önünde tek doğru blok, ardından ilk uygun tam segmentte sekiz doğru blok bulunmalı. Kapı çarpışma alanı ile görünen boşluk aynı ölçüde olmalı; havuzdan tekrar alınan kapı eski boşluğu taşımamalı.
- Hiç dokunmama, sabit yan konum ve tek sürüklemeden sonra bekleme kazanamamalı. Ölüm tek sonuç üretmeli ve sonraki bölümü açmamalı. Sürekli doğru rota ise gerçek giriş/fizik döngüsünde bitişe ulaşabilmeli.
- Renk uyarısı en az iki saniye olmalı; korunan geçiş alanında blok veya engel bulunmamalı. Alternatif parkur da aynı yönlendirme ve blok bütçesi kurallarını korumalı.
- İçerik sürümü 2, kayıt sürümü 3 olmalı. Eski para, kozmetik ve bölüm kayıtları korunmalı; kayıt yazma başarısızlığında satın alma ve ilerleme geri alınmalı.

Hızlandırılmış bot, çok sayıda parkur için regresyon kontrolüdür. Fare girdisi `Update`, hareket `FixedUpdate` üzerinden işlendiği için hızlandırma gerçek kontrol gecikmesini değiştirir. Normal hızda ayrıca doğrula; botun rota örnekleme gecikmesini doğrudan üretici hatası olarak raporlama. Unity Editör testi fiziksel cihaz FPS ölçümü değildir.

## Jev AI oynanış süiti

[Kurulum ve komutlar](tools/jev/README.md) doğrudan TypeSafe `jev-1.13.0` içindir. Önce `python tools/jev/test_driver.py` çalıştır. [README'deki yerel Jev WebGL komutuyla](README.md#doğrudan-jev-ai-testleri) Development çıktısını üret; sürücüyü `--web-root` ile bu çıktıya bağlayıp yerel sayfanın düğmesinden 32 koşuyu başlat. Testler ayrı kayıt klasörüne yazılmalı; bitişte önceki yerel kayıt geri yüklenmeli. Anahtar rapora veya WebGL dosyalarına yazılmamalı; test sembolü sürüm derlemesinde kaldırılmalı.

`viewportWidth`, `viewportHeight` ve `cameraAspect` her raporda bulunmalı. İzlenen sayfanın 540×960 dikey ekranı ile kamera oranı eşleşmeli. Editör alternatifi `JevSuiteEditorRunner.Run` kullanır, fakat gerçek Game View de dikey olmalı; komut satırındaki ekran bayrakları tek başına yeterli değildir. Yatay veya uyumsuz oranlı denemeyi nihai zorluk kanıtına ekleme.

1, 4, 6, 7, 12, 13, 19 ve 1.000 numaralı bölümlerde normal, +150 ms, +300 ms ve bir hatadan toparlanma profillerini ayrı değerlendir. Normal profil için en az 6/8 bitiş başlangıç hedefidir. Toparlanma koşusunda hatalı girişin uygulanıp gerçekten temas oluşturduğunu kaydet; uygulanmamış hata denemesini toparlanma başarısı sayma.

`decisions.jsonl`, `actions.jsonl`, `runs.jsonl`, sürücü ve Unity özetlerini birlikte sakla. Hataları oyun, model kararı, bağlantı ve test bağlantısı olarak kanıtlarıyla ayır. Model yalnız görünür sahne/HUD verisi almalı; tohum, gizli rota ve görünmeyen gelecek nesneler gözlemde bulunmamalı. Jev'in JSON üzerinden oynayabilmesi görsel algı veya insanlara uygun zorluk kanıtı değildir. Koşular tamamlanana kadar sonuçları **bekliyor** olarak belirt.

## Tarayıcı, görseller ve ses

Chrome/Edge/Firefox masaüstü, Android Chrome ve iOS Safari için ayrı ayrı kontrol et:

- HTTPS linki hesap girişi istemeden açılıyor; yükleme yüzdesi/hata/yeniden deneme çalışıyor.
- Fare/A-D veya tek parmak sürükleme çalışıyor; ikinci parmak ve UI'da başlayan hareket engelleniyor.
- Sekme değişiminde duraklatma, dönüşte sıçramasız devam ve Oyna sonrası ses.
- Sonuçtan sonra sayfa yenileme açık bölümü/parayı koruyor; başarısız kayıt değişiklikleri geri alıyor.
- Dar/uzun/yatay ekranlarda HUD kesilmiyor; 18→19→20 ve eski bölüme dönüş çalışıyor.
- 320×568, 390×844 ve 1440×900 görüntülerde üç temayı kontrol et: başlangıç, kapı yaklaşımı, renk uyarısı, hasar, merdiven ve sonuç.
- İlerleme çizgisi duraklatma düğmesini kesmiyor; renk adı kutunun altında ortalı. Başlangıç kuyruğu aktif renkte, yanlış renk bildirimi gerçek kaybı gösteriyor. Yeniden başlatmada kamera ilk karede doğru yerde; yol kenarları kesintisiz ve sonuç arkasında büyük bonus etiketleri görünmüyor.
- Oyna ile müzik ve efektler açılıyor; müzik/efekt ayarları ayrı çalışıyor ve yenilemede korunuyor. Happy Clappy Loop geçişi tekrarlı dinlemede tıklama veya belirgin boşluk oluşturmuyor. Doğru toplama, yanlış renk, hasar, düğme, renk geçişi ve güçlendirme sesleri ayırt ediliyor; kombo sesi aşırı yükselmiyor.
- Hazır seslerin dosya, kaynak ve CC0 lisansı [manifest](Assets/Audio/audio-manifest.json) ile eşleşiyor; yeni klipler lisans kaydı olmadan eklenmiyor.
- 30 dakika bölüm döngüsünde çökme ve kalıcı bellek artışı bulunmuyor.

## Fiziksel cihaz ve performans

Masaüstünde 60 FPS, desteklenen telefonda en az 30 FPS hedefle. Isınma sonrası medyan/yüzde95 kare süresi, draw call, görünür üçgen, aktif parçacık ve belleği kaydet. Başlangıç bütçesi: tek gölgeli ışık, 100 bin görünür üçgen, 90 draw call, 150 aktif parçacık ve en fazla 25 MB ilk indirme. Aşımda önce çözünürlük/dekor/efekt yoğunluğunu azalt.

Beş yeni oyuncuya dışarıdan kontrol/renk açıklaması yapmadan oynat:

| Oyuncu | Renk kuralını öğrendi | Sonraki bölümlerde yönlendirme gerektiğini söyledi | Karışan an |
| --- | --- | --- | --- |
| 1 | Bekliyor | Bekliyor | |
| 2 | Bekliyor | Bekliyor | |
| 3 | Bekliyor | Bekliyor | |
| 4 | Bekliyor | Bekliyor | |
| 5 | Bekliyor | Bekliyor | |

En az dört oyuncu renk kuralını öğrenmeli; en az üçü sonraki bölümlerin yönlendirme gerektirdiğini belirtmeli. Karşılanmazsa öğretici, kamera ve DifficultyProfile ayarlarını düzelt.
