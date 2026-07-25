namespace Garaj.Core;

// ---------------------------------------------------------------------------
// TEMEL ENUM'LAR
// ---------------------------------------------------------------------------

public enum SystemGroup
{
    Motor, Sanziman, Sogutma, Egzoz, Fren, Suspansiyon, Elektrik, Kaporta, IcMekan, Tekerlek
}

public enum DefectType
{
    Catlak, Kacak, Asinma, Korozyon, Egilme, ElektrikArizasi, Tikanma, Gurultu, Bosluk
}

public enum PartOrigin { OEM, Aftermarket, Used, Refurbished, Counterfeit }

public enum MethodId
{
    Gozle, Dokunma, Calistir, TestSurusuKisa, TestSurusuUzun,
    OBD, BoyaKalinlik, Lift, Kompresyon, Endoskop, Belgeler
}

public enum OwnerProfile
{
    YasliCift, GencSurucu, Taksi, KiralikFilo, MerakliTamirci, UzunSurePark, SehirIci, UzunYol
}

public enum SellerArchetype { Amator, Galerici, Duygusal, Dolandirici, Aceleci, Koleksiyoncu }

// ---------------------------------------------------------------------------

public static class Cash
{
    /// <summary>
    /// Parayı en yakın adıma yuvarlar (100₺, 1000₺ gibi).
    /// decimal negatif ondalık basamak desteklemediği için gerekli.
    /// </summary>
    public static decimal RoundTo(decimal value, int step)
        => step <= 1
            ? Math.Round(value, 0)
            : Math.Round(value / step, 0, MidpointRounding.AwayFromZero) * step;
}

// ---------------------------------------------------------------------------
// PARÇA TANIMLARI (statik veri — blueprint §2.1)
// ---------------------------------------------------------------------------

public sealed record PartDefinition(
    string Id,
    string Name,
    SystemGroup Group,
    decimal PartCost,
    float LaborHours,
    float WearRate,
    string[] RequiresRemoved
)
{
    /// <summary>Bazı parçalar değiştirilmez, sadece onarılır (tavan, şasi vb.).</summary>
    public bool IsReplaceable => PartCost > 0m;
}

public static class PartCatalog
{
    private static readonly Dictionary<string, PartDefinition> _byId;

    public static IReadOnlyList<PartDefinition> All { get; }

