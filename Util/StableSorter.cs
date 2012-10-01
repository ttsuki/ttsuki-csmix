using System;
using System.Collections.Generic;
using System.Text;

namespace Tsukikage.Util
{
    /// <summary>
    /// 安定ソートを提供します。
    /// </summary>
    public static class StableSorter
    {
        // パフォーマンスの都合で同じアルゴリズムが2つ書いてある。

        // 配列以外のコンテナ(試したのは List<T> だけ)に関しては、
        // どうやらそのまま[]を使ってソートするよりも、
        // 配列にコピーしてソートして戻した方が速いようなのでそうしてある。

        /// <summary>
        /// 安定なソート(マージソート)を実行する。
        /// 自動的に、最大 new T[target.Count]が2つ分のワークメモリが確保される。
        /// This method will do new T[target.Count] two times in maximum case.
        /// </summary>
        /// <typeparam name="T">Type 型</typeparam>
        /// <param name="target">配列など</param>
        /// <remarks>配列以外は、内部でで配列にコピーしてソートして戻す。</remarks>
        public static void StableSort<T>(ICollection<T> target)
            where T : IComparable<T>
        {
            T[] workingMemory = new T[target.Count];
            if (target is T[])
                StableSort((T[])target, workingMemory);
            else
            {
                T[] a = new T[target.Count];
                target.CopyTo(a, 0);
                StableSort<T>(a, workingMemory);
                target.Clear();
                for (int i = 0; i < a.Length; i++)
                    target.Add(a[i]);
            }
        }

        /// <summary>
        /// 安定なソート(マージソート)を実行する。
        /// 自動的に、最大 new T[target.Count]が2つ分のワークメモリが確保される。
        /// This method will do new T[target.Count] two times in maximum case.
        /// </summary>
        /// <typeparam name="T">型</typeparam>
        /// <param name="target">配列など</param>
        /// <param name="comparison">比較に使用するComparison&lt;T&gt;</param>
        /// <param name="workingMemory">ソートしたい配列と同じ長さ以上のワークメモリ</param>
        /// <remarks>配列以外は、内部でで配列にコピーしてソートして戻す。</remarks>
        public static void StableSort<T>(ICollection<T> target, Comparison<T> comparison)
        {
            T[] workingMemory = new T[target.Count];
            if (target is T[])
                StableSort((T[])target, comparison, workingMemory);
            else
            {
                T[] a = new T[target.Count];
                target.CopyTo(a, 0);
                StableSort<T>(a, comparison, workingMemory);
                target.Clear();
                for (int i = 0; i < a.Length; i++)
                    target.Add(a[i]);
            }
        }

        /// <summary>
        /// 安定なソート(マージソート)を実行する。
        /// 消費メモリとパフォーマンスにこだわる方はこちら。
        /// </summary>
        /// <typeparam name="T">型</typeparam>
        /// <param name="target">配列</param>
        /// <param name="workingMemory">ソートしたい配列と同じ長さ以上のワークメモリが要る。</param>
        public static void StableSort<T>(T[] target, T[] workingMemory)
            where T : IComparable<T>
        {
            int targLength = target.Length;
            if (targLength > workingMemory.Length)
                throw new ArgumentException("workingMemory.Length must be >= target.Length");

            T[] targ = target;
            T[] work = workingMemory;
            for (int n = 1; n < targLength; n <<= 1)
                for (int i = 0; ; i++)
                {
                    int s = n * i << 1;
                    if (s + n >= targLength) break;
                    int e = s + n * 2 < targLength ? s + n * 2 : targLength;
                    int i1 = s, e1 = i1 + n;
                    int i2 = e1, e2 = e;

                    int k = s;
                    while (i1 != e1 && i2 != e2)
                    {
                        // Compare object here.
                        bool comp = targ[i1].CompareTo(targ[i2]) <= 0;
                        work[k++] = comp ? targ[i1++] : targ[i2++];
                    }

                    while (i1 != e1) work[k++] = targ[i1++];
                    while (i2 != e2) work[k++] = targ[i2++];

                    for (int j = s; j < e; j++)
                        targ[j] = work[j];
                }
        }


        /// <summary>
        /// 安定なソート(マージソート)を実行する。
        /// 消費メモリとパフォーマンスにこだわる方はこちら。
        /// </summary>
        /// <typeparam name="T">型</typeparam>
        /// <param name="target">配列</param>
        /// <param name="comparison">比較に使用するComparison&lt;T&gt;</param>
        /// <param name="workingMemory">ソートしたい配列と同じ長さ以上のワークメモリが要る。</param>
        public static void StableSort<T>(T[] target, Comparison<T> comparison, T[] workingMemory)
        {
            int targLength = target.Length;
            if (targLength > workingMemory.Length)
                throw new ArgumentException("workingMemory.Length must be >= target.Length");

            T[] targ = (T[])target;
            T[] work = workingMemory;
            for (int n = 1; n < targLength; n <<= 1)
                for (int i = 0; ; i++)
                {
                    int s = n * i << 1;
                    if (s + n >= targLength) break;
                    int e = s + n * 2 < targLength ? s + n * 2 : targLength;
                    int i1 = s, e1 = i1 + n;
                    int i2 = e1, e2 = e;

                    int k = s;
                    while (i1 != e1 && i2 != e2)
                    {
                        // Compare object here.
                        bool comp = comparison(targ[i1], targ[i2]) <= 0;
                        work[k++] = comp ? targ[i1++] : targ[i2++];
                    }

                    while (i1 != e1) work[k++] = targ[i1++];
                    while (i2 != e2) work[k++] = targ[i2++];

                    for (int j = s; j < e; j++)
                        targ[j] = work[j];
                }
        }

#if BENCHiMARK
        // public
        class Benchmark
        {
            struct STRUCT : IComparable<STRUCT>
            {
                public int val;
                public static implicit operator STRUCT(int v) { return new STRUCT() { val = v }; }
                public int CompareTo(STRUCT other) { return val - other.val; }
            }

            class CLASS_A : IComparable<CLASS_A>
            {
                public int val;
                public int CompareTo(CLASS_A other) { return val - other.val; }
                public static implicit operator CLASS_A(int v) { return new CLASS_A() { val = v }; }
            }

            class CLASS_B : CLASS_A, IComparable<CLASS_B>
            {
                public static implicit operator CLASS_B(int v) { return new CLASS_B() { val = v }; }
                public int CompareTo(CLASS_B other) { return val - other.val; }
            }

            public static void DoIt()
            {
                System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
                sw.Start();
                Random random = new Random(0);

                var work = new CLASS_B[2000];
                for (var i = 0; i < 300; i++)
                {
                    Console.WriteLine(i + "Init...");
                    var array1 = new CLASS_B[1000 + random.Next(1000)];
                    for (var j = 0; j < array1.Length; j++)
                        array1[j] = random.Next(10000);
                    List<CLASS_B> list1 = new List<CLASS_B>(array1);

                    var array2 = (CLASS_B[])array1.Clone();

                    Console.WriteLine(i + "         StableSort...");
                    Tsukikage.Util.StableSorter.StableSort(list1);
                    Console.WriteLine(i + "                         Array.Sort...");
                    Array.Sort((CLASS_A[])array2);

                    Console.WriteLine(i + "                                        Comp...");
                    for (var j = 0; j < array1.Length; j++)
                        System.Diagnostics.Debug.Assert(array1[j].val == array2[j].val);
                }

                Console.WriteLine(sw.ElapsedMilliseconds);
            }
        }
#endif
    }
}
