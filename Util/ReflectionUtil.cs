using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;

namespace Tsukikage.Util
{
    /// <summary>
    /// リフレクションユーティリティ
    /// 型情報をつかってあれやこれや。
    /// CSharmCompiler のお供に。
    /// </summary>
    public static class ReflectionUtil
    {
        /// <summary>
        /// 指定したnamespace内のすべての型に対してactionしちゃう。
        /// このメソッドの呼び出し元アセンブリに含まれる型だけが対象。
        /// Apply <paramref name="action"/> for each type in the <paramref name="targetNamespace"/>.
        /// </summary>
        /// <param name="targetNamespace">検索対象のnamespace</param>
        /// <param name="action">アクション</param>
        public static void ForEachType(string targetNamespace, Action<Type> action)
        {
            foreach (Type t in Assembly.GetCallingAssembly().GetTypes())
                if (t.FullName.StartsWith(targetNamespace))
                    action(t);
        }

        /// <summary>
        /// 指定した型を持つ指定したオブジェクトのinstanceフィールドの値に対してactionしちゃう。
        /// Apply <paramref name="action"/> for each instance field `value' which the <paramref name="target"/> instance has.
        /// </summary>
        /// <typeparam name="TField">フィールドの型</typeparam>
        /// <param name="target">対象のinstanceフィールドを持つインスタンス</param>
        /// <param name="action">やること</param>
        /// <example>Texture 型について、全部のフィールドの Load() を呼びたい！　とかね。</example>
        public static void ForEachInstanceFieldValue<TField>(object target, Action<TField> action)
        {
            foreach (FieldInfo fi in target.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (fi.FieldType == typeof(TField) && fi.GetValue(null) != null)
                    action((TField)fi.GetValue(target));
            }
        }

        /// <summary>
        /// 指定した型のstaticフィールドの値に対してactionします。
        /// Apply <paramref name="action"/> for each static field `value' which the <paramref name="target"/> type has.
        /// </summary>
        /// <typeparam name="TField">フィールドの型</typeparam>
        /// <param name="target">対象のstaticフィールドを持つ型</param>
        /// <param name="action">やること</param>
        /// <example>たとえば、staticなリソースを定義している型に対して、全部のstaticフィールドリソースをLoad()したいとかね。</example>
        public static void ForEachStaticFieldValue<TField>(Type target, Action<TField> action)
        {
            foreach (FieldInfo fi in target.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (fi.FieldType == typeof(TField) && fi.GetValue(null) != null)
                    action((TField)fi.GetValue(null));
            }
        }

        /// <summary>
        /// 指定した型の指定した名前を持つstaticフィールドの「値」を得る。
        /// Get `value' of static field named <paramref name="name"/> which the <paramref name="target"/> type has.
        /// </summary>
        /// <typeparam name="TField">フィールドの型</typeparam>
        /// <param name="target">対象のフィールドを持つ型</param>
        /// <param name="fieledName">フィールドの名前</param>
        /// <returns>フィールドの値</returns>
        public static TField GetFieldValue<TField>(Type target, string fieledName)
        {
            FieldInfo fi = target.GetField(fieledName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (fi != null && fi.Name == fieledName && fi.FieldType == typeof(TField) && fi.GetValue(null) != null)
                return (TField)fi.GetValue(null);

            return default(TField);
        }

        /// <summary>
        /// 指定した型の指定した名前を持つinstanceフィールドの「値」を得る。
        /// Get `value' of instance field named <paramref name="name"/> which the <paramref name="target"/> object has.
        /// </summary>
        /// <typeparam name="TField">フィールドの型</typeparam>
        /// <param name="target">値</param>
        /// <param name="fieldName">フィールドの名前</param>
        /// <returns>フィールドの値</returns>
        public static TField GetFieldValue<TField>(object target, string fieldName)
        {
            FieldInfo fi = target.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (fi != null && fi.Name == fieldName && fi.FieldType == typeof(TField) && fi.GetValue(null) != null)
                return (TField)fi.GetValue(target);
            return default(TField);
        }

        /// <summary>
        /// 指定した型の指定した名前を持つstaticメソッドをデリゲートとして取得する。
        /// Get a delegate to the static method named <paramref name="methodName"/> which the <paramref name="target"/> type has.
        /// </summary>
        /// <typeparam name="TDelegate">delegateの型。いわゆる method signature。</typeparam>
        /// <param name="type">対象とする型</param>
        /// <param name="methodName">欲しいメソッドの名前</param>
        /// <returns>TDelegate</returns>
        /// <remarks>プライベートメソッドとかも出てきます。</remarks>
        public static TDelegate GetMethod<TDelegate>(Type type, string methodName)
            where TDelegate : class
        {
            MethodInfo mi = type.GetMethod(methodName,
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static, null, CallingConventions.Standard,
                Array.ConvertAll(typeof(TDelegate).GetMethod("Invoke").GetParameters(), pi => pi.ParameterType), null);
            if (mi == null) return null; // 定義されてない
            Delegate methodDelegate = Delegate.CreateDelegate(typeof(TDelegate), mi, false);
            TDelegate tDelegate = methodDelegate as TDelegate; // as ならいいのね……？
            if (methodDelegate != null && tDelegate == null)
                throw new InvalidCastException(); // ありえんはずだが。
            
            return tDelegate;
        }

        /// <summary>
        /// 指定した型の指定した名前を持つinstanceメソッドをデリゲートとして取得する。
        /// Get a delegate to the instance method named <paramref name="methodName"/> which the <paramref name="target"/> object has.
        /// </summary>
        /// <typeparam name="TDelegate">delegateの型。いわゆる method signature。</typeparam>
        /// <param name="target">対象とするinstance</param>
        /// <param name="methodName">欲しいメソッドの名前</param>
        /// <returns>TDelegate</returns>
        /// <remarks>プライベートメソッドとかも出てきます。</remarks>
        public static TDelegate GetMethod<TDelegate>(object target, string methodName)
            where TDelegate : class
        {
            MethodInfo mi = target.GetType().GetMethod(methodName,
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance, null, CallingConventions.HasThis,
                Array.ConvertAll(typeof(TDelegate).GetMethod("Invoke").GetParameters(), pi => pi.ParameterType), null);
            if (mi == null) return null; // 定義されてない
            Delegate methodDelegate = Delegate.CreateDelegate(typeof(TDelegate), target, mi, false);
            TDelegate tDelegate = methodDelegate as TDelegate; // as ならいいのね……？
            if (methodDelegate != null && tDelegate == null)
                throw new InvalidCastException(); // ありえんはずだが。

            return tDelegate;
        }

        /// <summary>
        /// 指定した型名を持つインスタンスを生成して返す。
        /// 引数なしコンストラクタが呼ばれます。
        /// Create a instance from <paramref name="typeName"/> defined in <paramref name="assembly"/> by calling no params ctor().
        /// </summary>
        /// <typeparam name="T">型</typeparam>
        /// <param name="assembly">アセンブリ</param>
        /// <param name="typeName">型の名前</param>
        /// <returns></returns>
        /// <example>
        /// 彼岸の向こうの<paramref name="assembly"/>が、
        /// こっちの<typeparamref name="T"/>を継承して何かしら実装しているはずだ。
        /// というときに使う。要するにプラグインとか実装するとき向け。
        /// </example>
        public static T CreateInstance<T>(Assembly assembly, string typeName)
        {
            if (assembly == null)
                return default(T);

            return (T)Activator.CreateInstance(assembly.GetType(typeName, true));
        }
    }
}
