using Garaj.Core;

namespace Garaj.Web;

/// <summary>
/// Web port of GarajApp.Program's game loop. Same Garaj.Core calls, same
/// architectural rule (presentation never reads PartInstance.Condition,
/// only PlayerKnowledge — except TruthReveal). Program.cs used nested
/// blocking console loops; here every "screen" is a state plus a stack of
/// parent screens for Back(), driven by button clicks from Game.razor.
/// </summary>
public sealed class GameState
{
    public enum Screen
    {
        Intro, MainMenu, Equipment, Listings, ListingDetail,
        Diagnosis, DiagnosisResult, AskSellerPickGroup, AskSellerResult,
        DocumentDesk, ReadDocumentPick, ReadDocumentView,
        CompareDocumentsPickLeft, CompareDocumentsPickRight, CompareDocumentsView, CompareResult,
        StatusTable, Observations,
        Negotiate, PressLeverage, LeverageResult,
        Garage, Repair, RepairJob, TestDriveResult,
        Sell, SellResult, TruthReveal,
        Event, EventResult, Closed
    }

    public sealed class Listing
    {
        public required VehicleInstance V { get; init; }
        public required Seller S { get; init; }
        public required PlayerKnowledge K { get; init; }
        public bool Sold { get; set; }
    }

    public Random Rng { get; private set; } = new();
    public int Seed { get; private set; }
    public PlayerState Player { get; } = new();
    public Storyteller Story { get; } = new();
    public List<Listing> Listings { get; private set; } = [];

    public Screen Current { get; private set; } = Screen.Intro;
    private readonly Stack<Screen> _back = new();

    // Context for whichever listing/vehicle screen is currently open.
    public Listing? CurrentListing { get; private set; }
    public VehicleInstance? CurrentVehicle { get; private set; }
    public PlayerKnowledge? CurrentKnowledge { get; private set; }
    public Seller? CurrentSeller { get; private set; }
    public bool StatusTableShowBet { get; private set; }
    public Screen StatusTableReturnTo { get; private set; } = Screen.MainMenu;

    // --- transient per-screen results ---
    public DiagnosisMethod? LastMethod { get; private set; }
    public DiagnosisResult? LastDiagnosisResult { get; private set; }
    public decimal LastDiagnosisPaid { get; private set; }

    public SystemGroup LastAskGroup { get; private set; }
    public DialogueResult? LastAskReply { get; private set; }
    public Observation? LastAskTell { get; private set; }

    public DocumentId ReadDoc { get; private set; }
    public DocumentId CompareLeftDoc { get; private set; }
    public DocumentId CompareRightDoc { get; private set; }
    public List<DocumentField> CompareLeftFields { get; private set; } = [];
    public List<DocumentField> CompareRightFields { get; private set; } = [];
    public int CompareSelectedLeft { get; private set; } = -1;
    public ChallengeResult? LastChallenge { get; private set; }

    public HashSet<string> NegotiateUsed { get; private set; } = [];
    public List<Leverage> CurrentLeverages { get; private set; } = [];
    public Leverage? PressedLeverage { get; private set; }
    public PressResult? LastPressResult { get; private set; }
    public string? NegotiateError { get; private set; }
    public string? LastNegotiateLine { get; private set; }
    public decimal? LastCounterOffer { get; private set; }

    public List<string> TestDriveSurfaced { get; private set; } = [];

    public SaleEngine.SaleResult? LastSale { get; private set; }
    public decimal SaleAsking { get; private set; }

    public GameEvent? CurrentEvent { get; private set; }
    public string? LastEventResult { get; private set; }

    public string? Flash { get; private set; }

    public RepairJobState? Job { get; private set; }

    public bool GameClosed => Current == Screen.Closed;

    // =====================================================================
    // BOOT
    // =====================================================================

    public void StartNewGame(int? seed = null)
    {
        Seed = seed ?? Environment.TickCount;
        Rng = new Random(Seed);
        RefreshListings();
        Current = Screen.MainMenu;
        _back.Clear();
    }

    private void RefreshListings()
        => Listings = Enumerable.Range(0, 5)
            .Select(_ =>
            {
                var (v, s) = VehicleGenerator.Generate(Rng);
                return new Listing { V = v, S = s, K = new PlayerKnowledge() };
            })
            .ToList();

    // =====================================================================
    // NAV HELPERS
    // =====================================================================

