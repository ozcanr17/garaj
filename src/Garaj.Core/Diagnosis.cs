namespace Garaj.Core;

// ---------------------------------------------------------------------------
// TEŞHİS YÖNTEMLERİ (blueprint §3.1)
// Her yöntem: maliyet (para + zaman + satıcının sabrı) → açığa çıkardığı bilgi
// ---------------------------------------------------------------------------

public sealed record DiagnosisMethod(
    MethodId Id,
    string Name,
    decimal Cost,
    int Minutes,
    float Precision,
    SystemGroup[] Covers,
    int PatienceCost,
    bool RequiresSellerPermission,
    string? RequiredEquipment,
    string FlavorText,
    bool GlancesEverything = false
);

public static class MethodCatalog
{
    public static IReadOnlyList<DiagnosisMethod> All { get; } =
    [
        new(MethodId.Gozle, "Gözle inceleme", 0m, 2, 0.32f,
            [SystemGroup.Kaporta, SystemGroup.IcMekan, SystemGroup.Tekerlek],
            PatienceCost: 1, RequiresSellerPermission: false, RequiredEquipment: null,
            FlavorText: "Aracın etrafında dolaşıp panellere, lastiklere, iç mekana bakıyorsun.",
            GlancesEverything: true),

        new(MethodId.Dokunma, "Dokunma / koklama", 0m, 1, 0.28f,
            [SystemGroup.Motor, SystemGroup.Sogutma],
            1, false, null,
            "Kaputu açıp elini gezdiriyorsun. Yağ, antifriz, yanık kokusu."),

        new(MethodId.Calistir, "Motoru çalıştırma", 0m, 3, 0.42f,
            [SystemGroup.Motor, SystemGroup.Elektrik],
            1, true, null,
            "Kontağı çeviriyorsun. Marş sesi, rölanti, egzoz dumanı, titreşim."),

        new(MethodId.TestSurusuKisa, "Test sürüşü (kısa)", 0m, 10, 0.46f,
            [SystemGroup.Sanziman, SystemGroup.Fren, SystemGroup.Suspansiyon],
            2, true, null,
            "Mahalle turu. Vites geçişleri, fren hissi, direksiyon boşluğu."),

        new(MethodId.TestSurusuUzun, "Test sürüşü (uzun)", 0m, 45, 0.66f,
            [SystemGroup.Motor, SystemGroup.Sanziman, SystemGroup.Sogutma,
             SystemGroup.Fren, SystemGroup.Suspansiyon],
            5, true, null,
            "Araç tamamen ısınıyor. Isınınca ortaya çıkan her şey ortaya çıkıyor."),

        new(MethodId.OBD, "OBD taraması", 0m, 5, 0.56f,
            [SystemGroup.Motor, SystemGroup.Elektrik],
            2, true, "obd",
            "Cihazı sokete takıyorsun. Hata kodları ve readiness monitor'ler."),

        new(MethodId.BoyaKalinlik, "Boya kalınlık ölçümü", 0m, 10, 0.80f,
            [SystemGroup.Kaporta],
            2, true, "boya_olcer",
            "Her panele tek tek dokunduruyorsun. Mikron cinsinden gerçek."),

        new(MethodId.Lift, "Lift / kanal", 500m, 20, 0.70f,
            [SystemGroup.Suspansiyon, SystemGroup.Egzoz, SystemGroup.Fren, SystemGroup.Kaporta],
            3, true, null,
            "Araç havada. Alt takım, şasi, egzoz, kaçak izleri — hepsi görünür."),

        new(MethodId.Kompresyon, "Kompresyon testi", 1500m, 30, 0.88f,
            [SystemGroup.Motor],
            4, true, "kompresyon_seti",
            "Bujiler sökülüyor, her silindir tek tek ölçülüyor. Motor yalan söyleyemez."),

        new(MethodId.Endoskop, "Endoskop", 0m, 15, 0.78f,
            [SystemGroup.Motor],
            3, true, "endoskop",
            "Kamerayı buji deliğinden sokuyorsun. Silindir içi, gözle görülmeyen yer."),

        new(MethodId.Belgeler, "Belgeleri inceleme", 0m, 15, 0f,
            [],
            1, true, null,
            "Ruhsat, servis defteri, tramer kaydı. Yan yana koy ve karşılaştır."),

        // ---------------- ÜST KADEME EKİPMAN ----------------

        new(MethodId.Stetoskop, "Stetoskopla dinleme", 0m, 15, 0.62f,
            [SystemGroup.Motor, SystemGroup.Sanziman, SystemGroup.Tekerlek],
            2, true, "stetoskop",
            "Ucu bloğa dayıyorsun. Gürültü kayboluyor, geriye tek bir ses kalıyor."),

        new(MethodId.LeakDown, "Sızdırmazlık testi", 1_200m, 25, 0.90f,
            [SystemGroup.Motor],
            4, true, "leak_down",
            "Silindire basınçlı hava veriyorsun. Kaçak varsa NEREDEN kaçtığını duyuyorsun."),

        new(MethodId.TermalKamera, "Termal kamera", 0m, 10, 0.72f,
            [SystemGroup.Elektrik, SystemGroup.Sogutma, SystemGroup.Motor],
            2, true, "termal_kamera",
            "Ekranda ısı haritası. Tıkalı petek, ısınan devre, çalışmayan silindir."),

        new(MethodId.ProOBD, "Profesyonel OBD + kodlama", 0m, 20, 0.80f,
            [SystemGroup.Motor, SystemGroup.Elektrik, SystemGroup.Sanziman],
            3, true, "pro_obd",
            "Her modüle tek tek bağlanıyorsun. Modüller birbirini tutmuyorsa ortaya çıkar."),

        // ---------------- DIŞ SERVİS ----------------

        new(MethodId.YagAnalizi, "Yağ analizi (laboratuvar)", 800m, 120, 0.84f,
            [SystemGroup.Motor],
            2, true, null,
            "Numune alıp laboratuvara yolluyorsun. Metal partiküller iç aşınmayı ele verir."),

        new(MethodId.SasiOlcum, "Şasi ölçümü (dış servis)", 3_000m, 240, 0.88f,
            [SystemGroup.Kaporta, SystemGroup.Suspansiyon],
            4, true, null,
            "Araç ölçüm tezgahına çekiliyor. Şasi milimetrik olarak taranıyor."),
    ];

