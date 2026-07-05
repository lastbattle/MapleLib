using System.Security.Cryptography;
using MapleLib.MapleCryptoLib;
using MapleCryptoEngine = MapleLib.MapleCryptoLib.MapleCrypto;

namespace MapleCrypto.Benchmarks;

internal static class CryptoCorrectness
{
    private const string ExpectedCustom512Sha256 = "004c23d22c2059394fe01b70a2eedee8016ba69e0d28a79e78f04872dc7454fe";
    private const string ExpectedAes512Sha256 = "8212e6e6942792d182eec391d000c7a1a36fb546625ef9441be31133c19b0df0";
    private const string ExpectedNewIv = "9374b162";
    private static readonly int[] PacketSizes = [32, 128, 512, 1460, 8192];
    private static readonly byte[] Iv = [0x4D, 0x23, 0xC7, 0x2B];

    public static void Verify()
    {
        foreach (int size in PacketSizes)
        {
            byte[] source = CreatePayload(size);

            byte[] custom = (byte[])source.Clone();
            MapleCustomEncryption.Encrypt(custom);
            MapleCustomEncryption.Decrypt(custom);
            EnsureEqual(source, custom, $"custom round trip ({size} bytes)");

            byte[] aes = (byte[])source.Clone();
            MapleAESEncryption.AesCrypt(Iv, aes, aes.Length);
            MapleAESEncryption.AesCrypt(Iv, aes, aes.Length);
            EnsureEqual(source, aes, $"AES round trip ({size} bytes)");
        }

        byte[] multiplied = MapleCryptoEngine.MultiplyBytes([1, 2, 3, 4], 4, 4);
        EnsureEqual([1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4], multiplied,
            "MultiplyBytes output");

        byte[] multipliedSimd = MapleCryptoEngine.MultiplyBytes_SIMD([1, 2, 3, 4], 4, 4);
        EnsureEqual(multiplied, multipliedSimd, "MultiplyBytes_SIMD output");

        byte[] source512 = CreatePayload(512);
        byte[] custom512 = (byte[])source512.Clone();
        byte[] aes512 = (byte[])source512.Clone();
        MapleCustomEncryption.Encrypt(custom512);
        MapleAESEncryption.AesCrypt(Iv, aes512, aes512.Length);
        EnsureEqual(ExpectedCustom512Sha256, Convert.ToHexStringLower(SHA256.HashData(custom512)),
            "custom encryption digest");
        EnsureEqual(ExpectedAes512Sha256, Convert.ToHexStringLower(SHA256.HashData(aes512)),
            "AES encryption digest");
        EnsureEqual(ExpectedNewIv, Convert.ToHexStringLower(MapleCryptoEngine.GetNewIV(Iv)),
            "IV shuffle output");

        VerifyMutableUserKey();
    }

    public static void PrintDigests()
    {
        byte[] source = CreatePayload(512);
        byte[] custom = (byte[])source.Clone();
        byte[] aes = (byte[])source.Clone();
        MapleCustomEncryption.Encrypt(custom);
        MapleAESEncryption.AesCrypt(Iv, aes, aes.Length);

        Console.WriteLine($"custom-512-sha256={Convert.ToHexStringLower(SHA256.HashData(custom))}");
        Console.WriteLine($"aes-512-sha256={Convert.ToHexStringLower(SHA256.HashData(aes))}");
        Console.WriteLine($"new-iv={Convert.ToHexStringLower(MapleCryptoEngine.GetNewIV(Iv))}");
    }

    public static byte[] CreatePayload(int size)
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

    private static void EnsureEqual(byte[] expected, byte[] actual, string operation)
    {
        if (!expected.AsSpan().SequenceEqual(actual))
        {
            throw new InvalidOperationException($"Correctness check failed: {operation}.");
        }
    }

    private static void VerifyMutableUserKey()
    {
        byte original = MapleCryptoConstants.UserKey_WzLib[0];
        try
        {
            MapleCryptoConstants.UserKey_WzLib[0] ^= 0x5A;
            byte[] source = CreatePayload(128);
            byte[] defaultKeyResult = (byte[])source.Clone();
            byte[] explicitKeyResult = (byte[])source.Clone();
            byte[] trimmed = MapleCryptoConstants.GetTrimmedUserKey(ref MapleCryptoConstants.UserKey_WzLib);
            MapleAESEncryption.AesCrypt(Iv, defaultKeyResult, defaultKeyResult.Length);
            MapleAESEncryption.AesCrypt(Iv, explicitKeyResult, explicitKeyResult.Length, trimmed);
            EnsureEqual(explicitKeyResult, defaultKeyResult, "mutable AES user key cache");
        }
        finally
        {
            MapleCryptoConstants.UserKey_WzLib[0] = original;
        }
    }

    private static void EnsureEqual(string expected, string actual, string operation)
    {
        if (!StringComparer.Ordinal.Equals(expected, actual))
        {
            throw new InvalidOperationException(
                $"Correctness check failed: {operation}; expected {expected}, got {actual}.");
        }
    }
}
