using Garaj.Core;

namespace Garaj.Web;

/// <summary>
/// Presentation-only helpers, the web equivalent of GarajApp.Ui. Same rule
/// applies: nothing here may read PartInstance.Condition — only
/// ConfidenceRange (the player's belief) and plain data the caller passes in.
/// </summary>
public static class UiFormat
{
    public static string Money(decimal m) => $"₺{m:N0}";

    public static string ColorClass(ConfidenceRange r) => r switch
    {
        { IsUnexamined: true } => "c-text-gray",
        { Mid: >= 70f } => "c-text-green",
        { Mid: >= 50f } => "c-text-yellow",
        { Mid: >= 32f } => "c-text-darkyellow",
        _ => "c-text-red"
    };

    public static string BarFillClass(ConfidenceRange r) => r switch
    {
        { IsUnexamined: true } => "unexamined",
        { Mid: >= 70f } => "c-green",
        { Mid: >= 50f } => "c-yellow",
        { Mid: >= 32f } => "c-darkyellow",
        _ => "c-red"
    };

    public static (double LeftPct, double WidthPct) BarGeometry(ConfidenceRange r)
    {
        if (r.IsUnexamined) return (0, 0);
        double lo = Math.Clamp(r.Min / 100.0, 0, 1) * 100.0;
        double hi = Math.Clamp(r.Max / 100.0, 0, 1) * 100.0;
        if (hi - lo < 2) hi = lo + 2;
        return (lo, hi - lo);
    }

    public static (string Prefix, string CssClass) ObservationStyle(ObservationKind k) => k switch
    {
        ObservationKind.Finding => ("▸ BULGU", "finding"),
        ObservationKind.Suspicion => ("? ŞÜPHE", "suspicion"),
        ObservationKind.Contradiction => ("! ÇELİŞKİ", "contradiction"),
        ObservationKind.SellerTell => ("~ SATICI", "sellertell"),
        ObservationKind.Detail => ("· gördüğün", "detail"),
        _ => ("· not", "detail"),
    };

    public static string GroupName(SystemGroup g) => PartCatalog.GroupName(g);
}
