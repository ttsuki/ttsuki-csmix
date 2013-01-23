using System;
using System.Runtime.InteropServices;

namespace Tsukikage.DirectShow
{
    using ComInterop;

    /// <summary>
    /// VideoとAudioに対するSampleGrabberとNullRendererによるデコードグラフを提供します。
    /// </summary>
    public class SampleGrabberGraph : GraphBase
    {
        public ISampleGrabber VideoGrabber { get; private set; }
        public VideoInfoHeader VideoInfo { get; private set; }
        public bool HasVideo { get { return VideoInfo != null; } }
        public System.Drawing.Size VideoSize { get { return new System.Drawing.Size(VideoInfo.biWidth, VideoInfo.biHeight); } }
        public double VideoFPS { get { return 10000000.0 / VideoInfo.AvgTimePerFrame; } }

        public ISampleGrabber AudioGrabber { get; private set; }
        public WaveFormatEx AudioInfo { get; private set; }
        public bool HasAudio { get { return AudioInfo != null; } }
        public int AudioSamplePerSec { get { return AudioInfo.nSamplesPerSec; } }
        public int AudioBitsPerSample { get { return AudioInfo.wBitsPerSample; } }
        public int AudioChannels { get { return AudioInfo.nChannels; } }

        public delegate void FrameCallbackDelegate(double sampleTime, IntPtr pFrame, int bufferSize);

        /// <summary>
        /// ビデオフレームの準備ができると発生します。
        /// 読み出しには Marshal.Copy などを利用します。
        /// </summary>
        public event FrameCallbackDelegate VideoFrame;

        /// <summary>
        /// オーディオフレームの準備ができると発生します。
        /// 読み出しには Marshal.Copy などを利用します。
        /// </summary>
        public event FrameCallbackDelegate AudioFrame;

        /// <summary>
        /// VideoとAudioに対するSampleGrabberとNullRendererによるデコードグラフを提供します。
        /// </summary>
        /// <param name="path">ソースファイルへのパス</param>
        public SampleGrabberGraph(string path)
            : base(path)
        {
        }

        protected override void BuildGraph(string path)
        {
            // Build partial graph
            IPin videoOutput = null, audioOutput = null;
            {
                VideoRendererDefault videoRenderer = new VideoRendererDefault();
                DSoundRender audioRenderer = new DSoundRender();
                try
                {
                    GraphBuilder.AddFilter(videoRenderer as IBaseFilter, "Default Video Renderer");
                    GraphBuilder.AddFilter(audioRenderer as IBaseFilter, "Default Audio Renderer");

                    GraphBuilder.RenderFile(path, null);

                    // Get Connected Pins
                    {
                        IPin videoInput = Util.FindInputPin(videoRenderer as IBaseFilter);
                        videoInput.ConnectedTo(out videoOutput);
                        Util.FreePin(videoInput);

                        IPin audioInput = Util.FindInputPin(audioRenderer as IBaseFilter);
                        audioInput.ConnectedTo(out audioOutput);
                        Util.FreePin(audioInput);
                    }

                    GraphBuilder.RemoveFilter(videoRenderer as IBaseFilter);
                    GraphBuilder.RemoveFilter(audioRenderer as IBaseFilter);
                }
                finally
                {
                    Marshal.ReleaseComObject(videoRenderer);
                    Marshal.ReleaseComObject(audioRenderer);
                }
            }

            // build video grabber
            if (videoOutput != null)
            {
                this.VideoGrabber = BuildGrabber("Video", videoOutput,
                    MEDIATYPE_Video, MEDIASUBTYPE_RGB32, FORMAT_VideoInfo, OnVideoFrame) as ISampleGrabber;
                this.VideoInfo = GetMediaFormat<VideoInfoHeader>(VideoGrabber);
                Marshal.ReleaseComObject(videoOutput);
            }

            // build audio grabber
            if (audioOutput != null)
            {
                this.AudioGrabber = BuildGrabber("Audio", audioOutput,
                    MEDIATYPE_Audio, MEDIASUBTYPE_PCM, FORMAT_WaveFormatEx, OnAudioFrame) as ISampleGrabber;
                this.AudioInfo = GetMediaFormat<WaveFormatEx>(AudioGrabber);
                Marshal.ReleaseComObject(audioOutput);
            }
        }

