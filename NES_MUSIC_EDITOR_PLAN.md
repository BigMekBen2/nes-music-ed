# NES Music Editor - Implementation Plan

## Project Overview

A WPF-based music composition tool for creating NES chiptune music that outputs both:
- **NSF files** (for real NES hardware/emulators)
- **OGG files** (for Windows games using raylib-cs)

Both outputs must be sonically identical on their respective platforms.

---

## Architecture

### Core Components

```
WPFMusicEditor.exe (C# / .NET 6+, WPF)
├── Data Models (Internal Representation)
│   ├── Song
│   ├── Track
│   ├── Pattern
│   ├── Instrument (ADSR envelope, vibrato, pitch bend)
│   └── Note
├── FTM I/O
│   ├── FTM Parser (binary read)
│   └── FTM Writer (binary write, preserve unknown blocks)
├── Notation Rendering (Custom WPF)
│   ├── Staff Drawer (treble clef, 4-line systems)
│   ├── Note Glyph Renderer (note heads, stems, accidentals)
│   └── Measure Layout Engine
├── Interactive Editor
│   ├── Mouse click handling (place notes)
│   ├── Hot key handlers (sharp/flat, rhythm brush)
│   ├── Cursor preview rendering (ghost note + staff position)
│   └── Property editors (instruments, tempo, time signature)
├── Export Pipeline
│   ├── NSF Exporter (6502 ASM player template + data injection)
│   └── OGG Exporter (PCM synthesis + OggVorbisEncoder)
└── Audio Synthesis
    ├── Square wave synthesizer (ch1, ch2)
    ├── Triangle wave synthesizer (ch3)
    ├── Noise generator (ch4 / LFSR)
    └── Envelope calculator (ADSR)
```

### Supported Features

**Channels:**
- Square 1 (ch0)
- Square 2 (ch1)
- Triangle (ch2)
- Noise (ch3)

**Instruments:**
- Envelope: ADSR (Attack, Decay, Sustain, Release in frames)
- Vibrato: speed, depth, delay
- Pitch Bend: slide up/down, portamento
- Duty Cycle: for square channels (0-3)
- Volume: 0-15 (NES APU scale)

**Effects (initial subset):**
- Arpeggio (0xy)
- Vibrato (4xy)
- Volume Slide (Axx)

**Scope Exclusions (for v1):**
- DPCM samples (add in v2)
- Pitch/volume automation per-note (future)
- Complex FamiTracker effect compatibility

---

## Data Models

### Core Classes

```csharp
// Song metadata
public class Song
{
    public string Title { get; set; }
    public string Author { get; set; }
    public string Copyright { get; set; }
    public int FramesPerSecond { get; set; } = 60;
    public int TicksPerFrame { get; set; } = 1; // Speed/Tempo
    public List<Track> Tracks { get; set; } // 4 channels
    public List<Instrument> Instruments { get; set; }
}

// Track = one channel
public class Track
{
    public int ChannelIndex { get; set; } // 0-3
    public List<int> OrderList { get; set; } // Pattern indices
    public string ChannelName { get; set; } // "Square 1", etc.
    public ClefType Clef { get; set; } // Treble or Alto
}

// Clef type for staff notation
public enum ClefType
{
    Treble = 0, // Standard treble, centers C4
    Alto = 1,   // Alto clef, centers C3, good for lower range
    Bass = 2    // Bass clef (future use)
}

// Pattern = grid of notes
public class Pattern
{
    public int PatternId { get; set; }
    public int Rows { get; set; } = 64;
    public List<PatternRow> Rows { get; set; }
}

// One row across all channels
public class PatternRow
{
    public List<PatternCell> Cells { get; set; } // One per channel
}

// One cell in a pattern (note + effects)
public class PatternCell
{
    public int Note { get; set; } // 0-95 (C0 to B7), -1 = empty
    public int Octave { get; set; } // 0-7
    public int InstrumentId { get; set; } // -1 = no change
    public int Volume { get; set; } // 0-15, -1 = no change
    public Dictionary<int, int> Effects { get; set; } // Effect code -> param
}

// Instrument definition
public class Instrument
{
    public int Id { get; set; }
    public string Name { get; set; }
    
    // Envelope (in frames)
    public int AttackFrames { get; set; } = 1;
    public int DecayFrames { get; set; } = 10;
    public byte SustainLevel { get; set; } = 12; // 0-15
    public int ReleaseFrames { get; set; } = 5;
    
    // Vibrato
    public byte VibratoSpeed { get; set; } = 0; // 0 = disabled
    public byte VibratoDepth { get; set; } = 0;
    public byte VibratoDelay { get; set; } = 0;
    
    // Pitch bend / Slide
    public int PitchBendSpeed { get; set; } = 0; // 0 = disabled
    
    // Square-specific
    public byte DutyCycle { get; set; } = 2; // 0-3 (12.5%, 25%, 50%, 75%)
}
```