    private void Push(Screen s) { _back.Push(Current); Current = s; Flash = null; }
    private void Replace(Screen s) { Current = s; Flash = null; }

    public void Back()
    {
        Flash = null;
        Current = _back.Count > 0 ? _back.Pop() : Screen.MainMenu;
        // Console: MainLoop's while(true) re-checks the storyteller on every
        // return to the main menu, not just explicit exits — a plain "Geri"
        // out of Listings/Equipment back to MainMenu must get the same check.
        if (Current == Screen.MainMenu) MaybeFireStoryOnMainMenu();
    }

    public void GoMainMenu()
    {
        _back.Clear();
        Current = Screen.MainMenu;
        MaybeFireStoryOnMainMenu();
    }

    private void MaybeFireStoryOnMainMenu()
    {
        var ev = Story.MaybeFire(Player, Rng);
        if (ev is not null)
        {
            CurrentEvent = ev;
            Current = Screen.Event;
        }
    }

    private static readonly Dictionary<EventCategory, string> CatName = new()
    {
        [EventCategory.Dukkan] = "DÜKKAN",
        [EventCategory.Musteri] = "MÜŞTERİ",
        [EventCategory.Piyasa] = "PİYASA",
        [EventCategory.Hikaye] = "HİKÂYE",
    };
    public string EventCategoryName(EventCategory c) => CatName[c];

    public void PickEventChoice(EventChoice pick)
    {
        var ctx = new StoryContext { Player = Player, Rng = Rng };
        LastEventResult = pick.Apply(ctx);
        Current = Screen.EventResult;
    }

    public void CloseEvent() => GoMainMenu();

    // =====================================================================
    // EKİPMAN
    // =====================================================================

    public void OpenEquipment() { Push(Screen.Equipment); }

    public bool CanBuyEquipment(Equipment e) => !Player.Has(e.Id);

    public void BuyEquipment(Equipment e)
    {
        if (Player.Money < e.Cost) { Flash = "Paran yetmiyor."; return; }
        Player.Money -= e.Cost;
        Player.Equipment.Add(e.Id);
        Flash = $"{e.Name} alındı.";
    }

    // =====================================================================
    // İLANLAR
    // =====================================================================

    public void OpenListings() => Push(Screen.Listings);

    public void OpenListing(Listing l)
    {
        if (l.Sold) return;
        CurrentListing = l;
        CurrentVehicle = l.V;
        CurrentKnowledge = l.K;
        CurrentSeller = l.S;
        Push(Screen.ListingDetail);
    }

    // =====================================================================
    // TEŞHİS
    // =====================================================================

    public void OpenDiagnosis() => Push(Screen.Diagnosis);

    public decimal EffectiveCost(DiagnosisMethod m)
        => m.Id == MethodId.Lift && Player.Has("lift") ? 0m : m.Cost;

    public void RunDiagnosis(DiagnosisMethod m)
    {
        var v = CurrentVehicle!; var k = CurrentKnowledge!; var s = CurrentSeller;
        if (Player.Money < EffectiveCost(m)) { Flash = "Bu teşhis için paran yok."; return; }

        LastMethod = m;
        var result = DiagnosisEngine.Run(v, k, m, s, Rng);
        LastDiagnosisResult = result;

        if (!result.SellerRefused)
        {
            LastDiagnosisPaid = EffectiveCost(m);
            Player.Money -= LastDiagnosisPaid;
            Player.AdvanceMinutes(result.Minutes);
        }

        Replace(Screen.DiagnosisResult);
    }

    /// <summary>Console flow: RunDiagnosis → Pause → StatusTable → Pause → back to Diagnosis list.</summary>
    public void DiagnosisResultContinue() => OpenStatusTable(showBet: false, returnTo: Screen.Diagnosis);

    // =====================================================================
    // EKSPERTİZ TABLOSU
    //
    // Always entered with Replace (never Push): it's a same-level overlay of
    // whichever screen asked for it, and "Devam" jumps back to that screen by
    // explicit target rather than popping the nav stack — this keeps the
    // stack representing only the ListingDetail/Garage-level ancestry.
    // =====================================================================

    public void OpenStatusTable(bool showBet, Screen returnTo)
    {
        StatusTableShowBet = showBet;
        StatusTableReturnTo = returnTo;
        Replace(Screen.StatusTable);
    }

