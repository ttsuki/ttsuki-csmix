using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;

namespace Tsukikage.Util
{
    /// <summary>
    /// プロセスの起動に使われたEXEファイルの情報を返します。
    /// Entry assembly information.
    /// </summary>
    public static class EntryAssemblyInfo
    {
        /// <summary>
        /// 起動に使われたEXEファイルのアセンブリを取得します。
        /// </summary>
        public static Assembly Assembly { get; private set; }

        /// <summary>
        /// EXEファイルのassembly:Title属性を取得します。
        /// </summary>
        public static string Title { get; private set; }

        /// <summary>
        /// EXEファイルのassembly:Description属性を取得します。
        /// </summary>
        public static string Description { get; private set; }

        /// <summary>
        /// EXEファイルのassembly:Company属性を取得します。
        /// </summary>
        public static string Company { get; private set; }

        /// <summary>
        /// EXEファイルのassembly:Product属性を取得します。
        /// </summary>
        public static string Product { get; private set; }

        /// <summary>
        /// EXEファイルのassembly:Copyright属性を取得します。
        /// </summary>
        public static string Copyright { get; private set; }

        /// <summary>
        /// 起動ディレクトリを取得します。
        /// </summary>
        public static string StartupPath { get; private set; }

        static EntryAssembly()
        {
            Assembly = Assembly.GetEntryAssembly();
            
            foreach (object obj in Assembly.GetCustomAttributes(typeof(AssemblyTitleAttribute), true))
                Title = (string)typeof(AssemblyTitleAttribute).GetProperty("Title").GetValue(obj, null);

            foreach (object obj in Assembly.GetCustomAttributes(typeof(AssemblyDescriptionAttribute), true))
                Description = ((AssemblyDescriptionAttribute)obj).Description;

            foreach (object obj in Assembly.GetCustomAttributes(typeof(AssemblyCompanyAttribute), true))
                Company = ((AssemblyCompanyAttribute)obj).Company;

            foreach (object obj in Assembly.GetCustomAttributes(typeof(AssemblyProductAttribute), true))
                Product = ((AssemblyProductAttribute)obj).Product;

            foreach (object obj in Assembly.GetCustomAttributes(typeof(AssemblyCopyrightAttribute), true))
                Copyright = ((AssemblyCopyrightAttribute)obj).Copyright;

            StartupPath = System.Windows.Forms.Application.StartupPath;

        }
    }

}
