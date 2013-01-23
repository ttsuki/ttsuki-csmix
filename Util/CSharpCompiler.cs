using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using Microsoft.CSharp;
using System.CodeDom.Compiler;
using System.IO;

namespace Tsukikage.Util
{
    /// <summary>
    /// C# Compiler.
    /// </summary>
    public static class CSharpCompiler
    {
        static CSharpCodeProvider cscp;

        /// <summary>
        /// Set or get Temporary file path. DLL and PDB for CompileDebuggerAttachable.
        /// </summary>
        public static string TemporaryFilePath { get; set; }

        static CSharpCompiler()
        {
            // C# 3.0 対応!!!
            cscp = new CSharpCodeProvider(new Dictionary<string, string> { { "CompilerVersion", "v3.5" } });
            TemporaryFilePath = Path.GetTempPath();
        }


        /// <summary>
        /// 最適化無効デバッグ情報ありでコンパイルしてロードしたアセンブリを返す。
        /// Compile sources and load assembly(as Debug build).
        /// </summary>
        /// <param name="sourceFileNameAndSourceCodePairs">ソースファイル名(物理的にファイルが存在する必要あり。Valueは無視されます！)</param>
        /// <exception cref="CSharpCompilerErrorException">コンパイルエラー</exception>
        /// <returns>コンパイルされたアセンブリ</returns>
        public static Assembly CompileDebuggerAttachable(IDictionary<string, string> sourceFileNameAndSourceCodePairs, bool treatWarningsAsErrors)
        {
            CompilerParameters param = new CompilerParameters();
            param.GenerateInMemory = false;
            param.WarningLevel = 4;
            param.IncludeDebugInformation = true;
            param.TempFiles = new TempFileCollection(TemporaryFilePath);
            param.TempFiles.KeepFiles = true;
            param.TreatWarningsAsErrors = treatWarningsAsErrors;

            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                param.ReferencedAssemblies.Add(asm.Location);

            string[] sourceFileNames = new string[sourceFileNameAndSourceCodePairs.Count];
            sourceFileNameAndSourceCodePairs.Keys.CopyTo(sourceFileNames, 0);

            return CompileFromFile(param, sourceFileNames);
        }

        /// <summary>
        /// 最適化有効デバッグ情報なしでコンパイルしアセンブリを返す。
        /// Compile sources and load assembly(as Release build).
        /// </summary>
        /// <param name="sourceFileNameAndSourceCodePairs">ファイル名とソースコードのKVP</param>
        /// <returns>コンパイルされたアセンブリ</returns>
        /// <exception cref="CSharpCompilerErrorException">コンパイルエラー</exception>
        public static Assembly CompileRelease(IDictionary<string, string> sourceFileNameAndSourceCodePairs, bool treatWarningsAsErrors)
        {
            CompilerParameters param = new CompilerParameters();
            param.GenerateInMemory = true;
            param.WarningLevel = 4;
            param.IncludeDebugInformation = false;
            param.CompilerOptions = "/optimize+";
            param.TreatWarningsAsErrors = treatWarningsAsErrors;

            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                param.ReferencedAssemblies.Add(asm.Location);

            return CompileFromSource(param, sourceFileNameAndSourceCodePairs);
        }

        /// <summary>
        /// 指定されたパラメータでコンパイルし、アセンブリを返す。
        /// </summary>
        /// <param name="param">コンパイルパラメータ</param>
        /// <param name="sourceFileNames">ソースファイル名</param>
        /// <returns>コンパイルされたアセンブリ</returns>
        public static Assembly CompileFromFile(CompilerParameters param, string[] sourceFileNames)
        {
            CompilerResults result = cscp.CompileAssemblyFromFile(param, sourceFileNames);
            
            if (result.Errors.HasErrors)
            {
                throw new CSharpCompilerErrorException(result.Errors);
            }
            return result.CompiledAssembly;
        }

        /// <summary>
        /// Compile and return loaded assembly.
        /// 指定されたパラメータでコンパイルし、アセンブリを返す。
        /// </summary>
        /// <param name="param">コンパイルパラメータ</param>
        /// <param name="sourceFileNameAndCodePairs">ファイル名とソースコードのKVP</param>
        /// <returns>コンパイルされたアセンブリ</returns>
        public static Assembly CompileFromSource(CompilerParameters param, IDictionary<string, string> sourceFileNameAndCodePairs)
        {
            KeyValuePair<string, string>[] kvp = new KeyValuePair<string, string>[sourceFileNameAndCodePairs.Count];
            Array.Sort(kvp);
            sourceFileNameAndCodePairs.CopyTo(kvp, 0);
            string[] sourceFileNames = Array.ConvertAll(kvp, k => k.Value);
            string[] sourceCodes = Array.ConvertAll(kvp, k => k.Value);

            // compile it
            CompilerResults result = cscp.CompileAssemblyFromSource(param, sourceCodes);

            // ファイル名が失われてるので戻しておく。
            // 実行時エラーのときは、そもそもデバッグ情報ないけんね。
            if (result.Errors.HasErrors)
            {
                foreach (CompilerError ce in result.Errors)
                {
                    string index = Path.GetExtension(Path.GetFileNameWithoutExtension(ce.FileName)).Substring(1);
                    ce.FileName = sourceFileNames[int.Parse(index)];
                }
                throw new CSharpCompilerErrorException(result.Errors);
            }

            return result.CompiledAssembly;
        }

        /// <summary>
        /// コンパイルエラーを文字列に。
        /// Convert erros to string.
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        public static string CompilerErrorToString(CompilerError error)
        {
            return (error.IsWarning ? "Warning" : "Error") + ":"
                    + error.FileName + ":" + "(L" + error.Line + ", C" + error.Column + ") " + ":"
                    + error.ErrorNumber + ":" + error.ErrorText;
        }
    }

    public class CSharpCompilerErrorException : ApplicationException
    {
        public CompilerErrorCollection CompilerErrors { get; private set; }
        public CSharpCompilerErrorException(CompilerErrorCollection errors)
            : base("Compile error occured.")
        {
            CompilerErrors = errors;
        }
    }
}
