using System.Collections.Generic;

namespace NESMusicEditor.Models;

public class Pattern
{
    public int PatternId { get; set; }
    public int TrackIndex { get; set; }
    public int RowCount { get; set; } = 64;
    public List<PatternRow> Rows { get; set; } = new();
}

public class PatternRow
{
    public List<PatternCell> Cells { get; set; } = new();
}
