namespace NESMusicEditor.Models;

public class Instrument
{
    public int Id { get; set; }
    public string Name { get; set; } = "New Instrument";
    public int AttackFrames { get; set; } = 1;
    public int DecayFrames { get; set; } = 10;
    public byte SustainLevel { get; set; } = 12;
    public int ReleaseFrames { get; set; } = 5;
    public byte VibratoSpeed { get; set; } = 0;
    public byte VibratoDepth { get; set; } = 0;
    public byte VibratoDelay { get; set; } = 0;
    public int PitchBendSpeed { get; set; } = 0;
    public byte DutyCycle { get; set; } = 2;
}
