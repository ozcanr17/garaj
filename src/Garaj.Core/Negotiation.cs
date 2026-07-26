namespace Garaj.Core;

// ---------------------------------------------------------------------------
// KOZ TABANLI PAZARLIK
//
// Pazarlık "bir rakam söyle" olmamalı. Teşhis yaptıysan elinde SOMUT bir şey
// vardır ve onu masaya koyabilmelisin. Bu, teşhis sistemini pazarlığa bağlar:
// inceleme yapmanın karşılığı sadece "bilgi" değil, PAZARLIK GÜCÜ olur.
//
// Kozun ağırlığı üç şeyden gelir:
//   1. Onarım maliyeti — somut para
//   2. Satıcının o konudaki geçmiş duruşu (kabul mü etti, inkâr mı)
//   3. Kanıt gücü (gördüğün kusur > belge çelişkisi > sezgi)
//
// En ağır koz: satıcının İNKÂR ETTİĞİ ama senin KANITLADIĞIN şey.
// ---------------------------------------------------------------------------

public enum LeverageKind
{
    /// <summary>Teşhisle bulduğun somut kusur.</summary>
    Kusur,
    /// <summary>Belgeler arası çelişki.</summary>
    Celiski,
    /// <summary>Yakaladığın maskeleme.</summary>
    Maskeleme,
    /// <summary>Satıcıyı yalan söylerken yakalamış olman.</summary>
    KanitliYalan,
    /// <summary>Satıcının aceleciliği / aracın uzun süredir satılamaması.</summary>
    PiyasaBaskisi
}

public sealed record Leverage(
    string Id,
    LeverageKind Kind,
    string Text,
    decimal MonetaryWeight,
    bool AlreadyUsed = false
)
{
    public string KindName => Kind switch
    {
        LeverageKind.Kusur         => "kusur",
        LeverageKind.Celiski       => "belge çelişkisi",
        LeverageKind.Maskeleme     => "maskeleme",
        LeverageKind.KanitliYalan  => "kanıtlı yalan",
        LeverageKind.PiyasaBaskisi => "piyasa baskısı",
        _ => Kind.ToString()
    };
}

public sealed record PressResult(
    string SellerLine,
    decimal ConcessionGained,
    bool Backfired,
    bool SellerWalkedAway,
    int PatienceCost
);

public static class NegotiationEngine
{
    // -----------------------------------------------------------------------
    // ELİNDEKİ KOZLAR
    // -----------------------------------------------------------------------

    public static List<Leverage> Available(VehicleInstance v, Seller s, PlayerKnowledge k)
    {
        var list = new List<Leverage>();

        // 1. Keşfedilmiş kusurlar — en somut koz
        foreach (var d in v.AllDefects.Where(d => k.DiscoveredDefects.Contains(d.Id)))
        {
            var part = v.Part(d.PartId);
            list.Add(new Leverage(
                $"kusur_{d.Id}",
                LeverageKind.Kusur,
                $"{part.Def.Name}: {d.Description}",
                d.ExtraRepairCost + part.Def.PartCost * 0.6m));
        }

        // 2. Belge çelişkileri
        foreach (var obs in k.Observations.Where(o => o.Kind == ObservationKind.Contradiction))
        {
            // "ÇELİŞKİ — ..." önekini at, cümleyi kısalt
            string text = obs.Text.Replace("ÇELİŞKİ — ", "");
            list.Add(new Leverage(
                $"celiski_{Math.Abs(obs.Text.GetHashCode())}",
                LeverageKind.Celiski,
                text,
                12_000m));
        }

        // 3. Yakalanmış maskelemeler
        foreach (var obs in k.Observations.Where(o =>
                     o.Kind == ObservationKind.Finding && o.Text.StartsWith("YAKALANDI")))
        {
            list.Add(new Leverage(
                $"maske_{Math.Abs(obs.Text.GetHashCode())}",
                LeverageKind.Maskeleme,
                obs.Text.Replace("YAKALANDI — ", ""),
                18_000m));
        }

        // 4. Kanıtlanmış yalan — en ağır koz
        if (s.ProvenLiar)
        {
            list.Add(new Leverage(
                "kanitli_yalan",
                LeverageKind.KanitliYalan,
                $"{s.Name} senin gördüğün bir kusuru yüzüne karşı inkâr etti.",
                25_000m));
        }

        // 5. Piyasa baskısı — satıcı acele ediyorsa oyuncu bunu okuyabilmeli
        if (s.Desperation > 0.55f)
        {
            list.Add(new Leverage(
                "acele",
                LeverageKind.PiyasaBaskisi,
                "Satıcının acelesi var ve bunu saklayamıyor.",
                8_000m));
        }

        return list;
    }

