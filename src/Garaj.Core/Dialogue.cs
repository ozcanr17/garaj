namespace Garaj.Core;

// ---------------------------------------------------------------------------
// SATICI DİYALOĞU
//
// Eski sistem 5 satırlık genel bir yalan dizisinden rastgele seçiyordu; hangi
// parçayı sorarsan sor aynı cevap geliyordu. Bu, oyunun poker katmanını öldürür.
//
// Yeni sistem üç şeye birden bakar:
//   1. Satıcı bu grupta bir kusur BİLİYOR mu?
//   2. Söyleyecek mi? (dürüstlük + arketip)
//   3. Oyuncu bunu ZATEN BULDU mu?  ← en önemlisi
//
// Üçüncüsü kritik: oyuncu kusuru zaten keşfettiyse ve satıcı hâlâ inkâr ediyorsa,
// bu artık bir yalan değil, KANITLANMIŞ bir yalandır. Oyuncu o andan itibaren
// bu satıcının söylediği hiçbir şeye güvenemez. Pazarlık da buradan beslenir.
// ---------------------------------------------------------------------------

public enum SellerStance
{
    /// <summary>Gerçekten bilmiyor.</summary>
    Bilmiyor,
    /// <summary>Grup gerçekten sağlam ve satıcı bunu doğru söylüyor.</summary>
    Guvenli,
    /// <summary>Biliyor ve dürüstçe söylüyor.</summary>
    DogruSoyluyor,
    /// <summary>Biliyor, kabul ediyor ama önemsizleştiriyor.</summary>
    Kucumsuyor,
    /// <summary>Biliyor ve düpedüz inkâr ediyor.</summary>
    Inkar,
    /// <summary>Oyuncu zaten bulmuş; satıcı geri adım atıyor.</summary>
    Yakalandi,
    /// <summary>Oyuncu zaten bulmuş ama satıcı hâlâ inkâr ediyor — kanıtlanmış yalan.</summary>
    IsrarliYalan
}

public sealed record DialogueResult(
    string Answer,
    SellerStance Stance,
    Observation? Tell,
    bool ProvenLiar
);

public static class SellerDialogue
{
    public static DialogueResult Respond(
        Seller s, SystemGroup group, VehicleInstance v, PlayerKnowledge k, Random rng)
    {
        string groupName = PartCatalog.GroupName(group);

        // Satıcının bu grupta bildiği kusurlar
        var known = v.AllDefects
            .Where(d => v.Part(d.PartId).Def.Group == group)
            .Where(d => s.KnownDefectIds.Contains(d.Id))
            .ToList();

        // Oyuncunun bu grupta zaten bulduğu kusurlar
        var discovered = v.AllDefects
            .Where(d => v.Part(d.PartId).Def.Group == group)
            .Where(d => k.DiscoveredDefects.Contains(d.Id))
            .ToList();

        // --- Hiçbir şey bilmiyor ---
        if (known.Count == 0)
        {
            // Grup gerçekten iyiyse dürüst bir güvence verebilir — soru sormak işe yarar
            var parts = v.Parts.Values.Where(p => p.Def.Group == group).ToList();
            float avg = parts.Count > 0 ? parts.Average(p => p.Condition) : 50f;

            var stance = avg > 68f && Rng.Chance(rng, 0.6f) ? SellerStance.Guvenli : SellerStance.Bilmiyor;
            return new DialogueResult(Line(s.Archetype, stance, groupName, null, rng), stance, null, false);
        }

        var defect = Rng.Pick(rng, known);
        string partName = v.Part(defect.PartId).Def.Name;
        bool playerKnows = discovered.Any(d => d.Id == defect.Id);

        // --- Oyuncu bunu zaten buldu ---
        if (playerKnows)
        {
            // Dürüst satıcı kabul eder; dolandırıcı ısrar eder — ve kendini ele verir
            bool doublesDown = !Rng.Chance(rng, s.Honesty + 0.25f);

            if (doublesDown)
            {
                var tell = new Observation(
                    $"{s.Name} senin kendi gözünle gördüğün şeyi inkâr etti. " +
                    "Bu adamın söylediği hiçbir şeye güvenemezsin.",
                    ObservationKind.SellerTell, MethodId.Gozle);

                return new DialogueResult(
                    Line(s.Archetype, SellerStance.IsrarliYalan, groupName, partName, rng),
                    SellerStance.IsrarliYalan, tell, ProvenLiar: true);
            }

            return new DialogueResult(
                Line(s.Archetype, SellerStance.Yakalandi, groupName, partName, rng),
                SellerStance.Yakalandi, null, false);
        }

        // --- Söyleyecek mi? ---
        if (Rng.Chance(rng, s.Honesty))
        {
            string answer = Line(s.Archetype, SellerStance.DogruSoyluyor, groupName, partName, rng)
                            + " " + Capitalize(defect.Description);
            return new DialogueResult(answer, SellerStance.DogruSoyluyor, null, false);
        }

        // --- Yalan: ya küçümser ya düpedüz inkâr eder ---
        var chosen = Rng.Chance(rng, 0.45f) ? SellerStance.Kucumsuyor : SellerStance.Inkar;

        // Yalan söylerken tell sızdırma ihtimali; dolandırıcı iyi oyuncudur
        float tellChance = s.Archetype == SellerArchetype.Dolandirici ? 0.22f : 0.52f;
        Observation? lieTell = Rng.Chance(rng, tellChance)
            ? new Observation($"{s.Name}: {Rng.Pick(rng, _tells)}", ObservationKind.SellerTell, MethodId.Gozle)
            : null;

        return new DialogueResult(Line(s.Archetype, chosen, groupName, partName, rng), chosen, lieTell, false);
    }

