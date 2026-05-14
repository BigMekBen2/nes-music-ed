using System;

namespace NESMusicEditor.Synthesis;

public static class NoteToFrequency
{
    public static float Get(int midiNote) // 0-95
    {
        const double A4 = 440.0;
        const int A4_midi = 69;
        return (float)(A4 * Math.Pow(2.0, (midiNote - A4_midi) / 12.0));
    }
}
