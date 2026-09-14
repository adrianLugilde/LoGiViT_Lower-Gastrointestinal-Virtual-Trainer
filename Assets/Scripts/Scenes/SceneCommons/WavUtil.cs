using System;
using System.IO;
using UnityEngine;

public static class WavUtil
{
    public static byte[] FromAudioClip(AudioClip clip, int targetHz = 16000, bool mono = true)
    {
        // Pull samples
        float[] samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        // Downmix to mono if needed
        float[] monoSamples = mono && clip.channels > 1
            ? DownmixToMono(samples, clip.channels)
            : samples;

        // Resample if needed
        float[] resampled = (clip.frequency == targetHz)
            ? monoSamples
            : ResampleLinear(monoSamples, clip.frequency, targetHz);

        // Convert to 16-bit PCM
        byte[] pcm16 = FloatToPcm16(resampled);

        // Wrap with WAV header
        return WriteWavHeader(pcm16, targetHz, mono ? 1 : clip.channels, bitsPerSample: 16);
    }

    static float[] DownmixToMono(float[] interleaved, int channels)
    {
        int frames = interleaved.Length / channels;
        float[] mono = new float[frames];
        for (int f = 0; f < frames; f++)
        {
            float sum = 0f;
            int baseIdx = f * channels;
            for (int c = 0; c < channels; c++) sum += interleaved[baseIdx + c];
            mono[f] = sum / channels;
        }
        return mono;
    }

    static float[] ResampleLinear(float[] src, int srcHz, int dstHz)
    {
        double ratio = (double)dstHz / srcHz;
        int dstLen = (int)Math.Floor(src.Length * ratio);
        float[] dst = new float[dstLen];
        for (int i = 0; i < dstLen; i++)
        {
            double srcPos = i / ratio;
            int i0 = (int)Math.Floor(srcPos);
            int i1 = Math.Min(i0 + 1, src.Length - 1);
            double t = srcPos - i0;
            dst[i] = (float)((1.0 - t) * src[i0] + t * src[i1]);
        }
        return dst;
    }

    static byte[] FloatToPcm16(float[] samples)
    {
        byte[] bytes = new byte[samples.Length * 2];
        const float clamp = 0.999f;
        for (int i = 0; i < samples.Length; i++)
        {
            float f = Mathf.Clamp(samples[i], -clamp, clamp);
            short s = (short)Mathf.RoundToInt(f * 32767f);
            bytes[i * 2 + 0] = (byte)(s & 0xFF);
            bytes[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
        }
        return bytes;
    }

    static byte[] WriteWavHeader(byte[] pcm, int sampleRate, int channels, short bitsPerSample)
    {
        using var ms = new MemoryStream(44 + pcm.Length);
        using var bw = new BinaryWriter(ms);

        int byteRate = sampleRate * channels * bitsPerSample / 8;
        short blockAlign = (short)(channels * bitsPerSample / 8);

        // RIFF header
        bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(36 + pcm.Length);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

        // fmt chunk
        bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        bw.Write(16); // PCM chunk size
        bw.Write((short)1); // PCM format
        bw.Write((short)channels);
        bw.Write(sampleRate);
        bw.Write(byteRate);
        bw.Write(blockAlign);
        bw.Write(bitsPerSample);

        // data chunk
        bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        bw.Write(pcm.Length);
        bw.Write(pcm);

        return ms.ToArray();
    }
}
