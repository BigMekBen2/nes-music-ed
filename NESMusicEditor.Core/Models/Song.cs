using System.Collections.Generic;

namespace NESMusicEditor.Models;

public class Song
{
    public string Title { get; set; } = "Untitled";
    public string Author { get; set; } = "";
    public string Copyright { get; set; } = "";
    public int FramesPerSecond { get; set; } = 60;
    public int TicksPerFrame { get; set; } = 1;
    public List<Track> Tracks { get; set; } = new();
    public List<Instrument> Instruments { get; set; } = new();
    public List<Pattern> Patterns { get; set; } = new();
}