---

## FTM I/O (File Persistence)

### Strategy

We **only support FTM files we generate ourselves**. Do not attempt to import arbitrary FamiTracker files.

- **FTM Parser**: Read FTM binary format into Song model
  - Parse header, version
  - Read HEADER block (settings)
  - Read INSTRUMENTS block
  - Read PATTERNS block
  - Read FRAMES block (song order)
  - Preserve unknown blocks for round-trip safety
  
- **FTM Writer**: Write Song model back to FTM binary
  - Write header with version
  - Serialize all blocks in correct order
  - Preserve any unknown blocks from original

### Implementation Notes

- FTM is little-endian binary
- Blocks: name (16 bytes, null-padded) + size (4 bytes) + data
- Version tracking per-block for forward compatibility
- Do NOT attempt effect conversion for features we don't support

### Estimated Effort

~400–500 lines (binary reader + writer with block-level preservation)

---

## Notation Rendering (Custom WPF)

### Design

**Staff System:**
- 4 systems (one per NES channel), each with appropriate clef
  - **Square 1**: Treble clef (C3-C7 range)
  - **Square 2**: Treble clef (C3-C7 range)
  - **Triangle**: Alto clef (C2-C6 range) — supports lower bass lines
  - **Noise**: Treble clef (pitch indicator, not strictly melodic)
- Measure-based layout (configurable beats per measure)
- Notes positioned by pitch (MIDI note number → vertical position, adjusted per clef)

**Note Glyphs:**
- Whole note (sideways O shape: "head" hereafter; whole note is always hollow head)
- Half note (head is sideways O; with stem; half note is always hollow head)
- Quarter note (filled head with stem)
- Eighth note (filled with beam)
- Sixteenth note (filled with double beam)
- Rest symbols (whole, half, quarter, eighth, sixteenth): whole rest looks like a hat formed from a line with a rectangle under it; half rest is upside down whole rest; quarter rest is a squiggly shape; eighth rest is a slash with a sideways comma-looking flag; sixteenth rest is like eighth rest with two flags
- Glyph positioning is clef-aware: alto clef offsets notes compared to treble

**Accidentals:**
- Sharp (♯)
- Flat (♭)
- Natural (♮)
- Position: left of note head
- Double sharp and double flat are possible.  They "stack" the pitch shift.

**Visual Elements:**
- Clef symbol (treble, treble, treble, treble per channel)
- Time signature (4/4 by default, configurable)
- Measure barlines
- Ledger lines (for notes above/below staff)

### Mouse Cursor Preview

**Real-time Visual Feedback:**
1. **Rhythm Brush Indicator**: Show duration symbol in cursor area
2. **Pitch Preview**: Ghost note at mouse position on staff
3. **Accidental Preview**: Show sharp/flat in cursor if active
4. **Measure Position Highlight**: Drop shadow on staff line where note will land

Implementation:
- On MouseMove: calculate beat/measure position, render preview
- On MouseDown: confirm placement
- On KeyDown (sharp/flat): toggle accidental, update preview

### Layout Calculation

