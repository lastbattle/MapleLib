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
        MapleAESEncryption.AesCrypt(Iv, actual, 17);

        Assert.Equal(source, actual);
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
