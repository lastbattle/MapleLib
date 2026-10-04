using System.Linq;
using MapleLib.WzLib.WzStructure.Data.CharacterStructure;
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests;

public sealed class MapleJobTypeExtensionsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(32800)]
    [InlineData(int.MinValue)]
    public void MatchingDecodesKnownWireMasks(int jobBitfield)
    {
        CharacterClassType[] expected = jobBitfield switch
        {
            0 => [],
            32800 => [CharacterClassType.Mercedes, CharacterClassType.Zero],
            int.MinValue => [CharacterClassType.UltimateAdventurer],
            _ => throw new ArgumentOutOfRangeException(nameof(jobBitfield))
        };
        Assert.Equal(expected, MapleJobTypeExtensions.GetMatchingJobs(jobBitfield));
    }

    [Fact]
    public void EnumerationExcludesNullAndPlacesNegativeJobAfterNonnegativeJobs()
    {
        CharacterClassType[] jobs = MapleJobTypeExtensions.GetAllJobTypes().ToArray();
        Assert.Equal(CharacterClassType.Resistance, jobs[0]);
        Assert.Equal(CharacterClassType.UltimateAdventurer, jobs[^1]);
        Assert.DoesNotContain(CharacterClassType.NULL, jobs);
        Assert.Equal(jobs.Length, jobs.Distinct().Count());
    }

    [Fact]
    public void GetMatchingJobsReturnsIndependentMutableLists()
    {
        List<CharacterClassType> first = MapleJobTypeExtensions.GetMatchingJobs(32800);
        List<CharacterClassType> second = MapleJobTypeExtensions.GetMatchingJobs(32800);

        Assert.NotSame(first, second);
        first.Clear();

        Assert.Equal(new[] { CharacterClassType.Mercedes, CharacterClassType.Zero }, second);
        Assert.Equal(new[] { CharacterClassType.Phantom }, MapleJobTypeExtensions.GetMatchingJobs(128));
    }
}
