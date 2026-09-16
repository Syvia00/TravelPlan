using System.Text;
using SkiaSharp;
using TravelPlan.Shared.DTOs.TripMemories.Reports;
using TravelPlan.Shared.Models.Enums;

namespace TravelPlan.Api.Services.Reports;

/// <summary>
/// Draws a report exactly once per render (via SKPictureRecorder, which also measures the
/// report's natural height in the same pass), then replays that single recorded picture onto a
/// raster surface for PNG and a PDF page for PDF — one drawing routine, two output formats, per
/// the "draw once" requirement. Itinerary (chronological) and MemorySummary (poster-style) both
/// flow through the same Draw() entry point and the same primitives (fonts, colors,
/// DrawSectionHeader, DrawRow) — only the body composition branches on report.ReportType.
/// </summary>
public static class TripReportSkiaRenderer
{
    private const int MaxHighlights = 8;
    private const float Width = 816f;
    private const float MarginX = 48f;
    private const float ContentWidth = Width - MarginX * 2;
    private const float MaxHeight = 20000f;

    private static readonly SKColor Desk = SKColor.Parse("#F6F4EE");
    private static readonly SKColor Accent = SKColor.Parse("#2C7DA0");
    private static readonly SKColor AccentDeep = SKColor.Parse("#1D5A78");
    private static readonly SKColor Ink = SKColor.Parse("#2F3E46");
    private static readonly SKColor InkSoft = SKColor.Parse("#5C6B73");
    private static readonly SKColor Divider = SKColor.Parse("#E3DEC9");

    public static byte[] RenderPng(TripReportDto report)
    {
        var (picture, height) = Record(report);
        using (picture)
        {
            using var surface = SKSurface.Create(new SKImageInfo((int)Width, (int)Math.Ceiling(height)));
            surface.Canvas.Clear(Desk);
            surface.Canvas.DrawPicture(picture);
            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }
    }

    public static byte[] RenderPdf(TripReportDto report)
    {
        var (picture, height) = Record(report);
        using (picture)
        {
            using var stream = new MemoryStream();
            using (var document = SKDocument.CreatePdf(stream))
            {
                var canvas = document.BeginPage(Width, (float)Math.Ceiling(height));
                canvas.Clear(Desk);
                canvas.DrawPicture(picture);
                document.EndPage();
                document.Close();
            }
            return stream.ToArray();
        }
    }

    private static (SKPicture Picture, float Height) Record(TripReportDto report)
    {
        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(new SKRect(0, 0, Width, MaxHeight));
        canvas.Clear(Desk);
        var height = Draw(canvas, report);
        return (recorder.EndRecording(), height);
    }

    private static float Draw(SKCanvas canvas, TripReportDto report)
    {
        using var bodyFont = new SKFont(SKTypeface.Default, 13);
        using var smallFont = new SKFont(SKTypeface.Default, 11);
        using var titleFont = new SKFont(SKTypeface.FromFamilyName(null, SKFontStyle.Bold), 28);
        using var sectionFont = new SKFont(SKTypeface.FromFamilyName(null, SKFontStyle.Bold), 16);
        using var labelFont = new SKFont(SKTypeface.FromFamilyName(null, SKFontStyle.Bold), 13);
        using var statFont = new SKFont(SKTypeface.FromFamilyName(null, SKFontStyle.Bold), 24);
        using var badgeFont = new SKFont(SKTypeface.FromFamilyName(null, SKFontStyle.Bold), 10);

        const float x = MarginX;
        var y = 40f;
        var isMemory = report.ReportType == TripMemoryReportType.MemorySummary;

        // Header block
        using (var headerPaint = new SKPaint { IsAntialias = true, Color = Accent })
        {
            canvas.DrawRoundRect(new SKRect(x, y, Width - MarginX, y + 120), 10, 10, headerPaint);
        }

        using (var whitePaint = new SKPaint { IsAntialias = true, Color = SKColors.White })
        {
            canvas.DrawText(isMemory ? "MEMORY" : "ITINERARY", x + 24, y + 20, SKTextAlign.Left, badgeFont, whitePaint);
            canvas.DrawText(report.Trip.Title, x + 24, y + 54, SKTextAlign.Left, titleFont, whitePaint);

            var dateRange =
                $"{report.Trip.StartDate:MMM d, yyyy} – {report.Trip.EndDate:MMM d, yyyy} · {report.Trip.DurationDays} days";
            canvas.DrawText(dateRange, x + 24, y + 82, SKTextAlign.Left, bodyFont, whitePaint);

            if (!string.IsNullOrWhiteSpace(report.Trip.Description))
            {
                canvas.DrawText(Truncate(report.Trip.Description!, 90), x + 24, y + 104, SKTextAlign.Left, smallFont, whitePaint);
            }
        }

        y += 150;

        y = isMemory
            ? DrawMemorySummaryBody(canvas, report, x, y, statFont, smallFont, sectionFont, labelFont, bodyFont)
            : DrawItineraryBody(canvas, report, x, y, sectionFont, labelFont, bodyFont, smallFont);

        y += 16;
        using (var footerPaint = new SKPaint { IsAntialias = true, Color = InkSoft })
        {
            canvas.DrawText($"Generated {report.GeneratedAt:MMM d, yyyy h:mm tt} UTC · TravelPlan", x, y, SKTextAlign.Left, smallFont, footerPaint);
        }
        y += 20;

        return y;
    }

