using System;
using System.Runtime.InteropServices;

namespace Tsukikage.DirectShow
{
    using ComInterop;

    /// <summary>
    /// ぐらふびるだ
    /// </summary>
    public abstract class GraphBase :IDisposable
    {
        public bool RegisterToROT { get; set; }

        string path;
        RunningObjectTableEntry RotEntry;

        public IGraphBuilder GraphBuilder { get; private set; }
        public IMediaControl MediaControl { get; private set; }
        public IMediaSeeking MediaSeeking { get; private set; }
        public IMediaFilter MediaFilter { get; private set; }
        public IMediaEventEx MediaEventEx { get; private set; }

        public event EventHandler MediaComplete;
        public event EventHandler MediaStart;
        public event EventHandler MediaPause;
        public event EventHandler MediaStop;
        public event EventHandler MediaSeek;

        /// <summary>
        /// ぐらふびるだ
        /// </summary>
        /// <param name="path"></param>
        public GraphBase(string path)
        {
            this.path = path;
        }

        Tsukikage.Windows.Messaging.MessageThread thread;
        Tsukikage.Windows.Messaging.MessageWindow window;

        public void Load()
        {
            GraphBuilder = new FilgraphManager() as IGraphBuilder;

            MediaControl = GraphBuilder as IMediaControl;
            MediaSeeking = GraphBuilder as IMediaSeeking;
            MediaEventEx = GraphBuilder as IMediaEventEx;
            MediaFilter = GraphBuilder as IMediaFilter;

            thread = new Tsukikage.Windows.Messaging.MessageThread(true);
            thread.Invoke(m => { window = new Tsukikage.Windows.Messaging.MessageWindow(); });
            
            const int WM_APP_MEDIA_EVENT = 0x8001;
            window.MessageHandlers[WM_APP_MEDIA_EVENT] = m =>
            {
                int ev, p1, p2;
                while (MediaEventEx.GetEvent(out ev, out p1, out p2, 0) == 0)
                {
                    if (ev == 0x01 && MediaComplete != null)
                        MediaComplete(this, EventArgs.Empty);
                    MediaEventEx.FreeEventParams(ev, p1, p2);
                }
            };
            MediaEventEx.SetNotifyWindow(window.Handle, WM_APP_MEDIA_EVENT, 0);

            BuildGraph(path);

            if (RegisterToROT)
                RotEntry = new RunningObjectTableEntry(GraphBuilder, "FilterGraph");

            MediaControl.Stop();
        }

        public void Release()
        {
            if (GraphBuilder != null)
            {
                MediaControl.Stop();
                ReleaseGraph();

                MediaEventEx.SetNotifyWindow(IntPtr.Zero, 0, 0);
                window.Dispose(); window = null;
                thread.Dispose(); thread = null;

                Marshal.ReleaseComObject(MediaControl); MediaControl = null;
                Marshal.ReleaseComObject(MediaSeeking); MediaSeeking = null;
                Marshal.ReleaseComObject(MediaEventEx); MediaEventEx = null;
                Marshal.ReleaseComObject(MediaFilter); MediaFilter = null;
                Marshal.ReleaseComObject(GraphBuilder); GraphBuilder = null;
            }

            if (RotEntry != null)
            {
                RotEntry.Revoke();
                RotEntry = null;
            }
        }

        /// <summary>
        /// オーバーライドしてグラフを構築します。
        /// </summary>
        protected abstract void BuildGraph(string path);

        /// <summary>
        /// オーバーライドしてグラフを解放します。
        /// </summary>
        protected abstract void ReleaseGraph();

        /// <summary>
        /// 現在の状態
        /// </summary>
        public GraphState Status { get; private set; }

        public bool IsPlaying { get { CheckAlive(); return Status == GraphState.Playing; } }
        public bool IsPaused { get { CheckAlive(); return Status == GraphState.Paused; } }
        public bool IsStopped { get { CheckAlive(); return Status == GraphState.Stopped; } }

