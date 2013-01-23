using System;
using System.Collections.Generic;
using System.Text;

namespace Tsukikage.Util
{
    /// <summary>
    /// リングインデクサ。
    /// 配列に負の値や大きな値を指定したとき、自動的に剰余を取ってアクセスする。
    /// 要するにi++のみで、ずっとぐるぐる回れる配列。
    /// </summary>
    /// <typeparam name="T">対象配列の型</typeparam>
    public class RingIndexer<T>
    {
        T[] array;

        public T[] BaseArray { get { return array; } }

        /// <summary>
        /// リングインデクサ。要するにぐるぐる回れる配列
        /// </summary>
        public RingIndexer(T[] array) { this.array = array; }

        /// <summary>
        /// 配列アクセス。<paramref name="index"/>が負の値や配列の範囲を超えて大きい場合もwrapしてくれる。
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public T this[int index]
        {
            get
            {
                if (index < 0) index -= (index / array.Length - 1) * array.Length;
                return array[index % array.Length];
            }
            set
            {
                if (index < 0) index -= (index / array.Length - 1) * array.Length;
                array[index % array.Length] = value;
            }
        }

        /// <summary>
        /// 一周の長さ
        /// </summary>
        public int Length { get { return array.Length; } }
    }
}
