using System.Windows;
using System.Windows.Media;

namespace NESMusicEditor.Controls.Rendering;

public enum NoteDuration { Whole, Half, Quarter, Eighth, Sixteenth }

public static class NoteGlyphRenderer
{
    private static readonly Brush NoteWhite = new SolidColorBrush(Color.FromRgb(0xEE, 0xEE, 0xEE));
    private static readonly Brush CyanBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0xFF));
    private static readonly Pen NotePen = new(NoteWhite, 1.2);
    private static readonly Pen AccidentalPen = new(CyanBrush, 1.2);

    static NoteGlyphRenderer()
    {
        NoteWhite.Freeze();
        CyanBrush.Freeze();
        NotePen.Freeze();
        AccidentalPen.Freeze();
    }

    public static void DrawNote(DrawingContext dc, double x, double y, NoteDuration duration,
        int accidental, double staffCenterY, Brush? overrideBrush = null, bool vibrato = false)
    {
        var fill = overrideBrush ?? NoteWhite;
        var pen = overrideBrush != null ? new Pen(overrideBrush, 1.2) : NotePen;

        bool hollow = duration == NoteDuration.Whole || duration == NoteDuration.Half;
        bool hasStem = duration != NoteDuration.Whole;
        bool stemUp = y >= staffCenterY;

        double hw = MeasureLayout.NoteHeadWidth / 2.0;
        double hh = MeasureLayout.NoteHeadHeight / 2.0;

        // Draw note head (slightly rotated ellipse look via transform)
        dc.PushTransform(new RotateTransform(-15, x, y));
        dc.DrawEllipse(hollow ? Brushes.Transparent : fill, pen,
            new Point(x, y), hw, hh);
        dc.Pop();

        // Accidental
        if (accidental != 0)
            DrawAccidental(dc, x - hw - 14, y, accidental, overrideBrush != null ? pen : AccidentalPen);

        // Stem
        if (hasStem)
            DrawStem(dc, x, y, stemUp, pen);

        // Flags
        if (duration == NoteDuration.Eighth)
            DrawFlag(dc, x, y, stemUp, 1, pen);
        else if (duration == NoteDuration.Sixteenth)
            DrawFlag(dc, x, y, stemUp, 2, pen);

        // Vibrato indicator: cyan "~" above note head
        if (vibrato && overrideBrush == null)
        {
            var vft = new FormattedText("~",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Consolas"), 10, CyanBrush, 96.0);
            dc.DrawText(vft, new Point(x - vft.Width / 2, y - hh - 12));
        }
    }

    public static void DrawStem(DrawingContext dc, double x, double y, bool stemUp, Pen pen)
    {
        double sh = MeasureLayout.StemHeight;
        double hw = MeasureLayout.NoteHeadWidth / 2.0;
        if (stemUp)
        {
            // stem goes up from right edge
            dc.DrawLine(pen, new Point(x + hw, y), new Point(x + hw, y - sh));
        }
        else
        {
            // stem goes down from left edge
            dc.DrawLine(pen, new Point(x - hw, y), new Point(x - hw, y + sh));
        }
    }

    public static void DrawFlag(DrawingContext dc, double x, double y, bool stemUp, int count, Pen pen)
    {
        double sh = MeasureLayout.StemHeight;
        double hw = MeasureLayout.NoteHeadWidth / 2.0;

        for (int i = 0; i < count; i++)
        {
            if (stemUp)
            {
                double tipX = x + hw;
                double tipY = y - sh + i * 8;
                var geo = new StreamGeometry();
                using var ctx = geo.Open();
                ctx.BeginFigure(new Point(tipX, tipY), false, false);
                ctx.BezierTo(
                    new Point(tipX + 12, tipY + 5),
                    new Point(tipX + 10, tipY + 15),
                    new Point(tipX + 4, tipY + 20),
                    true, true);
                geo.Freeze();
                dc.DrawGeometry(null, pen, geo);
            }
            else
            {
                double tipX = x - hw;
                double tipY = y + sh - i * 8;
                var geo = new StreamGeometry();
                using var ctx = geo.Open();
                ctx.BeginFigure(new Point(tipX, tipY), false, false);
                ctx.BezierTo(
                    new Point(tipX + 12, tipY - 5),
                    new Point(tipX + 10, tipY - 15),
                    new Point(tipX + 4, tipY - 20),
                    true, true);
                geo.Freeze();
                dc.DrawGeometry(null, pen, geo);
            }
        }
    }

    public static void DrawAccidental(DrawingContext dc, double x, double y, int accidental, Pen pen)
    {
        if (accidental == 1) // sharp
        {
            // Two vertical lines + two horizontal lines
            dc.DrawLine(pen, new Point(x + 3, y - 8), new Point(x + 3, y + 8));
            dc.DrawLine(pen, new Point(x + 7, y - 8), new Point(x + 7, y + 8));
            dc.DrawLine(pen, new Point(x, y - 3), new Point(x + 10, y - 3));
            dc.DrawLine(pen, new Point(x, y + 3), new Point(x + 10, y + 3));
        }
        else if (accidental == -1) // flat
        {
            // Vertical line + curved bump
            dc.DrawLine(pen, new Point(x + 2, y - 10), new Point(x + 2, y + 6));
            var geo = new StreamGeometry();
            using var ctx = geo.Open();
            ctx.BeginFigure(new Point(x + 2, y), false, false);
            ctx.BezierTo(
                new Point(x + 12, y),
                new Point(x + 12, y + 7),
                new Point(x + 2, y + 7),
                true, true);
            geo.Freeze();
            dc.DrawGeometry(null, pen, geo);
        }
        // natural = 0, draw nothing extra (already cleared)
    }

    public static void DrawRest(DrawingContext dc, double x, double y, NoteDuration duration, Pen pen)
    {
        switch (duration)
        {
            case NoteDuration.Whole:
                // filled rect hanging below a line
                dc.DrawRectangle(NoteWhite, null, new Rect(x - 5, y, 10, 4));
                dc.DrawLine(pen, new Point(x - 7, y - 1), new Point(x + 7, y - 1));
                break;
            case NoteDuration.Half:
                // filled rect sitting on top of a line
                dc.DrawRectangle(NoteWhite, null, new Rect(x - 5, y - 4, 10, 4));
                dc.DrawLine(pen, new Point(x - 7, y), new Point(x + 7, y));
                break;
            case NoteDuration.Quarter:
                // zigzag
                dc.DrawLine(pen, new Point(x, y - 8), new Point(x + 4, y - 4));
                dc.DrawLine(pen, new Point(x + 4, y - 4), new Point(x - 2, y));
                dc.DrawLine(pen, new Point(x - 2, y), new Point(x + 2, y + 4));
                dc.DrawLine(pen, new Point(x + 2, y + 4), new Point(x - 1, y + 8));
                break;
            case NoteDuration.Eighth:
                // diagonal slash + small circle at top
                dc.DrawLine(pen, new Point(x - 3, y + 8), new Point(x + 3, y - 8));
                dc.DrawEllipse(NoteWhite, null, new Point(x + 3, y - 8), 2.5, 2.5);
                break;
            case NoteDuration.Sixteenth:
                // two slashes + two circles
                dc.DrawLine(pen, new Point(x - 3, y + 8), new Point(x + 3, y - 8));
                dc.DrawEllipse(NoteWhite, null, new Point(x + 3, y - 8), 2.5, 2.5);
                dc.DrawLine(pen, new Point(x - 1, y + 4), new Point(x + 5, y - 4));
                dc.DrawEllipse(NoteWhite, null, new Point(x + 5, y - 4), 2.5, 2.5);
                break;
        }
    }

    public static void DrawLedgerLines(DrawingContext dc, double x, double noteY,
        double staffTopY, Pen linePen)
    {
        double ls = MeasureLayout.LineSpacing;
        double hw = MeasureLayout.NoteHeadWidth / 2.0 + 4;

        // lines above staff (above top line)
        for (double lineY = staffTopY - ls; lineY >= noteY - ls / 2.0 + 0.1; lineY -= ls)
            dc.DrawLine(linePen, new Point(x - hw, lineY), new Point(x + hw, lineY));

        // lines below staff (below bottom line)
        double staffBottomY = staffTopY + ls * 4;
        for (double lineY = staffBottomY + ls; lineY <= noteY + ls / 2.0 - 0.1; lineY += ls)
            dc.DrawLine(linePen, new Point(x - hw, lineY), new Point(x + hw, lineY));
    }
}