    private static float DrawItineraryBody(
        SKCanvas canvas, TripReportDto report, float x, float y,
        SKFont sectionFont, SKFont labelFont, SKFont bodyFont, SKFont smallFont)
    {
        y = DrawDestinations(canvas, report, x, y, sectionFont, bodyFont, smallFont, "Destinations");

        if (report.PlanItems.Count > 0)
        {
            y = DrawSectionHeader(canvas, "Itinerary", x, y, sectionFont);
            DateOnly? lastDate = null;
            using var dayPaint = new SKPaint { IsAntialias = true, Color = AccentDeep };
            foreach (var item in report.PlanItems)
            {
                if (item.Date != lastDate)
                {
                    lastDate = item.Date;
                    canvas.DrawText(item.Date!.Value.ToString("dddd, MMM d"), x, y, SKTextAlign.Left, labelFont, dayPaint);
                    y += 22;
                }

                var time = item.Time?.ToString("h:mm tt") ?? "Unscheduled";
                y = DrawRow(canvas, item.Title, time, x + 16, y, ContentWidth - 16, bodyFont, smallFont);
            }
            y += 14;
        }

        if (report.Accommodations.Count > 0)
        {
            y = DrawSectionHeader(canvas, "Stays", x, y, sectionFont);
            foreach (var a in report.Accommodations)
            {
                var sub = $"{a.CheckIn:MMM d, h:mm tt} -> {a.CheckOut:MMM d, h:mm tt} · {a.Nights} night{Plural(a.Nights)}"
                    + (string.IsNullOrWhiteSpace(a.ConfirmationCode) ? "" : $" · Conf# {a.ConfirmationCode}");
                y = DrawRow(canvas, a.Name, sub, x, y, ContentWidth, bodyFont, smallFont, twoLine: true);
            }
            y += 14;
        }

        if (report.TravelLegs.Count > 0)
        {
            y = DrawSectionHeader(canvas, "Transport", x, y, sectionFont);
            foreach (var l in report.TravelLegs)
            {
                var title = $"{l.TransportType}: {l.DepartureLocation} -> {l.ArrivalLocation}";
                var sub = $"{l.DepartureTime:MMM d, h:mm tt} -> {l.ArrivalTime:h:mm tt} · {l.DurationMinutes} min"
                    + (string.IsNullOrWhiteSpace(l.ConfirmationCode) ? "" : $" · Conf# {l.ConfirmationCode}");
                y = DrawRow(canvas, title, sub, x, y, ContentWidth, bodyFont, smallFont, twoLine: true);
            }
            y += 14;
        }

        return DrawBudget(canvas, report, x, y, sectionFont, labelFont, bodyFont, smallFont);
    }