        private SampleGrabber BuildGrabber(string filterNamePrefix, IPin srcOutputPin,
            Guid majorType, Guid subType, Guid formatType, SampleGrabberCallback.BufferCBEventHandler callback)
        {
            // Create Filter
            SampleGrabber sampleGrabber = CreateSampleGrabber(majorType, subType, formatType, callback);
            NullRenderer nullRenderer = CreateNullRenderer();
            
            // Add Filter
            GraphBuilder.AddFilter(sampleGrabber as IBaseFilter, filterNamePrefix + " Sample Grabber");
            GraphBuilder.AddFilter(nullRenderer as IBaseFilter, filterNamePrefix + " Null Renderer");

            // Connect srcOutput -> grabberInput, grabberOutput -> rendererInput
            IPin grabberIn = Util.FindInputPin(sampleGrabber as IBaseFilter);
            IPin grabberOut = Util.FindOutputPin(sampleGrabber as IBaseFilter);
            IPin rendererIn = Util.FindInputPin(nullRenderer as IBaseFilter);
            GraphBuilder.Connect(srcOutputPin, grabberIn);
            GraphBuilder.Connect(grabberOut, rendererIn);
            Util.FreePin(rendererIn);
            Util.FreePin(grabberOut);
            Util.FreePin(grabberIn);

            Marshal.ReleaseComObject(nullRenderer);
            return sampleGrabber;
        }

        protected override void ReleaseGraph()
        {
            if (VideoGrabber != null)
            {
                Marshal.ReleaseComObject(VideoGrabber);
                VideoGrabber = null;
            }

            if (AudioGrabber != null)
            {
                Marshal.ReleaseComObject(AudioGrabber);
                AudioGrabber = null;
            }
        }

        /// <summary>
        /// VideoFrameイベントを発生させます。
        /// </summary>
        /// <param name="sampleTime"></param>
        /// <param name="pBuffer"></param>
        /// <param name="bufferLength"></param>
        /// <returns></returns>
        protected virtual int OnVideoFrame(double sampleTime, IntPtr pBuffer, int bufferLength)
        {
            if (VideoFrame != null)
                VideoFrame(sampleTime, pBuffer, bufferLength);
            return 0;
        }

        /// <summary>
        /// AudioFrameイベントを発生させます。
        /// </summary>
        /// <param name="sampleTime"></param>
        /// <param name="pBuffer"></param>
        /// <param name="bufferLength"></param>
        /// <returns></returns>
        protected virtual int OnAudioFrame(double sampleTime, IntPtr pBuffer, int bufferLength)
        {
            if (AudioFrame != null)
                AudioFrame(sampleTime, pBuffer, bufferLength);
            return 0;
        }

        private class SampleGrabberCallback : ISampleGrabberCB
        {
            public delegate int BufferCBEventHandler(double sampleTime, IntPtr pBuffer, int bufferLength);
            const int E_NOTIMPL = unchecked((int)0x80004001);
            public BufferCBEventHandler OnBuffer = delegate { return E_NOTIMPL; };

            //public delegate int SampleCBEventHandler(double sampleTime, IntPtr sample);
            //public SampleCBEventHandler OnSample = delegate { return E_NOTIMPL; };

            public int BufferCB(double SampleTime, IntPtr pBuffer, int BufferLen)
            {
                return OnBuffer(SampleTime, pBuffer, BufferLen);
            }

            public int SampleCB(double SampleTime, IntPtr pSample)
            {
                // return OnSample(SampleTime, pSample);
                return E_NOTIMPL;
            }
        }

