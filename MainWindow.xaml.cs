using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using NESMusicEditor.Controls;
using NESMusicEditor.Controls.Rendering;
using NESMusicEditor.Editing;
using NESMusicEditor.Models;

namespace NESMusicEditor;

public partial class MainWindow : Window
{
    private readonly Song _song;
    private readonly UndoRedoManager _undo = new();
    private bool _isPlaying;
    private bool _isRestMode;

    private StaffCanvas[] AllStaves => new[] { StaffSquare1, StaffSquare2, StaffTriangle, StaffNoise };

    public MainWindow()
    {
        InitializeComponent();
        _song = CreateSong();
        WireSong();
        this.Focus();
    }

    private void WireSong()
    {
        StaffSquare1.Song = _song;
        StaffSquare2.Song = _song;
        StaffTriangle.Song = _song;
        StaffNoise.Song = _song;

        StaffTriangle.ClefType = ClefType.Alto;

        foreach (var staff in AllStaves)
        {
            var capturedStaff = staff;
            capturedStaff.NoteClicked += (m, b, midi, dur, acc, isRest)
                => PlaceNote(capturedStaff, m, b, midi, dur, acc, isRest);
            capturedStaff.NoteRightClicked += (m, b)
                => OnStaffNoteRightClicked(capturedStaff, m, b);
        }
    }

    private static Song CreateSong()
    {
        var song = new Song { Title = "Untitled" };

        string[] names = { "Square 1", "Square 2", "Triangle", "Noise" };
        ClefType[] clefs = { ClefType.Treble, ClefType.Treble, ClefType.Alto, ClefType.Treble };

        for (int i = 0; i < 4; i++)
        {
            var track = new NESMusicEditor.Models.Track { ChannelIndex = i, ChannelName = names[i], Clef = clefs[i] };
            track.OrderList.Add(i);
            song.Tracks.Add(track);

            var pattern = new Pattern { PatternId = i, TrackIndex = i, RowCount = 64 };
            // Pre-fill 64 empty rows
            for (int r = 0; r < 64; r++)
                pattern.Rows.Add(new PatternRow());
            song.Patterns.Add(pattern);
        }

        return song;
    }

    // ── Note placement ────────────────────────────────────────────────────────

    private void PlaceNote(StaffCanvas staff, int measure, int beat, int midiNote,
        NoteDuration duration, int accidental, bool isRest)
    {
        int ti = staff.TrackIndex;
        var pattern = _song.Patterns.Find(p => p.TrackIndex == ti && _song.Tracks[ti].OrderList.Contains(p.PatternId));
        if (pattern == null) return;

        int rowIndex = measure * staff.BeatsPerMeasure + beat;
        if (rowIndex >= pattern.Rows.Count) return;

        var row = pattern.Rows[rowIndex];

        // Snapshot old cell for undo
        PatternCell? oldCell = row.Cells.Count > 0 ? row.Cells[0] : null;
        PatternCell? oldCellCopy = oldCell == null ? null : new PatternCell
        {
            Note = oldCell.Note,
            Octave = oldCell.Octave,
            InstrumentId = oldCell.InstrumentId,
            Volume = oldCell.Volume,
            Effects = new Dictionary<int, int>(oldCell.Effects)
        };

        _undo.Execute(
            () =>
            {
                SetCell(pattern, rowIndex, midiNote, duration, accidental, isRest);
                staff.InvalidateVisual();
            },
            () =>
            {
                RestoreCell(pattern, rowIndex, oldCellCopy);
                staff.InvalidateVisual();
            });
    }

    private static void SetCell(Pattern pattern, int rowIndex, int midiNote, NoteDuration duration,
        int accidental, bool isRest)
    {
        var row = pattern.Rows[rowIndex];
        PatternCell cell;
        if (row.Cells.Count == 0)
        {
            cell = new PatternCell();
            row.Cells.Add(cell);
        }
        else
        {
            cell = row.Cells[0];
        }

        if (isRest)
        {
            cell.Note = -2;
            cell.Octave = 4;
        }
        else
        {
            // Decompose midiNote into pc + octave (FamiTracker style: octave = midi/12 - 1)
            int octave = midiNote / 12 - 1;
            int pc = midiNote % 12;
            cell.Note = pc;
            cell.Octave = octave;
        }

        cell.Effects[999] = (int)duration;
        if (accidental != 0)
            cell.Effects[998] = accidental;
        else
            cell.Effects.Remove(998);
    }

    private static void RestoreCell(Pattern pattern, int rowIndex, PatternCell? saved)
    {
        var row = pattern.Rows[rowIndex];
        row.Cells.Clear();
        if (saved != null)
            row.Cells.Add(saved);
    }

