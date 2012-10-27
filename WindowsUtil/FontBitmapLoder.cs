using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Security;
using System.Drawing;

namespace Tsukikage.WindowsUtil
{
    /// <summary>
    /// 1文字分のビットマップ
    /// </summary>
    public class FontBitmap
    {
        public char Char;
        public short Bpp;
        public Size BlackBox;
        public Size OffsetBox;
        public Size CellInc;
        public byte[] Bitmap;

        /// <summary>
        /// 32bit ARGBを格納してるIntPtrに対して、白文字Alpha付でビットマップをBitBltする。
        /// </summary>
        /// <param name="dest32bppBitmap"></param>
        /// <param name="destBitmapSize"></param>
        /// <param name="dest"></param>
        public void BitBltTo32bppTextureAlpha(IntPtr dest32bppBitmap, Size destBitmapSize, Point dest) { BitBltAlpha(this, dest32bppBitmap, destBitmapSize, dest); }

        static unsafe void BitBltAlpha(FontBitmap chara, IntPtr dest32bppBitmap, Size destBitmapSize, Point dest)
        {
            int sz = chara.Bitmap.Length;
            if (sz == 0) return;

            int dx = dest.X + chara.OffsetBox.Width;
            int dy = dest.Y + chara.OffsetBox.Height;
            int dp = destBitmapSize.Width; // dst pitch (px)

            int sx = Math.Max(0, -dx);
            int sy = Math.Max(0, -dy);
            int ex = Math.Min(chara.BlackBox.Width, destBitmapSize.Width - dx);
            int ey = Math.Min(chara.BlackBox.Height, destBitmapSize.Height - dy);
            int sw = chara.BlackBox.Width;

            fixed (byte* srcBitmap = &chara.Bitmap[0])
            {
                if (chara.Bpp == 8) BitBltAlpha8((uint*)dest32bppBitmap, dx, dy, dp, srcBitmap, sx, sy, ex, ey, sw);
                else if (chara.Bpp == 1) BitBltAlpha1((uint*)dest32bppBitmap, dx, dy, dp, srcBitmap, sx, sy, ex, ey, sw);
                else throw new NotImplementedException();
            }
        }

        static unsafe void BitBltAlpha8(uint* pDst, int dx, int dy, int dp, byte* pSrc, int sx, int sy, int ex, int ey, int sw)
        {
            int sp = sw + 3 & ~3;
            for (int y = sy; y < ey; y++)
            {
                int srcBase = y * sp;
                int dstBase = (y + dy) * dp + dx;
                for (int x = sx; x < ex; x++)
                {
                    int src = srcBase + x;
                    int dst = dstBase + x;
                    pDst[dst] = pSrc[src] * 0x3FC0000u | 0xFFFFFFu; // 下位ビットが0xFFFFFFだから成り立つ
                }
            }
        }

        static unsafe void BitBltAlpha1(uint* pDst, int dx, int dy, int dp, byte* pSrc, int sx, int sy, int ex, int ey, int sw)
        {
            int sp = sw + 31 & ~31;
            for (int y = sy; y < ey; y++)
            {
                int srcBase = y * sp;
                int dstBase = (y + dy) * dp + dx;
                for (int x = sx; x < ex; x++)
                {
                    int src = srcBase + x;
                    int dst = dstBase + x;
                    pDst[dst] = ((uint)pSrc[src >> 3] >> (~x & 7) & 1) * 0xFF000000u  | 0xFFFFFFu;
                }
            }
        }
    }

    /// <summary>
    /// Win32 GetGlyphOutlineからBitmapを得ます。Flyweightパターン版。
    /// </summary>
    public class FontBitmapLoaderFlyweight : IDisposable
    {
        Dictionary<char, FontBitmap> characterBitmapCache = new Dictionary<char, FontBitmap>();
        public FontBitmapLoader BitmapLoader { get; private set; }

        public string FontFace { get { return BitmapLoader.FontFace; } }
        public int Height { get { return BitmapLoader.Height; } }
        public int Ascent { get { return BitmapLoader.Ascent; } }
        public int LineHeight { get { return BitmapLoader.LineHeight; } }
        public bool Antialiased { get { return BitmapLoader.Antialiased; } }

