namespace Garaj.Core;

// ---------------------------------------------------------------------------
// EKİPMAN (blueprint §3.2 — kalıcı yatırım = ilerleme hissi)
// ---------------------------------------------------------------------------

public sealed record Equipment(string Id, string Name, decimal Cost, int Tier, string Unlocks);

public static class EquipmentCatalog
{
    // Blueprint §3.2 kademelerini takip eder. Ekipman kalıcı yatırımdır ve
    // ilerleme hissinin ana kaynağıdır: her alet yeni bir görme biçimi açar.
    public static IReadOnlyList<Equipment> All { get; } =
    [
        new("tork_anahtari",   "Tork Anahtarı",          1_500m, 1,
            "Montajda tork riskini kaldırır — cıvatalar hep ayarında"),
        new("stetoskop",       "Mekanik Stetoskop",      1_200m, 1,
            "Sesi kaynağında dinle — rulman, triger, supap"),
        new("obd",             "OBD Cihazı",             2_500m, 1,
            "Hata kodları + readiness monitor"),
        new("boya_olcer",      "Boya Kalınlık Ölçer",    8_000m, 2,
            "Boyalı/değişen panel tespiti (mikron)"),
        new("endoskop",        "Endoskop Kamera",       12_000m, 3,
            "Silindir içi, ulaşılamayan bölgeler"),
        new("kompresyon_seti", "Kompresyon Seti",       18_000m, 3,
            "Silindir sağlığı — motor yalan söyleyemez"),
        new("leak_down",       "Sızdırmazlık Seti",     22_000m, 4,
            "Kaçağın NEREDEN olduğunu ayırt eder: conta mı, segman mı, supap mı"),
        new("termal_kamera",   "Termal Kamera",         25_000m, 4,
            "Isı haritası — tıkalı radyatör, ısınan devre, soğuk silindir"),
        new("lift",            "Lift",                  45_000m, 5,
            "Alt takım incelemesi artık ücretsiz (aksi halde her seferinde ₺500)"),
        new("pro_obd",         "Profesyonel OBD",       35_000m, 5,
            "Modül-modül karşılaştırma — ECU'ya yazılmış sahte km buradan çıkar"),
    ];

    public static Equipment Get(string id) => All.First(e => e.Id == id);
}

// ---------------------------------------------------------------------------
// DEĞERLEME
//
// İki değer vardır: GERÇEK değer ve oyuncunun TAHMİN ettiği değer.
// Oyunun tamamı bu ikisinin arasındaki boşlukta oynanır.
// ---------------------------------------------------------------------------

public static class Valuation
{
    /// <summary>Aracın gerçek piyasa değeri — sadece satış anında hesaplanır.</summary>
    public static decimal TrueMarketValue(VehicleInstance v)
    {
        // KRİTİK: satış değeri, oyuncuya GÖSTERİLEN piyasa bandından TÜRETİLİR.
        // Ayrı bir formülle hesaplanırsa ikisi kaçınılmaz olarak birbirinden ayrışır.
        // (Bu tam olarak bir kez oldu: TrueMarketValue'da yaş çarpanı yoktu ve araçlar
        // bandın 3-4 katına satılıyordu. İki formül = iki gerçek = hata.)
        var (lo, hi) = RestoredValueBand(v);
        decimal restoredMid = (lo + hi) / 2m;

        // Bandın ortası "iyi durumda" (≈90 puan) bir aracı temsil eder.
        // Gerçek ortalama durum bundan saparsa değer orantılı olarak kayar.
        float avgCondition = v.Parts.Values.Average(p => p.Condition);
        decimal conditionScale = (decimal)Math.Clamp(
            0.32f + avgCondition / 100f * 0.76f, 0.25f, 1.10f);

        decimal value = restoredMid * conditionScale;

        // Kusurlar değeri düşürür — alıcının ekspertizi bunları bulur
        foreach (var d in v.AllDefects)
            value -= d.ExtraRepairCost * 0.6m;

        return Math.Max(5_000m, Cash.RoundTo(value, 100));
    }

    /// <summary>
    /// Onarılmış hâlde bu aracın piyasa bandı. Bu bilgi HERKESE AÇIKTIR —
    /// oyuncu piyasayı bilir, bilmediği şey elindeki aracın gerçek durumudur.
    /// </summary>
    public static (decimal Low, decimal High) RestoredValueBand(VehicleInstance v)
    {
        decimal b = BaseValueFor(v);
        decimal kmFactor = (decimal)Math.Clamp(1.15f - v.OdometerReading / 600_000f, 0.60f, 1.15f);
        decimal ageFactor = (decimal)Math.Clamp(
            1.0f - (VehicleGenerator.CurrentYear - v.ModelYear) * 0.012f, 0.55f, 1.0f);

        decimal mid = b * kmFactor * ageFactor;
        return (Cash.RoundTo(mid * 0.88m, 100), Cash.RoundTo(mid * 1.18m, 100));
    }

    private static decimal BaseValueFor(VehicleInstance v)
        => v.ModelBaseValue > 0m ? v.ModelBaseValue : 145_000m;