    private void DeleteNoteAt(StaffCanvas staff, int measure, int beat)
    {
        int ti = staff.TrackIndex;
        var pattern = _song.Patterns.Find(p => p.TrackIndex == ti && _song.Tracks[ti].OrderList.Contains(p.PatternId));
        if (pattern == null) return;

        int rowIndex = measure * staff.BeatsPerMeasure + beat;
        if (rowIndex >= pattern.Rows.Count) return;

        var row = pattern.Rows[rowIndex];
        if (row.Cells.Count == 0) return;

        var cellCopy = new PatternCell
        {
            Note = row.Cells[0].Note,
            Octave = row.Cells[0].Octave,
            InstrumentId = row.Cells[0].InstrumentId,
            Volume = row.Cells[0].Volume,
            Effects = new Dictionary<int, int>(row.Cells[0].Effects)
        };

        _undo.Execute(
            () =>
            {
                pattern.Rows[rowIndex].Cells.Clear();
                staff.InvalidateVisual();
            },
            () =>
            {
                RestoreCell(pattern, rowIndex, cellCopy);
                staff.InvalidateVisual();
            });
    }

    // ── Keyboard hotkeys ──────────────────────────────────────────────────────

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;

        if (ctrl && e.Key == Key.Z) { _undo.Undo(); foreach (var s in AllStaves) s.InvalidateVisual(); e.Handled = true; return; }
        if (ctrl && e.Key == Key.Y) { _undo.Redo(); foreach (var s in AllStaves) s.InvalidateVisual(); e.Handled = true; return; }
        if (ctrl && shift && e.Key == Key.Z) { _undo.Redo(); foreach (var s in AllStaves) s.InvalidateVisual(); e.Handled = true; return; }

        switch (e.Key)
        {
            case Key.D1: SetDuration(NoteDuration.Whole); e.Handled = true; break;
            case Key.D2: SetDuration(NoteDuration.Half); e.Handled = true; break;
            case Key.D3: SetDuration(NoteDuration.Quarter); e.Handled = true; break;
            case Key.D4: SetDuration(NoteDuration.Eighth); e.Handled = true; break;
            case Key.D5: SetDuration(NoteDuration.Sixteenth); e.Handled = true; break;

            case Key.Up when shift: SetAccidental(1); e.Handled = true; break;
            case Key.Down when shift: SetAccidental(-1); e.Handled = true; break;
            case Key.D0:
            case Key.N: SetAccidental(0); e.Handled = true; break;

            case Key.Space:
                _isPlaying = !_isPlaying;
                e.Handled = true;
                break;

            case Key.Delete:
            case Key.Back:
                DeleteAtLastClicked();
                e.Handled = true;
                break;
        }
    }

    private void SetDuration(NoteDuration d)
    {
        DurationCombo.SelectedIndex = (int)d;
        foreach (var s in AllStaves) s.CurrentDuration = d;
    }

    private void SetAccidental(int acc)
    {
        foreach (var s in AllStaves) s.CurrentAccidental = acc;
        AccNatural.IsChecked = acc == 0;
        AccSharp.IsChecked = acc == 1;
        AccFlat.IsChecked = acc == -1;
    }

    private void DeleteAtLastClicked()
    {
        // Find whichever staff was last interacted with — try all
        foreach (var staff in AllStaves)
        {
            var (m, b) = staff.LastClickedPos;
            if (m >= 0 && b >= 0)
            {
                DeleteNoteAt(staff, m, b);
                break;
            }
        }
    }

    // ── UI event handlers ─────────────────────────────────────────────────────

    private void DurationCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (StaffSquare1 == null) return;
        var duration = (NoteDuration)DurationCombo.SelectedIndex;
        foreach (var staff in AllStaves)
            staff.CurrentDuration = duration;
    }

    private void Accidental_Checked(object sender, RoutedEventArgs e)
    {
        if (StaffSquare1 == null) return;
        if (sender is not ToggleButton rb || rb.Tag is not string tag) return;
        int accidental = int.Parse(tag);
        foreach (var staff in AllStaves)
            staff.CurrentAccidental = accidental;
    }

    private void NoteMode_Checked(object sender, RoutedEventArgs e)
    {
        if (StaffSquare1 == null) return;
        if (sender is not ToggleButton rb || rb.Tag is not string tag) return;
        _isRestMode = tag == "rest";
        foreach (var staff in AllStaves)
            staff.IsRestMode = _isRestMode;
    }

    private void Menu_Stub(object sender, RoutedEventArgs e) { }
    private void Toolbar_Stub(object sender, RoutedEventArgs e) { }

    private void OnStaffNoteRightClicked(StaffCanvas staff, int measure, int beat)
        => DeleteNoteAt(staff, measure, beat);
}
