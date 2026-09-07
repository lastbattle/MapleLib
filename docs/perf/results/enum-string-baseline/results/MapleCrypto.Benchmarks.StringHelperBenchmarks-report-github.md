```

  [Host]     : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v4
  Job-IJPESX : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v4

IterationCount=5  LaunchCount=1  WarmupCount=3

```
| Method                   | Mean      | Error    | StdDev   | Min       | Max       | Gen0   | Allocated |
|------------------------- |----------:|---------:|---------:|----------:|----------:|-------:|----------:|
| CharacterJobFormatting   | 184.07 ns | 9.217 ns | 2.394 ns | 181.98 ns | 187.67 ns | 0.0174 |     880 B |
| PreBigBangJobFormatting  |  83.27 ns | 1.040 ns | 0.270 ns |  82.95 ns |  83.64 ns | 0.0125 |     632 B |
| CapitalizeFirstCharacter |  12.92 ns | 0.088 ns | 0.014 ns |  12.91 ns |  12.94 ns | 0.0029 |     144 B |
