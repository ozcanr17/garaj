namespace Garaj.Core;

// ---------------------------------------------------------------------------
// GÜVEN ARALIĞI — oyunun kalbi (blueprint §3.3)
//
// Oyuncuya ASLA kesin sayı gösterilmez. Gösterilen şey bir BANT ve o banda
// duyulan güvendir. Güven asla %100 olmaz.
// ---------------------------------------------------------------------------

public readonly record struct ConfidenceRange(float Min, float Max, float Confidence)
{
    /// <summary>Güven tavanı. Blueprint: "Güven asla %100 olmaz. Bu, gerginliği korur."</summary>
    public const float MaxConfidence = 0.94f;

    public static readonly ConfidenceRange Unknown = new(0f, 100f, 0.04f);

    public float Mid => (Min + Max) / 2f;
    public float Width => Max - Min;
    public bool IsUnexamined => Confidence <= 0.05f;

    public string Verdict => Mid switch
    {
        _ when IsUnexamined => "bilinmiyor",
        >= 85f => "çok iyi",
        >= 70f => "iyi",
        >= 55f => "idare eder",
        >= 40f => "yorgun",
        >= 25f => "kötü",
        _      => "hurda"
    };

    /// <summary>Belirsizliği ifade eden dil — güven düştükçe hedge artar.</summary>
    public string Phrase => Confidence switch
    {
        _ when IsUnexamined => "İncelenmedi",
        < 0.30f => $"Belki {Verdict}?",
        < 0.55f => $"{char.ToUpper(Verdict[0])}{Verdict[1..]} görünüyor",
        < 0.80f => $"Muhtemelen {Verdict}",
        _       => $"{char.ToUpper(Verdict[0])}{Verdict[1..]} (büyük ihtimalle)"
    };
}

public static class Belief
{
    /// <summary>
    /// Yeni kanıtı mevcut inanca kat. Kanıtlar KESİŞİRSE inanç daralır ve güven artar.
    /// KESİŞMEZSE bu bir çelişkidir — güven DÜŞER. Bu, dolandırıcılığın oyuncuya
    /// sezdirildiği yerdir: iki ölçüm birbirini tutmuyorsa bir şey yanlıştır.
    /// </summary>
    public static ConfidenceRange Combine(ConfidenceRange prior, ConfidenceRange evidence)
    {
        if (prior.IsUnexamined) return evidence;

        float lo = MathF.Max(prior.Min, evidence.Min);
        float hi = MathF.Min(prior.Max, evidence.Max);

        if (lo < hi)
        {
            // Uyumlu kanıt: bantlar kesişiyor, güven birleşir
            float c = 1f - (1f - prior.Confidence) * (1f - evidence.Confidence);
            return new ConfidenceRange(lo, hi, MathF.Min(c, ConfidenceRange.MaxConfidence));
        }

        // ÇELİŞKİ: yeni kanıt eskisini yalanlıyor. Daha hassas olanı al ama güveni kır.
        var better = evidence.Width <= prior.Width ? evidence : prior;
        float widened = MathF.Min(100f, better.Width * 1.6f);
        float mid = better.Mid;
        return new ConfidenceRange(
            MathF.Max(0f, mid - widened / 2f),
            MathF.Min(100f, mid + widened / 2f),
            MathF.Max(0.15f, better.Confidence * 0.55f));
    }

    /// <summary>
    /// Bir ölçümden kanıt üret. <paramref name="perceived"/> ALGILANAN değerdir —
    /// maskeleme varsa bu gerçek değerden farklıdır ve oyuncu yanlış banda inanır.
    /// </summary>
    public static ConfidenceRange Measure(float perceived, float precision, Random rng)
    {
        precision = Math.Clamp(precision, 0.05f, 0.92f);
        float width = MathF.Max(5f, 100f * (1f - precision));

        // Ölçüm hatası: bant her zaman gerçeğin tam ortasında değildir
        float bias = (float)Rng.Gaussian(rng) * width * 0.18f;
        float center = Math.Clamp(perceived + bias, 0f, 100f);

        return new ConfidenceRange(
            MathF.Max(0f, center - width / 2f),
            MathF.Min(100f, center + width / 2f),
            MathF.Min(precision, ConfidenceRange.MaxConfidence));
    }
}

// ---------------------------------------------------------------------------
// GÖZLEM — teşhisin ürettiği metin bulgular
// ---------------------------------------------------------------------------

public enum ObservationKind
{
    /// <summary>Kesin bulgu: kusur keşfedildi.</summary>
    Finding,
    /// <summary>Şüphe: bir şey tuhaf ama kanıt yok. Maskeleme ipuçları burada.</summary>
    Suspicion,
    /// <summary>Rahatlatıcı bulgu.</summary>
    Reassurance,
    /// <summary>Belgeler arası çelişki.</summary>
    Contradiction,
    /// <summary>Satıcının davranışsal ipucu.</summary>
    SellerTell
}

public sealed record Observation(string Text, ObservationKind Kind, MethodId Source);

// ---------------------------------------------------------------------------
// OYUNCU BİLGİSİ — aracın gerçeğinden TAMAMEN AYRI bir nesne
//
// Mimari kural: sunum katmanı VehicleInstance.Condition'a asla erişmez.
// Sadece PlayerKnowledge'ı okur. Bilgi asimetrisi böyle garanti altına alınır.
// ---------------------------------------------------------------------------

public sealed class PlayerKnowledge
{
    private readonly Dictionary<string, ConfidenceRange> _parts = [];

    public HashSet<string> DiscoveredDefects { get; } = [];
    public List<Observation> Observations { get; } = [];
    public HashSet<MethodId> MethodsUsed { get; } = [];
    public decimal SpentOnDiagnosis { get; set; }
    public int MinutesSpent { get; set; }

    public ConfidenceRange For(string partId)
        => _parts.TryGetValue(partId, out var r) ? r : ConfidenceRange.Unknown;

    public void Update(string partId, ConfidenceRange evidence)
        => _parts[partId] = Belief.Combine(For(partId), evidence);

    public void Observe(string text, ObservationKind kind, MethodId source)
        => Observations.Add(new Observation(text, kind, source));

    /// <summary>Sistem grubu için toplu inanç — parça bantlarının ağırlıklı bileşimi.</summary>
    public ConfidenceRange ForGroup(SystemGroup g)
    {
        var ids = PartCatalog.InGroup(g).Select(p => p.Id).ToList();
        var known = ids.Select(For).Where(r => !r.IsUnexamined).ToList();
        if (known.Count == 0) return ConfidenceRange.Unknown;

        float min = known.Average(r => r.Min);
        float max = known.Average(r => r.Max);
        // Grubun tamamı incelenmediyse güven düşük kalır
        float coverage = (float)known.Count / ids.Count;
        float conf = known.Average(r => r.Confidence) * coverage;
        return new ConfidenceRange(min, max, MathF.Min(conf, ConfidenceRange.MaxConfidence));
    }

    public IEnumerable<string> ExaminedParts => _parts.Keys;
}

// ---------------------------------------------------------------------------

public static class Rng
{
    /// <summary>Box-Muller. Ortalama 0, standart sapma 1.</summary>
    public static double Gaussian(Random rng)
    {
        double u1 = 1.0 - rng.NextDouble();
        double u2 = 1.0 - rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }

    public static float Range(Random rng, float min, float max)
        => min + (float)rng.NextDouble() * (max - min);

    public static bool Chance(Random rng, float p) => rng.NextDouble() < p;

    public static T Pick<T>(Random rng, IReadOnlyList<T> items) => items[rng.Next(items.Count)];
}
