using MapleLib.WzLib.Util;
using System.Security.Cryptography;
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests;

public sealed class WzMutableKeyTests
{
    [Fact]
    public void AesChain_MatchesIndependentVectorsAcrossBatchGrowth()
    {
        // Independently generated with Python cryptography/OpenSSL AES-256-ECB:
        // AES key 00..1F, first plaintext 00010203 repeated four times,
        // then each ciphertext is the next block's plaintext.
        var key = new WzMutableKey([0, 1, 2, 3], Enumerable.Range(0, 32).Select(i => (byte)i).ToArray());
        key.EnsureKeySize(0);
        Assert.Empty(key.GetKeys());
        key.EnsureKeySize(1);
        byte[] firstBatch = key.GetKeys();
        Assert.Equal(4096, firstBatch.Length);
        Assert.Equal("F90863F53D470511C090E8C5A15E2C40338B9A524BD8F11145964E9BFFC6B8C9",
            Convert.ToHexString(firstBatch.AsSpan(0, 32)));
        Assert.Equal("40EC3A6E9539A245E32F1B1CBADD15817BA6F303E42EE98ABC4F1E5133FACD11",
            Convert.ToHexString(SHA256.HashData(firstBatch)));
        firstBatch[0] ^= 1;
        Assert.Equal((byte)0xF9, key[0]); // GetKeys returns caller-owned bytes.
        key.EnsureKeySize(4096);
        Assert.Equal(4096, key.GetKeys().Length);
        key.EnsureKeySize(4097);
        Assert.Equal("48925D5DDDBC55095A46B934CF8D47BBD60D89078E8E6BF2B1FFAD6365CB7EE5",
            Convert.ToHexString(SHA256.HashData(key.GetKeys())));
        key.EnsureKeySize(65536);
        Assert.Equal("388C725155DF797C6C9B21EA03D006C747B7D458CCC8BE661678ACC41BBF1B63",
            Convert.ToHexString(SHA256.HashData(key.GetKeys())));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 4096)]
    [InlineData(4097, 8192)]
    public void ZeroIv_ProducesRoundedZeroKeyWithoutRequiringAesKey(int requested, int expectedLength)
    {
        var key = new WzMutableKey(new byte[4], []);
        key.EnsureKeySize(requested);
        byte[] actual = key.GetKeys();
        Assert.Equal(expectedLength, actual.Length);
        Assert.All(actual, value => Assert.Equal((byte)0, value));
    }
}
