```

  [Host]     : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v4
  Job-IJPESX : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v4

IterationCount=5  LaunchCount=1  WarmupCount=3

```
| Method                 | ValidValues | Mean      | Error     | StdDev    | Min       | Max       | Allocated |
|----------------------- |------------ |----------:|----------:|----------:|----------:|----------:|----------:|
| **CharacterSubJobFromInt** | **False**       | **1.8330 ns** | **0.0068 ns** | **0.0011 ns** | **1.8316 ns** | **1.8342 ns** |         **-** |
| QuestAreaFromInt       | False       | 5.0001 ns | 0.0281 ns | 0.0043 ns | 4.9953 ns | 5.0045 ns |         - |
| QuestMedalFromInt      | False       | 0.8011 ns | 0.0034 ns | 0.0009 ns | 0.8001 ns | 0.8024 ns |         - |
| InventoryFromByte      | False       | 0.3633 ns | 0.0004 ns | 0.0001 ns | 0.3632 ns | 0.3635 ns |         - |
| **CharacterSubJobFromInt** | **True**        | **1.8650 ns** | **0.0147 ns** | **0.0038 ns** | **1.8601 ns** | **1.8701 ns** |         **-** |
| QuestAreaFromInt       | True        | 4.2576 ns | 0.0736 ns | 0.0191 ns | 4.2323 ns | 4.2740 ns |         - |
| QuestMedalFromInt      | True        | 0.8017 ns | 0.0024 ns | 0.0006 ns | 0.8008 ns | 0.8024 ns |         - |
| InventoryFromByte      | True        | 0.3643 ns | 0.0006 ns | 0.0002 ns | 0.3641 ns | 0.3645 ns |         - |
