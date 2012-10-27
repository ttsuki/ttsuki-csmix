using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Tsukikage.WindowsUtil
{
    /// <summary>
    /// フォントローダー
    /// </summary>
    public static class FontFileLoader
    {
        /// <summary>
        /// 読み込み済みフォントのHGLOBAL
        /// </summary>
        static List<IntPtr> LoadedMemFonts = new List<IntPtr>();

        /// <summary>
        /// TTFファイルなどのフォントファイルを読み込みます。
        /// 読み込んだフォントが使えるようになります。
        /// </summary>
        /// <param name="path">ファイル名</param>
        /// <remarks>
        /// .NETの<c>System.Drawing.Font</c>は、これで読み込んだフォントを作れないみたい。
        /// Win32 APIのCreateFontとか、SlimDX.Direct3D.Fontだと使えるみたい。
        /// </remarks>
        public static IntPtr LoadFontFile(string path)
        {
            using (System.IO.Stream stream = System.IO.File.OpenRead(path))
            {
                byte[] fontImage = new byte[stream.Length];
                stream.Read(fontImage, 0, fontImage.Length);
                return LoadFontFromMemory(fontImage);
            }
        }

        /// <summary>
        /// TTFファイルなどのフォントファイルを読み込みます。
        /// 読み込んだフォントが使えるようになります。
        /// </summary>
        /// <param name="fontImage">フォントファイルの内容を格納したメモリ</param>
        /// <remarks>
        /// .NETの<c>System.Drawing.Font</c>は、これで読み込んだフォントを作れないみたい。
        /// Win32 APIのCreateFontとか、SlimDX.Direct3D.Fontだと使えるみたい。
        /// </remarks>
        public static IntPtr LoadFontFromMemory(byte[] fontImage)
        {
            IntPtr fontHandle = Win32Font.LoadFontMem(fontImage);
            if (fontHandle == IntPtr.Zero)
                throw new ArgumentException("FontLoader: フォントの追加に失敗しました。フォントが有効ではありません。");
            LoadedMemFonts.Add(fontHandle);
            return fontHandle;
        }

        /// <summary>
        /// 読み込んだフォントファイルを解放します。
        /// </summary>
        /// <param name="fontHandle"></param>
        public static void UnloadFontFile(IntPtr fontHandle)
        {
            Win32Font.UnloadFontMem(fontHandle);
            LoadedMemFonts.Remove(fontHandle);
        }

        /// <summary>
        /// 読み込んだすべてのフォントを解放します。
        /// </summary>
        public static void UnloadAllLoadedFontFiles()
        {
            foreach (IntPtr handle in LoadedMemFonts)
                Win32Font.UnloadFontMem(handle);

            LoadedMemFonts.Clear();
        }

        static class Win32Font
        {
            [DllImport("gdi32.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
            static extern IntPtr AddFontMemResourceEx(IntPtr pbFont, int cbFont, IntPtr pdv, out int pcFonts);
            [DllImport("gdi32.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
            static extern int RemoveFontMemResourceEx(IntPtr fh);

            [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
            static extern int AddFontResource(string lpszFilename);
            [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
            static extern int RemoveFontResource(string lpszFilename);
            [DllImport("user32.dll", CharSet = CharSet.Ansi)]
            static extern IntPtr SendMessageTimeout(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam, int fuFlags, int uTimeout, out int lpdwResult);

            static void BroadcastMessage(int message, IntPtr wparam, IntPtr lparam)
            {
                int a;
                SendMessageTimeout(new IntPtr(0xFFFF), message, wparam, lparam, 2, 1000, out a);
            }

            public static bool LoadFontFile(string path)
            {
                if (AddFontResource(path) != 0)
                {
                    BroadcastMessage(0x001D, IntPtr.Zero, IntPtr.Zero);
                    return true;
                }
                return false;
            }

            public static bool UnloadFontFile(string path)
            {
                if (RemoveFontResource(path) != 0)
                {
                    BroadcastMessage(0x001D, IntPtr.Zero, IntPtr.Zero);
                    return true;
                }
                return false;
            }

            public static IntPtr LoadFontMem(byte[] fontImage)
            {
                IntPtr ptr = Marshal.AllocHGlobal(fontImage.Length);
                Marshal.Copy(fontImage, 0, ptr, fontImage.Length);

                int fonts;
                IntPtr fontHandle = AddFontMemResourceEx(ptr, fontImage.Length, IntPtr.Zero, out fonts);

                if (fontHandle != IntPtr.Zero)
                    BroadcastMessage(0x001D, IntPtr.Zero, IntPtr.Zero);

                Marshal.FreeHGlobal(ptr);

                return fontHandle;
            }

            public static bool UnloadFontMem(IntPtr fontHandle)
            {
                if (RemoveFontMemResourceEx(fontHandle) != 0)
                {
                    BroadcastMessage(0x001D, IntPtr.Zero, IntPtr.Zero);
                    return true;
                }
                return false;
            }
        }
    }
}
