using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoolSerializer
{
    class Program
    {
        static void Main(string[] args)
        {
            //MemoryStream ms = new MemoryStream();
            //var guid = Guid.NewGuid();
            //var mar = new GuidMarshaler();
            //mar.SerializeGuid(ms,guid);
            //mar.SerializeGuid(ms,Guid.Empty);
            //ms.Flush();
            //ms.Position = 0;
            //var firstGuid = mar.DeserializeGuid(ms);
            //var emptyGuid = mar.DeserializeGuid(ms);
            //if (guid == firstGuid)
            //{
            //    Console.WriteLine("Happy!");
            //}
            
            var g = new Graph()
            {
                S = new InnerStruct
                {
                    I = 5,
                    L = 1
                },
                Z = new InnerGraphDerived()
                {
                    H = 6,
                    Prop = "asd"
                }
            };

            var c = new ObjectClimber();
            Stream ms = new MemoryStream();
            c.Climb(ms,g);
            ms.Position = 0;
            var d = new ObjectDeserializer();
            var newG = d.Deserialize<Graph>(ms);
            var holder = new GraphHolder(null, Console.WriteLine);
            ProcessGraph(ref holder.Graph,holder.Climb);
        }

        class GraphHolder
        {
            private readonly Action<Graph> mGAction;
            public Graph Graph;

            public GraphHolder(Graph graph, Action<Graph> gAction)
            {
                Graph = graph;
                mGAction = gAction;
            }


            public void Climb()
            {
                mGAction(Graph);
            }
        }

        public static void ProcessGraph(ref Graph g, Action climb)
        {
            g = new Graph();
            climb();
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
    }
}