    public void StatusTableDone() => Current = StatusTableReturnTo;

    public void OpenObservations() => Push(Screen.Observations);

    // =====================================================================
    // BELGE MASASI
    // =====================================================================

    public void OpenDocumentDesk() => Push(Screen.DocumentDesk);

    // ReadDocumentPick and the whole CompareDocuments chain are single-shot
    // sub-flows of DocumentDesk (which is itself a loop, like the console).
    // They Push once to leave the loop, then Replace through their own
    // internal steps, so "Devam"/Back always lands back on DocumentDesk.

    public void OpenReadDocumentPick() => Push(Screen.ReadDocumentPick);

    public void OpenReadDocument(DocumentId d)
    {
        ReadDoc = d;
        Replace(Screen.ReadDocumentView);
    }

    public void OpenComparePickLeft()
    {
        CompareSelectedLeft = -1;
        Push(Screen.CompareDocumentsPickLeft);
    }

    public void PickCompareLeft(DocumentId d)
    {
        CompareLeftDoc = d;
        Replace(Screen.CompareDocumentsPickRight);
    }

    public void PickCompareRight(DocumentId d)
    {
        CompareRightDoc = d;
        CompareLeftFields = DocumentDesk.Fields(CurrentVehicle!, CompareLeftDoc);
        CompareRightFields = DocumentDesk.Fields(CurrentVehicle!, CompareRightDoc);
        CompareSelectedLeft = -1;
        Replace(Screen.CompareDocumentsView);
    }

    public void SelectCompareLeftRow(int i) => CompareSelectedLeft = i;

    public void ChallengeCompareRow(int rightIndex)
    {
        if (CompareSelectedLeft < 0) return;
        var left = CompareLeftFields[CompareSelectedLeft];
        var right = CompareRightFields[rightIndex];

        var result = DocumentDesk.Challenge(CurrentVehicle!, CurrentKnowledge!, left, right);
        Player.AdvanceMinutes(8);
        LastChallenge = result;

        if (result.IsContradiction)
        {
            string id = $"{left.Key}|{right.Key}";
            if (CurrentKnowledge!.ProvenContradictions.Add(id))
                CurrentKnowledge.Observe(result.Explanation ?? result.Verdict, ObservationKind.Contradiction, MethodId.Belgeler);
        }
        else if (CurrentSeller is not null)
        {
            CurrentSeller.PatienceRemaining = Math.Max(0, CurrentSeller.PatienceRemaining - 1);
        }

        Replace(Screen.CompareResult);
    }

    // =====================================================================
    // SATICIYA SORU — single-shot: pick a group, see the reply, Back() lands
    // straight on ListingDetail (console: not a loop, ends after one Pause).
    // =====================================================================

    public void OpenAskSeller() => Push(Screen.AskSellerPickGroup);

    public void AskSellerAbout(SystemGroup g)
    {
        var s = CurrentSeller!; var v = CurrentVehicle!; var k = CurrentKnowledge!;
        LastAskGroup = g;
        var reply = s.AskAbout(g, v, k, Rng);
        LastAskReply = reply;

        var surfaced = reply.Tell ?? s.RollFalseTell(Rng);
        LastAskTell = surfaced;
        if (surfaced is not null) k.Observations.Add(surfaced);

        Player.AdvanceMinutes(3);
        Replace(Screen.AskSellerResult);
    }

    // =====================================================================
    // PAZARLIK
    // =====================================================================

    public void OpenNegotiate()
    {
        NegotiateUsed = [];
        NegotiateError = null;
        LastNegotiateLine = null;
        RefreshLeverages();
        Push(Screen.Negotiate);
    }

    private void RefreshLeverages()
    {
        var l = CurrentListing!;
        CurrentLeverages = NegotiationEngine.Available(l.V, l.S, l.K)
            .Where(x => !NegotiateUsed.Contains(x.Id))
            .ToList();
    }

    // PressLeverage/LeverageResult are overlays of the Negotiate loop, not
    // nav-stack children of it — they Replace and jump back to Negotiate
    // explicitly, mirroring PressLeverageScreen being called once and
    // returning into NegotiateScreen's own while(true).

    public void OpenPressLeverage()
    {
        RefreshLeverages();
        if (CurrentLeverages.Count == 0) { Flash = "Kullanacak kozun yok."; return; }
        Replace(Screen.PressLeverage);
    }

