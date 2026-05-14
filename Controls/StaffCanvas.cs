using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NESMusicEditor.Controls;

public class StaffCanvas : Canvas
{
    public static readonly DependencyProperty ChannelNameProperty =
        DependencyProperty.Register(nameof(ChannelName), typeof(string), typeof(StaffCanvas),
            new FrameworkPropertyMetadata("Channel", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MeasureCountProperty =
        DependencyProperty.Register(nameof(MeasureCount), typeof(int), typeof(StaffCanvas),
            new FrameworkPropertyMetadata(16, FrameworkPropertyMetadataOptions.AffectsRender));

    public string ChannelName
    {
        get => (string)GetValue(ChannelNameProperty);
        set => SetValue(ChannelNameProperty, value);
    }

    public int MeasureCount
    {
        get => (int)GetValue(MeasureCountProperty);
        set => SetValue(MeasureCountProperty, value);
    }

    private static readonly Pen StaffLinePen = new(new SolidColorBrush(Color.FromRgb(0x2A, 0x4A, 0x6A)), 1.0);
    private static readonly Pen BarLinePen = new(new SolidColorBrush(Color.FromRgb(0x40, 0x40, 0x80)), 1.5);
    private static readonly Pen BorderPen = new(new SolidColorBrush(Color.FromRgb(0x50, 0x50, 0xA0)), 1.0);

    private static readonly Brush BgBrush = new SolidColorBrush(Color.FromRgb(0x16, 0x21, 0x3E));
    private static readonly Brush LabelBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0xFF));
    private static readonly Brush ClefBrush = new SolidColorBrush(Color.FromRgb(0x70, 0x90, 0xC0));
    private static readonly Brush DimBrush = new SolidColorBrush(Color.FromRgb(0x40, 0x60, 0x80));

    private const double LeftMargin = 110.0;
    private const double LineSpacing = 8.0;
    private const double StaffTop = 10.0;
    private const double MeasureWidth = 80.0;

    static StaffCanvas()
    {
        StaffLinePen.Freeze();
        BarLinePen.Freeze();
        BorderPen.Freeze();
        BgBrush.Freeze();
        LabelBrush.Freeze();
        ClefBrush.Freeze();
        DimBrush.Freeze();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double w = ActualWidth;
        double h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        // Background
        dc.DrawRectangle(BgBrush, null, new Rect(0, 0, w, h));

        // Border top/bottom
        dc.DrawLine(BorderPen, new Point(0, 0), new Point(w, 0));
        dc.DrawLine(BorderPen, new Point(0, h - 1), new Point(w, h - 1));

        // Channel label (left margin area)
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x0D, 0x0D, 0x1F)), BorderPen,
            new Rect(0, 0, LeftMargin, h));

        var labelTypeface = new Typeface(new FontFamily("Consolas"), FontStyles.Normal,
            FontWeights.Bold, FontStretches.Normal);
        var labelText = new FormattedText(
            ChannelName,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            labelTypeface,
            11,
            LabelBrush,
            96.0);
        dc.DrawText(labelText, new Point(6, h / 2 - labelText.Height / 2));

        // Clef symbol
        var clefTypeface = new Typeface(new FontFamily("Segoe UI Symbol,Arial Unicode MS"), FontStyles.Normal,
            FontWeights.Normal, FontStretches.Normal);
        var clefText = new FormattedText(
            "\U0001D11E", // treble clef
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            clefTypeface,
            28,
            ClefBrush,
            96.0);
        double clefY = StaffTop + LineSpacing * 2 - clefText.Height * 0.6;
        dc.DrawText(clefText, new Point(LeftMargin + 4, clefY));

        // 5 staff lines
        double staffX0 = LeftMargin + 36;
        double totalWidth = MeasureCount * MeasureWidth;
        double staffX1 = Math.Max(staffX0 + totalWidth, w);

        for (int i = 0; i < 5; i++)
        {
            double y = StaffTop + i * LineSpacing;
            dc.DrawLine(StaffLinePen, new Point(staffX0, y), new Point(staffX1, y));
        }

        // Measure barlines
        double staffBottom = StaffTop + 4 * LineSpacing;
        for (int m = 0; m <= MeasureCount; m++)
        {
            double x = staffX0 + m * MeasureWidth;
            if (x > w) break;
            dc.DrawLine(BarLinePen, new Point(x, StaffTop), new Point(x, staffBottom));

            // Measure number label
            if (m > 0 && m < MeasureCount)
            {
                var numTypeface = new Typeface("Consolas");
                var numText = new FormattedText(
                    m.ToString(),
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    numTypeface,
                    8,
                    DimBrush,
                    96.0);
                dc.DrawText(numText, new Point(x + 2, StaffTop - 10));
            }
        }
    }
}
