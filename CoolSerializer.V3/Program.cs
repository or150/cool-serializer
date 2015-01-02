﻿using System;
using System.Diagnostics;
using System.IO;

namespace CoolSerializer.V3
{
    class Program
    {
        static void Main(string[] args)
        {
            var graph = new Graph()
            {
                S = new InnerStruct
                {
                    I = 5,
                    L = 1
                },
                Z = new InnerGraphDerived()
                {
                    H = 6,
                    Prop = "asd",
                    Surprise = 7ul
                }
            };
            ((InnerGraphDerived) graph.Z).Surprise = graph;

            //var graph = new InnerGraphDerived()
            //{
            //    H = 6,
            //    Prop = "Proppp",
            //    Surprise = 7ul
            //};
            var ser = new Serializer();
            var deserializer = new Deserializer();
            var s = new MemoryStream();
            ser.Serialize(s, graph);
            s.Position = 0;
            deserializer.Deserialize(s);

            var time  = Stopwatch.StartNew();
            var count = 1000 * 1000;
            for (int i = 0; i < count ; i++)
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