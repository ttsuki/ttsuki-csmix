using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.IO;
using System.Reflection;

namespace Tsukikage.DllPInvoke.MP3Tag
{
    /*
     *  C# から mp3infp を使うためのクラスライブラリ
     *      2010. 5. 3 / 月影とも <http://tu3.jp/>
     * 
     *  Win32工作小屋 様の mp3infp をC#から使うためのライブラリです。
     *  http://win32lab.com/fsw/mp3infp/
     *  
     *  License: NYSL (本ファイルのみ)
     *  いかなる損害にも責任追いません。
     *  
     *  簡単な使い方は Example クラス見てください。
     */

#if COMPILE_EXAMPLE
    /// <summary>
    /// 使用例
    /// </summary>
    static class Example
    {
        /// <summary>
        /// タグ情報を読み込んで表示してみる。
        /// </summary>
        public static void ShowTitleAndArtist()
        {
            // タグ情報を読み込んで表示してみる。
            Environment.CurrentDirectory = @"E:/Music";
            TagInfo tag1 = MP3infp.LoadTag(@"[丹下桜] SAKURA/14. New Frontier.mp3");
            Console.WriteLine(tag1.Title + " / " + tag1.Artist);
        }

        /// <summary>
        /// ID3v2 タグを ID3v1タグにコピーしてみる
        /// </summary>
        public static void CopyID3TagFromV1ToV2()
        {
            // ID3v2 タグを ID3v1タグにコピーしてみる
            MP3infp mp3infp = new MP3infp(@"E:/Music/[水樹奈々] PHANTOM MINDS/02. Don't be long.mp3");

            TagInfo tag_v2 = mp3infp.LoadTag<TagInfo.MP3_ID3v2>();

            // なかったら作る
            if (!mp3infp.ContainsMP3Tag(MP3infp.MP3TagType.ID3v1))
                mp3infp.AddMP3Tag(MP3infp.MP3TagType.ID3v1);

            TagInfo tag_v1 = mp3infp.LoadTag<TagInfo.MP3_ID3v1>();

            // コピー元とコピー先で共通して有効な項目のみコピーされます。
            TagInfo.Copy(tag_v2, tag_v1);
            tag_v1.Save();

            // ID3v2 は Unicode タグとして上書き保存します(変換されます)
            tag_v2.SaveUnicode();
        }
    }
#endif

    /// <summary>
    /// mp3infp.dll を使用してメディアファイルのタグ操作を提供します。
    /// </summary>
    public class MP3Infp
    {
        #region static methods

        /// <summary>
        /// Mp3Infpが利用可能かどうかを取得します。
        /// </summary>
        public static bool Available { get { return System.IO.File.Exists("mp3infp.dll"); } }

        /// <summary>
        /// バージョン番号を取得します。
        /// </summary>
        public static int Version { get { return Native.GetVer(); } }

        /// <summary>
        /// タグ情報をロードします。
        /// </summary>
        /// <param name="path">ファイル名</param>
        static void LoadFile(string path)
        {
            if (!File.Exists(path)) 
                throw new FileNotFoundException("指定されたファイルが見つかりません", path);
            
            path = Path.GetFullPath(path);
            lock (Native.SyncRoot)
            {
                Native.Load(IntPtr.Zero, path);
            }
        }

        /// <summary>
        /// ファイルの種類を判定します。
        /// </summary>
        /// <param name="path">ファイル名</param>
        /// <returns>ファイルの種類</returns>
        public static MediaFileType GetFileType(string path)
        {
            lock (Native.SyncRoot)
            {
                LoadFile(path);
                return Native.GetFileType();
            }
        }

        /// <summary>
        /// ファイルが指定の種類のファイルであるかどうかを判断します。
        /// </summary>
        /// <param name="path">ファイル名</param>
        /// <param name="t">種類</param>
        /// <returns>等しい場合 true</returns>
        public static bool IsFileType(string path, MediaFileType t)
        {
            return (GetFileType(path) == t);
        }

        /// <summary>
        /// MP3ファイルに格納されているタグの種類を取得します。
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static MP3TagType GetAvailableMP3TagType(string path)
        {
            MP3TagType t = MP3TagType.None;
            Native.MP3TagType tagType;

            lock (Native.SyncRoot)
            {
                LoadFile(path);
                tagType = Native.GetTagType();
            }

            if ((tagType & Native.MP3TagType.ID3V1) > 0) t |= MP3TagType.ID3v1;
            if ((tagType & Native.MP3TagType.ID3V2) > 0) t |= MP3TagType.ID3v2;
            if ((tagType & Native.MP3TagType.RIFFSIF) > 0) t |= MP3TagType.RIFFSIF;
            if ((tagType & (Native.MP3TagType.APEV1 | Native.MP3TagType.APEV2)) > 0) t |= MP3TagType.APE;
            return t;
        }