    private static float DrawMemorySummaryBody(
        SKCanvas canvas, TripReportDto report, float x, float y,
        SKFont statFont, SKFont statLabelFont, SKFont sectionFont, SKFont labelFont, SKFont bodyFont)
    {
        y = DrawStatsStrip(canvas, report, x, y, statFont, statLabelFont);
        y += 10;

        y = DrawDestinations(canvas, report, x, y, sectionFont, bodyFont, statLabelFont, "Route");

        y = DrawSectionHeader(canvas, "Highlights", x, y, sectionFont);
        if (report.PlanItems.Count == 0)
        {
            using var mutedPaint = new SKPaint { IsAntialias = true, Color = InkSoft };
            canvas.DrawText("No highlights recorded.", x, y, SKTextAlign.Left, bodyFont, mutedPaint);
            y += 24;
        }
        else
        {
            foreach (var item in report.PlanItems.Take(MaxHighlights))
            {
                var when = item.Date is { } date
                    ? (item.Time is { } time ? $"{date:MMM d}, {time:h:mm tt}" : $"{date:MMM d}")
                    : string.Empty;
                y = DrawRow(canvas, item.Title, when, x, y, ContentWidth, bodyFont, statLabelFont);
            }
        }
        y += 14;

        y = DrawBudget(canvas, report, x, y, sectionFont, labelFont, bodyFont, statLabelFont);

        y = DrawSectionHeader(canvas, "Reflection", x, y, sectionFont);
        using (var reflectionPaint = new SKPaint
               {
                   IsAntialias = true,
                   Color = string.IsNullOrWhiteSpace(report.Reflection) ? InkSoft : Ink,
               })
        {
            var text = string.IsNullOrWhiteSpace(report.Reflection) ? "No reflection written yet." : report.Reflection!;
            y = DrawWrappedText(canvas, text, x, y, ContentWidth, bodyFont, reflectionPaint);
        }
        y += 10;

        return y;
    }

    private static float DrawStatsStrip(SKCanvas canvas, TripReportDto report, float x, float y, SKFont numberFont, SKFont labelFont)
    {
        var stats = new (string Label, string Value)[]
        {
            ("Days", report.Trip.DurationDays.ToString()),
            ("Destinations", report.Destinations.Count.ToString()),
            ("Nights stayed", report.Accommodations.Sum(a => a.Nights).ToString()),
            ("Activities", report.PlanItems.Count.ToString()),
            ("Spent", FormatMoney(report.BudgetSummary.GrandTotal, report.BudgetSummary.Currency)),
        };

        var cellWidth = ContentWidth / stats.Length;
        using var numberPaint = new SKPaint { IsAntialias = true, Color = AccentDeep };
        using var labelPaint = new SKPaint { IsAntialias = true, Color = InkSoft };
        using var linePaint = new SKPaint { IsAntialias = true, Color = Divider, StrokeWidth = 1 };

        for (var i = 0; i < stats.Length; i++)
        {
            var cx = x + cellWidth * i + cellWidth / 2;
            canvas.DrawText(stats[i].Value, cx, y + 20, SKTextAlign.Center, numberFont, numberPaint);
            canvas.DrawText(stats[i].Label, cx, y + 38, SKTextAlign.Center, labelFont, labelPaint);
        }

        canvas.DrawLine(x, y + 54, x + ContentWidth, y + 54, linePaint);
        return y + 66;
    }

    private static float DrawDestinations(
        SKCanvas canvas, TripReportDto report, float x, float y,
        SKFont sectionFont, SKFont bodyFont, SKFont smallFont, string heading)
    {
        if (report.Destinations.Count == 0)
        {
            return y;
        }

        y = DrawSectionHeader(canvas, heading, x, y, sectionFont);
        foreach (var d in report.Destinations)
        {
            var range = d.ExitDate is { } exit
                ? $"{d.EntryDate:MMM d} – {exit:MMM d} · {d.Nights} night{Plural(d.Nights ?? 0)}"
                : $"From {d.EntryDate:MMM d}";
            y = DrawRow(canvas, $"{d.Name} ({d.CountryCode})", range, x, y, ContentWidth, bodyFont, smallFont);
        }
        return y + 14;
    }

