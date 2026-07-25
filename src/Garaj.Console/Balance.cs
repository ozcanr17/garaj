using Garaj.Core;
using Sys = System.Console;

namespace GarajApp;

/// <summary>
/// Denge test aracı. Binlerce araç üretip ekonominin çalışıp çalışmadığına bakar.
/// Kod yazmadan denge ayarı yapmanın en hızlı yolu bu (blueprint §7.1).
/// Kullanım:  dotnet run --project src/Garaj.Console -- --balance [adet]
/// </summary>
internal static class Balance
{
    public static void Run(int count, int seed)
    {
        var rng = new Random(seed);

        var rows = new List<(VehicleInstance V, Seller S, decimal Ask, decimal Restored,
                             decimal Repair, decimal Margin)>();

        for (int i = 0; i < count; i++)
        {
            var (v, s) = VehicleGenerator.Generate(rng);
            var (lo, hi) = Valuation.RestoredValueBand(v);
            decimal restored = (lo + hi) / 2m;
            decimal repair = Valuation.TrueRepairBill(v);
            decimal margin = restored - (v.AskingPrice + repair);
            rows.Add((v, s, v.AskingPrice, restored, repair, margin));
        }

        Sys.WriteLine();
        Sys.WriteLine($"=== DENGE RAPORU — {count} araç, seed {seed} ===");
        Sys.WriteLine();

        Stat("İstenen fiyat",        rows.Select(r => r.Ask));
        Stat("Onarılmış değer",      rows.Select(r => r.Restored));
        Stat("Gerçek onarım faturası", rows.Select(r => r.Repair));
        Stat("Marj (tam onarımda)",  rows.Select(r => r.Margin));

        Sys.WriteLine();
        int profitable = rows.Count(r => r.Margin > 0);
        Sys.WriteLine($"  Tam onarımda kârlı çıkan araç : {profitable}/{count}  " +
                      $"(%{profitable * 100.0 / count:F0})");

        // Kısmi onarım stratejisi: sadece 55'in altındaki parçaları onar
        int partialProfitable = 0;
        decimal partialTotal = 0m;
        foreach (var r in rows)
        {
            decimal cost = r.V.Parts.Values.Where(p => p.Condition < 55f).Sum(p => p.TrueRepairCost());
            decimal margin = r.Restored * 0.92m - (r.Ask + cost);
            partialTotal += margin;
            if (margin > 0) partialProfitable++;
        }
        Sys.WriteLine($"  Seçici onarımda kârlı çıkan   : {partialProfitable}/{count}  " +
                      $"(%{partialProfitable * 100.0 / count:F0})   " +
                      $"ort. marj {partialTotal / count:N0}₺");

        Sys.WriteLine();
        Sys.WriteLine($"  Maskeleme taşıyan araç        : {rows.Count(r => r.V.Tampers.Count > 0)}/{count}");
        Sys.WriteLine($"  Km oynatılmış                 : {rows.Count(r => r.V.OdometerReading != r.V.TrueOdometer)}/{count}");
        Sys.WriteLine($"  Zaman bombası taşıyan         : {rows.Count(r => r.V.AllDefects.Any(d => d.SurfacesAfterKm > 0))}/{count}");
        Sys.WriteLine($"  Ortalama kusur sayısı         : {rows.Average(r => r.V.AllDefects.Count()):F1}");
        Sys.WriteLine($"  Ortalama gerçek durum         : {rows.Average(r => r.V.Parts.Values.Average(p => p.Condition)):F1}");

        // Maskelemenin fiyata etkisi — dolandırıcılık gerçekten para kazandırıyor mu?
        var withTamper = rows.Where(r => r.V.Tampers.Count > 0).ToList();
        var without = rows.Where(r => r.V.Tampers.Count == 0).ToList();
        if (withTamper.Count > 0 && without.Count > 0)
        {
            Sys.WriteLine();
            Sys.WriteLine("  MASKELEMENİN ETKİSİ (satıcı ne kazanıyor):");
            Sys.WriteLine($"    Maskelemesiz araçta marj : {without.Average(r => r.Margin),12:N0}₺");
            Sys.WriteLine($"    Maskelemeli araçta marj  : {withTamper.Average(r => r.Margin),12:N0}₺");
            Sys.WriteLine("    (maskelemeli marj daha DÜŞÜK olmalı — oyuncu fazla ödüyor demektir)");
        }

        // Satıcı arketip dağılımı
        Sys.WriteLine();
        Sys.WriteLine("  SATICI ARKETİPLERİ:");
        foreach (var g in rows.GroupBy(r => r.S.Archetype).OrderByDescending(g => g.Count()))
            Sys.WriteLine($"    {g.Key,-14} {g.Count(),4}   ort. dürüstlük %{g.Average(r => r.S.Honesty) * 100:F0}");

        Sys.WriteLine();
    }

    private static void Stat(string label, IEnumerable<decimal> values)
    {
        var v = values.OrderBy(x => x).ToList();
        Sys.WriteLine($"  {label,-24} min {v.First(),10:N0}   " +
                      $"medyan {v[v.Count / 2],10:N0}   " +
                      $"ort {v.Average(),10:N0}   " +
                      $"max {v.Last(),10:N0}");
    }
}