    // -----------------------------------------------------------------------
    // KOZU MASAYA KOY
    // -----------------------------------------------------------------------

    public static PressResult Press(
        VehicleInstance v, Seller s, Leverage lev, Random rng)
    {
        // Taviz, kozun parasal ağırlığının bir oranı. Satıcı dürüstse daha kolay
        // kabul eder; dolandırıcı direnir ama kanıtlı yalan onu da yıkar.
        float baseRate = lev.Kind switch
        {
            LeverageKind.KanitliYalan  => 0.70f,
            LeverageKind.Maskeleme     => 0.62f,
            LeverageKind.Celiski       => 0.58f,
            LeverageKind.Kusur         => 0.55f,
            LeverageKind.PiyasaBaskisi => 0.38f,
            _ => 0.35f
        };

        // Arketip direnci
        float resistance = s.Archetype switch
        {
            SellerArchetype.Dolandirici  => 0.45f,
            SellerArchetype.Galerici     => 0.35f,
            SellerArchetype.Koleksiyoncu => 0.40f,
            SellerArchetype.Duygusal     => 0.30f,
            SellerArchetype.Amator       => 0.15f,
            SellerArchetype.Aceleci      => 0.10f,
            _ => 0.25f
        };

        // Kanıtlı yalan direnci kırar — inkâr edecek yüzü kalmamıştır
        if (lev.Kind == LeverageKind.KanitliYalan) resistance *= 0.35f;

        float effective = baseRate * (1f - resistance) * Rng.Range(rng, 0.80f, 1.20f);
        decimal concession = Cash.RoundTo(lev.MonetaryWeight * (decimal)effective, 100);

        // Taban: geçerli bir koz masaya konduysa hiçbir zaman "boşuna" hissettirmemeli
        if (concession > 0m) concession = Math.Max(concession, 800m);

        // --- Geri tepme: duygusal satıcıyı fazla sıkıştırmak ---
        bool backfired = false;
        bool walked = false;
        int patienceCost = 1;

        if (s.Archetype == SellerArchetype.Duygusal && s.PressedCount >= 2 && Rng.Chance(rng, 0.35f))
        {
            backfired = true;
            concession = 0m;
            patienceCost = 2;
        }
        else if (s.Archetype == SellerArchetype.Aceleci && s.PressedCount >= 3 && Rng.Chance(rng, 0.30f))
        {
            backfired = true;
            concession = 0m;
            patienceCost = 2;
        }

        // Sabır tükendiyse satıcı çekip gidebilir
        if (s.PatienceRemaining - patienceCost <= 0 && Rng.Chance(rng, 0.40f))
        {
            walked = true;
            s.WalkedAway = true;
        }

        s.PressedCount++;
        s.PatienceRemaining = Math.Max(0, s.PatienceRemaining - patienceCost);
        s.ExtraConcession += concession;

        string line = walked
            ? WalkLine(s.Archetype)
            : backfired
                ? BackfireLine(s.Archetype)
                : ConcedeLine(s.Archetype, lev, concession, rng);

        return new PressResult(line, concession, backfired, walked, patienceCost);
    }

    // -----------------------------------------------------------------------
    // TEPKİLER
    // -----------------------------------------------------------------------

