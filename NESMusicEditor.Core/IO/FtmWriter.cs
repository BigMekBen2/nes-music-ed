using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NESMusicEditor.Models;

namespace NESMusicEditor.IO;

public static class FtmWriter
{
    private static readonly byte[] Magic = { 0x4E, 0x45, 0x53, 0x4D }; // NESM

    public static void Write(Song song, string path)
    {
        var blocks = new List<(string Name, byte[] Data)>
        {
            ("HEADER", WriteHeader(song)),
            ("INSTRUMENTS", WriteInstruments(song)),
            ("TRACKS", WriteTracks(song)),
            ("PATTERNS", WritePatterns(song)),
        };

        using var fs = File.OpenWrite(path);
        using var bw = new BinaryWriter(fs, Encoding.UTF8, leaveOpen: false);

        bw.Write(Magic);
        bw.Write((ushort)1); // version
        bw.Write((ushort)blocks.Count);

        foreach (var (name, data) in blocks)
        {
            WriteBlockName(bw, name);
            bw.Write((uint)data.Length);
            bw.Write(data);
        }
    }

    private static byte[] WriteHeader(Song song)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        WriteFixedString(bw, song.Title, 64);
        WriteFixedString(bw, song.Author, 64);
        WriteFixedString(bw, song.Copyright, 64);
        bw.Write(song.FramesPerSecond);
        bw.Write(song.TicksPerFrame);
        return ms.ToArray();
    }

    private static byte[] WriteInstruments(Song song)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(song.Instruments.Count);
        foreach (var inst in song.Instruments)
        {
            bw.Write(inst.Id);
            WriteFixedString(bw, inst.Name, 32);
            bw.Write(inst.AttackFrames);
            bw.Write(inst.DecayFrames);
            bw.Write(inst.SustainLevel);
            bw.Write(inst.ReleaseFrames);
            bw.Write(inst.VibratoSpeed);
            bw.Write(inst.VibratoDepth);
            bw.Write(inst.VibratoDelay);
            bw.Write(inst.PitchBendSpeed);
            bw.Write(inst.DutyCycle);
        }
        return ms.ToArray();
    }

    private static byte[] WriteTracks(Song song)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(song.Tracks.Count);
        foreach (var track in song.Tracks)
        {
            bw.Write(track.ChannelIndex);
            WriteFixedString(bw, track.ChannelName, 32);
            bw.Write((int)track.Clef);
            bw.Write(track.OrderList.Count);
            foreach (var idx in track.OrderList)
                bw.Write(idx);
        }
        return ms.ToArray();
    }

    private static byte[] WritePatterns(Song song)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(song.Patterns.Count);
        foreach (var pattern in song.Patterns)
        {
            bw.Write(pattern.PatternId);
            bw.Write(pattern.TrackIndex);
            bw.Write(pattern.RowCount);
            foreach (var row in pattern.Rows)
            {
                bw.Write(row.Cells.Count);
                foreach (var cell in row.Cells)
                {
                    bw.Write(cell.Note);
                    bw.Write(cell.Octave);
                    bw.Write(cell.InstrumentId);
                    bw.Write(cell.Volume);
                    bw.Write(cell.Effects.Count);
                    foreach (var (k, v) in cell.Effects)
                    {
                        bw.Write(k);
                        bw.Write(v);
                    }
                }
            }
        }
        return ms.ToArray();
    }

    private static void WriteBlockName(BinaryWriter bw, string name)
    {
        var bytes = new byte[16];
        var encoded = Encoding.ASCII.GetBytes(name);
        Array.Copy(encoded, bytes, Math.Min(encoded.Length, 16));
        bw.Write(bytes);
    }

    private static void WriteFixedString(BinaryWriter bw, string value, int length)
    {
        var bytes = new byte[length];
        var encoded = Encoding.UTF8.GetBytes(value ?? "");
        Array.Copy(encoded, bytes, Math.Min(encoded.Length, length));
        bw.Write(bytes);
    }
}
