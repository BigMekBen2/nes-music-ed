using System.Collections.Generic;

namespace NESMusicEditor.Models;

public class PatternCell
{
    public int Note { get; set; } = -1;
    public int Octave { get; set; } = 4;
    public int InstrumentId { get; set; } = -1;
    public int Volume { get; set; } = -1;
    public Dictionary<int, int> Effects { get; set; } = new();
}
