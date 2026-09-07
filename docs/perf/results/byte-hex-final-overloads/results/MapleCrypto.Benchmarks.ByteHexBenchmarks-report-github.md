```

  [Host]     : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v4
  Job-IJPESX : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v4

IterationCount=5  LaunchCount=1  WarmupCount=3

```
| Method                         | Length | Mean        | Error       | StdDev      | Min         | Max         | Gen0   | Allocated |
|------------------------------- |------- |------------:|------------:|------------:|------------:|------------:|-------:|----------:|
| **HexToBytesSeparatedAndPrefixed** | **16**     |    **117.3 ns** |     **4.68 ns** |     **1.21 ns** |    **116.3 ns** |    **119.1 ns** | **0.0007** |      **40 B** |
| **HexToBytesSeparatedAndPrefixed** | **256**    |  **1,801.0 ns** |    **56.21 ns** |    **14.60 ns** |  **1,784.7 ns** |  **1,816.7 ns** | **0.0038** |     **280 B** |
| **HexToBytesSeparatedAndPrefixed** | **4096**   | **48,341.5 ns** | **4,628.85 ns** | **1,202.10 ns** | **47,224.6 ns** | **49,670.9 ns** | **0.0610** |    **4120 B** |
