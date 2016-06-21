using System;
using System.Runtime.InteropServices;
using System.Security;

namespace Tsukikage.DirectShow.ComInterop
{
    [ComImport]
    [Guid("E436EBB3-524F-11CE-9F53-0020AF0BA770")]
    public class FilgraphManager { }

    [ComImport]
    [Guid("C1F400A0-3F08-11D3-9F0B-006008039E37")]
    public class SampleGrabber { }

    [ComImport]
    [Guid("70E102B0-5556-11CE-97C0-00AA0055595A")]
    public class VideoRendererDefault { }

    [ComImport]
    [Guid("79376820-07D0-11CF-A24D-0020AFD79767")]
    public class DSoundRender { }

    [ComImport]
    [Guid("C1F400A4-3F08-11D3-9F0B-006008039E37")]
    public class NullRenderer { }

    [ComImport]
    [Guid("56A86899-0AD4-11CE-B03A-0020AF0BA770")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [SuppressUnmanagedCodeSecurity]
    public interface IMediaFilter
    {
        void GetClassID(out Guid pClassID);
        void Stop();
        void Pause();
        void Run(long tStart);
        void GetState(int dwMilliSecsTimeout, out FilterState State);
        void SetSyncSource(IntPtr pClock);
        void GetSyncSource(out IReferenceClock pClock);
    }

    [ComImport]
    [Guid("56A86895-0AD4-11CE-B03A-0020AF0BA770")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [SuppressUnmanagedCodeSecurity]
    public interface IBaseFilter
    {
        void GetClassID(out Guid pClassID);
        void Stop();
        void Pause();
        void Run(long tStart);
        void GetState(int dwMilliSecsTimeout, out FilterState State);
        void SetSyncSource(IReferenceClock pClock);
        void GetSyncSource(out  IReferenceClock pClock);
        void EnumPins(out IEnumPins ppEnum);
        void FindPin([MarshalAs(UnmanagedType.LPWStr)] string Id, out IPin ppPin);
        void QueryFilterInfo(/* out FilterInfo */ IntPtr pInfo);
        void JoinFilterGraph(IFilterGraph pGraph, [MarshalAs(UnmanagedType.LPWStr)] string pName);
        void QueryVendorInfo([MarshalAs(UnmanagedType.LPWStr)] out string pVendorInfo);
    }

    [ComImport]
    [Guid("56A86891-0AD4-11CE-B03A-0020AF0BA770")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [SuppressUnmanagedCodeSecurity]
    public interface IPin
    {
        void Connect(IPin pReceivePin, AMMediaType pmt);
        void ReceiveConnection(IPin pReceivePin, AMMediaType pmt);
        void Disconnect();
        [PreserveSig]
        int ConnectedTo(out IPin ppPin);
        void ConnectionMediaType([Out] AMMediaType pmt);
        void QueryPinInfo(out /* PinInfo */ IntPtr pInfo);
        void QueryDirection(out PinDirection pPinDir);
        void QueryId([MarshalAs(UnmanagedType.LPWStr)] out string Id);
        [PreserveSig]
        int QueryAccept(AMMediaType pmt);
        void EnumMediaTypes(out IEnumMediaTypes ppEnum);
        [PreserveSig]
        int QueryInternalConnections([Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] IPin[] ppPins, ref int nPin);
        void EndOfStream();
        void BeginFlush();
        void EndFlush();
        void NewSegment(long tStart, long tStop, double dRate);
    }

    [ComImport]
    [Guid("56A86893-0AD4-11CE-B03A-0020AF0BA770")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [SuppressUnmanagedCodeSecurity]
    public interface IEnumFilters
    {
        [PreserveSig]
        int Next(int cFilters, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)]	IBaseFilter[] ppFilter, IntPtr pcFetched);
        [PreserveSig]
        int Skip(int cFilters);
        void Reset();
        void Clone(out IEnumFilters ppEnum);
    }

    [ComImport]
    [Guid("56A86892-0AD4-11CE-B03A-0020AF0BA770")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [SuppressUnmanagedCodeSecurity]
    public interface IEnumPins
    {
        [PreserveSig]
        int Next(int cPins, [Out, MarshalAs(UnmanagedType.Interface)] out IPin ppPins, IntPtr pcFetched);
        [PreserveSig]
        int Skip(int cPins);
        void Reset();
        void Clone(out IEnumPins ppEnum);
    }

    [ComImport]
    [Guid("89C31040-846B-11CE-97D3-00AA0055595A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [SuppressUnmanagedCodeSecurity]
    public interface IEnumMediaTypes
    {
        [PreserveSig]
        int Next(int cMediaTypes, out IntPtr ppMediaTypes, IntPtr pcFetched);
        [PreserveSig]
        int Skip(int cMediaTypes);
        void Reset();
        void Clone(out IEnumMediaTypes ppEnum);
    }

    [ComImport]
    [Guid("56A8689F-0AD4-11CE-B03A-0020AF0BA770")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [SuppressUnmanagedCodeSecurity]
    public interface IFilterGraph
    {
        void AddFilter(IBaseFilter pFilter, [MarshalAs(UnmanagedType.LPWStr)] string pName);
        void RemoveFilter(IBaseFilter pFilter);
        void EnumFilters(out IEnumFilters ppEnum);
        void FindFilterByName([MarshalAs(UnmanagedType.LPWStr)] string pName, out IBaseFilter ppFilter);
        void ConnectDirect(IPin ppinOut, IPin ppinIn, ref AMMediaType pmt);
        void Reconnect(IPin ppin);
        void Disconnect(IPin ppin);
        void SetDefaultSyncSource();
    }

    [ComImport]
    [Guid("56A868A9-0AD4-11CE-B03A-0020AF0BA770")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [SuppressUnmanagedCodeSecurity]
    public interface IGraphBuilder
    {
        void AddFilter(IBaseFilter pFilter, [MarshalAs(UnmanagedType.LPWStr)] string pName);
        void RemoveFilter(IBaseFilter pFilter);
        void EnumFilters(out IEnumFilters ppEnum);
        void FindFilterByName([MarshalAs(UnmanagedType.LPWStr)] string pName, out IBaseFilter ppFilter);
        void ConnectDirect(IPin ppinOut, IPin ppinIn, ref AMMediaType pmt);
        void Reconnect(IPin ppin);
        void Disconnect(IPin ppin);
        void SetDefaultSyncSource();
        void Connect(IPin ppinOut, IPin ppinIn);
        void Render(IPin ppinOut);
        void RenderFile([MarshalAs(UnmanagedType.LPWStr)] string lpcwstrFile, [MarshalAs(UnmanagedType.LPWStr)] string lpcwstrPlayList);
        void AddSourceFilter([MarshalAs(UnmanagedType.LPWStr)] string lpcwstrFileName, [MarshalAs(UnmanagedType.LPWStr)] string lpcwstrFilterName, out IBaseFilter ppFilter);
        void SetLogFile(IntPtr hFile);
        void Abort();
        void ShouldOperationContinue();
    }

    [ComImport]
    [Guid("56A868B1-0AD4-11CE-B03A-0020AF0BA770")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    [SuppressUnmanagedCodeSecurity]
    public interface IMediaControl
    {
        void Run();
        void Pause();
        void Stop();
        void GetState(int msTimeout, out FilterState pfs);
        void RenderFile([MarshalAs(UnmanagedType.BStr)] string strFilename);
        void AddSourceFilter([MarshalAs(UnmanagedType.BStr)] string strFilename, [MarshalAs(UnmanagedType.IDispatch)] out object ppUnk);
        void get_FilterCollection([MarshalAs(UnmanagedType.Interface)] out object ppUnk);
        void get_RegFilterCollection([MarshalAs(UnmanagedType.Interface)] out object ppUnk);
        void StopWhenReady();
    }

    [ComImport]
    [Guid("36B73880-C2C8-11CF-8B46-00805F6CEF60")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [SuppressUnmanagedCodeSecurity]
    public interface IMediaSeeking
    {
        void GetCapabilities(out /* AMSeekingSeekingCapabilities */ int pCapabilities);
        void CheckCapabilities(ref /* AMSeekingSeekingCapabilities */ int pCapabilities);
        void IsFormatSupported([MarshalAs(UnmanagedType.LPStruct)] Guid pFormat);
        void QueryPreferredFormat(out Guid pFormat);
        void GetTimeFormat(out Guid pFormat);
        void IsUsingTimeFormat([MarshalAs(UnmanagedType.LPStruct)] Guid pFormat);
        void SetTimeFormat([MarshalAs(UnmanagedType.LPStruct)] Guid pFormat);
        void GetDuration(out long pDuration);
        void GetStopPosition(out long pStop);
        void GetCurrentPosition(out long pCurrent);
        void ConvertTimeFormat(out long pTarget, [MarshalAs(UnmanagedType.LPStruct)] Guid pTargetFormat, long Source, [MarshalAs(UnmanagedType.LPStruct)] Guid pSourceFormat);
        void SetPositions(ref long pCurrent, AMSeekingSeekingFlags dwCurrentFlags, ref long pStop, AMSeekingSeekingFlags dwStopFlags);
        void GetPositions(out long pCurrent, out long pStop);
        void GetAvailable(out long pEarliest, out long pLatest);
        void SetRate(double dRate);
        void GetRate(out double pdRate);
        void GetPreroll(out long pllPreroll);
    }

    [ComImport]
    [Guid("56A868C0-0AD4-11CE-B03A-0020AF0BA770")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    [SuppressUnmanagedCodeSecurity]
    public interface IMediaEventEx
    {
        void GetEventHandle(out IntPtr hEvent);
        [PreserveSig]
        int GetEvent(out int lEventCode, out int lParam1, out int lParam2, int msTimeout);
        void WaitForCompletion(int msTimeout, out int pEvCode);
        void CancelDefaultHandling(int lEvCode);
        void RestoreDefaultHandling(int lEvCode);
        [PreserveSig]
        int FreeEventParams(int lEvCode, int lParam1, int lParam2);

        void SetNotifyWindow(IntPtr hwnd, int lMsg, int lInstanceData);
        void SetNotifyFlags(int lNoNotifyFlags);
        void GetNotifyFlags(out int lplNoNotifyFlags);
    }

    [ComImport]
    [Guid("56A86897-0AD4-11CE-B03A-0020AF0BA770")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [SuppressUnmanagedCodeSecurity]
    public interface IReferenceClock
    {
        [PreserveSig]
        int GetTime(out long pTime);
        [PreserveSig]
        int AdviseTime(long baseTime, long streamTime, IntPtr hEvent, out int pdwAdviseCookie);
        [PreserveSig]
        int AdvisePeriodic(long startTime, long periodTime, IntPtr hSemaphore, out int pdwAdviseCookie);
        [PreserveSig]
        int Unadvise(int dwAdviseCookie);
    }

    [ComImport]
    [Guid("6B652FFF-11FE-4FCE-92AD-0266B5D7C78F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [SuppressUnmanagedCodeSecurity]
    public interface ISampleGrabber
    {
        void SetOneShot([MarshalAs(UnmanagedType.Bool)] bool oneShot);
        void SetMediaType(AMMediaType mediaType);
        void GetConnectedMediaType([Out]AMMediaType mediaType);
        void SetBufferSamples([MarshalAs(UnmanagedType.Bool)] bool bufferThem);
        void GetCurrentBuffer(ref int bufferSize, IntPtr buffer);
        void GetCurrentSample(IntPtr sample);
        void SetCallback(ISampleGrabberCB callback, int whichMethodToCallback);
    }

    [ComImport]
    [Guid("0579154A-2B53-4994-B0D0-E773148EFF85")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [SuppressUnmanagedCodeSecurity]
    public interface ISampleGrabberCB
    {
        [PreserveSig]
        int SampleCB(double sampleTime, IntPtr sample);

        [PreserveSig]
        int BufferCB(double sampleTime, IntPtr buffer, int bufferLen);
    }

    [StructLayout(LayoutKind.Sequential)]
    public class AMMediaType
    {
        public Guid majorType;
        public Guid subType;
        [MarshalAs(UnmanagedType.Bool)]
        public bool bFixedSizeSamples;
        [MarshalAs(UnmanagedType.Bool)]
        public bool bTemporalCompression;
        public uint lSampleSize;
        public Guid formatType;
        public IntPtr pUnk;
        public uint cbFormat;
        public IntPtr pbFormat;
    }

    [Flags]
    public enum AMSeekingSeekingFlags
    {
        NoPositioning = 0x00,
        AbsolutePositioning = 0x01,
        RelativePositioning = 0x02,
        IncrementalPositioning = 0x03,
        PositioningBitsMask = 0x03,
        SeekToKeyFrame = 0x04,
        ReturnTime = 0x08,
        Segment = 0x10,
        NoFlush = 0x20,
    }

    public enum FilterState
    {
        Stopped = 0,
        Paused = 1,
        Running = 2,
    }

    public enum PinDirection
    {
        Input = 0,
        Output = 1,
    }

    [StructLayout(LayoutKind.Sequential)]
    public class VideoInfoHeader
    {
        #region RECT rcSource
        public int rcSource_Left;
        public int rcSource_Top;
        public int rcSource_Right;
        public int rcSource_Bottom;
        #endregion
        #region RECT rcTarget
        public int rcTarget_Left;
        public int rcTarget_Top;
        public int rcTarget_Right;
        public int rcTarget_Bottom;
        #endregion
        public int dwBitRate;
        public int dwBitErrorRate;
        #region REFERENCE_TIME AvgTimePerFrame;
        public long AvgTimePerFrame;
        #endregion
        #region BITMAPINFOHEADER bmiHeader;
        public int biSize;
        public int biWidth;
        public int biHeight;
        public short biPlanes;
        public short biBitCount;
        public int biCompression;
        public int biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public int biClrUsed;
        public int biClrImportant;
        #endregion
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class WaveFormatEx
    {
        public ushort wFormatTag;
        public ushort nChannels;
        public int nSamplesPerSec;
        public int nAvgBytesPerSec;
        public ushort nBlockAlign;
        public ushort wBitsPerSample;
        public ushort cbSize;
    }
}
