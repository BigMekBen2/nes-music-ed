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
            capturedStaff.NoteClicked += (m, slot, midi, dur, acc, isRest)
                => PlaceNote(capturedStaff, m, slot, midi, dur, acc, isRest);
            capturedStaff.NoteRightClicked += (m, slot)
                => OnStaffNoteRightClicked(capturedStaff, m, slot);
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

            var pattern = new Pattern { PatternId = i, TrackIndex = i, RowCount = 256 };
            // Pre-fill 256 rows (16 measures × 4 beats × 4 sixteenths)
            for (int r = 0; r < 256; r++)
                pattern.Rows.Add(new PatternRow());
            song.Patterns.Add(pattern);
        }

        return song;
    }

    // ── Note placement ────────────────────────────────────────────────────────

    private static int DurationToSlots(NoteDuration d) => d switch
    {
        NoteDuration.Whole     => 16,
        NoteDuration.Half      => 8,
        NoteDuration.Quarter   => 4,
        NoteDuration.Eighth    => 2,
        NoteDuration.Sixteenth => 1,
        _ => 4
    };

    private void PlaceNote(StaffCanvas staff, int measure, int slotInMeasure, int midiNote,
        NoteDuration duration, int accidental, bool isRest)
    {
        int ti = staff.TrackIndex;
        var pattern = _song.Patterns.Find(p => p.TrackIndex == ti && _song.Tracks[ti].OrderList.Contains(p.PatternId));
        if (pattern == null) return;

        int slotsPerMeasure = staff.BeatsPerMeasure * 4;
        int startSlot = measure * slotsPerMeasure + slotInMeasure;
        int dSlots = DurationToSlots(duration);

        if (startSlot >= pattern.Rows.Count) return;

        // If an existing note-start is at this slot, overwrite pitch/accidental only (preserve duration/continuations)
        var startRow = pattern.Rows[startSlot];
        if (startRow.Cells.Count > 0 && startRow.Cells[0].Note != -3)
        {
            var existingCell = startRow.Cells[0];
            // Snapshot for undo
            var snap = new PatternCell { Note = existingCell.Note, Octave = existingCell.Octave,
                InstrumentId = existingCell.InstrumentId, Volume = existingCell.Volume,
                Effects = new Dictionary<int, int>(existingCell.Effects) };
            _undo.Execute(
                () =>
                {
                    if (isRest) { existingCell.Note = -2; existingCell.Octave = 4; }
                    else { existingCell.Note = midiNote % 12; existingCell.Octave = midiNote / 12 - 1; }
                    if (accidental != 0) existingCell.Effects[998] = accidental;
                    else existingCell.Effects.Remove(998);
                    staff.InvalidateVisual();
                },
                () =>
                {
                    existingCell.Note = snap.Note; existingCell.Octave = snap.Octave;
                    existingCell.Effects = new Dictionary<int, int>(snap.Effects);
                    staff.InvalidateVisual();
                });
            return;
        }

        // Find the range to clear: walk back to find if startSlot is inside an existing note's span
        int clearFrom = startSlot;
        for (int k = startSlot; k >= 0; k--)
        {
            var r = pattern.Rows[k];
            if (r.Cells.Count == 0) break;
            if (r.Cells[0].Note == -3) continue; // continuation
            // Found a note-start at k — check if it spans into startSlot
            int nd = DurationToSlots(r.Cells[0].Effects.TryGetValue(999, out int dv) ? (NoteDuration)dv : NoteDuration.Quarter);
            if (k + nd > startSlot) clearFrom = k;
            break;
        }
        int clearTo = Math.Min(startSlot + dSlots, pattern.Rows.Count); // exclusive

        // Snapshot affected rows for undo
        var snapshots = new List<(int idx, PatternCell? cell)>();
        for (int i = clearFrom; i < clearTo; i++)
        {
            var row = pattern.Rows[i];
            PatternCell? copy = null;
            if (row.Cells.Count > 0)
            {
                var c = row.Cells[0];
                copy = new PatternCell { Note = c.Note, Octave = c.Octave, InstrumentId = c.InstrumentId, Volume = c.Volume, Effects = new Dictionary<int, int>(c.Effects) };
            }
            snapshots.Add((i, copy));
        }

        _undo.Execute(
            () =>
            {
                // Clear affected range
                for (int i = clearFrom; i < clearTo; i++)
                    pattern.Rows[i].Cells.Clear();

                // Place note
                SetCell(pattern, startSlot, midiNote, duration, accidental, isRest);

                // Mark continuation slots
                for (int i = startSlot + 1; i < startSlot + dSlots && i < pattern.Rows.Count; i++)
                {
                    var contCell = new PatternCell { Note = -3 };
                    pattern.Rows[i].Cells.Add(contCell);
                }

                staff.InvalidateVisual();
            },
            () =>
            {
                foreach (var (idx, saved) in snapshots)
                    RestoreCell(pattern, idx, saved);
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

    private void DeleteNoteAt(StaffCanvas staff, int measure, int slotInMeasure)
    {
        int ti = staff.TrackIndex;
        var pattern = _song.Patterns.Find(p => p.TrackIndex == ti && _song.Tracks[ti].OrderList.Contains(p.PatternId));
        if (pattern == null) return;

        int slotsPerMeasure = staff.BeatsPerMeasure * 4;
        int slot = measure * slotsPerMeasure + slotInMeasure;
        if (slot >= pattern.Rows.Count) return;

        // Walk back to find the note-start if this is a continuation cell
        int noteStart = slot;
        for (int k = slot; k >= 0; k--)
        {
            var r = pattern.Rows[k];
            if (r.Cells.Count == 0) return; // nothing to delete
            if (r.Cells[0].Note != -3) { noteStart = k; break; }
        }

        var startRow = pattern.Rows[noteStart];
        if (startRow.Cells.Count == 0) return;

        int dSlots = DurationToSlots(startRow.Cells[0].Effects.TryGetValue(999, out int dv) ? (NoteDuration)dv : NoteDuration.Quarter);
        int clearTo = Math.Min(noteStart + dSlots, pattern.Rows.Count);

        var snapshots = new List<(int idx, PatternCell? cell)>();
        for (int i = noteStart; i < clearTo; i++)
        {
            var row = pattern.Rows[i];
            PatternCell? copy = null;
            if (row.Cells.Count > 0)
            {
                var c = row.Cells[0];
                copy = new PatternCell { Note = c.Note, Octave = c.Octave, InstrumentId = c.InstrumentId, Volume = c.Volume, Effects = new Dictionary<int, int>(c.Effects) };
            }
            snapshots.Add((i, copy));
        }

        _undo.Execute(
            () =>
            {
                for (int i = noteStart; i < clearTo; i++)
                    pattern.Rows[i].Cells.Clear();
                staff.InvalidateVisual();
            },
            () =>
            {
                foreach (var (idx, saved) in snapshots)
                    RestoreCell(pattern, idx, saved);
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
            case Key.NumPad0:
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
        AccidentalStatusRun.Text = acc switch { 1 => "♯ Sharp", -1 => "♭ Flat", _ => "♮ Natural" };
    }

    private void DeleteAtLastClicked()
    {
        foreach (var staff in AllStaves)
        {
            var (m, slot) = staff.LastClickedPos;
            if (m >= 0 && slot >= 0)
            {
                DeleteNoteAt(staff, m, slot);
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
        SetAccidental(accidental);
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

    private void OnStaffNoteRightClicked(StaffCanvas staff, int measure, int slot)
        => DeleteNoteAt(staff, measure, slot);
}
