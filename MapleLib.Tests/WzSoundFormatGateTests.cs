using System.Runtime.InteropServices;
using MapleLib.WzLib.WzProperties;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NAudio.Wave;

namespace UnitTest_WzFile
{
    /// <summary>
    /// Regression coverage for the WZ sound parse gate: the wave format
    /// published on <see cref="WzBinaryProperty.WavFormat"/> must be
    /// playback-plausible, because consumers pick decoder legs and derive
    /// playback positions from these fields.
    /// </summary>
    [TestClass]
    public class WzSoundFormatGateTests
    {
        private const int ProbeFrameLength = 417; // MPEG-1 layer 3, 128 kbit/s, 44.1 kHz, mono.

        private static void PutShort(byte[] blob, int offset, short value)
        {
            blob[offset] = (byte)value;
            blob[offset + 1] = (byte)(value >> 8);
        }

        private static void PutInt(byte[] blob, int offset, int value)
        {
            blob[offset] = (byte)value;
            blob[offset + 1] = (byte)(value >> 8);
            blob[offset + 2] = (byte)(value >> 16);
            blob[offset + 3] = (byte)(value >> 24);
        }

        // WAVEFORMATEX has a fixed ABI layout, so hostile values are plain
        // bytes at natural-aligned offsets regardless of struct packing.
        private static byte[] WaveFormatBlob(short formatTag, short channels, int sampleRate, int averageBytesPerSecond, short blockAlign, short bitsPerSample, int totalLength)
        {
            byte[] blob = new byte[totalLength];
            PutShort(blob, 0, formatTag);
            PutShort(blob, 2, channels);
            PutInt(blob, 4, sampleRate);
            PutInt(blob, 8, averageBytesPerSecond);
            PutShort(blob, 12, blockAlign);
            PutShort(blob, 14, bitsPerSample);
            return blob;
        }

        private static byte[] SoundHeaderFor(byte[] formatBlob)
        {
            return WzBinaryProperty.soundHeader
                .Concat(new byte[] { (byte)formatBlob.Length })
                .Concat(formatBlob)
                .ToArray();
        }

        private static byte[] MpegLayer3Blob()
        {
            int waveFormatSize = Marshal.SizeOf<WaveFormat>();
            int mpegFormatSize = Marshal.SizeOf<Mp3WaveFormat>();
            byte[] blob = WaveFormatBlob(
                (short)WaveFormatEncoding.MpegLayer3,
                2, 44100, 16000, 1, 0, mpegFormatSize);
            PutShort(blob, 16, (short)(mpegFormatSize - waveFormatSize));
            PutShort(blob, 18, 1);
            PutInt(blob, 20, 2);
            PutShort(blob, 24, (short)ProbeFrameLength);
            PutShort(blob, 26, 1);
            return blob;
        }

        [TestMethod]
        public void MpegLayer3Header_PublishesCraftedFields()
        {
            var probe = new WzBinaryProperty("mpeg_probe", 2 * 60 * 1000, SoundHeaderFor(MpegLayer3Blob()), new byte[ProbeFrameLength]);
            Assert.IsNotNull(probe.WavFormat);
            Assert.AreEqual(WaveFormatEncoding.MpegLayer3, probe.WavFormat?.Encoding);
            Assert.AreEqual(2, probe.WavFormat?.Channels);
            Assert.AreEqual(44100, probe.WavFormat?.SampleRate);
        }

        [TestMethod]
        public void ZeroAverage_GatesToNull()
        {
            byte[] blob = WaveFormatBlob((short)WaveFormatEncoding.Pcm, 2, 44100, 0, 4, 16, Marshal.SizeOf<WaveFormat>());
            var probe = new WzBinaryProperty("zero_average", 0, SoundHeaderFor(blob), new byte[] { 0x01 });
            Assert.IsNull(probe.WavFormat);
        }

        [TestMethod]
        public void ZeroChannels_GatesToNull()
        {
            byte[] blob = WaveFormatBlob((short)WaveFormatEncoding.Pcm, 0, 44100, 88200, 4, 16, Marshal.SizeOf<WaveFormat>());
            var probe = new WzBinaryProperty("zero_channels", 0, SoundHeaderFor(blob), new byte[] { 0x01 });
            Assert.IsNull(probe.WavFormat);
        }

        [TestMethod]
        public void SanePcm_PassesThrough()
        {
            byte[] blob = WaveFormatBlob((short)WaveFormatEncoding.Pcm, 2, 44100, 176400, 4, 16, Marshal.SizeOf<WaveFormat>());
            var probe = new WzBinaryProperty("pcm_probe", 0, SoundHeaderFor(blob), Array.Empty<byte>());
            Assert.IsNotNull(probe.WavFormat);
            Assert.AreEqual(WaveFormatEncoding.Pcm, probe.WavFormat?.Encoding);
            Assert.AreEqual(4, probe.WavFormat?.BlockAlign);
        }
    }
}
