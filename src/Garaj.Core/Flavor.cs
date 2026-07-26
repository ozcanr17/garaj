namespace Garaj.Core;

// ---------------------------------------------------------------------------
// İNCELEME DETAYI
//
// Teşhis yalnızca "kusur buldun / bulamadın" olamaz. Gerçek bir usta arabaya
// baktığında ONLARCA küçük şey görür ve bunların hiçbiri tek başına kanıt
// değildir — ama hepsi birlikte bir izlenim oluşturur.
//
// Buradaki satırlar kusur AÇIĞA ÇIKARMAZ. Durumla ilişkili duyusal ipuçlarıdır.
// Oyuncu bunlardan çıkarım yapar; oyun ona cevabı söylemez.
//
// KRİTİK: satırlar ALGILANAN duruma göre seçilir (ScamEngine.PerceivedCondition).
// Motoru yıkanmış bir araç "tertemiz" satırlarını üretir — maskeleme burada da çalışır.
// ---------------------------------------------------------------------------

public sealed record FlavorLine(
    SystemGroup Group,
    float MinCondition,
    float MaxCondition,
    MethodId[] Methods,
    string Text
);

public static class InspectionFlavor
{
    private const float Bad = 0f, Poor = 34f, Mid = 62f, Good = 101f;

    private static FlavorLine L(SystemGroup g, float lo, float hi, MethodId[] m, string t)
        => new(g, lo, hi, m, t);

    private static readonly MethodId[] Look   = [MethodId.Gozle];
    private static readonly MethodId[] Touch  = [MethodId.Dokunma];
    private static readonly MethodId[] Start  = [MethodId.Calistir];
    private static readonly MethodId[] Drive  = [MethodId.TestSurusuKisa, MethodId.TestSurusuUzun];
    private static readonly MethodId[] Long   = [MethodId.TestSurusuUzun];
    private static readonly MethodId[] Lift   = [MethodId.Lift];
    private static readonly MethodId[] Obd    = [MethodId.OBD];
    private static readonly MethodId[] Paint  = [MethodId.BoyaKalinlik];
    private static readonly MethodId[] Comp   = [MethodId.Kompresyon];
    private static readonly MethodId[] Scope  = [MethodId.Endoskop];
    private static readonly MethodId[] Steth  = [MethodId.Stetoskop];
    private static readonly MethodId[] Leak   = [MethodId.LeakDown];
    private static readonly MethodId[] Therm  = [MethodId.TermalKamera];
    private static readonly MethodId[] Pro    = [MethodId.ProOBD];
    private static readonly MethodId[] OilLab = [MethodId.YagAnalizi];
    private static readonly MethodId[] Frame  = [MethodId.SasiOlcum];