        /// <summary>
        /// 再生
        /// </summary>
        public void Play()
        {
            CheckAlive();
            Status = GraphState.Playing;
            MediaControl.Run();
            if (MediaStart != null) MediaStart(this, EventArgs.Empty);
        }

        /// <summary>
        /// 一時停止
        /// </summary>
        public void Pause()
        {
            CheckAlive();
            Status = GraphState.Paused;
            MediaControl.Pause();
            if (MediaPause != null) MediaPause(this, EventArgs.Empty);
        }

        /// <summary>
        /// 停止
        /// </summary>
        public void Stop()
        {
            CheckAlive();
            Status = GraphState.Stopped;
            MediaControl.Stop();
            long pos = 0;
            MediaSeeking.SetPositions(ref pos, AMSeekingSeekingFlags.AbsolutePositioning, ref pos, AMSeekingSeekingFlags.NoPositioning);
            MediaControl.StopWhenReady();
            if (MediaStop != null) MediaStop(this, EventArgs.Empty);
        }

        /// <summary>
        /// 現在位置。単位はミリ秒。
        /// </summary>
        public double CurrentPosition
        {
            get
            {
                CheckAlive();
                long time;
                MediaSeeking.GetCurrentPosition(out time);
                return (double)time / 10000.0; // ms
            }
            set
            {
                CheckAlive();
                long vl = (long)(value * 10000);
                MediaSeeking.SetPositions(ref vl, AMSeekingSeekingFlags.AbsolutePositioning, ref vl, AMSeekingSeekingFlags.NoPositioning);
                if (MediaSeek != null) MediaSeek(this, EventArgs.Empty);
            }
        }

        public void Dispose()
        {
            Release();
            GC.SuppressFinalize(this);
        }

        protected void CheckAlive()
        {
            if (GraphBuilder == null)
                throw new InvalidOperationException("The object is not loaded.");
        }

        #region Utilities

        public class Util
        {
            const int S_OK = 0;

            /// <summary>
            /// 指定したピンが指定したMediaTypeをサポートするか？
            /// </summary>
            /// <param name="pin">ピン</param>
            /// <param name="major">majorType</param>
            /// <param name="sub">subType or 検証不要な場合 Guid.Empty</param>
            /// <returns>サポートしてる場合true</returns>
            public static bool IsPinSupportsMediaType(IPin pin, Guid major, Guid sub)
            {
                bool found = false;
                IEnumMediaTypes enumerator = null;
                try
                {
                    pin.EnumMediaTypes(out enumerator);
                    IntPtr pMedia = IntPtr.Zero;
                    AMMediaType media = new AMMediaType();

                    found = false;
                    enumerator.Reset();
                    while (!found && enumerator.Next(1, out pMedia, IntPtr.Zero) == S_OK)
                    {
                        if (pMedia == IntPtr.Zero) continue;
                        Marshal.PtrToStructure(pMedia, media);

                        if (media.majorType == major && (sub == Guid.Empty || media.subType != sub))
                            found = true;

                        Util.FreeMediaType(media);
                        Marshal.FreeCoTaskMem(pMedia);
                    }
                }
                finally
                {
                    if (enumerator != null)
                        Marshal.ReleaseComObject(enumerator);
                    enumerator = null;
                }
                return found;
            }

            /// <summary>
            /// 指定したピンが指定したMediaTypeをサポートするか？
            /// </summary>
            /// <param name="pin">ピン</param>
            /// <param name="major">majorType</param>
            /// <returns>サポートしてる場合true</returns>
            public static bool IsPinSupportsMediaType(IPin pin, Guid majorMediaType)
            {
                return IsPinSupportsMediaType(pin, majorMediaType, Guid.Empty);
            }

            /// <summary>
            /// 指定したピンはOutputピンか？
            /// </summary>
            /// <param name="pin">ピン</param>
            /// <returns>Outputピンである場合true</returns>
            public static bool IsOutputPin(IPin pin)
            {
                PinDirection dir;
                pin.QueryDirection(out dir);
                return dir == PinDirection.Output;
            }

