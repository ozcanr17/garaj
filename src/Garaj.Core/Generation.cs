namespace Garaj.Core;

// ---------------------------------------------------------------------------
// PROSEDÜREL ARAÇ ÜRETİMİ (blueprint §8.1)
//
// Katmanlı üretim. Her katman bir öncekinin üstüne yazar:
//   1. Temel model  2. Yaş + km  3. Bakım geçmişi  4. Olay geçmişi
//   5. Önceki sahip profili  6. Maskeleme  7. Evrak paketi
//
// KRİTİK: Sahip profili oyuncuya ASLA söylenmez. Oyuncu aşınma kalıbından
// tahmin eder. Blueprint: "50 saat sonra 'bu araba taksi olmuş' diyebilir."
// ---------------------------------------------------------------------------

public sealed record BaseModel(
    string Name, string Trim, int YearMin, int YearMax, decimal BaseValue);

public sealed record MaintenanceStyle(string Name, float ConditionModifier, int ServiceRecordCount);

public static class VehicleGenerator
{
    public const int CurrentYear = 2026;

    // NOT: Değerler 2026 gerçekliğine göre. Parça fiyatları gerçek olduğu için
    // araç değerleri de gerçek olmak ZORUNDA — yoksa her araç ekonomik hurdadır.
    private static readonly BaseModel[] _models =
    [
        new("Tofaş Şahin S",   "1.6 ie", 1990, 1999, 145_000m),
        new("Renault 12 TS",   "1.3",    1975, 1985, 120_000m),
        new("Tofaş Doğan SLX", "1.6 ie", 1992, 2000, 175_000m),
        new("Tofaş Kartal SLX","1.6 ie", 1995, 2002, 160_000m),
    ];

    private static readonly string[] _cities =
    [
        "Çankaya, Ankara", "Bornova, İzmir", "Osmangazi, Bursa",
        "Pendik, İstanbul", "Selçuklu, Konya", "Seyhan, Adana"
    ];

    private static readonly string[] _sellerNames =
    [
        "Hakan U.", "Mehmet A.", "Serkan T.", "Yılmaz K.", "Osman D.",
        "Bülent Ş.", "Kadir E.", "Necati B.", "Ferhat G.", "İlhan M."
    ];

    private static readonly MaintenanceStyle[] _maintenance =
    [
        new("iyi bakılmış",   +12f, 5),
        new("düzenli",         +4f, 3),
        new("karışık",         -6f, 2),
        new("ihmal edilmiş",  -18f, 0),
    ];

    // -----------------------------------------------------------------------
    // ANA ÜRETİM
    // -----------------------------------------------------------------------

    public static (VehicleInstance Vehicle, Seller Seller) Generate(Random rng)
    {
        // --- KATMAN 1: Temel model ---
        var model = Rng.Pick(rng, _models);
        int year = rng.Next(model.YearMin, model.YearMax + 1);
        int age = CurrentYear - year;

        var v = new VehicleInstance { InstanceId = Guid.NewGuid().ToString("N")[..8] }
        ;
        v.ModelName = model.Name;
        v.Trim = model.Trim;
        v.ModelYear = year;
        v.ModelBaseValue = model.BaseValue;
        v.City = Rng.Pick(rng, _cities);
        v.Vin = $"NM4{rng.Next(100000, 999999)}{(char)('A' + rng.Next(26))}";
        v.EngineNumber = $"{(char)('A' + rng.Next(26))}{rng.Next(10, 99)}" +
                         $"{(char)('A' + rng.Next(26))}{rng.Next(1000, 9999)}";
        v.Plate = $"{rng.Next(1, 82):00} {(char)('A' + rng.Next(26))}" +
                  $"{(char)('A' + rng.Next(26))} {rng.Next(100, 999)}";

        // --- KATMAN 2: Yaş + kilometre ---
        var owner = (OwnerProfile)rng.Next(Enum.GetValues<OwnerProfile>().Length);
        v.Owner = owner;
        int trueKm = GenerateKilometers(owner, age, rng);
        v.TrueOdometer = trueKm;
        v.OdometerReading = trueKm;   // KATMAN 6'da oynatılabilir

        // --- KATMAN 3: Bakım geçmişi ---
        var maint = Rng.Pick(rng, _maintenance);

        // --- KATMAN 4+5: Parça durumları (sahip profili imzasıyla) ---
        foreach (var def in PartCatalog.All)
        {
            float cond = ComputeCondition(def, age, trueKm, maint, owner, rng);
            var part = new PartInstance
            {
                DefId = def.Id,
                Condition = cond,
                Origin = RollOrigin(rng, maint),
                InstalledAtKm = 0,
                IsSeized = cond < 40f && Rng.Chance(rng, 0.30f),
            };
            v.Parts[def.Id] = part;
        }

        // --- Kusurlar ---
        GenerateDefects(v, rng);

        // --- KATMAN 6: Maskeleme ---
        var seller = GenerateSeller(v, rng);
        ApplyTampers(v, seller, rng);

        // --- KATMAN 7: Evrak ---
        GenerateDocuments(v, maint, rng);

        // --- Fiyat ve ilan metni ---
        v.AskingPrice = ComputeAskingPrice(v, seller, rng);
        v.ListingText = GenerateListingText(v, seller, rng);

        // Satıcı hangi kusurları biliyor?
        foreach (var d in v.AllDefects)
            if (Rng.Chance(rng, seller.Knowledge))
                seller.KnownDefectIds.Add(d.Id);

        return (v, seller);
    }

