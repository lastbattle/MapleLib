using System;
using System.IO;
using MapleLib.WzLib.WzProperties;
using NAudio.Wave;
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests;

public sealed class WzBinaryPropertyAudioTests
{
    [Fact]
    public void PcmWaveFilesRoundTripThroughWzSoundProperty()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"MapleLib-Wav-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string inputPath = Path.Combine(directory, "input.wav");
        string outputPath = Path.Combine(directory, "output.wav");
        WaveFormat format = new WaveFormat(44100, 16, 1);
        byte[] pcm = new byte[format.AverageBytesPerSecond / 10];

        try
        {
            using (FileStream stream = File.Create(inputPath))
            using (WaveFileWriter writer = new WaveFileWriter(stream, format))
            {
                writer.Write(pcm, 0, pcm.Length);
            }

            WzBinaryProperty property = new WzBinaryProperty("test", inputPath);

            Assert.Equal(WzBinaryPropertyType.WAV, property.SoundType);
            Assert.True(property.IsWaveFile);
            Assert.Equal(".wav", property.FileExtension);
            Assert.Equal(pcm, property.GetBytes(false));

            byte[] exported = property.GetBytesForWAVPlayback();
            AssertWaveFile(exported, format, pcm.Length);

            property.SaveToFile(outputPath);
            AssertWaveFile(File.ReadAllBytes(outputPath), format, pcm.Length);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void AssertWaveFile(byte[] bytes, WaveFormat expectedFormat, long expectedDataLength)
    {
        using MemoryStream stream = new MemoryStream(bytes);
        using WaveFileReader reader = new WaveFileReader(stream);

        Assert.Equal(expectedFormat.Encoding, reader.WaveFormat.Encoding);
        Assert.Equal(expectedFormat.SampleRate, reader.WaveFormat.SampleRate);
        Assert.Equal(expectedFormat.Channels, reader.WaveFormat.Channels);
        Assert.Equal(expectedFormat.BitsPerSample, reader.WaveFormat.BitsPerSample);
        Assert.Equal(expectedDataLength, reader.Length);
    }
}
