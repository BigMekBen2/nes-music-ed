using System;
using System.Collections.Generic;
using NESMusicEditor.Models;

namespace NESMusicEditor.Synthesis;

public class SynthesisEngine
{
    private const int SampleRate = 44100;
    private const int FrameRate = 60; // NES runs at 60Hz
    private const int SamplesPerFrame = SampleRate / FrameRate; // 735

    public float[] Synthesize(Song song)
    {
        var frames = BuildFrameSequence(song);
        var samples = new List<float>();

        var sq1 = new SquareOscillator();
        var sq2 = new SquareOscillator();
        var tri = new TriangleOscillator();
        var noise = new NoiseGenerator();
        var env = new Envelope();

        var noteOnFrame = new int[4];
        var noteOffFrame = new int[4];
        var currentNote = new int[4];
        var currentInst = new Instrument[4];

        for (int i = 0; i < 4; i++) { noteOnFrame[i] = -1; noteOffFrame[i] = -1; currentNote[i] = -1; }

        for (int frame = 0; frame < frames.Count; frame++)
        {
            var channelNotes = frames[frame];

            for (int ch = 0; ch < 4; ch++)
            {
                int note = channelNotes[ch];
                if (note != currentNote[ch])
                {
                    if (note >= 0)
                    {
                        noteOnFrame[ch] = frame;
                        noteOffFrame[ch] = -1;
                        if (ch == 0) sq1.Reset();
                        else if (ch == 1) sq2.Reset();
                        else if (ch == 2) tri.Reset();
                        else noise.Reset();
                    }
                    else if (currentNote[ch] >= 0)
                    {
                        noteOffFrame[ch] = frame;
                    }
                    currentNote[ch] = note;
                }
            }

            for (int s = 0; s < SamplesPerFrame; s++)
            {
                float mix = 0f;
                for (int ch = 0; ch < 4; ch++)
                {
                    if (noteOnFrame[ch] < 0) continue;
                    var inst = currentInst[ch] ?? DefaultInstrument(ch);
                    int framesSinceOn = frame - noteOnFrame[ch];
                    int framesSinceOff = noteOffFrame[ch] >= 0 ? frame - noteOffFrame[ch] : -1;
                    float amplitude = env.CalculateAmplitude(framesSinceOn, framesSinceOff, inst) * 0.25f;
                    int midi = Math.Max(currentNote[ch], 0);
                    float freq = NoteToFrequency.Get(midi);

                    mix += ch switch
                    {
                        0 => sq1.Next(freq, inst.DutyCycle, amplitude, SampleRate),
                        1 => sq2.Next(freq, inst.DutyCycle, amplitude, SampleRate),
                        2 => tri.Next(freq, amplitude, SampleRate),
                        _ => noise.Next(amplitude, SampleRate)
                    };
                }
                samples.Add(Math.Clamp(mix, -1f, 1f));
            }
        }

        return samples.ToArray();
    }

    private static Instrument DefaultInstrument(int ch) => new()
    {
        AttackFrames = 1, DecayFrames = 5, SustainLevel = 12, ReleaseFrames = 10,
        DutyCycle = 2, VibratoSpeed = 0
    };

    private static List<int[]> BuildFrameSequence(Song song)
    {
        int fps = song.FramesPerSecond;
        int bpm = 120;
        double framesPerSixteenth = fps / ((bpm / 60.0) * 4.0);

        int maxSlots = 0;
        foreach (var track in song.Tracks)
        {
            foreach (var pid in track.OrderList)
            {
                var pat = song.Patterns.Find(p => p.PatternId == pid);
                if (pat != null) maxSlots = Math.Max(maxSlots, pat.Rows.Count);
            }
        }

        int totalFrames = (int)Math.Ceiling(maxSlots * framesPerSixteenth) + fps;

        var result = new List<int[]>(totalFrames);
        for (int f = 0; f < totalFrames; f++)
            result.Add(new int[4] { -1, -1, -1, -1 });

        for (int ti = 0; ti < song.Tracks.Count && ti < 4; ti++)
        {
            var track = song.Tracks[ti];
            foreach (var pid in track.OrderList)
            {
                var pat = song.Patterns.Find(p => p.PatternId == pid);
                if (pat == null) continue;

                for (int slot = 0; slot < pat.Rows.Count; slot++)
                {
                    var row = pat.Rows[slot];
                    if (row.Cells.Count == 0 || row.Cells[0].Note < 0) continue;
                    var cell = row.Cells[0];
                    if (cell.Note == -3) continue;

                    int midiNote = (cell.Octave + 1) * 12 + cell.Note;
                    int durSlots = cell.Effects.TryGetValue(999, out int dv)
                        ? DurationToSlots((NoteDuration)dv) : 4;

                    int startFrame = (int)(slot * framesPerSixteenth);
                    int endFrame = (int)((slot + durSlots) * framesPerSixteenth);

                    for (int f = startFrame; f < endFrame && f < totalFrames; f++)
                        result[f][ti] = midiNote;
                }
            }
        }

        return result;
    }

    private static int DurationToSlots(NoteDuration d) => d switch
    {
        NoteDuration.Whole => 16, NoteDuration.Half => 8, NoteDuration.Quarter => 4,
        NoteDuration.Eighth => 2, _ => 1
    };
}
