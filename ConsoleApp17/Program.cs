using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

using BbCAT.Domain.Objects.Courses;

namespace ConsoleApp17
{
    public delegate T ObjectActivator<T>(params object[] args);

    public class Program
    {


        static void Main(string[] args)
        {
            var repeat = 100;
            var c = new Course();
            var valuesA = new List<ICourse>();
            var valuesB = new List<ITest>();
            var valuesC = new List<ICourse>();
            var valuesD = new List<ICourse>();

            Action foundTypeAndCreate = () => valuesA.Add(BuildFind<ICourse>());
               
            Action createDirectlyWithNew = () => valuesB.Add(BuildDirectly());
                
            Action foundTypeCacheAndCreate = () => valuesC.Add(BuildFindCache<ICourse>());

            Action foundLambda = () => valuesD.Add(BuildFindCacheLambda<ICourse>());

            GetType<ICourse>();
            for (int i = 0; i <5; i++)
            {
                Measure(nameof(foundLambda), repeat, foundLambda);
                Measure(nameof(foundTypeCacheAndCreate), repeat, foundTypeCacheAndCreate);
                Measure(nameof(createDirectlyWithNew), repeat, createDirectlyWithNew);
                Measure(nameof(foundTypeAndCreate), repeat, foundTypeAndCreate);

                //repeat = (i+1) * 1000;
                Console.WriteLine();
            }


            Console.ReadLine();
        }
        private static void Measure(string name, int repeat, Action action)
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();

            for(int i = 0; i< repeat; i++)
            {
                action();
            }
            watch.Stop();

            Console.WriteLine("{0,-35} {1,5} ticks / {3,6} ms,  {2} ticks per item ({4} ms)", name, watch.ElapsedTicks, watch.ElapsedTicks / repeat, watch.ElapsedMilliseconds, watch.ElapsedMilliseconds/ repeat);
        }


        private static Type GetType<T>()
        {
            var assemblyNameLocal = "BbCAT.Domain";

            string assemblyQualifiedTypeName = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}.{1}, {2}",
                    typeof(T).Namespace,
                    typeof(T).Name.Substring(1),
                    assemblyNameLocal);

            var assemblyName = assemblyQualifiedTypeName.Split(',')[1].Trim();
            var appDomain = AppDomain.CurrentDomain;
            var assembly = appDomain.GetAssemblies().FirstOrDefault(a => a.FullName.Split(',')[0].Trim() == assemblyName);

            var sb = new StringBuilder(assemblyQualifiedTypeName);

            if (assemblyQualifiedTypeName.Contains("{"))
            {
                var idx = assemblyQualifiedTypeName.IndexOf('{');
                sb.Remove(idx, assemblyQualifiedTypeName.Length - idx);
                sb.Append("`1");
            }

            assemblyQualifiedTypeName = sb.ToString();
            var assemblyTypes = assembly.GetTypes();

            var found = assembly.GetTypes()
                          .First(
                              t =>
                              t.AssemblyQualifiedName.StartsWith(
                                  assemblyQualifiedTypeName, StringComparison.OrdinalIgnoreCase));


            return found;
        }

        public static T BuildFind<T>(params object[] initializationData)
        {
            Type type = GetType<T>();

            return (T)Activator.CreateInstance(type, initializationData);

        }

        static Dictionary<Type, Type> types = new Dictionary<Type, Type>();
        static Dictionary<Type, object> typeInvokes = new Dictionary<Type, object>();
        public static T BuildFindCache<T>(params object[] initializationData)
        {
            Type type = null;
            var keyType = typeof(T);

            if (types.ContainsKey(keyType))
            {
                type = types[keyType];
            }
            else
            {
                type = GetType<T>();
                types.Add(keyType, type);
            }

            return (T)Activator.CreateInstance(type, initializationData);
        }


        public static T BuildFindCacheLambda<T>(params object[] initializationData)
        {
            
            var keyType = typeof(T);
            ObjectActivator<T> activator = null;

            if (typeInvokes.ContainsKey(keyType))
            {
                activator = typeInvokes[keyType] as ObjectActivator<T>;
            }
            else
            {

                var type = GetType<T>();
                var ctor = type.GetConstructors().First();

                activator = GetActivator<T>(ctor);

                typeInvokes.Add(keyType, activator);
            }


            return activator(initializationData);
        }


        private static ITest BuildDirectly()
        {
            return new Test();
        }

        // https://ayende.com/blog/3167/creating-objects-perf-implications
        // https://rogerjohansson.blog/2008/02/28/linq-expressions-creating-objects/
        // https://vagifabilov.wordpress.com/2010/04/02/dont-use-activator-createinstance-or-constructorinfo-invoke-use-compiled-lambda-expressions/

        public static ObjectActivator<T> GetActivator<T>(ConstructorInfo ctor)
        {
            Type type = ctor.DeclaringType;
            ParameterInfo[] paramsInfo = ctor.GetParameters();

            //create a single param of type object[]
            ParameterExpression param =  Expression.Parameter(typeof(object[]), "args");

            Expression[] argsExp = new Expression[paramsInfo.Length];

            //pick each arg from the params array 
            //and create a typed expression of them
            for (int i = 0; i < paramsInfo.Length; i++)
            {
                Expression index = Expression.Constant(i);
                Type paramType = paramsInfo[i].ParameterType;

                Expression paramAccessorExp =
                    Expression.ArrayIndex(param, index);

                Expression paramCastExp =
                    Expression.Convert(paramAccessorExp, paramType);

                argsExp[i] = paramCastExp;
            }

            //make a NewExpression that calls the
            //ctor with the args we just created
            NewExpression newExp = Expression.New(ctor, argsExp);

            //create a lambda with the New
            //Expression as body and our param object[] as arg
            LambdaExpression lambda =
                Expression.Lambda(typeof(ObjectActivator<T>), newExp, param);

            //compile it
            ObjectActivator<T> compiled = (ObjectActivator<T>)lambda.Compile();
            return compiled;
        }
    }


    public interface ITest
    {
        int Number { get; set; }
    }


    public class Test : ITest
    {
        public int Number { get; set; }
    }
}