    static PartCatalog()
    {
        PartDefinition P(string id, string name, SystemGroup g, decimal cost,
                         float hours, float wear, params string[] requires)
            => new(id, name, g, cost, hours, wear, requires);

        All =
        [
            // ---- MOTOR ----
            P("motor_blok",     "Motor Bloğu",              SystemGroup.Motor,    18000m, 20f, 0.35f),
            P("silindir_kapak", "Silindir Kapağı",          SystemGroup.Motor,     6500m,  8f, 0.55f),
            P("kapak_conta",    "Silindir Kapak Contası",   SystemGroup.Motor,      850m,  6f, 0.90f, "silindir_kapak"),
            P("triger_kayis",   "Triger Kayışı",            SystemGroup.Motor,      600m,  3f, 1.40f),
            P("devirdaim",      "Devirdaim (Su Pompası)",   SystemGroup.Motor,      900m,  3f, 1.00f, "triger_kayis"),
            P("krank_kecesi",   "Krank Ön Keçesi",          SystemGroup.Motor,      250m,  4f, 1.10f, "triger_kayis"),
            P("enjektor",       "Enjektörler",              SystemGroup.Motor,     2400m,  2f, 0.80f),
            P("bujiler",        "Bujiler",                  SystemGroup.Motor,      320m,  0.5f, 1.60f),
            P("yag_filtresi",   "Yağ Filtresi",             SystemGroup.Motor,      180m,  0.5f, 1.80f),

            // ---- ŞANZIMAN ----
            P("debriyaj",       "Debriyaj Seti",            SystemGroup.Sanziman,  3200m,  6f, 1.20f),
            P("sanziman",       "Şanzıman",                 SystemGroup.Sanziman, 12000m, 10f, 0.45f),

            // ---- SOĞUTMA ----
            P("radyator",       "Radyatör",                 SystemGroup.Sogutma,   2200m,  2f, 0.95f),
            P("termostat",      "Termostat",                SystemGroup.Sogutma,    350m,  1.5f, 1.10f),
            P("su_hortumlari",  "Su Hortumları",            SystemGroup.Sogutma,    400m,  1.5f, 1.30f),

            // ---- EGZOZ ----
            P("egzoz_orta",     "Orta Susturucu",           SystemGroup.Egzoz,     1100m,  1.5f, 1.25f),

            // ---- FREN ----
            P("on_balata",      "Ön Balatalar",             SystemGroup.Fren,       700m,  1f, 1.90f),
            P("on_disk",        "Ön Diskler",               SystemGroup.Fren,      1400m,  1.5f, 1.10f),
            P("ana_merkez",     "Fren Ana Merkezi",         SystemGroup.Fren,      1800m,  3f, 0.70f),

            // ---- SÜSPANSİYON ----
            P("on_amortisor",   "Ön Amortisörler",          SystemGroup.Suspansiyon, 2600m, 3f, 1.30f),
            P("rotil",          "Rotiller",                 SystemGroup.Suspansiyon,  900m, 2f, 1.45f),
            P("rot_basi",       "Rot Başları",              SystemGroup.Suspansiyon,  600m, 1.5f, 1.40f),

            // ---- ELEKTRİK ----
            P("aku",            "Akü",                      SystemGroup.Elektrik,  2800m, 0.3f, 1.70f),
            P("alternator",     "Alternatör",               SystemGroup.Elektrik,  3400m,  2f, 0.85f),
            P("mars_motoru",    "Marş Motoru",              SystemGroup.Elektrik,  2900m,  2f, 0.80f),
            P("kablo_demeti",   "Kablo Demeti",             SystemGroup.Elektrik,  4500m, 12f, 0.50f),

            // ---- KAPORTA ----
            P("on_kaput",       "Ön Kaput",                 SystemGroup.Kaporta,   3200m,  3f, 0.60f),
            P("sol_on_camurluk","Sol Ön Çamurluk",          SystemGroup.Kaporta,   1900m,  4f, 0.65f),
            P("sag_on_kapi",    "Sağ Ön Kapı",              SystemGroup.Kaporta,   3800m,  4f, 0.55f),
            P("tavan",          "Tavan",                    SystemGroup.Kaporta,      0m,  8f, 0.40f),
            P("arka_tampon",    "Arka Tampon",              SystemGroup.Kaporta,   1500m,  2f, 0.75f),

            // ---- İÇ MEKAN ----
            P("on_koltuklar",   "Ön Koltuklar",             SystemGroup.IcMekan,   3500m,  2f, 1.15f),
            P("torpido",        "Torpido",                  SystemGroup.IcMekan,   2600m,  5f, 0.60f),

            // ---- TEKERLEK ----
            P("lastikler",      "Lastikler",                SystemGroup.Tekerlek,  4800m,  1f, 1.50f),
        ];

        _byId = All.ToDictionary(p => p.Id);
    }

    public static PartDefinition Get(string id) => _byId[id];

    public static IEnumerable<PartDefinition> InGroup(SystemGroup g) => All.Where(p => p.Group == g);

    public static string GroupName(SystemGroup g) => g switch
    {
        SystemGroup.Motor       => "Motor",
        SystemGroup.Sanziman    => "Şanzıman",
        SystemGroup.Sogutma     => "Soğutma",
        SystemGroup.Egzoz       => "Egzoz",
        SystemGroup.Fren        => "Fren",
        SystemGroup.Suspansiyon => "Süspansiyon",
        SystemGroup.Elektrik    => "Elektrik",
        SystemGroup.Kaporta     => "Kaporta",
        SystemGroup.IcMekan     => "İç Mekan",
        SystemGroup.Tekerlek    => "Tekerlek",
        _ => g.ToString()
    };
}

// ---------------------------------------------------------------------------
// KUSUR
// ---------------------------------------------------------------------------

public sealed class Defect
{
    public required string Id { get; init; }
    public required string PartId { get; init; }
    public DefectType Type { get; init; }

    /// <summary>0-1. Onarım maliyetini ve arıza riskini belirler.</summary>
    public float Severity { get; init; }

    /// <summary>Oyuncuya gösterilecek metin — ancak keşfedildikten SONRA.</summary>
    public required string Description { get; init; }

    /// <summary>Bu kusuru açığa çıkarabilen teşhis yöntemleri.</summary>
    public MethodId[] RevealedBy { get; init; } = [];

    /// <summary>Ek onarım maliyeti (parça fiyatının üstüne).</summary>
    public decimal ExtraRepairCost { get; init; }