    // -----------------------------------------------------------------------

    private static int GenerateKilometers(OwnerProfile owner, int age, Random rng)
    {
        float perYear = owner switch
        {
            OwnerProfile.YasliCift      => Rng.Range(rng, 3_000f, 7_000f),
            OwnerProfile.GencSurucu     => Rng.Range(rng, 14_000f, 22_000f),
            OwnerProfile.Taksi          => Rng.Range(rng, 45_000f, 70_000f),
            OwnerProfile.KiralikFilo    => Rng.Range(rng, 25_000f, 40_000f),
            OwnerProfile.MerakliTamirci => Rng.Range(rng, 8_000f, 15_000f),
            OwnerProfile.UzunSurePark   => Rng.Range(rng, 2_000f, 5_000f),
            OwnerProfile.SehirIci       => Rng.Range(rng, 6_000f, 11_000f),
            OwnerProfile.UzunYol        => Rng.Range(rng, 30_000f, 45_000f),
            _ => 15_000f
        };
        int km = (int)(perYear * age);
        return Math.Clamp(km / 1000 * 1000, 25_000, 890_000);
    }

    /// <summary>
    /// Sahip profilinin AŞINMA İMZASI. Bu tablo oyunun anti-ezberleme katmanının
    /// temeli — aynı km'deki iki araç tamamen farklı yerlerinden yorgun olur.
    /// </summary>
    private static float OwnerSignature(OwnerProfile owner, PartDefinition def)
    {
        var g = def.Group;
        return owner switch
        {
            // Düşük km ama uzun park: kauçuk ve akü ölü, diskler paslı, motor iyi
            OwnerProfile.YasliCift => g switch
            {
                SystemGroup.Motor => +18f,
                SystemGroup.Tekerlek => -25f,
                SystemGroup.Elektrik => def.Id == "aku" ? -35f : +5f,
                SystemGroup.Fren => -12f,
                SystemGroup.Sogutma => -14f,   // hortumlar/contalar kurumuş
                SystemGroup.IcMekan => +20f,
                _ => 0f
            },

            // Debriyaj bitik, fren aşırı, süspansiyon zorlanmış, motor iyi
            OwnerProfile.GencSurucu => g switch
            {
                SystemGroup.Sanziman => def.Id == "debriyaj" ? -38f : -14f,
                SystemGroup.Fren => -26f,
                SystemGroup.Suspansiyon => -22f,
                SystemGroup.Motor => +8f,
                SystemGroup.Kaporta => -10f,
                _ => 0f
            },

            // Yüksek km ama düzenli bakım: motor sağlam, iç mekan bitik
            OwnerProfile.Taksi => g switch
            {
                SystemGroup.Motor => +22f,
                SystemGroup.Sanziman => +8f,
                SystemGroup.IcMekan => -40f,
                SystemGroup.Suspansiyon => -30f,
                SystemGroup.Fren => +6f,
                _ => 0f
            },

            OwnerProfile.KiralikFilo => g switch
            {
                SystemGroup.IcMekan => -28f,
                SystemGroup.Kaporta => -20f,
                SystemGroup.Sanziman => -18f,
                SystemGroup.Motor => -6f,
                _ => 0f
            },

            // Modifiye, bazı işler mükemmel bazıları amatör
            OwnerProfile.MerakliTamirci => g switch
            {
                SystemGroup.Motor => +14f,
                SystemGroup.Elektrik => -24f,   // amatör kablo işi
                SystemGroup.Suspansiyon => +10f,
                SystemGroup.Kaporta => -8f,
                _ => 0f
            },

            // Kemirgen hasarı, tüm kauçuk sertleşmiş, yakıt bozulmuş
            OwnerProfile.UzunSurePark => g switch
            {
                SystemGroup.Elektrik => def.Id == "kablo_demeti" ? -45f : -20f,
                SystemGroup.Sogutma => -30f,
                SystemGroup.Tekerlek => -32f,
                SystemGroup.Fren => -25f,
                SystemGroup.Motor => -16f,
                SystemGroup.Kaporta => -14f,
                _ => 0f
            },

            // Düşük km ama yüksek motor saati, çok soğuk çalıştırma
            OwnerProfile.SehirIci => g switch
            {
                SystemGroup.Motor => -20f,
                SystemGroup.Sanziman => -16f,
                SystemGroup.Fren => -18f,
                SystemGroup.Kaporta => +8f,
                _ => 0f
            },

            // Yüksek km ama düşük aşınma — otoyol kilometresi
            OwnerProfile.UzunYol => g switch
            {
                SystemGroup.Motor => +16f,
                SystemGroup.Sanziman => +14f,
                SystemGroup.Fren => +10f,
                SystemGroup.Tekerlek => -12f,
                SystemGroup.Suspansiyon => -8f,
                _ => 0f
            },

            _ => 0f
        };
    }

