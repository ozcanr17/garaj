using Garaj.Core;
using Sys = System.Console;

namespace GarajApp;

internal sealed record Listing(VehicleInstance V, Seller S, PlayerKnowledge K)
{
    public bool Sold { get; set; }
}

internal static class Program
{
    private static Random _rng = new();
    private static readonly PlayerState _player = new();
    private static List<Listing> _listings = [];

    private static void Main(string[] args)
    {
        try { Sys.OutputEncoding = System.Text.Encoding.UTF8; } catch { }

        if (args.Length > 0 && args[0] == "--balance")
        {
            int n = args.Length > 1 && int.TryParse(args[1], out var c) ? c : 500;
            int bseed = args.Length > 2 && int.TryParse(args[2], out var bs) ? bs : 1;
            Balance.Run(n, bseed);
            return;
        }

        int seed = args.Length > 0 && int.TryParse(args[0], out var s) ? s : Environment.TickCount;
        _rng = new Random(seed);

        Intro(seed);
        RefreshListings();
        MainLoop();
    }

    // =======================================================================

    private static void Intro(int seed)
    {
        Ui.Clear();
        Ui.WriteLine(@"
   ██████╗  █████╗ ██████╗  █████╗      ██╗
  ██╔════╝ ██╔══██╗██╔══██╗██╔══██╗     ██║
  ██║  ███╗███████║██████╔╝███████║     ██║
  ██║   ██║██╔══██║██╔══██╗██╔══██║██   ██║
  ╚██████╔╝██║  ██║██║  ██║██║  ██║╚█████╔╝
   ╚═════╝ ╚═╝  ╚═╝╚═╝  ╚═╝╚═╝  ╚═╝ ╚════╝ ", ConsoleColor.DarkYellow);

        Ui.WriteLine("  Simülasyon çekirdeği — oynanabilir prototip", ConsoleColor.DarkGray);
        Ui.WriteLine($"  seed: {seed}", ConsoleColor.DarkGray);
        Sys.WriteLine();
        Ui.WriteLine("  Babandan kalan garajda ₺250.000 sermayen var.", ConsoleColor.Gray);
        Ui.WriteLine("  Bir araç al, ne aldığını çözmeye çalış, onar, sat.", ConsoleColor.Gray);
        Sys.WriteLine();
        Ui.WriteLine("  Hiçbir sayı kesin değildir. Hiçbir satıcı tam dürüst değildir.", ConsoleColor.DarkYellow);
        Ui.WriteLine("  Gördüğün her bant GERÇEK değil, senin İNANDIĞIN şeydir.", ConsoleColor.DarkYellow);
        Ui.Pause();
    }

    private static void RefreshListings()
    {
        _listings = Enumerable.Range(0, 5)
            .Select(_ =>
            {
                var (v, s) = VehicleGenerator.Generate(_rng);
                return new Listing(v, s, new PlayerKnowledge());
            })
            .ToList();
    }

    // =======================================================================
    // ANA DÖNGÜ
    // =======================================================================

    private static void MainLoop()
    {
        while (true)
        {
            Ui.Clear();
            StatusBar();

            var options = _player.OwnedVehicle is null
                ? new[] { "İlanlara bak", "Ekipman satın al" }
                : new[] { "İlanlara bak", "Ekipman satın al", $"GARAJ — {_player.OwnedVehicle.DisplayName}" };

            int choice = Ui.Menu("Ne yapıyorsun?", options);

            switch (choice)
            {
                case 0: Ui.WriteLine("\n  Kepenkleri indirdin.\n", ConsoleColor.DarkGray); return;
                case 1: ListingsScreen(); break;
                case 2: EquipmentScreen(); break;
                case 3: GarageScreen(); break;
            }
        }
    }

    private static void StatusBar()
    {
        Ui.Rule();
        Sys.Write("  ");
        Ui.Write($"GÜN {_player.Day}", ConsoleColor.White);
        Ui.Write($"  {_player.Clock}", ConsoleColor.Gray);
        Ui.Write("   │   ", ConsoleColor.DarkGray);
        Ui.Write(Ui.Money(_player.Money), _player.Money > 10_000m ? ConsoleColor.Green : ConsoleColor.Red);
        Ui.Write("   │   ", ConsoleColor.DarkGray);
        Ui.Write($"İtibar {_player.Reputation:F0}", ConsoleColor.Cyan);

        if (_player.Equipment.Count > 0)
        {
            Ui.Write("   │   ", ConsoleColor.DarkGray);
            Ui.Write(string.Join(" ", _player.Equipment.Select(e => EquipmentCatalog.Get(e).Name.Split(' ')[0])),
                     ConsoleColor.DarkCyan);
        }
        Sys.WriteLine();
        Ui.Rule();
    }

    // =======================================================================
    // EKİPMAN
    // =======================================================================

    private static void EquipmentScreen()
    {
        while (true)
        {
            Ui.Clear();
            Ui.Header("Ekipman");
            Ui.WriteLine("  Ekipman kalıcı yatırımdır. Her biri yeni bir teşhis yöntemi açar.\n",
                         ConsoleColor.DarkGray);

            foreach (var e in EquipmentCatalog.All.Where(e => _player.Has(e.Id)))
            {
                Ui.Write($"  ✓ {e.Name.PadRight(24)}", ConsoleColor.Green);
                Ui.WriteLine(e.Unlocks, ConsoleColor.DarkGray);
            }

            var available = EquipmentCatalog.All.Where(e => !_player.Has(e.Id)).ToList();
            if (available.Count == 0)
            {
                Ui.WriteLine("\n  Tüm ekipmana sahipsin.", ConsoleColor.Green);
                Ui.Pause();
                return;
            }

            var labels = available
                .Select(e => $"{e.Name.PadRight(24)} {Ui.Money(e.Cost).PadLeft(9)}   {e.Unlocks}")
                .ToArray();

            int c = Ui.Menu("Satın al", labels);
            if (c == 0) return;

            var pick = available[c - 1];
            if (_player.Money < pick.Cost)
            {
                Ui.WriteLine("\n  Paran yetmiyor.", ConsoleColor.Red);
                Ui.Pause();
                continue;
            }

            _player.Money -= pick.Cost;
            _player.Equipment.Add(pick.Id);
            Ui.WriteLine($"\n  {pick.Name} alındı.", ConsoleColor.Green);
            Ui.Pause();
        }
    }

    // =======================================================================
    // İLANLAR
    // =======================================================================

    private static void ListingsScreen()
    {
        while (true)
        {
            Ui.Clear();
            StatusBar();
            Ui.Header("İlanlar");

            var active = _listings.Where(l => !l.Sold).ToList();
            if (active.Count == 0)
            {
                Ui.WriteLine("  Şu an ilan yok.", ConsoleColor.DarkGray);
                Ui.Pause();
                return;
            }

            var labels = active.Select(l =>
            {
                var v = l.V;
                string examined = l.K.MethodsUsed.Count > 0 ? "•" : " ";
                return $"{examined} {v.ModelName,-20} {v.ModelYear}  " +
                       $"{v.OdometerReading,9:N0} km  {v.City,-18} {Ui.Money(v.AskingPrice),10}";
            }).ToArray();

            int c = Ui.Menu("İlan seç", labels);
            if (c == 0) return;

            ListingDetail(active[c - 1]);
        }
    }

    private static void ListingDetail(Listing l)
    {
        var (v, s, k) = (l.V, l.S, l.K);

        while (true)
        {
            Ui.Clear();
            StatusBar();
            Ui.Header(v.DisplayName);

            Sys.Write("  ");
            Ui.Write($"{v.ModelName} {v.Trim}", ConsoleColor.White);
            Ui.WriteLine($"   {v.ModelYear} model", ConsoleColor.Gray);
            Ui.WriteLine($"  {v.OdometerReading:N0} km  ·  {v.City}  ·  {v.Plate}", ConsoleColor.Gray);
            Sys.WriteLine();
            Ui.Write("  İSTENEN: ", ConsoleColor.DarkGray);
            Ui.WriteLine(Ui.Money(v.AskingPrice), ConsoleColor.Green);
            Sys.WriteLine();
            Ui.WriteLine("  \"" + Ui.Wrap(v.ListingText, 60, 3) + "\"", ConsoleColor.DarkYellow);
            Sys.WriteLine();

            Ui.Write($"  Satıcı: {s.Name}", ConsoleColor.White);
            Ui.WriteLine($"  ({s.ArchetypeName})", ConsoleColor.DarkGray);
            Ui.Write("  Sabrı: ", ConsoleColor.DarkGray);
            Ui.WriteLine(s.PatienceMood, s.PatienceRemaining <= 1 ? ConsoleColor.Red : ConsoleColor.Gray);

            if (s.ProvenLiar)
                Ui.WriteLine("  ⚠ Bu adamı yüzüne karşı yalan söylerken yakaladın.", ConsoleColor.Red);

            if (s.WalkedAway)
            {
                Ui.WriteLine("\n  Satıcı gitti. Bu araç artık senin için yok.", ConsoleColor.Red);
                Ui.Pause();
                l.Sold = true;
                return;
            }

            int c = Ui.Menu("Ne yapıyorsun?",
                "Teşhis yap",
                "Satıcıya soru sor",
                $"Ekspertiz tablosu   ({k.MethodsUsed.Count} yöntem kullanıldı)",
                $"Bulgular            ({k.Observations.Count} not)",
                "Pazarlık / satın al");

            switch (c)
            {
                case 0: return;
                case 1: DiagnosisScreen(v, k, s); break;
                case 2: AskSellerScreen(v, s, k); break;
                case 3: StatusTable(v, k, showBet: true); break;
                case 4: ObservationsScreen(k); break;
                case 5:
                    if (NegotiateScreen(l)) return;
                    break;
            }
        }
    }

    // =======================================================================
    // TEŞHİS
    // =======================================================================

    private static void DiagnosisScreen(VehicleInstance v, PlayerKnowledge k, Seller? s)
    {
        while (true)
        {
            Ui.Clear();
            StatusBar();
            Ui.Header("Teşhis yöntemleri");

            if (s is not null)
            {
                Ui.Write("  Satıcının sabrı: ", ConsoleColor.DarkGray);
                Ui.WriteLine($"{s.PatienceRemaining}/{s.PatienceMax}  ({s.PatienceMood})",
                             s.PatienceRemaining <= 1 ? ConsoleColor.Red : ConsoleColor.Gray);
                Sys.WriteLine();
            }

            var usable = MethodCatalog.All
                .Where(m => m.RequiredEquipment is null || _player.Has(m.RequiredEquipment))
                .ToList();

            var locked = MethodCatalog.All
                .Where(m => m.RequiredEquipment is not null && !_player.Has(m.RequiredEquipment))
                .ToList();

            foreach (var m in locked)
                Ui.WriteLine($"    [kilitli] {m.Name,-24} — {EquipmentCatalog.Get(m.RequiredEquipment!).Name} gerekiyor",
                             ConsoleColor.DarkGray);

            var labels = usable.Select(m =>
            {
                string used = k.MethodsUsed.Contains(m.Id) ? "✓" : " ";
                string cost = m.Cost > 0 ? Ui.Money(m.Cost) : "ücretsiz";
                string pat = s is not null && m.RequiresSellerPermission ? $"sabır -{m.PatienceCost}" : "";
                return $"{used} {m.Name,-24} {cost,10}  {m.Minutes,3}dk  {pat}";
            }).ToArray();

            int c = Ui.Menu("Yöntem seç", labels);
            if (c == 0) return;

            var method = usable[c - 1];

            if (_player.Money < method.Cost)
            {
                Ui.WriteLine("\n  Bu teşhis için paran yok.", ConsoleColor.Red);
                Ui.Pause();
                continue;
            }

            RunDiagnosis(v, k, s, method);
        }
    }

    private static void RunDiagnosis(VehicleInstance v, PlayerKnowledge k, Seller? s, DiagnosisMethod m)
    {
        Ui.Clear();
        Ui.Header(m.Name);
        Ui.WriteLine("  " + Ui.Wrap(m.FlavorText, 60, 2) + "\n", ConsoleColor.DarkGray);

        var result = DiagnosisEngine.Run(v, k, m, s, _rng);

        if (result.SellerRefused)
        {
            Ui.WriteLine("  SATICI REDDETTİ", ConsoleColor.Red);
            Ui.WriteLine("  " + Ui.Wrap(result.RefusalText ?? "", 60, 2), ConsoleColor.DarkYellow);
            Ui.Pause();
            return;
        }

        _player.Money -= result.Cost;
        _player.AdvanceMinutes(result.Minutes);

        foreach (var obs in result.Observations)
            Ui.PrintObservation(obs);

        Sys.WriteLine();
        if (result.Cost > 0)
            Ui.WriteLine($"  Maliyet: {Ui.Money(result.Cost)}   Süre: {result.Minutes} dk", ConsoleColor.DarkGray);
        else
            Ui.WriteLine($"  Süre: {result.Minutes} dk", ConsoleColor.DarkGray);

        Ui.Pause();
        StatusTable(v, k, showBet: false);
    }

    // =======================================================================
    // EKSPERTİZ TABLOSU — oyunun imza ekranı
    // =======================================================================

    private static void StatusTable(VehicleInstance v, PlayerKnowledge k, bool showBet)
    {
        Ui.Clear();
        Ui.Header("Ekspertiz tablosu");
        Ui.WriteLine("  Bantlar durumu DEĞİL, senin inandığın ARALIĞI gösterir.\n", ConsoleColor.DarkGray);

        foreach (var g in Enum.GetValues<SystemGroup>())
            Ui.ConditionLine(PartCatalog.GroupName(g), k.ForGroup(g), 18);

        Sys.WriteLine();
        Ui.Header("Parça detayı");

        foreach (var g in Enum.GetValues<SystemGroup>())
        {
            var parts = PartCatalog.InGroup(g).Where(p => !k.For(p.Id).IsUnexamined).ToList();
            if (parts.Count == 0) continue;

            Ui.WriteLine($"  {PartCatalog.GroupName(g)}", ConsoleColor.DarkCyan);
            foreach (var p in parts)
                Ui.ConditionLine("  " + p.Name, k.For(p.Id), 24);
        }

        int unexamined = PartCatalog.All.Count(p => k.For(p.Id).IsUnexamined);
        if (unexamined > 0)
        {
            Sys.WriteLine();
            Ui.WriteLine($"  {unexamined} parça hiç incelenmedi. Onlar hakkında hiçbir şey bilmiyorsun.",
                         ConsoleColor.DarkGray);
        }

        if (showBet) PrintBet(v, k);
        Ui.Pause();
    }

    private static void PrintBet(VehicleInstance v, PlayerKnowledge k)
    {
        var (low, high, conf) = Valuation.EstimatedRepairBill(v, k);
        var (rLow, rHigh) = Valuation.RestoredValueBand(v);

        Sys.WriteLine();
        Ui.Header("Bahis");

        Ui.Write("  İstenen fiyat".PadRight(30), ConsoleColor.Gray);
        Ui.WriteLine(Ui.Money(v.AskingPrice), ConsoleColor.White);

        Ui.Write("  Tahmini onarım faturası".PadRight(30), ConsoleColor.Gray);
        Ui.Write($"{Ui.Money(low)} – {Ui.Money(high)}", ConsoleColor.Yellow);
        Ui.WriteLine($"   (güven %{conf * 100:F0})", ConsoleColor.DarkGray);

        Ui.Write("  Toplam maliyetin".PadRight(30), ConsoleColor.Gray);
        Ui.WriteLine($"{Ui.Money(v.AskingPrice + low)} – {Ui.Money(v.AskingPrice + high)}", ConsoleColor.White);

        Ui.Write("  Onarılmış piyasa değeri".PadRight(30), ConsoleColor.Gray);
        Ui.WriteLine($"{Ui.Money(rLow)} – {Ui.Money(rHigh)}", ConsoleColor.Cyan);

        decimal bestCase = rHigh - (v.AskingPrice + low);
        decimal worstCase = rLow - (v.AskingPrice + high);

        Sys.WriteLine();
        Ui.Write("  Olası sonuç".PadRight(30), ConsoleColor.Gray);
        Ui.Write(Ui.Money(worstCase), worstCase >= 0 ? ConsoleColor.Green : ConsoleColor.Red);
        Ui.Write("  …  ", ConsoleColor.DarkGray);
        Ui.WriteLine(Ui.Money(bestCase), bestCase >= 0 ? ConsoleColor.Green : ConsoleColor.Red);

        Sys.WriteLine();
        if (conf < 0.35f)
            Ui.WriteLine("  Bu aracı neredeyse hiç tanımıyorsun. Şu an alırsan kumar oynuyorsun.",
                         ConsoleColor.Red);
        else if (conf < 0.6f)
            Ui.WriteLine("  Fikrin var ama bant hâlâ geniş. Bir teşhis daha bandı daraltabilir.",
                         ConsoleColor.DarkYellow);
        else
            Ui.WriteLine("  Bu araç hakkında iyi bir fikrin var. Yine de %100 asla bilemezsin.",
                         ConsoleColor.Green);
    }

    private static void ObservationsScreen(PlayerKnowledge k)
    {
        Ui.Clear();
        Ui.Header("Bulgular");

        if (k.Observations.Count == 0)
        {
            Ui.WriteLine("  Henüz hiçbir şey incelemedin.", ConsoleColor.DarkGray);
            Ui.Pause();
            return;
        }

        foreach (var o in k.Observations) Ui.PrintObservation(o);

        Sys.WriteLine();
        Ui.WriteLine("  ŞÜPHE'ler kanıt değildir — ama dumansız ateş de olmaz.", ConsoleColor.DarkGray);
        Ui.Pause();
    }

    // =======================================================================
    // SATICIYA SORU
    // =======================================================================

    private static void AskSellerScreen(VehicleInstance v, Seller s, PlayerKnowledge k)
    {
        var groups = Enum.GetValues<SystemGroup>();
        var labels = groups.Select(PartCatalog.GroupName).ToArray();

        int c = Ui.Menu("Ne hakkında soruyorsun?", labels);
        if (c == 0) return;

        var group = groups[c - 1];

        Ui.Clear();
        Ui.Header($"{s.Name} — {PartCatalog.GroupName(group)}");
        Ui.WriteLine($"  Sen: \"{PartCatalog.GroupName(group)} tarafında bir sıkıntı var mı?\"\n",
                     ConsoleColor.Gray);

        var reply = s.AskAbout(group, v, k, _rng);
        Ui.WriteLine("  " + s.Name + ": " + Ui.Wrap(reply.Answer, 55, 4), ConsoleColor.White);

        // Oyuncunun kendi gözüyle gördüğünü inkâr etmek kalıcı bir kırılma anıdır
        if (reply.Stance == SellerStance.IsrarliYalan)
        {
            Sys.WriteLine();
            Ui.WriteLine("  Bunu sen zaten kendi gözünle gördün. Adam yüzüne karşı inkâr ediyor.",
                         ConsoleColor.Red);
        }
        else if (reply.Stance == SellerStance.Yakalandi)
        {
            Sys.WriteLine();
            Ui.WriteLine("  Bulduğun şeyi kabul etti. Artık pazarlıkta elinde bir koz var.",
                         ConsoleColor.Green);
        }

        var surfaced = reply.Tell ?? s.RollFalseTell(_rng);
        if (surfaced is not null)
        {
            Sys.WriteLine();
            Ui.PrintObservation(surfaced);
            k.Observations.Add(surfaced);
        }

        _player.AdvanceMinutes(3);
        Sys.WriteLine();
        Ui.WriteLine("  Tell'ler %100 güvenilir değildir. Dürüst insanlar da gergin olur.",
                     ConsoleColor.DarkGray);
        Ui.Pause();
    }

    // =======================================================================
    // PAZARLIK
    // =======================================================================

    private static bool NegotiateScreen(Listing l)
    {
        var (v, s, k) = (l.V, l.S, l.K);

        while (true)
        {
            Ui.Clear();
            StatusBar();
            Ui.Header("Pazarlık");

            Ui.WriteLine($"  {v.DisplayName}  ·  isteniyor {Ui.Money(v.AskingPrice)}", ConsoleColor.White);
            Ui.WriteLine($"  Cebinde {Ui.Money(_player.Money)} var.\n", ConsoleColor.Gray);

            var (low, high, conf) = Valuation.EstimatedRepairBill(v, k);
            Ui.WriteLine($"  Tahmini onarım: {Ui.Money(low)} – {Ui.Money(high)} (güven %{conf * 100:F0})",
                         ConsoleColor.DarkYellow);
            Sys.WriteLine();

            var offer = Ui.AskMoney("Teklifin (0 = vazgeç)");
            if (offer is null || offer <= 0) return false;

            if (offer > _player.Money)
            {
                Ui.WriteLine("\n  O kadar paran yok.", ConsoleColor.Red);
                Ui.Pause();
                continue;
            }

            var outcome = s.Negotiate(offer.Value, v, _rng);
            Sys.WriteLine();
            Ui.WriteLine("  " + Ui.Wrap(outcome.Line, 60, 2), ConsoleColor.DarkYellow);

            if (s.WalkedAway) { Ui.Pause(); l.Sold = true; return true; }

            if (!outcome.Accepted) { Ui.Pause(); continue; }

            // --- SATIN ALMA ---
            _player.Money -= offer.Value;
            _player.PurchasePrice = offer.Value;
            _player.RepairSpend = 0m;
            _player.OwnedVehicle = v;
            _player.OwnedKnowledge = k;
            l.Sold = true;

            Sys.WriteLine();
            Ui.WriteLine($"  ARAÇ SENİN. {Ui.Money(offer.Value)} ödedin.", ConsoleColor.Green);
            Ui.WriteLine("  Şimdi ne aldığını öğreneceksin.", ConsoleColor.DarkYellow);
            Ui.Pause();
            return true;
        }
    }

    // =======================================================================
    // GARAJ
    // =======================================================================

    private static void GarageScreen()
    {
        while (_player.OwnedVehicle is not null)
        {
            var v = _player.OwnedVehicle;
            var k = _player.OwnedKnowledge!;

            Ui.Clear();
            StatusBar();
            Ui.Header($"Garaj — {v.DisplayName}");

            Ui.WriteLine($"  Alış: {Ui.Money(_player.PurchasePrice)}   " +
                         $"Onarım: {Ui.Money(_player.RepairSpend)}   " +
                         $"Toplam: {Ui.Money(_player.PurchasePrice + _player.RepairSpend)}",
                         ConsoleColor.Gray);

            if (v.KmSincePurchase > 0)
                Ui.WriteLine($"  Aldığından beri {v.KmSincePurchase:N0} km yaptın.", ConsoleColor.DarkGray);

            int c = Ui.Menu("Ne yapıyorsun?",
                "Ekspertiz tablosu",
                "Teşhis yap (satıcı yok — istediğini yap)",
                "Parça onar",
                "Test sürüşü (200 km — gizli şeyler ortaya çıkabilir)",
                "Sat");

            switch (c)
            {
                case 0: return;
                case 1: StatusTable(v, k, showBet: false); break;
                case 2: DiagnosisScreen(v, k, null); break;
                case 3: RepairScreen(v, k); break;
                case 4: TestDrive(v, k); break;
                case 5: SellScreen(v, k); return;
            }
        }
    }

    private static void RepairScreen(VehicleInstance v, PlayerKnowledge k)
    {
        while (true)
        {
            Ui.Clear();
            StatusBar();
            Ui.Header("Onarım");
            Ui.WriteLine("  Sadece incelediğin parçaları onarabilirsin — göremediğin şeyi tamir edemezsin.\n",
                         ConsoleColor.DarkGray);

            var repairable = PartCatalog.All
                .Where(p => !k.For(p.Id).IsUnexamined && k.For(p.Id).Mid < 72f)
                .ToList();

            if (repairable.Count == 0)
            {
                Ui.WriteLine("  İncelediğin parçaların hepsi iyi görünüyor.", ConsoleColor.Green);
                Ui.WriteLine("  (İncelemediklerin hakkında hiçbir şey söylenemez.)", ConsoleColor.DarkGray);
                Ui.Pause();
                return;
            }

            var labels = repairable.Select(p =>
            {
                var b = k.For(p.Id);
                decimal est = p.IsReplaceable ? p.PartCost : p.PartCost + 1_500m;
                return $"{p.Name,-24} inanç {b.Min:F0}-{b.Max:F0}   ~{Ui.Money(est),9}  {p.LaborHours:F1} saat";
            }).ToArray();

            int c = Ui.Menu("Hangi parça?", labels);
            if (c == 0) return;

            var pick = repairable[c - 1];
            var result = RepairEngine.Repair(v, pick.Id, _rng);

            Sys.WriteLine();
            if (!result.Success)
            {
                Ui.WriteLine("  " + result.Message, ConsoleColor.DarkGray);
                Ui.Pause();
                continue;
            }

            if (_player.Money < result.Cost)
            {
                Ui.WriteLine($"  Bu iş {Ui.Money(result.Cost)} tutuyor, paran yetmiyor.", ConsoleColor.Red);
                Ui.Pause();
                continue;
            }

            _player.Money -= result.Cost;
            _player.RepairSpend += result.Cost;
            _player.AdvanceMinutes((int)(result.Hours * 60));

            Ui.WriteLine("  " + result.Message, result.BoltStripped ? ConsoleColor.DarkYellow : ConsoleColor.Green);
            Ui.WriteLine($"  {Ui.Money(result.Cost)}  ·  {result.Hours:F1} saat", ConsoleColor.DarkGray);

            k.Update(pick.Id, new ConfidenceRange(86f, 97f, ConfidenceRange.MaxConfidence));
            Ui.Pause();
        }
    }

    private static void TestDrive(VehicleInstance v, PlayerKnowledge k)
    {
        Ui.Clear();
        Ui.Header("Test sürüşü");
        Ui.WriteLine("  200 km yol yapıyorsun. Araç ısınıyor, her şey yerine oturuyor.\n", ConsoleColor.DarkGray);

        var surfaced = ScamEngine.AdvanceKm(v, 200);
        _player.AdvanceMinutes(180);

        if (surfaced.Count == 0)
        {
            Ui.WriteLine("  Sorunsuz. Şimdilik.", ConsoleColor.Green);
        }
        else
        {
            foreach (var text in surfaced)
            {
                var o = new Observation(text, ObservationKind.Finding, MethodId.TestSurusuUzun);
                Ui.PrintObservation(o);
                k.Observations.Add(o);
            }
            Sys.WriteLine();
            Ui.WriteLine("  Gizlenen şeyler ortaya çıktı. Satıcı bunu bildiği için acele ettiriyordu.",
                         ConsoleColor.DarkYellow);
        }

        Ui.Pause();
    }

    // =======================================================================
    // SATIŞ + GERÇEĞİN AÇIKLANMASI
    // =======================================================================

    private static void SellScreen(VehicleInstance v, PlayerKnowledge k)
    {
        Ui.Clear();
        StatusBar();
        Ui.Header("Satış");

        decimal spent = _player.PurchasePrice + _player.RepairSpend;
        Ui.WriteLine($"  Bu araca toplam {Ui.Money(spent)} yatırdın.\n", ConsoleColor.Gray);

        var (rLow, rHigh) = Valuation.RestoredValueBand(v);
        Ui.WriteLine($"  Piyasa bu modeli iyi durumda {Ui.Money(rLow)} – {Ui.Money(rHigh)} arası görüyor.",
                     ConsoleColor.Cyan);
        Sys.WriteLine();

        var asking = Ui.AskMoney("Kaça ilan veriyorsun? (0 = vazgeç)");
        if (asking is null || asking <= 0) return;

        var sale = SaleEngine.Evaluate(v, asking.Value, _rng);

        Ui.Clear();
        Ui.Header("Alıcı geldi");
        Ui.WriteLine("  Alıcı da ekspertiz yapıyor. Senin göremediğini o görebilir.\n", ConsoleColor.DarkGray);

        if (sale.BuyerFindings.Count == 0)
            Ui.WriteLine("  Alıcı bir şey bulamadı.", ConsoleColor.Green);
        else
            foreach (var f in sale.BuyerFindings)
                Ui.PrintObservation(new Observation(f, ObservationKind.Finding, MethodId.Gozle));

        Sys.WriteLine();
        Ui.WriteLine($"  Alıcının teklifi: {Ui.Money(sale.Offer)}", ConsoleColor.Yellow);

        decimal finalPrice;
        if (sale.Sold)
        {
            finalPrice = asking.Value;
        }
        else
        {
            Ui.WriteLine($"  \"{Ui.Money(asking.Value)} çok fazla. {Ui.Money(sale.Offer)} veririm, olmazsa yok.\"",
                         ConsoleColor.DarkYellow);
            int accept = Ui.Menu("Kabul ediyor musun?", $"Evet, {Ui.Money(sale.Offer)}'a sat", "Hayır, vazgeç");
            if (accept != 1) { Ui.Pause(); return; }
            finalPrice = sale.Offer;
        }

        _player.Money += finalPrice;
        decimal profit = finalPrice - spent;
        _player.Reputation += profit > 0 ? 3f : -2f;

        Sys.WriteLine();
        Ui.WriteLine($"  SATILDI — {Ui.Money(finalPrice)}", ConsoleColor.Green);
        Ui.Write("  Kâr/zarar: ", ConsoleColor.Gray);
        Ui.WriteLine(Ui.Money(profit), profit >= 0 ? ConsoleColor.Green : ConsoleColor.Red);

        Ui.Pause();
        TruthReveal(v, k, profit);

        _player.OwnedVehicle = null;
        _player.OwnedKnowledge = null;
        RefreshListings();
    }

    /// <summary>
    /// Oyunun öğretme anı. Burası — ve SADECE burası — gerçeği gösterir.
    /// </summary>
    private static void TruthReveal(VehicleInstance v, PlayerKnowledge k, decimal profit)
    {
        Ui.Clear();
        Ui.Header("Gerçek");
        Ui.WriteLine("  Şimdi bu aracın ne olduğunu görüyorsun. Oyun sırasında göremezdin.\n",
                     ConsoleColor.DarkGray);

        Ui.Write("  Önceki sahip profili: ", ConsoleColor.Gray);
        Ui.WriteLine(OwnerName(v.Owner), ConsoleColor.Cyan);
        Ui.WriteLine("  " + Ui.Wrap(OwnerHint(v.Owner), 58, 2), ConsoleColor.DarkGray);

        if (v.OdometerReading != v.TrueOdometer)
        {
            Sys.WriteLine();
            Ui.Write("  Gösterge: ", ConsoleColor.Gray);
            Ui.Write($"{v.OdometerReading:N0} km", ConsoleColor.White);
            Ui.Write("     Gerçek: ", ConsoleColor.Gray);
            Ui.WriteLine($"{v.TrueOdometer:N0} km", ConsoleColor.Red);
        }

        if (v.Tampers.Count > 0)
        {
            Sys.WriteLine();
            Ui.WriteLine("  Bu araçta yapılmış maskelemeler:", ConsoleColor.Gray);
            foreach (var t in v.Tampers)
            {
                bool caught = k.Observations.Any(o =>
                    o.Kind == ObservationKind.Finding &&
                    o.Text.Contains(t.Def.Name, StringComparison.OrdinalIgnoreCase));

                Ui.Write("    • ", ConsoleColor.DarkGray);
                Ui.Write(t.Def.Name.PadRight(30), caught ? ConsoleColor.Green : ConsoleColor.Red);
                Ui.WriteLine(caught ? "YAKALADIN" : t.Surfaced ? "sonradan ortaya çıktı" : "kaçırdın",
                             caught ? ConsoleColor.Green : ConsoleColor.Red);
            }
        }

        // GERÇEK yanılgı = gerçeğin inandığın BANDIN DIŞINDA kalması.
        // Geniş ama doğruyu içeren bir bant hata değildir — sadece belirsizliktir.
        static float Miss(ConfidenceRange b, float truth)
            => truth >= b.Min && truth <= b.Max ? 0f
             : truth < b.Min ? b.Min - truth : truth - b.Max;

        Sys.WriteLine();
        Ui.Header("Yanıldığın yerler");

        var wrong = v.Parts.Values
            .Where(p => !k.For(p.DefId).IsUnexamined)
            .Select(p => new { Part = p, Belief = k.For(p.DefId), Miss = Miss(k.For(p.DefId), p.Condition) })
            .Where(x => x.Miss > 6f)
            .OrderByDescending(x => x.Miss)
            .Take(6)
            .ToList();

        if (wrong.Count == 0)
        {
            Ui.WriteLine("  İncelediğin hiçbir parçada bandın dışına düşmedin.", ConsoleColor.Green);
            Ui.WriteLine("  Tahminlerin belirsizdi ama dürüsttü. Ustalık budur.", ConsoleColor.DarkGray);
        }
        else
        {
            foreach (var e in wrong)
            {
                Ui.Write($"    {e.Part.Def.Name,-24}", ConsoleColor.White);
                Ui.Write($"sandın {e.Belief.Min:F0}-{e.Belief.Max:F0}".PadRight(22), ConsoleColor.Yellow);
                Ui.Write($"gerçek {e.Part.Condition:F0}", ConsoleColor.Red);
                Ui.WriteLine($"   ({e.Miss:F0} puan dışında)", ConsoleColor.DarkGray);
            }
        }

        // Hiç bakmadığın ve gerçekten bozuk olan parçalar — ayrı ve daha acı bir ders
        var blindSpots = v.Parts.Values
            .Where(p => k.For(p.DefId).IsUnexamined && p.Condition < 45f)
            .OrderBy(p => p.Condition)
            .Take(6)
            .ToList();

        if (blindSpots.Count > 0)
        {
            Sys.WriteLine();
            Ui.WriteLine("  Hiç bakmadığın ve bozuk çıkan parçalar:", ConsoleColor.Gray);
            foreach (var p in blindSpots)
            {
                Ui.Write($"    {p.Def.Name,-24}", ConsoleColor.White);
                Ui.Write("hiç bakmadın".PadRight(22), ConsoleColor.DarkGray);
                Ui.WriteLine($"gerçek {p.Condition:F0}", ConsoleColor.Red);
            }
        }

        var missed = v.AllDefects.Where(d => !k.DiscoveredDefects.Contains(d.Id)).ToList();
        if (missed.Count > 0)
        {
            Sys.WriteLine();
            Ui.WriteLine($"  Bulamadığın {missed.Count} kusur vardı:", ConsoleColor.Gray);
            foreach (var d in missed.Take(6))
            {
                Ui.Write("    - ", ConsoleColor.Red);
                Ui.Write($"{v.Part(d.PartId).Def.Name}: ", ConsoleColor.White);
                Ui.WriteLine(d.Description, ConsoleColor.Gray);

                string how = d.SurfacesAfterKm > 0
                    ? "bulunamazdı — zaman bombasıydı"
                    : "bulunurdu: " + string.Join(", ", d.RevealedBy.Select(m => MethodCatalog.Get(m).Name));
                Ui.WriteLine("      " + Ui.Wrap(how, 52, 6), ConsoleColor.DarkGray);
            }
        }

        Sys.WriteLine();
        Ui.Rule();
        Ui.WriteLine(profit >= 0
            ? "  Kâr ettin. Ama ne kadarı beceri, ne kadarı şanstı?"
            : "  Zarar ettin. Yukarıdaki listeye bak — hangisini önleyebilirdin?",
            profit >= 0 ? ConsoleColor.Green : ConsoleColor.DarkYellow);
        Ui.Rule();
        Ui.Pause();
    }

    private static string OwnerName(OwnerProfile p) => p switch
    {
        OwnerProfile.YasliCift      => "Yaşlı çift",
        OwnerProfile.GencSurucu     => "Genç sürücü",
        OwnerProfile.Taksi          => "Taksi",
        OwnerProfile.KiralikFilo    => "Kiralık filo",
        OwnerProfile.MerakliTamirci => "Meraklı / tamirci",
        OwnerProfile.UzunSurePark   => "Uzun süre park edilmiş",
        OwnerProfile.SehirIci       => "Şehir içi kullanım",
        OwnerProfile.UzunYol        => "Uzun yol",
        _ => p.ToString()
    };

    private static string OwnerHint(OwnerProfile p) => p switch
    {
        OwnerProfile.YasliCift      => "İmza: düşük km ama ölü akü, sertleşmiş kauçuk, paslı diskler, tertemiz motor.",
        OwnerProfile.GencSurucu     => "İmza: bitik debriyaj, aşırı yıpranmış fren, zorlanmış süspansiyon, sağlam motor.",
        OwnerProfile.Taksi          => "İmza: yüksek km ama sapasağlam motor, bitmiş iç mekan, çökmüş süspansiyon.",
        OwnerProfile.KiralikFilo    => "İmza: hasarlı iç mekan, kötü kaporta, çok el değiştirmiş.",
        OwnerProfile.MerakliTamirci => "İmza: iyi motor, amatör kablo işi, elden geçmiş süspansiyon.",
        OwnerProfile.UzunSurePark   => "İmza: kemirgen kablo hasarı, çürümüş soğutma, sertleşmiş lastikler.",
        OwnerProfile.SehirIci       => "İmza: düşük km ama içten yorgun motor — çok soğuk çalıştırma.",
        OwnerProfile.UzunYol        => "İmza: yüksek km ama düşük aşınma. Otoyol kilometresi motoru yormaz.",
        _ => ""
    };
}
