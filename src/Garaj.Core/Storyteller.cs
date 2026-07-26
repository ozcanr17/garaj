namespace Garaj.Core;

// ---------------------------------------------------------------------------
// STORYTELLER (blueprint §8.2 + §4.1)
//
// RimWorld'ün anlatıcısı gibi: durum üretir, olay değil. Ama olaylar da lazım.
// Bu sistem, oyuncunun DURUMUNA göre ağırlıklandırılmış olaylar üretir:
//   - Zengin oyuncuya vergi/hırsızlık daha çok gelir (ironi).
//   - İtibarlı oyuncuya koleksiyoncu/gazeteci gelir.
//   - Zorlanan oyuncuya nefes aldıran olay gelir (§8.2 dengesi).
//   - Aynı kategori üst üste gelmez.
//
// Olaylar SOMUT etkilidir (para, itibar, zaman, parça fiyatı, ekipman) —
// sadece dekor değil. Bazıları RimWorld tarzı SEÇİM sunar.
// ---------------------------------------------------------------------------

public enum EventCategory { Dukkan, Musteri, Piyasa, Hikaye }

public sealed class StoryContext
{
    public required PlayerState Player { get; init; }
    public required Random Rng { get; init; }
    public string Result { get; set; } = "";
}

public sealed record EventChoice(string Label, Func<StoryContext, string> Apply);

public sealed record GameEvent(
    string Id,
    string Title,
    EventCategory Category,
    string Body,
    Func<PlayerState, float> Weight,   // 0 = uygun değil
    IReadOnlyList<EventChoice> Choices
);

public sealed class Storyteller
{
    private long _lastEventStamp = long.MinValue;
    private EventCategory? _lastCategory;

    /// <summary>Olaylar arası en az bu kadar oyun-içi dakika geçmeli.</summary>
    private const long GapMinutes = 150;

    public int FiredCount { get; private set; }

    /// <summary>
    /// Ana menüye her dönüşte çağrılır. Yeterli aktivite biriktiyse ve zar
    /// tutarsa bir olay döndürür; yoksa null.
    /// </summary>
    public GameEvent? MaybeFire(PlayerState p, Random rng)
    {
        long now = p.TotalMinutes;

        // İlk çağrıda saati kur — oyun başında, hiçbir şey yapmadan olay çıkmasın
        if (_lastEventStamp == long.MinValue) { _lastEventStamp = now; return null; }

        if (now - _lastEventStamp < GapMinutes) return null;

        // Pencere açık. Çoğu zaman olay çıkar; bazen sessiz geçer.
        if (!Rng.Chance(rng, 0.72f)) { _lastEventStamp = now; return null; }

        var candidates = Catalog
            .Select(e => (Event: e, W: e.Weight(p) * CategoryBias(e.Category, p)))
            .Where(x => x.W > 0f && x.Event.Category != _lastCategory)
            .ToList();

        if (candidates.Count == 0) { _lastEventStamp = now; return null; }

        var chosen = WeightedPick(candidates, rng);
        _lastEventStamp = now;
        _lastCategory = chosen.Category;
        FiredCount++;
        return chosen;
    }

    /// <summary>Nefes aldırma ve ironi: durum kategoriyi eğer.</summary>
    private static float CategoryBias(EventCategory c, PlayerState p)
    {
        bool struggling = p.Money < 25_000m;
        bool wealthy = p.Money > 120_000m;

        return c switch
        {
            // Zorlanınca olumlu ağırlıklı kategoriler öne çıkar
            EventCategory.Musteri => struggling ? 1.6f : 1.0f,
            EventCategory.Hikaye  => struggling ? 1.4f : 1.0f,
            // Zenginleşince dükkan belaları (vergi, hırsızlık) artar
            EventCategory.Dukkan  => wealthy ? 1.5f : 1.0f,
            EventCategory.Piyasa  => 1.0f,
            _ => 1.0f
        };
    }

    private static GameEvent WeightedPick(List<(GameEvent Event, float W)> items, Random rng)
    {
        float total = items.Sum(x => x.W);
        float r = (float)rng.NextDouble() * total;
        foreach (var (ev, w) in items)
        {
            r -= w;
            if (r <= 0f) return ev;
        }
        return items[^1].Event;
    }

    // =======================================================================
    // OLAY KATALOĞU
    // =======================================================================

    private static EventChoice Ok(Func<StoryContext, string> apply) => new("Tamam", apply);

    private static decimal Round100(decimal v) => Cash.RoundTo(v, 100);

