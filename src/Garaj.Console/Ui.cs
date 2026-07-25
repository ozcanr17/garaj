using System.Globalization;
using Garaj.Core;
using Sys = System.Console;

namespace GarajApp;

/// <summary>
/// Sunum katmanı. KURAL: bu sınıf VehicleInstance.Condition'a ASLA erişmez.
/// Sadece PlayerKnowledge okur. Tek istisna: oyun sonu "gerçek" ekranı.
/// </summary>
internal static class Ui
{
    /// <summary>Türkçe'de i→İ ve ı→I. Invariant ToUpper "SATıŞ" gibi bozuk çıktı üretir.</summary>
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    public static void Header(string title)
    {
        Sys.WriteLine();
        Write("─── ", ConsoleColor.DarkGray);
        Write(title.ToUpper(Tr), ConsoleColor.Yellow);
        Write(" " + new string('─', Math.Max(2, 62 - title.Length)), ConsoleColor.DarkGray);
        Sys.WriteLine();
    }

    public static void Rule() => WriteLine(new string('─', 68), ConsoleColor.DarkGray);

    public static void Write(string s, ConsoleColor c)
    {
        var old = Sys.ForegroundColor;
        Sys.ForegroundColor = c;
        Sys.Write(s);
        Sys.ForegroundColor = old;
    }

    public static void WriteLine(string s, ConsoleColor c)
    {
        Write(s, c);
        Sys.WriteLine();
    }

    public static string Money(decimal m) => $"₺{m:N0}";

    // -----------------------------------------------------------------------
    // GÜVEN BANDI ÇİZİMİ — oyunun görsel imzası
    //
    // Dolu bölge "durum" DEĞİL, oyuncunun inandığı ARALIK'tır.
    // Bant ne kadar dar ve ne kadar sağdaysa o kadar iyi haber.
    // -----------------------------------------------------------------------

    public static string Bar(ConfidenceRange r, int width = 22)
    {
        if (r.IsUnexamined)
            return "·" + string.Join("", Enumerable.Repeat(" ·", width / 2 - 1));

        int lo = (int)MathF.Round(r.Min / 100f * width);
        int hi = (int)MathF.Round(r.Max / 100f * width);
        hi = Math.Max(hi, lo + 1);

        var chars = new char[width];
        for (int i = 0; i < width; i++)
            chars[i] = i >= lo && i < hi ? '█' : '░';

        return new string(chars);
    }

    public static ConsoleColor ColorFor(ConfidenceRange r) => r switch
    {
        { IsUnexamined: true } => ConsoleColor.DarkGray,
        { Mid: >= 70f } => ConsoleColor.Green,
        { Mid: >= 50f } => ConsoleColor.Yellow,
        { Mid: >= 32f } => ConsoleColor.DarkYellow,
        _ => ConsoleColor.Red
    };

    /// <summary>
    /// Bir satır durum gösterimi. Blueprint §3.3'ün doğrudan uygulaması:
    /// asla tek bir kesin sayı yok — bant, sözel yargı ve güven yüzdesi var.
    /// </summary>
    public static void ConditionLine(string label, ConfidenceRange r, int labelWidth = 22)
    {
        Sys.Write("  " + label.PadRight(labelWidth));
        Write("[", ConsoleColor.DarkGray);
        Write(Bar(r), ColorFor(r));
        Write("] ", ConsoleColor.DarkGray);

        if (r.IsUnexamined)
        {
            Write("?".PadRight(9), ConsoleColor.DarkGray);
            WriteLine("incelenmedi", ConsoleColor.DarkGray);
            return;
        }

        Write($"{r.Min:F0}-{r.Max:F0}".PadRight(9), ConsoleColor.Gray);
        Write(r.Phrase.PadRight(26), ColorFor(r));
        WriteLine($"güven %{r.Confidence * 100:F0}", ConsoleColor.DarkGray);
    }

    // -----------------------------------------------------------------------

    public static void PrintObservation(Observation o)
    {
        var (prefix, color) = o.Kind switch
        {
            ObservationKind.Finding       => ("  ▸ BULGU       ", ConsoleColor.Red),
            ObservationKind.Suspicion     => ("  ? ŞÜPHE       ", ConsoleColor.Magenta),
            ObservationKind.Contradiction => ("  ! ÇELİŞKİ     ", ConsoleColor.Cyan),
            ObservationKind.SellerTell    => ("  ~ SATICI      ", ConsoleColor.DarkCyan),
            _                             => ("  · not         ", ConsoleColor.DarkGray),
        };

        Write(prefix, color);
        WriteLine(Wrap(o.Text, 52, 16), color == ConsoleColor.DarkGray ? ConsoleColor.DarkGray : ConsoleColor.White);
    }

    public static string Wrap(string text, int width, int indent)
    {
        var words = text.Split(' ');
        var lines = new List<string>();
        var cur = "";

        foreach (var w in words)
        {
            if (cur.Length + w.Length + 1 > width) { lines.Add(cur); cur = w; }
            else cur = cur.Length == 0 ? w : cur + " " + w;
        }
        if (cur.Length > 0) lines.Add(cur);

        return string.Join("\n" + new string(' ', indent), lines);
    }

    // -----------------------------------------------------------------------

    public static int Menu(string prompt, params string[] options)
    {
        Sys.WriteLine();
        for (int i = 0; i < options.Length; i++)
        {
            Write($"  [{i + 1}] ", ConsoleColor.Yellow);
            Sys.WriteLine(options[i]);
        }
        Write($"  [0] ", ConsoleColor.DarkGray);
        WriteLine("Geri", ConsoleColor.DarkGray);

        while (true)
        {
            Sys.WriteLine();
            Write($"{prompt} > ", ConsoleColor.Yellow);
            var input = Sys.ReadLine()?.Trim();
            if (int.TryParse(input, out int n) && n >= 0 && n <= options.Length) return n;
            WriteLine("  Geçersiz seçim.", ConsoleColor.Red);
        }
    }

    public static decimal? AskMoney(string prompt)
    {
        Write($"{prompt} > ", ConsoleColor.Yellow);
        var input = Sys.ReadLine()?.Trim().Replace(".", "").Replace(",", "").Replace("₺", "");
        return decimal.TryParse(input, out var v) ? v : null;
    }

    public static void Pause()
    {
        Sys.WriteLine();
        WriteLine("  [Devam etmek için Enter]", ConsoleColor.DarkGray);
        Sys.ReadLine();
    }

    public static void Clear()
    {
        try { Sys.Clear(); } catch { /* yönlendirilmiş çıktı */ }
    }
}