        /// <summary>
        /// ファイルが指定した種類のタグを含むかどうかを判断します
        /// </summary>
        /// <param name="path">ファイル名</param>
        /// <param name="t">タグの種類</param>
        /// <returns>含む場合 true</returns>
        public static bool ContainsMP3Tag(string path, MP3TagType t)
        {
            return ((GetAvailableMP3TagType(path) & t) == t);
        }

        /// <summary>
        /// 指定した形式のMP3タグを作成します。即座に反映します。
        /// </summary>
        /// <param name="path">ファイル名</param>
        /// <param name="type">タグの種類</param>
        /// <returns>成功した場合 true</returns>
        public static bool AddMP3Tag(string path, MP3TagType type)
        {
            path = Path.GetFullPath(path);
            int error = -1;

            lock (Native.SyncRoot)
            {
                LoadFile(path);
                switch (type)
                {
                    case MP3TagType.ID3v1: error = Native.MakeId3v1Tag(path); break;
                    case MP3TagType.ID3v2: error = Native.MakeId3v2Tag(path); break;
                    case MP3TagType.RIFFSIF: error = Native.MakeRMPTag(path); break;
                    case MP3TagType.APE: error = Native.MakeApeTag(path); break;
                }
                if (error > 0)
                    throw new System.ComponentModel.Win32Exception(error);
            }
            return error == 0;
        }

        /// <summary>
        /// 指定した形式のMP3タグを削除します。即座に反映します。
        /// </summary>
        /// <param name="path">ファイル名</param>
        /// <param name="type">タグの種類</param>
        /// <returns>成功した場合 true</returns>
        public static bool RemoveMP3Tag(string path, MP3TagType type)
        {
            path = Path.GetFullPath(path);
            int error = -1;
            
            lock (Native.SyncRoot)
            {
                LoadFile(path);
                switch (type)
                {
                    case MP3TagType.ID3v1: error = Native.DeleteId3v1Tag(path); break;
                    case MP3TagType.ID3v2: error = Native.DeleteId3v2Tag(path); break;
                    case MP3TagType.RIFFSIF: error = Native.DeleteRMPTag(path); break;
                    case MP3TagType.APE: error = Native.DeleteApeTag(path); break;
                }
            }
            if (error > 0)
                throw new System.ComponentModel.Win32Exception(error);

            return error == 0;
        }

        /// <summary>
        /// プロパティウィンドウを開きます。
        /// </summary>
        /// <param name="host">ホストとなるフォーム</param>
        /// <param name="path">対象のファイル名</param>
        /// <param name="modeless">trueならばモードレス、falseならばモーダルダイアログとして開きます。</param>
        /// <returns></returns>
        public static void OpenPropertyDialog(System.Windows.Forms.Form host, string path, bool modeless)
        {
            path = Path.GetFullPath(path);
            if (Native.ViewPropEx(host != null ? host.Handle : IntPtr.Zero, path, 0, modeless, 0, 0) < 0)
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            return ;
        }

        /// <summary>
        /// 指定したファイルのタグを読み込みます。
        /// </summary>
        /// <param name="path">ファイル名</param>
        /// <returns>タグ情報</returns>
        public static TagInfo LoadTag(string path)
        {
            path = Path.GetFullPath(path);
            switch (GetFileType(path))
            {
                // ID3v2 > APE > RMP > ID3v1 の順で優先。
                case MediaFileType.MP3:
                    return (TagInfo)LoadTag<TagInfo.MP3_ID3v2>(path)
                        ?? (TagInfo)LoadTag<TagInfo.MP3_APE>(path)
                        ?? (TagInfo)LoadTag<TagInfo.MP3_RiffSIF>(path)
                        ?? (TagInfo)LoadTag<TagInfo.MP3_ID3v1>(path)
                        ?? (TagInfo)new TagImpl.Unknown(path);
                case MediaFileType.WAV: return LoadTag<TagInfo.WAV>(path);
                case MediaFileType.AVI: return LoadTag<TagInfo.AVI>(path);
                case MediaFileType.VQF: return LoadTag<TagInfo.VQF>(path);
                case MediaFileType.ASF: return LoadTag<TagInfo.ASF>(path);
                case MediaFileType.OGG: return LoadTag<TagInfo.OGG>(path);
                case MediaFileType.APE: return LoadTag<TagInfo.APE>(path);
                case MediaFileType.MP4: return LoadTag<TagInfo.MP4>(path);
                default: return new TagImpl.Unknown(path);
            }
        }