    public void PressLeverage(Leverage lev)
    {
        var l = CurrentListing!;
        NegotiateUsed.Add(lev.Id);
        PressedLeverage = lev;

        var res = NegotiationEngine.Press(l.V, l.S, lev, Rng);
        Player.AdvanceMinutes(4);
        LastPressResult = res;

        Replace(Screen.LeverageResult);
    }

    public void LeverageResultContinue()
    {
        var l = CurrentListing!;
        if (l.S.WalkedAway) { l.Sold = true; GoMainMenu(); return; }
        RefreshLeverages();
        Current = Screen.Negotiate;
    }

    /// <summary>Cancel out of the leverage-picking screen back to Negotiate.</summary>
    public void CancelPressLeverage() => Current = Screen.Negotiate;

    public void MakeOffer(decimal offer)
    {
        NegotiateError = null;
        var l = CurrentListing!; var v = l.V; var s = l.S;

        if (offer <= 0) return;
        if (offer > Player.Money) { NegotiateError = "O kadar paran yok."; return; }

        var outcome = s.Negotiate(offer, v, Rng);
        LastNegotiateLine = outcome.Line;
        LastCounterOffer = outcome.Accepted ? null : outcome.Counter;

        if (s.WalkedAway)
        {
            l.Sold = true;
            Flash = null;
            return; // outcome shown inline on Negotiate; user then leaves
        }

        if (!outcome.Accepted)
        {
            return; // stays on Negotiate; counter offer shown inline
        }

        // --- purchase ---
        Player.Money -= offer;
        Player.PurchasePrice = offer;
        Player.RepairSpend = 0m;
        Player.OwnedVehicle = v;
        Player.OwnedKnowledge = l.K;
        l.Sold = true;

        Flash = $"ARAÇ SENİN. {offer:N0}₺ ödedin.";
        GoMainMenu();
    }

    public bool NegotiateSellerWalked => CurrentListing?.S.WalkedAway ?? false;

    public void LeaveNegotiate() => GoMainMenu();

    // =====================================================================
    // GARAJ
    // =====================================================================

    public void OpenGarage()
    {
        if (Player.OwnedVehicle is null) return;
        CurrentVehicle = Player.OwnedVehicle;
        CurrentKnowledge = Player.OwnedKnowledge;
        CurrentSeller = null;
        CurrentListing = null;
        Push(Screen.Garage);
    }

    public void OpenRepair() => Push(Screen.Repair);

    public IEnumerable<PartDefinition> RepairablePartsList()
    {
        var k = CurrentKnowledge!;
        return PartCatalog.All.Where(p => !k.For(p.Id).IsUnexamined && k.For(p.Id).Mid < 72f);
    }

    public void OpenTestDrive()
    {
        var v = CurrentVehicle!; var k = CurrentKnowledge!;
        TestDriveSurfaced = ScamEngine.AdvanceKm(v, 200);
        Player.AdvanceMinutes(180);
        foreach (var text in TestDriveSurfaced)
        {
            var o = new Observation(text, ObservationKind.Finding, MethodId.TestSurusuUzun);
            k.Observations.Add(o);
        }
        Push(Screen.TestDriveResult);
    }

    // =====================================================================
    // ONARIM İŞİ (söküm → cıvata → onarım → montaj/tork)
    // =====================================================================

    public sealed class RepairJobState
    {
        public required PartDefinition Target;
        public required List<PartInstance> ToRemove;
        public int RemoveIndex;
        public decimal JobCost;
        public int JobMinutes;

        public BoltState? PendingBoltState;
        public WrenchApproach[] PendingOptions = [];
        public string? LastRemoveMessage;
        public bool LastRemoveStripped;

        public string? RepairMessage;
        public decimal RepairCost;

        public bool HasWrench;
        public List<(string PartName, string Message, bool Flaw)> TorqueResults = [];
        public bool AnyTorqueFlaw;

        public bool Cancelled;
    }

    public void StartRepairJob(PartDefinition target)
    {
        var v = CurrentVehicle!;
        decimal partCost = v.Part(target.Id).TrueRepairCost();
        if (Player.Money < partCost)
        {
            Flash = $"Bu iş kabaca {partCost:N0}₺ tutar, paran yetmiyor. Önce sat.";
            return;
        }

        var chain = Disassembly.RemovalChain(target.Id);
        var toRemove = chain.Select(id => v.Part(id)).Append(v.Part(target.Id)).ToList();

        Job = new RepairJobState
        {
            Target = target,
            ToRemove = toRemove,
            HasWrench = Player.Has("tork_anahtari"),
        };

        // Replace, not Push: RepairJob is an overlay of Repair (which stays
        // on the nav stack); Finish/Cancel jump back to Repair directly.
        Replace(Screen.RepairJob);
        AdvanceRemoval();
    }

