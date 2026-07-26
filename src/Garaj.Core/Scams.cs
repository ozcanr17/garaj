namespace Garaj.Core;

// ---------------------------------------------------------------------------
// MASKELEME SİSTEMİ (blueprint §2.3)
//
// Bir maskeleme üç şeyi tanımlar:
//   1. Neyi gizler (hangi kusur tipini, hangi sistemde)
//   2. Hangi yöntemleri KÖRLEŞTİRİR (o yöntem gerçeği göremez)
//   3. Hangi yöntem onu YENER (maskeyi delip geçer)
//
// Ayrıca her maskeleme olasılıksal bir İPUCU sızdırır. Blueprint'in kuralı:
// "Tespit edilemeyen dolandırıcılık %100 gizli değil, olasılıksal ipucuna sahip."
// ---------------------------------------------------------------------------

public sealed record TamperDefinition(
    string Id,
    string Name,
    DefectType[] Hides,
    SystemGroup[] AffectsGroups,
    MethodId[] BlindsMethods,
    MethodId[] DefeatedBy,
    int LifespanKmMin,
    int LifespanKmMax,
    string TellText,
    float TellChance,
    MethodId[] TellSurfacesIn,
    float ConditionInflation,
    string SurfacingText
)
{
    public const int Permanent = int.MaxValue;
    public bool IsPermanent => LifespanKmMax >= Permanent;
}

