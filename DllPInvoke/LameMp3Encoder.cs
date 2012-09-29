using System;
using System.Runtime.InteropServices;

namespace Tsukikage.DllPInvoke.LameMP3Encoder
{
    public class LameEncoder : IDisposable
    {
        uint stream = 0;
        uint samplesPerChunk, bufferSize;
        byte[] alreadyQueuedData = new byte[0];

        public int QueuedDataLength { get { return alreadyQueuedData.Length; } }
        public int SamplesPerChunk { get { return (int)samplesPerChunk; } }

        /// <summary>
        /// チャンクあたりのサンプル数を取得します
        /// </summary>
        /// <param name="samplingRate">サンプリングレート 32000 or 44100 or 48000</param>
        /// <param name="channels">チャンネル数 1 or 2 </param>
        /// <param name="bitRateInKBPS">ビットレート (kBps) 128 とか 192 とか 320 とか</param>
        /// <returns></returns>
        public static int GetSamplesPerChunk(int samplingRate, short channels, short bitRateInKBPS)
        {
            uint samplesPerChunk, bufferSize, stream;
            Native.BECONFIG conf = new Native.BECONFIG(samplingRate, channels, bitRateInKBPS);
            uint r = Native.beInitStream(ref conf, out samplesPerChunk, out bufferSize, out stream);
            if (r != 0) 
                throw new Exception("Lameの初期化に失敗したっす。(" + r + ")");
            Native.beCloseStream(stream);
            return (int)samplesPerChunk;
        }

        /// <summary>
        /// エンコーダを初期化します。
        /// </summary>
        /// <param name="samplingRate">サンプリングレート 32000 or 44100 or 48000</param>
        /// <param name="channels">チャンネル数 1 or 2 </param>
        /// <param name="bitRateInKBPS">ビットレート (kBps) 128 とか 192 とか 320 とか</param>
        public LameEncoder(int samplingRate, int channels, int bitRateInKBPS)
        {
            Native.BECONFIG conf = new Native.BECONFIG(samplingRate, channels, bitRateInKBPS);
            uint r = Native.beInitStream(ref conf, out samplesPerChunk, out bufferSize, out stream);

            if (r != 0)
                throw new Exception("Lameの初期化に失敗したっす。(" + r + ")");
        }

        /// <summary>
        /// Encode PCM to MP3.
        /// エンコードする。
        /// </summary>
        /// <param name="waveformStream">waveform</param>
        /// <returns>mp3chunks</returns>
        /// <remarks>
        /// 入力されるデータは SamplesPerChunk 単位になる。
        /// 単位に満たなかったサンプルはEncoder内部でバッファされ、次のEncode時に使われる。
        /// </remarks>
        public byte[] Encode(byte[] waveformStream)
        {
            if (stream == 0) 
                throw new InvalidOperationException("開いてないんだけど！");

            byte[] inputBuffer = new byte[alreadyQueuedData.Length + waveformStream.Length];
            Array.Copy(alreadyQueuedData, inputBuffer, alreadyQueuedData.Length);
            Array.Copy(waveformStream, 0, inputBuffer, alreadyQueuedData.Length, waveformStream.Length);
            
            GCHandle hIn = GCHandle.Alloc(inputBuffer, GCHandleType.Pinned);
            IntPtr pIn = hIn.AddrOfPinnedObject();

            int bytesPerChunk = (int)samplesPerChunk * 2;
            int inputSamples = inputBuffer.Length / 2;
            int loops = inputBuffer.Length / bytesPerChunk;
            int restbyte = inputBuffer.Length % bytesPerChunk;

            byte[] output = new byte[bufferSize * loops];
            uint written = 0;
            GCHandle hOut = GCHandle.Alloc(output, GCHandleType.Pinned);
            IntPtr pOut = hOut.AddrOfPinnedObject();

            for (int i = 0; i < loops; i++)
            {
                uint w = 0;
                Native.beEncodeChunk(stream, samplesPerChunk, (IntPtr)((int)pIn + (i * samplesPerChunk * 2)), (IntPtr)((int)pOut + written), out w);
                written += w;
            }

            hIn.Free();
            hOut.Free();

            Array.Resize<byte>(ref alreadyQueuedData, restbyte);
            Array.Copy(inputBuffer, bytesPerChunk * loops, alreadyQueuedData, 0, restbyte);

            byte[] encodedPackets = new byte[written];
            Array.Copy(output, encodedPackets, written);
            return encodedPackets;
        }

        /// <summary>
        /// Encode already queued PCM to MP3 and Close stream.
        /// バッファ済みのPCMをエンコードして、ストリームを閉じる。
        /// </summary>
        /// <returns></returns>
        public byte[] Close()
        {
            if (stream == 0) return new byte[0];

            GCHandle hIn = GCHandle.Alloc(alreadyQueuedData, GCHandleType.Pinned);
            IntPtr pIn = hIn.AddrOfPinnedObject();

            byte[] output = new byte[bufferSize * 20];
            uint outputPtr = 0;
            GCHandle hOut = GCHandle.Alloc(output, GCHandleType.Pinned);
            IntPtr pOut = hOut.AddrOfPinnedObject();

            uint written = 0;
            Native.beEncodeChunk(stream, (uint)alreadyQueuedData.Length / 2, alreadyQueuedData, output, out written);
            outputPtr += written;
            Native.beDeinitStream(stream, (IntPtr)((int)pOut + outputPtr), out written);
            outputPtr += written;

            hIn.Free();
            hOut.Free();

            Native.beCloseStream(stream);
            alreadyQueuedData = null;

            byte[] encodedPackets = new byte[outputPtr];
            Array.Copy(output, encodedPackets, outputPtr);
            GC.SuppressFinalize(this);
            return encodedPackets;
        }