    private static float DrawBudget(
        SKCanvas canvas, TripReportDto report, float x, float y,
        SKFont sectionFont, SKFont labelFont, SKFont bodyFont, SKFont smallFont)
    {
        y = DrawSectionHeader(canvas, "Budget", x, y, sectionFont);
        using (var totalPaint = new SKPaint { IsAntialias = true, Color = AccentDeep })
        {
            canvas.DrawText($"{FormatMoney(report.BudgetSummary.GrandTotal, report.BudgetSummary.Currency)} total", x, y, SKTextAlign.Left, labelFont, totalPaint);
        }
        y += 26;

        if (report.BudgetSummary.Categories.Count == 0)
        {
            using var mutedPaint = new SKPaint { IsAntialias = true, Color = InkSoft };
            canvas.DrawText("No expenses recorded.", x, y, SKTextAlign.Left, bodyFont, mutedPaint);
            return y + 24;
        }

        foreach (var cat in report.BudgetSummary.Categories)
        {
            var sub = $"{FormatMoney(cat.Total, report.BudgetSummary.Currency)} ({cat.Percentage:0.#}%)";
            y = DrawRow(canvas, cat.Category.ToString(), sub, x, y, ContentWidth, bodyFont, smallFont);
        }

        return y;
    }

    private static float DrawSectionHeader(SKCanvas canvas, string title, float x, float y, SKFont font)
    {
        using var textPaint = new SKPaint { IsAntialias = true, Color = AccentDeep };
        canvas.DrawText(title, x, y, SKTextAlign.Left, font, textPaint);

        using var linePaint = new SKPaint { IsAntialias = true, Color = Divider, StrokeWidth = 1, Style = SKPaintStyle.Stroke };
        canvas.DrawLine(x, y + 10, x + ContentWidth, y + 10, linePaint);

        return y + 34;
    }

    private static float DrawRow(
        SKCanvas canvas, string title, string subtitle, float x, float y, float width,
        SKFont titleFont, SKFont subFont, bool twoLine = false)
    {
        using var titlePaint = new SKPaint { IsAntialias = true, Color = Ink };
        using var subPaint = new SKPaint { IsAntialias = true, Color = InkSoft };
        using var linePaint = new SKPaint { IsAntialias = true, Color = Divider, StrokeWidth = 1 };

        canvas.DrawText(title, x, y, SKTextAlign.Left, titleFont, titlePaint);

        if (twoLine)
        {
            canvas.DrawText(subtitle, x, y + 18, SKTextAlign.Left, subFont, subPaint);
            canvas.DrawLine(x, y + 28, x + width, y + 28, linePaint);
            return y + 40;
        }

        canvas.DrawText(subtitle, x + width, y, SKTextAlign.Right, subFont, subPaint);
        canvas.DrawLine(x, y + 8, x + width, y + 8, linePaint);
        return y + 28;
    }

    private static float DrawWrappedText(
        SKCanvas canvas, string text, float x, float y, float maxWidth, SKFont font, SKPaint paint, float lineHeight = 18)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var line = new StringBuilder();

        foreach (var word in words)
        {
            var candidate = line.Length == 0 ? word : $"{line} {word}";
            if (line.Length > 0 && font.MeasureText(candidate) > maxWidth)
            {
                canvas.DrawText(line.ToString(), x, y, SKTextAlign.Left, font, paint);
                y += lineHeight;
                line.Clear();
                line.Append(word);
            }
            else
            {
                line.Clear();
                line.Append(candidate);
            }
        }

        if (line.Length > 0)
        {
            canvas.DrawText(line.ToString(), x, y, SKTextAlign.Left, font, paint);
            y += lineHeight;
        }

        return y;
    }

    private static string Plural(int count) => count == 1 ? string.Empty : "s";

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..(maxLength - 1)] + "…";

    private static string FormatMoney(decimal amount, string currency) => currency switch
    {
        "USD" => $"${amount:N0}",
        "JPY" => $"¥{amount:N0}",
        "EUR" => $"€{amount:N0}",
        "GBP" => $"£{amount:N0}",
        _ => $"{amount:N0} {currency}",
    };
}
