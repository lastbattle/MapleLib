using MapleLib.Helpers;
using MapleLib.WzLib.WzStructure.Data.CharacterStructure;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using MapleLib.WzLib.WzStructure.Data.QuestStructure;
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests;

public sealed class EnumStringOptimizationTests
{
    [Fact]
    public void IntegerEnumConversionsMatchDefinedValueContract()
    {
        int[] values = [-1_000, -1, 0, 1, 2, 3, 4, 30, 99, 1_000, 10_000, int.MaxValue];
        foreach (int value in values)
        {
            CharacterSubJobFlagType expectedSubJob = Enum.IsDefined(typeof(CharacterSubJobFlagType), value)
                ? (CharacterSubJobFlagType)value
                : CharacterSubJobFlagType.Any;
            QuestAreaCodeType expectedArea = Enum.IsDefined(typeof(QuestAreaCodeType), value)
                ? (QuestAreaCodeType)value
                : QuestAreaCodeType.Unknown;
            QuestMedalType expectedMedal = Enum.IsDefined(typeof(QuestMedalType), value)
                ? (QuestMedalType)value
                : QuestMedalType.NoneOrUnknown;

            Assert.Equal(expectedSubJob, CharacterSubJobFlagTypeExt.ToEnum(value));
            Assert.Equal(expectedArea, QuestAreaCodeTypeExt.ToEnum(value));
            Assert.Equal(expectedMedal, QuestMedalTypeExt.ToEnum(value));
        }
    }

    [Fact]
    public void InventoryLookupPreservesNoneFallbackAndDefinedValues()
    {
        foreach (InventoryType value in Enum.GetValues<InventoryType>())
            Assert.Equal(value, InventoryTypeExtensions.GetByType((byte)value));

        Assert.Equal(InventoryType.NONE, InventoryTypeExtensions.GetByType(99));
    }

    [Theory]
    [InlineData(CharacterJob.FirePoisonArchmage, true, "( Fire/ Poison) Archmage")]
    [InlineData(CharacterJob.Evan10, true, "Evan")]
    [InlineData(CharacterJob.Evan10, false, "Evan10")]
    [InlineData(CharacterJob.AranBeginner, true, "Aran (Beginner)")]
    [InlineData(CharacterJob.Pirate, true, "Pirate (Pirate)")]
    [InlineData(CharacterJob.CannonShooter, true, "Pirate (Cannon Shooter)")]
    public void CharacterJobFormattingPreservesCurrentOutput(CharacterJob job, bool removeProgression, string expected)
    {
        Assert.Equal(expected, job.GetFormattedJobName(removeProgression));
    }

    [Fact]
    public void CharacterJobFormattingMatchesFrozenImplementationForEveryValueAndUndefinedIntegers()
    {
        CharacterJob[] jobs = [.. Enum.GetValues<CharacterJob>(), (CharacterJob)(-12345), (CharacterJob)12345];
        foreach (CharacterJob job in jobs)
        foreach (bool removeProgression in new[] { false, true })
            Assert.Equal(FrozenCharacterJobFormat(job, removeProgression), job.GetFormattedJobName(removeProgression));
    }

    [Theory]
    [InlineData(CharacterJobPreBBType.ExplorerMagician, "Explorer Magician")]
    [InlineData(CharacterJobPreBBType.DawnWarrior, "Dawn Warrior")]
    public void PreBigBangJobFormattingPreservesCurrentOutput(CharacterJobPreBBType job, string expected)
    {
        Assert.Equal(expected, job.GetFormattedJobName());
    }

    [Fact]
    public void PreBigBangJobFormattingMatchesFrozenImplementationForEveryValueAndUndefinedIntegers()
    {
        CharacterJobPreBBType[] jobs = [.. Enum.GetValues<CharacterJobPreBBType>(), (CharacterJobPreBBType)(-1), (CharacterJobPreBBType)12345];
        foreach (CharacterJobPreBBType job in jobs)
            Assert.Equal(FrozenAddSpaces(job.ToString()), job.GetFormattedJobName());
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("Maple", "Maple")]
    [InlineData("maple", "Maple")]
    [InlineData("éclair", "Éclair")]
    [InlineData("1maple", "1maple")]
    public void CapitalizationPreservesCurrentOutput(string input, string expected)
    {
        Assert.Equal(expected, StringUtility.CapitalizeFirstCharacter(input));
    }

    [Fact]
    public void CapitalizationPreservesNullFailure()
    {
        Assert.Throws<NullReferenceException>(() => StringUtility.CapitalizeFirstCharacter(null!));
    }

    [Fact]
    public void CapitalizationReturnsOriginalInstanceWhenFirstCharacterIsUnchanged()
    {
        string input = new(['M', 'a', 'p', 'l', 'e']);
        Assert.Same(input, StringUtility.CapitalizeFirstCharacter(input));
    }

    private static string FrozenCharacterJobFormat(CharacterJob job, bool removeProgression)
    {
        string jobName = job.ToString()
            .Replace("FirePoison", " (Fire/Poison)")
            .Replace("IceLightning", " (Ice/Lightning)")
            .Replace("CrossBowman", "Crossbowman")
            .Replace("CannonShooter", "Cannon Shooter");
        if (jobName == "Pirate" || jobName.StartsWith("Cannon"))
            return $"Pirate ({jobName})";
        if (removeProgression)
            jobName = jobName.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
        jobName = FrozenAddSpaces(jobName);
        if (jobName.EndsWith(" Beginner"))
            jobName = jobName.Replace(" Beginner", "") + " (Beginner)";
        return jobName;
    }

    private static string FrozenAddSpaces(string value) =>
        string.Concat(value.Trim().Select(character => char.IsUpper(character) ? " " + character : character.ToString())).Trim();
}