        /// <summary>
        /// 指定したファイルのタグを読み込みます。
        /// </summary>
        /// <typeparam name="T">タグ種別. TagInfo.XXXX を指定します。指定した形式のタグが利用可能でない場合はnullが返ります。</typeparam>
        /// <param name="path">ファイル名</param>
        /// <returns>タグ情報</returns>
        public static T LoadTag<T>(string path) where T : TagInfo
        {
            path = Path.GetFullPath(path);
            if (typeof(T) == typeof(TagInfo.MP3_ID3v1)) return IsFileType(path, MediaFileType.MP3) && ContainsMP3Tag(path, MP3TagType.ID3v1) ? new TagImpl.MP3_ID3v1(path) as T : null;
            if (typeof(T) == typeof(TagInfo.MP3_ID3v2)) return IsFileType(path, MediaFileType.MP3) && ContainsMP3Tag(path, MP3TagType.ID3v2) ? new TagImpl.MP3_ID3v2(path) as T : null;
            if (typeof(T) == typeof(TagInfo.MP3_RiffSIF)) return IsFileType(path, MediaFileType.MP3) && ContainsMP3Tag(path, MP3TagType.RIFFSIF) ? new TagImpl.MP3_RiffSIF(path) as T : null;
            if (typeof(T) == typeof(TagInfo.MP3_APE)) return IsFileType(path, MediaFileType.MP3) && ContainsMP3Tag(path, MP3TagType.APE) ? new TagImpl.MP3_APE(path) as T : null;
            if (typeof(T) == typeof(TagInfo.WAV)) return IsFileType(path, MediaFileType.WAV) ? new TagImpl.WAV(path) as T : null;
            if (typeof(T) == typeof(TagInfo.AVI)) return IsFileType(path, MediaFileType.AVI) ? new TagImpl.AVI(path) as T : null;
            if (typeof(T) == typeof(TagInfo.VQF)) return IsFileType(path, MediaFileType.VQF) ? new TagImpl.VQF(path) as T : null;
            if (typeof(T) == typeof(TagInfo.ASF)) return IsFileType(path, MediaFileType.ASF) ? new TagImpl.ASF(path) as T : null;
            if (typeof(T) == typeof(TagInfo.OGG)) return IsFileType(path, MediaFileType.OGG) ? new TagImpl.OGG(path) as T : null;
            if (typeof(T) == typeof(TagInfo.APE)) return IsFileType(path, MediaFileType.APE) ? new TagImpl.APE(path) as T : null;
            if (typeof(T) == typeof(TagInfo.MP4)) return IsFileType(path, MediaFileType.MP4) ? new TagImpl.MP4(path) as T : null;
            return null;
        }

        /// <summary>
        /// タグ情報を再読み込みします。
        /// </summary>
        /// <param name="path">ファイル名</param>
        /// <param name="tag">タグ</param>
        public static void ReloadTagInfo(TagInfo tag)
        {
            lock (Native.SyncRoot)
            {
                // 各プロパティのValueNameAttributeを見て読み込む。
                LoadFile(tag.Path);
                PropertyInfo[] properties = tag.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                foreach (var p in properties)
                {
                    ValueNameAttribute vna = Attribute.GetCustomAttribute(p, typeof(ValueNameAttribute)) as ValueNameAttribute;
                    if (vna != null)
                    {
                        IntPtr ptr;
                        if (Native.GetValue(vna.ValueName, out ptr))
                            p.GetSetMethod(true).Invoke(tag, new object[] { Marshal.PtrToStringAnsi(ptr) });
                    }
                }
            }
        }

        /// <summary>
        /// タグ情報を保存します。Unicode
        /// </summary>
        /// <param name="tag">タグ</param>
        public static void SaveTagInfoUnicode(TagInfo tag)
        {
            SaveTagInfo(tag, true);
        }

        /// <summary>
        /// タグ情報を保存します。ANSI
        /// </summary>
        /// <param name="tag">タグ</param>
        public static void SaveTagInfo(TagInfo tag)
        {
            SaveTagInfo(tag, false);
        }

