using MapleLib.Helpers;
using System.IO;
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests;

[CollectionDefinition("ErrorLogger", DisableParallelization = true)]
public sealed class ErrorLoggerCollectionDefinition
{
}

[Collection("ErrorLogger")]
public sealed class ErrorLoggerAdversarialTests : IDisposable
{
    public ErrorLoggerAdversarialTests() => ErrorLogger.ClearErrors();

    public void Dispose() => ErrorLogger.ClearErrors();

    [Fact]
    public void SaveFailurePreservesPendingErrors()
    {
        ErrorLogger.Log(ErrorLevel.Critical, "must survive");
        string missingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "errors.log");

        Assert.Throws<DirectoryNotFoundException>(() => ErrorLogger.SaveToFile(missingDirectory));

        Assert.Equal(1, ErrorLogger.NumberOfErrorsPresent());
        Assert.Equal("must survive", ErrorLogger.GetErrorSnapshot()[ErrorLevel.Critical][0].Message);
    }

    [Fact]
    public async Task ConcurrentReadsAndWritesRemainConsistent()
    {
        Task[] writers = Enumerable.Range(0, 4)
            .Select(worker => Task.Run(() =>
            {
                for (int i = 0; i < 500; i++)
                    ErrorLogger.Log(ErrorLevel.Info, $"{worker}:{i}");
            }))
            .ToArray();
        Task reader = Task.Run(() =>
        {
            while (writers.Any(task => !task.IsCompleted))
            {
                _ = ErrorLogger.ErrorsPresent();
                _ = ErrorLogger.NumberOfErrorsPresent();
            }
        });

        await Task.WhenAll(writers.Append(reader));

        Assert.Equal(2_000, ErrorLogger.NumberOfErrorsPresent());
    }

    [Fact]
    public async Task ConcurrentSavesPersistEachSnapshotOnlyOnce()
    {
        const int saveCount = 8;
        string marker = $"save-marker-{Guid.NewGuid():N}";
        string path = Path.Combine(Path.GetTempPath(), $"MapleLib.Errors-{Guid.NewGuid():N}.log");
        ErrorLogger.Log(ErrorLevel.Critical, new string('x', 100_000) + marker);

        using var barrier = new Barrier(saveCount);
        try
        {
            Task[] saves = Enumerable.Range(0, saveCount)
                .Select(_ => Task.Factory.StartNew(() =>
                {
                    barrier.SignalAndWait();
                    ErrorLogger.SaveToFile(path);
                }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default))
                .ToArray();
            await Task.WhenAll(saves);

            string contents = File.ReadAllText(path);
            Assert.Equal(1, CountOccurrences(contents, marker));
            Assert.False(ErrorLogger.ErrorsPresent());
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private static int CountOccurrences(string value, string needle)
    {
        int count = 0;
        for (int index = 0; (index = value.IndexOf(needle, index, StringComparison.Ordinal)) >= 0; index += needle.Length)
            count++;
        return count;
    }
}
