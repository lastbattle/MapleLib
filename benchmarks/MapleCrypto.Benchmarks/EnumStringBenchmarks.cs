using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using MapleLib.Helpers;
using MapleLib.WzLib.WzStructure.Data.CharacterStructure;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using MapleLib.WzLib.WzStructure.Data.QuestStructure;

namespace MapleCrypto.Benchmarks;

[MemoryDiagnoser]
[MinColumn, MaxColumn]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class EnumConversionBenchmarks
{
    private const int Operations = 1_024;

    [Params(false, true)] public bool ValidValues { get; set; }

    private int[] _subJobValues = null!;
    private int[] _areaValues = null!;
    private int[] _medalValues = null!;
    private byte[] _inventoryValues = null!;

    [GlobalSetup]
    public void Setup()
    {
        _subJobValues = ValidValues ? [0, 1, 2, 4] : [-1, 3, 99, int.MaxValue];
        _areaValues = ValidValues ? [0, 1, 10, 30] : [-1, 999, 20_000, int.MaxValue];
        _medalValues = ValidValues ? [0, 1, 2, 3] : [-1, 999, 20_000, int.MaxValue];
        _inventoryValues = ValidValues ? [0, 1, 4, 255] : [6, 99, 128, 254];
    }

    [Benchmark(OperationsPerInvoke = Operations)]
    public int CharacterSubJobFromInt()
    {
        int result = 0;
        for (int index = 0; index < Operations; index++)
            result += (int)CharacterSubJobFlagTypeExt.ToEnum(_subJobValues[index & 3]);
        return result;
    }

    [Benchmark(OperationsPerInvoke = Operations)]
    public int QuestAreaFromInt()
    {
        int result = 0;
        for (int index = 0; index < Operations; index++)
            result += (int)QuestAreaCodeTypeExt.ToEnum(_areaValues[index & 3]);
        return result;
    }

    [Benchmark(OperationsPerInvoke = Operations)]
    public int QuestMedalFromInt()
    {
        int result = 0;
        for (int index = 0; index < Operations; index++)
            result += (int)QuestMedalTypeExt.ToEnum(_medalValues[index & 3]);
        return result;
    }

    [Benchmark(OperationsPerInvoke = Operations)]
    public int InventoryFromByte()
    {
        int result = 0;
        for (int index = 0; index < Operations; index++)
            result += (int)InventoryTypeExtensions.GetByType(_inventoryValues[index & 3]).GetValueOrDefault();
        return result;
    }
}

[MemoryDiagnoser]
[MinColumn, MaxColumn]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class StringHelperBenchmarks
{
    private CharacterJob _job = CharacterJob.FirePoisonArchmage;
    private CharacterJobPreBBType _preBigBangJob = CharacterJobPreBBType.ExplorerMagician;
    private string _lowercase = "mapleStory";

    [Benchmark] public string CharacterJobFormatting() => _job.GetFormattedJobName();
    [Benchmark] public string PreBigBangJobFormatting() => _preBigBangJob.GetFormattedJobName();
    [Benchmark] public string CapitalizeFirstCharacter() => StringUtility.CapitalizeFirstCharacter(_lowercase);
}