    /// <summary>
    /// KRİTİK MODEL: 36 yaşındaki bir aracın parçaları 36 yaşında DEĞİLDİR.
    /// Aşınan parçalar ömrü dolunca değişmiştir — bakım tam olarak budur.
    /// Bu yüzden aşınma toplam km üzerinden değil, PARÇANIN ÜZERİNDEKİ km
    /// üzerinden hesaplanır. İhmal edilmiş araçta değişimler atlanmıştır.
    ///
    /// Yaş ise her parçayı değil, sadece kauçuk/korozyon hassas parçaları vurur.
    /// </summary>
    private static float ComputeCondition(
        PartDefinition def, int age, int km, MaintenanceStyle maint, OwnerProfile owner, Random rng)
    {
        // Parçanın servis ömrü (km). Yüksek WearRate = kısa ömür = sık değişim.
        float serviceLifeKm = 200_000f / MathF.Max(0.30f, def.WearRate);

        // Bakım titizliği: değişimlerin kaçı gerçekten yapılmış?
        float diligence = maint.Name switch
        {
            "iyi bakılmış" => 0.95f,
            "düzenli"      => 0.88f,
            "karışık"      => 0.68f,
            _              => 0.42f,
        };

        float cycles = km / serviceLifeKm;
        float replaced = MathF.Floor(cycles) * diligence;
        float kmOnPart = MathF.Max(0f, (cycles - replaced)) * serviceLifeKm;

        float wear = Math.Clamp(kmOnPart / serviceLifeKm, 0f, 1.9f);
        float cond = 100f - wear * 38f;

        // Yaş aşınması — kauçuk, conta, pas. Motor bloğunu ilgilendirmez.
        float ageSensitivity = def.Group switch
        {
            SystemGroup.Tekerlek => 1.10f,
            SystemGroup.Sogutma  => 0.90f,
            SystemGroup.Kaporta  => 0.80f,
            SystemGroup.Elektrik => 0.50f,
            SystemGroup.IcMekan  => 0.45f,
            SystemGroup.Egzoz    => 0.70f,
            _                    => 0.22f,
        };
        cond -= age * 0.46f * ageSensitivity;

        cond += maint.ConditionModifier * 0.35f;
        cond += OwnerSignature(owner, def) * 0.55f;
        cond += (float)Rng.Gaussian(rng) * 7f;

        return Math.Clamp(cond, 4f, 97f);
    }

