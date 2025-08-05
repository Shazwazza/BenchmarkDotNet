using System;
using System.Diagnostics;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;

namespace BenchmarkDotNet.Samples
{
    // *** Basic AnsiConsoleLogger Style ***
    [Config(typeof(BasicConfig))]
    public class IntroAnsiConsoleLogger
    {
        private class BasicConfig : ManualConfig
        {
            public BasicConfig()
            {
                AddLogger(new AnsiConsoleLogger());
                UnionRule = ConfigUnionRule.AlwaysUseLocal;
            }
        }

        [Benchmark]
        public long ComputeWithProgress()
        {
            long waitUntil = Stopwatch.GetTimestamp() + 5000;
            while (Stopwatch.GetTimestamp() < waitUntil) { }
            return waitUntil;
        }

        [Benchmark]
        public double MathOperations()
        {
            double result = 0;
            for (int i = 0; i < 10000; i++)
            {
                result += Math.Sqrt(i) * Math.Sin(i);
            }
            return result;
        }
    }

    // *** AnsiConsoleLogger with Unicode Support ***
    [Config(typeof(UnicodeConfig))]
    public class IntroAnsiConsoleLoggerUnicode
    {
        private class UnicodeConfig : ManualConfig
        {
            public UnicodeConfig() => AddLogger(new AnsiConsoleLogger(unicodeSupport: true));
        }

        [Benchmark]
        public string StringOperations()
        {
            var chars = Enumerable.Range(0, 1000).Select(i => (char)('A' + (i % 26))).ToArray();
            return new string(chars);
        }

        [Benchmark]
        public int ArraySum()
        {
            var array = Enumerable.Range(1, 1000).ToArray();
            return array.Sum();
        }
    }

    // *** AnsiConsoleLogger with Custom Color Scheme ***
    [Config(typeof(CustomColorConfig))]
    public class IntroAnsiConsoleLoggerCustomColors
    {
        private class CustomColorConfig : ManualConfig
        {
            public CustomColorConfig() => AddLogger(new AnsiConsoleLogger(
                unicodeSupport: true,
                enableLiveUpdates: true,
                styleScheme: AnsiConsoleLogger.CreateGrayScheme()));
        }

        [Benchmark]
        public long LongRunningTask()
        {
            long waitUntil = Stopwatch.GetTimestamp() + 3000;
            while (Stopwatch.GetTimestamp() < waitUntil) { }
            return waitUntil;
        }

        [Benchmark]
        public void MemoryAllocation()
        {
            var arrays = new int[100][];
            for (int i = 0; i < arrays.Length; i++)
            {
                arrays[i] = new int[100];
            }
        }
    }

    // *** Fluent Config Style ***
    public class IntroAnsiConsoleLoggerFluentConfig
    {
        public static void Run()
        {
            BenchmarkRunner.Run<IntroAnsiConsoleLoggerFluentConfig>(
                DefaultConfig.Instance
                    .AddLogger(new AnsiConsoleLogger(unicodeSupport: true, enableLiveUpdates: true)));
        }

        [Benchmark]
        public double ComplexCalculation()
        {
            double result = 1.0;
            for (int i = 1; i <= 100; i++)
            {
                result *= Math.Log(i + 1) / Math.Sqrt(i);
            }
            return result;
        }

        [Benchmark]
        public string TextProcessing()
        {
            var text = "BenchmarkDotNet with AnsiConsoleLogger provides rich colorized output for better visual experience";
            return string.Join(" ", text.Split(' ').Reverse());
        }
    }

    // *** Comparison with Multiple Loggers ***
    [Config(typeof(ComparisonConfig))]
    public class IntroAnsiConsoleLoggerComparison
    {
        private class ComparisonConfig : ManualConfig
        {
            public ComparisonConfig()
            {
                // Add both loggers to compare output
                AddLogger(ConsoleLogger.Default);
                AddLogger(new AnsiConsoleLogger(unicodeSupport: true));
            }
        }

        [Benchmark]
        public void QuickTask()
        {
            long waitUntil = Stopwatch.GetTimestamp() + 100;
            while (Stopwatch.GetTimestamp() < waitUntil) { }
        }

        [Benchmark]
        [Arguments(1000)]
        [Arguments(5000)]
        [Arguments(10000)]
        public int ParameterizedBenchmark(int iterations)
        {
            int sum = 0;
            for (int i = 0; i < iterations; i++)
            {
                sum += i * i;
            }
            return sum;
        }
    }
}