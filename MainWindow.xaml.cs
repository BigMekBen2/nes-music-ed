using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using NESMusicEditor.Controls;
using NESMusicEditor.Controls.Rendering;
using NESMusicEditor.Models;

namespace NESMusicEditor;

public partial class MainWindow : Window
{
    private readonly Song _song;

    public MainWindow()
    {
        InitializeComponent();
        _song = CreateDemoSong();
        WireSong();
    }

    private void WireSong()
    {
        StaffSquare1.Song = _song;
        StaffSquare2.Song = _song;
        StaffTriangle.Song = _song;
        StaffNoise.Song = _song;

        StaffTriangle.ClefType = ClefType.Alto;
    }

    private static Song CreateDemoSong()
    {
        var song = new Song { Title = "Demo" };

        // Track 0: Square 1 — treble, variety of note durations + accidentals + ledger line
        var sq1 = new TrackWithNotes { ChannelIndex = 0, ChannelName = "Square 1", Clef = ClefType.Treble };
        sq1.OrderList.Add(0);
        // Measure 0: C4 whole, E4 half, G4 quarter, B4 quarter
        sq1.DemoNotes.Add(new DemoNote(0, 0, 60, NoteDuration.Whole));    // C4
        sq1.DemoNotes.Add(new DemoNote(0, 1, 64, NoteDuration.Half));     // E4
        sq1.DemoNotes.Add(new DemoNote(0, 2, 67, NoteDuration.Quarter));  // G4
        sq1.DemoNotes.Add(new DemoNote(0, 3, 71, NoteDuration.Quarter));  // B4
        // Measure 1: D5 eighth, F#4 quarter(sharp), Bb4 quarter(flat), C6 ledger
        sq1.DemoNotes.Add(new DemoNote(1, 0, 74, NoteDuration.Eighth));   // D5
        sq1.DemoNotes.Add(new DemoNote(1, 1, 66, NoteDuration.Quarter, 1));  // F#4 sharp
        sq1.DemoNotes.Add(new DemoNote(1, 2, 70, NoteDuration.Quarter, -1)); // Bb4 flat
        sq1.DemoNotes.Add(new DemoNote(1, 3, 84, NoteDuration.Sixteenth)); // C6 (ledger line above)

        // Track 1: Square 2 — treble, simple melody
        var sq2 = new TrackWithNotes { ChannelIndex = 1, ChannelName = "Square 2", Clef = ClefType.Treble };
        sq2.DemoNotes.Add(new DemoNote(0, 0, 67, NoteDuration.Quarter));  // G4
        sq2.DemoNotes.Add(new DemoNote(0, 1, 69, NoteDuration.Quarter));  // A4
        sq2.DemoNotes.Add(new DemoNote(0, 2, 71, NoteDuration.Half));     // B4
        sq2.DemoNotes.Add(new DemoNote(1, 0, 72, NoteDuration.Whole));    // C5

        // Track 2: Triangle — alto clef, lower notes
        var tri = new TrackWithNotes { ChannelIndex = 2, ChannelName = "Triangle", Clef = ClefType.Alto };
        tri.DemoNotes.Add(new DemoNote(0, 0, 48, NoteDuration.Whole));    // C3
        tri.DemoNotes.Add(new DemoNote(0, 2, 52, NoteDuration.Half));     // E3
        tri.DemoNotes.Add(new DemoNote(1, 0, 55, NoteDuration.Quarter));  // G3
        tri.DemoNotes.Add(new DemoNote(1, 1, 57, NoteDuration.Eighth));   // A3
        tri.DemoNotes.Add(new DemoNote(1, 2, 59, NoteDuration.Quarter, 1)); // B3 sharp

        // Track 3: Noise — treble, sparse
        var noise = new TrackWithNotes { ChannelIndex = 3, ChannelName = "Noise", Clef = ClefType.Treble };
        noise.DemoNotes.Add(new DemoNote(0, 0, 60, NoteDuration.Quarter));
        noise.DemoNotes.Add(new DemoNote(0, 2, 60, NoteDuration.Quarter));
        noise.DemoNotes.Add(new DemoNote(1, 0, 60, NoteDuration.Quarter));
        noise.DemoNotes.Add(new DemoNote(1, 2, 60, NoteDuration.Eighth));

        song.Tracks.Add(sq1);
        song.Tracks.Add(sq2);
        song.Tracks.Add(tri);
        song.Tracks.Add(noise);

        return song;
    }

    private void DurationCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var duration = (NoteDuration)DurationCombo.SelectedIndex;
        foreach (var staff in new[] { StaffSquare1, StaffSquare2, StaffTriangle, StaffNoise })
            staff.CurrentDuration = duration;
    }

    private void Accidental_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton rb || rb.Tag is not string tag) return;
        int accidental = int.Parse(tag);
        foreach (var staff in new[] { StaffSquare1, StaffSquare2, StaffTriangle, StaffNoise })
            staff.CurrentAccidental = accidental;
    }

    private void Menu_Stub(object sender, RoutedEventArgs e) { }
    private void Toolbar_Stub(object sender, RoutedEventArgs e) { }
}
