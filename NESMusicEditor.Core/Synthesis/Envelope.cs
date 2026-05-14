using System;
using NESMusicEditor.Models;

namespace NESMusicEditor.Synthesis;

public class Envelope
{
    // frame = one 60Hz tick
    public float CalculateAmplitude(int framesSinceNoteOn, int framesSinceNoteOff, Instrument inst)
    {
        // Attack
        if (framesSinceNoteOn < inst.AttackFrames)
            return framesSinceNoteOn / (float)inst.AttackFrames;
        int postAttack = framesSinceNoteOn - inst.AttackFrames;
        // Decay
        if (postAttack < inst.DecayFrames)
        {
            float t = postAttack / (float)inst.DecayFrames;
            return 1f - t * (1f - inst.SustainLevel / 15f);
        }
        // Release (note off)
        if (framesSinceNoteOff >= 0)
        {
            if (inst.ReleaseFrames <= 0) return 0f;
            float sustain = inst.SustainLevel / 15f;
            float t = Math.Min(framesSinceNoteOff / (float)inst.ReleaseFrames, 1f);
            return sustain * (1f - t);
        }
        // Sustain
        return inst.SustainLevel / 15f;
    }
}
