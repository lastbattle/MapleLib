using MapleLib.WzLib;
using MapleLib.WzLib.Serializer;
using MapleLib.WzLib.Util;
using System.IO;
using System.Xml;
using Xunit;
using Assert = Xunit.Assert;

// Cross-cutting parser contracts, pinned by seeded mutation fuzz over real
// content: hostile archives and dumps must reach callers as the documented
// exception families instead of raw null-reference, invalid-cast or index
// failures. The documented families are enforced here as permanent
// regression gates; see docs/safety-contracts.md.

public sealed class WzContainerMutationContractTests
{
    private const int SeedCount = 150;
    private const int MutationSeedBase = 20261109;

    [Fact]
    public void MutatedContainerParseStaysInDocumentedExceptionFamily()
    {
        string source = Path.Combine(AppContext.BaseDirectory, "WzFiles", "Common", "TamingMob_GMS_75.wz");
        Assert.True(File.Exists(source), $"Missing test asset: {source}");

        // The baseline must parse so mutations exercise real structure.
        ParseContainerTree(source);

        byte[] baseline = File.ReadAllBytes(source);
        string workDir = Path.Combine(Path.GetTempPath(), "wz-container-mutation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        try
        {
            string mutatedPath = Path.Combine(workDir, "mutated.wz");
            List<string> violations = new List<string>();

            for (int seed = 0; seed < SeedCount; seed++)
            {
                byte[] mutated = (byte[])baseline.Clone();
                Random rng = new Random(MutationSeedBase + seed);
                int mutationCount = 1 + rng.Next(6);
                for (int mutation = 0; mutation < mutationCount; mutation++)
                    mutated[rng.Next(mutated.Length)] = (byte)rng.Next(256);

                File.WriteAllBytes(mutatedPath, mutated);
                try
                {
                    ParseContainerTree(mutatedPath);
                }
                catch (Exception ex)
                {
                    if (!IsDocumentedParseFamily(ex))
                        violations.Add($"seed {seed}: {ex.GetType().Name}: {ex.Message}");
                }
            }

            Assert.True(violations.Count == 0,
                "Undocumented exception(s) escaped the container parse path:"
                + Environment.NewLine + string.Join(Environment.NewLine, violations));
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    private static void ParseContainerTree(string path)
    {
        using WzFile file = new WzFile(path, WzMapleVersion.GMS);
        file.ParseWzFile();
        ParseImages(file.WzDirectory);
    }

    private static void ParseImages(WzDirectory directory)
    {
        foreach (WzImage image in directory.WzImages)
            image.ParseImage();

        foreach (WzDirectory child in directory.WzDirectories)
            ParseImages(child);
    }

    private static bool IsDocumentedParseFamily(Exception ex)
    {
        return ex is InvalidDataException
            or EndOfStreamException
            or IOException
            or NotImplementedException
            or ArgumentException
            or OverflowException;
    }

    internal static void TryDelete(string path)
    {
        try { Directory.Delete(path, true); } catch (IOException) { }
    }
}

public sealed class WzXmlMutationContractTests
{
    private const int SeedCount = 120;
    private const int MutationSeedBase = 20261109;

    // Exercises the element dispatch, attribute reads and the invariant
    // numeric parse path of the importer.
    private const string Corpus =
        "<imgdir name=\"mut.img\">" +
        "<int name=\"a\" value=\"7\"/>" +
        "<short name=\"sh\" value=\"3\"/>" +
        "<long name=\"lo\" value=\"123456789\"/>" +
        "<string name=\"s\" value=\"hello\"/>" +
        "<uol name=\"u\" value=\"a/b\"/>" +
        "<null name=\"n\"/>" +
        "</imgdir>";

    [Fact]
    public void MutatedXmlImportStaysInDocumentedExceptionFamily()
    {
        string workDir = Path.Combine(Path.GetTempPath(), "wz-xml-mutation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        try
        {
            string path = Path.Combine(workDir, "mutated.xml");
            byte[] iv = WzTool.GetIvByMapleVersion(WzMapleVersion.BMS);

            File.WriteAllText(path, Corpus);
            Assert.Single(new WzXmlDeserializer(false, iv).ParseXML(path));

            char[] pool = { '<', '>', '"', '=', '&', ';', 'x', '0', ' ', '.' };
            List<string> violations = new List<string>();

            for (int seed = 0; seed < SeedCount; seed++)
            {
                char[] text = Corpus.ToCharArray();
                Random rng = new Random(MutationSeedBase + 1000 + seed);
                int mutationCount = 1 + rng.Next(4);
                for (int mutation = 0; mutation < mutationCount; mutation++)
                    text[rng.Next(text.Length)] = pool[rng.Next(pool.Length)];

                File.WriteAllText(path, new string(text));
                try
                {
                    WzXmlDeserializer deserializer = new WzXmlDeserializer(false, iv);
                    deserializer.ParseXML(path);
                }
                catch (Exception ex)
                {
                    if (!IsDocumentedXmlFamily(ex))
                        violations.Add($"seed {seed}: {ex.GetType().Name}: {ex.Message}");
                }
            }

            Assert.True(violations.Count == 0,
                "Undocumented exception(s) escaped the XML import path:"
                + Environment.NewLine + string.Join(Environment.NewLine, violations));
        }
        finally
        {
            WzContainerMutationContractTests.TryDelete(workDir);
        }
    }

    private static bool IsDocumentedXmlFamily(Exception ex)
    {
        return ex is InvalidDataException
            or XmlException
            or NoBase64DataException
            or EndOfStreamException
            or IOException
            or ArgumentException;
    }
}
