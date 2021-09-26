using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

namespace ObjectiveEnumBenchmark
{
    public enum SystemEnumDemo
    {
        first = 1, 
        second, 
        third, 
        fourth, 
        fifth, 
        sixth, 
        seventh,
        eighth, 
        ninth, 
        zero = 0
    }

    //[SimpleJob(RunStrategy.ColdStart, targetCount: 100)]
    public class SystemEnumTests
    {
        public SystemEnumDemo[] ToStringObjects => new SystemEnumDemo[] { SystemEnumDemo.zero, SystemEnumDemo.first, SystemEnumDemo.ninth };

        [Benchmark]
        public Array GetNames()
        {
            return Enum.GetNames<SystemEnumDemo>();
        }

        [Benchmark]
        public Array GetValues()
        {
            return Enum.GetValues<SystemEnumDemo>();
        }

        [Benchmark]
        [Arguments(0), Arguments(1), Arguments(9)]
        public SystemEnumDemo IntToEnumCast(int ordinal)
        {
            return (SystemEnumDemo)ordinal;
        }

        [Benchmark]
        [Arguments("first"), Arguments(9)]
        public bool ExistsPositive(object value)
        {
            return Enum.IsDefined(typeof(SystemEnumDemo), value);
        }

        [Benchmark]
        [Arguments("nothing"), Arguments(11)]
        public bool ExistsNegative(object value)
        {
            return Enum.IsDefined(typeof(SystemEnumDemo), value);
        }

        [Benchmark]
        [Arguments("first"), Arguments("ninth"), Arguments("zero")]
        public SystemEnumDemo Parse(string name)
        {
            return Enum.Parse<SystemEnumDemo>(name);
        }

        [Benchmark]
        [ArgumentsSource(nameof(ToStringObjects))]
        public string ToStringMeth(SystemEnumDemo enumObject)
        {
            return enumObject.ToString();
        }
    }
}
