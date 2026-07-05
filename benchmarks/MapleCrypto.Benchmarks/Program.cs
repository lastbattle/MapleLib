using BenchmarkDotNet.Running;

namespace MapleCrypto.Benchmarks;

internal static class Program
{
    public static void Main(string[] args)
    {
        CryptoCorrectness.Verify();

        if (args.Contains("--verify", StringComparer.Ordinal))
        {
            CryptoCorrectness.PrintDigests();
            return;
        }

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
