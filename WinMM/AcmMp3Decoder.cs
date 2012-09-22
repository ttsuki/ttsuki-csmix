
using System;
using System.Runtime.InteropServices;

namespace Tsukikage.WinMM.AcmMP3Decoder
{
    /// <summary>
    /// Audio codec manager MP3 decoder.
    /// ACMを使ってMP3をデコードします。
    /// </summary>
    [System.Security.SuppressUnmanagedCodeSecurity]
    public class AcmMp3Decoder : IDisposable
    {
        IntPtr deviceHandle;
        int maxBufferSize;

        // tabenokoshi
        byte[] buffer = new byte[0];

        /// <summary>
        /// Open Audio Codec Manager MP3 decoder.
        /// ACMを使ってMP3をデコードします。
        /// </summary>
        public AcmMp3Decoder()
            : this(44100) { }

        /// <summary>
        /// Open Audio Codec Manager MP3 decoder.
        /// ACMを使ってMP3をデコードする。
        /// </summary>
        /// <param name="maxBufferSize">Max decode bytes per calling Decode.</param>
        public AcmMp3Decoder(int maxBufferSize)
        {
            this.maxBufferSize = maxBufferSize;

            Win32.MPEGLayer3WaveFormat src = new Win32.MPEGLayer3WaveFormat(44100, 16, 2);
            Win32.WaveFormatEx dst = new Win32.WaveFormatEx(44100, 16, 2);
            int mmret = Win32.acmFormatSuggest(IntPtr.Zero, ref src, ref dst, (uint)Win32.WaveFormatEx.SizeOfWaveFormatEx, Win32.ACM_FORMATSUGGESTF_WFORMATTAG);
            if (mmret != 0)
                throw new Exception("このマシン、MP3デコードできないって言ってます (" + mmret + ")");

            mmret = Win32.acmStreamOpen(out deviceHandle, IntPtr.Zero, ref src, ref dst, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0);
            if (mmret != 0)
                throw new Exception("デバイスが開けませんでした。(" + mmret + ")");
        }

        /// <summary>
        /// Decode stream and get PCM waveform.
        /// MP3ストリームをデコードして、PCMを得ます。
        /// </summary>
        /// <param name="mp3data">mp3 stream</param>
        /// <returns>pcm waveform</returns>
        public byte[] Decode(byte[] mp3data)
        {
            if (deviceHandle == IntPtr.Zero) 
                throw new InvalidOperationException("開いてないんだけど！");

            byte[] input = new byte[buffer.Length + mp3data.Length];
            Array.Copy(buffer, input, buffer.Length);
            Array.Copy(mp3data, 0, input, buffer.Length, mp3data.Length);

            uint size;

            Win32.acmStreamSize(deviceHandle, (uint)input.Length, out size, 1);
            byte[] output = new byte[maxBufferSize];

            GCHandle hIn = GCHandle.Alloc(input, GCHandleType.Pinned);
            GCHandle hOut = GCHandle.Alloc(output, GCHandleType.Pinned);

            IntPtr pHeader = Marshal.AllocHGlobal(Win32.SizeOfAcmStreamHeader);

            Win32.AcmStreamHeader ash = new Win32.AcmStreamHeader();
            ash.cbStruct = (uint)Win32.SizeOfAcmStreamHeader;
            ash.pbSrc = hIn.AddrOfPinnedObject();
            ash.cbSrcLength = (uint)input.Length;
            ash.cbSrcLengthUsed = 0;
            ash.pbDst = hOut.AddrOfPinnedObject();
            ash.cbDstLength = (uint)output.Length;
            ash.cbDstLengthUsed = 0;
            Marshal.StructureToPtr(ash, pHeader, true);

            Win32.acmStreamPrepareHeader(deviceHandle, pHeader, 0);
            Win32.acmStreamConvert(deviceHandle, pHeader, 0);
            Win32.acmStreamUnprepareHeader(deviceHandle, pHeader, 0);

            ash = (Win32.AcmStreamHeader)Marshal.PtrToStructure(pHeader, typeof(Win32.AcmStreamHeader));
            hIn.Free();
            hOut.Free();

            Array.Resize<byte>(ref buffer, (int)(ash.cbSrcLength - ash.cbSrcLengthUsed));
            Array.Copy(input, ash.cbSrcLengthUsed, buffer, 0, ash.cbSrcLength - ash.cbSrcLengthUsed);

            byte[] decodedSamples = new byte[ash.cbDstLengthUsed];
            Array.Copy(output, decodedSamples, decodedSamples.Length);

            return decodedSamples;
        }

        /// <summary>
        /// Close stream and release all resources.
        /// ストリームを閉じてすべてのリソースを解放します。
        /// </summary>
        /// <returns></returns>
        public byte[] Close()
        {
            if (deviceHandle == IntPtr.Zero)
                return new byte[0];

            byte[] decodedSamples = Decode(new byte[0]);

            Win32.acmStreamClose(deviceHandle);
            deviceHandle = IntPtr.Zero;
            GC.SuppressFinalize(this);
            return decodedSamples;
        }

        void IDisposable.Dispose()
        {
            Close();
        }

        [System.Security.SuppressUnmanagedCodeSecurity]
        class Win32
        {
            public const int MMSYSERR_NOERROR = 0;

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
            public struct WaveFormatEx
            {
                public short wFormatTag;
                public short nChannels;
                public int nSamplesPerSec;
                public int nAvgBytesPerSec;
                public short nBlockAlign;
                public short wBitsPerSample;
                public short cbSize;

