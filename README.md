# ERPES Operasyon Kulesi — Kurumsal Kaynak, Filo ve Tedarik Zinciri Yönetim Merkezi

**ERPES Operasyon Kulesi**, çoklu üretim tesislerinde (**Kocaeli — Tesis A**, **Bursa — Tesis B**, **İzmir — Tesis C**) kapasite dengeleme, kritik hammadde/yarı mamul emniyet stoğu izleme, **Kara-Hava-Deniz taşıma filosu takibi** ve **fabrikalar arası dinamik iş gücü görevlendirmesi** sağlayan gerçek zamanlı bir operasyon yönetim platformudur.

---

## Temel Özellikler

- **Canlı Fabrika, Filo ve Sevkiyat Haritası (Leaflet.js & ArcGIS Dark)**:
  - Kocaeli, Bursa ve İzmir fabrikalarının anlık üretim yükü ve personel kadrolarının coğrafi takibi.
  - **Kara (🚚)**, **Hava (✈️)** ve **Deniz (🚢)** taşıma araçlarının anlık konumları, yük manifestoları ve tahmini varış süreleri (ETA).
  - Kriz onaylandığında kaynak fabrikadan hedef fabrikaya (**İzmir ➔ Kocaeli O-5 Koridoru**, **Bursa ➔ Kocaeli Osmangazi Koridoru**, **İzmir ➔ Bursa**) gerçek karayolu güzergahı üzerinden canlı araç animasyonu.
- **Fabrika Bazlı Kaynak ve İş Gücü Yönetimi**:
  - Her fabrikanın altında o fabrikaya kaynak taşıyan kara, hava ve deniz araçlarının listesi ve tıklanabilir **Taşıma Aracı Detay Kartı**.
  - Her fabrikanın kendi **İşçi Listesi**, uzmanlık alanları, yetkinlik puanları (`SkillScore`), sözleşme durumu (*Rotasyona Uygun* / *Sabit Kadro*) ve tek tıkla başka fabrikaya görevlendirme / geri döndürme iş akışı.
- **Çeşitlendirilmiş ve Rastgeleleştirilmiş Kriz Simülasyonları**:
  - Deniz lojistik grevleri ve boğaz/gümrük gecikmeleri.
  - Hava kargo rötarları ve slot kısıtlamaları.
  - Farklı fabrikalarda CNC, Robotik Kaynak, Kalite Güvence ve PLC/Otomasyon iş gücü darboğazları.

---

## Kurulum ve Çalıştırma

### 1. Yapılandırma ve Güvenlik
Hassas API anahtarları ve veritabanı bağlantı bilgileri Git geçmişine dahil edilmez (`appsettings.Development.json` `.gitignore` içerisindedir).
- Örnek yapılandırma dosyasını kopyalayarak kendi yerel ayarlarınızı oluşturabilirsiniz:
  ```bash
  cp appsettings.example.json appsettings.Development.json
  ```
- Herhangi bir dış veritabanı ayarı girilmediğinde sistem otomatik olarak yerel veritabanı (`SQLite`) ile sıfır kurulum (zero-config) modunda çalışır ve 3 fabrikayı, stokları ve 35 uzman personeli otomatik oluşturur.

### 2. Uygulamayı Başlatma
```bash
dotnet build
dotnet run --launch-profile http
```
Tarayıcınızda **`http://localhost:5200`** adresini açarak Operasyon Kulesi arayüzüne erişebilirsiniz.
