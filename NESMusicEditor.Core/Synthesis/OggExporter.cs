using System;
using System.IO;
using NESMusicEditor.Models;
using OggVorbisEncoder;

namespace NESMusicEditor.Synthesis;

public class OggExporter
{
    public void Export(Song song, string outputPath)
    {
        var engine = new SynthesisEngine();
        float[] pcm = engine.Synthesize(song);

        using var stream = File.OpenWrite(outputPath);

        var info = VorbisInfo.InitVariableBitRate(1, 44100, 0.5f);
        var comments = new Comments();
        comments.AddTag("TITLE", song.Title ?? "NES Export");
        comments.AddTag("ARTIST", song.Author ?? "");

        var infoPacket = HeaderPacketBuilder.BuildInfoPacket(info);
        var commentsPacket = HeaderPacketBuilder.BuildCommentsPacket(comments);
        var booksPacket = HeaderPacketBuilder.BuildBooksPacket(info);

        var oggOut = new OggStream(1);
        oggOut.PacketIn(infoPacket);
        oggOut.PacketIn(commentsPacket);
        oggOut.PacketIn(booksPacket);

        OggPage page;
        while (oggOut.PageOut(out page, true))
        {
            stream.Write(page.Header);
            stream.Write(page.Body);
        }

        var state = ProcessingState.Create(info);
        const int chunkSize = 1024;
        float[][] buffer = new float[1][];
        for (int i = 0; i < pcm.Length; i += chunkSize)
        {
            int count = Math.Min(chunkSize, pcm.Length - i);
            buffer[0] = pcm[i..(i + count)];
            state.WriteData(buffer, count);
            OggPacket packet;
            while (state.PacketOut(out packet))
            {
                oggOut.PacketIn(packet);
                while (oggOut.PageOut(out page, false))
                {
                    stream.Write(page.Header);
                    stream.Write(page.Body);
                }
            }
        }

        state.WriteEndOfStream();
        OggPacket eosPacket;
        while (state.PacketOut(out eosPacket))
        {
            oggOut.PacketIn(eosPacket);
            while (oggOut.PageOut(out page, false))
            {
                stream.Write(page.Header);
                stream.Write(page.Body);
            }
        }
        while (oggOut.PageOut(out page, true))
        {
            stream.Write(page.Header);
            stream.Write(page.Body);
        }
    }
}