    public static IReadOnlyList<GameEvent> Catalog { get; } =
    [
        // ------------------------------- DÜKKAN -------------------------------

        new("vergi_denetimi", "Vergi Denetimi", EventCategory.Dukkan,
            "Kapıda bir vergi müfettişi. \"Defterlere bir bakalım usta.\" Kazancın " +
            "arttıkça gözden kaçman zorlaşıyor.",
            p => p.Money > 40_000m ? 1.0f : 0.3f,
            [
                new("Defterleri düzgün tut, vergini öde", ctx =>
                {
                    decimal tax = Round100(ctx.Player.Money * 0.06m);
                    ctx.Player.Money -= tax;
                    return $"Vergini temiz ödedin: −{tax:N0}₺. Müfettiş memnun ayrıldı.";
                }),
                new("Bir kısmını gizle (riskli)", ctx =>
                {
                    if (Rng.Chance(ctx.Rng, 0.6f))
                        return "Bu sefer yakayı sıyırdın. Müfettiş bir şey fark etmedi.";
                    decimal fine = Round100(ctx.Player.Money * 0.14m);
                    ctx.Player.Money -= fine;
                    ctx.Player.Reputation -= 5f;
                    return $"Yakalandın. Ceza −{fine:N0}₺ ve itibarın sarsıldı (−5).";
                }),
            ]),

        new("ekipman_arizasi", "Ekipman Arızası", EventCategory.Dukkan,
            "Aletlerden biri elinde kaldı. Tamir ettirmezsen kullanamazsın.",
            p => p.Equipment.Count > 0 ? 1.0f : 0f,
            [
                new("Tamir ettir", ctx =>
                {
                    var id = ctx.Player.Equipment.OrderBy(_ => ctx.Rng.Next()).First();
                    var eq = EquipmentCatalog.Get(id);
                    decimal cost = Round100(eq.Cost * 0.35m);
                    ctx.Player.Money -= cost;
                    return $"{eq.Name} tamir edildi: −{cost:N0}₺. Yine çalışıyor.";
                }),
                new("Şimdilik idare et (aleti kaybet)", ctx =>
                {
                    var id = ctx.Player.Equipment.OrderBy(_ => ctx.Rng.Next()).First();
                    var eq = EquipmentCatalog.Get(id);
                    ctx.Player.Equipment.Remove(id);
                    return $"{eq.Name} bozuk kaldı, rafa kaldırdın. Yeniden almadan kullanamazsın.";
                }),
            ]),

        new("hirsizlik", "Hırsızlık", EventCategory.Dukkan,
            "Gece dükkâna girmişler. Raftan birkaç parça ve kasadan biraz nakit gitmiş. " +
            "Güvenlik önlemi almamıştın.",
            p => p.Money > 15_000m ? 1.0f : 0.4f,
            [
                Ok(ctx =>
                {
                    decimal loss = Round100(Math.Min(ctx.Player.Money * 0.06m, 9_000m));
                    ctx.Player.Money -= loss;
                    return $"−{loss:N0}₺ değerinde kayıp. Bir güvenlik sistemi düşünme vakti.";
                }),
            ]),

        new("elektrik_kesintisi", "Elektrik Kesintisi", EventCategory.Dukkan,
            "Bütün mahalle karardı. Yarım iş öylece kaldı, jeneratör de yok.",
            p => 0.7f,
            [
                Ok(ctx =>
                {
                    ctx.Player.AdvanceMinutes(240);
                    return "Yarım gün boşa gitti. Elektrik gelince kaldığın yerden devam ettin.";
                }),
            ]),

        new("belediye_sikayeti", "Belediye Şikâyeti", EventCategory.Dukkan,
            "Komşu, gürültü ve boya kokusundan şikâyetçi olmuş. Zabıta bir uyarı bıraktı.",
            p => 0.6f,
            [
                new("Cezayı öde, sesini kes", ctx =>
                {
                    ctx.Player.Money -= 2_500m;
                    return "−2.500₺ ceza. En azından mesele kapandı.";
                }),
                new("Aldırma", ctx =>
                {
                    ctx.Player.Reputation -= 3f;
                    return "Komşuyla aran bozuldu (itibar −3). Mahalle esnafı dedikodunu yapıyor.";
                }),
            ]),

        // ------------------------------ MÜŞTERİ ------------------------------

        new("forumda_ovgu", "Forumda Övgü", EventCategory.Musteri,
            "Geçen ay iş yaptığın bir müşteri seni yerel bir forumda övmüş. " +
            "\"Dürüst usta, hak ediyor\" demiş. Telefonun çalmaya başladı.",
            p => p.CarsSold > 0 ? 1.2f : 0.4f,
            [
                Ok(ctx => { ctx.Player.Reputation += 6f; return "İtibarın yükseldi (+6). Güzel bir his."; }),
            ]),

        new("musteri_geri_geldi", "Sattığın Araç Bozuldu", EventCategory.Musteri,
            "Kapıda öfkeli bir müşteri. Sattığın araç iki hafta sonra yolda kalmış. " +
            "\"Sen bunu biliyordun usta.\" Belki de biliyordun.",
            p => p.RiskySales > 0 ? 2.0f : 0f,
            [
                new("Ücretsiz onar, gönlünü al", ctx =>
                {
                    ctx.Player.Money -= 6_000m;
                    ctx.Player.Reputation += 2f;
                    ctx.Player.RiskySales--;
                    return "−6.000₺ masraf ama müşteri yumuşadı (itibar +2). Doğru olanı yaptın.";
                }),
                new("\"Garantim yok\" de, kapıyı göster", ctx =>
                {
                    ctx.Player.Reputation -= 8f;
                    ctx.Player.RiskySales--;
                    return "Müşteri küplere bindi. Seni her yerde kötüleyecek (itibar −8).";
                }),
            ]),

        new("gazeteci", "Gazeteci İlgisi", EventCategory.Musteri,
            "Yerel bir gazeteci restorasyonlarını duymuş, bir röportaj istiyor. " +
            "Yarım gününü alır ama görünürlük getirir.",
            p => p.Reputation > 35f ? 1.0f : 0.2f,
            [
                new("Röportajı kabul et", ctx =>
                {
                    ctx.Player.AdvanceMinutes(300);
                    ctx.Player.Reputation += 8f;
                    return "Röportaj çıktı, adın duyuldu (itibar +8). Yarım gün gitti ama değdi.";
                }),
                new("Vaktim yok de", _ => "Kibarca geçiştirdin. İş başa düşer."),
            ]),

        new("koleksiyoncu_is", "Koleksiyoncu Teklifi", EventCategory.Musteri,
            "Şık giyimli bir bey. \"Sizin işçiliğinizi övdüler. Elimde bir proje var, " +
            "avans veriyorum, acele yok.\" İtibarın kapı açıyor.",
            p => p.Reputation > 45f ? 1.2f : 0f,
            [
                new("Avansı al, işi üstlen", ctx =>
                {
                    ctx.Player.Money += 8_000m;
                    ctx.Player.Reputation += 4f;
                    ctx.Player.AdvanceMinutes(180);
                    return "+8.000₺ avans, itibar +4. Koleksiyoncu çevresine girdin.";
                }),
                new("Bu aralar yoğunum, reddet", _ => "Nazikçe reddettin. Belki başka zaman."),
            ]),

        // ------------------------------ PİYASA -------------------------------

        new("tedarikci_kapandi", "Tedarikçi Kapandı", EventCategory.Piyasa,
            "Uzun süredir çalıştığın parçacı dükkânı kapatmış. Parçalar bir süre " +
            "daha pahalıya, daha zor bulunacak.",
            p => 0.9f,
            [
                Ok(ctx =>
                {
                    ctx.Player.PartsCostMultiplier = 1.30f;
                    ctx.Player.PartsCostUntilDay = ctx.Player.Day + ctx.Rng.Next(4, 9);
                    return "Parça fiyatları bir süre %30 zamlı. Alternatif tedarikçi bulana kadar dişini sık.";
                }),
            ]),

        new("parca_kampanyasi", "Parça Kampanyası", EventCategory.Piyasa,
            "Bir toptancı stok eritiyor. Bir süreliğine parçalar ucuz.",
            p => 0.8f,
            [
                Ok(ctx =>
                {
                    ctx.Player.PartsCostMultiplier = 0.82f;
                    ctx.Player.PartsCostUntilDay = ctx.Player.Day + ctx.Rng.Next(3, 7);
                    return "Parça fiyatları bir süre %18 ucuz. Bekleyen işleri şimdi hallet.";
                }),
            ]),

        new("yakit_zammi", "Yakıt Zammı", EventCategory.Piyasa,
            "Gece yarısı zam geldi. Ertesi gün herkes ekonomik araç konuşuyor; " +
            "büyük motorlu araçlara talep düştü.",
            p => 0.7f,
            [
                Ok(_ => "Piyasa dalgalandı. Hangi aracın tutacağını okumak artık daha önemli."),
            ]),

        // ------------------------------ HİKÂYE -------------------------------

        new("baba_defteri", "Babanın Defterinden Bir Sayfa", EventCategory.Hikaye,
            "Torna tezgâhının altında, yağ lekeli eski bir sayfa. Babanın el yazısı: " +
            "\"İyi usta parçayı değil, sesi dinler. Sesi öğren, gerisi gelir.\"",
            p => 0.8f,
            [
                Ok(ctx => { ctx.Player.Reputation += 1f; return "Bir şeyler öğrendin gibi (itibar +1). Defterin gerisi nerede?"; }),
            ]),

        new("mahmut_hasta", "Mahmut Usta Hastalandı", EventCategory.Hikaye,
            "Babanın eski ortağı Mahmut Usta hastanede. \"Merak etme, ben iyiyim\" diyor " +
            "ama sesi zayıf. Bir süre ona danışamayacaksın.",
            p => p.MahmutIll ? 0f : 0.6f,
            [
                Ok(ctx => { ctx.Player.MahmutIll = true; return "Mahmut Usta bir süre yok. Kendi kararlarına daha çok güveneceksin."; }),
            ]),

        new("rakip_riza", "Rakip Galerici Rıza", EventCategory.Hikaye,
            "Rakip galerici Rıza dükkânın önünden geçerken laf attı: " +
            "\"Babanın enkazını hâlâ deviremedin mi? Bırak bu işi evladım.\"",
            p => 0.5f,
            [
                Ok(_ => "İçin sızladı ama belli etmedin. Bu işi ona da kanıtlayacaksın."),
            ]),
    ];
}
