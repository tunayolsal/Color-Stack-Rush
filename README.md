# Color Stack Rush

Unity **6000.3.3f1**, sürüm **0.4.0**. Yalnız renk eşleştirmeye dayalı dikey 3D oyun; bu teslim kurallı parkur üretimi, Jev AI oynanış testleri ve WebGL tarayıcı sürümüne odaklanır.

Fareyle veya tek parmakla sağa/sola sürükle; bilgisayarda A/D ve yön tuşları da kullanılabilir. Aktif renkteki bloklar kuyruğu büyütür ve puan verir. Yanlış renk iki, engel üç blok götürür. Kuyruk en fazla 32 blok taşır; sonraki doğru toplamalar puan vermeye devam eder. Kozmetikler aktif rengi örtmez ve oyun avantajı vermez.

[Tarayıcıda oyna](https://color-stack-rush.tnaylsl1327.chatgpt.site) — hesap gerektirmez.

## Bölümler ve görünüş

Her koşunun bir bitişi vardır. Bölüm 1, 2, 3… biçiminde devam eder; ayrı sonsuz mod veya 18 bölüm sınırı yoktur. Bölümler tek üreticiden çıkar; 1.000 ayrı parkur elle yazılmaz. Bölüm kimliği ve içerik sürümü sabit rastgeleliği belirler; aynı sürümde aynı bölümü yeniden denemek aynı parkuru açar. Üç tema altışar bölümde döner.

`CoursePlan` önce geçilebilir rota ve dönüş noktalarını üretir, ardından sekiz desenin bloklarını, yanıltıcı renklerini ve engellerini bu rotanın çevresine yerleştirir. İlk bölüm dahil her parkurun başlangıcında dört dönüşümlü geçiş kapısı bulunur. Boşluklar 2,5 birim genişlikte, merkezleri ±1,9 ve kapılar arası mesafe en az 36 birimdir; gerçek konumlar uygun alan içinde tohumdan hesaplanır. Her kapıdan altı birim önce tek doğru renkli blok vardır. Dört blokluk başlangıç kuyruğuna bu bölümde başka doğru blok veya güçlendirme eklenmez. Dördüncü kapıdan sonraki ilk tam, uygun segment sekiz doğru blok verir. Böylece yönlendirme zorunludur ve temiz oynayan oyuncunun üç yıldız için yeterli bloğu olur.

Üretici bütün sonlu parkuru doğrular: oyuncu/engel çarpışma ölçüleri, hareketli engelin süpürdüğü alan, yatay hareket süresi, renk geçişleri ve blok bütçesi kontrol edilir. Sabit merkez ve yan rotaların dört kapıyı canlı geçememesi de doğrulanır. En fazla sekiz deterministik aday denenir; doğrulanmış alternatif parkur aynı dört dönüşü ve ödülü korur. Bölüm ilerledikçe ek kapılar ve desen birleşimleri sıklaşır. Üretim koşu başında yapılır; oynarken hazır plan havuzlanmış nesnelerle, yeniden kullanılan segment tamponundan akıtılır.

İlk 18 bölümün uzunlukları korunur; sonrakiler 480–624 birimde kalır. Hız 1–6 arasında 10–12, 7–12 arasında 12,5–14 ve 13–18 arasında 14,5–16 olur; ardından 16'da kalır. Mutlak hız sınırı 18'dir. Kolay toplama segmentleri seyrekleşir, güvenli rota merkezin dışına kayar. Rota kontrol hızı ve engellerin süpürdüğü alanla doğrulanır. Renk geçişinden en az iki saniye önce uyarı ve boş geçiş bölgesi vardır.

Tamamlama bir yıldız, sekiz merdiven iki ve on dört merdiven üç yıldız verir. Eski bölümler sayfalı ekrandan tekrar oynanabilir. Parlak oyuncak görünüşü, ortak yuvarlatılmış blok modeli, renkli oyuncu, ince halka, nötr yol ve okunabilir engeller kullanır. Geometrik renk simgeleri bulunmaz. Türkçe HUD, güvenli alan, azaltılmış hareket ve kalite seçenekleri vardır.

Hazır CC0 görseller: [Kenney Particle Pack](https://kenney.nl/assets/particle-pack), [Kenney UI Pack](https://kenney.nl/assets/ui-pack), [Kenney Mini Forest](https://kenney.nl/assets/mini-forest). Quaternius indirme kotası nedeniyle çevrede sekiz Mini Forest modeli kullanılır. Dosya/lisans kayıtları [Assets/Art/SOURCES.md](Assets/Art/SOURCES.md) içindedir.

Müzik, OwlishMedia'nın CC0 [Happy Clappy Loop](https://opengameart.org/content/happy-clappy-loop) döngüsüdür. Toplama, yanlış renk, hasar, düğme, renk geçişi ve diğer efektler CC0 [Kenney Interface Sounds](https://kenney.nl/assets/interface-sounds) paketinden gelir. Dosya eşlemeleri, kaynak, lisans, süre, dönüşüm ve SHA-256 kayıtları [ses manifestinde](Assets/Audio/audio-manifest.json); Kenney lisansı [burada](Assets/Audio/Kenney-License.txt) bulunur. Müzik ve efektlerin ses ayarları ayrıdır; kombo toplama sesinin perdesini artırır. Tarayıcıda ses ilk Oyna etkileşiminden sonra açılır.

## Aç ve WebGL derle

Unity Hub'dan projeyi 6000.3.3f1 ile aç; WebGL Build Support kurulmuş olmalıdır. SampleScene veya ColorStackRushRelease sahnesinde Play'e bas. **Tools → Color Stack Rush → Build Browser Preview** tarayıcı dosyalarını üretir.

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.3.3f1/Editor/Unity.exe' `
  -batchmode -quit -projectPath 'C:/projects/Color-Stack-Rush' `
  -buildTarget WebGL -executeMethod ReleaseBuilder.BuildWebPreview `
  -outputPath 'C:/builds/ColorStackRush-Web' -logFile 'C:/builds/web.log'
```

Çıktıyı HTTP/HTTPS üzerinden sun; index.html dosyasını çift tıklamak yeterli değildir. Varsayılan derleme Brotli sıkıştırmasını Unity `.unityweb` açma desteğiyle kullanır; Sites statik sunucusu `_headers` kurallarını uygulamadığı için bu ayar gerekir. JavaScript ile açma ilk yüklemeyi biraz uzatabilir ve native WASM streaming kullanmaz. Kendi sunucunda native açma için derleme komutuna `-nativeWebDecompression true` ekle; bu durumda Brotli için `Content-Encoding: br`, WebAssembly için `Content-Type: application/wasm` gerekir. Çıktıdaki `_headers` bu kuralları destekleyen sunucular içindir. Oyun üst seviye sayfada çalışır; iframe kullanılmaz. Telefon çözünürlüğü sınırlanır. Sayfa yükleme ilerlemesi, hata sonrası yeniden deneme ve isteğe bağlı tam ekran içerir.

## Kayıt ve mimari

`LevelCatalog.Get(long)` sonlu bölüm tanımını hesaplar; parkur içerik sürümü **2**'dir. `TrackPlanner.CreateCourse(RunConfig)` doğrulanan `CoursePlan` üretir; `SpawnManager` bunu akıtır. `RunConfig.Level(long)` koşuyu, `GameManager.StartRun(RunConfig)` yaşam döngüsünü yönetir. Ölüm/bitiş sonucu tek kez kaydedilir; hızlı yeniden başlatma eski animasyonları ve sonuçları iptal eder. Hareket FixedUpdate/Rigidbody.MovePosition kullanır.

SaveData v3 yalnız oynanan bölümlerin kayıtlarını ve en yüksek açık bölümü saklar. V1/v2 göçü para, kozmetik, ayar ve sonuçları korur; 18 tamamlanmışsa 19 açılır. Eski genel/sonsuz rekorları arşivlenir, bölüm skorlarına karışmaz.

Yerel kayıt geçici dosya ve sağlam yedek kullanır. Web kaydı IndexedDB senkronizasyonunu bekler; `SaveManager.SaveAsync` sonucu bildirir. Satın alma/günlük ödül/bölüm ilerlemesi başarısız yazmada geri alınır. Ses ilk oynama etkileşiminde açılır; arka plandaki sekme oyunu duraklatır. Tarayıcı verileri aynı site ve tarayıcıya aittir; yerel bilgisayar/Android kayıtları otomatik aktarılmaz. Tarayıcı verilerini silmek ilerlemeyi silebilir.

## Testler

Unity Test Runner üzerinden EditMode ve PlayMode çalıştır. Batch testlerde `-quit` ekleme:

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.3.3f1/Editor/Unity.exe' `
  -batchmode -projectPath 'C:/projects/Color-Stack-Rush' `
  -runTests -testPlatform EditMode `
  -testResults 'C:/builds/editmode.xml' -logFile 'C:/builds/editmode.log'
# Aynısını -testPlatform PlayMode ile de çalıştır.
```

Testler büyük bölüm kimliklerini, 1.000 gerçek sonlu bölümde rotaları ve hareketsiz oyuncunun blok bütçesini, deterministik yeniden denemeyi, doğrulanmış alternatif parkuru, göç/hata kurtarmayı, tek sonucu, havuz ve dokunma sahipliğini kapsar. PlayMode botu gerçek Input System girdisi ve fizik döngüsünü kullanır; `-snapshotPath` ekran görüntüsü/bölüm CSV'si üretir. Hızlandırılmış koşulara ek olarak normal hızda oynanış kontrolü gerekir; hızlandırma fare olayları ile fizik adımlarının zamanlamasını değiştirir.

## Doğrudan Jev AI testleri

[Jev test sürücüsü ve çalıştırma komutları](tools/jev/README.md), doğrudan TypeSafe **`jev-1.13.0`** modelini kullanır. Model kamera görünümündeki nesne ayak izleri ve mevcut HUD durumundan sınırlı sürükleme kararları seçer. Tohum, `CoursePlan`, güvenli rota ve görünmeyen gelecek nesneler gönderilmez; oyun karar beklerken normal hızda ilerler. Anahtar yerel Python sürecinde kalır. Test bağlantısı herkese açık WebGL paketine dahil edilmez.

İzlenebilen test için ayrı yerel Development WebGL derlemesi kullanılır. `JevWebPreviewBuilder.Build` aynı oyun başlangıcını 540×960 dikey ekranla derler; geçici test sahnesini ve Jev sembolünü işlem sonunda temizler. Bu çıktı yayımlanmaz. Komuta `-quit` ekleme; derleyici sembol değişimi sonrasındaki derlemeyi bekleyip kendisi çıkar:

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.3.3f1/Editor/Unity.exe' `
  -batchmode -projectPath 'C:/projects/Color-Stack-Rush' -buildTarget WebGL `
  -executeMethod ColorStackRush.Testing.JevWebPreviewBuilder.Build `
  -outputPath 'C:/builds/ColorStackRush-Jev-Local' -logFile 'C:/builds/jev-web.log'
```

Yerel sürücüyü bu çıktıyı gösteren `--web-root` seçeneğiyle başlat ve `http://127.0.0.1:8877` sayfasındaki Jev başlat düğmesini kullan. Test ayrı kayıt klasöründe çalışır; bitince önceki yerel ilerleme geri yüklenir. Süit gerçek ekran boyutunu ve kamera oranını raporlar. Editör alternatifi gerçek Game View dikey olduğu zaman kullanılabilir; yalnız `-screen-width/-screen-height` vermek bu oranı garanti etmez.

Süit 1, 4, 6, 7, 12, 13, 19 ve 1.000 numaralı bölümleri normal, 150 ms ek gecikme, 300 ms ek gecikme ve bir hatadan toparlanma profilleriyle oynar: toplam 32 koşu. Sonuçlar karar, giriş, temas, gecikme ve bitiş kayıtlarıyla değerlendirilir. Normal profilde en az 6/8 bitiş başlangıç hedefidir; ulaşılmayan hedef veya bağlantı hatası başarılı sonuç olarak gösterilmez. Bu süit görsel algı ya da insan oyuncu kabul testi yerine geçmez. Tamamlanmış test sonuçları sürümün doğrulama raporunda ayrıca yayımlanır.

Fiziksel FPS, ses döngüsü, ekran düzeni ve oyuncu kabulü için [WEB_QA.md](WEB_QA.md) listesini uygula. Hedef FPS, Editör koşusu veya mobil ekran simülasyonu fiziksel cihaz ölçümü değildir. Reklam, hesap, sunucu, çevrimiçi sıralama ve yeni APK bu teslimde yoktur.