    /// <summary>Auto-resolves clean bolts (no decision needed); stops at the first
    /// part that needs a player choice, or moves on to the repair phase.</summary>
    private void AdvanceRemoval()
    {
        var job = Job!;
        while (job.RemoveIndex < job.ToRemove.Count)
        {
            var part = job.ToRemove[job.RemoveIndex];
            var state = Disassembly.BoltsFor(part, Rng);

            if (state == BoltState.Temiz)
            {
                var auto = Disassembly.TryRemove(part, WrenchApproach.Normal, Rng);
                job.JobCost += auto.Cost;
                job.JobMinutes += auto.Minutes;
                job.RemoveIndex++;
                continue;
            }

            job.PendingBoltState = state;
            job.PendingOptions = Disassembly.Options(state);
            job.LastRemoveMessage = null;
            return;
        }

        job.PendingBoltState = null;
        PerformRepair();
    }

    public (int Minutes, decimal Cost, float Risk, string RiskLabel) BoltApproachInfo(WrenchApproach a)
    {
        var job = Job!;
        var (m, cst) = Disassembly.Effort(a);
        float risk = Disassembly.StripRisk(job.PendingBoltState!.Value, a);
        string label = a == WrenchApproach.DrillHelicoil ? "kesin çözüm"
                     : risk <= 0.05f ? "güvenli"
                     : risk < 0.20f ? "az riskli"
                     : risk < 0.40f ? "riskli" : "ÇOK RİSKLİ";
        return (m, cst, risk, label);
    }

    public void ChooseBoltApproach(WrenchApproach a)
    {
        var job = Job!;
        var part = job.ToRemove[job.RemoveIndex];
        var outcome = Disassembly.TryRemove(part, a, Rng);
        job.JobCost += outcome.Cost;
        job.JobMinutes += outcome.Minutes;
        job.LastRemoveMessage = outcome.Message;
        job.LastRemoveStripped = outcome.Stripped;

        if (outcome.Removed)
        {
            job.RemoveIndex++;
            job.PendingBoltState = null;
        }
        else
        {
            // stripped: stays on this part, next choice is DrillHelicoil only
            job.PendingBoltState = BoltState.Siyrik;
            job.PendingOptions = Disassembly.Options(BoltState.Siyrik);
        }
    }

    public void BoltStepContinue()
    {
        var job = Job!;
        if (job.LastRemoveMessage is null) return; // no decision made yet
        job.LastRemoveMessage = null;
        if (job.PendingBoltState is null) AdvanceRemoval();
        // else: cıvata sıyrıldı, aynı parça için sadece DrillHelicoil kaldı
    }

    private void PerformRepair()
    {
        var v = CurrentVehicle!;
        var job = Job!;
        var (repCost, repHours, repMsg) = RepairEngine.Repair(v, job.Target.Id, Rng);
        repCost = Cash.RoundTo(repCost * (decimal)Player.PartsMultiplier, 100);
        job.JobCost += repCost;
        job.JobMinutes += (int)(repHours * 60);
        job.RepairMessage = repMsg;
        job.RepairCost = repCost;
    }

    public void RepairDoneContinue()
    {
        var job = Job!;
        if (job.HasWrench)
        {
            ApplyTorque(TorqueChoice.Normal);
        }
        // else UI shows a torque-choice prompt (screen stays RepairJob, job.RepairMessage cleared)
        job.RepairMessage = null;
    }

    public void ApplyTorque(TorqueChoice choice)
    {
        var job = Job!;
        if (!job.HasWrench)
            job.JobMinutes += choice switch { TorqueChoice.Dikkatli => 20, TorqueChoice.Hizli => 5, _ => 10 };

        job.TorqueResults = [];
        job.AnyTorqueFlaw = false;
        foreach (var part in job.ToRemove)
        {
            var (msg, flaw) = Disassembly.Torque(part, choice, job.HasWrench, Rng);
            job.TorqueResults.Add((part.Def.Name, msg, flaw));
            if (flaw) job.AnyTorqueFlaw = true;
        }
    }

