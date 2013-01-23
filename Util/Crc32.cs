using System;
using System.Collections.Generic;
using System.Text;

namespace Tsukikage.Util
{
    /// <summary>
    /// CRC32算出機能を提供します
    /// </summary>
    public class Crc32
    {
        private static readonly uint[] table;

        static Crc32()
        {
            table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int j = 0; j < 8; j++)
                    if ((c & 1) != 0)
                        c = c >> 1 ^ 0xEDB88320;
                    else
                        c = c >> 1;

                table[i] = c;
            }
        }

        public const UInt32 InitialValue = 0x00000000u;

        /// <summary>
        /// CRC32を計算する
        /// </summary>
        /// <param name="crc">アップデート対象のCRC値。初回時はCrc32.InitialValueを指定するとよい。</param>
        /// <param name="data">算出対象のデータ</param>
        /// <param name="offset">算出を開始する位置</param>
        /// <param name="count">算出するバイト数</param>
        /// <returns>新しいCRC32値</returns>
        public static UInt32 CalcCRC32(UInt32 crc, byte[] data, int offset, int count)
        {
            crc ^= 0xFFFFFFFF;
            for (int i = 0; i < data.Length; i++)
                crc = crc >> 8 ^ table[(byte)(crc ^ data[i])];
            return crc ^ 0xFFFFFFFF;
        }

        /// <summary>
        /// CRC32を計算する
        /// </summary>
        /// <param name="crc">アップデート対象のCRC値。初回時はCrc32.InitialValueを指定するとよい。</param>
        /// <param name="data">算出対象のデータ</param>
        /// <returns>新しいCRC32値</returns>
        public static UInt32 CalcCRC32(UInt32 crc, byte[] data)
        {
            return CalcCRC32(crc, data, 0, data.Length);
        }

        UInt32 crc;

        /// <summary>
        /// 現在のCRC32値を取得します。
        /// </summary>
        public UInt32 Current { get { return crc; } }

        /// <summary>
        /// Crc32を分割計算するための新しいインスタンスを取得します。
        /// </summary>
        public Crc32() { crc = InitialValue; }

        /// <summary>
        /// 指定したデータでCRC32の値を更新します。
        /// </summary>
        /// <param name="data">追加計算するデータ</param>
        public void Update(byte[] data) { crc = CalcCRC32(crc, data); }

        /// <summary>
        /// 指定したデータでCRC32の値を更新します。
        /// </summary>
        /// <param name="data">追加計算するデータ</param>
        /// <param name="offset">算出を開始する位置</param>
        /// <param name="count">算出するバイト数</param>
        public void Update(byte[] data, int offset, int count)
        {
            crc = CalcCRC32(crc, data, offset, count);
        }
    }

    /// <summary>
    /// HashAlgorithmのCrc32実装 (未テスト)
    /// </summary>
    public class Crc32Hash : System.Security.Cryptography.HashAlgorithm
    {
        Crc32 crc32;

        protected override void HashCore(byte[] array, int ibStart, int cbSize)
        {
            crc32.Update(array, ibStart, cbSize);
        }

        protected override byte[] HashFinal()
        {
            return BitConverter.GetBytes(crc32.Current);
        }

        public override void Initialize()
        {
            crc32 = new Crc32();
            HashSizeValue = 32;
            HashValue = new byte[4];
        }
    }
}