    private static PartOrigin RollOrigin(Random rng, MaintenanceStyle maint)
    {
        double r = rng.NextDouble();
        if (maint.Name == "ihmal edilmiş")
            return r < 0.45 ? PartOrigin.Used : r < 0.70 ? PartOrigin.Aftermarket
                 : r < 0.78 ? PartOrigin.Counterfeit : PartOrigin.OEM;

        return r < 0.55 ? PartOrigin.OEM : r < 0.80 ? PartOrigin.Aftermarket
             : r < 0.94 ? PartOrigin.Used : PartOrigin.Refurbished;
    }

    // -----------------------------------------------------------------------
    // KUSUR ÜRETİMİ
    // -----------------------------------------------------------------------

    private sealed record DefectTemplate(
        SystemGroup Group, DefectType Type, string Description,
        MethodId[] RevealedBy, decimal ExtraCost);

    private static readonly DefectTemplate[] _defectTemplates =
    [
        new(SystemGroup.Motor, DefectType.Kacak, "karter contasından yağ sızıyor, altı ıslak.",
            [MethodId.Lift, MethodId.Dokunma, MethodId.TestSurusuUzun], 2_200m),
        new(SystemGroup.Motor, DefectType.Gurultu, "triger bölgesinden ritmik bir tıkırtı geliyor.",
            [MethodId.Calistir, MethodId.TestSurusuKisa, MethodId.TestSurusuUzun], 1_800m),
        new(SystemGroup.Motor, DefectType.Asinma, "silindirler arası kompresyon dengesiz.",
            [MethodId.Kompresyon, MethodId.Endoskop], 14_000m),
        new(SystemGroup.Motor, DefectType.Kacak, "silindir kapak contası su-yağ karıştırıyor.",
            [MethodId.Kompresyon, MethodId.TestSurusuUzun, MethodId.Endoskop], 6_500m),

        new(SystemGroup.Sogutma, DefectType.Kacak, "radyatör peteğinden sızıntı izi var.",
            [MethodId.Gozle, MethodId.Lift, MethodId.TestSurusuUzun], 2_400m),
        new(SystemGroup.Sogutma, DefectType.Tikanma, "termostat takılı kalmış, hararet dengesiz.",
            [MethodId.TestSurusuUzun, MethodId.OBD], 900m),

        new(SystemGroup.Sanziman, DefectType.Asinma, "debriyaj çok yüksekte kavrıyor, balata bitmek üzere.",
            [MethodId.TestSurusuKisa, MethodId.TestSurusuUzun], 3_800m),
        new(SystemGroup.Sanziman, DefectType.Bosluk, "2. viteste zorlama var, senkromeç yorgun.",
            [MethodId.TestSurusuUzun], 9_000m),

        new(SystemGroup.Fren, DefectType.Asinma, "balatalar sınırın altına inmiş.",
            [MethodId.Gozle, MethodId.Lift, MethodId.TestSurusuKisa], 800m),
        new(SystemGroup.Fren, DefectType.Egilme, "diskler tabla yapmış, frende titreme var.",
            [MethodId.TestSurusuKisa, MethodId.Lift], 1_600m),

        new(SystemGroup.Suspansiyon, DefectType.Bosluk, "rotilde elle hissedilen boşluk var.",
            [MethodId.Lift], 1_100m),
        new(SystemGroup.Suspansiyon, DefectType.Kacak, "ön amortisör yağ atmış.",
            [MethodId.Lift, MethodId.Gozle], 2_800m),

        new(SystemGroup.Elektrik, DefectType.ElektrikArizasi, "alternatör şarj dalgalanması yapıyor.",
            [MethodId.OBD, MethodId.Calistir], 3_600m),
        new(SystemGroup.Elektrik, DefectType.ElektrikArizasi, "kablo demetinde kemirgen hasarı var.",
            [MethodId.Lift, MethodId.OBD], 5_200m),

        new(SystemGroup.Kaporta, DefectType.Korozyon, "çamurluk iç kısmında kabaran pas var.",
            [MethodId.Lift, MethodId.BoyaKalinlik], 4_500m),
        new(SystemGroup.Kaporta, DefectType.Egilme, "panel düzeltilmiş, hat gözle fark ediliyor.",
            [MethodId.BoyaKalinlik, MethodId.Gozle], 3_200m),

        new(SystemGroup.Egzoz, DefectType.Korozyon, "orta susturucu delinmiş, ses artıyor.",
            [MethodId.Lift, MethodId.Calistir], 1_200m),

        new(SystemGroup.IcMekan, DefectType.Asinma, "sürücü koltuğu yanı yırtılmış, sünger görünüyor.",
            [MethodId.Gozle], 1_900m),

        new(SystemGroup.Tekerlek, DefectType.Asinma, "lastiklerin DOT tarihi eski, kauçuk sertleşmiş.",
            [MethodId.Gozle, MethodId.Lift], 4_800m),
    ];

