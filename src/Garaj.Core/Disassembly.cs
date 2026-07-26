namespace Garaj.Core;

// ---------------------------------------------------------------------------
// SÖKME + CIVATA MEKANİĞİ (blueprint §6.1)
//
// Bu sistem oyunun İKİNCİ direğini hayata geçirir: Geri Alınamazlık.
// "Bir vidayı sıyırdıysan sıyrılmıştır. Undo yok."
//
// Bir parçaya ulaşmak, onu tıkayan parçaları teker teker sökmek demektir
// (bağımlılık grafiği). Her sökme bir cıvata işlemidir ve cıvatanın durumu
// gizlidir — söküme başlayana kadar paslı mı sıkışmış mı bilemezsin.
//
// Yanlış yöntemle zorlarsan cıvata SIYRILIR. Bu kalıcıdır: artık matkapla
// delip helicoil takmak zorundasın (+2 saat, +₺500). İşte geri alınamazlık.
// ---------------------------------------------------------------------------

public enum BoltState
{
    Temiz,     // sorunsuz söker
    Pasli,     // pas sökücü ister, zorlanırsa sıyrılabilir
    Sikismis,  // ısıtma/darbeli ister, normal anahtarla büyük risk
    Siyrik     // sıyrılmış — sadece delip helicoil ile sökülür
}

public enum WrenchApproach
{
    Normal,          // düz anahtar — temiz cıvatada bedava ve hızlı
    PasSokucu,       // pas sökücü sık + bekle — paslıda güvenli
    Isitma,          // ısıtma tabancası — sıkışmışta en iyi
    Darbeli,         // darbeli anahtar — hızlı ama sıkışmışta orta risk
    DrillHelicoil    // matkap + helicoil — sıyrık cıvatanın tek çıkışı
}

public enum TorqueChoice
{
    Dikkatli,   // yavaş, tork tahmini iyi
    Normal,     // sıradan sıkma
    Hizli       // "olsun bitsin" — yüksek risk
}

public sealed record RemoveOutcome(
    bool Removed, bool Stripped, int Minutes, decimal Cost, string Message);

public static class Disassembly
{
    // -----------------------------------------------------------------------
    // BAĞIMLILIK GRAFİĞİ
    // -----------------------------------------------------------------------

    /// <summary>
    /// Hedef parçaya ulaşmak için önce sökülmesi gereken parçalar, doğru sırada.
    /// Post-order: en derindeki engel önce sökülür.
    /// </summary>
    public static List<string> RemovalChain(string targetId)
    {
        var chain = new List<string>();

        void Collect(string id)
        {
            foreach (var req in PartCatalog.Get(id).RequiresRemoved)
            {
                Collect(req);
                if (!chain.Contains(req)) chain.Add(req);
            }
        }

        Collect(targetId);
        return chain;
    }

    // -----------------------------------------------------------------------
    // CIVATA DURUMU (gizli gerçek — bir kez atanır, sabit kalır)
    // -----------------------------------------------------------------------

    public static BoltState BoltsFor(PartInstance p, Random rng)
    {
        if (p.Bolts.HasValue) return p.Bolts.Value;

        // Alt takım, egzoz, fren daha çok pas tutar; motor içi daha korunaklı
        float rustProne = p.Def.Group switch
        {
            SystemGroup.Egzoz or SystemGroup.Suspansiyon or SystemGroup.Tekerlek => 1.45f,
            SystemGroup.Fren or SystemGroup.Sogutma => 1.20f,
            SystemGroup.Kaporta => 1.05f,
            SystemGroup.Motor or SystemGroup.Sanziman => 0.95f,
            _ => 0.70f
        };

        float bad = (1f - p.Condition / 100f) * rustProne;
        double r = rng.NextDouble();

        BoltState s = bad switch
        {
            > 0.85f => r < 0.50 ? BoltState.Sikismis : BoltState.Pasli,
            > 0.55f => r < 0.25 ? BoltState.Sikismis : r < 0.70 ? BoltState.Pasli : BoltState.Temiz,
            > 0.32f => r < 0.45 ? BoltState.Pasli : BoltState.Temiz,
            _       => r < 0.12 ? BoltState.Pasli : BoltState.Temiz
        };

        p.Bolts = s;
        return s;
    }

    public static string BoltName(BoltState s) => s switch
    {
        BoltState.Temiz    => "temiz",
        BoltState.Pasli    => "paslı",
        BoltState.Sikismis => "sıkışmış",
        BoltState.Siyrik   => "sıyrık",
        _ => s.ToString()
    };

    public static string ApproachName(WrenchApproach a) => a switch
    {
        WrenchApproach.Normal        => "Düz anahtarla sök",
        WrenchApproach.PasSokucu     => "Pas sökücü sık, 10 dk bekle, sök",
        WrenchApproach.Isitma        => "Isıtma tabancasıyla ısıt, sök",
        WrenchApproach.Darbeli       => "Darbeli anahtarla zorla",
        WrenchApproach.DrillHelicoil => "Matkapla del, helicoil tak",
        _ => a.ToString()
    };

    /// <summary>Bu cıvata durumunda denenebilecek yöntemler.</summary>
    public static WrenchApproach[] Options(BoltState s) => s switch
    {
        BoltState.Siyrik => [WrenchApproach.DrillHelicoil],
        _ => [WrenchApproach.Normal, WrenchApproach.PasSokucu,
              WrenchApproach.Isitma, WrenchApproach.Darbeli],
    };

    // -----------------------------------------------------------------------
    // RİSK VE EFOR
    // -----------------------------------------------------------------------