        /// <summary>
        /// タグ情報を保存します。
        /// </summary>
        /// <param name="tag">タグ</param>
        /// <param name="unicode">Unicode形式で保存するか？ false: ANSI</param>
        public static void SaveTagInfo(TagInfo tag, bool unicode)
        {
            if (tag is TagInfo.Unknown)
                throw new MP3Infp.Mp3infpException("タグの種類が不明なため保存できません。\nMP3ファイルの場合は、ファイルがタグを含むかどうかを確認し、もしタグを持たない場合はあらかじめAddMP3Tagで作成します。");
           
            int error = 0;
            lock (Native.SyncRoot)
            {
                LoadFile(tag.Path);
                Native.SetConf("mp3_ID3v2Unicode", unicode ? "1" : "0");
                Native.SetConf("mp3_ID3v2Unsync", !unicode ? "1" : "0");

                // 各プロパティのValueNameAttributeを見て書き込む。
                PropertyInfo[] properties = tag.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                foreach (var p in properties)
                {
                    ValueNameAttribute vna = Attribute.GetCustomAttribute(p, typeof(ValueNameAttribute)) as ValueNameAttribute;
                    if (vna != null && p.GetSetMethod() != null) {
                        var val = p.GetValue(tag, null);
                        if (val != null)
                            Native.SetValue(vna.ValueName, val.ToString());
                    }
                }

                error = Native.Save(tag.Path);
            }
            if (error > 0) throw new System.ComponentModel.Win32Exception(error);
            if (error < 0) throw new MP3Infp.Mp3infpException("タグの保存中にエラーが発生しました。");
        }

        #endregion
     
        #region instance methods
        /// <summary>
        /// 対象ファイルの名前
        /// </summary>
        string path;

        /// <summary>
        /// mp3infp.dll を使用してメディアファイルのタグ操作を提供します。
        /// </summary>
        /// <param name="path">ファイル名</param>
        public MP3Infp(string path)
        {
            this.path = Path.GetFullPath(path);
            LoadFile(path);
        }

        /// <summary>
        /// ファイルの種類を取得します。
        /// </summary>
        public MediaFileType FileType { get { return GetFileType(path); } }

        /// <summary>
        /// MP3ファイルに格納されているタグの種類を取得します。
        /// </summary>
        public MP3TagType AvailableMP3TagType { get { return GetAvailableMP3TagType(path); } }

        /// <summary>
        /// プロパティウィンドウを開きます。
        /// </summary>
        /// <param name="host">ホストとなるフォーム</param>
        /// <param name="modeless">trueならばモードレス、falseならばモーダルダイアログとして開きます。</param>
        public void OpenPropertyDialog(System.Windows.Forms.Form host, bool modeless) { OpenPropertyDialog(host, path, modeless); }

        /// <summary>
        /// タグを読み込みます。
        /// </summary>
        /// <returns>タグ情報</returns>
        public TagInfo LoadTag() { return LoadTag(path); }

        /// <summary>
        /// 指定した形式のタグを読み込みます。
        /// </summary>
        /// <typeparam name="T">タグの形式。TagInfo.XXXX を指定します。利用可能でない場合はnullが返ります。</typeparam>
        /// <param name="path">ファイル名</param>
        /// <returns>タグ情報</returns>
        public T LoadTag<T>() where T : TagInfo { return LoadTag<T>(path); }

        /// <summary>
        /// 指定した形式のMP3タグを作成します。
        /// </summary>
        /// <param name="type">タグの種類</param>
        /// <returns>成功した場合 true</returns>
        public bool AddMP3Tag(MP3TagType type) { return AddMP3Tag(path, type); }

        /// <summary>
        /// 指定した種類のタグを含むかどうかを判断します
        /// </summary>
        /// <param name="t">タグの種類</param>
        /// <returns>含む場合 true</returns>
        public bool ContainsMP3Tag(MP3TagType t)
        {
            return ContainsMP3Tag(path, t);
        }

        /// <summary>
        /// 指定した形式のMP3タグを削除します。
        /// </summary>
        /// <param name="type">タグの種類</param>
        /// <returns>成功した場合 true</returns>
        public bool RemoveMP3Tag(MP3TagType type) { return RemoveMP3Tag(path, type); }
        #endregion

        #region enum
        /// <summary>
        /// メディアファイルの種類を表します。
        /// </summary>
        public enum MediaFileType
        {
            Unknown = 0x00,
            MP3 = 0x01,
            WAV = 0x02,
            AVI = 0x03,
            VQF = 0x04,
            ASF = 0x05, // WMA, WMV, etc.
            OGG = 0x07,
            APE = 0x08,
            MP4 = 0x09,
        }

        /// <summary>
        /// MP3ファイルのタグの組み合わせを表します。
        /// </summary>
        [Flags]
        public enum MP3TagType
        {
            None = 0x0,
            ID3v1 = 0x1,
            ID3v2 = 0x2,
            RIFFSIF = 0x4,
            APE = 0x8,
        }