    private static string ConcedeLine(
        SellerArchetype a, Leverage lev, decimal concession, Random rng)
    {
        bool big = concession > 8_000m;

        string[] pool = (a, big) switch
        {
            (SellerArchetype.Amator, true) =>
            [
                "\"Ya... haklısın. Ben bunu bilmiyordum valla. Düşeyim o zaman.\"",
                "\"Doğru söylüyorsun. Ben o kadar anlamam, sen bilirsin. İndiririm.\"",
            ],
            (SellerArchetype.Amator, false) =>
            [
                "\"Hmm. Peki, biraz düşerim.\"",
                "\"Tamam, onu hesaba katalım o zaman.\"",
            ],
            (SellerArchetype.Galerici, true) =>
            [
                "\"Tamam. Görüyorum ki işi biliyorsunuz. Rakamı revize edelim.\"",
                "\"Haklısınız. Ben de sizinle uğraşmak istemem, düşüyorum.\"",
            ],
            (SellerArchetype.Galerici, false) =>
            [
                "\"Onu zaten fiyata koymuştuk ama biraz daha esneyebilirim.\"",
                "\"Ufak bir jest yapayım, olsun.\"",
            ],
            (SellerArchetype.Duygusal, true) =>
            [
                "\"...Peki. Sen bu arabaya iyi bakarsın herhalde. İndiririm.\"",
                "\"Canımı sıktın ama haklısın. Tamam.\"",
            ],
            (SellerArchetype.Duygusal, false) =>
            [
                "\"Az bir şey düşerim, o kadar.\"",
                "\"Tamam ama fazla üstüme gelme.\"",
            ],
            (SellerArchetype.Dolandirici, true) =>
            [
                "\"...Tamam abi, tamam. Sen kazandın. Düşüyorum.\"",
                "\"Hah. Sen bu işi biliyormuşsun. Peki, konuşalım.\"",
            ],
            (SellerArchetype.Dolandirici, false) =>
            [
                "\"Ha o mu? Onun için çok az düşerim, o kadar iş değil.\"",
                "\"Tamam da abartma, iki kuruşluk şey.\"",
            ],
            (SellerArchetype.Aceleci, true) =>
            [
                "\"Tamam tamam! Düşüyorum, hadi bitirelim şu işi.\"",
                "\"Ne diyorsan o. Ben bugün bu arabayı satmak istiyorum.\"",
            ],
            (SellerArchetype.Aceleci, false) =>
            [
                "\"Peki, biraz kırarım. Hadi karar ver artık.\"",
            ],
            (SellerArchetype.Koleksiyoncu, true) =>
            [
                "\"Doğru tespit. Bunu bilen biriyle pazarlık etmek zevkli. İndiriyorum.\"",
                "\"Kabul ediyorum. Rakamı ona göre düzeltelim.\"",
            ],
            (SellerArchetype.Koleksiyoncu, false) =>
            [
                "\"Küçük bir kalem. Yine de sayayım.\"",
            ],
            _ => ["\"Peki, biraz düşerim.\""],
        };

        return Rng.Pick(rng, pool) + $"  (−{concession:N0}₺)";
    }

    private static string BackfireLine(SellerArchetype a) => a switch
    {
        SellerArchetype.Duygusal =>
            "\"Yeter ama! Arabamı kötüleyip duruyorsun. Ben bunu böyle satmam.\"  " +
            "(sinirlendi, taviz yok)",
        SellerArchetype.Aceleci =>
            "\"Ya bak, sen alacak mısın almayacak mısın? Bir sürü kusur sayıyorsun.\"  " +
            "(sabrı taştı, taviz yok)",
        _ =>
            "\"Bu kadarı da fazla artık.\"  (taviz yok)",
    };

    private static string WalkLine(SellerArchetype a) => a switch
    {
        SellerArchetype.Duygusal =>
            "\"Bu arabayı sana satmayacağım. Hadi eyvallah.\"  — Satıcı gitti.",
        SellerArchetype.Koleksiyoncu =>
            "\"Sanırım biz anlaşamayacağız. İyi günler.\"  — Satıcı gitti.",
        _ =>
            "\"Yeter. Başkasına satarım ben bunu.\"  — Satıcı gitti.",
    };
}