public static class TamperCatalog
{
    public static IReadOnlyList<TamperDefinition> All { get; } =
    [
        new("motor_yikama", "Motor yıkama",
            Hides: [DefectType.Kacak],
            AffectsGroups: [SystemGroup.Motor],
            BlindsMethods: [MethodId.Gozle, MethodId.Dokunma],
            DefeatedBy: [MethodId.Lift, MethodId.TestSurusuUzun],
            LifespanKmMin: 200, LifespanKmMax: 800,
            TellText: "Motor bölmesi bu yaştaki bir araca göre şüpheli derecede temiz.",
            TellChance: 0.60f,
            TellSurfacesIn: [MethodId.Gozle],
            ConditionInflation: 22f,
            SurfacingText: "Motorun altında yağ birikmeye başladı — yıkanmış kaçak geri geldi."),

        new("kalin_yag", "Kalın yağ / katkı",
            Hides: [DefectType.Asinma, DefectType.Gurultu],
            AffectsGroups: [SystemGroup.Motor],
            BlindsMethods: [MethodId.Calistir, MethodId.TestSurusuKisa],
            DefeatedBy: [MethodId.Kompresyon, MethodId.TestSurusuUzun, MethodId.Endoskop, MethodId.LeakDown, MethodId.YagAnalizi],
            LifespanKmMin: 300, LifespanKmMax: 1500,
            TellText: "Yağ çubuğundaki yağ olması gerekenden koyu ve ağdalı.",
            TellChance: 0.45f,
            TellSurfacesIn: [MethodId.Gozle, MethodId.Dokunma],
            ConditionInflation: 25f,
            SurfacingText: "Motordan soğukken belirgin bir tıkırtı gelmeye başladı."),

        new("hata_kodu_silme", "Hata kodu silme",
            Hides: [DefectType.ElektrikArizasi, DefectType.Tikanma],
            AffectsGroups: [SystemGroup.Elektrik, SystemGroup.Motor],
            BlindsMethods: [MethodId.Gozle, MethodId.Calistir, MethodId.TestSurusuKisa],
            DefeatedBy: [MethodId.OBD],
            LifespanKmMin: 20, LifespanKmMax: 100,
            TellText: "Kontak açıldığında arıza lambası hiç yanıp sönmüyor — ampul sökülmüş olabilir.",
            TellChance: 0.35f,
            TellSurfacesIn: [MethodId.Calistir],
            ConditionInflation: 18f,
            SurfacingText: "Arıza lambası yandı — silinen hata kodu geri geldi."),

        new("macun_dolgu", "Macun / dolgu ile pas örtme",
            Hides: [DefectType.Korozyon, DefectType.Egilme],
            AffectsGroups: [SystemGroup.Kaporta],
            BlindsMethods: [MethodId.Gozle, MethodId.Dokunma],
            DefeatedBy: [MethodId.BoyaKalinlik, MethodId.Lift, MethodId.SasiOlcum],
            LifespanKmMin: TamperDefinition.Permanent, LifespanKmMax: TamperDefinition.Permanent,
            TellText: "Panel aralıkları simetrik değil — bir yerde iş görmüş olabilir.",
            TellChance: 0.40f,
            TellSurfacesIn: [MethodId.Gozle],
            ConditionInflation: 30f,
            SurfacingText: "Macunun altındaki pas kabarmaya başladı."),

        new("km_oynatma", "Kilometre oynatma",
            Hides: [DefectType.Asinma],
            AffectsGroups: [SystemGroup.Sanziman, SystemGroup.IcMekan, SystemGroup.Suspansiyon],
            BlindsMethods: [MethodId.Gozle, MethodId.Calistir, MethodId.TestSurusuKisa],
            // Belgeler bilerek YOK: km oynatmayı belgelerden yakalamak oyuncunun
            // masada kendi kuracağı bir iddia olmalı, otomatik bir bildirim değil.
            DefeatedBy: [MethodId.ProOBD],
            LifespanKmMin: TamperDefinition.Permanent, LifespanKmMax: TamperDefinition.Permanent,
            TellText: "Pedal lastikleri ve direksiyon derisi, gösterilen kilometreye göre fazla yıpranmış.",
            TellChance: 0.50f,
            TellSurfacesIn: [MethodId.Gozle, MethodId.TestSurusuKisa],
            ConditionInflation: 20f,
            SurfacingText: "Aşınma beklenenden çok daha hızlı ilerliyor."),

        new("radyator_katki", "Radyatöre katkı maddesi",
            Hides: [DefectType.Kacak],
            AffectsGroups: [SystemGroup.Sogutma],
            BlindsMethods: [MethodId.Gozle, MethodId.Calistir, MethodId.TestSurusuKisa],
            DefeatedBy: [MethodId.Lift, MethodId.TestSurusuUzun, MethodId.TermalKamera],
            LifespanKmMin: 50, LifespanKmMax: 300,
            TellText: "Radyatör kapağının altında tuhaf, bulanık bir tortu var.",
            TellChance: 0.55f,
            TellSurfacesIn: [MethodId.Gozle],
            ConditionInflation: 28f,
            SurfacingText: "Hararet göstergesi tırmanıyor — su kaçağı geri döndü."),

        new("sanziman_katki", "Şanzımana kalın yağ",
            Hides: [DefectType.Asinma, DefectType.Bosluk],
            AffectsGroups: [SystemGroup.Sanziman],
            BlindsMethods: [MethodId.Calistir, MethodId.TestSurusuKisa],
            DefeatedBy: [MethodId.TestSurusuUzun],
            LifespanKmMin: 200, LifespanKmMax: 1000,
            TellText: "Vites geçişleri ilk birkaç dakika fazla yumuşak, sanki fazla yağlı.",
            TellChance: 0.30f,
            TellSurfacesIn: [MethodId.TestSurusuKisa],
            ConditionInflation: 24f,
            SurfacingText: "Şanzıman ısındıkça vites atlamaya başladı."),
    ];

    private static readonly Dictionary<string, TamperDefinition> _byId = All.ToDictionary(t => t.Id);
    public static TamperDefinition Get(string id) => _byId[id];
}

/// <summary>Bir araçta aktif olan maskeleme.</summary>
public sealed class ActiveTamper
{
    public required string TamperId { get; init; }
    public int KmRemaining { get; set; }
    public bool Surfaced { get; set; }
    public TamperDefinition Def => TamperCatalog.Get(TamperId);
    public bool IsActive => !Surfaced && KmRemaining > 0;
}

// ---------------------------------------------------------------------------
// SCAM ENGINE
// ---------------------------------------------------------------------------

