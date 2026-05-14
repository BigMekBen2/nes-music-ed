using System;
using System.Collections.Generic;
using System.IO;
using NESMusicEditor.Models;

namespace NESMusicEditor.Synthesis;

/// <summary>
/// Exports a Song to NES Sound Format (NSF).
/// Layout:
///   File $0000–$007F : NSF header (128 bytes)
///   File $0080–$00FF : 6502 player code (128 bytes, NES $8000)
///   File $0100–$015F : APU timer lo table, 96 entries (NES $8080)
///   File $0160–$01BF : APU timer hi table, 96 entries (NES $80E0)
///   File $01C0–$01FF : padding to align song data to NES $8200 = file $01C0...
///                      Actually NES $8200 = file offset (0x8200-0x8000)+0x80 = 0x0280
///   File $0280+      : frame data, 7 bytes/frame, terminated by 7×$FF
/// </summary>
public class NsfExporter
{
    // NES load address
    private const int LoadAddr = 0x8000;
    // APU timer tables at NES $8080 / $80E0
    private const int TimerLoAddr = 0x8080;
    private const int TimerHiAddr = 0x80E0;
    // Song data starts at NES $8200
    private const int SongDataAddr = 0x8200;

    // Pre-assembled 6502 player.
    // INIT = $8000, PLAY = $800E
    // References timer lo at $8080, timer hi at $80E0, song data ptr from $8200 init.
    private static readonly byte[] PlayerCode = new byte[]
    {
        // INIT ($8000): enable channels, frame counter, init pointer to song data
        0xA9, 0x0F,             // LDA #$0F
        0x8D, 0x15, 0x40,       // STA $4015
        0xA9, 0x40,             // LDA #$40
        0x8D, 0x17, 0x40,       // STA $4017
        0xA9, 0x00,             // LDA #$00  (lo byte of SongDataAddr $8200)
        0x85, 0x00,             // STA $00
        0xA9, 0x82,             // LDA #$82  (hi byte of $8200)
        0x85, 0x01,             // STA $01
        0x60,                   // RTS       -- end of INIT, addr $8012

        // PLAY ($8013): read 7-byte frame record, write APU, advance pointer
        // --- Square 1 (bytes 0, 1) ---
        0xA0, 0x00,             // LDY #0
        0xB1, 0x00,             // LDA ($00),Y  ; note index
        0xC9, 0xFF,             // CMP #$FF
        0xF0, 0x1A,             // BEQ sq1_sil  (+26 bytes)
        0xAA,                   // TAX
        0xBD, 0x80, 0x80,       // LDA $8080,X  ; timer lo
        0x8D, 0x02, 0x40,       // STA $4002
        0xBD, 0xE0, 0x80,       // LDA $80E0,X  ; timer hi
        0x29, 0x07,             // AND #$07
        0x8D, 0x03, 0x40,       // STA $4003
        0xC8,                   // INY
        0xB1, 0x00,             // LDA ($00),Y  ; volume
        0x09, 0x30,             // ORA #$30     ; duty=01, constant vol
        0x8D, 0x00, 0x40,       // STA $4000
        0x4C, 0x41, 0x80,       // JMP sq1_done ($8041 = PLAY+0x2E)
        // sq1_sil ($803E):
        0xA9, 0x00,             // LDA #0
        0x8D, 0x00, 0x40,       // STA $4000
        // sq1_done ($8041):

        // --- Square 2 (bytes 2, 3) ---
        0xA0, 0x02,             // LDY #2
        0xB1, 0x00,             // LDA ($00),Y
        0xC9, 0xFF,             // CMP #$FF
        0xF0, 0x1A,             // BEQ sq2_sil  (+26)
        0xAA,
        0xBD, 0x80, 0x80,       // LDA $8080,X
        0x8D, 0x06, 0x40,       // STA $4006
        0xBD, 0xE0, 0x80,       // LDA $80E0,X
        0x29, 0x07,
        0x8D, 0x07, 0x40,       // STA $4007
        0xC8,
        0xB1, 0x00,
        0x09, 0x30,
        0x8D, 0x04, 0x40,       // STA $4004
        0x4C, 0x6F, 0x80,       // JMP sq2_done ($806F)
        // sq2_sil ($806C):
        0xA9, 0x00,
        0x8D, 0x04, 0x40,
        // sq2_done ($806F):

        // --- Triangle (byte 4) ---
        0xA0, 0x04,             // LDY #4
        0xB1, 0x00,
        0xC9, 0xFF,
        0xF0, 0x16,             // BEQ tri_sil (+22)
        0xAA,
        0xBD, 0x80, 0x80,       // LDA $8080,X
        0x8D, 0x0A, 0x40,       // STA $400A
        0xBD, 0xE0, 0x80,       // LDA $80E0,X
        0x29, 0x07,
        0x8D, 0x0B, 0x40,       // STA $400B
        0xA9, 0x80,             // LDA #$80
        0x8D, 0x08, 0x40,       // STA $4008
        0x4C, 0x97, 0x80,       // JMP tri_done ($8097)
        // tri_sil ($8094):
        0xA9, 0x00,
        0x8D, 0x08, 0x40,
        // tri_done ($8097):

        // --- Noise (bytes 5, 6) ---
        0xA0, 0x05,             // LDY #5
        0xB1, 0x00,
        0xC9, 0xFF,
        0xF0, 0x11,             // BEQ noise_sil (+17)
        0x29, 0x0F,             // AND #$0F
        0x8D, 0x0E, 0x40,       // STA $400E
        0xC8,
        0xB1, 0x00,
        0x09, 0x30,
        0x8D, 0x0C, 0x40,       // STA $400C
        0x4C, 0xB8, 0x80,       // JMP noise_done ($80B8)
        // noise_sil ($80B5):
        0xA9, 0x00,
        0x8D, 0x0C, 0x40,
        // noise_done ($80B8):

        // Advance pointer by 7
        0x18,                   // CLC
        0xA5, 0x00,             // LDA $00
        0x69, 0x07,             // ADC #7
        0x85, 0x00,             // STA $00
        0x90, 0x02,             // BCC done
        0xE6, 0x01,             // INC $01
        // done:
        0x60,                   // RTS
    };

