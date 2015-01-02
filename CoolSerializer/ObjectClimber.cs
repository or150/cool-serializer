using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace CoolSerializer
{
    public class ObjectClimber
    {
        private static readonly MethodInfo WriteIntMethod = new Action<int>(new Document(Stream.Null).WriteNumber).Method;
        private static readonly MethodInfo WriteHeaderMethod = new Action<TypeInformation>(new Document(Stream.Null).WriteHeader).Method;
        private static readonly MethodInfo WriteStringMethod = new Action<string>(new Document(Stream.Null).WriteString).Method;
        private static readonly MethodInfo WriteNullMethod = new Action(new Document(Stream.Null).WriteNull).Method;

        private readonly Dictionary<Type, Delegate> mDelegates;
        private readonly Dictionary<Tuple<Type,Type>, Delegate> mPolyDelegates;
        private readonly Dictionary<Type, Expression> mExpressions;
        private readonly Dictionary<Type, TypeInformation> mKnownTypes;
        private readonly IFieldProvider mFieldProvider;

        public ObjectClimber()
        {
            mDelegates = new Dictionary<Type, Delegate>();
            mPolyDelegates = new Dictionary<Tuple<Type, Type>, Delegate>();
            mKnownTypes = new Dictionary<Type, TypeInformation>();
            mExpressions = new Dictionary<Type, Expression>();
            mFieldProvider = new FieldProvider();
        }

        public void Climb<T>(Stream stream, T obj)
        {
            var document = new Document(stream);
            if (obj == null)
            {
                document.WriteNull();
                return;
            }
            GetPolymorphicClimbDelegate<T>(obj.GetType())(document, obj);
        }
        private Action<Document, T> GetClimbDelegate<T>()
        {
            Delegate tempDel;
            if (mDelegates.TryGetValue(typeof(T), out tempDel))
            {
                return (Action<Document, T>)tempDel;
            }

            Expression<Action<Document, T>> actionExpr;
            Action<Document, T> action;
            CreateClimbDelegate(out action, out actionExpr);
            mDelegates[typeof(T)] = action;
            mExpressions[typeof(T)] = actionExpr;
            return action;
        }

        private Action<Document, TBase> GetPolymorphicClimbDelegate<TBase>(Type derivedType)
        {
            var baseType = typeof (TBase);
            if (derivedType == baseType)
            {
                return GetClimbDelegate<TBase>();
            }

            Delegate tempDel;
            if(mPolyDelegates.TryGetValue(Tuple.Create(baseType,derivedType), out tempDel))
            {
                return (Action<Document, TBase>) tempDel;
            }

            Action<Document, TBase> action = CreatePolymorphicClimbDelegate<TBase>(derivedType);
            mPolyDelegates[Tuple.Create(baseType,derivedType)] = action;
            return action;
        }

        private Action<Document, TBase> CreatePolymorphicClimbDelegate<TBase>(Type derivedType)
        {
            var objParam = Expression.Parameter(typeof (TBase),"graph");
            var documentParam = Expression.Parameter(typeof (Document),"document");
            var casted = Expression.Variable(derivedType, "castedGraph");
            
            var method = this.GetType()
                .GetMethod("GetClimbExpression",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .MakeGenericMethod(derivedType);
            var childClimb = ((LambdaExpression)method.Invoke(this, new object[0]));

            var setExpr = new ParameterReplacer(childClimb.Parameters, new Expression[] { documentParam, casted }).Visit(childClimb.Body);


            var body = Expression.Block(new[]{casted},new[]{
                Expression.Assign(casted, Expression.Convert(objParam, derivedType)),
                setExpr}
                );
            var lambda = Expression.Lambda<Action<Document, TBase>>(body, new[] {documentParam, objParam});
            return lambda.Compile();
        }


// ReSharper disable once UnusedMember.Local
        private Expression<Action<Document, T>> GetClimbExpression<T>()
        {
            Expression tempExpr;
            if (mExpressions.TryGetValue(typeof(T), out tempExpr))
            {
                return (Expression<Action<Document, T>>)tempExpr;
            }

            Expression<Action<Document, T>> actionExpr;
            Action<Document, T> action;

            CreateClimbDelegate(out action, out actionExpr);
            mDelegates[typeof(T)] = action;
            mExpressions[typeof(T)] = actionExpr;
            return actionExpr;
        }

        private void CreateClimbDelegate<T>(out Action<Document, T> @delegate, out Expression<Action<Document, T>> expression)
        {
            var type = typeof(T);
            var objectParam = Expression.Parameter(type, "graph");
            var documentParam = Expression.Parameter(typeof(Document), "document");
            List<Expression> processChildren = new List<Expression>();

            TypeInformation info = GetTypeInfo(type);
            var members = info.Members;

            processChildren.Add(Expression.Call(documentParam, WriteHeaderMethod, Expression.Constant(info))); //document.WriteHeader(typeof(graph))
            foreach (var memberInfo in members)
            {
                switch (memberInfo.RawType)
                {
                    case FieldType.Int32:
                        processChildren.Add(Expression.Call(documentParam, WriteIntMethod, memberInfo.GetGetter(objectParam))); //document.WriteNumber(graph.<PropName>)
                        break;
                    case FieldType.String:
                        processChildren.Add(Expression.Call(documentParam, WriteStringMethod, memberInfo.GetGetter(objectParam))); //document.WriteNumber(graph.<PropName>)
                        break;
                    case FieldType.Object:
                        var x = GetComplexProcess(memberInfo, documentParam, objectParam);
                        processChildren.Add(x);
                        break;
                    default:
                        break;
                }
            }
            var processChildrenLambda =
                Expression.Lambda<Action<Document, T>>(Expression.Block(processChildren.ToArray()),
                    new[] { documentParam, objectParam });
            @delegate = processChildrenLambda.Compile();
            expression = processChildrenLambda;
        }

        private Expression GetComplexProcess(FieldInformation memberInfo, ParameterExpression documentParam,
            ParameterExpression objectParam)
        {
            var graphMember = memberInfo.GetGetter(objectParam);
            Expression setExpr;
            if (!memberInfo.Type.IsSealed)
            {
                setExpr = GetPolymorphicSetExpr(memberInfo, documentParam, graphMember);
            }
            else
            {
                setExpr = GetNonPolymorphicSetExpr(memberInfo, documentParam, graphMember);
            }
            if (!memberInfo.Type.IsValueType)
            {
                setExpr = Expression.Condition(
                    Expression.ReferenceNotEqual(graphMember, Expression.Constant(null, memberInfo.Type)), setExpr,
                    Expression.Call(documentParam,WriteNullMethod), typeof (void));
                                                        //if(graph.Member != null) 
                                                        //{
                                                        //  ...
                                                        //}
                                                        //else
                                                        //{
                                                        //document.WriteNull();
                                                        //}
            }
            return setExpr;
        }

        private Expression GetPolymorphicSetExpr(FieldInformation memberInfo, ParameterExpression documentParam,
            Expression graphMember)
        {
            var method1 = this.GetType()
                .GetMethod("GetPolymorphicClimbDelegate",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).MakeGenericMethod(memberInfo.Type);
            var polyExprGetter = Expression.Call(Expression.Constant(this), method1,
                Expression.Call(graphMember, new Func<Type>(new object().GetType).Method));
            var incovationMethod = typeof (Action<,>).MakeGenericType(typeof (Document), memberInfo.Type).GetMethod("Invoke");
            var polyExpr = Expression.Call(polyExprGetter, incovationMethod, documentParam, graphMember);
            return polyExpr;
        }

        private Expression GetNonPolymorphicSetExpr(FieldInformation memberInfo, ParameterExpression documentParam,
            Expression graphMember)
        {
            var method = this.GetType()
                .GetMethod("GetClimbExpression",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .MakeGenericMethod(memberInfo.Type);
            var childClimb = ((LambdaExpression) method.Invoke(this, new object[0]));

            var setExpr =
                new ParameterReplacer(childClimb.Parameters, new Expression[] {documentParam, graphMember}).Visit(
                    childClimb.Body);
            return setExpr;
        }

        private TypeInformation GetTypeInfo(Type type)
        {
            TypeInformation info;
            if (!mKnownTypes.TryGetValue(type, out info))
            {
                info = mKnownTypes[type] = new TypeInformation(type, mFieldProvider);
            }

            return info;
        }
    }

    internal class TypeInformation
    {
        private readonly byte[] mSeializedInfo;

        public TypeInformation(Type type, IFieldProvider provider)
        {
            TypeId = Guid.NewGuid();
            Type = type;
            Members = provider.ProvideMembers(type).ToList();
            mSeializedInfo = CreateSerializedInfo();
        }

        private TypeInformation(byte[] serializedInfo)
        {
            mSeializedInfo = serializedInfo;
        }

        internal static TypeInformation FromStream(Guid id, Stream stream, IFieldProvider provider)
        {
            var serializedInfo = GetTypeInfoDataFromStream(stream);
            MemoryStream serializedInfoStream = new MemoryStream(serializedInfo, sizeof(int), serializedInfo.Length - sizeof(int));
            var reader = new BinaryReader(serializedInfoStream);
            var info = new TypeInformation(serializedInfo);
            info.TypeId = id;
            var typeName = reader.ReadString();
            info.Type = Type.GetType(typeName);

            var membersLength = reader.ReadInt32();
            var members = new List<FieldInformation>(membersLength);
            for (int i = 0; i < membersLength; i++)
            {
                var memberName = reader.ReadString();
                var memberType = (FieldType)reader.ReadByte();
                members.Add(provider.GetFieldInformation(info, memberName, memberType));
            }
            info.Members = members;
            return info;
        }

        private static byte[] GetTypeInfoDataFromStream(Stream stream)
        {
            var lengthBuffer = new byte[sizeof(int)];
            stream.Read(lengthBuffer, 0, sizeof(int));
            var length = BitConverter.ToInt32(lengthBuffer, 0);
            var streamBuffer = new byte[sizeof(int) + length];
            Array.Copy(lengthBuffer, streamBuffer, sizeof(int));
            stream.Read(streamBuffer, sizeof(int), length);

            return streamBuffer;
        }

        private byte[] CreateSerializedInfo()
        {
            MemoryStream data = new MemoryStream();
            var lengthHeaderPosition = data.Position;
            var writer = new BinaryWriter(data);
            data.Seek(sizeof(int), SeekOrigin.Current);
            writer.Write(Type.AssemblyQualifiedName);
            writer.Write((int)Members.Count);
            foreach (var member in Members)
            {
                writer.Write(member.Name);
                writer.Write((byte)member.RawType);
            }
            writer.Flush();
            var dataLength = (int)(data.Length - (lengthHeaderPosition + sizeof(int)));
            data.Position = lengthHeaderPosition;
            data.Write(BitConverter.GetBytes(dataLength), 0, sizeof(int));
            return data.ToArray();
        }

        internal Guid TypeId { get; private set; }
        internal Type Type { get; set; }
        internal List<FieldInformation> Members { get; private set; }


        internal byte[] GetSerializedTypeInfo()
        {
            return mSeializedInfo;
        }

        public override string ToString()
        {
            return "TypeInformation:" + Type.FullName;
        }
    }

    //internal interface ISerializeExpressionProvider
}