public static class ScamEngine
{
    /// <summary>
    /// Bu yöntemle bakıldığında parça KAÇ görünüyor? Maskeleme varsa gerçekten
    /// daha iyi görünür — oyuncu yanlış banda inanır ve parasını ona göre verir.
    /// </summary>
    public static float PerceivedCondition(VehicleInstance v, PartInstance part, MethodId method)
    {
        float perceived = part.Condition;

        foreach (var t in v.Tampers.Where(t => t.IsActive))
        {
            var d = t.Def;
            if (!d.AffectsGroups.Contains(part.Def.Group)) continue;
            if (d.DefeatedBy.Contains(method)) continue;      // bu yöntem maskeyi deler
            if (!d.BlindsMethods.Contains(method)) continue;  // bu yöntemi körleştirmiyor

            perceived += d.ConditionInflation;
        }

        return Math.Clamp(perceived, 0f, 100f);
    }

    /// <summary>Bu kusur, bu yöntemden gizleniyor mu?</summary>
    public static bool IsDefectHiddenFrom(VehicleInstance v, Defect defect, MethodId method)
    {
        var part = v.Part(defect.PartId);

        foreach (var t in v.Tampers.Where(t => t.IsActive))
        {
            var d = t.Def;
            if (!d.AffectsGroups.Contains(part.Def.Group)) continue;
            if (!d.Hides.Contains(defect.Type)) continue;
            if (d.DefeatedBy.Contains(method)) return false;  // maske delindi
            if (d.BlindsMethods.Contains(method)) return true;
        }

        return false;
    }

    /// <summary>Bu yöntemi kullanan oyuncuya sızan maskeleme ipuçları.</summary>
    public static IEnumerable<Observation> RollTells(VehicleInstance v, MethodId method, Random rng)
    {
        foreach (var t in v.Tampers.Where(t => t.IsActive))
        {
            var d = t.Def;
            if (!d.TellSurfacesIn.Contains(method)) continue;
            if (!Rng.Chance(rng, d.TellChance)) continue;
            yield return new Observation(d.TellText, ObservationKind.Suspicion, method);
        }
    }

    /// <summary>Maskeyi delen yöntem kullanıldığında oyuncu maskelemeyi YAKALAR.</summary>
    public static IEnumerable<Observation> RollExposures(VehicleInstance v, MethodId method)
    {
        foreach (var t in v.Tampers.Where(t => t.IsActive))
        {
            var d = t.Def;
            if (!d.DefeatedBy.Contains(method)) continue;
            yield return new Observation(
                $"YAKALANDI — {d.Name}: bu araçta bu iş yapılmış.",
                ObservationKind.Finding, method);
        }
    }

    /// <summary>
    /// Km ilerledikçe maskeler çözülür. Satın alma sonrası "mide bulantısı" anı.
    /// </summary>
    public static List<string> AdvanceKm(VehicleInstance v, int km)
    {
        var surfaced = new List<string>();
        v.KmSincePurchase += km;

        // 1. Maskeler çözülür
        foreach (var t in v.Tampers.Where(t => t.IsActive && !t.Def.IsPermanent))
        {
            t.KmRemaining -= km;
            if (t.KmRemaining > 0) continue;

            t.Surfaced = true;
            surfaced.Add(t.Def.SurfacingText);
        }

        // 2. Zaman bombaları patlar (§4.4 Seviye 4 — satın almadan önce bulunamayan kusurlar)
        foreach (var part in v.Parts.Values)
        {
            foreach (var d in part.Defects.Where(d => d.SurfacesAfterKm > 0))
            {
                if (v.KmSincePurchase < d.SurfacesAfterKm) continue;

                d.SurfacesAfterKm = 0;                       // artık fiziksel iz var
                part.Condition = Math.Max(5f, part.Condition - d.Severity * 30f);
                surfaced.Add($"{part.Def.Name}: {d.Description} " +
                             "Satın alırken hiçbir yöntemle bulunamazdı.");
            }
        }

        return surfaced;
    }
}
