# Color Stack Rush

Unity **6000.3.3f1**, sürüm **0.3.0**. Yalnız renk eşleştirmeye dayalı dikey 3D oyun; bu teslim WebGL tarayıcı sürümüne odaklanır.

Fareyle veya tek parmakla sağa/sola sürükle; bilgisayarda A/D ve yön tuşları da kullanılabilir. Aktif renkteki bloklar kuyruğu büyütür ve puan verir. Yanlış renk iki, engel üç blok götürür. Kuyruk en fazla 32 blok taşır; sonraki doğru toplamalar puan vermeye devam eder. Kozmetikler aktif rengi örtmez ve oyun avantajı vermez.

[Tarayıcıda oyna](https://color-stack-rush.tnaylsl1327.chatgpt.site) — hesap gerektirmez.

## Bölümler ve görünüş

Her koşunun bir bitişi vardır. Bölüm 1, 2, 3… biçiminde devam eder; ayrı sonsuz mod veya 18 bölüm sınırı yoktur. İlk altı bölüm mekanikleri sırayla öğretir. Sekiz desen, bölüm kimliğinin sabit tohumu ile değişik parkurlar oluşturur; aynı bölümü yeniden denemek aynı parkuru açar. Üç tema altışar bölümde döner.

İlk 18 bölümün uzunlukları korunur; sonrakiler 480–624 birimde kalır. Hız 1–6 arasında 10–12, 7–12 arasında 12,5–14 ve 13–18 arasında 14,5–16 olur; ardından 16'da kalır. Mutlak hız sınırı 18'dir. Kolay toplama segmentleri seyrekleşir, güvenli rota merkezin dışına kayar. Rota kontrol hızı ve engellerin süpürdüğü alanla doğrulanır. Renk geçişinden en az iki saniye önce uyarı ve boş geçiş bölgesi vardır.

Tamamlama bir yıldız, sekiz merdiven iki ve on dört merdiven üç yıldız verir. Eski bölümler sayfalı ekrandan tekrar oynanabilir. Parlak oyuncak görünüşü, ortak yuvarlatılmış blok modeli, renkli oyuncu, ince halka, nötr yol ve okunabilir engeller kullanır. Geometrik renk simgeleri bulunmaz. Türkçe HUD, güvenli alan, azaltılmış hareket ve kalite seçenekleri vardır.

Hazır CC0 kaynaklar: [Kenney Particle Pack](https://kenney.nl/assets/particle-pack), [Kenney UI Pack](https://kenney.nl/assets/ui-pack), [Kenney Mini Forest](https://kenney.nl/assets/mini-forest). Quaternius indirme kotası nedeniyle çevrede sekiz Mini Forest modeli kullanılır. Dosya/lisans kayıtları [Assets/Art/SOURCES.md](Assets/Art/SOURCES.md) içindedir.

## Aç ve WebGL derle

Unity Hub'dan projeyi 6000.3.3f1 ile aç; WebGL Build Support kurulmuş olmalıdır. SampleScene veya ColorStackRushRelease sahnesinde Play'e bas. **Tools → Color Stack Rush → Build Web Preview** tarayıcı dosyalarını üretir.

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.3.3f1/Editor/Unity.exe' `
  -batchmode -quit -projectPath 'C:/projects/Color-Stack-Rush' `
  -buildTarget WebGL -executeMethod ReleaseBuilder.BuildWebPreview `
  -outputPath 'C:/builds/ColorStackRush-Web' -logFile 'C:/builds/web.log'
```

Çıktıyı HTTP/HTTPS üzerinden sun; index.html dosyasını çift tıklamak yeterli değildir. Varsayılan derleme Brotli sıkıştırmasını Unity `.unityweb` açma desteğiyle kullanır; Sites statik sunucusu `_headers` kurallarını uygulamadığı için bu ayar gerekir. JavaScript ile açma ilk yüklemeyi biraz uzatabilir ve native WASM streaming kullanmaz. Kendi sunucunda native açma için derleme komutuna `-nativeWebDecompression true` ekle; bu durumda Brotli için `Content-Encoding: br`, WebAssembly için `Content-Type: application/wasm` gerekir. Çıktıdaki `_headers` bu kuralları destekleyen sunucular içindir. Oyun üst seviye sayfada çalışır; iframe kullanılmaz. Telefon çözünürlüğü sınırlanır. Sayfa yükleme ilerlemesi, hata sonrası yeniden deneme ve isteğe bağlı tam ekran içerir.

## Kayıt ve mimari

`LevelCatalog.Get(long)` sonlu bölüm tanımını hesaplar. `RunConfig.Level(long)` koşuyu, `GameManager.StartRun(RunConfig)` yaşam döngüsünü yönetir. Ölüm/bitiş sonucu tek kez kaydedilir; hızlı yeniden başlatma eski animasyonları ve sonuçları iptal eder. Hareket FixedUpdate/Rigidbody.MovePosition kullanır.

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

Testler büyük bölüm kimliklerini, 1.000 bölümde rotaları, deterministik yeniden denemeyi, göç/hata kurtarmayı, tek sonucu, havuz ve dokunma sahipliğini kapsar. PlayMode botu gerçek Input System girdisi ve fizik döngüsünü kullanır; `-snapshotPath` ekran görüntüsü/bölüm CSV'si üretir. Fiziksel FPS ve oyuncu kabulü için [WEB_QA.md](WEB_QA.md) listesini uygula. Hedef FPS veya hızlandırılmış bot koşusu cihaz ölçümü değildir. Reklam, hesap, sunucu, çevrimiçi sıralama ve yeni APK bu teslimde yoktur.
