```

  [Host]     : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v4
  Job-IJPESX : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v4

IterationCount=5  LaunchCount=1  WarmupCount=3

```
| Method                 | ValidValues | Mean      | Error     | StdDev    | Min       | Max       | Gen0   | Allocated |
|----------------------- |------------ |----------:|----------:|----------:|----------:|----------:|-------:|----------:|
| **CharacterSubJobFromInt** | **False**       |  **5.819 ns** | **0.1069 ns** | **0.0278 ns** |  **5.796 ns** |  **5.862 ns** | **0.0005** |      **24 B** |
| QuestAreaFromInt       | False       |  9.559 ns | 0.9987 ns | 0.2594 ns |  9.352 ns |  9.869 ns | 0.0005 |      24 B |
| QuestMedalFromInt      | False       |  4.860 ns | 0.1079 ns | 0.0167 ns |  4.837 ns |  4.873 ns | 0.0005 |      24 B |
| InventoryFromByte      | False       | 14.073 ns | 0.1554 ns | 0.0403 ns | 14.041 ns | 14.118 ns | 0.0024 |     120 B |
| **CharacterSubJobFromInt** | **True**        |  **5.881 ns** | **0.0324 ns** | **0.0084 ns** |  **5.869 ns** |  **5.888 ns** | **0.0005** |      **24 B** |
| QuestAreaFromInt       | True        |  8.143 ns | 0.0349 ns | 0.0054 ns |  8.138 ns |  8.148 ns | 0.0005 |      24 B |
| QuestMedalFromInt      | True        |  4.834 ns | 0.0413 ns | 0.0107 ns |  4.824 ns |  4.849 ns | 0.0005 |      24 B |
| InventoryFromByte      | True        | 13.071 ns | 0.0473 ns | 0.0073 ns | 13.061 ns | 13.077 ns | 0.0024 |     120 B |
