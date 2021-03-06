# Jobs

Basically, *job* describes how to run your benchmark. Practically, it's a set of characteristics which can be specified. You can set one or several jobs for your benchmarks.

## Characteristics

There are several categories of characteristics which you can specify. Let's consider each category in detail.

### Id
It's a single string characteristics. It allows to name your job. This name will be used in logs and a part of a folder name with generated files for this job. `Id` doesn't affect benchmark results, but it can be useful for diagnostics. If you don't specify `Id`, random value will be chosed based on other characteristics

### Env
`Env` specifies an environment of the job. You can specify the following characteristics:

* `Platform`: `x86` or `x64`
* `Runtime`:
  * `Clr`: Full .NET Framework (available only on Windows)
  * `Core`: CoreCLR (x-plat)
  * `Mono`: Mono (x-plat)
* `Jit`:
  * `LegacyJit` (available only for `Runtime.Clr`)
  * `RyuJit` (avaiable only for `Runtime.Clr` and `Runtime.Core`)
  * `Llvm` (avaiable only for `Runtime.Mono`)
* `Affinity`: [Affinity](https://msdn.microsoft.com/library/system.diagnostics.process.processoraffinity.aspx) of a benchmark process
* `GcMode`: settings of Garbage Collector
  * `Server`: `true` (Server mode) or `false` (Workstation mode)
  * `Concurrent`:  `true` (Concurrent mode) or `false` (NonConcurrent mode)
  * `CpuGroups`:  Specifies whether garbage collection supports multiple CPU groups
  * `Force`: Specifies whether the BenchmarkDotNet's benchmark runner forces full garbage collection after each benchmark invocation
  * `AllowVeryLargeObjects`:  On 64-bit platforms, enables arrays that are greater than 2 gigabytes (GB) in total size

BenchmarkDotNet will use host process environment characteristics for non specified values.

### Run
In this category, you can specifiy how to benchmark each method.

* `RunStrategy`:
  * `Throughput`: default strategy which allows to get good precision level
  * `ColdStart`: should be used only for measuring cold start of the application or testing purpose
* `LaunchCount`: how many times we should launch process with target benchmark
* `WarmupCount`: how many warmup iterations should be performed
* `TargetCount`: how many target iterations should be performed
* `IterationTime`: desired time of a single iteration
* `UnrollFactor`: how many times the benchmark method will be invoked per one iteration of a generated loop
* `InvocationCount`: count of invocation in a single iteration (if specified, `IterationTime` will be ignored), must be a multiple of `UnrollFactor`

Usually, you shouldn't specify such characteristics like `LaunchCount`, `WarmupCount`, `TargetCount`, or `IterationTime` because BenchmarkDotNet has a smart algorithm to choose these values automatically based on recieved measurements. You can specify it for testing purposes or when you are damn sure that you know perfect characteristics for your benchmark (when you set `TargetCount` = `20` you should unserstand why `20` is a good value for your case).

### Accuracy
If you want to change the accuracy level, you should use the following characteristics instead of manual of values of `WarmupCount`, `TargetCount`, and so on.

* `MaxStdErrRelative`: Maximum relative standard error (`StandardError`/`Mean`) which you want to achive.
* `MinIterationTime`: Minimum time of a single iteration. Unlike `Run.IterationTime`, this characteristic specify only the lower limit. In case of need, BenchmarkDotNet can increase this value.
* `MinInvokeCount`:  Minimum about of target method invocation. Default value if `4` but you can decrease this value for cases when single invocations takes a lot of time.
* `EvaluateOverhead`: if you benchmark method takes nanoseconds, BenchmarkDotNet overhead can significantly affect measurements. If this characterics is enable, the overhead will be evaluated and substracted from the result measurements. Default value is `true`.
* `RemoveOutliers`: sometimes you could have outliers in your measurements. Usually these are *unexpected* ourliers which arised because of other processes activities. If this characteristics is enable, all outliers will be removed from the result measurements. However, some of benchmarks have *expected* outliers. In these situation, you expect that some of invocation can produce ourliers measurements (e.g. in case of network acitivities, cache operations, and so on). If you want to see result statistics with these outliers, you should disable this characteristic. Default value is `true`.
* `AnaylyzeLaunchVariance`: this characteristics makes sense only if `Run.LaunchCount` is default. If this mode is enabled and , BenchmarkDotNet will try to perform several launches and detect if there is a veriance betnween launches. If this mode is disable, only one launch will be performed.

### Infrastructure
Usually, you shouldn't specify any characteristics from this section, it can be used for advanced cases only.

* `Toolchain`: a toolchain which generate source code for target benchmark methods, build it, and execute it. BenchmarkDotNet has own toolchains for CoreCLR projects and classic projects (the last one is `RoslynToolchain`, you can find it in the [BenchmarkDotNet.Toolchains.Roslyn](https://www.nuget.org/packages/BenchmarkDotNet.Toolchains.Roslyn/) NuGet package). If you want, you can define own toolchain.
* `Clock`: a clock which will be used for measurements. BenchmarkDotNet automatically choose the best available clock source, but you can specify own clock source.
* `EngineFactory`: a provider for measurement engine which performs all the measurement magic. If you don't trust BenchmarkDotNet, you can define own engine and implement all the measurement stages manually.

## Usage

There are several ways to specify a job.

### Object style

You can create own jobs directly from the source code via a cusom config:

```cs
[Config(typeof(Config))]
public class MyBenchmarks
{
    private class Config : ManualConfig
    {
        public Config()
        {
            Add(
                new Job("MySuperJob", EnvMode.RyuJitX64, RunMode.Dry)
                {
                    Env = { Runtime = Runtime.Core },
                    Run = { LaunchCount = 5, IterationTime = TimeInterval.Millisecond * 200 },
                    Accuracy = { MaxStdErrRelative = 0.01 }
                })
        }
    }
    // Benchmarks
}
```

Basically, it's a good idea to start with predefined values (e.g. `EnvMode.RyuJitX64` and `RunMode.Dry` passed as constructor args) and modify rest of the properties using property setters or with help of object initialzer syntax.

Note that the job cannot be modified after it's added into config. Trying to set a value on property of the frozen job will throw an `InvalidOperationException`. Use the `Job.Frozen` property to determine if the code properties can be altered.

### Attribute style

You can also add new jobs via attributes. Examples:

```cs
[DryJob]
[ClrJob, CoreJob, MonoJob]
[LegacyJitX86Job, LegacyJitX64, RyuJitX64Job]
[SimpleJob(RunStrategy.ColdStart, launchCount: 1, warmupCount: 5, targetCount: 5, id: "FastAndDirtyJob")]
public class MyBenchmarkClass
```

Note that each of the attribute identifies a separate job, the sample above will result in 8 different jobs, not merged one.

#### Custom attributes

You can also create own custom attribute with your favorite set of jobs. Example:

```cs
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly)]
public class MySuperJobAttribute : Attribute, IConfigSource
{
    protected MySuperJobAttribute()
    {
        var job = new Job("MySuperJob", RunMode.Core);
        job.Env.Platform = Platform.X64;
        Config = ManualConfig.CreateEmpty().With(job);
    }

    public IConfig Config { get; }
}

[MySuperJob]
public class MyBenchmarks
```