    private static readonly Dictionary<MethodId, DiagnosisMethod> _byId = All.ToDictionary(m => m.Id);
    public static DiagnosisMethod Get(MethodId id) => _byId[id];
}

// ---------------------------------------------------------------------------

public sealed class DiagnosisResult
{
    public List<Observation> Observations { get; } = [];
    public decimal Cost { get; set; }
    public int Minutes { get; set; }
    public bool SellerRefused { get; set; }
    public string? RefusalText { get; set; }
}

public static class DiagnosisEngine
{
    /// <summary>
    /// Bir teşhis yöntemini uygula. Aracın gerçek durumu DEĞİŞMEZ — sadece
    /// oyuncunun bilgisi güncellenir. Maskeleme varsa bilgi YANLIŞ yönde güncellenir.
    /// </summary>
    public static DiagnosisResult Run(
        VehicleInstance v, PlayerKnowledge k, DiagnosisMethod m, Seller? seller, Random rng)
    {
        var result = new DiagnosisResult { Cost = m.Cost, Minutes = m.Minutes };

        // --- Satıcı izin veriyor mu? ---
        if (seller is not null && m.RequiresSellerPermission)
        {
            if (seller.PatienceRemaining < m.PatienceCost)
            {
                result.SellerRefused = true;
                result.RefusalText = seller.RefusalLine(m, rng);
                return result;
            }
            seller.PatienceRemaining -= m.PatienceCost;
        }

        k.MethodsUsed.Add(m.Id);
        k.SpentOnDiagnosis += m.Cost;
        k.MinutesSpent += m.Minutes;

        // --- 1. Parça durumlarını ölç (algılanan değer üzerinden!) ---
        foreach (var part in v.Parts.Values)
        {
            bool covered = m.Covers.Contains(part.Def.Group);
            if (!covered && !m.GlancesEverything) continue;

            float precision = covered ? m.Precision : m.Precision * 0.35f;
            float perceived = ScamEngine.PerceivedCondition(v, part, m.Id);
            k.Update(part.DefId, Belief.Measure(perceived, precision, rng));
        }

        // --- 2. Kusurları açığa çıkar ---
        foreach (var part in v.Parts.Values)
        {
            if (!m.Covers.Contains(part.Def.Group) && !m.GlancesEverything) continue;

            foreach (var defect in part.Defects)
            {
                if (k.DiscoveredDefects.Contains(defect.Id)) continue;
                if (defect.SurfacesAfterKm > 0) continue;             // zaman bombası, henüz yok
                if (!defect.RevealedBy.Contains(m.Id)) continue;
                if (ScamEngine.IsDefectHiddenFrom(v, defect, m.Id)) continue;

                k.DiscoveredDefects.Add(defect.Id);
                result.Observations.Add(new Observation(
                    $"{part.Def.Name}: {defect.Description}",
                    ObservationKind.Finding, m.Id));
            }
        }

        // --- 3. Duyusal detaylar (kusur açığa çıkarmaz, çıkarım hammaddesi verir) ---
        result.Observations.AddRange(InspectionFlavor.For(v, m, rng));

        // --- 4. Maskeleme ipuçları ve ifşalar ---
        foreach (var obs in ScamEngine.RollExposures(v, m.Id)) result.Observations.Add(obs);
        foreach (var obs in ScamEngine.RollTells(v, m.Id, rng)) result.Observations.Add(obs);

        // --- 5. Belgelere göz atma ---
        // DİKKAT: burası çelişkiyi SÖYLEMEZ. Sadece "bir şey tutmuyor" hissi verir.
        // Çelişkiyi oyuncu belge masasında kendi bulmalı (bkz. DocumentDesk).
        // Cevabı burada vermek, oyunun en zeki mekaniğini bir bildirim satırına çevirir.
        if (m.Id == MethodId.Belgeler)
            result.Observations.AddRange(DocumentAnalyzer.Skim(v, rng));

        // --- 6. Gerçekten hiçbir şey yoksa bunu da söyle (yokluk da bilgidir) ---
        if (result.Observations.Count == 0)
            result.Observations.Add(new Observation(
                "Dikkat çeken bir şey görmedin. Bu, bir şey olmadığı anlamına gelmez.",
                ObservationKind.Reassurance, m.Id));

        foreach (var obs in result.Observations) k.Observations.Add(obs);
        return result;
    }
}