    /// <summary>
    /// Oyuncunun BİLDİĞİ kadarıyla tahmini onarım faturası.
    /// Bilmediği her şey bu tahminin dışındadır — sürpriz tam olarak burada saklanır.
    /// </summary>
    public static (decimal Low, decimal High, float Confidence) EstimatedRepairBill(
        VehicleInstance v, PlayerKnowledge k)
    {
        decimal low = 0m, high = 0m;
        float totalConf = 0f;
        int counted = 0;

        foreach (var part in v.Parts.Values)
        {
            var belief = k.For(part.DefId);
            var def = part.Def;
            decimal replacement = def.IsReplaceable ? def.PartCost : def.PartCost + 1_500m;

            if (belief.IsUnexamined)
            {
                // İncelenmemiş parça: geniş belirsizlik. Tahmine dahil edilir ama bant açılır.
                high += replacement * 0.55m;
                continue;
            }

            counted++;
            totalConf += belief.Confidence;

            // Bandın kötü ucu → yüksek tahmin, iyi ucu → düşük tahmin
            low  += EstimateFor(belief.Max, replacement);
            high += EstimateFor(belief.Min, replacement);
        }

        // Keşfedilen kusurların ek maliyetleri kesin olarak bilinir
        foreach (var d in v.AllDefects.Where(d => k.DiscoveredDefects.Contains(d.Id)))
        {
            low += d.ExtraRepairCost * 0.85m;
            high += d.ExtraRepairCost * 1.15m;
        }

        float conf = counted > 0 ? totalConf / counted : 0.05f;
        return (Cash.RoundTo(low, 100), Cash.RoundTo(high, 100), conf);
    }

    /// <summary>PartInstance.TrueRepairCost ile aynı kademeler — bkz. oradaki not.</summary>
    private static decimal EstimateFor(float condition, decimal replacement) => condition switch
    {
        >= 70f => 0m,
        >= 55f => replacement * 0.20m,
        >= 30f => replacement * 0.45m,
        _      => replacement
    };

    /// <summary>Gerçek onarım faturası. Oyuncu bunu ancak işe başlayınca öğrenir.</summary>
    public static decimal TrueRepairBill(VehicleInstance v)
        => v.Parts.Values.Sum(p => p.TrueRepairCost());
}

// ---------------------------------------------------------------------------
// ONARIM
// ---------------------------------------------------------------------------

public static class RepairEngine
{
    /// <summary>
    /// Bir parçanın durumunu onarır — parça/onarım bedeli döner.
    /// Söküm ve cıvata mekaniği ARTIK burada değil (bkz. Disassembly); bu metot
    /// sadece parça hedefe ulaşıldıktan sonra çağrılır ve durumu yeniler.
    /// </summary>
    public static (decimal Cost, float Hours, string Message) Repair(
        VehicleInstance v, string partId, Random rng)
    {
        var part = v.Part(partId);
        var def = part.Def;

        decimal cost = part.TrueRepairCost();
        float hours = def.LaborHours;

        part.Condition = def.IsReplaceable ? Rng.Range(rng, 88f, 97f) : Rng.Range(rng, 72f, 88f);
        part.Defects.Clear();
        part.IsSeized = false;
        part.InstalledAtKm = v.TrueOdometer;
        part.Origin = PartOrigin.Aftermarket;

        return (Math.Round(cost, 0), hours,
            def.IsReplaceable ? $"{def.Name} yenisiyle değiştirildi." : $"{def.Name} elden geçirildi.");
    }
}

// ---------------------------------------------------------------------------
// SATIŞ — alıcı da ekspertiz yapar (blueprint §4.4)
// ---------------------------------------------------------------------------

public static class SaleEngine
{
    public sealed record SaleResult(
        decimal Offer, List<string> BuyerFindings, decimal Deduction, bool Sold);

    public static SaleResult Evaluate(VehicleInstance v, decimal askingPrice, Random rng)
    {
        decimal market = Valuation.TrueMarketValue(v);
        var findings = new List<string>();
        decimal deduction = 0m;

        // Alıcının ekspertiz becerisi
        float buyerSkill = Rng.Range(rng, 0.35f, 0.85f);

        foreach (var d in v.AllDefects)
        {
            if (d.SurfacesAfterKm > 0) continue;          // henüz fiziksel iz yok
            if (!Rng.Chance(rng, buyerSkill)) continue;   // alıcı her şeyi bulamaz

            findings.Add($"\"{v.Part(d.PartId).Def.Name} — {d.Description}\"");
            deduction += d.ExtraRepairCost * 0.8m;
        }

        // Maskeleme hâlâ aktifse alıcı da kanabilir (ironi)
        foreach (var t in v.Tampers.Where(t => t.IsActive))
        {
            if (Rng.Chance(rng, buyerSkill * 0.5f))
                findings.Add($"\"Bu araca {t.Def.Name.ToLowerInvariant()} yapılmış, gördüm.\"");
        }

        decimal offer = Math.Max(3_000m, Cash.RoundTo(market - deduction, 100));
        bool sold = askingPrice <= offer * 1.05m;

        return new SaleResult(offer, findings, deduction, sold);
    }
}

// ---------------------------------------------------------------------------
// OYUNCU DURUMU
// ---------------------------------------------------------------------------

public sealed class PlayerState
{
    // 60.000₺ kasıtlı olarak dar. Medyan araç ~39.000₺ olduğu için oyuncu
    // ilk turda ekipman ile araç bütçesi arasında seçim yapmak ZORUNDA.
    // Bu kısıt oyunun gerilimini besliyor — rahat bir başlangıç sermayesi
    // ilk 5 saatteki bütün kararları önemsizleştirir.
    public decimal Money { get; set; } = 60_000m;
    public int Day { get; set; } = 1;
    public int Minutes { get; set; } = 9 * 60;   // 09:00
    public HashSet<string> Equipment { get; } = [];
    public float Reputation { get; set; } = 20f;

    public VehicleInstance? OwnedVehicle { get; set; }
    public PlayerKnowledge? OwnedKnowledge { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal RepairSpend { get; set; }

    public bool Has(string equipmentId) => Equipment.Contains(equipmentId);

    public string Clock => $"{Minutes / 60:00}:{Minutes % 60:00}";

    public void AdvanceMinutes(int m)
    {
        Minutes += m;
        while (Minutes >= 24 * 60) { Minutes -= 24 * 60; Day++; }
    }
}