        #endregion

        #region inner classes
        static class TagImpl
        {
            public class MP3_ID3v1 : TagInfo.MP3_ID3v1 { public MP3_ID3v1(string path) : base(path) { } }
            public class MP3_ID3v2 : TagInfo.MP3_ID3v2 { public MP3_ID3v2(string path) : base(path) { } }
            public class MP3_RiffSIF : TagInfo.MP3_RiffSIF { public MP3_RiffSIF(string path) : base(path) { } }
            public class MP3_APE : TagInfo.MP3_APE { public MP3_APE(string path) : base(path) { } }
            public class WAV : TagInfo.WAV { public WAV(string path) : base(path) { } }
            public class AVI : TagInfo.AVI { public AVI(string path) : base(path) { } }
            public class VQF : TagInfo.VQF { public VQF(string path) : base(path) { } }
            public class ASF : TagInfo.ASF { public ASF(string path) : base(path) { } }
            public class OGG : TagInfo.OGG { public OGG(string path) : base(path) { } }
            public class APE : TagInfo.APE { public APE(string path) : base(path) { } }
            public class MP4 : TagInfo.MP4 { public MP4(string path) : base(path) { } }
            public class Unknown : TagInfo.Unknown { public Unknown(string path) : base(path) { } }
        }

        /// <summary>
        /// mp3infp に関するエラーを表します。
        /// </summary>
        public class Mp3infpException : Exception
        {
            /// <summary>
            /// mp3infp に関するエラーを表します。
            /// </summary>
            public Mp3infpException(string message) : base(message) { }
        }

        #endregion

        #region Native API
        static class Native
        {
            public static object SyncRoot = new object();

            [DllImport("mp3infp.dll", EntryPoint = "mp3infp_GetVer", CharSet = CharSet.Ansi)]
            public static extern int GetVer();

            [DllImport("mp3infp.dll", EntryPoint = "mp3infp_ViewPropEx", CharSet = CharSet.Ansi, SetLastError=true)]
            public static extern int ViewPropEx(IntPtr hWnd, [MarshalAs(UnmanagedType.LPStr)] string szFileName, int dwPage, [MarshalAs(UnmanagedType.Bool)] bool modeless, int param1, int param2);

            [DllImport("mp3infp.dll", EntryPoint = "mp3infp_Load", CharSet = CharSet.Ansi)]
            public static extern int Load(IntPtr hWnd, [MarshalAs(UnmanagedType.LPStr)] string szFileName);

            [DllImport("mp3infp.dll", EntryPoint = "mp3infp_GetType", CharSet = CharSet.Ansi)]
            public static extern MP3Infp.MediaFileType GetFileType();

