using System;
using UnityEngine;

public static class WavUtility
{
    const int HeaderSize = 44;

    public static byte[] FromAudioClip(AudioClip clip)
    {
        var samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        byte[] wavData = ConvertAndWrite(samples, clip.channels, clip.frequency);
        WriteHeader(wavData, clip);

        return wavData;
    }

    private static byte[] ConvertAndWrite(float[] samples, int channels, int sampleRate)
    {
        int sampleCount = samples.Length;
        int byteCount = sampleCount * sizeof(short);

        byte[] bytes = new byte[HeaderSize + byteCount];

        int i = HeaderSize;
        foreach (float sample in samples)
        {
            // Clamp the sample to the range [-1.0f, 1.0f] and convert to a short value
            short intSample = (short)(Mathf.Clamp(sample, -1.0f, 1.0f) * short.MaxValue);
            bytes[i++] = (byte)(intSample & 0xFF);
            bytes[i++] = (byte)((intSample >> 8) & 0xFF);
        }

        return bytes;
    }

    private static void WriteHeader(byte[] bytes, AudioClip clip)
    {
        int channels = clip.channels;
        int sampleRate = clip.frequency;
        int byteCount = clip.samples * clip.channels * sizeof(short);

        // Chunk ID "RIFF"
        bytes[0] = (byte)'R';
        bytes[1] = (byte)'I';
        bytes[2] = (byte)'F';
        bytes[3] = (byte)'F';

        // Chunk size
        BitConverter.GetBytes(HeaderSize + byteCount - 8).CopyTo(bytes, 4);

        // Format "WAVE"
        bytes[8] = (byte)'W';
        bytes[9] = (byte)'A';
        bytes[10] = (byte)'V';
        bytes[11] = (byte)'E';

        // Sub chunk ID "fmt "
        bytes[12] = (byte)'f';
        bytes[13] = (byte)'m';
        bytes[14] = (byte)'t';
        bytes[15] = (byte)' ';

        // Sub chunk size (16 for PCM)
        BitConverter.GetBytes(16).CopyTo(bytes, 16);

        // Audio format (1 for PCM)
        BitConverter.GetBytes((ushort)1).CopyTo(bytes, 20);

        // Channels
        BitConverter.GetBytes((ushort)channels).CopyTo(bytes, 22);

        // Sample rate
        BitConverter.GetBytes(sampleRate).CopyTo(bytes, 24);

        // Byte rate
        BitConverter.GetBytes(sampleRate * channels * sizeof(short)).CopyTo(bytes, 28);

        // Block align
        BitConverter.GetBytes((ushort)(channels * sizeof(short))).CopyTo(bytes, 32);

        // Bits per sample
        BitConverter.GetBytes((ushort)(8 * sizeof(short))).CopyTo(bytes, 34);

        // Data chunk ID "data"
        bytes[36] = (byte)'d';
        bytes[37] = (byte)'a';
        bytes[38] = (byte)'t';
        bytes[39] = (byte)'a';

        // Data chunk size
        BitConverter.GetBytes(byteCount).CopyTo(bytes, 40);
    }
}
