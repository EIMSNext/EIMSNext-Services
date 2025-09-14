using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace EIMSNext.Common
{
    public static class ObjectConvert
    {
        public static T CastTo<S, T>(this S s)
        {
            return CastExp<S, T>().Compile().Invoke(s);
        }

        public static void CopyTo<S, T>(this S s, T t)
        {
            CopyToExp<S, T>().Invoke(s, t);
        }

        public static S Proj<S, P>(this S s, Expression<Func<S, P>> keySelector, P val)
        {
            return ProjExp<S, P>(keySelector).Compile().Invoke(s, val);
        }

        #region Cast Expression

        private static ConcurrentDictionary <TypePair, Expression> CastCache = new ConcurrentDictionary<TypePair, Expression>();

        public static Expression<Func<S, T>> CastExp<S, T>()
        {
            Type source = typeof(S);
            Type target = typeof(T);

            var key = new TypePair { SourceType = source, TargetType = target };

            if (CastCache.TryGetValue(key, out Expression? exp) && exp is Expression<Func<S, T>>)
            {
                return (Expression<Func<S, T>>)exp;
            }
            else
            {
                var parameter = Expression.Parameter(source, "x"); // x =>
                var targetProps = target.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite);

                // 创建成员绑定表达式集合
                var bindings = targetProps
                    .Select(targetProp =>
                    {
                        var sourceProp = source.GetProperty(targetProp.Name, BindingFlags.Public | BindingFlags.Instance);

                        if (sourceProp == null) return null;

                        // 创建属性访问表达式：x.Property
                        var sourceAccess = Expression.Property(parameter, sourceProp);

                        // 创建成员绑定表达式：Property = x.Property
                        return Expression.Bind(targetProp, sourceAccess);
                    })
                    .Where(b => b != null)
                    .ToList();

                // 创建对象初始化表达式
                var newExpr = Expression.New(target); // new T()
                var memberInit = Expression.MemberInit(newExpr, bindings!); // { Id = x.Id, Name = x.Name }

                // 构建Lambda表达式
                var lambda = Expression.Lambda<Func<S, T>>(memberInit, parameter);

                CastCache.TryAdd(key, lambda);

                return lambda;
            }
        }

        private struct TypePair
        {
            public Type SourceType { get; set; }
            public Type TargetType { get; set; }
        }

        #endregion

        #region Proj Expression

        private static ConcurrentDictionary<TypePropPair, Expression> ProjCache = new ConcurrentDictionary<TypePropPair, Expression>();

        public static Expression<Func<S, P, S>> ProjExp<S, P>(Expression<Func<S, P>> keySelector)
        {
            Type source = typeof(S);
            Type projType = typeof(P);
            var pName = (keySelector.Body as MemberExpression)?.Member?.Name;

            var key = new TypePropPair { SourceType = source, SourcePropName = pName!, ProjectType = projType, ProjectPropName = pName! };

            if (ProjCache.TryGetValue(key, out Expression? exp) && exp is Expression<Func<S, P, S>>)
            {
                return (Expression<Func<S, P, S>>)exp;
            }
            else
            {
                var parameter = Expression.Parameter(source, "x"); // x =>
                var pValue = Expression.Parameter(projType, "y");

                var targetProps = source.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite);

                // 创建成员绑定表达式集合
                var bindings = targetProps
                    .Select(targetProp =>
                    {
                        if (targetProp.Name.Equals(pName, StringComparison.OrdinalIgnoreCase))
                        {
                            // 创建成员绑定表达式：Property = y
                            return Expression.Bind(targetProp, pValue);
                        }
                        else
                        {
                            // 创建属性访问表达式：x.Property
                            var sourceAccess = Expression.Property(parameter, targetProp);

                            // 创建成员绑定表达式：Property = x.Property
                            return Expression.Bind(targetProp, sourceAccess);
                        }
                    })
                    .Where(b => b != null)
                    .ToList();

                // 创建对象初始化表达式
                var newExpr = Expression.New(source); // new T()
                var memberInit = Expression.MemberInit(newExpr, bindings!); // { Id = x.Id, Name = x.Name }

                // 构建Lambda表达式
                var lambda = Expression.Lambda<Func<S, P, S>>(memberInit, parameter, pValue);

                ProjCache.TryAdd(key, lambda);

                return lambda;
            }
        }

        private struct TypePropPair
        {
            public Type SourceType { get; set; }
            public string SourcePropName { get; set; }
            public Type ProjectType { get; set; }
            public string ProjectPropName { get; set; }
        }

        public static Expression<Func<S, P, S>> ProjExp<S, P, TProp>(Expression<Func<S, TProp>> srcKeySelector, Expression<Func<P, TProp>> projKeySelector)
        {
            Type source = typeof(S);
            Type projType = typeof(P);
            var srcPropName = (srcKeySelector.Body as MemberExpression)?.Member?.Name;
            var projPropName = (projKeySelector.Body as MemberExpression)?.Member?.Name;

            var key = new TypePropPair { SourceType = source, SourcePropName = srcPropName!, ProjectType = projType, ProjectPropName = projPropName! };

            if (ProjCache.TryGetValue(key, out Expression? exp) && exp is Expression<Func<S, P, S>>)
            {
                return (Expression<Func<S, P, S>>)exp;
            }
            else
            {
                var parameter = Expression.Parameter(source, "x"); // x =>
                var pValue = Expression.Parameter(projType, "y");

                var targetProps = source.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite);
                var projProp = projType.GetProperty(projPropName!, BindingFlags.NonPublic | BindingFlags.Instance);

                // 创建成员绑定表达式集合
                var bindings = targetProps
                    .Select(targetProp =>
                    {
                        if (targetProp.Name.Equals(srcPropName, StringComparison.OrdinalIgnoreCase))
                        {
                            // 创建属性访问表达式：y.Property
                            var sourceAccess = Expression.Property(pValue, projProp!);

                            // 创建成员绑定表达式：Property = y.Property
                            return Expression.Bind(targetProp, sourceAccess);
                        }
                        else
                        {
                            // 创建属性访问表达式：x.Property
                            var sourceAccess = Expression.Property(parameter, targetProp);

                            // 创建成员绑定表达式：Property = x.Property
                            return Expression.Bind(targetProp, sourceAccess);
                        }
                    })
                    .Where(b => b != null)
                    .ToList();

                // 创建对象初始化表达式
                var newExpr = Expression.New(source); // new T()
                var memberInit = Expression.MemberInit(newExpr, bindings!); // { Id = x.Id, Name = x.Name }

                // 构建Lambda表达式
                var lambda = Expression.Lambda<Func<S, P, S>>(memberInit, parameter, pValue);

                ProjCache.TryAdd(key, lambda);

                return lambda;
            }
        }

        #endregion

        #region Copy Expression

        private static ConcurrentDictionary<TypePair, Delegate> CopyToCache = new ConcurrentDictionary<TypePair, Delegate>();

        private static Action<S, T> CopyToExp<S, T>()
        {
            var source = typeof(S);
            var target = typeof(T);
            var key = new TypePair { SourceType = source, TargetType = target };

            if (CopyToCache.TryGetValue(key, out Delegate? exp) && exp is Action<S, T>)
            {
                return (Action<S, T>)exp;
            }
            else
            {
                var sourceParam = Expression.Parameter(source, "s");
                var targetParam = Expression.Parameter(target, "t");

                var assignments = new List<Expression>();

                // 遍历目标类型属性，匹配同名可写属性
                foreach (var targetProp in target.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!targetProp.CanWrite) continue;

                    var sourceProp = source.GetProperty(targetProp.Name,
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

                    if (sourceProp == null || !sourceProp.CanRead) continue;

                    // 生成属性赋值表达式
                    var sourcePropExpr = Expression.Property(sourceParam, sourceProp);
                    var targetPropExpr = Expression.Property(targetParam, targetProp);
                    var assignExpr = Expression.Assign(targetPropExpr, sourcePropExpr);
                    assignments.Add(assignExpr);
                }

                // 构建 Lambda 表达式
                var block = Expression.Block(assignments);
                var lambda = Expression.Lambda<Action<S, T>>(block, sourceParam, targetParam);
                var action = lambda.Compile();

                CopyToCache.TryAdd(key, action);

                return action;
            }
        }

        #endregion
    }
}