                public WaveFormatEx(int SamplesPerSec, int BitsPerSample, int Channels)
                {
                    wFormatTag = (short)WAVE_FORMAT_PCM;
                    nSamplesPerSec = SamplesPerSec;
                    nChannels = (short)Channels;
                    wBitsPerSample = (short)BitsPerSample;
                    nBlockAlign = (short)(Channels * BitsPerSample >> 3);
                    nAvgBytesPerSec = SamplesPerSec * nBlockAlign;
                    cbSize = 0;
                }

                public static int WAVE_FORMAT_PCM { get { return 1; } }
                public static int SizeOfWaveFormatEx { get { return Marshal.SizeOf(typeof(WaveFormatEx)); } }
            }

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack=1)]
            public struct MPEGLayer3WaveFormat
            {
                public WaveFormatEx wfx;
                public short wID;
                public uint fdwFlags;
                public short nBlockSize;
                public short nFramesPerBlock;
                public short nCodecDelay;

                public MPEGLayer3WaveFormat(int SamplesPerSec, int Kbps, int Channels)
                {
                    wfx.wFormatTag = (short)0x55; // WAVE_FORMAT_MPEGLAYER3
                    wfx.nSamplesPerSec = SamplesPerSec;
                    wfx.nChannels = (short)Channels;
                    wfx.wBitsPerSample = 0;
                    wfx.nBlockAlign = 1;
                    wfx.nAvgBytesPerSec = Kbps * 1000 / 8;
                    wfx.cbSize = 12;
                    wID = 1;
                    fdwFlags = 2; // MPEGLAYER3_FLAG_PADDING_OFF
                    nBlockSize = (short)(720 * Kbps / 441);
                    nFramesPerBlock = 1;
                    nCodecDelay = 0x571;
                }

                public static int SizeOfMPEGLayer3WaveFormat { get { return Marshal.SizeOf(typeof(MPEGLayer3WaveFormat)); } }
            }

            public static readonly int SizeOfAcmStreamHeader = Marshal.SizeOf(typeof(AcmStreamHeader));
            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
            public struct AcmStreamHeader
            {
                public uint cbStruct;
                public uint fdwStatus;
                public IntPtr dwUser;
                public IntPtr pbSrc;
                public uint cbSrcLength;
                public uint cbSrcLengthUsed;
                public IntPtr dwSrcUser;
                public IntPtr pbDst;
                public uint cbDstLength;
                public uint cbDstLengthUsed;
                public IntPtr dwDstUser;
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
                public uint[] dwReservedDriver;
            }

            public const int ACM_FORMATSUGGESTF_WFORMATTAG = 0x00010000;

            [DllImport("msacm32.dll", CharSet = CharSet.Ansi)]
            public static extern int acmFormatSuggest(IntPtr had, IntPtr pwfxSrc, IntPtr pwfxDst, uint cbwfxDst, uint fdwSuggest);

            [DllImport("msacm32.dll", CharSet = CharSet.Ansi)]
            public static extern int acmFormatSuggest(IntPtr had, ref MPEGLayer3WaveFormat pwfxSrc, ref WaveFormatEx pwfxDst, uint cbwfxDst, uint fdwSuggest);

            [DllImport("msacm32.dll", CharSet = CharSet.Ansi)]
            public static extern int acmStreamOpen(out IntPtr phas, IntPtr had, IntPtr pwfxSrc, IntPtr pwfxDst, IntPtr pwfltr, IntPtr dwCallback, IntPtr dwInstance, uint fdwOpen);

            [DllImport("msacm32.dll", CharSet = CharSet.Ansi)]
            public static extern int acmStreamOpen(out IntPtr phas, IntPtr had, ref MPEGLayer3WaveFormat pwfxSrc, ref WaveFormatEx pwfxDst, IntPtr pwfltr, IntPtr dwCallback, IntPtr dwInstance, uint fdwOpen);

            [DllImport("msacm32.dll", CharSet = CharSet.Ansi)]
            public static extern int acmStreamPrepareHeader(IntPtr has, ref AcmStreamHeader pash, uint fdwPrepare);

            [DllImport("msacm32.dll", CharSet = CharSet.Ansi)]
            public static extern int acmStreamPrepareHeader(IntPtr has, IntPtr pash, uint fdwPrepare);

            [DllImport("msacm32.dll", CharSet = CharSet.Ansi)]
            public static extern int acmStreamConvert(IntPtr has, ref AcmStreamHeader pash, uint fdwPrepare);

            [DllImport("msacm32.dll", CharSet = CharSet.Ansi)]
            public static extern int acmStreamConvert(IntPtr has, IntPtr pash, uint fdwPrepare);

            [DllImport("msacm32.dll", CharSet = CharSet.Ansi)]
            public static extern int acmStreamUnprepareHeader(IntPtr has, ref AcmStreamHeader pash, uint fdwPrepare);

            [DllImport("msacm32.dll", CharSet = CharSet.Ansi)]
            public static extern int acmStreamUnprepareHeader(IntPtr has, IntPtr pash, uint fdwPrepare);

            [DllImport("msacm32.dll", CharSet = CharSet.Ansi)]
            public static extern int acmStreamClose(IntPtr has);

            [DllImport("msacm32.dll", CharSet = CharSet.Ansi)]
            public static extern int acmStreamSize(IntPtr has, uint cbInput, out uint pdwOutputBytes, uint fdwSize);
        }
    }
}