    private static string Capitalize(string s)
        => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0], System.Globalization.CultureInfo.GetCultureInfo("tr-TR")) + s[1..];

    // -----------------------------------------------------------------------
    // ARKETİP SESLERİ
    // {0} = sistem grubu adı,  {1} = parça adı
    // -----------------------------------------------------------------------

    private static string Line(
        SellerArchetype a, SellerStance st, string group, string? part, Random rng)
    {
        var pool = Pool(a, st);
        string raw = Rng.Pick(rng, pool);
        return "\"" + raw.Replace("{0}", group).Replace("{1}", part ?? group) + "\"";
    }

    private static string[] Pool(SellerArchetype a, SellerStance st) => (a, st) switch
    {
        // ---------------- AMATÖR: bilmez, dürüsttür ----------------
        (SellerArchetype.Amator, SellerStance.Bilmiyor) =>
        [
            "Valla ben o kadarını bilmiyorum, babamın arabasıydı.",
            "{0} tarafına hiç bakmadım açıkçası. Ustaya sorsan daha iyi.",
            "Ben sadece bakımını yaptırdım, gerisine karışmadım.",
            "Bilmiyorum ki. Bana hiç sıkıntı çıkarmadı bugüne kadar.",
        ],
        (SellerArchetype.Amator, SellerStance.Guvenli) =>
        [
            "{0} tarafından hiç şikâyetim olmadı, onu rahat söylerim.",
            "Orası iyidir bence. En azından ben hiç uğraşmadım.",
        ],
        (SellerArchetype.Amator, SellerStance.DogruSoyluyor) =>
        [
            "Ha evet, {1} için bir uyarı almıştım.",
            "Doğrusu {1} konusunda bir şey söylemişlerdi bana.",
        ],
        (SellerArchetype.Amator, SellerStance.Kucumsuyor) =>
        [
            "{1} biraz yorgun ama iş görüyor işte, ben kullanıyordum.",
            "Ya {1} eskimiş tabii, araba kaç yaşında sonuçta.",
        ],
        (SellerArchetype.Amator, SellerStance.Inkar) =>
        [
            "Yok, {0} tarafında bir sorun görmedim hiç.",
            "{1} iyidir bence, bir şey olsa fark ederdim herhalde.",
        ],
        (SellerArchetype.Amator, SellerStance.Yakalandi) =>
        [
            "Ha... onu diyorsan evet. Ben o kadar ciddi sanmıyordum.",
            "Şey... doğru. Ama bana kimse söylemedi ki bunu.",
        ],
        (SellerArchetype.Amator, SellerStance.IsrarliYalan) =>
        [
            "Yok canım, öyle bir şey yok. Sen yanlış bakmışsındır.",
        ],

        // ---------------- GALERİCİ: profesyonel, pürüzsüz ----------------
        (SellerArchetype.Galerici, SellerStance.Bilmiyor) =>
        [
            "Bu araç bize takas geldi, geçmişini tam bilmiyoruz. Ama ekspertizden geçti.",
            "{0} için elimde kayıt yok. İsterseniz kendi ustanıza baktırın.",
        ],
        (SellerArchetype.Galerici, SellerStance.Guvenli) =>
        [
            "{0} tarafı temiz. Biz aracı içeri alırken kontrol ediyoruz zaten.",
            "Orada iş yok. Olsaydı fiyata yansıtırdık, öyle çalışıyoruz.",
        ],
        (SellerArchetype.Galerici, SellerStance.DogruSoyluyor) =>
        [
            "{1} konusunda dürüst olayım — fiyatta zaten değerlendirdik.",
            "{1} için açık konuşayım, sonra sürpriz olmasın.",
        ],
        (SellerArchetype.Galerici, SellerStance.Kucumsuyor) =>
        [
            "{1} standart bakım kalemi, her araçta çıkar. Masraf sayılmaz.",
            "{1} rutin bir şey. Sanayide yarım saatlik iş, abartmayalım.",
        ],
        (SellerArchetype.Galerici, SellerStance.Inkar) =>
        [
            "{0} tertemiz. Bu araca biz de baktık, bir şey yok.",
            "{1} sıfır gibi. Ben size yanlış bilgi vermem, itibarım var.",
        ],
        (SellerArchetype.Galerici, SellerStance.Yakalandi) =>
        [
            "Ustasınız, gözünüzden kaçmıyor. Tamam, orada küçük bir iş var.",
            "Doğru söylüyorsunuz. Rakamda konuşuruz o zaman.",
        ],
        (SellerArchetype.Galerici, SellerStance.IsrarliYalan) =>
        [
            "Olmaz öyle şey. Bizim ekspertiz raporumuz var, isterseniz gösteririm.",
        ],

        // ---------------- DUYGUSAL: savunmacı, arabaya bağlı ----------------
        (SellerArchetype.Duygusal, SellerStance.Bilmiyor) =>
        [
            "Bilmiyorum ki... Ben bu arabaya hiç sıkıntı çektirmedim.",
            "{0} mi? Hiç aklıma gelmedi bakmak. Hep yolda kaldığım olmadı.",
        ],
        (SellerArchetype.Duygusal, SellerStance.Guvenli) =>
        [
            "{0} sağlamdır, ben bu arabayla Ankara'ya kaç kez gittim geldim.",
            "Orası iyidir, ben bilirim bu arabayı.",
        ],
        (SellerArchetype.Duygusal, SellerStance.DogruSoyluyor) =>
        [
            "{1} için haklısın. Söylemem lazım, bu arabaya yalan söyleyemem.",
            "{1} evet, doğru. Yapacaktım ama satmaya karar verdim.",
        ],
        (SellerArchetype.Duygusal, SellerStance.Kucumsuyor) =>
        [
            "Ya {1} biraz eskimiş tabii, araba benden yaşlı sayılır.",
            "{1} öyle küçük bir şey ki, ben hiç dert etmedim.",
        ],
        (SellerArchetype.Duygusal, SellerStance.Inkar) =>
        [
            "Olmaz öyle şey, ben bu arabaya çocuğum gibi baktım.",
            "{0} tarafında bir şey yok. Olsa ben bilirdim.",
        ],
        (SellerArchetype.Duygusal, SellerStance.Yakalandi) =>
        [
            "...Peki. Evet, var. Ama ben yine de bu arabayı çok sevdim.",
            "Gördün demek. Doğru söylüyorsun, kusura bakma.",
        ],
        (SellerArchetype.Duygusal, SellerStance.IsrarliYalan) =>
        [
            "Hayır! Öyle bir şey yok. Sen arabamı kötülüyorsun şimdi.",
        ],

        // ---------------- DOLANDIRICI: pürüzsüz, ele vermez ----------------
        (SellerArchetype.Dolandirici, SellerStance.Bilmiyor) =>
        [
            "{0} mi? Yok abi, o taraf sağlam. İstersen bak.",
            "Orada bir şey yok. Ben bu arabayı iyi bilirim.",
            "{0} için için rahat olsun. Ben sana yanlış bir şey söylemem.",
            "Ha {0}. Yok, orası dert değil. Sen asıl motora bak.",
            "Bak ben bu işi yıllardır yapıyorum. {0} tarafı temizdir.",
        ],
        (SellerArchetype.Dolandirici, SellerStance.Guvenli) =>
        [
            "{0} tertemiz. Zaten bakarsın anlarsın, gizlisi saklısı yok.",
            "{0} sağlam, orayı hiç kurcalamana gerek yok.",
            "Orası iyi. Sana yalan söyleyecek değilim ya.",
        ],
        (SellerArchetype.Dolandirici, SellerStance.DogruSoyluyor) =>
        [
            "{1} için söyleyeyim de sonra dert olmasın —",
            "Bak {1} konusunda açık olayım, ufak bir şey var:",
        ],
        (SellerArchetype.Dolandirici, SellerStance.Kucumsuyor) =>
        [
            "{1} zaten her araçta olur, o kadarı kusur sayılmaz.",
            "{1} mi? Ha o normal. Bu modellerin hepsinde var, hepsinde.",
        ],
        (SellerArchetype.Dolandirici, SellerStance.Inkar) =>
        [
            "{1} sıfır gibi, geçen ay elden geçti zaten.",
            "Yok yok. O konuda için rahat olsun, sözüm söz.",
            "{0} tarafına ben kendim baktırdım. Temiz çıktı.",
        ],
        (SellerArchetype.Dolandirici, SellerStance.Yakalandi) =>
        [
            "Hmm. Ha evet, onu unutmuşum. Küçük bir şey.",
            "Ha onu mu diyorsun? Yaa, o zaten belliydi. Ben saklamadım ki.",
            "İyi bakmışsın. Tamam, o var. Ama iki kuruşluk iş.",
        ],
        (SellerArchetype.Dolandirici, SellerStance.IsrarliYalan) =>
        [
            "Ne? Yok canım, öyle bir şey yok. Sen yanlış görmüşsün.",
            "Olmaz. Ben bu arabayı bilmez miyim? Sende bir yanlışlık var.",
        ],

        // ---------------- ACELECİ: sabırsız, kestirip atar ----------------
        (SellerArchetype.Aceleci, SellerStance.Bilmiyor) =>
        [
            "Ya bilmiyorum, bak vaktim yok. Alacaksan al.",
            "{0} falan derken akşam olacak. Bilmiyorum işte.",
        ],
        (SellerArchetype.Aceleci, SellerStance.Guvenli) =>
        [
            "{0} iyi, sorun yok. Hadi bakalım, karar verdin mi?",
            "{0} sağlam. Başka sorun var mı, çünkü benim işim var.",
            "Orası temiz. Bak saat kaç oldu.",
        ],
        (SellerArchetype.Aceleci, SellerStance.DogruSoyluyor) =>
        [
            "{1} var evet, söyleyeyim de vakit kaybetmeyelim.",
            "{1} bozuk, açık söylüyorum. Fiyattan düşeriz, hallederiz.",
        ],
        (SellerArchetype.Aceleci, SellerStance.Kucumsuyor) =>
        [
            "{1} iki kuruşluk iş, takma kafana.",
            "{1} mi? Boş ver onu, sen arabaya bak.",
        ],
        (SellerArchetype.Aceleci, SellerStance.Inkar) =>
        [
            "{0}'da bir şey yok. Hadi karar ver artık, başka müşteri var.",
            "Yok. Bak ben akşama kadar burada duramam.",
        ],
        (SellerArchetype.Aceleci, SellerStance.Yakalandi) =>
        [
            "Tamam tamam, var. Biraz indiririm, olur mu? Hadi.",
            "Ya tamam, gördün işte. Ne yapalım, fiyatı konuşalım.",
            "Evet var. Uzatmayalım şimdi, teklifini söyle.",
        ],
        (SellerArchetype.Aceleci, SellerStance.IsrarliYalan) =>
        [
            "Yok dedim ya. Alacaksan al, almayacaksan ben kapatayım.",
        ],

        // ---------------- KOLEKSİYONCU: bilgili, kesin, gururlu ----------------
        (SellerArchetype.Koleksiyoncu, SellerStance.Bilmiyor) =>
        [
            "{0} için elimde belge yok, o yüzden bir şey söylemeyeceğim.",
            "Bilmediğim şeyi söylemem. {0} tarafına hiç girmedim.",
        ],
        (SellerArchetype.Koleksiyoncu, SellerStance.Guvenli) =>
        [
            "{0} sağlam. Kendim kontrol ettim, kayıtları da duruyor.",
            "Orası orijinal ve sağlam. Bu araçta özensiz iş yoktur.",
        ],
        (SellerArchetype.Koleksiyoncu, SellerStance.DogruSoyluyor) =>
        [
            "{1} — evet. Bunu bilerek alırsınız, saklamam.",
            "{1} konusunda açık olayım, ben böyle iş yapmam.",
        ],
        (SellerArchetype.Koleksiyoncu, SellerStance.Kucumsuyor) =>
        [
            "{1} orijinal parça. Yorgun ama değiştirmedim — orijinalliğe önem veririm.",
            "{1} yaşına göre normal. Ben restorasyonda sabırlıyımdır.",
        ],
        (SellerArchetype.Koleksiyoncu, SellerStance.Inkar) =>
        [
            "{0} tarafında sorun yok. Bu araç benim elimde ihmal edilmedi.",
        ],
        (SellerArchetype.Koleksiyoncu, SellerStance.Yakalandi) =>
        [
            "Fark ettiniz demek. Doğru. Anlayan biriyle konuşmak güzel.",
        ],
        (SellerArchetype.Koleksiyoncu, SellerStance.IsrarliYalan) =>
        [
            "Hayır. Ben bu aracı on yıldır tanıyorum, siz yanılıyorsunuz.",
        ],

        _ => ["Bilmiyorum."],
    };

    private static readonly string[] _tells =
    [
        "(konuyu değiştiriyor) \"Neyse, sen şu motora bak, harika değil mi?\"",
        "(fazla açıklıyor) \"...zaten hep öyleydi, hep, hiç değişmedi, gerçekten hiç.\"",
        "(aniden) \"Bin lira da indirebilirim aslında.\"",
        "(acele ettiriyor) \"Başka müşteri yolda, karar versen iyi olur.\"",
        "(o tarafa bakmanı istemiyor) \"Ordan bir şey göremezsin, boşuna eğilme.\"",
        "(soruyu soruyla karşılıyor) \"Ne oldu, bir şey mi gördün?\"",
        "(çok kısa duraksıyor) \"...Yok. Yok, sorun yok.\"",
        "(gözünü kaçırıyor) \"Hı-hı. Yani. Öyle işte.\"",
        "(gereksiz yere gülüyor) \"Ha ha, yok ya, nereden çıkardın onu?\"",
    ];
}