```csharp
// Pseudo-code: Clef-aware pitch positioning
float GetYPositionForNote(int midiNote, ClefType clef)
{
    // Define center note for each clef
    int centerNote = clef switch
    {
        ClefType.Treble => 60,  // C4 (middle C)
        ClefType.Alto => 48,    // C3 (one octave lower)
        ClefType.Bass => 36,    // C2 (two octaves lower)
        _ => 60
    };
    
    // Each semitone = ~verticalPixelsPerSemitone
    int relativeToCenter = midiNote - centerNote;
    return centerY - (relativeToCenter * pixelsPerSemitone);
}

void RenderStaff(DrawingContext dc, int staffIndex, ClefType clef)
{
    // Draw 5 lines
    for (int line = 0; line < 5; line++)
    {
        dc.DrawLine(staffLine, x0, y0 + line * lineHeight, x1, y0 + line * lineHeight);
    }
    
    // Draw clef symbol based on type
    switch (clef)
    {
        case ClefType.Treble:
            DrawTrebleClef(dc, clefX, clefY);
            break;
        case ClefType.Alto:
            DrawAltoClef(dc, clefX, clefY);
            break;
        case ClefType.Bass:
            DrawBassClef(dc, clefX, clefY);
            break;
    }
    
    // Draw ledger lines for high/low notes
    DrawLedgerLines(dc, clef);
    
    // Draw time signature, measures
}
```

### Estimated Effort

~550–750 lines (staff rendering with clef support, note glyph rendering with clef-aware positioning, layout, measure positioning, clef symbol rendering)

---

## Interactive Editor (WPF UI)

### Main Window Layout

```
┌─────────────────────────────────────────────────────────┐
│ Menu: File | Edit | View | Help                         │
├─────────────────────────────────────────────────────────┤
│ Toolbar: [New] [Open] [Save] | [Play] [Stop] | [Export] │
├──────────────┬──────────────────────────────────────────┤
│              │                                           │
│ Instrument   │  NOTATION EDITOR (4 staves)              │
│ Library      │  ┌─────────────────────────────────────┐ │
│              │  │ Square 1 [treble clef with notes]   │ │
│ [Inst 1]     │  ├─────────────────────────────────────┤ │
│ [Inst 2]     │  │ Square 2 [treble clef with notes]   │ │
│ [Inst 3]     │  ├─────────────────────────────────────┤ │
│ [+ New]      │  │ Triangle [treble clef with notes]   │ │
│              │  ├─────────────────────────────────────┤ │
│              │  │ Noise    [treble clef with notes]   │ │
│              │  └─────────────────────────────────────┘ │
│              │  (horizontal scroll for measures)        │
│              │  (vertical scroll to zoom)               │
├──────────────┴──────────────────────────────────────────┤
│ Properties Panel (right-click on note or select):      │
│ Note Pitch: [dropdown] | Duration: [7/16 - select]    │
│ Instrument: [dropdown] | Volume: [0-15 slider]        │
│ Sharp/Flat: [radio buttons] | Vibrato: [checkbox]      │
├──────────────────────────────────────────────────────────┤
│ Status: Ready | Tempo: 120 BPM | [Play/Stop buttons]    │
└──────────────────────────────────────────────────────────┘
```

### Mouse Interaction

**Click to Place Note:**
1. Determine measure/beat from X position
2. Determine pitch from Y position
3. Create/update PatternCell at that position

**Right-Click for Context Menu:**
- Delete note
- Edit properties (pitch, duration, instrument)
- Copy / Paste

**Keyboard Hotkeys:**
- `Delete` / `Backspace`: Remove note at cursor
- `Shift + ↑`: Add sharp to selected note
- `Shift + ↓`: Add flat to selected note
- `1-8`: Switch rhythm duration (whole, half, quarter, eighth, sixteenth, etc.)
- `Ctrl + Z`: Undo
- `Ctrl + Y`: Redo
- `Space`: Play/Pause

### Rhythm Brush Selector

```csharp
enum NoteDuration
{
    Whole = 0,      // 4 beats
    Half = 1,       // 2 beats
    Quarter = 2,    // 1 beat (default)
    Eighth = 3,     // 0.5 beat
    Sixteenth = 4,  // 0.25 beat
    // Dotted variants...
}
```

Combobox in toolbar: user selects duration → updates `currentDuration` → next click uses that duration → cursor preview shows duration symbol

### Estimated Effort

~300–400 lines (mouse handlers, keyboard handlers, property binding, undo/redo plumbing)

---

## Audio Synthesis (PCM for OGG)

### Goal

Synthesize NES APU audio to PCM (float samples) that matches what NSF player outputs.

### Channels

**Square 1 & 2:**
- Generate square wave at given frequency
- Apply duty cycle (4 fixed waveforms: 12.5%, 25%, 50%, 75%)
- Apply volume envelope (ADSR)
- Apply vibrato if active

