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
        public static int GetMostRightOneIndex(long n)
        {
            return MostRightOneIndex64.GetMostRightOneIndex(n);
        }
    }
}
