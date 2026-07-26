namespace Garaj.Core;

// ---------------------------------------------------------------------------
// BELGE MASASI (blueprint §2.4 — "oyunun en zeki mekaniği olabilir")
//
// Papers Please modeli: oyun çelişkiyi SÖYLEMEZ. Oyuncu iki belgeyi yan yana
// koyar, hangi iki alanın çeliştiğini KENDİ seçer, ve iddiasını ortaya atar.
//
// Papers Please'den ayrılan iki nokta:
//   1. Kural kitabı kesin değil. Belgeler sahte olabilir; çelişki bulmak
//      kesin kanıt değil, güçlü bir şüphedir.
//   2. Bazı çelişkiler yalnızca FİZİKSEL teşhisle birleşince görünür
//      (tramer temiz + boya kalın). Masa tek başına yetmez.
//
// Yanlış iddia bedavaya gelmez: zaman yer ve satıcının sabrını tüketir.
// Aksi halde oyuncu her kombinasyonu deneyerek çözer — bu da bulmaca değildir.
// ---------------------------------------------------------------------------

public enum DocumentId { Ruhsat, ServisDefteri, Tramer, MuayeneRaporu, AracUzeri }

public sealed record DocumentField(
    string Key,
    string Label,
    string Value,
    DocumentId Source
);

/// <summary>İki alan arasında kurulabilecek iddia.</summary>
public sealed record FieldPair(DocumentField A, DocumentField B);

public sealed record ChallengeResult(
    bool IsContradiction,
    string Verdict,
    string? Explanation,
    decimal LeverageWeight
);

public static class DocumentDesk
{
    // -----------------------------------------------------------------------
    // BELGELERİ ALANLARA AÇ
    // -----------------------------------------------------------------------

    public static List<DocumentField> Fields(VehicleInstance v, DocumentId doc)
    {
        var d = v.Documents;
        var list = new List<DocumentField>();

        switch (doc)
        {
            case DocumentId.Ruhsat:
                list.Add(new("ruhsat_yil", "Model yılı", d.RuhsatModelYear.ToString(), doc));
                list.Add(new("ruhsat_motor", "Motor no", d.RuhsatEngineNumber, doc));
                list.Add(new("ruhsat_sasi", "Şasi no", v.Vin, doc));
                list.Add(new("ruhsat_sahip", "Sahip sayısı", $"{d.OwnerCount} el", doc));
                list.Add(new("ruhsat_plaka", "Plaka", v.Plate, doc));
                break;

            case DocumentId.ServisDefteri:
                if (d.ServiceHistory.Count == 0)
                {
                    list.Add(new("servis_yok", "Kayıt", "Hiç kayıt yok", doc));
                    break;
                }
                for (int i = 0; i < d.ServiceHistory.Count; i++)
                {
                    var s = d.ServiceHistory[i];
                    list.Add(new($"servis_{i}", $"{s.Year} — {s.Work}", $"{s.Km:N0} km", doc));
                }
                break;

            case DocumentId.Tramer:
                if (d.TramerRecords.Count == 0)
                {
                    list.Add(new("tramer_temiz", "Hasar kaydı", "Kayıt bulunamadı", doc));
                    break;
                }
                for (int i = 0; i < d.TramerRecords.Count; i++)
                {
                    var tr = d.TramerRecords[i];
                    list.Add(new($"tramer_{i}", $"{tr.Year} — {tr.Panel}", $"{tr.Amount:N0}₺ ödendi", doc));
                }
                break;

            case DocumentId.MuayeneRaporu:
                if (!d.HasInspectionReport)
                {
                    list.Add(new("muayene_yok", "Rapor", "Muayene raporu yok", doc));
                    break;
                }
                list.Add(new("muayene_yil", "Muayene yılı", d.InspectionYear.ToString(), doc));
                list.Add(new("muayene_sonuc", "Sonuç", "Geçti", doc));
                break;

            case DocumentId.AracUzeri:
                list.Add(new("arac_km", "Gösterge", $"{v.OdometerReading:N0} km", doc));
                list.Add(new("arac_motor", "Blok üzerindeki motor no", v.EngineNumber, doc));
                list.Add(new("arac_sasi", "Şasi plakası", v.Vin, doc));
                list.Add(new("arac_yil", "İlandaki model yılı", v.ModelYear.ToString(), doc));
                break;
        }

        return list;
    }

