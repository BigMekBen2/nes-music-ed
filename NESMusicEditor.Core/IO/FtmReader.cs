using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NESMusicEditor.Models;

namespace NESMusicEditor.IO;

public static class FtmReader
{
    private static readonly byte[] Magic = { 0x4E, 0x45, 0x53, 0x4D };

    public static Song Read(string path)
    {
        using var fs = File.OpenRead(path);
        using var br = new BinaryReader(fs, Encoding.UTF8, leaveOpen: false);

        var magic = br.ReadBytes(4);
        if (magic[0] != Magic[0] || magic[1] != Magic[1] || magic[2] != Magic[2] || magic[3] != Magic[3])
            throw new InvalidDataException("Not a valid FTM file.");

        var version = br.ReadUInt16();
        var blockCount = br.ReadUInt16();

        var song = new Song();

        for (int i = 0; i < blockCount; i++)
        {
            var nameBytes = br.ReadBytes(16);
            var name = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');
            var size = br.ReadUInt32();
            var data = br.ReadBytes((int)size);

            switch (name)
            {
                case "HEADER":
                    ReadHeader(song, data);
                    break;
                case "INSTRUMENTS":
                    ReadInstruments(song, data);
                    break;
                case "TRACKS":
                    ReadTracks(song, data);
                    break;
                case "PATTERNS":
                    ReadPatterns(song, data);
                    break;
                // unknown blocks: silently ignored
            }
        }

        return song;
    }

    private static void ReadHeader(Song song, byte[] data)
    {
        using var br = new BinaryReader(new MemoryStream(data));
        song.Title = ReadFixedString(br, 64);
        song.Author = ReadFixedString(br, 64);
        song.Copyright = ReadFixedString(br, 64);
        song.FramesPerSecond = br.ReadInt32();
        song.TicksPerFrame = br.ReadInt32();
    }

    private static void ReadInstruments(Song song, byte[] data)
    {
        using var br = new BinaryReader(new MemoryStream(data));
        int count = br.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            var inst = new Instrument
            {
                Id = br.ReadInt32(),
                Name = ReadFixedString(br, 32),
                AttackFrames = br.ReadInt32(),
                DecayFrames = br.ReadInt32(),
                SustainLevel = br.ReadByte(),
                ReleaseFrames = br.ReadInt32(),
                VibratoSpeed = br.ReadByte(),
                VibratoDepth = br.ReadByte(),
                VibratoDelay = br.ReadByte(),
                PitchBendSpeed = br.ReadInt32(),
                DutyCycle = br.ReadByte(),
            };
            song.Instruments.Add(inst);
        }
    }

    private static void ReadTracks(Song song, byte[] data)
    {
        using var br = new BinaryReader(new MemoryStream(data));
        int count = br.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            var track = new Track
            {
                ChannelIndex = br.ReadInt32(),
                ChannelName = ReadFixedString(br, 32),
                Clef = (ClefType)br.ReadInt32(),
            };
            int orderCount = br.ReadInt32();
            for (int j = 0; j < orderCount; j++)
                track.OrderList.Add(br.ReadInt32());
            song.Tracks.Add(track);
        }
    }

    private static void ReadPatterns(Song song, byte[] data)
    {
        using var br = new BinaryReader(new MemoryStream(data));
        int count = br.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            var pattern = new Pattern
            {
                PatternId = br.ReadInt32(),
                RowCount = br.ReadInt32(),
            };
            for (int r = 0; r < pattern.RowCount; r++)
            {
                var row = new PatternRow();
                int cellCount = br.ReadInt32();
                for (int c = 0; c < cellCount; c++)
                {
                    var cell = new PatternCell
                    {
                        Note = br.ReadInt32(),
                        Octave = br.ReadInt32(),
                        InstrumentId = br.ReadInt32(),
                        Volume = br.ReadInt32(),
                    };
                    int effectCount = br.ReadInt32();
                    for (int e = 0; e < effectCount; e++)
                    {
                        int k = br.ReadInt32();
                        int v = br.ReadInt32();
                        cell.Effects[k] = v;
                    }
                    row.Cells.Add(cell);
                }
                pattern.Rows.Add(row);
            }
            song.Patterns.Add(pattern);
        }
    }

    private static string ReadFixedString(BinaryReader br, int length)
    {
        var bytes = br.ReadBytes(length);
        return Encoding.UTF8.GetString(bytes).TrimEnd('\0');
    }
}
