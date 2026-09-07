```

  [Host]     : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v4
  Job-IJPESX : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v4

IterationCount=5  LaunchCount=1  WarmupCount=3

```
| Method                   | Mean      | Error     | StdDev    | Min       | Max       | Gen0   | Allocated |
|------------------------- |----------:|----------:|----------:|----------:|----------:|-------:|----------:|
| CharacterJobFormatting   | 75.380 ns | 0.6265 ns | 0.1627 ns | 75.244 ns | 75.562 ns | 0.0033 |     168 B |
| PreBigBangJobFormatting  | 23.680 ns | 0.2473 ns | 0.0383 ns | 23.622 ns | 23.704 ns | 0.0016 |      80 B |
| CapitalizeFirstCharacter |  5.451 ns | 0.3027 ns | 0.0786 ns |  5.365 ns |  5.567 ns | 0.0010 |      48 B |