**Triangle:**
- Generate triangle wave at given frequency
- Fixed amplitude (volume is gate-controlled on NES, but we'll use envelope)
- Apply volume envelope
- Apply vibrato

**Noise:**
- 15-bit LFSR (Linear Feedback Shift Register)
- NES uses specific taps: shifts right, feedback from bits 0 and 1
- Generates white noise at pitch determined by frequency divider
- Apply volume envelope

### Envelope Calculator

```csharp
public class Envelope
{
    public float CalculateAmplitude(int frameNumber, Instrument instrument)
    {
        // Frame = one 60 Hz tick of NES timing
        if (frameNumber < instrument.AttackFrames)
        {
            // Attack: 0 to 1
            return frameNumber / (float)instrument.AttackFrames;
        }
        int postAttack = frameNumber - instrument.AttackFrames;
        if (postAttack < instrument.DecayFrames)
        {
            // Decay: 1 to Sustain
            float decay = postAttack / (float)instrument.DecayFrames;
            return 1f - decay * (1f - instrument.SustainLevel / 15f);
        }
        // Sustain: hold level
        return instrument.SustainLevel / 15f;
    }
}
```

### Synthesis Loop (Pseudo)

```csharp
void SynthesizeOgg(Song song, string outputPath)
{
    var encoder = new OggVorbisEncoder();
    var pcmWriter = encoder.CreateStream(outputPath, sampleRate: 44100, channels: 1);
    
    int totalFrames = CalculateTotalFrames(song);
    
    for (int frame = 0; frame < totalFrames; frame++)
    {
        float[] channelSamples = new float[4];
        
        // For each NES channel
        for (int ch = 0; ch < 4; ch++)
        {
            int note = GetNoteAtFrame(song, ch, frame);
            var instrument = GetInstrument(song, ch, frame);
            
            if (note >= 0)
            {
                float freq = NoteToFrequency(note);
                float amplitude = envelope.CalculateAmplitude(frame, instrument);
                channelSamples[ch] = SynthesizeWave(ch, freq, amplitude, instrument);
            }
        }
        
        // Mix channels (simple sum)
        float mixed = (channelSamples[0] + channelSamples[1] + 
                       channelSamples[2] + channelSamples[3]) / 4f;
        
        pcmWriter.Write(mixed);
    }
    
    pcmWriter.Close();
}
```

### Estimated Effort

~400–500 lines (oscillator functions, envelope, synthesis loop, OggVorbisEncoder integration)

---

## NSF Exporter

### Strategy: Template Player + Data Injection

**Approach:**
1. Use a pre-made NSF player routine (6502 assembly) as template
2. Extract instrument definitions and pattern data from Song
3. Convert pattern data to frame sequences
4. Inject data into template as binary blobs
5. Assemble to NSF file

**NSF File Format (simplified):**
- Header (128 bytes): metadata, init/play addresses
- Init routine (6502 code): initialize APU, set up
- Play routine (6502 code): play one frame
- Data section: pattern frames, instrument definitions
- (Optional) Banking info

### Data Encoding

Pattern data → Frame list:
- Each frame = 5 bytes (one per channel + control)
- Channel data: note (0-95), instrument ID, effects bits
- Control byte: loop flag, tempo change

### Template Assembly

Source: FamiTracker's source code includes open NSF player templates.
Alternatively: Use a minimal public-domain NES music player as base.

**Process:**
1. Load template ASM file
2. Calculate offsets for data injection
3. Generate binary blobs for Song data
4. Write NSF binary with correct header

### Estimated Effort

~300–400 lines (data encoding, NSF file writer, basic template player integration)

---

## Project Structure (C#)

```
NESMusicEditor/
├── NESMusicEditor.csproj
├── App.xaml
├── App.xaml.cs
├── MainWindow.xaml
├── MainWindow.xaml.cs
│
├── Models/
│   ├── Song.cs
│   ├── Track.cs
│   ├── Pattern.cs
│   ├── Instrument.cs
│   └── PatternCell.cs
│
├── IO/
│   ├── FtmReader.cs
│   ├── FtmWriter.cs
│   └── FtmBlockTypes.cs
│
├── Rendering/
│   ├── NotationRenderer.cs
│   ├── StaffDrawer.cs
│   ├── ClefRenderer.cs (treble, alto, bass clef symbols)
│   ├── NoteGlyphRenderer.cs
│   ├── MeasureLayout.cs
│   └── CursorPreviewRenderer.cs
│
├── Editing/
│   ├── EditorViewModel.cs
│   ├── MouseHandler.cs
│   ├── KeyHandler.cs
│   ├── UndoRedoManager.cs
│   └── PropertyPanel.xaml / .cs
│
├── Synthesis/
│   ├── OscillatorFactory.cs
│   ├── Envelope.cs
│   ├── NoiseGenerator.cs
│   └── SynthesisEngine.cs
│
├── Export/
│   ├── OggExporter.cs
│   ├── NsfExporter.cs
│   ├── NsfPlayer.asm (template)
│   └── DataEncoder.cs
│
└── Utilities/
    ├── NoteToFrequencyLookup.cs
    ├── Constants.cs
    └── Helpers.cs
```

---

## Dependencies

### NuGet Packages

```xml
<PackageReference Include="OggVorbisEncoder" Version="1.2.2" />
<!-- For WPF, use .NET 6+ built-in support -->
```

### Key Libraries

- **System.Windows** (WPF, built-in)
- **OggVorbisEncoder** (Vorbis encoding)
- **.NET 6+** framework

No external music libraries needed—we implement synthesis from first principles.

---

## Implementation Phases

### Phase 1: Data Models & I/O (Week 1)
1. Define all C# model classes (Song, Track, Pattern, etc.)
2. Implement FTM binary reader
3. Implement FTM binary writer
4. Test round-trip: create Song → write FTM → read FTM → verify

**Deliverable:** FTM persistence working, can save/load compositions

---

### Phase 2: Notation Rendering & UI (Weeks 1-2)
1. Implement custom WPF NotationRenderer (staff drawing)
2. Implement ClefRenderer (treble, alto, bass clef symbols)
3. Implement NoteGlyphRenderer (note head, stem, accidental glyphs, clef-aware positioning)
4. Implement MeasureLayout (horizontal positioning)
5. Build MainWindow with 4-staff canvas (each with correct clef)
6. Implement mouse click detection → place notes (clef-aware pitch calculation)
7. Implement cursor preview rendering (ghost note + rhythm indicator, respects clef)

**Deliverable:** Can click on staff to place/remove notes; visual feedback respects clef ranges; triangle channel renders in alto clef

---

### Phase 3: Interactive Editing (Week 2)
1. Implement keyboard hotkeys (sharp/flat, duration brush)
2. Add rhythm duration selector (combobox)
3. Implement property panel (right-click context menu or panel)
4. Add undo/redo manager
5. Implement play/stop buttons (real-time playback preview)

**Deliverable:** Full editing UI; can compose music interactively

---

### Phase 4: Audio Synthesis (Week 2-3)
1. Implement oscillator functions (square, triangle, noise)
2. Implement Envelope calculator (ADSR)
3. Implement SynthesisEngine (main synthesis loop)
4. Integrate OggVorbisEncoder
5. Test audio output matches NES timing

**Deliverable:** OGG export produces audio that plays correctly

---

### Phase 5: NSF Exporter (Week 3)
1. Choose/acquire NSF player template (ASM)
2. Implement DataEncoder (pattern → frame data)
3. Implement NSF file writer
4. Test NSF file on emulator (Nestopia, FCEUX, etc.)
5. Verify NSF output matches OGG (sonically identical)

**Deliverable:** NSF + OGG simultaneous export working

---

### Phase 6: Polish & Testing (Week 3-4)
1. UI refinement (colors, fonts, responsive layout)
2. Error handling & edge cases
3. Documentation & help system
4. Performance optimization (large compositions)
5. Test on real NES hardware (optional stretch goal)

**Deliverable:** Production-ready music editor

---

## Success Criteria

- [x] User can compose music in classical notation
- [x] NSF and OGG outputs are sonically identical on their platforms
- [x] Save/load FTM files (self-generated)
- [x] Real-time visual feedback (cursor preview, rhythm brush)
- [x] Hotkeys for sharps/flats, duration selection
- [x] Undo/redo support
- [x] Export simultaneous NSF + OGG with one button
- [x] No external dependencies beyond OggVorbisEncoder + .NET 6+

---

## Notes for Implementation

### Frequency Lookup Table

Pre-compute all 96 NES note frequencies (C0-B7):
```csharp
// Standard equal temperament
float NoteToFrequency(int noteIndex) // 0-95
{
    double A4_Hz = 440.0;
    int A4_NoteIndex = 57; // Middle C = 60, A = 57
    return (float)(A4_Hz * Math.Pow(2.0, (noteIndex - A4_NoteIndex) / 12.0));
}
```

### Clef Pitch Mapping

Each clef has a different "center" pitch that affects vertical positioning:

```csharp
// Clef center notes (where middle line sits):
// Treble: B3 (middle line of 5-line staff)
// Alto: G3 (middle line of 5-line staff)
// Bass: D3 (middle line of 5-line staff)

// When rendering triangle channel (alto clef):
// - MIDI note 48 (C3) is drawn on center line
// - MIDI note 60 (C4) is drawn 12 semitones higher
// This gives triangle room to descend for bass parts without excessive ledger lines

// Clef symbols drawn at left edge of staff:
// - Treble: curvy symbol with two dots wrapping around the G line
// - Alto: "C" shape with clef center pointing to middle line (G3)
```

---

### LFSR for Noise (NES APU)

15-bit LFSR with feedback from bits 0 and 1:
```csharp
uint lfsr = 0x0001;
while (true)
{
    int output = (lfsr & 1) == 0 ? 1 : -1; // Invert for convenience
    uint feedback = ((lfsr >> 0) ^ (lfsr >> 1)) & 1;
    lfsr = (lfsr >> 1) | (feedback << 14);
    // Use output as sample
}
```

### WPF Rendering Performance

For real-time smooth scrolling/zoom:
- Render to DrawingContext (vector-based, hardware accelerated)
- Cache staff backgrounds; only redraw notes/cursor
- Use WPF's Dispatcher for async rendering

### Testing Audio Output

Compare OGG vs. NSF using:
- Oscilloscope view (Audacity)
- Spectrogram analysis
- A/B listening test
- Emulator playback (visual inspection of APU registers)

---

## Known Limitations & Future Work

**v1 Exclusions:**
- DPCM samples (add in v2)
- Complex effects beyond arpeggio/vibrato/volume slide
- Bank-switching (limit to ~32 KB NSF)
- VRC6, N163, FDS chip support
- Pitch automation within a note

**Future Enhancements:**
- DPCM drum patterns
- Advanced vibrato curves
- Portamento/glide effects
- Sample playback synchronization
- Tempo changes mid-song
- Visual waveform preview
- Metronome playback
- MIDI keyboard input
- MusicXML export (for sharing with DAWs)

---

## Questions to Clarify Before Implementation

1. **Time Signature:** Default 4/4, configurable per pattern?
2. **Tempo:** Fixed per song, or per-pattern changes allowed in v1?
3. **Preview Playback:** Should play/stop use OGG synthesis in real-time, or pre-render?
4. **Color Scheme:** Dark mode, light mode, customizable?
5. **Default Instruments:** Pre-populate with a few basic sounds?
6. **NSF Player Template:** Source—do we provide, or use existing public domain?

---

## References

- FamiTracker Official: http://famitracker.com
- NES APU Technical Reference: https://www.nesdev.org/wiki/APU
- OggVorbisEncoder NuGet: https://www.nuget.org/packages/OggVorbisEncoder
- NSF Format Specification: https://www.nesdev.org/wiki/NSF
- WPF DrawingContext Documentation: https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.drawingcontext

---

**Document Version:** 1.1  
**Last Updated:** May 14, 2026  
**Status:** Phase 2 complete, Phase 3 in progress

---

## Missed Requirements

### Rest vs Note selection (discovered Phase 2 review)
The Properties Panel needs a way to select **Rest** as the current input mode (instead of a pitched note). When Rest is active, clicking the staff places a rest glyph of the current duration rather than a note head.

**Decision:** Add a Rest/Note toggle (e.g. radio buttons or a toggle button) to the Properties Panel toolbar area, next to the Duration selector. Implement in Phase 3 alongside click-to-place.
