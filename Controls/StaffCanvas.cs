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
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrackIndexProperty =
        DependencyProperty.Register(nameof(TrackIndex), typeof(int), typeof(StaffCanvas),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CurrentDurationProperty =
        DependencyProperty.Register(nameof(CurrentDuration), typeof(NoteDuration), typeof(StaffCanvas),
            new FrameworkPropertyMetadata(NoteDuration.Quarter, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CurrentAccidentalProperty =
        DependencyProperty.Register(nameof(CurrentAccidental), typeof(int), typeof(StaffCanvas),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

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

    // ── Brushes / Pens ────────────────────────────────────────────────────────

    private static readonly Brush BgBrush = new SolidColorBrush(Color.FromRgb(0x16, 0x16, 0x28));
    private static readonly Brush LabelBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xE0, 0x00));
    private static readonly Pen StaffLinePen = new(new SolidColorBrush(Color.FromRgb(0x4A, 0x4A, 0x7A)), 1.0);
    private static readonly Pen BarLinePen = new(new SolidColorBrush(Color.FromRgb(0x4A, 0x4A, 0x7A)), 1.0);
    private static readonly Pen LedgerPen = new(new SolidColorBrush(Color.FromRgb(0x4A, 0x4A, 0x7A)), 1.0);
    private static readonly Brush MeasureNumBrush = new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x90));
    private static readonly Brush GhostBrush;
    private static readonly Brush BeatHighlightBrush;

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

        var beatColor = Color.FromArgb(13, 0xFF, 0xFF, 0xFF); // 5% white
        BeatHighlightBrush = new SolidColorBrush(beatColor);
        BeatHighlightBrush.Freeze();
    }

    // ── Layout constants ──────────────────────────────────────────────────────

    private const double LeftMargin = 8.0; // left side channel label area width
    private const double LabelAreaWidth = 72.0;

    // ── Mouse state ───────────────────────────────────────────────────────────
    private bool _mouseOver;
    private Point _mousePos;

    public StaffCanvas()
    {
        MouseEnter += (_, _) => { _mouseOver = true; InvalidateVisual(); };
        MouseLeave += (_, _) => { _mouseOver = false; InvalidateVisual(); };
        MouseMove += (_, e) => { _mousePos = e.GetPosition(this); InvalidateVisual(); };
    }

    // ── OnRender ─────────────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double w = ActualWidth;
        double h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        double ls = MeasureLayout.LineSpacing;
        double staffTopY = (h - ls * 4) / 2.0; // center staff in canvas height
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

        // 8 & 9. Notes + ledger lines from Song model
        DrawNotes(dc, staffStartX, staffTopY, staffCenterY, measureWidth);

        // 10. Cursor preview ghost note
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

    private void DrawNotes(DrawingContext dc, double staffStartX, double staffTopY,
        double staffCenterY, double measureWidth)
    {
        var song = Song;
        if (song == null) return;
        int ti = TrackIndex;
        if (ti < 0 || ti >= song.Tracks.Count) return;
        var track = song.Tracks[ti];
        if (track.OrderList.Count == 0) return;

        // Render notes from the first pattern in order list for now
        int beatCursor = 0;
        foreach (int patId in track.OrderList)
        {
            var pattern = song.Patterns.Find(p => p.PatternId == patId);
            if (pattern == null) continue;

            foreach (var row in pattern.Rows)
            {
                if (row.Cells.Count == 0) { beatCursor++; continue; }
                var cell = row.Cells[0];
                if (cell.Note < 0) { beatCursor++; continue; }

                int midiNote = (cell.Octave + 1) * 12 + cell.Note;
                int measure = beatCursor / BeatsPerMeasure;
                int beat = beatCursor % BeatsPerMeasure;
                if (measure >= MeasureCount) break;

                double nx = MeasureLayout.GetBeatX(measure, beat, BeatsPerMeasure, PixelsPerBeat, staffStartX);
                double ny = MeasureLayout.GetNoteY(midiNote, ClefType, staffTopY);

                NoteGlyphRenderer.DrawLedgerLines(dc, nx, ny, staffTopY, LedgerPen);
                NoteGlyphRenderer.DrawNote(dc, nx, ny, CurrentDuration, 0, staffCenterY);

                beatCursor++;
            }
            break; // only first pattern for Phase 2
        }

        // Also render demo notes stored directly on track via extension
        if (track is TrackWithNotes twn)
        {
            foreach (var dn in twn.DemoNotes)
            {
                double nx = MeasureLayout.GetBeatX(dn.Measure, dn.Beat, BeatsPerMeasure, PixelsPerBeat, staffStartX);
                double ny = MeasureLayout.GetNoteY(dn.MidiNote, ClefType, staffTopY);
                NoteGlyphRenderer.DrawLedgerLines(dc, nx, ny, staffTopY, LedgerPen);
                NoteGlyphRenderer.DrawNote(dc, nx, ny, dn.Duration, dn.Accidental, staffCenterY);
            }
        }
    }

    private void DrawGhostNote(DrawingContext dc, double staffStartX, double staffTopY,
        double staffCenterY, double measureWidth, double h)
    {
        double mx = _mousePos.X;
        double my = _mousePos.Y;

        double ls = MeasureLayout.LineSpacing;

        // snap Y to nearest diatonic step (half line spacing)
        double halfStep = ls / 2.0;
        double ghostY = Math.Round((my - staffTopY) / halfStep) * halfStep + staffTopY;

        // snap X to nearest beat
        if (mx < staffStartX) return;
        double relX = mx - staffStartX;
        double ppb = PixelsPerBeat;
        int beatIdx = (int)(relX / ppb);
        int measure = beatIdx / BeatsPerMeasure;
        int beat = beatIdx % BeatsPerMeasure;
        if (measure >= MeasureCount) return;

        double ghostX = MeasureLayout.GetBeatX(measure, beat, BeatsPerMeasure, ppb, staffStartX);

        // beat column highlight
        dc.DrawRectangle(BeatHighlightBrush, null,
            new Rect(ghostX - ppb / 2, 0, ppb, h));

        // ghost note
        NoteGlyphRenderer.DrawNote(dc, ghostX, ghostY, CurrentDuration, CurrentAccidental,
            staffCenterY, GhostBrush);
    }
}

// ── Demo note support ─────────────────────────────────────────────────────────

public record DemoNote(int Measure, int Beat, int MidiNote, NoteDuration Duration, int Accidental = 0);

public class TrackWithNotes : Track
{
    public List<DemoNote> DemoNotes { get; } = new();
}
