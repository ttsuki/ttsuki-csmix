using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Tsukikage.DllPInvoke.WinampDspPlugin
{
    /// <summary>
    /// DSP Plugin
    /// </summary>
    public interface DSPPlugin
    {
        /// <summary>
        /// Version of DSP.H file.
        /// </summary>
        int DspHeaderVersion { get; }

        /// <summary>
        /// Description of plugin.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Modules.
        /// </summary>
        ReadOnlyCollection<DSPModule> Modules { get; }

        /// <summary>
        /// Free library.
        /// </summary>
        void Release();
    }

    /// <summary>
    /// DSP Module
    /// </summary>
    public interface DSPModule
    {
        /// <summary>
        /// Description of module.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Open configuration dialog if avalable.
        /// </summary>
        void Config();

        /// <summary>
        /// Initialize module.
        /// </summary>
        /// <returns>0 on success, creates window, etc (if needed)</returns>
        int Init();

        /// <summary>
        /// Modify waveform samples.
        /// </summary>
        /// <param name="pSamples">pointer to waveform samples</param>
        /// <param name="nSamples">number of samples. (should always be at least 128)</param>
        /// <param name="bitsPerSample">bit per sample. 8, 16, 24, ...</param>
        /// <param name="channels">number of channels. 2, ...</param>
        /// <param name="samplingRate">sampling rate in Hz. 44100, ...</param>
        /// <returns>number of samples to actually write (typically <paramref name="nSamples"/>, but no more than twice <paramref name="nSamples"/>, and no less than half <paramref name="nSamples"/>)</returns>
        int ModifySample(IntPtr pSamples, int nSamples, int bitsPerSample, int channels, int samplingRate);

        /// <summary>
        /// Unload module.
        /// </summary>
        void Quit();
    }

    /// <summary>
    /// Plugin loader
    /// </summary>
    public static class DSPPluginLoader
    {
        /// <summary>
        /// Load a plugin.
        /// </summary>
        /// <param name="dspPluginFileName"></param>
        /// <param name="parentWindowHandle"></param>
        /// <returns></returns>
        public static DSPPlugin LoadDSPPlugin(string dspPluginFileName, IntPtr parentWindowHandle)
        {
            return DSPPluginImpl.LoadLibrary(dspPluginFileName, parentWindowHandle);
        }


        /// <summary>
        /// DSPPlugin Implementation
        /// </summary>
        private class DSPPluginImpl : DSPPlugin
        {
            private DSPPluginImpl() { }

            public bool Loaded { get; private set; }
            public IntPtr ModuleHandle { get; private set; }
            public string ModulePath { get; private set; }
            public string Description { get; private set; }
            public int DspHeaderVersion { get; private set; }
            public ReadOnlyCollection<DSPModule> Modules { get; private set; }

            public static DSPPlugin LoadLibrary(string moduleFileName, IntPtr parentWindowHandle)
            {
                DSPPluginImpl plugin = new DSPPluginImpl();
                Debug.WriteLine("Loading DSP Plugin: " + Path.GetFullPath(moduleFileName));

                // LoadLibrary
                IntPtr hModule = NativeMethods.LoadLibrary(Path.GetFullPath(moduleFileName));
                if (hModule == IntPtr.Zero)
                {
                    Debug.WriteLine(" - Failed to LoadLibrary.");
                    return null;
                }

                // Get address of winampDSPGetHeader2()
                var getHeader = NativeMethods.GetProcAddress<NativeMethods.winampDSPGetHeader2Delegate>(hModule, "winampDSPGetHeader2");
                if (getHeader == null)
                {
                    Debug.WriteLine(" - Failed to GetProcAddress(winampDSPGetHeader2). ");
                    NativeMethods.FreeLibrary(hModule);
                    return null;
                }

                // Get winampDSPHeaderEx heder from winampDSPGetHeader2()
                IntPtr pHeader = getHeader(parentWindowHandle);
                if (pHeader == IntPtr.Zero)
                {
                    Debug.WriteLine(" - Failed to winampDSPGetHeader2()");
                    NativeMethods.FreeLibrary(hModule);
                    return null;
                }
                var header = NativeMethods.PtrToStructure<NativeMethods.winampDSPHeaderEx>(pHeader);
                Debug.WriteLine(" + Plug-in: " + header.Description);
                Debug.WriteLine(" + Header version: " + header.DspHeaderVersion);

                // enumrate modules
                List<DSPModule> modules = new List<DSPModule>();
                var GetModule = NativeMethods.GetDelegateFromFunctionPtr<NativeMethods.GetModuleDelegate>(header.GetModule);

                for (int i = 0; ; i++)
                {
                    IntPtr p = GetModule(i);
                    if (p == IntPtr.Zero) { break; }

                    Marshal.WriteIntPtr(new IntPtr(p.ToInt64() + 4), parentWindowHandle);
                    Marshal.WriteIntPtr(new IntPtr(p.ToInt64() + 8), hModule);
                    var module = DSPModuleImpl.FromModulePtr(p);
                    Debug.WriteLine(" + Module #" + (i + 1) + ": " + module.Description);
                    modules.Add(module);
                }

                plugin.Loaded = true;
                plugin.ModuleHandle = hModule;
                plugin.ModulePath = moduleFileName;
                plugin.DspHeaderVersion = header.DspHeaderVersion;
                plugin.Description = header.Description;
                plugin.Modules = modules.AsReadOnly();

                return plugin;
            }

            public void Release()
            {
                NativeMethods.FreeLibrary(ModuleHandle);
            }
        }

        /// <summary>
        /// DSPModule Implementation
        /// </summary>
        private class DSPModuleImpl : DSPModule
        {
            private DSPModuleImpl() { }

            public string Description { get; private set; }
            private IntPtr modulePtr;
            private NativeMethods.ConfigModuleDelegate ConfigFunc;
            private NativeMethods.InitModuleDelegate InitFunc;
            private NativeMethods.ModifySampleDelegate ModifySampleFunc;
            private NativeMethods.QuitModuleDelegate QuitFunc;

            public static DSPModule FromModulePtr(IntPtr pModule)
            {
                if (pModule == IntPtr.Zero)
                {
                    return null;
                }

                DSPModuleImpl module = new DSPModuleImpl();
                module.modulePtr = pModule;
                var m = NativeMethods.PtrToStructure<NativeMethods.winampDSPModule>(pModule);
                module.Description = m.Description;
                module.ConfigFunc = NativeMethods.GetDelegateFromFunctionPtr<NativeMethods.ConfigModuleDelegate>(m.Config) ?? (p => { });
                module.InitFunc = NativeMethods.GetDelegateFromFunctionPtr<NativeMethods.InitModuleDelegate>(m.Init) ?? (p => 0);
                module.ModifySampleFunc = NativeMethods.GetDelegateFromFunctionPtr<NativeMethods.ModifySampleDelegate>(m.ModifySample) ?? ((IntPtr p, IntPtr a, int b, int c, int d, int e) => 0);
                module.QuitFunc = NativeMethods.GetDelegateFromFunctionPtr<NativeMethods.QuitModuleDelegate>(m.Quit) ?? (p => { });
                return module;
            }

            public void Config()
            {
                ConfigFunc(modulePtr);
            }

            public int Init()
            {
                return InitFunc(modulePtr);
            }

            public int ModifySample(IntPtr pSamples, int nSamples, int bitsPerSample, int channels, int samplingRate)
            {
                return ModifySampleFunc(modulePtr, pSamples, nSamples, bitsPerSample, channels, samplingRate);
            }

            public void Quit()
            {
                QuitFunc(modulePtr);
            }
        };

        /// <summary>
        /// Win32 Native Methods and misc.
        /// </summary>
        static class NativeMethods
        {
            #region Kernel32
            [DllImport("Kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern IntPtr LoadLibrary(string lpFileName);
            [DllImport("Kernel32", SetLastError = true)]
            public static extern bool FreeLibrary(IntPtr hModule);
            [DllImport("Kernel32", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = false)]
            public static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

            public static T GetDelegateFromFunctionPtr<T>(IntPtr p) where T : class
            {
                return p != IntPtr.Zero ? (T)(Object)Marshal.GetDelegateForFunctionPointer(p, typeof(T)) : null;
            }

            public static T GetProcAddress<T>(IntPtr hModule, string lpProcName) where T : class
            {
                return GetDelegateFromFunctionPtr<T>(GetProcAddress(hModule, lpProcName));
            }

            public static T PtrToStructure<T>(IntPtr p) where T : struct
            {
                return (T)Marshal.PtrToStructure(p, typeof(T));
            }
            #endregion

            #region Winamp SDK
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            public delegate IntPtr winampDSPGetHeader2Delegate(IntPtr parentWindow);

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            public delegate IntPtr GetModuleDelegate(int index);

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            public delegate void ConfigModuleDelegate(IntPtr pModule);

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            public delegate int InitModuleDelegate(IntPtr pModule);

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            public delegate int ModifySampleDelegate(IntPtr pModule, IntPtr pSamples, int nSamples, int bitsPerSample, int channels, int samplingRate);

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            public delegate void QuitModuleDelegate(IntPtr pModule);

            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct winampDSPHeaderEx
            {
                public int DspHeaderVersion;
                [MarshalAs(UnmanagedType.LPStr)]
                public string Description;
                public IntPtr GetModule;
            }

            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct winampDSPModule
            {
                [MarshalAs(UnmanagedType.LPStr)]
                public string Description;
                public IntPtr ParentWindowHandle;
                public IntPtr DllInstanceHandle;
                public IntPtr Config;
                public IntPtr Init;
                public IntPtr ModifySample;
                public IntPtr Quit;
            }

            #endregion
        }
    }

#if EXAMPLE_CODE
    namespace Test
    {
        using System.Diagnostics;
        using System.Runtime.InteropServices;
        using Tsukikage.Audio;
        using Tsukikage.DllPInvoke.WinampDspPlugin;
        using Tsukikage.WinMM.WaveIO;

        class TestClass
        {
            public static void Test(string[] args)
            {
                Test_EnumrateInstalledPlugins();
                Test_PlayWithSATools();
            }

            /// <summary>
            /// Test
            /// </summary>
            static void Test_EnumrateInstalledPlugins()
            {
                //Debug.Listeners.Add(new ConsoleTraceListener());

                // Create ParentWindow
                IntPtr parentHandle = IntPtr.Zero;
                /*
                MessageWindow f = new MessageWindow();
                f.MessageHandlers.Add(0x400, (ref Message m) => { m.Result = new IntPtr(1); });
                Application.DoEvents();
                IntPtr parentHandle = f.Handle;
                */

                // find all installed plugins.
                var ProgramFilesPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                var dllFiles = Directory.GetFiles(ProgramFilesPath + "/Winamp/Plugins", "dsp_*.dll");

                // foreach plugin dll...
                foreach (var dllPath in dllFiles)
                {
                    Console.WriteLine("Path: " + dllPath);

                    // Since the window f does not provide Winamp APIs,
                    // passing f.Handle will cause crash in loading dsp_sps.dll.
                    // dsp_sps requires IPC_GET_API_SERVICE.
                    DSPPlugin dsp = DSPPluginLoader.LoadDSPPlugin(dllPath, parentHandle);

                    if (dsp == null)
                    {
                        Console.WriteLine(" - Error.");
                        continue;
                    }

                    Console.WriteLine(" + Plug-in: " + dsp.Description);
                    Console.WriteLine(" + Header version: " + dsp.DspHeaderVersion);

                    for (int i = 0; i < dsp.Modules.Count; i++)
                    {
                        Console.WriteLine(" + Module #" + (i + 1) + ": " + dsp.Modules[i].Description);
                    }

                    dsp.Release();
                }
            }

            /// <summary>
            /// Test
            /// </summary>
            static void Test_PlayWithSATools()
            {
                string audioFileName = "./test.ogg";
                string dspPluginDllName = "dsp_stereo_tool.dll";

                // Load plugin
                var ProgramFilesPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                var pluginDllPath = Path.Combine(ProgramFilesPath, "Winamp/Plugins/" + dspPluginDllName);
                Console.WriteLine("Loading: " + pluginDllPath);
                DSPPlugin dsp = DSPPluginLoader.LoadDSPPlugin(pluginDllPath, IntPtr.Zero);
                if (dsp == null || dsp.Modules == null || dsp.Modules.Count == 0)
                {
                    Console.WriteLine(" - Error on LoadDSPPlugin(...)");
                    return;
                }

                DSPModule mod = dsp.Modules[0];
                Console.WriteLine(" + Plug-in: " + dsp.Description);
                Console.WriteLine(" + Header version: " + dsp.DspHeaderVersion);
                Console.WriteLine(" + Module: " + mod.Description);

                // Initialize Plugin
                int initResult = mod.Init();
                if (initResult != 0)
                {
                    Console.WriteLine(" - Error on mod.Init(...)");
                    return;
                }

                using (OggDecodeStream waveStream = new OggDecodeStream(File.OpenRead(audioFileName)))
                using (WaveOut waveOut = new WaveOut(-1, waveStream.SamplesPerSecond, waveStream.BitsPerSample, waveStream.Channels))
                {
                    var bytesPerSample = waveStream.BitsPerSample / 8 * waveStream.Channels;

                    // Create waveform buffer (and get pinned pointer)
                    byte[] buffer = new byte[bytesPerSample * 4096];
                    GCHandle bufferHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                    IntPtr pBuffer = bufferHandle.AddrOfPinnedObject();

                    while (true)
                    {
                        // read (to half of buffer)
                        int read = waveStream.Read(buffer, 0, buffer.Length / 2);
                        if (read == 0) { break; }

                        // modify by dsp
                        int samplesWritten = mod.ModifySample(pBuffer,
                            read / bytesPerSample, waveStream.BitsPerSample,
                            waveStream.Channels, waveStream.SamplesPerSecond);

                        // write
                        waveOut.Write(buffer, 0, samplesWritten * bytesPerSample);
                        while (waveOut.EnqueuedBufferSize >= buffer.Length * 4)
                        {
                            System.Windows.Forms.Application.DoEvents();
                            System.Threading.Thread.Sleep(1);
                        }
                    }

                    // release buffer
                    pBuffer = IntPtr.Zero;
                    bufferHandle.Free();
                }

                mod.Quit();
                dsp.Release();
            }
        }
    }
#endif
}