    private static void GenerateDefects(VehicleInstance v, Random rng)
    {
        int counter = 0;

        foreach (var part in v.Parts.Values)
        {
            var candidates = _defectTemplates.Where(t => t.Group == part.Def.Group).ToList();
            if (candidates.Count == 0) continue;

            // Durum ne kadar kötüyse kusur ihtimali o kadar yüksek.
            // Hedef: araç başına ortalama 3-5 kusur. Daha fazlası oyuncuyu boğar.
            float chance = part.Condition switch
            {
                < 20f => 0.55f,
                < 35f => 0.30f,
                < 50f => 0.14f,
                < 65f => 0.05f,
                _     => 0.015f
            };

            if (!Rng.Chance(rng, chance)) continue;

            var t = Rng.Pick(rng, candidates);
            float severity = Math.Clamp(1f - part.Condition / 100f + Rng.Range(rng, -0.15f, 0.15f), 0.1f, 1f);

            part.Defects.Add(new Defect
            {
                Id = $"d{counter++}_{part.DefId}",
                PartId = part.DefId,
                Type = t.Type,
                Severity = severity,
                Description = t.Description,
                RevealedBy = t.RevealedBy,
                ExtraRepairCost = Cash.RoundTo(t.ExtraCost * (decimal)(0.6f + severity * 0.8f), 100),
                SurfacesAfterKm = 0,
            });
        }

        // ZAMAN BOMBASI (§4.4 Seviye 4) — ARAÇ başına en fazla bir tane, %8 ihtimalle.
        // Blueprint'in şartı: oranı düşük, etkisi yıkıcı değil sinir bozucu olmalı.
        // Kusur başına zar atmak bunu %30'a çıkarıyordu; oyuncuyu çaresiz hissettirir.
        if (Rng.Chance(rng, 0.08f))
        {
            var all = v.AllDefects.ToList();
            if (all.Count > 0)
                Rng.Pick(rng, all).SurfacesAfterKm = rng.Next(800, 4000);
        }
    }

    // -----------------------------------------------------------------------
    // MASKELEME KATMANI
    // -----------------------------------------------------------------------

    private static void ApplyTampers(VehicleInstance v, Seller seller, Random rng)
    {
        // Önce ARAÇ bazında karar: bu araca hiç dokunulmuş mu?
        // (Her maskeleme için ayrı ayrı zar atmak neredeyse her aracı sahte yapıyordu.)
        float carTamperChance = (1f - seller.Honesty) * 0.72f;
        if (!Rng.Chance(rng, carTamperChance)) return;

        int maxTampers = seller.Archetype == SellerArchetype.Dolandirici
            ? rng.Next(1, 4)
            : rng.Next(1, 3);

        // Gerçekten gizleyecek bir şeyi olan maskelemeler önce denenir
        var pool = TamperCatalog.All
            .OrderByDescending(def => v.AllDefects.Any(d =>
                def.Hides.Contains(d.Type) &&
                def.AffectsGroups.Contains(v.Part(d.PartId).Def.Group)))
            .ThenBy(_ => rng.Next())
            .ToList();

        foreach (var def in pool)
        {
            if (v.Tampers.Count >= maxTampers) break;

            bool hasSomethingToHide = v.AllDefects.Any(d =>
                def.Hides.Contains(d.Type) &&
                def.AffectsGroups.Contains(v.Part(d.PartId).Def.Group));

            // Gizleyecek şey yoksa nadiren yapılır (satıcı yine de "temizlik" yapmış olabilir)
            if (!hasSomethingToHide && !Rng.Chance(rng, 0.18f)) continue;

            v.Tampers.Add(new ActiveTamper
            {
                TamperId = def.Id,
                KmRemaining = def.IsPermanent
                    ? int.MaxValue
                    : rng.Next(def.LifespanKmMin, def.LifespanKmMax + 1),
            });

            // Kilometre oynatma göstergeyi de değiştirir
            if (def.Id == "km_oynatma")
            {
                // Yuvarlak sayıya çek — aksi halde "tek tuhaf sayı" istemsiz bir tell olur
                int rollback = rng.Next(40, 180) * 1000;
                v.OdometerReading = Math.Max(35_000, (v.TrueOdometer - rollback) / 1000 * 1000);
            }
        }
    }