    /// <summary>
    /// Satın alma sonrası ortaya çıkana kadar gizli kalan "zaman bombası".
    /// 0 olduğunda kusur fiziksel iz bırakmıştır ve teşhisle bulunabilir.
    /// </summary>
    public int SurfacesAfterKm { get; set; }
}

// ---------------------------------------------------------------------------
// PARÇA ÖRNEĞİ (gerçek, gizli durum)
// ---------------------------------------------------------------------------

public sealed class PartInstance
{
    public required string DefId { get; init; }

    /// <summary>0-100. GİZLİ. UI katmanı bunu asla doğrudan okumaz.</summary>
    public float Condition { get; set; }

    public PartOrigin Origin { get; set; } = PartOrigin.OEM;
    public List<Defect> Defects { get; } = [];
    public bool IsSeized { get; set; }
    public int InstalledAtKm { get; set; }

    public PartDefinition Def => PartCatalog.Get(DefId);

    /// <summary>
    /// Bu parçayı sağlıklı hale getirmenin gerçek maliyeti.
    /// Kademeler Valuation.EstimateFor ile AYNI olmalı — yoksa oyuncunun tahmini
    /// sistematik olarak yanlı olur ve belirsizlik değil, sadece hata üretiriz.
    /// </summary>
    public decimal TrueRepairCost()
    {
        if (Condition >= 70f && Defects.Count == 0) return 0m;
        var d = Def;
        decimal cost = d.IsReplaceable ? d.PartCost : d.PartCost + 1500m;

        decimal partShare = Condition switch
        {
            >= 70f => 0m,          // sağlam; sadece varsa kusur bedeli ödenir
            >= 55f => cost * 0.20m,
            >= 30f => cost * 0.45m,
            _      => cost          // bitmiş, değişecek
        };

        decimal extras = Defects.Sum(x => x.ExtraRepairCost);
        return Math.Round(partShare + extras, 0);
    }
}

// ---------------------------------------------------------------------------
// EVRAK (blueprint §2.4)
// ---------------------------------------------------------------------------

public sealed record ServiceRecord(int Year, int Km, string Work);
public sealed record TramerEntry(int Year, decimal Amount, string Panel);

public sealed class DocumentPackage
{
    public int RuhsatModelYear { get; set; }
    public string RuhsatEngineNumber { get; set; } = "";
    public int OwnerCount { get; set; }
    public List<ServiceRecord> ServiceHistory { get; } = [];
    public List<TramerEntry> TramerRecords { get; } = [];
    public bool HasInspectionReport { get; set; }
    public int InspectionYear { get; set; }
}

// ---------------------------------------------------------------------------
// ARAÇ ÖRNEĞİ
// ---------------------------------------------------------------------------

public sealed class VehicleInstance
{
    public required string InstanceId { get; init; }
    public string ModelName { get; set; } = "Tofaş Şahin S";
    public string Trim { get; set; } = "1.6 ie";
    public int ModelYear { get; set; }
    public string Vin { get; set; } = "";
    public string EngineNumber { get; set; } = "";
    public string Plate { get; set; } = "";
    public string City { get; set; } = "";

    /// <summary>Gösterge panelindeki km — oynatılmış olabilir.</summary>
    public int OdometerReading { get; set; }

    /// <summary>GERÇEK km. GİZLİ.</summary>
    public int TrueOdometer { get; set; }

    public Dictionary<string, PartInstance> Parts { get; } = [];

    /// <summary>Önceki sahip profili. GİZLİ — oyuncu aşınma kalıbından tahmin eder.</summary>
    public OwnerProfile Owner { get; set; }

    public List<ActiveTamper> Tampers { get; } = [];
    public DocumentPackage Documents { get; set; } = new();

    public decimal AskingPrice { get; set; }
    public string ListingText { get; set; } = "";

    /// <summary>Modelin temel piyasa değeri. Herkese açık bilgi (piyasa bilinir).</summary>
    public decimal ModelBaseValue { get; set; }

    /// <summary>Satın alındıktan sonra kat edilen km (tamper çözülmesi için).</summary>
    public int KmSincePurchase { get; set; }

    public IEnumerable<Defect> AllDefects => Parts.Values.SelectMany(p => p.Defects);

    public PartInstance Part(string id) => Parts[id];

    public string DisplayName => $"{ModelName} ({ModelYear})";
}
