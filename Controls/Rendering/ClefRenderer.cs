using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace NESMusicEditor.Controls.Rendering;

public static class ClefRenderer
{
    private static readonly Brush ClefBrush = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xDD));
    private static readonly Pen ClefPen = new(ClefBrush, 1.5);

    static ClefRenderer()
    {
        ClefBrush.Freeze();
        ClefPen.Freeze();
    }

    // Draw treble clef using FormattedText (Unicode U+1D11E)
    public static void DrawTrebleClef(DrawingContext dc, Point origin)
    {
        var tf = new Typeface(new FontFamily("Segoe UI Symbol,Arial Unicode MS,Symbola"),
            FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var ft = new FormattedText("\U0001D11E",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            tf, 36, ClefBrush, 96.0);
        // origin.Y = staffTopY; position so curl sits on second line from bottom
        // second line from bottom = line index 3 from top (0-based), y = staffTopY + 3*lineSpacing
        // The glyph's G-line curl is roughly at 55% of glyph height from top
        double glyphY = origin.Y - ft.Height * 0.28;
        dc.DrawText(ft, new Point(origin.X, glyphY));
    }

    // Draw alto clef using FormattedText (Unicode U+1D121)
    public static void DrawAltoClef(DrawingContext dc, Point origin)
    {
        var tf = new Typeface(new FontFamily("Segoe UI Symbol,Arial Unicode MS,Symbola"),
            FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var ft = new FormattedText("\U0001D121",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            tf, 28, ClefBrush, 96.0);
        // center on middle line (C clef)
        double glyphY = origin.Y - ft.Height * 0.5;
        dc.DrawText(ft, new Point(origin.X, glyphY));
    }
}