    // -----------------------------------------------------------------------
    // EVRAK PAKETİ
    // -----------------------------------------------------------------------

    private static void GenerateDocuments(VehicleInstance v, MaintenanceStyle maint, Random rng)
    {
        var docs = v.Documents;
        docs.RuhsatModelYear = v.ModelYear;
        docs.RuhsatEngineNumber = v.EngineNumber;
        docs.OwnerCount = v.Owner switch
        {
            OwnerProfile.KiralikFilo => rng.Next(5, 9),
            OwnerProfile.YasliCift => rng.Next(1, 3),
            OwnerProfile.Taksi => rng.Next(2, 5),
            _ => rng.Next(2, 7)
        };

        // Servis kayıtları
        int records = maint.ServiceRecordCount;
        int age = CurrentYear - v.ModelYear;
        for (int i = 0; i < records; i++)
        {
            int recYear = v.ModelYear + (int)((i + 1) / (float)(records + 1) * age);
            int recKm = (int)(v.TrueOdometer * ((i + 1) / (float)(records + 1)));
            docs.ServiceHistory.Add(new ServiceRecord(
                recYear, recKm / 1000 * 1000,
                Rng.Pick(rng, ["Periyodik bakım", "Triger seti değişimi", "Debriyaj değişimi",
                               "Fren bakımı", "Yağ ve filtre", "Radyatör onarımı"])));
        }

        // Kilometre oynatılmışsa servis kaydı ELE VERİR — ama her zaman değil (%65)
        bool kmTampered = v.Tampers.Any(t => t.TamperId == "km_oynatma");
        if (kmTampered && docs.ServiceHistory.Count > 0 && !Rng.Chance(rng, 0.65f))
        {
            // İzini örtmüş: tüm kayıtları göstergenin altına çek
            var cleaned = docs.ServiceHistory
                .Select(s => s with { Km = Math.Min(s.Km, v.OdometerReading - rng.Next(5_000, 20_000)) })
                .ToList();
            docs.ServiceHistory.Clear();
            docs.ServiceHistory.AddRange(cleaned);
        }

        // Tramer kayıtları — kaporta hasarı varsa bazen kayıtlı, bazen değil
        var bodyDefects = v.AllDefects.Where(d => v.Part(d.PartId).Def.Group == SystemGroup.Kaporta).ToList();
        foreach (var d in bodyDefects)
        {
            if (!Rng.Chance(rng, 0.45f)) continue;   // %55 kayıtsız kaza
            docs.TramerRecords.Add(new TramerEntry(
                v.ModelYear + rng.Next(3, Math.Max(4, age)),
                Cash.RoundTo((decimal)(d.Severity * 40_000f), 1000),
                v.Part(d.PartId).Def.Name));
        }

        // Motor değişmiş mi? (%10 — ruhsatla çelişir)
        if (Rng.Chance(rng, 0.10f))
        {
            docs.RuhsatEngineNumber = $"{(char)('A' + rng.Next(26))}{rng.Next(10, 99)}" +
                                      $"{(char)('A' + rng.Next(26))}{rng.Next(1000, 9999)}";
        }

        docs.HasInspectionReport = Rng.Chance(rng, 0.7f);
        docs.InspectionYear = CurrentYear - rng.Next(0, 3);
    }

    // -----------------------------------------------------------------------
    // SATICI ÜRETİMİ
    // -----------------------------------------------------------------------