            /// <summary>
            /// 指定したピンはInputピンか？
            /// </summary>
            /// <param name="pin">ピン</param>
            /// <returns>Inputピンである場合true</returns>
            public static bool IsInputPin(IPin pin)
            {
                PinDirection dir;
                pin.QueryDirection(out dir);
                return dir == PinDirection.Input;
            }

            /// <summary>

            /// 指定したフィルタから指定した条件を満たすピンを探し、最初に見つかったピンを返す。
            /// </summary>
            /// <param name="filter">フィルタ</param>
            /// <param name="pred">条件</param>
            /// <returns>最初に見つかったピン</returns>
            public static IPin FindPin(IBaseFilter filter, Predicate<IPin> pred)
            {
                IPin result = null;

                IEnumPins enumerator = null;
                try
                {
                    filter.EnumPins(out enumerator);
                    enumerator.Reset();

                    IPin pin = null;
                    try
                    {
                        while (enumerator.Next(1, out pin, IntPtr.Zero) == S_OK)
                        {
                            if (pin != null)
                            {
                                if (pred(pin))
                                {
                                    result = pin;
                                    pin = null;
                                    break;
                                }
                                Marshal.ReleaseComObject(pin);
                                pin = null;
                            }
                        }
                    }
                    finally
                    {
                        if (pin != null)
                            Marshal.ReleaseComObject(pin);
                    }
                }
                finally
                {
                    if (enumerator != null)
                        Marshal.ReleaseComObject(enumerator);
                    enumerator = null;
                }
                return result;
            }

            /// <summary>
            /// 指定したフィルタのInputピンのうち最初に見つかったものを返す
            /// </summary>
            /// <param name="filter">フィルタ</param>
            /// <returns>見つかったピン</returns>
            public static IPin FindInputPin(IBaseFilter filter)
            {
                return FindPin(filter, IsInputPin);
            }

            /// <summary>
            /// 指定したフィルタのOutputピンのうち最初に見つかったものを返す
            /// </summary>
            /// <param name="filter">フィルタ</param>
            /// <returns>見つかったピン</returns>
            public static IPin FindOutputPin(IBaseFilter filter)
            {
                return FindPin(filter, IsOutputPin);
            }

            /// <summary>
            /// 指定したフィルタの指定したmediaTypeをサポートするInputピンのうち最初に見つかったものを返す
            /// </summary>
            /// <param name="filter">フィルタ</param>
            /// <param name="mediaType">mediaType</param>
            /// <returns>見つかったピン</returns>
            public static IPin FindInputPin(IBaseFilter filter, Guid mediaType)
            {
                return FindPin(filter, pin => IsInputPin(pin) && IsPinSupportsMediaType(pin, mediaType));
            }

            /// <summary>
            /// 指定したフィルタの指定したmediaTypeをサポートするOutputピンのうち最初に見つかったものを返す
            /// </summary>
            /// <param name="filter">フィルタ</param>
            /// <param name="mediaType">mediaType</param>
            /// <returns>見つかったピン</returns>
            public static IPin FindOutputPin(IBaseFilter filter, Guid mediaType)
            {
                return FindPin(filter, pin => IsOutputPin(pin) && IsPinSupportsMediaType(pin, mediaType));
            }

            /// <summary>
            /// AMMediaTypeオブジェクトを解放する
            /// </summary>
            /// <param name="media">解放するオブジェクト</param>
            public static void FreeMediaType(AMMediaType media)
            {
                if (media.pUnk != IntPtr.Zero)
                {
                    Marshal.Release(media.pUnk);
                    media.pUnk = IntPtr.Zero;
                }

                if (media.cbFormat != 0)
                {
                    Marshal.FreeCoTaskMem(media.pbFormat);
                    media.cbFormat = 0;
                    media.pbFormat = IntPtr.Zero;
                }
            }

            public static void FreePin(IPin pin)
            {
                Marshal.ReleaseComObject(pin);
            }
        }
        #endregion
    }

    /// <summary>
    /// グラフの状態を表します。
    /// </summary>
    public enum GraphState
    {
        Stopped = FilterState.Stopped,
        Paused = FilterState.Paused,
        Playing = FilterState.Running,
    }
}