    public static IReadOnlyList<FlavorLine> All { get; } =
    [
        // ================= MOTOR =================
        L(SystemGroup.Motor, Bad, Poor, Touch, "Karter çevresinde kurumuş yağ tabakası var; parmağın siyah çıkıyor."),
        L(SystemGroup.Motor, Bad, Poor, Touch, "Yağ kapağının iç yüzü kahverengi köpükle kaplı."),
        L(SystemGroup.Motor, Bad, Poor, Touch, "Yağ çubuğundaki seviye minimumun altında ve yağ is gibi siyah."),
        L(SystemGroup.Motor, Bad, Poor, Look,  "Blok yan yüzeyinde eski sızıntıların bıraktığı kabuk var."),
        L(SystemGroup.Motor, Bad, Poor, Start, "Marş birkaç saniye zorluyor, sonra dengesiz bir rölantiye oturuyor."),
        L(SystemGroup.Motor, Bad, Poor, Start, "Egzozdan mavimsi duman geliyor — yağ yakıyor."),
        L(SystemGroup.Motor, Bad, Poor, Start, "Rölanti devri 700 ile 1100 arasında geziniyor."),
        L(SystemGroup.Motor, Bad, Poor, Long,  "Isındıkça motor sesi kabalaşıyor, hararet ibresi ortanın üstüne çıkıyor."),
        L(SystemGroup.Motor, Bad, Poor, Comp,  "Silindirler arasındaki kompresyon farkı 2 bar'ı aşıyor."),
        L(SystemGroup.Motor, Bad, Poor, Scope, "Silindir duvarında gözle görülür çizik izleri var."),
        L(SystemGroup.Motor, Poor, Mid, Touch, "Hortum kelepçelerinde hafif pas var ama aktif sızdırma yok."),
        L(SystemGroup.Motor, Poor, Mid, Start, "Rölanti biraz kaba ama düzenli; devir sabit duruyor."),
        L(SystemGroup.Motor, Poor, Mid, Start, "İlk çalıştırmada gri duman var, birkaç saniyede temizleniyor."),
        L(SystemGroup.Motor, Poor, Mid, Long,  "Uzun sürüşte sıcaklık normalin biraz üstünde ama sabit kalıyor."),
        L(SystemGroup.Motor, Poor, Mid, Comp,  "Bir silindir diğerlerinden ölçülebilir şekilde düşük."),
        L(SystemGroup.Motor, Mid, Good, Touch, "Motor bölmesi yaşına göre derli toplu, contalar kuru."),
        L(SystemGroup.Motor, Mid, Good, Start, "İlk denemede çalışıyor, rölanti taş gibi oturuyor."),
        L(SystemGroup.Motor, Mid, Good, Comp,  "Dört silindir de birbirine yakın; sapma %5'in altında."),

        // ================= ŞANZIMAN =================
        L(SystemGroup.Sanziman, Bad, Poor, Drive, "Debriyaj pedalın en üstünde kavrıyor, ayağını kaldırır kaldırmaz tutuyor."),
        L(SystemGroup.Sanziman, Bad, Poor, Drive, "2. vitese geçerken belirgin bir zorlama ve tık sesi var."),
        L(SystemGroup.Sanziman, Bad, Poor, Drive, "Rölantide debriyaja basınca uğultu kesiliyor — rulman sesi."),
        L(SystemGroup.Sanziman, Bad, Poor, Long,  "Isındıktan sonra 3. vitesten atıyor, gaz kesince geri giriyor."),
        L(SystemGroup.Sanziman, Bad, Poor, Lift,  "Şanzıman körüğü yırtılmış, gres dışarı sıçramış."),
        L(SystemGroup.Sanziman, Poor, Mid, Drive, "Vites geçişleri biraz sert ama her vites yerine oturuyor."),
        L(SystemGroup.Sanziman, Poor, Mid, Drive, "Debriyaj orta yükseklikte kavrıyor."),
        L(SystemGroup.Sanziman, Mid, Good, Drive, "Vitesler yağ gibi giriyor, debriyaj alt-orta noktada tutuyor."),

        // ================= SOĞUTMA =================
        L(SystemGroup.Sogutma, Bad, Poor, Look,  "Radyatör peteklerinin alt kısmında kurumuş antifriz izi var."),
        L(SystemGroup.Sogutma, Bad, Poor, Look,  "Su deposundaki sıvı bulanık ve içinde yağımsı bir film var."),
        L(SystemGroup.Sogutma, Bad, Poor, Touch, "Üst radyatör hortumu elle sıkılınca çıtırdıyor — kauçuk sertleşmiş."),
        L(SystemGroup.Sogutma, Bad, Poor, Long,  "Uzun sürüşte hararet kırmızıya yaklaşıyor, fan sürekli çalışıyor."),
        L(SystemGroup.Sogutma, Poor, Mid, Look,  "Antifriz seviyesi minimum ile maksimum arasında ama rengi solmuş."),
        L(SystemGroup.Sogutma, Poor, Mid, Long,  "Hararet biraz geç oturuyor ama kırmızıya girmiyor."),
        L(SystemGroup.Sogutma, Mid, Good, Look,  "Radyatör ve hortumlar kuru, antifriz rengi berrak."),

        // ================= EGZOZ =================
        L(SystemGroup.Egzoz, Bad, Poor, Start, "Egzoz sesi olması gerekenden derin ve patlamalı."),
        L(SystemGroup.Egzoz, Bad, Poor, Lift,  "Orta susturucunun dibi delinmiş, kenarları pul pul dökülüyor."),
        L(SystemGroup.Egzoz, Bad, Poor, Lift,  "Egzoz askı lastikleri kopmuş, boru şaseye değiyor."),
        L(SystemGroup.Egzoz, Poor, Mid, Lift,  "Egzoz hattında yüzeysel pas var ama delik yok."),
        L(SystemGroup.Egzoz, Mid, Good, Start, "Egzoz sesi temiz ve düzenli."),

        // ================= FREN =================
        L(SystemGroup.Fren, Bad, Poor, Drive, "Fren pedalı normalden derine iniyor, ilk basışta boşluk hissediliyor."),
        L(SystemGroup.Fren, Bad, Poor, Drive, "Frende direksiyon titriyor — diskler tabla yapmış olabilir."),
        L(SystemGroup.Fren, Bad, Poor, Look,  "Jant aralığından bakınca balata kalınlığı gözle 2-3 mm görünüyor."),
        L(SystemGroup.Fren, Bad, Poor, Lift,  "Disk yüzeyinde belirgin basamak oluşmuş, kenarı tırnak yapmış."),
        L(SystemGroup.Fren, Poor, Mid, Drive, "Fren tutuyor ama pedal hissi yumuşak."),
        L(SystemGroup.Fren, Poor, Mid, Lift,  "Balatalarda ömrün yarısı civarı kalmış."),
        L(SystemGroup.Fren, Mid, Good, Drive, "Fren pedalı sert ve yüksek, araç düz duruyor."),

        // ================= SÜSPANSİYON =================
        L(SystemGroup.Suspansiyon, Bad, Poor, Drive, "Her kasiste ön taraftan tak tak sesler geliyor."),
        L(SystemGroup.Suspansiyon, Bad, Poor, Drive, "Direksiyonda belirgin boşluk var, araç yolda geziniyor."),
        L(SystemGroup.Suspansiyon, Bad, Poor, Look,  "Aracın ön tarafı arkaya göre gözle görülür şekilde çökmüş."),
        L(SystemGroup.Suspansiyon, Bad, Poor, Lift,  "Amortisör gövdesi yağ atmış, üstü toz tutmuş çamur olmuş."),
        L(SystemGroup.Suspansiyon, Bad, Poor, Lift,  "Rotil körüğü yırtık, elle oynatınca boşluk hissediliyor."),
        L(SystemGroup.Suspansiyon, Poor, Mid, Drive, "Bozuk yolda hafif takırtı var ama araç yolu tutuyor."),
        L(SystemGroup.Suspansiyon, Poor, Mid, Lift,  "Takozlarda çatlama başlamış ama kopma yok."),
        L(SystemGroup.Suspansiyon, Mid, Good, Drive, "Araç düz gidiyor, direksiyon merkezde duruyor."),

        // ================= ELEKTRİK =================
        L(SystemGroup.Elektrik, Bad, Poor, Start, "Marşa basınca farlar gözle görülür şekilde kararıyor."),
        L(SystemGroup.Elektrik, Bad, Poor, Look,  "Akü kutup başlarında beyaz-mavi oksit birikmiş."),
        L(SystemGroup.Elektrik, Bad, Poor, Look,  "Torpido altında bantla eklenmiş, renkleri tutmayan kablolar var."),
        L(SystemGroup.Elektrik, Bad, Poor, Obd,   "Birden fazla kalıcı hata kodu kayıtlı."),
        L(SystemGroup.Elektrik, Bad, Poor, Start, "Şarj lambası rölantide sönmüyor, gaz verince kayboluyor."),
        L(SystemGroup.Elektrik, Poor, Mid, Look,  "Kablo demeti orijinal görünüyor ama birkaç yerde izole bandı var."),
        L(SystemGroup.Elektrik, Poor, Mid, Obd,   "Geçmiş hata kaydı var ama aktif arıza yok."),
        L(SystemGroup.Elektrik, Mid, Good, Start, "Tüm göstergeler yanıp sönüyor, marş anında dönüyor."),

        // ================= KAPORTA =================
        L(SystemGroup.Kaporta, Bad, Poor, Look,  "Sol arka çamurluğun alt kenarında boya kabarmış, altı kahverengi."),
        L(SystemGroup.Kaporta, Bad, Poor, Look,  "Kaput ile çamurluk arasındaki aralık iki tarafta belirgin farklı."),
        L(SystemGroup.Kaporta, Bad, Poor, Look,  "Güneşe karşı bakınca kapı üzerinde portakal kabuğu dokusu var."),
        L(SystemGroup.Kaporta, Bad, Poor, Paint, "Bir panelde kalınlık 300 mikronun üstünde — fabrika çıkışı iki katı."),
        L(SystemGroup.Kaporta, Bad, Poor, Lift,  "Marşpiyel altı pul pul dökülüyor, tornavida ucu batıyor."),
        L(SystemGroup.Kaporta, Poor, Mid, Look,  "Panellerde yaşına göre makul çizikler ve birkaç taş vuruğu var."),
        L(SystemGroup.Kaporta, Poor, Mid, Paint, "İki panelde kalınlık diğerlerinden yüksek ama sınırda."),
        L(SystemGroup.Kaporta, Mid, Good, Look,  "Panel aralıkları düzgün, boya tonu her yerde tutuyor."),
        L(SystemGroup.Kaporta, Mid, Good, Paint, "Tüm paneller 100-140 mikron bandında — fabrika boyası."),

        // ================= İÇ MEKAN =================
        L(SystemGroup.IcMekan, Bad, Poor, Look, "Sürücü koltuğunun yan desteği yırtılmış, sünger görünüyor."),
        L(SystemGroup.IcMekan, Bad, Poor, Look, "Direksiyon derisi parlamış, gaz pedalı lastiği tamamen düzleşmiş."),
        L(SystemGroup.IcMekan, Bad, Poor, Look, "Tavan kaplaması arkadan sarkmış, iğneyle tutturulmuş."),
        L(SystemGroup.IcMekan, Bad, Poor, Look, "Araç içinde bastırılmış bir rutubet kokusu var."),
        L(SystemGroup.IcMekan, Poor, Mid, Look, "Döşemede kullanım izi var ama yırtık yok."),
        L(SystemGroup.IcMekan, Poor, Mid, Look, "Koltuklara kılıf geçirilmiş — altını göremiyorsun."),
        L(SystemGroup.IcMekan, Mid, Good, Look, "İç mekan yaşına göre şaşırtıcı derecede korunmuş."),

        // ================= TEKERLEK =================
        L(SystemGroup.Tekerlek, Bad, Poor, Look, "Lastiklerin yanağında ince çatlaklar ağ gibi yayılmış."),
        L(SystemGroup.Tekerlek, Bad, Poor, Look, "Ön lastikler iç kenardan düzensiz aşınmış — rot ayarı bozuk."),
        L(SystemGroup.Tekerlek, Bad, Poor, Look, "Diş derinliği gözle 2 mm civarı, dört lastik de farklı marka."),
        L(SystemGroup.Tekerlek, Bad, Poor, Lift, "Tekerleği elle çevirince poyra rulmanından uğultu geliyor."),
        L(SystemGroup.Tekerlek, Poor, Mid, Look, "Lastiklerde yarı ömür var, aşınma düzgün."),
        L(SystemGroup.Tekerlek, Mid, Good, Look, "Dört lastik de aynı marka ve DOT tarihleri yakın."),

        // ============ ÜST KADEME ALETLER ============
        // Her alet aynı gerçeğe FARKLI bir pencereden bakar. Aletin değeri
        // "daha çok bilgi" değil, "başka türlü görülemeyecek bilgi"dir.

        // --- Stetoskop ---
        L(SystemGroup.Motor, Bad, Poor, Steth, "Ucu eksantrik kapağına dayayınca düzenli bir metalik tıkırtı duyuluyor."),
        L(SystemGroup.Motor, Bad, Poor, Steth, "Triger kapağından gelen ses devirle birebir senkron — kayış veya gergi."),
        L(SystemGroup.Motor, Poor, Mid, Steth, "Hafif bir supap sesi var ama düzenli; soğukken belirginleşiyor."),
        L(SystemGroup.Motor, Mid, Good, Steth, "Blok üzerinde tek tük mekanik ses var, hepsi normal ritmde."),
        L(SystemGroup.Sanziman, Bad, Poor, Steth, "Şanzıman gövdesinden hıza bağlı bir uğultu geliyor — rulman."),
        L(SystemGroup.Tekerlek, Bad, Poor, Steth, "Poyra göbeğinde tekerlek dönerken artan bir hırıltı var."),
        L(SystemGroup.Tekerlek, Poor, Mid, Steth, "Rulmanlardan hafif ses geliyor ama henüz belirgin değil."),

        // --- Sızdırmazlık (leak-down) ---
        L(SystemGroup.Motor, Bad, Poor, Leak, "Kaçak %40'ın üzerinde ve ses yağ doldurma kapağından geliyor — segmanlar."),
        L(SystemGroup.Motor, Bad, Poor, Leak, "Hava egzozdan kaçıyor: egzoz supabı tam kapanmıyor."),
        L(SystemGroup.Motor, Bad, Poor, Leak, "Radyatörden kabarcık çıkıyor — kapak contası yanmış."),
        L(SystemGroup.Motor, Poor, Mid, Leak, "Kaçak %20 civarı; yaşına göre sınırda ama kabul edilebilir."),
        L(SystemGroup.Motor, Mid, Good, Leak, "Dört silindirde de kaçak %10'un altında. Motor sızdırmıyor."),

        // --- Termal kamera ---
        L(SystemGroup.Sogutma, Bad, Poor, Therm, "Radyatörün alt üçte biri soğuk kalıyor — petekler tıkalı."),
        L(SystemGroup.Sogutma, Poor, Mid, Therm, "Radyatör yüzeyinde birkaç soğuk leke var, kısmi tıkanma."),
        L(SystemGroup.Sogutma, Mid, Good, Therm, "Radyatör baştan sona eşit ısınıyor."),
        L(SystemGroup.Elektrik, Bad, Poor, Therm, "Sigorta kutusunda bir devre çevresine göre belirgin sıcak."),
        L(SystemGroup.Elektrik, Bad, Poor, Therm, "Alternatör gövdesi olması gerekenden çok daha sıcak."),
        L(SystemGroup.Motor, Bad, Poor, Therm, "Bir silindirin egzoz portu diğerlerinden soğuk — o silindir tam çalışmıyor."),
        L(SystemGroup.Motor, Mid, Good, Therm, "Dört egzoz portu da aynı sıcaklıkta."),

        // --- Profesyonel OBD ---
        L(SystemGroup.Elektrik, Bad, Poor, Pro, "ABS modülü ile gösterge paneli arasında iletişim hatası kayıtlı."),
        L(SystemGroup.Motor, Bad, Poor, Pro, "Motor modülünde silinmemiş donuk kare (freeze frame) verisi duruyor."),
        L(SystemGroup.Sanziman, Bad, Poor, Pro, "Şanzıman modülü aşırı ısınma olayı kaydetmiş."),
        L(SystemGroup.Elektrik, Mid, Good, Pro, "Tüm modüller birbiriyle tutarlı, kayıtlı olay yok."),

        // --- Yağ analizi ---
        L(SystemGroup.Motor, Bad, Poor, OilLab, "Raporda demir ve alüminyum partikül değerleri sınırın çok üstünde."),
        L(SystemGroup.Motor, Bad, Poor, OilLab, "Yağda antifriz izi var — su ile yağ bir yerde buluşuyor."),
        L(SystemGroup.Motor, Poor, Mid, OilLab, "Metal değerleri yüksek ama yaşına göre beklenen bantta."),
        L(SystemGroup.Motor, Mid, Good, OilLab, "Partikül değerleri düşük. Motor içi temiz çalışıyor."),

        // --- Şasi ölçümü ---
        L(SystemGroup.Kaporta, Bad, Poor, Frame, "Ön sol şasi ayağı fabrika değerinden 14 mm sapmış."),
        L(SystemGroup.Kaporta, Bad, Poor, Frame, "Ölçüm raporunda arka panelde kaynak izleri işaretlenmiş."),
        L(SystemGroup.Kaporta, Poor, Mid, Frame, "Şasi genel olarak düzgün, bir noktada 3 mm sapma var."),
        L(SystemGroup.Kaporta, Mid, Good, Frame, "Şasi tüm noktalarda fabrika toleransında."),
        L(SystemGroup.Suspansiyon, Bad, Poor, Frame, "Süspansiyon bağlantı noktaları simetrik değil — araç bir yana çekecek."),
    ];

