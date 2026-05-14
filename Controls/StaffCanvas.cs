using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using NESMusicEditor.Controls.Rendering;
using NESMusicEditor.Models;

namespace NESMusicEditor.Controls;

public class StaffCanvas : System.Windows.Controls.Canvas
{
    // ── Dependency Properties ─────────────────────────────────────────────────

    public static readonly DependencyProperty ChannelNameProperty =
        DependencyProperty.Register(nameof(ChannelName), typeof(string), typeof(StaffCanvas),
            new FrameworkPropertyMetadata("Channel", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ClefTypeProperty =
        DependencyProperty.Register(nameof(ClefType), typeof(ClefType), typeof(StaffCanvas),
            new FrameworkPropertyMetadata(ClefType.Treble, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MeasureCountProperty =
        DependencyProperty.Register(nameof(MeasureCount), typeof(int), typeof(StaffCanvas),
            new FrameworkPropertyMetadata(16, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BeatsPerMeasureProperty =
        DependencyProperty.Register(nameof(BeatsPerMeasure), typeof(int), typeof(StaffCanvas),
            new FrameworkPropertyMetadata(4, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PixelsPerBeatProperty =
        DependencyProperty.Register(nameof(PixelsPerBeat), typeof(double), typeof(StaffCanvas),
            new FrameworkPropertyMetadata(60.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SongProperty =
        DependencyProperty.Register(nameof(Song), typeof(Song), typeof(StaffCanvas),
            new FrameworkPropertyMetadata(null, OnSongChanged));

    private static void OnSongChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((StaffCanvas)d).InvalidateVisual();

    public static readonly DependencyProperty TrackIndexProperty =
        DependencyProperty.Register(nameof(TrackIndex), typeof(int), typeof(StaffCanvas),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CurrentDurationProperty =
        DependencyProperty.Register(nameof(CurrentDuration), typeof(NoteDuration), typeof(StaffCanvas),
            new FrameworkPropertyMetadata(NoteDuration.Quarter, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CurrentAccidentalProperty =
        DependencyProperty.Register(nameof(CurrentAccidental), typeof(int), typeof(StaffCanvas),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsRestModeProperty =
        DependencyProperty.Register(nameof(IsRestMode), typeof(bool), typeof(StaffCanvas),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public string ChannelName
    {
        get => (string)GetValue(ChannelNameProperty);
        set => SetValue(ChannelNameProperty, value);
    }
    public ClefType ClefType
    {
        get => (ClefType)GetValue(ClefTypeProperty);
        set => SetValue(ClefTypeProperty, value);
    }
    public int MeasureCount
    {
        get => (int)GetValue(MeasureCountProperty);
        set => SetValue(MeasureCountProperty, value);
    }
    public int BeatsPerMeasure
    {
        get => (int)GetValue(BeatsPerMeasureProperty);
        set => SetValue(BeatsPerMeasureProperty, value);
    }
    public double PixelsPerBeat
    {
        get => (double)GetValue(PixelsPerBeatProperty);
        set => SetValue(PixelsPerBeatProperty, value);
    }
    public Song? Song
    {
        get => (Song?)GetValue(SongProperty);
        set => SetValue(SongProperty, value);
    }
    public int TrackIndex
    {
        get => (int)GetValue(TrackIndexProperty);
        set => SetValue(TrackIndexProperty, value);
    }
    public NoteDuration CurrentDuration
    {
        get => (NoteDuration)GetValue(CurrentDurationProperty);
        set => SetValue(CurrentDurationProperty, value);
    }
    public int CurrentAccidental
    {
        get => (int)GetValue(CurrentAccidentalProperty);
        set => SetValue(CurrentAccidentalProperty, value);
    }
    public bool IsRestMode
    {
        get => (bool)GetValue(IsRestModeProperty);
        set => SetValue(IsRestModeProperty, value);
    }

    // ── Event for note placement (so MainWindow can wire undo/redo) ──────────
    public event Action<int, int, int, NoteDuration, int, bool>? NoteClicked;
        // measure, slot, midiNote, duration, accidental, isRest
    public event Action<int, int>? NoteRightClicked; // measure, slot

    // ── Brushes / Pens ────────────────────────────────────────────────────────

    private static readonly Brush BgBrush = new SolidColorBrush(Color.FromRgb(0x16, 0x16, 0x28));
    private static readonly Brush LabelBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xE0, 0x00));
    private static readonly Pen StaffLinePen = new(new SolidColorBrush(Color.FromRgb(0x4A, 0x4A, 0x7A)), 1.0);
    private static readonly Pen BarLinePen = new(new SolidColorBrush(Color.FromRgb(0x4A, 0x4A, 0x7A)), 1.0);
    private static readonly Pen LedgerPen = new(new SolidColorBrush(Color.FromRgb(0x4A, 0x4A, 0x7A)), 1.0);
    private static readonly Brush MeasureNumBrush = new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x90));
    private static readonly Brush GhostBrush;
    private static readonly Brush BeatHighlightBrush;
    private static readonly Pen GhostPen;

    static StaffCanvas()
    {
        BgBrush.Freeze();
        LabelBrush.Freeze();
        StaffLinePen.Freeze();
        BarLinePen.Freeze();
        LedgerPen.Freeze();
        MeasureNumBrush.Freeze();

        var ghostColor = Color.FromArgb(128, 0xFF, 0xFF, 0xFF);
        GhostBrush = new SolidColorBrush(ghostColor);
        GhostBrush.Freeze();

        GhostPen = new Pen(GhostBrush, 1.2);
        GhostPen.Freeze();

        var beatColor = Color.FromArgb(13, 0xFF, 0xFF, 0xFF);
        BeatHighlightBrush = new SolidColorBrush(beatColor);
        BeatHighlightBrush.Freeze();
    }

    // ── Layout constants ──────────────────────────────────────────────────────

    private const double LeftMargin = 8.0;
    private const double LabelAreaWidth = 72.0;

    // ── Mouse state ───────────────────────────────────────────────────────────
    private bool _mouseOver;
    private Point _mousePos;

    // Last clicked position (for Delete hotkey) — stored as (measure, slot)
    public (int measure, int slot) LastClickedPos { get; private set; } = (-1, -1);

    private double PixelsPerSixteenth => PixelsPerBeat / 4.0;
    private int SlotsPerMeasure => BeatsPerMeasure * 4;

    public StaffCanvas()
    {
        Focusable = false;
        MouseEnter += (_, _) => { _mouseOver = true; InvalidateVisual(); };
        MouseLeave += (_, _) => { _mouseOver = false; InvalidateVisual(); };
        MouseMove += (_, e) => { _mousePos = e.GetPosition(this); InvalidateVisual(); };
        MouseLeftButtonDown += OnLeftClick;
        MouseRightButtonDown += OnRightClick;
    }

    private void OnLeftClick(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(this);
        if (!HitTestSlot(pos, out int measure, out int slot)) return;
        LastClickedPos = (measure, slot);

        double h = ActualHeight;
        double ls = MeasureLayout.LineSpacing;
        double staffTopY = (h - ls * 4) / 2.0;

        double halfStep = ls / 2.0;
        double snappedY = Math.Round((pos.Y - staffTopY) / halfStep) * halfStep + staffTopY;
        int midiNote = MeasureLayout.GetMidiNoteFromY(snappedY, ClefType, staffTopY);
        midiNote += CurrentAccidental;

        NoteClicked?.Invoke(measure, slot, midiNote, CurrentDuration, CurrentAccidental, IsRestMode);
        Window.GetWindow(this)?.Focus();
    }

    private void OnRightClick(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(this);
        if (!HitTestSlot(pos, out int measure, out int slot)) return;
        LastClickedPos = (measure, slot);
        NoteRightClicked?.Invoke(measure, slot);
        Window.GetWindow(this)?.Focus();
    }

    private bool HitTestSlot(Point pos, out int measure, out int slot)
    {
        measure = slot = 0;
        double clefAreaX = LeftMargin + LabelAreaWidth;
        double staffStartX = clefAreaX + MeasureLayout.ClefAreaWidth + MeasureLayout.TimeSigAreaWidth;
        if (pos.X < staffStartX) return false;
        double relX = pos.X - staffStartX;
        double pps = PixelsPerSixteenth;
        int slotIdx = (int)(relX / pps);

        int durationSlots = CurrentDuration switch {
            NoteDuration.Whole => 16,
            NoteDuration.Half => 8,
            NoteDuration.Quarter => 4,
            NoteDuration.Eighth => 2,
            _ => 1
        };
        slotIdx = (slotIdx / durationSlots) * durationSlots;

        measure = slotIdx / SlotsPerMeasure;
        slot = slotIdx % SlotsPerMeasure;
        return measure < MeasureCount;
    }

    // ── OnRender ─────────────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double w = ActualWidth;
        double h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        double ls = MeasureLayout.LineSpacing;
        double staffTopY = (h - ls * 4) / 2.0;
        double staffBottomY = staffTopY + ls * 4;
        double staffCenterY = staffTopY + ls * 2;

        double clefAreaX = LeftMargin + LabelAreaWidth;
        double staffStartX = clefAreaX + MeasureLayout.ClefAreaWidth + MeasureLayout.TimeSigAreaWidth;
        double measureWidth = BeatsPerMeasure * PixelsPerBeat;
        double staffEndX = staffStartX + MeasureCount * measureWidth;
        if (staffEndX < w) staffEndX = w;

        // 1. Background
        dc.DrawRectangle(BgBrush, null, new Rect(0, 0, w, h));

        // 2. Channel label
        var labelTf = new Typeface(new FontFamily("Consolas"), FontStyles.Normal,
            FontWeights.Bold, FontStretches.Normal);
        var labelFt = new FormattedText(ChannelName,
            CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            labelTf, 10, LabelBrush, 96.0);
        dc.DrawText(labelFt, new Point(LeftMargin, staffCenterY - labelFt.Height / 2));

        // 3. Five staff lines
        for (int i = 0; i < 5; i++)
        {
            double y = staffTopY + i * ls;
            dc.DrawLine(StaffLinePen, new Point(clefAreaX, y), new Point(staffEndX, y));
        }

        // 4. Clef
        DrawClef(dc, clefAreaX + 2, staffTopY, staffCenterY);

        // 5. Time signature
        DrawTimeSig(dc, clefAreaX + MeasureLayout.ClefAreaWidth + 2, staffTopY, ls);

        // 6 & 7. Barlines + measure numbers
        for (int m = 0; m <= MeasureCount; m++)
        {
            double x = staffStartX + m * measureWidth;
            if (x > w + 1) break;
            dc.DrawLine(BarLinePen, new Point(x, staffTopY), new Point(x, staffBottomY));

            if (m > 0 && m < MeasureCount)
            {
                var numFt = new FormattedText((m + 1).ToString(),
                    CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    new Typeface("Consolas"), 8, MeasureNumBrush, 96.0);
                dc.DrawText(numFt, new Point(x + 2, staffTopY - 11));
            }
        }

        // 8. Dashed duration grid lines
        DrawDurationGrid(dc, staffStartX, staffTopY, staffBottomY, measureWidth);

        // 9 & 10. Notes from Song model
        DrawNotes(dc, staffStartX, staffTopY, staffCenterY, measureWidth);

        // 11. Ghost note preview
        if (_mouseOver)
            DrawGhostNote(dc, staffStartX, staffTopY, staffCenterY, measureWidth, h);
    }

    private void DrawClef(DrawingContext dc, double x, double staffTopY, double staffCenterY)
    {
        if (ClefType == ClefType.Treble)
            ClefRenderer.DrawTrebleClef(dc, new Point(x, staffTopY));
        else
            ClefRenderer.DrawAltoClef(dc, new Point(x, staffCenterY));
    }

    private void DrawTimeSig(DrawingContext dc, double x, double staffTopY, double ls)
    {
        var tf = new Typeface(new FontFamily("Consolas"), FontStyles.Normal,
            FontWeights.Bold, FontStretches.Normal);
        var topFt = new FormattedText(BeatsPerMeasure.ToString(),
            CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            tf, 14, Brushes.White, 96.0);
        var botFt = new FormattedText("4",
            CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            tf, 14, Brushes.White, 96.0);
        dc.DrawText(topFt, new Point(x, staffTopY));
        dc.DrawText(botFt, new Point(x, staffTopY + ls * 2));
    }

    private void DrawDurationGrid(DrawingContext dc, double staffStartX, double staffTopY,
        double staffBottomY, double measureWidth)
    {
        int step = CurrentDuration switch {
            NoteDuration.Whole => 16,
            NoteDuration.Half => 8,
            NoteDuration.Quarter => 4,
            NoteDuration.Eighth => 2,
            _ => 1
        };
        if (step >= 16) return; // barlines already cover whole-note boundaries

        var dashPen = new Pen(new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x5A)), 1.0);
        dashPen.DashStyle = new DashStyle(new double[] { 4, 4 }, 0);

        double pps = PixelsPerSixteenth;
        int slotsPerMeasure = SlotsPerMeasure;

        for (int m = 0; m < MeasureCount; m++)
        {
            for (int s = step; s < slotsPerMeasure; s += step)
            {
                double x = staffStartX + m * measureWidth + s * pps;
                dc.DrawLine(dashPen, new Point(x, staffTopY), new Point(x, staffBottomY));
            }
        }
    }

    private void DrawNotes(DrawingContext dc, double staffStartX, double staffTopY,
        double staffCenterY, double measureWidth)
    {
        var song = Song;
        if (song == null) return;
        int ti = TrackIndex;
        if (ti < 0 || ti >= song.Tracks.Count) return;
        var track = song.Tracks[ti];

        double pps = PixelsPerSixteenth;

        // Render from patterns in the order list
        int slotCursor = 0;
        foreach (int patId in track.OrderList)
        {
            var pattern = song.Patterns.Find(p => p.PatternId == patId && p.TrackIndex == ti);
            if (pattern == null) continue;

            for (int rowIdx = 0; rowIdx < pattern.Rows.Count; rowIdx++)
            {
                var row = pattern.Rows[rowIdx];
                int measure = slotCursor / SlotsPerMeasure;
                if (measure >= MeasureCount) break;

                if (row.Cells.Count == 0) { slotCursor++; continue; }
                var cell = row.Cells[0];

                // Skip continuation slots
                if (cell.Note == -3) { slotCursor++; continue; }

                double nx = MeasureLayout.GetSlotX(slotCursor, pps, staffStartX);

                var duration = cell.Effects.TryGetValue(999, out int durVal)
                    ? (NoteDuration)durVal
                    : NoteDuration.Quarter;

                if (cell.Note == -2)
                {
                    double restY = staffTopY + MeasureLayout.LineSpacing * 2;
                    NoteGlyphRenderer.DrawRest(dc, nx, restY, duration, new Pen(Brushes.White, 1.2));
                }
                else if (cell.Note >= 0)
                {
                    int midiNote = (cell.Octave + 1) * 12 + cell.Note;
                    double ny = MeasureLayout.GetNoteY(midiNote, ClefType, staffTopY);
                    int accidental = cell.Effects.TryGetValue(998, out int accVal) ? accVal : 0;

                    NoteGlyphRenderer.DrawLedgerLines(dc, nx, ny, staffTopY, LedgerPen);
                    NoteGlyphRenderer.DrawNote(dc, nx, ny, duration, accidental, staffCenterY);
                }

                slotCursor++;
            }
        }
    }

    private void DrawGhostNote(DrawingContext dc, double staffStartX, double staffTopY,
        double staffCenterY, double measureWidth, double h)
    {
        double mx = _mousePos.X;
        double my = _mousePos.Y;

        double ls = MeasureLayout.LineSpacing;
        double halfStep = ls / 2.0;
        double ghostY = Math.Round((my - staffTopY) / halfStep) * halfStep + staffTopY;

        double clefAreaX = LeftMargin + LabelAreaWidth;
        double ssx = clefAreaX + MeasureLayout.ClefAreaWidth + MeasureLayout.TimeSigAreaWidth;
        if (mx < ssx) return;
        double relX = mx - ssx;
        double pps = PixelsPerSixteenth;
        int slotIdx = (int)(relX / pps);

        int durationSlots = DurationToSlots(CurrentDuration);
        slotIdx = (slotIdx / durationSlots) * durationSlots; // snap to duration boundary

        int measure = slotIdx / SlotsPerMeasure;
        if (measure >= MeasureCount) return;

        double ghostX = MeasureLayout.GetSlotX(slotIdx, pps, staffStartX);

        dc.DrawRectangle(BeatHighlightBrush, null,
            new Rect(ghostX, 0, durationSlots * pps, h));

        if (IsRestMode)
        {
            double restY = staffTopY + ls * 2;
            NoteGlyphRenderer.DrawRest(dc, ghostX, restY, CurrentDuration, GhostPen);
        }
        else
        {
            NoteGlyphRenderer.DrawNote(dc, ghostX, ghostY, CurrentDuration, CurrentAccidental,
                staffCenterY, GhostBrush);
        }
    }

    private static int DurationToSlots(NoteDuration d) => d switch
    {
        NoteDuration.Whole     => 16,
        NoteDuration.Half      => 8,
        NoteDuration.Quarter   => 4,
        NoteDuration.Eighth    => 2,
        NoteDuration.Sixteenth => 1,
        _ => 4
    };
}

// ── Demo note support (kept for backward compat, no longer used in MainWindow) ──

public record DemoNote(int Measure, int Beat, int MidiNote, NoteDuration Duration, int Accidental = 0);

public class TrackWithNotes : Track
{
    public List<DemoNote> DemoNotes { get; } = new();
}