        /// <summary>
        /// Win32 GetGlyphOutlineを使って文字ビットマップを得ます。Flyweightパターン版。
        /// </summary>
        /// <param name="face">フォント名</param>
        /// <param name="height">高さ。正を指定するとセルの高さ、負を指定すると文字の高さと見なす。</param>
        /// <param name="antialiased">アンチエイリアス？　trueの場合65階調、falseの場合2階調のビットマップを得ます</param>
        public FontBitmapLoaderFlyweight(string face, int height, bool antialiased)
        {
            this.BitmapLoader = new FontBitmapLoader(face, height, antialiased);
        }

        /// <summary>
        /// キャラクタ情報を取得します
        /// </summary>
        /// <param name="c">文字</param>
        /// <returns>キャラクタ情報</returns>
        public FontBitmap GetCharacterBitmap(char c)
        {
            FontBitmap bmp;
            if (characterBitmapCache.TryGetValue(c, out bmp))
                return bmp;
            bmp = BitmapLoader.GetCharacterBitmap(c);
            characterBitmapCache[c] = bmp;
            return bmp;
        }

        public void Dispose()
        {
            if (BitmapLoader != null)
            {
                BitmapLoader.Dispose();
                BitmapLoader = null;
            }
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Win32 GetGlyphOutlineからBitmapを得ます。
    /// </summary>
    public class FontBitmapLoader : IDisposable
    {
        IntPtr hdc;
        IntPtr hFont;

        public string FontFace { get; private set; }
        public int Height { get; private set; }
        public int Ascent { get; private set; }
        public int LineHeight { get; private set; }
        public bool Antialiased { get; private set; }

        /// <summary>
        /// 文字サイズのポイントをピクセルに変換する。
        /// Windowsの文字サイズ設定に依存しない。
        /// </summary>
        /// <param name="pt"></param>
        /// <returns></returns>
        public static int Pt2Px(int pt) { return pt * 96 / 72; }

        /// <summary>
        /// Win32 GetGlyphOutlineを使って文字ビットマップを得る
        /// </summary>
        /// <param name="fontface">フォント名</param>
        /// <param name="height">高さ。正を指定するとセルの高さ、負を指定すると文字の高さと見なす。</param>
        /// <param name="antialiased">アンチエイリアス？　trueの場合65階調、falseの場合2階調のビットマップを得ます</param>
        public FontBitmapLoader(string fontface, int height, bool antialiased)
        {
            this.FontFace = fontface;
            this.Height = height;
            this.Antialiased = antialiased;

            // System.Drawing.Font は、ローカルに読み込んだフォントを扱えないっぽいので、ここもWin32 APIで。
            this.hFont = Win32.CreateFont(height, 0, 0, 0, 400, 0, 0, 0, 1, 0, 0, 4, 0, FontFace);
            this.hdc = Win32.CreateCompatibleDC(IntPtr.Zero);
            this.hFont = Win32.SelectObject(hdc, hFont);
            Win32.TEXTMETRIC tm;
            Win32.GetTextMetrics(hdc, out tm);
            this.Ascent = tm.tmAscent;
            this.LineHeight = tm.tmHeight;
        }

        void ReleaseUnmanaged()
        {
            if (hFont != IntPtr.Zero)
            {
                this.hFont = Win32.SelectObject(hdc, hFont);
                Win32.DeleteObject(this.hFont);
                this.hFont = IntPtr.Zero;

                Win32.DeleteDC(this.hdc);
                this.hdc = IntPtr.Zero;
            }
        }

        public void Dispose()
        {
            ReleaseUnmanaged();
            GC.SuppressFinalize(this);
        }

        ~FontBitmapLoader()
        {
            ReleaseUnmanaged();
        }

        /// <summary>
        /// 1文字分のグリフを得る
        /// </summary>
        /// <param name="c">文字</param>
        /// <returns>bitmap</returns>
        public FontBitmap GetCharacterBitmap(char c)
        {
            uint flag = Antialiased ? Win32.GGO_GRAY8_BITMAP : Win32.GGO_BITMAP;
            Win32.GLYPHMETRICS metrics = new Win32.GLYPHMETRICS();
            Win32.MAT2 mat2 = Win32.MAT2.Identity;

            int bufSize = Win32.GetGlyphOutline(hdc, c, flag, ref metrics, 0, null, ref mat2);
            byte[] buf = new byte[bufSize];
            Win32.GetGlyphOutline(hdc, c, flag, ref metrics, (uint)bufSize, buf, ref mat2);

            FontBitmap bmp = new FontBitmap();
            bmp.Char = c;
            bmp.Bpp = (short)(Antialiased ? 0x8 : 0x1);
            bmp.Bitmap = buf;
            bmp.BlackBox = new Size(metrics.gmBlackBoxX, metrics.gmBlackBoxY);
            bmp.OffsetBox = new Size(metrics.gmptGlyphOriginX, Ascent - metrics.gmptGlyphOriginY);
            bmp.CellInc = new Size(metrics.gmCellIncX, metrics.gmCellIncY);

            return bmp;
        }

        private static class Win32
        {
            [SuppressUnmanagedCodeSecurity]
            [DllImport("gdi32.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
            public static extern int GetGlyphOutline(IntPtr hdc, uint uChar, uint uFormat,
               ref GLYPHMETRICS lpgm, uint cbBuffer, byte[] lpvBuffer, [In] ref MAT2 lpmat2);

            [SuppressUnmanagedCodeSecurity]
            [DllImport("gdi32.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
            public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

            [SuppressUnmanagedCodeSecurity]
            [DllImport("gdi32.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
            public static extern IntPtr DeleteObject(IntPtr hgdiobj);

            [SuppressUnmanagedCodeSecurity]
            [DllImport("gdi32.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
            public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

            [SuppressUnmanagedCodeSecurity]
            [DllImport("gdi32.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool DeleteDC(IntPtr hdc);

            [SuppressUnmanagedCodeSecurity]
            [DllImport("gdi32.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
            public static extern IntPtr GetTextMetrics(IntPtr hdc, out TEXTMETRIC lptm);

            [SuppressUnmanagedCodeSecurity]
            [DllImport("gdi32.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
            public static extern IntPtr CreateFont(int nHeight, int nWidth, int nEscapement, int nOrientation, int fnWeight,
                int fdwItalic, int fdwUnderline, int fdwStrikeOut, int fdwCharSet, int fdwOutputPrecision, int fdwClipPrecision,
                int fdwQuality, int fdwPitchAndFamily, string lpszFace);

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            public struct TEXTMETRIC
            {
                public int tmHeight;
                public int tmAscent;
                public int tmDescent;
                public int tmInternalLeading;
                public int tmExternalLeading;
                public int tmAveCharWidth;
                public int tmMaxCharWidth;
                public int tmWeight;
                public int tmOverhang;
                public int tmDigitizedAspectX;
                public int tmDigitizedAspectY;
                public char tmFirstChar;
                public char tmLastChar;
                public char tmDefaultChar;
                public char tmBreakChar;
                public byte tmItalic;
                public byte tmUnderlined;
                public byte tmStruckOut;
                public byte tmPitchAndFamily;
                public byte tmCharSet;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct MAT2
            {
                public short eM11fra, eM11val;
                public short eM12fra, eM12val;
                public short eM21fra, eM21val;
                public short eM22fra, eM22val;

                static readonly MAT2 E;
                public static MAT2 Identity { get { return E; } }

                static MAT2()
                {
                    E.eM11val = E.eM22val = 1;
                }
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct GLYPHMETRICS
            {
                public int gmBlackBoxX;
                public int gmBlackBoxY;
                public int gmptGlyphOriginX;
                public int gmptGlyphOriginY;
                public short gmCellIncX;
                public short gmCellIncY;
            }

            public const uint GGO_GRAY8_BITMAP = 6;
            public const uint GGO_BITMAP = 1;
        }
    }
}