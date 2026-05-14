using NESMusicEditor.Models;

namespace NESMusicEditor.Controls.Rendering;

public static class MeasureLayout
{
    public const double LineSpacing = 8.0;
    public const double StaffTopMargin = 16.0;
    public const double ClefAreaWidth = 48.0;
    public const double TimeSigAreaWidth = 24.0;
    public const double NoteHeadWidth = 10.0;
    public const double NoteHeadHeight = 7.0;
    public const double StemHeight = 30.0;

    // diatonic pitch class: C=0 D=1 E=2 F=3 G=4 A=5 B=6
    private static readonly int[] ChromaticToDiatonic = { 0, 0, 1, 1, 2, 3, 3, 4, 4, 5, 5, 6 };

    public static int GetDiatonicStep(int midiNote)
    {
        int pc = midiNote % 12;
        int octave = midiNote / 12 - 1; // octave 4 = midi 60-71
        return octave * 7 + ChromaticToDiatonic[pc];
    }

    public static double GetNoteY(int midiNote, ClefType clef, double staffTopY)
    {
        // center note (C4=60 for treble, C3=48 for alto)
        int centerNote = clef == ClefType.Alto ? 48 : 60;
        // staff center is on B3 (treble: middle line = B4) — actually treble staff lines are E4,G4,B4,D5,F5
        // middle line (line index 2 from top, 0-based) = B4 for treble, G3 for alto
        // Let's use: staffCenterLine is line index 2 (middle of 5), which corresponds to B4 treble / G3 alto
        // In diatonic steps from C4: B4 = 6+7=13 steps above C0 -> octave4*7+6 = 4*7+6=34
        // G3 for alto: octave3*7+4 = 3*7+4=25
        int centerLineDiatonic = clef == ClefType.Alto
            ? (3 * 7 + 4)  // G3
            : (4 * 7 + 6); // B4

        double centerLineY = staffTopY + LineSpacing * 2; // middle line

        int noteDiatonic = GetDiatonicStep(midiNote);
        int steps = noteDiatonic - centerLineDiatonic;
        // each step = half line spacing
        return centerLineY - steps * (LineSpacing / 2.0);
    }

    public static double GetBeatX(int measure, int beat, int beatsPerMeasure, double pixelsPerBeat,
        double staffStartX)
    {
        double measureWidth = beatsPerMeasure * pixelsPerBeat;
        return staffStartX + measure * measureWidth + beat * pixelsPerBeat + pixelsPerBeat * 0.5;
    }

    public static double GetStaffStartX(double leftMargin)
        => leftMargin + ClefAreaWidth + TimeSigAreaWidth;
}