// ---------------------------------------------------------------------------
// ÇAPRAZ DOĞRULAMA (blueprint §2.4 — "oyunun en zeki mekaniği olabilir")
//
// Belgeler tek başına yalan söyleyebilir. Ama BİRBİRİYLE çelişemezler.
// Bazı çelişkiler yalnızca oyuncu başka bir teşhis de yaptıysa ortaya çıkar —
// bu, yöntemleri BİRLEŞTİRMEYİ ödüllendirir.
// ---------------------------------------------------------------------------

public static class DocumentAnalyzer
{
    /// <summary>
    /// Belgelere hızlı göz atma. Çelişki VARSA bunu sezdirir ama NE olduğunu
    /// söylemez — oyuncuyu masaya yönlendirir. Yoksa yanlış bir güven verir.
    /// </summary>
    public static List<Observation> Skim(VehicleInstance v, Random rng)
    {
        var found = new List<Observation>();
        var docs = v.Documents;

        int maxServiceKm = docs.ServiceHistory.Count > 0 ? docs.ServiceHistory.Max(s => s.Km) : 0;
        bool kmProblem = maxServiceKm > v.OdometerReading;
        bool engineProblem = !string.IsNullOrEmpty(docs.RuhsatEngineNumber)
                             && docs.RuhsatEngineNumber != v.EngineNumber;
        bool yearProblem = docs.RuhsatModelYear != v.ModelYear;

        int problems = (kmProblem ? 1 : 0) + (engineProblem ? 1 : 0) + (yearProblem ? 1 : 0);

        // Envanter — bunlar bilgi, çelişki değil
        if (docs.ServiceHistory.Count == 0)
            found.Add(new Observation(
                "Servis geçmişi hiç yok. Bu tek başına kanıt değil ama bilinmeyeni büyütüyor.",
                ObservationKind.Detail, MethodId.Belgeler));
        else
            found.Add(new Observation(
                $"Serviste {docs.ServiceHistory.Count} kayıt var, en eskisi {docs.ServiceHistory.Min(s => s.Year)}.",
                ObservationKind.Detail, MethodId.Belgeler));

        found.Add(new Observation(
            docs.TramerRecords.Count == 0
                ? "Tramer kaydı temiz görünüyor."
                : $"Tramerde {docs.TramerRecords.Count} hasar kaydı var.",
            ObservationKind.Detail, MethodId.Belgeler));

        if (docs.OwnerCount >= 6)
            found.Add(new Observation(
                $"{docs.OwnerCount} el değişmiş. Kimse elinde tutmak istememiş.",
                ObservationKind.Detail, MethodId.Belgeler));

        // Sezgi — ne olduğunu SÖYLEMEZ
        if (problems > 0 && Rng.Chance(rng, 0.75f))
        {
            found.Add(new Observation(
                problems > 1
                    ? "Kâğıtlarda bir şeyler oturmuyor. Birden fazla yerde bir tuhaflık var — " +
                      "ama üstünkörü bakışla ne olduğunu çıkaramıyorsun."
                    : "Bir şey gözüne takıldı ama tam yakalayamadın. Belgeleri masaya yayıp " +
                      "yan yana koymak gerek.",
                ObservationKind.Suspicion, MethodId.Belgeler));
        }

        return found;
    }

