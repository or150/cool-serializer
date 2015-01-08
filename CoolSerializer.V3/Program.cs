using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace CoolSerializer.V3
{
    public class Program
    {
        private static Graph CreateGraph()
        {
            var graph = new Graph()
            {
                //Arr = new[] { 1, 3, 4, 5 },
                Coll = new ArrayList() { 4, 5, 6, 7, null, 9 ,Guid.NewGuid()},
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
                },
                StructInObj = new InnerStruct()
                {
                    I = 9,
                    L = 4
                }
            };

            ((InnerGraphDerived)graph.Z).Surprise = graph;
            return graph;
        }

        private static Dictionary<int, object> CreateMyBadClass()
        {
            var graph = new Dictionary<int, object>
            {
                {6, "6"},
                {12, "SAD"},
                {4, "but"},
                {5, "true"},
                {9, new MyBadClass(67, "89")}
            };
            ((MyBadClass)graph[9]).InitBadClass((MyBadClass)graph[9]);
            return graph;
        }

        public abstract class ShekerBase// : IExtraDataHolder
        {
            //public ExtraData ExtraData { get; set; }
            public abstract int Number { get; set; }
        }

        //public class Sheker1 : ShekerBase
        //{
        //    public int Number { get; set; }
        //}

        public class Sheker2 : ShekerBase
        {
            public string Text { get; set; }
            public override int Number { get; set; }
        }
        static void Main(string[] args)
        {
            //var graph = new List<ShekerBase> { new Sheker1() { Number = 8 }, new Sheker2() { Text = "wasdwasd" } };

            //var graph = CreateGraph();

            //var random = new Random();
            //var graph = Enumerable.Range(0, 150).Select((x) => CreateGraph())
            //    .Concat<object>(Enumerable.Range(0, 150).Select(x => CreateMyBadClass()))
            //    .Concat(Enumerable.Range(0, 150).Select(x => Guid.NewGuid()).Cast<object>())
            //    .Concat(Enumerable.Range(0, 150).Select(x => new DateTime(random.Next(ushort.MaxValue, int.MaxValue / 2) * (long)random.Next(int.MaxValue / 2, int.MaxValue))).Cast<object>())
            //    .Concat(Enumerable.Range(0, 150).Select(x=> Guid.NewGuid().ToString("B"))).ToList();
            //var graph = CreateMyBadClass();

            //var graph = new object[] {CreateGraph(), CreateMyBadClass(), null};
            //graph[2] = graph;


            //var graph = new Graph() {Z = new InnerGraph() {H = 9}};
            //((ArrayList) graph2.Coll).Add(graph2);
            //var graph = new List<Graph>() {null, graph2, null, (Graph)graph2};
            //((ArrayList)graph2.Coll).Add(graph);

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
            //using (var sWrite = File.Open(@"..\asd.txt", FileMode.Create))
            //{
            //    ser.Serialize(sWrite, graph);
            //    sWrite.Position = 0;
            //}

            var deserializer = new Deserializer();
            var s = new MemoryStream();
            byte[] sReadArr = null;
            using (var sRead = File.Open(@"..\asd.txt", FileMode.Open))
            {
                sRead.CopyTo(s);
                sReadArr = s.ToArray();
                s.Position = 0;
            }
            var myObj = deserializer.Deserialize(s);
            //myObj.Arr2 = new[] { 6.98f, 7.0f, 6f };
            s.Position = 0;
            ser.Serialize(s, myObj);

            using (var sWrite = File.Open(@"..\asd.txt", FileMode.Create))
            {
                ser.Serialize(sWrite, myObj);
                sWrite.Position = 0;
            }

            var count = 1000 * 10;

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
                obj = Volatile.Read(ref obj);
            }

            time.Stop();
            var mps = count / time.Elapsed.TotalSeconds;
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

    public class Graph : IExtraDataHolder
    {
        public int[] Arr { get; set; }
        public ICollection Coll { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public InnerGraph Z { get; set; }
        public InnerStruct S { get; set; }
        public object StructInObj { get; set; }
        public float[] Arr2 { get; set; }
        public ExtraData ExtraData { get; set; }
    }

    public struct InnerStruct
    {
        public long L { get; set; }
        public int I { get; set; }
    }

    public class InnerGraph : IExtraDataHolder
    {
        public int H { get; set; }
        public ExtraData ExtraData { get; set; }
    }

    class MyBadClass
    {
        public MyBadClass(int i, string s)
        {
            S = s;
            I = i;
        }

        public void InitBadClass(MyBadClass badClass)
        {
            BadBadClass = badClass;
        }

        public int I { get; private set; }
        public string S { get; private set; }
        public MyBadClass BadBadClass { get; private set; }
    }

    class MyGoodClass
    {
        public int MyGoodI { get; set; }
        public string MyGoodS { get; set; }
        public MyBadClass MyBadBadClass { get; set; }
    }

    class MyNiceSimplifier : ISimpifier<MyBadClass, MyGoodClass>
    {
        public MyGoodClass Simplify(MyBadClass obj)
        {
            return new MyGoodClass() { MyGoodI = obj.I, MyGoodS = obj.S, MyBadBadClass = obj.BadBadClass };
        }

        public MyBadClass Desimplify(MyGoodClass simpleObj)
        {
            var x=  new MyBadClass(simpleObj.MyGoodI, simpleObj.MyGoodS);
            x.InitBadClass(simpleObj.MyBadBadClass);
            return x;
        }
    }

    class InnerGraphDerived : InnerGraph
    {
        public int? MyInt { get; set; }
        public string Prop { get; set; }
        public object Surprise { get; set; }
    }
}