    public static string DocumentName(DocumentId d) => d switch
    {
        DocumentId.Ruhsat        => "Ruhsat",
        DocumentId.ServisDefteri => "Servis Defteri",
        DocumentId.Tramer        => "Tramer Kaydı",
        DocumentId.MuayeneRaporu => "Muayene Raporu",
        DocumentId.AracUzeri     => "Aracın Üzeri",
        _ => d.ToString()
    };

    // -----------------------------------------------------------------------
    // İDDİAYI DEĞERLENDİR
    //
    // Oyuncu iki alanı seçer ve "bunlar çelişiyor" der. Doğru mu?
    // -----------------------------------------------------------------------

    public static ChallengeResult Challenge(
        VehicleInstance v, PlayerKnowledge k, DocumentField a, DocumentField b)
    {
        var keys = new[] { a.Key, b.Key };

        // Servis defteri KENDİ İÇİNDE tutarsız olabilir: kayıtlar tarih ilerlerken
        // km geriye gidemez. Bu yüzden aynı belgenin iki satırı burada geçerlidir.
        if (a.Source == DocumentId.ServisDefteri && b.Source == DocumentId.ServisDefteri)
        {
            if (a.Key == b.Key)
                return new(false, "Aynı satırı kendisiyle karşılaştırdın.", null, 0m);

            var (first, second) = string.CompareOrdinal(a.Key, b.Key) < 0 ? (a, b) : (b, a);
            int kmFirst = ParseKm(first.Value), kmSecond = ParseKm(second.Value);
            int yearFirst = ParseYear(first.Label), yearSecond = ParseYear(second.Label);

            if (yearSecond > yearFirst && kmSecond < kmFirst)
            {
                return new(true, "ÇELİŞKİ YAKALANDI",
                    $"{yearFirst} yılında {kmFirst:N0} km, {yearSecond} yılında {kmSecond:N0} km yazıyor. " +
                    "Araç iki yılda geri gitmiş olamaz. Bu kayıtlar sonradan uydurulmuş.",
                    18_000m);
            }

            return new(false, "Tutarlı.",
                "Bu iki kayıt arasında km düzgün ilerlemiş.", 0m);
        }

        // Aynı belgeden iki alan (servis defteri hariç) karşılaştırılamaz
        if (a.Source == b.Source)
            return new(false, "Aynı belgenin iki satırı birbiriyle çelişemez.", null, 0m);
        bool Has(string p1, string p2)
            => keys.Any(x => x.StartsWith(p1)) && keys.Any(x => x.StartsWith(p2));

        // --- 0. Servis defteri boşsa karşılaştıracak bir şey yok ---
        if (keys.Any(x => x == "servis_yok"))
            return new(false, "Karşılaştıracak kayıt yok.",
                "Servis defteri boş. Yokluk bir çelişki üretmez — sadece bilinmeyeni büyütür.",
                0m);

        // --- 1. Servis kaydı km'si > gösterge ---
        if (Has("servis_", "arac_km"))
        {
            var servis = a.Key.StartsWith("servis_") ? a : b;
            int servisKm = ParseKm(servis.Value);

            if (servisKm > v.OdometerReading)
            {
                return new(true,
                    "ÇELİŞKİ YAKALANDI",
                    $"Servis defteri {servisKm:N0} km'de bakım yapıldığını yazıyor, " +
                    $"ama gösterge {v.OdometerReading:N0} km. Bir araç geri gidemez. " +
                    "Kilometre oynatılmış.",
                    22_000m);
            }

            return new(false,
                "Tutarlı.",
                $"{servisKm:N0} km, göstergedeki {v.OdometerReading:N0} km'den küçük. " +
                "Burada bir sorun yok.",
                0m);
        }

        // --- 2. Ruhsat motor no ≠ blok üzerindeki no ---
        if (Has("ruhsat_motor", "arac_motor"))
        {
            if (v.Documents.RuhsatEngineNumber != v.EngineNumber)
            {
                return new(true,
                    "ÇELİŞKİ YAKALANDI",
                    $"Ruhsatta {v.Documents.RuhsatEngineNumber}, blokta {v.EngineNumber} yazıyor. " +
                    "Motor değişmiş ve bu ruhsata işlenmemiş. Bu ciddi bir hukuki sorun.",
                    20_000m);
            }

            return new(false, "Tutarlı.", "İki numara birebir aynı. Motor orijinal.", 0m);
        }

        // --- 3. Ruhsat model yılı ≠ ilan ---
        if (Has("ruhsat_yil", "arac_yil"))
        {
            if (v.Documents.RuhsatModelYear != v.ModelYear)
            {
                return new(true, "ÇELİŞKİ YAKALANDI",
                    $"Ruhsatta {v.Documents.RuhsatModelYear}, ilanda {v.ModelYear} yazıyor. " +
                    "Biri yanlış ve muhtemelen kasıtlı.",
                    9_000m);
            }
            return new(false, "Tutarlı.", "Model yılları uyuşuyor.", 0m);
        }

        // --- 4. Şasi no karşılaştırması ---
        if (Has("ruhsat_sasi", "arac_sasi"))
            return new(false, "Tutarlı.", "Şasi numaraları aynı.", 0m);

        // --- 5. Tramer temiz + boya ölçümü kalın (YÖNTEM KOMBİNASYONU) ---
        if (Has("tramer_temiz", "arac_") || Has("tramer_", "arac_"))
        {
            bool measured = k.MethodsUsed.Contains(MethodId.BoyaKalinlik);
            var repainted = PartCatalog.InGroup(SystemGroup.Kaporta)
                .Where(p => !k.For(p.Id).IsUnexamined && k.For(p.Id).Mid < 55f)
                .ToList();

            if (v.Documents.TramerRecords.Count == 0 && measured && repainted.Count > 0)
            {
                return new(true, "ÇELİŞKİ YAKALANDI",
                    $"Tramer tertemiz görünüyor, ama boya ölçümünde {repainted[0].Name} " +
                    "boyalı çıktı. Sigortaya bildirilmeden onarılmış bir kaza var.",
                    16_000m);
            }

            if (v.Documents.TramerRecords.Count == 0 && !measured)
            {
                return new(false, "Kanıtın yok.",
                    "Tramer temiz. Bunun yalan olduğunu iddia etmek için önce boya " +
                    "kalınlığını ölçmen gerek — elinde ölçüm yokken bu sadece bir his.",
                    0m);
            }

            return new(false, "Tutarlı.", "Kayıt ile araç durumu örtüşüyor.", 0m);
        }

        // --- 6. Tramer kaydı aracın üretiminden önce tarihli ---
        if (Has("tramer_", "ruhsat_yil") || Has("tramer_", "arac_yil"))
        {
            var tramer = a.Key.StartsWith("tramer_") ? a : b;
            int hasarYili = ParseYear(tramer.Label);

            if (hasarYili > 0 && hasarYili < v.ModelYear)
            {
                return new(true, "ÇELİŞKİ YAKALANDI",
                    $"Tramerde {hasarYili} yılına ait bir hasar kaydı var, ama araç {v.ModelYear} model. " +
                    "Araç daha üretilmeden hasar görmüş olamaz. Bu kayıt başka bir araca ait.",
                    17_000m);
            }

            return new(false, "Tutarlı.",
                hasarYili > 0
                    ? $"{hasarYili} hasar kaydı, {v.ModelYear} model yılından sonra. Normal."
                    : "Karşılaştırılabilir bir tarih yok.",
                0m);
        }

        // --- 7. Muayene raporu gelecek tarihli ---
        if (Has("muayene_yil", "arac_") || Has("muayene_yil", "servis_"))
        {
            if (v.Documents.InspectionYear > VehicleGenerator.CurrentYear)
            {
                return new(true, "ÇELİŞKİ YAKALANDI",
                    $"Muayene raporu {v.Documents.InspectionYear} tarihli, ama bulunduğumuz yıl " +
                    $"{VehicleGenerator.CurrentYear}. Henüz olmamış bir muayene raporu tutuyorsun. Sahte.",
                    14_000m);
            }
            return new(false, "Tutarlı.", "Muayene tarihi geçerli bir aralıkta.", 0m);
        }

        // --- Anlamsız eşleştirme ---
        return new(false,
            "Bu ikisi karşılaştırılabilir şeyler değil.",
            "Farklı türde iki bilgiyi yan yana koydun ama aralarında bir ilişki yok.",
            0m);
    }

    private static int ParseKm(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var n) ? n : 0;
    }

    /// <summary>Etiketin başındaki 4 haneli yılı okur ("1994 — Periyodik bakım").</summary>
    private static int ParseYear(string label)
    {
        var token = label.Split(' ', '—').FirstOrDefault(t => t.Length == 4 && t.All(char.IsDigit));
        return token is not null && int.TryParse(token, out var y) ? y : 0;
    }
}
