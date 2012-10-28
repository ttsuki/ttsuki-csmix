using System;
using System.Collections.Generic;
using System.Text;

namespace Tsukikage.GameSDK.Util
{
    /// <summary>
    /// ハッカーのたのしみ - Hacker's delight.
    /// </summary>
    public static class BinaryUtil
    {
        static class MostRightOneIndex64
        {
            const ulong salt = 0x03F566ED27179461UL;
            static int[] table = new int[64];

            static MostRightOneIndex64()
            {
                ulong s = salt;
                for (int i = 0; i < 64; i++)
                {
                    table[s >> 58] = i;
                    s <<= 1;
                }
            }

            public static int GetMostRightOneIndex(long n)
            {
                unchecked
                {
                    if (n == 0) return -1;
                    return table[(((ulong)(n & -n) * salt) >> 58)];
                }
            }
        }

        static class PopulationCount
        {
            public static int GetPopulationCount(uint n)
            {
                n = (n & 0x55555555) + ((n >> 1) & 0x55555555);
                n = (n & 0x33333333) + ((n >> 2) & 0x33333333);
                n = (n & 0x0F0F0F0F) + ((n >> 4) & 0x0F0F0F0F);
                n = (n & 0x00FF00FF) + ((n >> 8) & 0x00FF00FF);
                n = (n & 0x0000FFFF) + ((n >> 16) & 0x0000FFFF);
                return (int)n;
            }

            public static int GetPopulationCount(ulong n)
            {
                n = (n & 0x5555555555555555) + ((n >> 1) & 0x5555555555555555);
                n = (n & 0x3333333333333333) + ((n >> 2) & 0x3333333333333333);
                n = (n & 0x0F0F0F0F0F0F0F0F) + ((n >> 4) & 0x0F0F0F0F0F0F0F0F);
                n = (n & 0x00FF00FF00FF00FF) + ((n >> 8) & 0x00FF00FF00FF00FF);
                n = (n & 0x0000FFFF0000FFFF) + ((n >> 16) & 0x0000FFFF0000FFFF);
                n = (n & 0x00000000FFFFFFFF) + ((n >> 32) & 0x00000000FFFFFFFF);
                return (int)n;
            }
        }

        static class NumberOfLeadingZeros
        {
            public static int GetNumberOfLeadingZeros(uint n)
            {
                n = n | n >> 1;
                n = n | n >> 2;
                n = n | n >> 4;
                n = n | n >> 8;
                n = n | n >> 16;
                return PopulationCount.GetPopulationCount(~n);
            }

            public static int GetPopulationCount(ulong n)
            {
                n = n | n >> 1;
                n = n | n >> 2;
                n = n | n >> 4;
                n = n | n >> 8;
                n = n | n >> 16;
                n = n | n >> 32;
                return PopulationCount.GetPopulationCount(~n);
            }
        }

        static class CeilingToPowerOf2
        {
            public static int CeilToPowerOf2(int n)
            {
                n = n - 1;
                n = n | n >> 1;
                n = n | n >> 2;
                n = n | n >> 4;
                n = n | n >> 8;
                n = n | n >> 16;
                return n + 1;
            }

            public static long CeilToPowerOf2(long n)
            {
                n = n - 1;
                n = n | n >> 1;
                n = n | n >> 2;
                n = n | n >> 4;
                n = n | n >> 8;
                n = n | n >> 16;
                n = n | n >> 32;
                return n + 1;
            }
        }

        /// <summary>
        /// 最右の1を残して他のビットを消す。
        /// </summary>
        /// <param name="n">最右の1を残して他のビットを消す</param>
        /// <returns></returns>
        public static long GetMostRightOne(long n) { return (n & -n); }

        /// <summary>
        /// 最右の1を残して他のビットを消す。
        /// </summary>
        /// <param name="n">最右の1を残して他のビットを消す</param>
        /// <returns></returns>
        public static int GetMostRightOne(int n) { return (n & -n); }

        /// <summary>
        /// 最右ビットのindexを取り出す
        /// </summary>
        /// <param name="n">最右の1のindexを取り出す</param>
        /// <returns></returns>
        public static int GetMostRightOneIndex(long n) { return MostRightOneIndex64.GetMostRightOneIndex(n); }

        /// <summary>
        /// 立ってるビットの数を数える
        /// </summary>
        /// <param name="n">立ってるビットの数を数える</param>
        /// <returns></returns>
        public static long GetPopulationCount(int n) { return PopulationCount.GetPopulationCount((uint)n); }

        /// <summary>
        /// 立ってるビットの数を数える
        /// </summary>
        /// <param name="n">立ってるビットの数を数える</param>
        /// <returns></returns>
        public static long GetPopulationCount(long n) { return PopulationCount.GetPopulationCount((ulong)n); }

        /// <summary>
        /// 2のn乗までCeilする
        /// </summary>
        /// <param name="n">立ってるビットの数を数える</param>
        /// <returns></returns>
        public static long CeilToPowerOf2(int n) { return CeilingToPowerOf2.CeilToPowerOf2(n); }

        /// <summary>
        /// 立ってるビットの数を数える
        /// </summary>
        /// <param name="n">立ってるビットの数を数える</param>
        /// <returns></returns>
        public static long CeilToPowerOf2(long n) { return CeilingToPowerOf2.CeilToPowerOf2(n); }
    }
}