        void IDisposable.Dispose()
        {
            Close();
        }

        /// <summary>
        /// Lame_enc.dll ラッピングクラス
        /// </summary>
        public static class Native
        {
            public static bool Available
            {
                get { return System.IO.File.Exists("lame_enc.dll"); }
            }

            [DllImport("lame_enc.dll", CharSet = CharSet.Ansi)]
            public static extern uint beInitStream(ref BECONFIG pbeConfig, out uint dwSamples, out uint dwBufferSize, out uint phbeStream);
            [DllImport("lame_enc.dll", CharSet = CharSet.Ansi)]
            public static extern uint beEncodeChunk(uint hbeStream, uint nSamples, byte[] pSamples, [In, Out] byte[] pOutput, out uint pdwOutput);
            [DllImport("lame_enc.dll", CharSet = CharSet.Ansi)]
            public static extern uint beEncodeChunk(uint hbeStream, uint nSamples, IntPtr pSamples, IntPtr pOutput, out uint pdwOutput);
            [DllImport("lame_enc.dll", CharSet = CharSet.Ansi)]
            public static extern uint beDeinitStream(uint hbeStream, [In, Out] byte[] pOutput, out uint pdwOutput);
            [DllImport("lame_enc.dll", CharSet = CharSet.Ansi)]
            public static extern uint beDeinitStream(uint hbeStream, IntPtr pOutput, out uint pdwOutput);
            [DllImport("lame_enc.dll", CharSet = CharSet.Ansi)]
            public static extern uint beCloseStream(uint hbeStream);

            [StructLayout(LayoutKind.Sequential, Size = 331), Serializable]
            public struct BECONFIG // BE_CONFIG_LAME LAME header version 1
            {
                // STRUCTURE INFORMATION
                public uint dwConfig;
                public uint dwStructVersion;
                public uint dwStructSize;
                // BASIC ENCODER SETTINGS
                public uint dwSampleRate;		// SAMPLERATE OF INPUT FILE
                public uint dwReSampleRate;		// DOWNSAMPLERATE, 0=ENCODER DECIDES  
                public uint nMode;				// STEREO, MONO
                public uint dwBitrate;			// CBR bitrate, VBR min bitrate
                public uint dwMaxBitrate;		// CBR ignored, VBR Max bitrate
                public int nPreset;			// Quality preset
                public uint dwMpegVersion;		// MPEG-1 OR MPEG-2
                public uint dwPsyModel;			// FUTURE USE, SET TO 0
                public uint dwEmphasis;			// FUTURE USE, SET TO 0
                // BIT STREAM SETTINGS
                public int bPrivate;			// Set Private Bit (TRUE/FALSE)
                public int bCRC;				// Insert CRC (TRUE/FALSE)
                public int bCopyright;			// Set Copyright Bit (TRUE/FALSE)
                public int bOriginal;			// Set Original Bit (TRUE/FALSE)
                // VBR STUFF
                public int bWriteVBRHeader;	// WRITE XING VBR HEADER (TRUE/FALSE)
                public int bEnableVBR;			// USE VBR ENCODING (TRUE/FALSE)
                public int nVBRQuality;		// VBR QUALITY 0..9
                public uint dwVbrAbr_bps;		// Use ABR in stead of nVBRQuality
                public int nVbrMethod;
                public int bNoRes;				// Disable Bit resorvoir (TRUE/FALSE)
                // MISC SETTINGS
                public int bStrictIso;			// Use strict ISO encoding rules (TRUE/FALSE)
                public ushort nQuality;			// Quality Setting, HIGH BYTE should be NOT LOW byte, otherwhise quality=5
                // FUTURE USE, SET TO 0, align strucutre to 331 bytes
                //[ MarshalAs( UnmanagedType.ByValArray, SizeConst=255-4*4-2 )]
                //public byte[]   btReserved;//[255-4*sizeof(DWORD) - sizeof( WORD )];

                public BECONFIG(int freq, int ch, int kbps)
                {
                    dwConfig = 256;
                    dwStructVersion = 1;
                    dwStructSize = (uint)Marshal.SizeOf(typeof(BECONFIG));

                    dwSampleRate = (uint)freq;
                    dwReSampleRate = 0;
                    nMode = (ch == 1) ? 3u : 1u; // MONO or JSTEREO
                    dwBitrate = (uint)kbps;
                    nPreset = 2; // HIGH_QUALITY
                    dwMpegVersion = 1; // MPEG_1
                    dwPsyModel = 1;
                    dwEmphasis = 0;
                    bOriginal = 1;
                    bWriteVBRHeader = 0;
                    bNoRes = 0;
                    bCopyright = 0;
                    bCRC = 0;
                    bEnableVBR = 0;
                    bPrivate = 0;
                    bStrictIso = 0;
                    dwMaxBitrate = 0;
                    dwVbrAbr_bps = 0;
                    nQuality = 0;
                    nVbrMethod = -1;
                    nVBRQuality = 0;
                }
            }
        }
    }
}