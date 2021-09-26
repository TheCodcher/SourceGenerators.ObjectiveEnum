using System;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet;
using BenchmarkDotNet.Running;
using System.Linq;
using BenchmarkDotNet.Engines;

namespace ObjectiveEnumBenchmark
{
    class Program
    {
        static void Main(string[] args)
        {
            BenchmarkRunner.Run<OOPEnumTests>();
            BenchmarkRunner.Run<SystemEnumTests>();
        }
    }
}