            [DllImport("mp3infp.dll", EntryPoint = "mp3infp_GetValue", CharSet = CharSet.Ansi)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool GetValue([MarshalAs(UnmanagedType.LPStr)] string szValueName, out IntPtr buf);

            [DllImport("mp3infp.dll", EntryPoint = "mp3infp_mp3_GetTagType", CharSet = CharSet.Ansi)]
            public static extern MP3TagType GetTagType();

            [DllImport("mp3infp.dll", EntryPoint = "mp3infp_SetConf", CharSet = CharSet.Ansi)]
            public static extern int SetConf([MarshalAs(UnmanagedType.LPStr)] string tag, [MarshalAs(UnmanagedType.LPStr)] string value);

            [DllImport("mp3infp.dll", EntryPoint = "mp3infp_SetValue", CharSet = CharSet.Ansi)]
            public static extern int SetValue([MarshalAs(UnmanagedType.LPStr)] string szValueName, [MarshalAs(UnmanagedType.LPStr)] string buf);

            [DllImport("mp3infp.dll", EntryPoint = "mp3infp_Save", CharSet = CharSet.Ansi)]
            public static extern int Save([MarshalAs(UnmanagedType.LPStr)] string szFileName);

            [DllImport("mp3infp.dll", EntryPoint = "mp3infp_mp3_MakeId3v1", CharSet = CharSet.Ansi)]
            public static extern int MakeId3v1Tag([MarshalAs(UnmanagedType.LPStr)] string szFileName);

            [DllImport("mp3infp.dll", EntryPoint = "mp3infp_mp3_DelId3v1", CharSet = CharSet.Ansi)]
            public static extern int DeleteId3v1Tag([MarshalAs(UnmanagedType.LPStr)] string szFileName);

            [DllImport("mp3infp.dll", EntryPoint = "mp3infp_mp3_MakeId3v2", CharSet = CharSet.Ansi)]
            public static extern int MakeId3v2Tag([MarshalAs(UnmanagedType.LPStr)] string szFileName);

            [DllImport("mp3infp.dll", EntryPoint = "mp3infp_mp3_DelId3v2", CharSet = CharSet.Ansi)]
            public static extern int DeleteId3v2Tag([MarshalAs(UnmanagedType.LPStr)] string szFileName);

            [DllImport("mp3infp.dll", EntryPoint = "mp3infp_mp3_MakeRMP", CharSet = CharSet.Ansi)]
            public static extern int MakeRMPTag([MarshalAs(UnmanagedType.LPStr)] string szFileName);

            [DllImport("mp3infp.dll", EntryPoint = "mp3infp_mp3_DelRMP", CharSet = CharSet.Ansi)]
            public static extern int DeleteRMPTag([MarshalAs(UnmanagedType.LPStr)] string szFileName);

            [DllImport("mp3infp.dll", EntryPoint = "mp3infp_mp3_MakeApeTag", CharSet = CharSet.Ansi)]
            public static extern int MakeApeTag([MarshalAs(UnmanagedType.LPStr)] string szFileName);

            [DllImport("mp3infp.dll", EntryPoint = "mp3infp_mp3_DelApeTag", CharSet = CharSet.Ansi)]
            public static extern int DeleteApeTag([MarshalAs(UnmanagedType.LPStr)] string szFileName);

            [Flags]
            public enum MP3TagType
            {
                ID3V1 = 0x00000001,
                ID3V2 = 0x00000002,
                RIFFSIF = 0x00000004,
                ID3V1_0 = 0x00000008,	// v2.43～
                ID3V1_1 = 0x00000010,	// v2.43～
                ID3V2_2 = 0x00000020,	// v2.43～
                ID3V2_3 = 0x00000040,	// v2.43～
                ID3V2_4 = 0x00000080,	// v2.43～
                APEV1 = 0x00000100,	// v2.47～
                APEV2 = 0x00000200,	// v2.47～
            }
        }
        #endregion
    }

    #region TagInfo class
    public abstract class TagInfo
    {
        protected TagInfo(string path)
        {
            this.Path = System.IO.Path.GetFullPath(path);
            MP3Infp.ReloadTagInfo(this);
        }

        public string Path { get; protected set; }

        [ValueName("FILE")]
        public virtual string FileName { get; protected set; }

        [ValueName("FEXT")]
        public virtual string FileExt { get; protected set; }

        [ValueName("SIZ1")]
        public virtual string FileSize { get; protected set; }

        [ValueName("VFMT")]
        public virtual string VideoFormat { get; protected set; }

        [ValueName("AFMT")]
        public virtual string AudioFormat { get; protected set; }

        [ValueName("TIME")]
        public virtual string Duration { get; protected set; }

        [ValueName("INAM")]
        public virtual string Title { get; set; }

        [ValueName("IART")]
        public virtual string Artist { get; set; }

        [ValueName("IPRD")]
        public virtual string Album { get; set; }

        [ValueName("ICMT")]
        public virtual string Comment { get; set; }

        [ValueName("ICRD")]
        public virtual string CreationDate { get; set; }

        [ValueName("IGNR")]
        public virtual string Genre { get; set; }

        [ValueName("TRACK")]
        public virtual string TrackNumber { get; set; }

        [ValueName("ICOP")]
        public virtual string Copyright { get; set; }

        public abstract class MP3_ID3v1 : TagInfo
        {
            protected MP3_ID3v1(string path) : base(path) { }

            [ValueName("INAM_v1")]
            public override string Title { get { return base.Title; } set { base.Title = value; } }

            [ValueName("IART_v1")]
            public override string Artist { get { return base.Artist; } set { base.Artist = value; } }

            [ValueName("IPRD_v1")]
            public override string Album { get { return base.Album; } set { base.Album = value; } }

            [ValueName("ICMT_v1")]
            public override string Comment { get { return base.Comment; } set { base.Comment = value; } }

            [ValueName("ICRD_v1")]
            public override string CreationDate { get { return base.CreationDate; } set { base.CreationDate = value; } }

            [ValueName("IGNR_v1")]
            public override string Genre { get { return base.Genre; } set { base.Genre = value; } }

            [ValueName("TRACK_v1")]
            public override string TrackNumber { get { return base.TrackNumber; } set { base.TrackNumber = value; } }

            [ValueName("(unsupported)")]
            public override string Copyright { get { return ""; } set { } }
        }

        public abstract class MP3_ID3v2 : TagInfo
        {
            protected MP3_ID3v2(string path) : base(path) { }

