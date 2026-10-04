# Tarayıcı ve oyuncu kabulü

Her sonuçta commit, yayın sürümü, tarayıcı, cihaz ve kalite ayarını kaydet. Yapılmamış ölçümler bekliyor olarak kalır; mobil ekran simülasyonu fiziksel telefon testi değildir.

Chrome/Edge/Firefox masaüstü, Android Chrome ve iOS Safari için ayrı ayrı kontrol et:

- HTTPS linki hesap girişi istemeden açılıyor; yükleme yüzdesi/hata/yeniden deneme çalışıyor.
- Fare/A-D veya tek parmak sürükleme çalışıyor; ikinci parmak ve UI'da başlayan hareket engelleniyor.
- Sekme değişiminde duraklatma, dönüşte sıçramasız devam ve Oyna sonrası ses.
- Sonuçtan sonra sayfa yenileme açık bölümü/parayı koruyor; başarısız kayıt değişiklikleri geri alıyor.
- Dar/uzun/yatay ekranlarda HUD kesilmiyor; 18→19→20 ve eski bölüme dönüş çalışıyor.
- 30 dakika bölüm döngüsünde çökme ve kalıcı bellek artışı bulunmuyor.

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
