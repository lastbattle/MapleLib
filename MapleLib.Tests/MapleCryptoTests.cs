using System.Security.Cryptography;
using MapleLib.MapleCryptoLib;
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests;

public class MapleCryptoTests
{
    private static readonly byte[] Iv = [0x4D, 0x23, 0xC7, 0x2B];

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(128)]
    [InlineData(512)]
    [InlineData(1460)]
    [InlineData(8192)]
    public void CustomEncryption_RoundTrips(int size)
    {
        byte[] expected = CreatePayload(size);
        byte[] actual = (byte[])expected.Clone();

        MapleCustomEncryption.Encrypt(actual);
        MapleCustomEncryption.Decrypt(actual);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CustomEncryption_RejectsNullInput()
    {
        Assert.Throws<ArgumentNullException>(() => MapleCustomEncryption.Encrypt(null!));
        Assert.Throws<ArgumentNullException>(() => MapleCustomEncryption.Decrypt(null!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(128)]
    [InlineData(512)]
    [InlineData(1460)]
    [InlineData(8192)]
    public void AesEncryption_RoundTrips(int size)
    {
        byte[] expected = CreatePayload(size);
        byte[] actual = (byte[])expected.Clone();

        MapleAESEncryption.AesCrypt(Iv, actual, actual.Length);
        MapleAESEncryption.AesCrypt(Iv, actual, actual.Length);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Encryption_Known512ByteVectorsRemainStable()
    {
        byte[] source = CreatePayload(512);
        byte[] custom = (byte[])source.Clone();
        byte[] aes = (byte[])source.Clone();

        MapleCustomEncryption.Encrypt(custom);
        MapleAESEncryption.AesCrypt(Iv, aes, aes.Length);

        Assert.Equal("004c23d22c2059394fe01b70a2eedee8016ba69e0d28a79e78f04872dc7454fe",
            Convert.ToHexStringLower(SHA256.HashData(custom)));
        Assert.Equal("8212e6e6942792d182eec391d000c7a1a36fb546625ef9441be31133c19b0df0",
            Convert.ToHexStringLower(SHA256.HashData(aes)));
    }

    [Fact]
    public void AesEncryption_OnlyChangesRequestedPrefix()
    {
        byte[] source = CreatePayload(64);
        byte[] actual = (byte[])source.Clone();

        MapleAESEncryption.AesCrypt(Iv, actual, 17);
        Assert.Equal(source.AsSpan(17).ToArray(), actual.AsSpan(17).ToArray());
        MapleAESEncryption.AesCrypt(Iv, actual, 17);

        Assert.Equal(source, actual);
    }

    [Fact]
    public void AesEncryption_RejectsInvalidArgumentsBeforeMutatingData()
    {
        byte[] original = CreatePayload(32);
        byte[] data = (byte[])original.Clone();

        Assert.Throws<ArgumentOutOfRangeException>(() => MapleAESEncryption.AesCrypt(Iv, data, -1));
        Assert.Equal(original, data);

        Assert.Throws<ArgumentOutOfRangeException>(() => MapleAESEncryption.AesCrypt(Iv, data, data.Length + 1));
        Assert.Equal(original, data);

        Assert.Throws<ArgumentException>(() => MapleAESEncryption.AesCrypt(new byte[3], data, data.Length));
        Assert.Equal(original, data);

        Assert.Throws<ArgumentException>(() => MapleAESEncryption.AesCrypt(Iv, data, data.Length, new byte[31]));
        Assert.Equal(original, data);
    }

    [Fact]
    public void MapleCrypto_ClonesIvOnConstructionAssignmentAndRead()
    {
        byte[] source = (byte[])Iv.Clone();
        var crypto = new MapleCrypto(source, 95);

        source[0] ^= 0xFF;
        Assert.Equal(Iv, crypto.IV);

        byte[] read = crypto.IV;
        read[1] ^= 0xFF;
        Assert.Equal(Iv, crypto.IV);

        byte[] replacement = [1, 2, 3, 4];
        crypto.IV = replacement;
        replacement[0] = 0xFF;
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, crypto.IV);
    }

    [Fact]
    public void MapleCrypto_RejectsInvalidHeadersAndIvInputs()
    {
        Assert.Throws<ArgumentNullException>(() => new MapleCrypto(null!, 95));
        Assert.Throws<ArgumentException>(() => new MapleCrypto([1, 2, 3], 95));
        Assert.Throws<ArgumentNullException>(() => MapleCrypto.GetNewIV(null!));
        Assert.Throws<ArgumentException>(() => MapleCrypto.GetNewIV([1, 2, 3]));
        Assert.Throws<ArgumentNullException>(() => MapleCrypto.Shuffle(0, null!));
        Assert.Throws<ArgumentException>(() => MapleCrypto.Shuffle(0, [1, 2, 3]));

        var crypto = new MapleCrypto((byte[])Iv.Clone(), 95);
        Assert.Throws<ArgumentOutOfRangeException>(() => crypto.GetHeaderToClient(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => crypto.GetHeaderToClient(ushort.MaxValue + 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => crypto.GetHeaderToServer(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => crypto.GetHeaderToServer(ushort.MaxValue + 1));
        Assert.Equal(-1, MapleCrypto.GetPacketLength((byte[])null!));
        Assert.Equal(-1, MapleCrypto.GetPacketLength([1, 2, 3]));
        Assert.False(crypto.CheckPacketToServer(null!));
        Assert.False(crypto.CheckPacketToServer([1]));
    }

    [Fact]
    public void IvShuffleAndPacketHeadersRemainStable()
    {
        Assert.Equal("9374b162", Convert.ToHexStringLower(MapleCrypto.GetNewIV(Iv)));

        var crypto = new MapleCrypto((byte[])Iv.Clone(), 95);
        byte[] clientHeader = crypto.GetHeaderToClient(1460);
        byte[] serverHeader = crypto.GetHeaderToServer(1460);

        Assert.Equal(1460, MapleCrypto.GetPacketLength(clientHeader));
        Assert.Equal(1460, MapleCrypto.GetPacketLength(serverHeader));
        Assert.Equal(MapleCrypto.GetPacketLength(BitConverter.ToInt32(clientHeader)),
            MapleCrypto.GetPacketLength(clientHeader));
    }

    [Fact]
    public void MultiplyBytes_RepeatsWholeInputForVectorSizedPatterns()
    {
        byte[] input = Enumerable.Range(0, 32).Select(static value => (byte)value).ToArray();
        byte[] expected = input.Concat(input).Concat(input).ToArray();

        Assert.Equal(expected, MapleCrypto.MultiplyBytes(input, input.Length, 3));
        Assert.Equal(expected, MapleCrypto.MultiplyBytes_SIMD(input, input.Length, 3));
    }

    [Fact]
    public void MultiplyBytes_RejectsInvalidCountsAndOverflow()
    {
        byte[] input = [1, 2, 3];

        Assert.Throws<ArgumentNullException>(() => MapleCrypto.MultiplyBytes(null!, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => MapleCrypto.MultiplyBytes(input, -1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => MapleCrypto.MultiplyBytes(input, input.Length + 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => MapleCrypto.MultiplyBytes(input, 1, -1));
        byte[] largeInput = new byte[1_500_000];
        Assert.Throws<ArgumentOutOfRangeException>(() => MapleCrypto.MultiplyBytes(largeInput, largeInput.Length, 2_000));
        Assert.Throws<ArgumentOutOfRangeException>(() => MapleCrypto.MultiplyBytes_SIMD(input, -1, 1));

        Assert.Empty(MapleCrypto.MultiplyBytes(input, 0, int.MaxValue));
    }

    [Fact]
    public void TrimmedUserKey_UsesEverySixteenthByte()
    {
        byte[] source = Enumerable.Range(0, 128).Select(static value => (byte)value).ToArray();
        byte[] trimmed = MapleCryptoConstants.GetTrimmedUserKey(ref source);

        Assert.Equal(32, trimmed.Length);
        for (int i = 0; i < trimmed.Length; i++)
        {
            Assert.Equal(i % 4 == 0 ? source[i * 4] : 0, trimmed[i]);
        }
    }

    [Fact]
    public void TrimmedUserKey_RejectsShortInputBeforeIndexing()
    {
        byte[] shortKey = new byte[112];

        Assert.Throws<ArgumentException>(() => MapleCryptoConstants.GetTrimmedUserKey(ref shortKey));
    }

    private static byte[] CreatePayload(int size)
    {
        byte[] payload = new byte[size];
        uint state = 0x1234ABCDu + (uint)size;
        for (int i = 0; i < payload.Length; i++)
        {
            state = state * 1_664_525u + 1_013_904_223u;
            payload[i] = (byte)(state >> 24);
        }
        return payload;
    }
}