    public void Export(Song song, string outputPath)
    {
        var frames = BuildFrameSequence(song);

        using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        using var bw = new BinaryWriter(fs);

        // --- Header (128 bytes) ---
        WriteHeader(bw, song);

        // --- Player code (128 bytes, NES $8000) ---
        var code = new byte[128];
        Array.Copy(PlayerCode, code, Math.Min(PlayerCode.Length, 128));
        bw.Write(code);

        // --- Timer lo table (96 bytes, NES $8080, file $0100) ---
        for (int n = 0; n < 96; n++)
        {
            var (lo, _) = NoteToApuTimer(n);
            bw.Write(lo);
        }

        // --- Timer hi table (96 bytes, NES $80E0, file $0160) ---
        for (int n = 0; n < 96; n++)
        {
            var (_, hi) = NoteToApuTimer(n);
            bw.Write(hi);
        }

        // Padding from file $01C0 to $0280 (NES $81C0–$8200): 192 bytes
        // Current pos: 0x80 + 0x80 + 0x60 + 0x60 = 0x200; need 0x280
        int padBytes = 0x0280 - (int)fs.Position;
        if (padBytes > 0) bw.Write(new byte[padBytes]);

        // --- Song frame data (NES $8200, file $0280) ---
        foreach (var rec in frames)
            bw.Write(rec);

        // End marker
        bw.Write(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF });
    }

    private static void WriteHeader(BinaryWriter bw, Song song)
    {
        // Magic
        bw.Write(new byte[] { 0x4E, 0x45, 0x53, 0x4D, 0x1A }); // "NESM\x1A"
        bw.Write((byte)0x01);   // version
        bw.Write((byte)0x01);   // total songs
        bw.Write((byte)0x01);   // starting song

        bw.Write((ushort)LoadAddr);  // load address
        bw.Write((ushort)LoadAddr);  // init address = $8000
        // Play address = $8013 (PLAY routine offset in PlayerCode)
        bw.Write((ushort)0x8013);    // play address

        WriteFixedString(bw, song.Title ?? "Untitled", 32);
        WriteFixedString(bw, "", 32); // artist
        WriteFixedString(bw, "", 32); // copyright

        bw.Write((ushort)16666); // NTSC speed ~60Hz
        bw.Write(new byte[8]);   // bankswitch (none)
        bw.Write((ushort)0);     // PAL speed
        bw.Write((byte)0x00);    // PAL/NTSC: NTSC only
        bw.Write((byte)0x00);    // extra chips: none
        bw.Write(new byte[4]);   // reserved
        // Total so far: 5+1+1+1+2+2+2+32+32+32+2+8+2+1+1+4 = 128 bytes
    }

    private static void WriteFixedString(BinaryWriter bw, string s, int len)
    {
        var buf = new byte[len];
        var bytes = System.Text.Encoding.ASCII.GetBytes(s);
        Array.Copy(bytes, buf, Math.Min(bytes.Length, len));
        bw.Write(buf);
    }

    private static (byte lo, byte hi) NoteToApuTimer(int noteIndex)
    {
        // noteIndex 0-95 maps to MIDI notes 0-95 (C-1 to B7)
        double freq = 440.0 * Math.Pow(2.0, (noteIndex - 69) / 12.0);
        int period = (int)Math.Round(1789773.0 / (16.0 * freq) - 1);
        period = Math.Clamp(period, 0, 0x7FF);
        return ((byte)(period & 0xFF), (byte)((period >> 8) & 0x07));
    }

    // Returns list of 7-byte frame records
    private static List<byte[]> BuildFrameSequence(Song song)
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

        // Per-frame: [sq1note, sq1vol, sq2note, sq2vol, trinote, noiseperiod, noisevol]
        // 0xFF = silence
        var notePerFrame = new int[4][];
        for (int ch = 0; ch < 4; ch++)
        {
            notePerFrame[ch] = new int[totalFrames];
            Array.Fill(notePerFrame[ch], -1);
        }

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
                    if (row.Cells.Count == 0) continue;
                    var cell = row.Cells[0];
                    if (cell.Note < 0) continue; // rest or continuation

                    int midiNote = (cell.Octave + 1) * 12 + cell.Note;
                    int durSlots = cell.Effects.TryGetValue(999, out int dv)
                        ? DurationToSlots((NoteDuration)dv) : 4;

                    int startFrame = (int)(slot * framesPerSixteenth);
                    int endFrame = (int)((slot + durSlots) * framesPerSixteenth);
                    for (int f = startFrame; f < endFrame && f < totalFrames; f++)
                        notePerFrame[ti][f] = midiNote;
                }
            }
        }

        var result = new List<byte[]>(totalFrames);
        for (int f = 0; f < totalFrames; f++)
        {
            var rec = new byte[7];

            // Square 1
            int n = notePerFrame[0][f];
            if (n >= 0 && n < 96) { rec[0] = (byte)n; rec[1] = 12; }
            else { rec[0] = 0xFF; rec[1] = 0; }

            // Square 2
            n = notePerFrame[1][f];
            if (n >= 0 && n < 96) { rec[2] = (byte)n; rec[3] = 12; }
            else { rec[2] = 0xFF; rec[3] = 0; }

            // Triangle
            n = notePerFrame[2][f];
            if (n >= 0 && n < 96) { rec[4] = (byte)n; }
            else rec[4] = 0xFF;

            // Noise
            n = notePerFrame[3][f];
            if (n >= 0)
            {
                rec[5] = (byte)Math.Clamp((n - 48) / 6, 0, 15);
                rec[6] = 12;
            }
            else { rec[5] = 0xFF; rec[6] = 0; }

            result.Add(rec);
        }

        return result;
    }

    private static int DurationToSlots(NoteDuration d) => d switch
    {
        NoteDuration.Whole => 16, NoteDuration.Half => 8, NoteDuration.Quarter => 4,
        NoteDuration.Eighth => 2, _ => 1
    };
}
