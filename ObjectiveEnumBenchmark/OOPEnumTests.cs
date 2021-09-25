using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ObjectiveEnum;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

namespace ObjectiveEnumBenchmark
{
    [ObjectiveEnum]
    public partial class OOPEnumDemo
    {
        static OOPEnumDemo()
        {
            Enum.first()(1); 
            Enum.second(); 
            Enum.third(); 
            Enum.fourth(); 
            Enum.fifth(); 
            Enum.sixth(); 
            Enum.seventh(); 
            Enum.eighth(); 
            Enum.ninth();
            Enum.zero()(0);
        }
    }

    [SimpleJob(RunStrategy.ColdStart, targetCount: 100)]
    public class OOPEnumTests
    {
        public OOPEnumDemo[] ToStringObjects => new OOPEnumDemo[] { OOPEnumDemo.zero, OOPEnumDemo.first, OOPEnumDemo.ninth };

        [GlobalSetup]
        public void Prepare()
        {
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(OOPEnumDemo).TypeHandle);
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(ObjectiveEnum).TypeHandle);
        }

        [Benchmark]
        public Array GetNames()
        {
            return ObjectiveEnum.GetNames<OOPEnumDemo>();
        }

        [Benchmark]
        public Array GetValues()
        {
            return ObjectiveEnum.GetValues<OOPEnumDemo>();
        }

        [Benchmark]
        [Arguments(0), Arguments(1), Arguments(9)]
        public OOPEnumDemo IntToEnumCast(int ordinal)
        {
            return (OOPEnumDemo)ordinal;
        }

        [Benchmark]
        [Arguments("first"), Arguments(9)]
        public bool ExistsPositive(object value)
        {
            return value is string s ? ObjectiveEnum.Exists(typeof(OOPEnumDemo), s) : ObjectiveEnum.Exists(typeof(OOPEnumDemo), (int)value);
        }
        
        [Benchmark]
        [Arguments("nothing"), Arguments(11)]
        public bool ExistsNegative(object value)
        {
            return value is string s ? ObjectiveEnum.Exists(typeof(OOPEnumDemo), s) : ObjectiveEnum.Exists(typeof(OOPEnumDemo), (int)value);
        }

        [Benchmark]
        [Arguments("first"), Arguments("ninth"), Arguments("zero")]
        public OOPEnumDemo Parse(string name)
        {
            return ObjectiveEnum.GetValue<OOPEnumDemo>(name);
        }

        [Benchmark]
        [ArgumentsSource(nameof(ToStringObjects))]
        public string ToStringMeth(OOPEnumDemo enumObject)
        {
            return enumObject.ToString();
        }

        [Benchmark]
        public bool IsDefinedPositive()
        {
            return ObjectiveEnum.IsDefined<OOPEnumDemo>();
        }

        [Benchmark]
        public bool IsDefinedNegative()
        {
            return ObjectiveEnum.IsDefined<SystemEnumDemo>();
        }
    }
}