            [ValueName("INAM_v2")]
            public override string Title { get { return base.Title; } set { base.Title = value; } }

            [ValueName("IART_v2")]
            public override string Artist { get { return base.Artist; } set { base.Artist = value; } }

            [ValueName("IPRD_v2")]
            public override string Album { get { return base.Album; } set { base.Album = value; } }

            [ValueName("ICMT_v2")]
            public override string Comment { get { return base.Comment; } set { base.Comment = value; } }

            [ValueName("ICRD_v2")]
            public override string CreationDate { get { return base.CreationDate; } set { base.CreationDate = value; } }

            [ValueName("IGNR_v2")]
            public override string Genre { get { return base.Genre; } set { base.Genre = value; } }

            [ValueName("ICOP_v2")]
            public override string Copyright { get { return base.Copyright; } set { base.Copyright = value; } }

            [ValueName("TRACK_v2")]
            public override string TrackNumber { get { return base.TrackNumber; } set { base.TrackNumber = value; } }

            [ValueName("ISFT_v2")]
            public virtual string Software { get; set; }

            [ValueName("OART_v2")]
            public virtual string OriginalArtist { get; set; }

            [ValueName("COMP_v2")]
            public virtual string Composer { get; set; }

            [ValueName("URL_v2")]
            public virtual string URL { get; set; }

            [ValueName("ENC2_v2")]
            public virtual string Encoder { get; set; }
        }

        public abstract class MP3_RiffSIF : TagInfo
        {
            protected MP3_RiffSIF(string path) : base(path) { }

            [ValueName("INAM_rmp")]
            public override string Title { get { return base.Title; } set { base.Title = value; } }

            [ValueName("IART_rmp")]
            public override string Artist { get { return base.Artist; } set { base.Artist = value; } }

            [ValueName("IPRD_rmp")]
            public override string Album { get { return base.Album; } set { base.Album = value; } }

            [ValueName("ICMT_rmp")]
            public override string Comment { get { return base.Comment; } set { base.Comment = value; } }

            [ValueName("ICRD_rmp")]
            public override string CreationDate { get { return base.CreationDate; } set { base.CreationDate = value; } }

            [ValueName("IGNR_rmp")]
            public override string Genre { get { return base.Genre; } set { base.Genre = value; } }

            [ValueName("ICOP_rmp")]
            public override string Copyright { get { return base.Copyright; } set { base.Copyright = value; } }

            [ValueName("(unsupported)")]
            public override string TrackNumber { get { return ""; } set { } }

            [ValueName("ISFT_rmp")]
            public virtual string Software { get; set; }

            [ValueName("ISRC_rmp")]
            public virtual string Source { get; set; }

            [ValueName("IENG_rmp")]
            public virtual string Engineer { get; set; }
        }

        public abstract class MP3_APE : TagInfo
        {
            protected MP3_APE(string path) : base(path) { }

            [ValueName("INAM_APE")]
            public override string Title { get { return base.Title; } set { base.Title = value; } }

            [ValueName("IART_APE")]
            public override string Artist { get { return base.Artist; } set { base.Artist = value; } }

            [ValueName("IPRD_APE")]
            public override string Album { get { return base.Album; } set { base.Album = value; } }

            [ValueName("ICMT_APE")]
            public override string Comment { get { return base.Comment; } set { base.Comment = value; } }

            [ValueName("ICRD_APE")]
            public override string CreationDate { get { return base.CreationDate; } set { base.CreationDate = value; } }

            [ValueName("IGNR_APE")]
            public override string Genre { get { return base.Genre; } set { base.Genre = value; } }

            [ValueName("TRACK_APE")]
            public virtual string Engineer { get; set; }

            [ValueName("(unsupported)")]
            public override string TrackNumber { get { return ""; } set { } }

            [ValueName("(unsupported)")]
            public override string Copyright { get { return ""; } set { } }
        }

        public abstract class WAV : TagInfo
        {
            protected WAV(string path) : base(path) { }

            [ValueName("(unsupported)")]
            public override string TrackNumber { get { return ""; } set { } }

            [ValueName("ISFT")]
            public virtual string Software { get; set; }

            [ValueName("ISRC")]
            public virtual string Source { get; set; }

            [ValueName("IENG")]
            public virtual string Engineer { get; set; }

            [ValueName("ISBJ")]
            public virtual string Title_ISBJ { get; set; }
        }

        public abstract class AVI : TagInfo
        {
            protected AVI(string path) : base(path) { }

            [ValueName("(unsupported)")]
            public override string Album { get { return ""; } set { } }