    /// <summary>
    /// Otomatik çapraz doğrulama. ARTIK OYUNDA KULLANILMIYOR — oyuncu çelişkiyi
    /// belge masasında kendi bulur. Referans ve test amacıyla duruyor.
    /// </summary>
    public static List<Observation> FindContradictions(VehicleInstance v, PlayerKnowledge k)
    {
        var found = new List<Observation>();
        var docs = v.Documents;

        void Flag(string text) => found.Add(new Observation(text, ObservationKind.Contradiction, MethodId.Belgeler));

        // 1. Servis kaydındaki km, gösterge kilometresinden büyük mü?
        var maxServiceKm = docs.ServiceHistory.Count > 0 ? docs.ServiceHistory.Max(s => s.Km) : 0;
        if (maxServiceKm > v.OdometerReading)
        {
            Flag($"ÇELİŞKİ — Servis defterinde {maxServiceKm:N0} km'de bakım kaydı var, " +
                 $"ama gösterge {v.OdometerReading:N0} km gösteriyor. Kilometre geri alınmış.");
        }

        // 2. Ruhsattaki motor numarası, motorun üzerindekiyle uyuşuyor mu?
        if (!string.IsNullOrEmpty(docs.RuhsatEngineNumber) &&
            docs.RuhsatEngineNumber != v.EngineNumber)
        {
            Flag($"ÇELİŞKİ — Ruhsat motor no: {docs.RuhsatEngineNumber}, " +
                 $"blok üzerindeki no: {v.EngineNumber}. Motor değişmiş ve bildirilmemiş.");
        }

        // 3. Model yılı tutarlılığı
        if (docs.RuhsatModelYear != v.ModelYear)
        {
            Flag($"ÇELİŞKİ — Ruhsatta {docs.RuhsatModelYear} model yazıyor, " +
                 $"ilanda {v.ModelYear}. Biri yanlış.");
        }

        // 4. Tramer temiz ama boya ölçümü kalın çıkmışsa → kayıtsız kaza
        //    (yalnızca oyuncu boya ölçümü yaptıysa görülebilir — yöntem kombinasyonu ödülü)
        if (k.MethodsUsed.Contains(MethodId.BoyaKalinlik) && docs.TramerRecords.Count == 0)
        {
            var repainted = PartCatalog.InGroup(SystemGroup.Kaporta)
                .Where(p => k.For(p.Id).Mid < 55f && !k.For(p.Id).IsUnexamined)
                .ToList();

            if (repainted.Count > 0)
            {
                Flag($"ÇELİŞKİ — Tramer kaydı tertemiz, ama {repainted[0].Name} boyalı görünüyor. " +
                     "Sigortaya bildirilmeden onarılmış bir kaza var.");
            }
        }

        // 5. Servis geçmişi hiç yok ama araç çok eski
        if (docs.ServiceHistory.Count == 0)
        {
            Flag("Servis geçmişi hiç yok. Bu tek başına kanıt değil ama bilinmeyeni büyütüyor.");
        }

        // 6. Sahiplik sayısı yüksek
        if (docs.OwnerCount >= 6)
        {
            Flag($"{docs.OwnerCount} el değişmiş. Kimse elinde tutmak istememiş.");
        }

        if (found.Count == 0)
        {
            found.Add(new Observation(
                "Belgeler kendi içinde tutarlı. Bu, temiz oldukları anlamına gelmez — " +
                "sadece yalanları birbirini tutuyor olabilir.",
                ObservationKind.Reassurance, MethodId.Belgeler));
        }

        return found;
    }
}
