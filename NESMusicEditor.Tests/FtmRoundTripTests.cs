using System;
using System.IO;
using System.Linq;
using NESMusicEditor.IO;
using NESMusicEditor.Models;
using Xunit;

namespace NESMusicEditor.Tests;

public class FtmRoundTripTests
{
    [Fact]
    public void Empty_Song_RoundTrip()
    {
        var song = new Song
        {
            Title = "My Song",
            Author = "Test Author",
            Copyright = "2026",
            FramesPerSecond = 50,
            TicksPerFrame = 2,
        };

        var path = Path.GetTempFileName();
        try
        {
            FtmWriter.Write(song, path);
            var loaded = FtmReader.Read(path);

            Assert.Equal(song.Title, loaded.Title);
            Assert.Equal(song.Author, loaded.Author);
            Assert.Equal(song.Copyright, loaded.Copyright);
            Assert.Equal(song.FramesPerSecond, loaded.FramesPerSecond);
            Assert.Equal(song.TicksPerFrame, loaded.TicksPerFrame);
            Assert.Empty(loaded.Instruments);
            Assert.Empty(loaded.Tracks);
            Assert.Empty(loaded.Patterns);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Song_With_Notes_All_Channels()
    {
        var song = new Song { Title = "4Chan" };

        for (int ch = 0; ch < 4; ch++)
        {
            song.Tracks.Add(new Track { ChannelIndex = ch, ChannelName = $"CH{ch}", OrderList = { ch } });

            var pattern = new Pattern { PatternId = ch, RowCount = 2 };
            pattern.Rows.Add(new PatternRow { Cells = { new PatternCell { Note = ch * 10, Octave = 4, InstrumentId = 0, Volume = 10 } } });
            pattern.Rows.Add(new PatternRow { Cells = { new PatternCell { Note = ch * 10 + 1, Octave = 5, InstrumentId = 0, Volume = 8 } } });
            song.Patterns.Add(pattern);
        }

        var path = Path.GetTempFileName();
        try
        {
            FtmWriter.Write(song, path);
            var loaded = FtmReader.Read(path);

            Assert.Equal(4, loaded.Tracks.Count);
            Assert.Equal(4, loaded.Patterns.Count);

            for (int ch = 0; ch < 4; ch++)
            {
                var p = loaded.Patterns[ch];
                Assert.Equal(ch * 10, p.Rows[0].Cells[0].Note);
                Assert.Equal(4, p.Rows[0].Cells[0].Octave);
                Assert.Equal(ch * 10 + 1, p.Rows[1].Cells[0].Note);
                Assert.Equal(5, p.Rows[1].Cells[0].Octave);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Instrument_NonDefault_ADSR()
    {
        var song = new Song();
        song.Instruments.Add(new Instrument
        {
            Id = 7,
            Name = "Lead",
            AttackFrames = 5,
            DecayFrames = 20,
            SustainLevel = 8,
            ReleaseFrames = 15,
            DutyCycle = 1,
            VibratoSpeed = 3,
            VibratoDepth = 4,
            VibratoDelay = 2,
            PitchBendSpeed = -10,
        });

        var path = Path.GetTempFileName();
        try
        {
            FtmWriter.Write(song, path);
            var loaded = FtmReader.Read(path);

            Assert.Single(loaded.Instruments);
            var inst = loaded.Instruments[0];
            Assert.Equal(7, inst.Id);
            Assert.Equal("Lead", inst.Name);
            Assert.Equal(5, inst.AttackFrames);
            Assert.Equal(20, inst.DecayFrames);
            Assert.Equal((byte)8, inst.SustainLevel);
            Assert.Equal(15, inst.ReleaseFrames);
            Assert.Equal((byte)1, inst.DutyCycle);
            Assert.Equal((byte)3, inst.VibratoSpeed);
            Assert.Equal((byte)4, inst.VibratoDepth);
            Assert.Equal((byte)2, inst.VibratoDelay);
            Assert.Equal(-10, inst.PitchBendSpeed);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Unknown_Blocks_Preserved()
    {
        var song = new Song { Title = "BlockTest", Author = "Dev", FramesPerSecond = 60 };

        var path = Path.GetTempFileName();
        try
        {
            FtmWriter.Write(song, path);

            // Append a fake unknown block
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite))
            {
                // Patch block count from 4 to 5 (at offset 6, uint16)
                fs.Seek(6, SeekOrigin.Begin);
                fs.WriteByte(5);
                fs.WriteByte(0);

                fs.Seek(0, SeekOrigin.End);
                // Block name: "RESERVED" padded to 16 bytes
                var nameBytes = new byte[16];
                var n = System.Text.Encoding.ASCII.GetBytes("RESERVED");
                Array.Copy(n, nameBytes, n.Length);
                fs.Write(nameBytes, 0, 16);
                // Size: 4 bytes
                var extraData = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
                fs.Write(BitConverter.GetBytes((uint)extraData.Length), 0, 4);
                fs.Write(extraData, 0, extraData.Length);
            }

            var loaded = FtmReader.Read(path);
            Assert.Equal("BlockTest", loaded.Title);
            Assert.Equal("Dev", loaded.Author);
            Assert.Equal(60, loaded.FramesPerSecond);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Many_Sixteenth_Notes()
    {
        var song = new Song { Title = "Dense" };
        song.Instruments.Add(new Instrument { Id = 0, Name = "Inst0" });

        for (int ch = 0; ch < 4; ch++)
        {
            song.Tracks.Add(new Track { ChannelIndex = ch, ChannelName = $"CH{ch}", OrderList = { ch } });
            var pattern = new Pattern { PatternId = ch, RowCount = 64 };
            for (int r = 0; r < 64; r++)
            {
                int note = r % 36 + 48;
                pattern.Rows.Add(new PatternRow
                {
                    Cells = { new PatternCell { Note = note, Octave = 4, InstrumentId = 0, Volume = 12 } }
                });
            }
            song.Patterns.Add(pattern);
        }

        var path = Path.GetTempFileName();
        try
        {
            FtmWriter.Write(song, path);
            var loaded = FtmReader.Read(path);

            for (int ch = 0; ch < 4; ch++)
            {
                var p = loaded.Patterns[ch];
                Assert.Equal(64, p.Rows.Count);
                for (int r = 0; r < 64; r++)
                {
                    var cell = p.Rows[r].Cells[0];
                    Assert.Equal(r % 36 + 48, cell.Note);
                    Assert.Equal(12, cell.Volume);
                }
            }
        }
        finally
        {
            File.Delete(path);
        }
    }
}
