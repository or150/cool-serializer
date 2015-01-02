using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace CoolSerializer.V2
{
    class Program
    {
        static void Main(string[] args)
        {
            var info = new RawTypeInformation("Type1", new List<RawFieldInformation>()
            {
                new RawFieldInformation("ASd", FieldType.Int32),
                new RawFieldInformation("WASD", FieldType.String)
            });
            var newInfo = RawTypeInformation.Deserialize(new MemoryStream(info.Serialize()));
            IFieldInformationProvider provider = new PropertyFieldInformationProvider();
            var a = provider.GetFieldInformation(typeof(InnerGraphDerived));
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

    internal interface ITypeInformationProvider
    {
        ITypeInformation GetTypeInformation(Guid typeId, RawTypeInformation rawInfo);
        ITypeInformation GetTypeInformation(Type type);
    }

    interface IFieldInformationProvider
    {
        IEnumerable<IFieldInformation> GetFieldInformation(RawTypeInformation typeInfo);
        IEnumerable<IFieldInformation> GetFieldInformation(Type typeInfo);
    }

    class PropertyFieldInformationProvider : IFieldInformationProvider
    {
        public IEnumerable<IFieldInformation> GetFieldInformation(RawTypeInformation typeInfo)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<IFieldInformation> GetFieldInformation(Type typeInfo)
        {
            return typeInfo.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.SetProperty).Select(CreateFieldInformation).ToList();
        }

        private IFieldInformation CreateFieldInformation(PropertyInfo info)
        {
            switch (info.PropertyType.GetRawType())
            {
                case FieldType.Int32:
                    return new PrimitiveFieldInformation(info, new Action<int>(new Document(Stream.Null).WriteNumber).Method);
                case FieldType.String:
                    return new PrimitiveFieldInformation(info, new Action<string>(new Document(Stream.Null).WriteString).Method);
                default:
                    throw new NotImplementedException();
            }
        }
    }

    internal class PrimitiveFieldInformation : IFieldInformation
    {
        private readonly PropertyInfo mFieldInfo;
        private readonly MethodInfo mMethod;

        public PrimitiveFieldInformation(PropertyInfo fieldInfo, MethodInfo method)
        {
            Name = fieldInfo.Name;
            mFieldInfo = fieldInfo;
            mMethod = method;
            RawType = fieldInfo.PropertyType.GetRawType();
        }

        public string Name { get; private set; }
        public FieldType RawType { get; private set; }
        public Expression GetSerializeExpression(Expression graph, Expression document)
        {
            return Expression.Call(document, mMethod, Expression.MakeMemberAccess(graph, mFieldInfo));
        }

        public Expression GetDeserializeExpression(Expression graph, Expression document)
        {
            throw new NotImplementedException();
        }
    }

    interface ITypeInformation
    {
        Guid Id { get; }
        string Name { get; }
        IList<IFieldInformation> Fields { get; }
        Action<Document, T> GetDeserializeMethod<T>();
        Action<Document, T> GetSerializeMethod<T>();
        Expression GetSerializeExpression();
        Expression GetDeserializeExpression();
    }

    class RealTypeInformation : ITypeInformation
    {
        public Guid Id { get; private set; }
        public string Name { get; private set; }
        public IList<IFieldInformation> Fields { get; private set; }

        public RealTypeInformation(Type type)
        {
            Id = Guid.NewGuid();
            Name = type.FullName;
            Fields = new List<IFieldInformation>();
        }
        public Action<Document, T> GetDeserializeMethod<T>()
        {
            throw new NotImplementedException();
        }

        public Action<Document, T> GetSerializeMethod<T>()
        {
            throw new NotImplementedException();
        }

        public Expression GetSerializeExpression()
        {
            throw new NotImplementedException();
        }

        public Expression GetDeserializeExpression()
        {
            throw new NotImplementedException();
        }
    }

    interface IFieldInformation
    {
        string Name { get; }
        FieldType RawType { get; }
        Expression GetSerializeExpression(Expression graph, Expression document);
        Expression GetDeserializeExpression(Expression graph, Expression document);
    }
}