    /// <summary>Bu yöntemle bu cıvatayı sıyırma olasılığı.</summary>
    public static float StripRisk(BoltState b, WrenchApproach a) => (b, a) switch
    {
        (BoltState.Temiz, _) => 0f,

        (BoltState.Pasli, WrenchApproach.Normal)    => 0.22f,
        (BoltState.Pasli, WrenchApproach.PasSokucu) => 0.02f,
        (BoltState.Pasli, WrenchApproach.Isitma)    => 0.05f,
        (BoltState.Pasli, WrenchApproach.Darbeli)   => 0.09f,

        (BoltState.Sikismis, WrenchApproach.Normal)    => 0.58f,
        (BoltState.Sikismis, WrenchApproach.PasSokucu) => 0.30f,
        (BoltState.Sikismis, WrenchApproach.Isitma)    => 0.12f,
        (BoltState.Sikismis, WrenchApproach.Darbeli)   => 0.20f,

        _ => 0.30f
    };

    /// <summary>Yöntemin ek süresi (dk) ve parasal maliyeti.</summary>
    public static (int Minutes, decimal Cost) Effort(WrenchApproach a) => a switch
    {
        WrenchApproach.Normal        => (0, 0m),
        WrenchApproach.PasSokucu     => (12, 40m),
        WrenchApproach.Isitma        => (8, 0m),
        WrenchApproach.Darbeli       => (4, 0m),
        WrenchApproach.DrillHelicoil => (120, 500m),
        _ => (5, 0m)
    };

    // -----------------------------------------------------------------------
    // BİR PARÇAYI SÖK
    // -----------------------------------------------------------------------

    public static RemoveOutcome TryRemove(PartInstance p, WrenchApproach a, Random rng)
    {
        var (m, c) = Effort(a);
        m += (int)(p.Def.LaborHours * 60 * 0.4f);   // sökme, işçiliğin ~%40'ı

        if (a == WrenchApproach.DrillHelicoil)
        {
            p.Bolts = BoltState.Temiz;   // helicoil ile diş yenilendi
            return new(true, false, m, c,
                $"{p.Def.Name}: sıyrık cıvatayı deldin, helicoil taktın, söktün. (+2 saat, +₺500)");
        }

        var state = p.Bolts ?? BoltState.Temiz;

        if (state == BoltState.Siyrik)
            return new(false, true, 0, 0m,
                $"{p.Def.Name}: bu cıvata sıyrık. Ancak matkapla delip helicoil takarak sökebilirsin.");

        if (Rng.Chance(rng, StripRisk(state, a)))
        {
            p.Bolts = BoltState.Siyrik;
            return new(false, true, m, c,
                $"{p.Def.Name}: cıvata SIYRILDI. Zorladın, diş gitti. Artık delip helicoil gerekiyor.");
        }

        p.Bolts = BoltState.Temiz;   // söküldü; yerine takılınca temiz diş olacak
        return new(true, false, m, c, RemoveMessage(p, a, state));
    }

    private static string RemoveMessage(PartInstance p, WrenchApproach a, BoltState was) => (was, a) switch
    {
        (BoltState.Temiz, _) => $"{p.Def.Name} sorunsuz söküldü.",
        (BoltState.Pasli, WrenchApproach.PasSokucu) => $"{p.Def.Name}: pas sökücü işe yaradı, cıvata döndü.",
        (BoltState.Sikismis, WrenchApproach.Isitma) => $"{p.Def.Name}: ısıtınca cıvata gevşedi, söküldü.",
        _ => $"{p.Def.Name} söküldü — cıvata zorladı ama kurtuldu.",
    };

    // -----------------------------------------------------------------------
    // MONTAJ + TORK
    // -----------------------------------------------------------------------

    /// <summary>
    /// Yanlış tork geri alınamaz değil ama gizli bir zaman bombası bırakır:
    /// az sıkma gevşer, fazla sıkma diş/parça zorlar. Tork anahtarı bu riski
    /// tamamen kaldırır — ekipmanın somut değeri budur.
    /// </summary>
    public static (string Message, bool CreatedFlaw) Torque(
        PartInstance p, TorqueChoice choice, bool hasTorqueWrench, Random rng)
    {
        if (hasTorqueWrench)
            return ($"{p.Def.Name}: tork anahtarıyla tam değerinde sıkıldı.", false);

        float flawChance = choice switch
        {
            TorqueChoice.Dikkatli => 0.12f,
            TorqueChoice.Normal   => 0.28f,
            TorqueChoice.Hizli    => 0.48f,
            _ => 0.30f
        };

        if (!Rng.Chance(rng, flawChance))
            return ($"{p.Def.Name}: tork göz kararı tuttu.", false);

        bool over = Rng.Chance(rng, 0.5f);
        p.Defects.Add(new Defect
        {
            Id = $"montaj_{p.DefId}_{rng.Next(100000)}",
            PartId = p.DefId,
            Type = over ? DefectType.Catlak : DefectType.Bosluk,
            Severity = 0.4f,
            Description = over
                ? "montajda fazla sıkılmış, diş/parça zorlanmış."
                : "montajda gevşek kalmış, bağlantı boşluk yapıyor.",
            RevealedBy = [MethodId.TestSurusuUzun, MethodId.Lift],
            ExtraRepairCost = 700m,
            SurfacesAfterKm = rng.Next(300, 1000),
        });

        return ($"{p.Def.Name}: tork tutmadı ama şimdilik belli değil. " +
                "Yolda ortaya çıkabilir.", true);
    }
}
