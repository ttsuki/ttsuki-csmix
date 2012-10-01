using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace Tsukikage.Util
{
    /// <summary>
    /// Text File Reader with Guessing Japanese Kanji-code.
    /// 
    /// 日本語文字コード推測つきテキストファイル読み込みクラス。
    /// 読み込み時改行コードをLFに、保存時はCRLFに変換します。BOMは消します。
    /// iso-2022-jp(JIS), euc-jp, shift_jis, UTF-16 UTF-8 を推測します。
    /// BOM がある場合は、 UTF-16BE UTF-32(LE/BE) にも対応。
    /// </summary>
    public class TextFile
    {
        #region Load Methods
        /// <summary>
        /// テキストファイルを読み込む(エンコード推測＋改行を\n単体に変換)。
        /// </summary>
        /// <param name="path">ファイル</param>
        /// <returns></returns>
        public static string LoadText(string path)
        {
            using (Stream fs = File.OpenRead(path))
                return LoadText(fs);
        }

        /// <summary>
        /// テキストファイルを読み込む(エンコード推測＋改行を\n単体に変換)。
        /// </summary>
        /// <param name="path">ファイル</param>
        /// <param name="encoding">読み取りに使ったエンコード</param>
        /// <param name="withBOM">BOMがあったか？</param>
        /// <param name="eol">推測される改行コード</param>
        public static string LoadText(string path, out Encoding encoding, out bool withBOM, out EndOfLine endOfLine)
        {
            using (Stream fs = File.OpenRead(path))
                return LoadText(fs, out encoding, out withBOM, out endOfLine);
        }

        /// <summary>
        /// ストリームからテキストを読み込む(エンコード推測＋改行を\n単体に変換)。
        /// </summary>
        /// <param name="stream">読み取り元ストリーム</param>
        /// <returns></returns>
        public static string LoadText(Stream stream)
        {
            Encoding enc;
            bool bom;
            EndOfLine eol;
            return LoadText(stream, out enc, out bom, out eol);
        }

        /// <summary>
        /// ストリームからテキストを読み込む(エンコード推測＋改行を\n単体に変換)。
        /// </summary>
        /// <param name="stream">読み取り元ストリーム</param>
        /// <param name="encoding">読み取りに使ったエンコード</param>
        /// <param name="withBOM">BOMがあったか？</param>
        /// <param name="eol">推測される改行コード</param>
        /// <returns></returns>
        public static string LoadText(Stream stream, out Encoding encoding, out bool withBOM, out EndOfLine eol)
        {
            byte[] buf = new byte[stream.Length];
            stream.Read(buf, 0, buf.Length);

            encoding = GuessEncode(buf);

            string text = encoding.GetString(buf);
            eol = GuessFileFormat(text);
            text = ConvertNewLineCharacter(text, EndOfLine.LF);
            withBOM = text[0] == 0xFEFF; // BOMで始まっているか？
            if (withBOM)
                text = text.Substring(1); //BOMは消す。
            return text;
        }

        #endregion

        #region Save Methods

        /// <summary>
        /// テキストファイルに保存。(UTF-8N, CRLF)
        /// </summary>
        /// <param name="path">ファイル</param>
        /// <param name="text">テキスト</param>
        public static void SaveText(string path, string text)
        {
            SaveText(path, text, Encoding.UTF8);
        }

        /// <summary>
        /// テキストファイルに保存。(UTF-8以外はBOM付き, CRLFに変換)
        /// </summary>
        /// <param name="path">ファイル</param>
        /// <param name="text">テキスト</param>
        /// <param name="encoding">エンコード</param>
        public static void SaveText(string path, string text, Encoding encoding)
        {
            SaveText(path, text, encoding, encoding != Encoding.UTF8);
        }

        /// <summary>
        /// テキストファイルに保存。(CRLFに変換)
        /// </summary>
        /// <param name="path">ファイル</param>
        /// <param name="text">テキスト</param>
        /// <param name="encoding">エンコード</param>
        /// <param name="withBOM">BOMをつけるか？</param>
        public static void SaveText(string path, string text, Encoding encoding, bool withBOM)
        {
            // UTF-8 以外ならBOMつける。
            SaveText(path, text, encoding, encoding != Encoding.UTF8, EndOfLine.CRLF);
        }

        /// <summary>
        /// テキストファイルに保存。
        /// </summary>
        /// <param name="path">ファイル</param>
        /// <param name="text">テキスト</param>
        /// <param name="encoding">エンコード</param>
        /// <param name="withBOM">BOMをつけるか？</param>
        /// <param name="endOfLine">改行コード</param>
        public static void SaveText(string path, string text, Encoding encoding, bool withBOM, EndOfLine endOfLine)
        {
            using (Stream fs = File.OpenWrite(path))
                SaveText(fs, text, encoding, withBOM, endOfLine);
        }

        /// <summary>
        /// ストリームに書き込み
        /// </summary>
        /// <param name="stream">保存先ストリーム</param>
        /// <param name="text">テキスト</param>
        /// <param name="encoding">エンコード</param>
        /// <param name="withBOM">BOMをつけるか？</param>
        /// <param name="endOfLine">改行コード</param>
        public static void SaveText(Stream stream, string text, Encoding encoding, bool withBOM, EndOfLine endOfLine)
        {
            if (withBOM)
            {
                byte[] preamble = encoding.GetPreamble();
                stream.Write(preamble, 0, preamble.Length);
            }

            text = ConvertNewLineCharacter(text, endOfLine);

            byte[] buf = encoding.GetBytes(text);
            stream.Write(buf, 0, buf.Length);
        }

        #endregion

        #region Guessing

        /// <summary>
        /// 指定されたテキストファイルの文字コードを推測します。
        /// iso-2022-jp(JIS), euc-jp, shift_jis, UTF-16 UTF-8 に対応。
        /// BOM がある場合は、 UTF-16BE UTF-32(LE/BE) にも対応。
        /// </summary>
        /// <param name="path">ファイル名</param>
        /// <returns>エンコード</returns>
        public static Encoding GuessEncode(string path)
        {
            bool bom;
            EndOfLine eol;
            return GuessEncode(path, out bom, out eol);
        }

        /// <summary>
        /// 指定されたテキストファイルの文字コードを推測します。
        /// iso-2022-jp(JIS), euc-jp, shift_jis, UTF-16 UTF-8 に対応。
        /// BOM がある場合は、 UTF-16BE UTF-32(LE/BE) にも対応。
        /// </summary>
        /// <param name="path">ファイル名</param>
        /// <returns>エンコード</returns>
        public static Encoding GuessEncode(string path, out bool withBOM, out EndOfLine eol)
        {
            Encoding enc;
            using (Stream fs = File.OpenRead(path))
                LoadText(fs, out enc, out withBOM, out eol);
            return enc;
        }

        /// <summary>
        /// 指定されたテキストデータの文字コードを推測します。
        /// iso-2022-jp(JIS), euc-jp, shift_jis, UTF-16 UTF-8 に対応。
        /// BOM がある場合は、 UTF-16BE UTF-32(LE/BE) にも対応。
        /// </summary>
        /// <param name="data">文字コードを調べるデータ</param>
        /// <returns>適当と思われるEncodingオブジェクト。判断できなかった時はUTF-8。</returns>
        public static Encoding GuessEncode(byte[] data)
        {
            int len = data.Length;

            // BOMがあればUnicodeだ
            {
                uint b1 = len >= 1 ? data[0] : 0u;
                uint b2 = len >= 2 ? data[1] | b1 << 8 : 0u;
                uint b3 = len >= 3 ? data[2] | b2 << 8 : 0u;
                uint b4 = len >= 4 ? data[3] | b3 << 8 : 0u;

                if (b2 == 0xFFFE) return Encoding.Unicode;
                if (b3 == 0xEFBBBF) return Encoding.UTF8;
                if (b2 == 0xFEFF) return Encoding.GetEncoding("unicodeFFFE");
                if (b4 == 0xFFFE0000) return Encoding.GetEncoding("utf-32");
                if (b4 == 0x0000FEFF) return Encoding.GetEncoding("utf-32BE");
            }

            // unicodeっぽいASCII文字がある 0x?? 0x00
            for (int i = 0; i < len - 1; i++)
                if (data[i] <= 0x7F && data[i + 1] == 0x00)
                    return Encoding.Unicode;

            // JISっぽいエスケープシーケンスがある [ESC]$Bとか
            for (int i = 0; i < len - 3; i++)
            {
                uint c = (uint)(data[i] << 24 | data[i + 1] << 16 | data[i + 2] << 8 | data[i + 3]);
                switch (c)
                {
                    case 0x1B242840: // JIS C 6226 [ESC]$(@
                    case 0x1B242842: // JIS X 0208 [ESC]$(B
                    case 0x1B242844: // JIS X 0212 [ESC]$(D
                    case 0x1B24284F: // JIS X 0213 [ESC]$(O
                    case 0x1B242850: // JIS X 0213 [ESC]$(P
                    case 0x1B242851: // JIS X 0213 [ESC]$(Q
                        return Encoding.GetEncoding("iso-2022-jp");
                }

                switch (c >> 8)
                {
                    case 0x1B2440: // JIS C 6226 [ESC]$@ 
                    case 0x1B2442: // JIS X 0208 [ESC]$B
                    //case 0x1B2840: // JIS X 0201 [ESC](B // ASCII
                    case 0x1B2849: // JIS X 0201 [ESC](I
                        //case 0x1B284A: // JIS X 0201 [ESC](J // Roman
                        return Encoding.GetEncoding("iso-2022-jp");
                }
            }

            int sjis = 0;
            int euc = 0;
            int utf8 = 0;

            // Shift-JIS っぽい文字を数える
            for (int i = 0; i < len - 1; i++)
            {
                uint b1 = data[i];
                uint b2 = data[i + 1];
                if ((b1 ^ 0x20) - 0xA1 < 0x3C && b2 - 0x40 < 0xBD && b2 != 0x7F)
                {
                    sjis += 2;
                    i++;
                }
            }

            // EUC-JPっぽい文字を数える
            for (int i = 0; i < len - 1; i++)
            {
                uint b1 = data[i];
                uint b2 = data[i + 1];
                uint b3 = i < len - 2 ? data[i + 2] : 0u;

                if (b1 == 0x8E)
                {
                    if (b2 - 0xA1 < 0x3F) { euc += 2; i++; }
                    else if (b2 - 0xA1 < 0x5E && b3 - 0xA1 < 0x5E) { euc += 3; i += 2; }
                }
                else if (b1 - 0xA1 < 0x5E && b2 - 0xA1 < 0x5E) { euc += 2; i++; }
            }

            // UTF-8 として解釈できる文字を数える。
            for (int i = 0; i < len - 1; i++)
            {
                uint b1 = data[i];
                bool b2 = i < len - 1 && (uint)data[i + 1] - 0x80 < 0x40;
                bool b3 = i < len - 2 && (uint)data[i + 2] - 0x80 < 0x40;
                bool b4 = i < len - 3 && (uint)data[i + 3] - 0x80 < 0x40;

                if (b1 - 0xC2 < 0x33)
                {
                    switch (b1 >> 4)
                    {
                        case 0xC: if (b2) { utf8 += 2; i++; } break;
                        case 0xE: if (b2 && b3) { utf8 += 3; i += 2; } break;
                        case 0xF: if (b2 && b3 && b4) { utf8 += 4; i += 3; } break;
                    }
                }
            }

            // EUCスコアが高い。
            if (euc >= utf8 && euc >= sjis) return Encoding.GetEncoding("euc-jp");

            // UTF-8がShift-JISと誤判定される方が逆誤判定より確率が高い。
            if (utf8 >= sjis) return Encoding.UTF8;
            return Encoding.GetEncoding("shift_jis");
        }

        /// <summary>
        /// 改行コードを推測する。
        /// 1つも改行がないとCRLFと判定されます。
        /// </summary>
        /// <param name="s">テキスト</param>
        /// <returns>改行コード</returns>
        public static EndOfLine GuessFileFormat(string s)
        {
            int len = s.Length;
            int lf = 0, cr = 0, crlf = 0;
            for (int i = 0; i < len - 1; i++)
            {
                if (s[i] == '\r') cr++;
                if (s[i] == '\n') lf++;
                if (s[i] == '\r' && s[i + 1] == '\n') crlf++;
            }
            if (s[len - 1] == '\r') cr++;
            if (s[len - 1] == '\n') lf++;

            lf -= crlf;
            cr -= crlf;
            if (lf != 0 && cr == 0 && crlf == 0) return EndOfLine.LF;
            if (lf == 0 && cr != 0 && crlf == 0) return EndOfLine.CR;
            if (lf == 0 && cr == 0 && crlf != 0) return EndOfLine.CRLF;
            if (lf == 0 && cr == 0 && crlf == 0) return EndOfLine.CRLF; // default
            return EndOfLine.Mixed; // OMG...
        }

        #endregion
        /// <summary>
        /// 改行コードを変換する
        /// </summary>
        /// <param name="s">変換元テキスト</param>
        /// <param name="to">改行コード</param>
        /// <returns>変換されたテキスト</returns>
        public static string ConvertNewLineCharacter(string s, EndOfLine to)
        {
            switch (to)
            {
                case EndOfLine.LF:
                    return s.Replace("\r\n", "\n").Replace("\r", "\n");
                case EndOfLine.CR:
                    return s.Replace("\r\n", "\r").Replace("\n", "\r");
                case EndOfLine.CRLF:
                    return s.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");
            }
            return s;
        }

        /// <summary>
        /// 改行コードを表す
        /// </summary>
        public enum EndOfLine
        {
            /// <summary></summary>
            NoConvert = 0x00,
            /// <summary>UNIX</summary>
            LF = 0x0A,
            /// <summary>Mac</summary>
            CR = 0x0D,
            /// <summary>Windows</summary>
            CRLF = 0x0D0A,
            /// <summary>混ざってる...</summary>
            Mixed = 0xFFFF,
        }
    }
}