        private static readonly Guid MEDIATYPE_Video = new Guid("73646976-0000-0010-8000-00AA00389B71");
        private static readonly Guid MEDIATYPE_Audio = new Guid("73647561-0000-0010-8000-00AA00389B71");
        private static readonly Guid MEDIASUBTYPE_RGB32 = new Guid("E436EB7E-524F-11CE-9F53-0020AF0BA770");
        private static readonly Guid MEDIASUBTYPE_PCM = new Guid("00000001-0000-0010-8000-00AA00389B71");
        private static readonly Guid FORMAT_VideoInfo = new Guid("05589F80-C356-11CE-BF01-00AA0055595A");
        private static readonly Guid FORMAT_WaveFormatEx = new Guid("05589F81-C356-11CE-BF01-00AA0055595A");

        private static SampleGrabber CreateSampleGrabber(Guid majorType, Guid subType, Guid formatType,
            SampleGrabberGraph.SampleGrabberCallback.BufferCBEventHandler callback)
        {
            SampleGrabber sampleGrabber = new SampleGrabber();
            ISampleGrabber grabber = sampleGrabber as ISampleGrabber;
            grabber.SetMediaType(new AMMediaType { majorType = majorType, subType = subType, formatType = formatType });
            grabber.SetBufferSamples(false);
            grabber.SetOneShot(false);
            grabber.SetCallback(new SampleGrabberCallback() { OnBuffer = callback }, 1); // 0 = Sample, 1 = Buffer
            return sampleGrabber;
        }

        private static TFormat GetMediaFormat<TFormat>(ISampleGrabber sampleGrabber)
        {
            TFormat format = default(TFormat);
            AMMediaType media = new AMMediaType();
            try
            {
                sampleGrabber.GetConnectedMediaType(media);
                if (media.cbFormat < Marshal.SizeOf(typeof(TFormat))) throw new ArgumentException();
                format = (TFormat)Marshal.PtrToStructure(media.pbFormat, typeof(TFormat));
            }
            finally
            {
                if (media != null) Util.FreeMediaType(media);
            }
            return format;
        }

        private static NullRenderer CreateNullRenderer()
        {
            return new NullRenderer();
        }

        #region util

        /// <summary>
        /// Copy videoframe in <paramref name="src"/> to RGB32bit buffer <paramref name="dst"/>.
        /// </summary>
        /// <param name="src"></param>
        /// <param name="dst"></param>
        /// <param name="dstWidth"></param>
        /// <param name="dstHeight"></param>
        public void RenderVideoFrameToTexture(IntPtr src, IntPtr dst, int dstWidth, int dstHeight)
        {
            //{
            //    Int64 pSrc = src.ToInt64();
            //    Int64 pDst = dst.ToInt64();
            //    int height = VideoInfo.biHeight;
            //    int loop = Math.Min(height, dstHeight);
            //    int dstPitch = dstWidth * 4;
            //    int srcPitch = Math.Min(VideoInfo.biWidth * 4, dstPitch);

            //    for (int i = 0; i < loop; i++)
            //        MoveMemory(new IntPtr(pDst + i * dstPitch), new IntPtr(pSrc + (height - i - 1) * srcPitch), srcPitch);
            //}

            unsafe
            {
                uint* pSrc = (uint*)src;
                uint* pDst = (uint*)dst;
                int height = VideoInfo.biHeight;
                int width = VideoInfo.biWidth;
                int yloop = Math.Min(height, dstHeight);
                int xloop = Math.Min(width, dstWidth);
                
                // set 255 to alpha channel...
                for (int y = 0; y < yloop; y++)
                    for (int x = 0; x < xloop; x++)
                        pDst[y * dstWidth + x] = 0xFF000000 | pSrc[(height - y - 1) * width + x];
            }
        }

        [DllImport("kernel32.dll")]
        [System.Security.SuppressUnmanagedCodeSecurity]
        static extern void MoveMemory(IntPtr dst, IntPtr src, int length);

        #endregion
    }
}
