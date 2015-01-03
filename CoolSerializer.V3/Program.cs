using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace CoolSerializer.V3
{
    class Program
    {
        static void Main(string[] args)
        {
            var graph2 = new Graph()
            {
                Arr = new[] { 1, 3, 4, 5 },
                Coll = new ArrayList() { 4, 5, 6, 7, null,9 },
                S = new InnerStruct
                {
                    I = 5,
                    L = 1
                },
                Z = new InnerGraphDerived()
                {
                    H = 6,
                    Prop = "asd",
                    Surprise = null,
                    MyInt = 29
                }
            };
            ((InnerGraphDerived)graph2.Z).Surprise = graph2;
            ((ArrayList) graph2.Coll).Add(graph2);
            var graph = new List<Graph>() {null, graph2, null, (Graph)graph2};
            ((ArrayList)graph2.Coll).Add(graph);

            //var coll = Enumerable.Range(0, 300).Select(e =>
            //{
            //    var x = new Graph()
            //    {
            //        Arr = new[] {1, 3, 4, 5},
            //        Coll = new ArrayList() {4, 5, 6, 7, null, 9},
            //        S = new InnerStruct
            //        {
            //            I = 5,
            //            L = 1
            //        },
            //        Z = new InnerGraphDerived()
            //        {
            //            H = 6,
            //            Prop = "asd",
            //            Surprise = null,
            //            MyInt = 29
            //        }
            //    };
            //    return x;
            //}).ToList();
            
            var ser = new Serializer();
            var deserializer = new Deserializer();
            var s = new MemoryStream();
            ser.Serialize(s, graph);
            s.Position = 0;
            deserializer.Deserialize(s);

            var count = 1000 * 1000;

            var time = Stopwatch.StartNew();
            //for (int i = 0; i < count; i++)
            //{
            //    s.Position = 0;
            //    ser.Serialize(s, graph);
            //}

            for (int i = 0; i < count; i++)
            {
                s.Position = 0;
                var obj = deserializer.Deserialize(s);
            }

            time.Stop();
            var mps = count/time.Elapsed.TotalSeconds;
            Console.WriteLine(mps);
            //var asd = new TypeInfo(Guid.NewGuid(), typeof(Program).Name,
            //        new[]
            //        {
            //            new FieldInfo(FieldType.Int32,"Field1")
            //        }
            //);

            //var info = new TypeInfoProvider().Provide(typeof(Graph));
            //Console.WriteLine(info);

            //var s = new MemoryStream();
            //for (int i = 0; i < 1000; i++)
            //{
            //    new StreamDocumentWriter(s).WriteTypeInfo(info);
            //}
            //s.Position = 0;
            //TypeInfo infoDes = null;
            //for (int i = 0; i < 1000; i++)
            //{
            //    infoDes = new StreamDocumentReader(s).ReadTypeInfo();
            //}
            //Console.WriteLine(infoDes);
            //var info2 = new TypeInfoProvider().Provide(typeof(InnerGraphDerived));
            //Console.WriteLine(info2);
            //while (true)
            //{

            //}
        }
    }

    public class Graph
    {
        public int[] Arr { get; set; }
        public ICollection Coll { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public InnerGraph Z { get; set; }
        public InnerStruct S { get; set; }
    }

    public struct InnerStruct
    {
        public long L { get; set; }
        public int I { get; set; }
    }

    public class InnerGraph
    {
        public int H { get; set; }
    }

    class InnerGraphDerived : InnerGraph
    {
        public int? MyInt { get; set; }
        public string Prop { get; set; }
        public object Surprise { get; set; }
    }

    class TypeInfoDescriptorProvider
    {
        public TypeDescriptor Provide(TypeInfo info)
        {
            throw new NotImplementedException();
        }
    }

    class TypeDescriptor
    {

    }
}
