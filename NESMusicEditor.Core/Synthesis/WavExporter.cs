using System;
using System.IO;
using NESMusicEditor.Models;

namespace NESMusicEditor.Synthesis;

public class WavExporter
{
    private const int SampleRate = 44100;

    public void Export(Song song, string outputPath)
    {
        var engine = new SynthesisEngine();
        float[] pcm = engine.Synthesize(song);
        WriteWav(pcm, outputPath);
    }

    public static void WriteWav(float[] pcm, string outputPath)
    {
        using var fs = File.OpenWrite(outputPath);
        using var bw = new BinaryWriter(fs);

        int numSamples = pcm.Length;
        int byteRate = SampleRate * 2; // 16-bit mono
        int dataSize = numSamples * 2;

        // RIFF header
        bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(36 + dataSize);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        // fmt chunk
        bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        bw.Write(16);        // chunk size
        bw.Write((short)1); // PCM
        bw.Write((short)1); // mono
        bw.Write(SampleRate);
        bw.Write(byteRate);
        bw.Write((short)2); // block align
        bw.Write((short)16); // bits per sample
        // data chunk
        bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        bw.Write(dataSize);
        foreach (var s in pcm)
        {
            short val = (short)Math.Clamp((int)(s * 32767), -32768, 32767);
            bw.Write(val);
        }
    }
}