    /// <summary>
    /// Bu yöntemle bakıldığında görülen duyusal detaylar.
    /// ALGILANAN durumu kullanır — maskeleme burada da oyuncuyu yanıltır.
    /// </summary>
    public static List<Observation> For(
        VehicleInstance v, DiagnosisMethod method, Random rng, int maxLines = 4)
    {
        var candidates = new List<FlavorLine>();

        foreach (var group in Enum.GetValues<SystemGroup>())
        {
            bool covered = method.Covers.Contains(group);
            if (!covered && !method.GlancesEverything) continue;

            // Grubun algılanan ortalama durumu
            var parts = v.Parts.Values.Where(p => p.Def.Group == group).ToList();
            if (parts.Count == 0) continue;
            float perceived = parts.Average(p => ScamEngine.PerceivedCondition(v, p, method.Id));

            candidates.AddRange(All.Where(f =>
                f.Group == group &&
                f.Methods.Contains(method.Id) &&
                perceived >= f.MinCondition &&
                perceived < f.MaxCondition));
        }

        if (candidates.Count == 0) return [];

        // Farklı gruplardan seç — tek bir sistem hakkında dört satır sıkıcı olur
        return candidates
            .OrderBy(_ => rng.Next())
            .GroupBy(f => f.Group)
            .Select(g => g.First())
            .OrderBy(_ => rng.Next())
            .Take(maxLines)
            .Select(f => new Observation(f.Text, ObservationKind.Detail, method.Id))
            .ToList();
    }
}