    private static Seller GenerateSeller(VehicleInstance v, Random rng)
    {
        var archetype = (SellerArchetype)rng.Next(Enum.GetValues<SellerArchetype>().Length);

        (float know, float honest, float desp, int patience, float flex) = archetype switch
        {
            SellerArchetype.Amator       => (0.25f, 0.85f, 0.40f, 8, 0.12f),
            SellerArchetype.Galerici     => (0.80f, 0.35f, 0.30f, 6, 0.10f),
            SellerArchetype.Duygusal     => (0.60f, 0.80f, 0.20f, 4, 0.06f),
            SellerArchetype.Dolandirici  => (0.95f, 0.08f, 0.55f, 5, 0.18f),
            SellerArchetype.Aceleci      => (0.45f, 0.55f, 0.90f, 3, 0.25f),
            SellerArchetype.Koleksiyoncu => (0.90f, 0.75f, 0.10f, 9, 0.05f),
            _ => (0.5f, 0.5f, 0.5f, 5, 0.10f)
        };

        // Arketip içinde de varyans olmalı — ezberlenmesin
        know   = Math.Clamp(know   + (float)Rng.Gaussian(rng) * 0.10f, 0.05f, 0.99f);
        honest = Math.Clamp(honest + (float)Rng.Gaussian(rng) * 0.12f, 0.02f, 0.98f);
        desp   = Math.Clamp(desp   + (float)Rng.Gaussian(rng) * 0.15f, 0.02f, 0.98f);

        return new Seller
        {
            Name = Rng.Pick(rng, _sellerNames),
            Archetype = archetype,
            Knowledge = know,
            Honesty = honest,
            Desperation = desp,
            PatienceMax = patience,
            PatienceRemaining = patience,
            PriceFlexibility = flex,
        };
    }

    // -----------------------------------------------------------------------
    // FİYAT VE İLAN METNİ
    // -----------------------------------------------------------------------

    /// <summary>
    /// İstenen fiyat, ONARILMIŞ piyasa değerinin bir oranıdır. Bu oran aracın
    /// GÖRÜNEN durumundan gelir — maskeleme görünen durumu yükselttiği için
    /// fiyatı da yükseltir. Oyuncunun ödediği fazlalık tam olarak budur.
    /// </summary>
    private static decimal ComputeAskingPrice(VehicleInstance v, Seller seller, Random rng)
    {
        float visibleCondition = v.Parts.Values
            .Average(p => ScamEngine.PerceivedCondition(v, p, MethodId.Gozle));

        var (lo, hi) = Valuation.RestoredValueBand(v);
        decimal restoredMid = (lo + hi) / 2m;

        // Hurda görünen araç restore değerinin ~%42'sine, temiz görünen ~%92'sine istenir
        decimal ratio = (decimal)(0.30f + visibleCondition / 100f * 0.53f);

        decimal price = restoredMid * ratio;
        price *= seller.Archetype == SellerArchetype.Galerici ? 1.12m : 1.00m;
        price *= (decimal)(1f - seller.Desperation * 0.12f);
        price *= (decimal)Rng.Range(rng, 0.94f, 1.08f);

        return Cash.RoundTo(price, 1000);
    }

    private static string GenerateListingText(VehicleInstance v, Seller seller, Random rng)
    {
        var lines = new List<string>();

        lines.Add(Rng.Pick(rng, [
            "Aracımız yürür durumda, sorunsuz.",
            "Muayenesi yeni yapıldı.",
            "Babadan kalma, temiz kullanılmış.",
            "İlk sahibinden, hatasız.",
            "Yürüyeninde hiçbir sorun yok.",
        ]));

        if (seller.Honesty > 0.7f && v.AllDefects.Any())
            lines.Add("Ufak tefek eksikleri var, fiyata yansıttım.");
        else if (seller.Honesty < 0.3f)
            lines.Add(Rng.Pick(rng, [
                "Değişeni boyası yoktur.",
                "Motor sıfır gibi, yağ yakmaz.",
                "Hiçbir masraf istemez, bin git.",
            ]));

        if (seller.Desperation > 0.6f)
            lines.Add(Rng.Pick(rng, [
                "ACİL İHTİYAÇTAN SATILIK. Ciddi alıcılar arasın.",
                "Bugün alana pazarlık payı var. Acelem var.",
            ]));

        lines.Add(Rng.Pick(rng, [
            "Pazarlık payı vardır.", "Takas olmaz, nakit.",
            "Ciddi alıcılar arasın.", "Görmeden karar vermeyin.",
        ]));

        return string.Join(" ", lines);
    }
}