            [ValueName("(unsupported)")]
            public override string TrackNumber { get { return ""; } set { } }

            [ValueName("ISFT")]
            public virtual string Software { get; set; }

            [ValueName("ISRC")]
            public virtual string Source { get; set; }

            [ValueName("IENG")]
            public virtual string Engineer { get; set; }

            [ValueName("ISBJ")]
            public virtual string Title_ISBJ { get; set; }

            [ValueName("AVIV")]
            public virtual string AVIVersion { get; protected set; }
        }
        public abstract class VQF : TagInfo
        {
            protected VQF(string path) : base(path) { }

            [ValueName("(unsupported)")]
            public override string Album { get { return ""; } set { } }

            [ValueName("(unsupported)")]
            public override string CreationDate { get { return ""; } set { } }

            [ValueName("(unsupported)")]
            public override string Genre { get { return ""; } set { } }
        }

        public abstract class ASF : TagInfo
        {
            protected ASF(string path) : base(path) { }

            [ValueName("URL1")]
            public virtual string AlbumURL { get; set; }

            [ValueName("URL2")]
            public virtual string URL { get; set; }
        }

        public abstract class OGG : TagInfo
        {
            protected OGG(string path) : base(path) { }

            [ValueName("(unsupported)")]
            public override string Copyright { get { return ""; } set { } }
        }

        public abstract class APE : TagInfo
        {
            protected APE(string path) : base(path) { }

            [ValueName("(unsupported)")]
            public override string Copyright { get { return ""; } set { } }
        }

        public abstract class MP4 : TagInfo
        {
            protected MP4(string path) : base(path) { }

            [ValueName("TRACK1")]
            public override string TrackNumber { get { return base.TrackNumber; } set { base.TrackNumber = value; } }

            [ValueName("(unsupported)")]
            public override string Copyright { get { return ""; } set { } }

            [ValueName("TRACK2")]
            public virtual string TrackNumberDenom { get; set; }

            [ValueName("DISC1")]
            public virtual string DiscNumber { get; set; }

            [ValueName("DISC2")]
            public virtual string DiscNumberDenom { get; set; }

            [ValueName("BPM")]
            public virtual string BPM { get; set; }

            [ValueName("COMPILATION")]
            public virtual string Compilation { get; set; }

            [ValueName("TOOL")]
            public virtual string Software { get; set; }

            [ValueName("IGRP")]
            public virtual string Groupe { get; set; }

            [ValueName("COMPOSER")]
            public virtual string Composer { get; set; }
        }

        public abstract class Unknown : TagInfo
        {
            protected Unknown(string path) : base(path) { }
        }

        /// <summary>
        /// タグ情報を保存します。
        /// </summary>
        public virtual void Save() { MP3Infp.SaveTagInfo(this); }

        /// <summary>
        /// タグ情報をUnicode形式で保存します。
        /// </summary>
        public virtual void SaveUnicode() { MP3Infp.SaveTagInfoUnicode(this); }

        /// <summary>
        /// タグ間の共通項目をコピーします
        /// Copy common properties.
        /// </summary>
        /// <param name="source">コピー元タグ</param>
        /// <param name="destination">コピー先タグ</param>
        public static void Copy(TagInfo source, TagInfo destination)
        {
            PropertyInfo[] properties = source.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var p in properties)
            {
                // 同じ名前のプロパティを探して、Setを持っていればSetする。
                PropertyInfo d = destination.GetType().GetProperty(p.Name, BindingFlags.Public | BindingFlags.Instance);
                if (d != null && d.GetSetMethod(false) != null) 
                    d.GetSetMethod(true).Invoke(destination, new object[] { p.GetValue(source, null), });
            }
        }

        public override string ToString()
        {
            PropertyInfo[] properties = this.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            List<string> propertyList = new List<string>();
            foreach (var p in properties)
            {
                ValueNameAttribute vna = Attribute.GetCustomAttribute(p, typeof(ValueNameAttribute)) as ValueNameAttribute;
                if (vna != null && p.GetValue(this, null).ToString() != "")
                    propertyList.Add("" + p.Name + "=\"" + p.GetValue(this, null).ToString() + "\", ");
            }

            StringBuilder sb = new StringBuilder();
            propertyList.Sort();
            foreach (string s in propertyList)
                sb.Append(s);
            return "{ " + sb.ToString() + " }";
        }
    }

    [global::System.AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    sealed class ValueNameAttribute : Attribute
    {
        public ValueNameAttribute(string valueName)
        {
            this.ValueName = valueName;
        }

        public string ValueName { get; private set; }
    }
    #endregion
}
