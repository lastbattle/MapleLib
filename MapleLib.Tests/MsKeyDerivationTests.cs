using MapleLib.WzLib.MSFile;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace MapleLib.Tests;

[TestClass]
public class MsKeyDerivationTests
{
    private delegate void SnowKey(WzMsEntry entry, Span<byte> key);
    private delegate void ChaChaKey(WzMsEntry entry, Span<byte> key, Span<byte> nonce, out uint counter);

    [TestMethod]
    [DataRow("", "5CC44404E431864718B96A358BFEFE19", "27EB714CA7A484FEB628CCD45328DAAD5165ACDE1D7E40D39D67018ABC363075", "000000009F00FC8285E6238E", 1416948222u)]
    [DataRow("aBcD", "509340C88431467718592A4399CA3019", "2BBC7580C7A444CEB6C88CA2411C14ADADC7ACBEA14EE0937D673925BC942CBA", "00000000271C4888D9E8798B", 1362028450u)]
    [DataRow("aBcD日本語12345", "A9E2D6F1049A5687E8899A31DC9598BE", "D2CDE3B9470F543E46183CD00443BC0AA07DBF8ECC9EF043FDB77716A1AFC56B", "000000007FF78A46759D18EC", 911184398u)]
    public void KeysMatchGoldenVectorsAfterHeaderReplacement(string salt, string snowHex, string chaChaHex, string nonceHex, uint expectedCounter)
    {
        using var file = new WzMsFile(new MemoryStream(), "test.ms", "test.ms", isSavingFile: true);
        var snow = typeof(WzMsFile).GetMethod("DeriveImgKey", BindingFlags.Instance | BindingFlags.NonPublic)!.CreateDelegate<SnowKey>(file);
        var chaCha = typeof(WzMsFile).GetMethod("DeriveChaCha20ImgKey", BindingFlags.Instance | BindingFlags.NonPublic)!.CreateDelegate<ChaChaKey>(file);
        var entry = CreateEntry();
        byte[] key = new byte[32], nonce = new byte[12];
        SetHeader(file, "previous salt");
        snow(entry, key.AsSpan(0, 16));
        chaCha(entry, key, nonce, out _);
        SetHeader(file, salt);
        snow(entry, key.AsSpan(0, 16));
        Assert.AreEqual(snowHex, Convert.ToHexString(key.AsSpan(0, 16)));
        chaCha(entry, key, nonce, out uint counter);
        Assert.AreEqual(chaChaHex, Convert.ToHexString(key));
        Assert.AreEqual(nonceHex, Convert.ToHexString(nonce));
        Assert.AreEqual(expectedCounter, counter);
    }

    private static void SetHeader(WzMsFile file, string salt) => typeof(WzMsFile).GetProperty(nameof(WzMsFile.Header))!
        .SetValue(file, new WzMsHeader("test.ms", salt, "test.ms" + salt, 0, 2, 24, 0, 0));

    private static WzMsEntry CreateEntry() => new("Mob/0100000.img", 0, 0, 0, 0, 0, 0, 0,
        Enumerable.Range(0, 16).Select(static i => (byte)(i * 31 + 17)).ToArray());
}
