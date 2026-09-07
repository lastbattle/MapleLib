```

  [Host]     : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v4
  Job-IJPESX : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v4

IterationCount=5  LaunchCount=1  WarmupCount=3

```
| Method            | ValidValues | Mean      | Error     | StdDev    | Min       | Max       | Allocated |
|------------------ |------------ |----------:|----------:|----------:|----------:|----------:|----------:|
| **InventoryFromByte** | **False**       | **0.3641 ns** | **0.0004 ns** | **0.0001 ns** | **0.3640 ns** | **0.3642 ns** |         **-** |
| **InventoryFromByte** | **True**        | **0.3642 ns** | **0.0015 ns** | **0.0004 ns** | **0.3637 ns** | **0.3648 ns** |         **-** |
