```

  [Host]     : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v4
  Job-IJPESX : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v4

IterationCount=5  LaunchCount=1  WarmupCount=3

```
| Method                   | Mean      | Error     | StdDev    | Min       | Max       | Gen0   | Allocated |
|------------------------- |----------:|----------:|----------:|----------:|----------:|-------:|----------:|
| CharacterJobFormatting   | 71.417 ns | 0.3010 ns | 0.0782 ns | 71.319 ns | 71.509 ns | 0.0033 |     168 B |
| PreBigBangJobFormatting  | 23.784 ns | 1.1751 ns | 0.1818 ns | 23.588 ns | 24.028 ns | 0.0016 |      80 B |
| CapitalizeFirstCharacter |  5.258 ns | 0.0271 ns | 0.0070 ns |  5.252 ns |  5.268 ns | 0.0010 |      48 B |
