using System.Collections.Generic;

namespace NESMusicEditor.Models;

public enum ClefType { Treble = 0, Alto = 1, Bass = 2 }

public class Track
{
    public int ChannelIndex { get; set; }
    public string ChannelName { get; set; } = "";
    public ClefType Clef { get; set; } = ClefType.Treble;
    public List<int> OrderList { get; set; } = new();
}
