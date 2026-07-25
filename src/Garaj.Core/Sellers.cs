namespace Garaj.Core;

// ---------------------------------------------------------------------------
// SATICI (blueprint §4.3)
//
// Satıcı bir bilgi filtresidir. Bildiği kadarını, dürüst olduğu kadar söyler.
// Yalan söylediğinde davranışsal ipucu (tell) sızdırır — ama tell'ler ASLA
// %100 güvenilir değildir. Dürüst satıcılar da gergin olabilir.
// ---------------------------------------------------------------------------

public sealed class Seller
{
    public required string Name { get; init; }
    public SellerArchetype Archetype { get; init; }

    /// <summary>0-1: aracı ne kadar biliyor.</summary>
    public float Knowledge { get; init; }

    /// <summary>0-1: bildiğini ne kadar söylüyor.</summary>
    public float Honesty { get; init; }

    /// <summary>0-1: ne kadar acil satmak istiyor.</summary>
    public float Desperation { get; init; }

    /// <summary>Kaç teşhis işlemine izin verir. Bitince kapıyı gösterir.</summary>
    public int PatienceRemaining { get; set; }
    public int PatienceMax { get; init; }

    public float PriceFlexibility { get; init; }

    /// <summary>Satıcının bildiği kusurlar (hepsini bilmez).</summary>
    public HashSet<string> KnownDefectIds { get; } = [];

    /// <summary>Kaç kez pazarlık turu oldu.</summary>
    public int NegotiationRounds { get; set; }

    /// <summary>Oyuncunun teklifleri reddedildikçe satıcı gerginleşir.</summary>
    public bool WalkedAway { get; set; }

    public string ArchetypeName => Archetype switch
    {
        SellerArchetype.Amator       => "Amatör",
        SellerArchetype.Galerici     => "Galerici",
        SellerArchetype.Duygusal     => "Duygusal sahip",
        SellerArchetype.Dolandirici  => "Dolandırıcı",
        SellerArchetype.Aceleci      => "Aceleci",
        SellerArchetype.Koleksiyoncu => "Koleksiyoncu",
        _ => Archetype.ToString()
    };

    /// <summary>
    /// Oyuncu bu satıcıyı, kendi gözüyle gördüğü bir kusuru inkâr ederken yakaladı mı?
    /// Yakaladıysa bu kalıcıdır ve pazarlıkta oyuncunun eline koz verir.
    /// </summary>
    public bool ProvenLiar { get; set; }

    /// <summary>Oyuncunun sorduğu grup sayısı — satıcının sabrını yer.</summary>
    public int QuestionsAsked { get; set; }

    // -----------------------------------------------------------------------
    // SORU-CEVAP → SellerDialogue'a devredildi (bkz. Dialogue.cs)
    // -----------------------------------------------------------------------

    public DialogueResult AskAbout(SystemGroup group, VehicleInstance v, PlayerKnowledge k, Random rng)
    {
        QuestionsAsked++;
        var result = SellerDialogue.Respond(this, group, v, k, rng);
        if (result.ProvenLiar) ProvenLiar = true;
        return result;
    }

    /// <summary>Dürüst satıcı da gergin olabilir — tell'ler güvenilmez olmalı (§4.3).</summary>
    public Observation? RollFalseTell(Random rng)
        => Rng.Chance(rng, 0.12f)
            ? new Observation($"{Name}: {Rng.Pick(rng, _tells)}", ObservationKind.SellerTell, MethodId.Gozle)
            : null;

    // -----------------------------------------------------------------------
    // SABIR
    // -----------------------------------------------------------------------

    public string RefusalLine(DiagnosisMethod m, Random rng) => Archetype switch
    {
        SellerArchetype.Aceleci =>
            $"\"Ya abi bak, {m.Name.ToLowerInvariant()} falan derken akşam olacak. " +
            "Başka müşteri de geliyor, karar ver artık.\"",
        SellerArchetype.Dolandirici =>
            $"\"{m.Name} mi? Yok yok, gerek yok ona. Araba ortada işte, bakıyorsun.\"",
        SellerArchetype.Duygusal =>
            "\"Arabayı parçalayacak mısın kardeşim? Ben bu arabayla evlendim ya.\"",
        _ =>
            $"\"Yeter artık, bu kadar da olmaz. {m.Name} istiyorsan arabayı al öyle yap.\""
    };

    public string PatienceMood => PatienceRemaining switch
    {
        <= 0 => "kapıyı gösteriyor",
        1    => "gözle görülür şekilde sıkıldı",
        2    => "sabırsızlanıyor",
        <= 4 => "hâlâ rahat",
        _    => "sakin"
    };

    // -----------------------------------------------------------------------
    // PAZARLIK
    // -----------------------------------------------------------------------

    /// <summary>Satıcının gizli taban fiyatı.</summary>
    public decimal ReservePrice(VehicleInstance v)
    {
        float flex = PriceFlexibility + Desperation * 0.10f + NegotiationRounds * 0.02f;

        // Yüzüne karşı yalan söylerken yakalandıysa pazarlık gücü zayıflar
        if (ProvenLiar) flex += 0.09f;

        flex = Math.Clamp(flex, 0.02f, 0.45f);
        return Cash.RoundTo(v.AskingPrice * (decimal)(1f - flex), 100);
    }

    public NegotiationOutcome Negotiate(decimal offer, VehicleInstance v, Random rng)
    {
        NegotiationRounds++;
        decimal reserve = ReservePrice(v);

        if (NegotiationRounds > 4 && offer < reserve * 0.85m)
        {
            WalkedAway = true;
            return new NegotiationOutcome(false, 0m,
                "\"Bak, sen ciddi değilsin. Hadi eyvallah.\"  — Satıcı gitti.");
        }

        if (offer >= reserve)
        {
            return new NegotiationOutcome(true, offer,
                Desperation > 0.6f
                    ? "\"Tamam tamam, anlaştık. Elimde kalmasın da.\""
                    : "\"...Peki. Ama bu fiyata kimseye söyleme.\"");
        }

        // Karşı teklif — taban ile teklif arasında bir yerde
        decimal counter = Cash.RoundTo(reserve + (v.AskingPrice - reserve) * 0.35m, 100);
        if (counter <= offer) counter = reserve;

        string line = offer < reserve * 0.6m
            ? $"\"Ciddi misin? O paraya jant alamazsın. En son {counter:N0}₺.\""
            : $"\"Yaklaştın ama olmadı. {counter:N0}₺ desem?\"";

        return new NegotiationOutcome(false, counter, line);
    }

    // Diyalog satırları SellerDialogue'da (Dialogue.cs) — arketip ve duruma göre
    // dallanıyor. Buradaki genel diziler oradaki sisteme taşındı.

    private static readonly string[] _tells =
    [
        "(konuyu değiştiriyor) \"Neyse, sen şu motora bak, harika değil mi?\"",
        "(fazla açıklıyor) \"...zaten hep öyleydi, hep, hiç değişmedi, gerçekten hiç.\"",
        "(aniden) \"Bin lira da indirebilirim aslında.\"",
        "(acele ettiriyor) \"Başka müşteri yolda, karar versen iyi olur.\"",
        "(o tarafa bakmanı istemiyor) \"Ordan bir şey göremezsin, boşuna eğilme.\"",
        "(soruyu soruyla karşılıyor) \"Ne oldu, bir şey mi gördün?\"",
        "(çok kısa duraksıyor) \"...Yok. Yok, sorun yok.\"",
    ];
}

public sealed record NegotiationOutcome(bool Accepted, decimal Counter, string Line);
