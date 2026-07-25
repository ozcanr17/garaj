# GARAJ — Simülasyon Çekirdeği

İkinci el araç restorasyon simülasyonunun **oynanabilir prototipi**. Sanat yok, motor yok,
3D yok — sadece oyunun gerçekten riskli olan kısmı: *belirsizlik*.

Blueprint'in Faz 0 sorusunu cevaplamak için yazıldı:
**"Kaputu açmak heyecan verici mi?"** Bu soruya Unity'ye ve Blender'a tek kuruş
harcamadan cevap verilebilir.

## Çalıştırma

```bash
dotnet run --project src/Garaj.Console              # oyna
dotnet run --project src/Garaj.Console -- 12345     # sabit seed ile oyna
dotnet run --project src/Garaj.Console -- --balance 1000 7   # ekonomi denge raporu
```

## Mimarinin tek kuralı

> **Aracın GERÇEĞİ ile oyuncunun İNANDIĞI şey iki ayrı nesnedir.**

- `VehicleInstance` → gerçek, gizli durum
- `PlayerKnowledge` → oyuncunun eksik, hatalı, daraltılabilir inancı

Sunum katmanı (`Ui`, ekranlar) `PartInstance.Condition`'a **asla** erişmez. Sadece
`PlayerKnowledge` okur. Tek istisna oyun sonundaki "Gerçek" ekranıdır.

Bu ayrım mimari bir tercih değil, **oyunun kendisidir**. Bilgi asimetrisi böyle
garanti altına alınır — bir ekranın yanlışlıkla gerçeği sızdırması imkânsız hale gelir.

### Güven aralığı (`ConfidenceRange`)

Hiçbir yerde kesin durum sayısı gösterilmez. Gösterilen şey bir **bant** ve o banda
duyulan **güven**dir. Güven tavanı `0.94` — asla %100 olmaz.

```
Ön Kaput          [░░░░░░░░██████████████] 38-100   İdare eder görünüyor   güven %32
Sol Ön Çamurluk   [░░░░░███████████████░░] 21-89    İdare eder görünüyor   güven %32
Lastikler         [███████░░░░░░░░░░░░░░░] 0-34     Hurda görünüyor        güven %32
```

Yeni kanıt geldiğinde bantlar **kesişir** ve güven artar. Kesişmezlerse bu bir
**çelişkidir**: güven düşer. Dolandırıcılık oyuncuya işte böyle sezdirilir —
iki ölçüm birbirini tutmuyorsa bir şey yanlıştır.

## Katmanlar

| Dosya | Sorumluluk |
|---|---|
| `Model.cs` | Parça kataloğu (33 parça), kusur, araç, evrak — gerçek durum |
| `Knowledge.cs` | `ConfidenceRange`, `Belief.Combine`, `PlayerKnowledge` — inanç |
| `Diagnosis.cs` | 11 teşhis yöntemi + belge çapraz doğrulama |
| `Scams.cs` | 7 maskeleme tipi: neyi gizler, hangi yöntemi körleştirir, hangisi onu yener |
| `Generation.cs` | Katmanlı prosedürel üretim (blueprint §8.1) |
| `Sellers.cs` | 6 arketip, yalanlar, tell'ler, sabır, pazarlık |
| `Economy.cs` | Değerleme, onarım, satış, ekipman |

## Uygulanan çekirdek mekanikler

- **Maskeleme**: her maskeleme belirli yöntemleri körleştirir, belirli bir yöntem onu
  deler, bir ömrü (km) ve olasılıksal bir ipucu vardır. Km ilerledikçe çözülür.
- **Çapraz doğrulama**: servis defteri km'si göstergeden büyükse km oynatılmıştır.
  Ruhsat motor no'su blokla uyuşmuyorsa motor değişmiştir. Tramer temizken boya
  kalın çıkıyorsa kayıtsız kaza vardır — *bu sonuncusu yalnızca oyuncu boya ölçümü
  de yaptıysa görünür* (yöntem kombinasyonu ödülü).
- **Sahip profili**: 8 profil, her biri farklı bir aşınma imzası bırakır. Oyuncuya
  asla söylenmez; kalıptan tahmin edilir.
- **Zaman bombası**: araçların %8'inde satın almadan önce hiçbir yöntemle
  bulunamayan bir kusur vardır. Blueprint'in şartı: yıkıcı değil, sinir bozucu.
- **Tell'ler**: satıcı yalan söylerken ipucu sızdırır — ama dürüst satıcılar da
  %12 ihtimalle gerginlik gösterir. Tell'ler asla kesin değildir.

## Denge (`--balance`)

Denge harness'i binlerce araç üretip ekonomiyi ölçer. Kod yazmadan denge ayarının
en hızlı yolu bu. Mevcut kalibrasyon (3 seed'de kararlı):

| Metrik | Değer | Hedef |
|---|---|---|
| Ortalama gerçek durum | 54 | 50-60 |
| Araç başına kusur | 4,7 | 3-5 |
| Maskeleme taşıyan araç | %29 | — |
| Zaman bombası taşıyan | %6 | %5-8 (blueprint) |
| Seçici onarımda kârlı | %50 | ~%50 |
| Maskelemenin oyuncuya maliyeti | ~7.000₺ | > 0 olmalı |

**Marj medyanı istenen fiyatta ≈ −3.000₺.** Yani aracı liste fiyatına, mükemmel
bilgiyle alsan bile başa baş. Kâr iki yerden gelir: **pazarlık** (%10-25) ve
**doğru teşhis**. Dikkatsiz oyuncu zarar eder. Tasarım hedefi tam olarak buydu.

### Blueprint'te düzeltilen iki tutarsızlık

1. **Araç değerleri vs parça fiyatları.** Blueprint'teki ₺42.000'lik araçlar 2015
   fiyatı; parça fiyatları ise gerçekçi (debriyaj seti ₺3.200). Bu oranla her araç
   ekonomik hurda çıkıyor — tam onarım aracın değerinin 3 katı. Araç değerleri 2026
   gerçekliğine çekildi (Şahin ₺145.000). Başlangıç sermayesi ₺50.000 → ₺250.000.

2. **Yaş = aşınma varsayımı.** 36 yaşındaki aracın parçaları 36 yaşında değildir;
   aşınan parçalar ömrü dolunca değişmiştir. Aşınma toplam km üzerinden değil,
   **parçanın üzerindeki km** üzerinden hesaplanır. Bakım kalitesi, atlanan
   değişimlerin oranıdır. Bu düzeltmeden önce ortalama araç durumu 22/100'dü ve
   1000 araçtan 4'ü kârlıydı.

## Buradan sonrası

Bu katman Unity'ye taşınırken **yeniden yazılmaz** — `Garaj.Core` olduğu gibi
Unity projesine girer, Unity sadece bir render katmanı olur. Konsol arayüzü
atılabilir; simülasyon atılamaz.

Eksikler: boya sistemi, sökme bağımlılık grafiği (veri var, UI yok), modifiye,
piyasa simülasyonu, çalışan sistemi, itibar derinliği, storyteller.