    public void FinishRepairJob()
    {
        var job = Job!;
        var v = CurrentVehicle!; var k = CurrentKnowledge!;
        Player.Money -= job.JobCost;
        Player.RepairSpend += job.JobCost;
        Player.AdvanceMinutes(job.JobMinutes);
        k.Update(job.Target.Id, new ConfidenceRange(86f, 97f, ConfidenceRange.MaxConfidence));
        Job = null;
        Current = Screen.Repair;
    }

    public void CancelRepairJob()
    {
        Job = null;
        Current = Screen.Repair;
    }

    // =====================================================================
    // SATIŞ + GERÇEĞİN AÇIKLANMASI
    // =====================================================================

    public void OpenSell() => Push(Screen.Sell);

    public void SubmitAskingPrice(decimal asking)
    {
        if (asking <= 0) return;
        SaleAsking = asking;
        var v = CurrentVehicle!;
        LastSale = SaleEngine.Evaluate(v, asking, Rng);
        Push(Screen.SellResult);
    }

    public void AcceptSaleAtAsking() => FinalizeSale(SaleAsking);
    public void AcceptSaleAtOffer() => FinalizeSale(LastSale!.Offer);
    public void DeclineSale() => Current = Screen.Sell;

    private void FinalizeSale(decimal finalPrice)
    {
        var v = CurrentVehicle!; var k = CurrentKnowledge!;
        decimal spent = Player.PurchasePrice + Player.RepairSpend;

        Player.Money += finalPrice;
        decimal profit = finalPrice - spent;
        Player.Reputation += profit > 0 ? 3f : -2f;
        Player.CarsSold++;

        int undisclosed = v.AllDefects.Count(d => d.SurfacesAfterKm == 0);
        if (undisclosed > 0 && LastSale!.BuyerFindings.Count < undisclosed) Player.RiskySales++;

        LastSaleProfit = profit;
        Current = Screen.TruthReveal;
    }

    public decimal LastSaleProfit { get; private set; }

    public void TruthRevealDone()
    {
        Player.OwnedVehicle = null;
        Player.OwnedKnowledge = null;
        RefreshListings();
        GoMainMenu();
    }

    // =====================================================================
    // OWNER LABELS (only used inside TruthReveal — the one screen allowed
    // to read the truth)
    // =====================================================================

    public static string OwnerName(OwnerProfile p) => p switch
    {
        OwnerProfile.YasliCift => "Yaşlı çift",
        OwnerProfile.GencSurucu => "Genç sürücü",
        OwnerProfile.Taksi => "Taksi",
        OwnerProfile.KiralikFilo => "Kiralık filo",
        OwnerProfile.MerakliTamirci => "Meraklı / tamirci",
        OwnerProfile.UzunSurePark => "Uzun süre park edilmiş",
        OwnerProfile.SehirIci => "Şehir içi kullanım",
        OwnerProfile.UzunYol => "Uzun yol",
        _ => p.ToString()
    };

    public static string OwnerHint(OwnerProfile p) => p switch
    {
        OwnerProfile.YasliCift => "İmza: düşük km ama ölü akü, sertleşmiş kauçuk, paslı diskler, tertemiz motor.",
        OwnerProfile.GencSurucu => "İmza: bitik debriyaj, aşırı yıpranmış fren, zorlanmış süspansiyon, sağlam motor.",
        OwnerProfile.Taksi => "İmza: yüksek km ama sapasağlam motor, bitmiş iç mekan, çökmüş süspansiyon.",
        OwnerProfile.KiralikFilo => "İmza: hasarlı iç mekan, kötü kaporta, çok el değiştirmiş.",
        OwnerProfile.MerakliTamirci => "İmza: iyi motor, amatör kablo işi, elden geçmiş süspansiyon.",
        OwnerProfile.UzunSurePark => "İmza: kemirgen kablo hasarı, çürümüş soğutma, sertleşmiş lastikler.",
        OwnerProfile.SehirIci => "İmza: düşük km ama içten yorgun motor — çok soğuk çalıştırma.",
        OwnerProfile.UzunYol => "İmza: yüksek km ama düşük aşınma. Otoyol kilometresi motoru yormaz.",
        _ => ""
    };

    public void QuitGame() => Current = Screen.Closed;
}
