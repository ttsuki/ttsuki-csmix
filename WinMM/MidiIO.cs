using System;
using System.Collections.Generic;
using System.Threading;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Tsukikage.Windows.Messaging;

namespace Tsukikage.WinMM.MidiIO
{
    /// <summary>
    /// Win32 MidiOut制御クラス
    /// </summary>
    [System.Security.SuppressUnmanagedCodeSecurity]
    public class MidiOut : IDisposable
    {
        public delegate void MidiOutLongMessageDoneHandler();

        IntPtr deviceHandle = IntPtr.Zero;
        int enqueuedBufferSize = 0;
        MessageThread eventHandler;

        public const int MidiMapper = -1;
        public IntPtr Handle { get { return deviceHandle; } }

        /// <summary>
        /// Not played yet (contains playing data).
        /// 再生が終わってないデータの量(Write単位)
        /// </summary>
        public int EnqueuedBufferSize { get { return enqueuedBufferSize; } }

        /// <summary>
        /// On complete played one buffer.
        /// WriteしたMidiデータの再生が終わると呼び出されます。
        /// </summary>
        /// <remarks>
        /// The event will be called from another thread.
        /// イベントは別のスレッドから呼ばれることがあります。
        /// </remarks>
        public event MidiOutLongMessageDoneHandler OnDone;

        /// <summary>
        /// Open MidiOut. MidiOutを開く
        /// </summary>
        /// <param name="deviceId">MidiOut.MidiMapper or index of GetDeviceNames(). MidiOut.MidiMapperか、GetDeviceNames()のindex</param>
        public MidiOut(int deviceId)
        {
            eventHandler = new MessageThread();
            eventHandler.MessageHandlers[Win32.MM_MOM_DONE] = delegate(Message m)
            {
                Win32.MidiHeader hdr = Win32.MidiHeader.FromIntPtr(m.LParam);
                MidiBuffer buf = MidiBuffer.FromMidiHeader(hdr);
                Win32.midiOutUnprepareHeader(deviceHandle, buf.pHeader, Win32.MidiHeader.SizeOfMidiHeader);
                buf.Dispose();
                Interlocked.Add(ref enqueuedBufferSize, -buf.Data.Length);
                if (OnDone != null) 
                    OnDone();
            };

            int mmret = Win32.midiOutOpen(out deviceHandle, (uint)deviceId, new IntPtr(eventHandler.Win32ThreadID), IntPtr.Zero, Win32.CALLBACK_THREAD);
            if (mmret != Win32.MMSYSERR_NOERROR)
            {
                eventHandler.Dispose();
                throw new Exception("デバイスが開けませんでした。(" + mmret + ")");
            }
        }

        void EnsureOpened()
        {
            if (deviceHandle == IntPtr.Zero)
                throw new InvalidOperationException("開いてないんだけど！");
        }

        /// <summary>
        /// Send short midi message.
        /// 短いmidiデータを送ります
        /// </summary>
        /// <param name="data">データ</param>
        public void ShortMessage(uint data)
        {
            EnsureOpened();
            Win32.midiOutShortMsg(deviceHandle, data);
        }

        /// <summary>
        /// Send long midi message.
        /// 長いmidiデータを送ります
        /// </summary>
        /// <param name="data">データ</param>
        public void Write(params byte[] data)
        {
            EnsureOpened();
            MidiBuffer buf = new MidiBuffer(data.Length);
            Array.Copy(data, buf.Data, data.Length);
            Win32.midiOutPrepareHeader(deviceHandle, buf.pHeader, Win32.MidiHeader.SizeOfMidiHeader);
            Win32.midiOutLongMsg(deviceHandle, buf.pHeader, Win32.MidiHeader.SizeOfMidiHeader);
        }

        /// <summary>
        /// Stop.
        /// 止める。
        /// </summary>
        public void Stop()
        {
            EnsureOpened();
            Win32.midiOutReset(deviceHandle);
            while (enqueuedBufferSize != 0)
                Thread.Sleep(0);

            // Pedalを放す。, All Sound Off,
            for (uint i = 0; i < 16; i++)
            {
                ShortMessage(0x0040B0 | i);
                ShortMessage(0x007BB0 | i);
            }
        }

        /// <summary>
        /// Close MidiOut and release all resources.
        /// MidiOutを閉じ、すべてのリソースを解放します。
        /// </summary>
        public void Close()
        {
            if (deviceHandle != IntPtr.Zero)
            {
                OnDone = null;
                Stop();
                Win32.midiOutClose(deviceHandle);
                deviceHandle = IntPtr.Zero;
                eventHandler.Dispose();
                GC.SuppressFinalize(this);
            }
        }

        void IDisposable.Dispose()
        {
            Close();
        }

        /// <summary>
        /// Get names of installed devices.
        /// インストール済みのデバイス名を得ます。
        /// </summary>
        /// <returns></returns>
        public static string[] GetDeviceNames()
        {
            uint devs = Win32.midiOutGetNumDevs();
            string[] devNames = new string[devs];
            for (uint i = 0; i < devs; i++)
            {
                Win32.MidiOutCaps caps = new Win32.MidiOutCaps();
                Win32.midiOutGetDevCaps(i, out caps, Win32.SizeOfMidiOutCaps);
                devNames[i] = caps.szPname;
            }
            return devNames;
        }
    }

    /// <summary>
    /// Win32 MidiIn 制御クラス
    /// </summary>
    [System.Security.SuppressUnmanagedCodeSecurity]
    public class MidiIn : IDisposable
    {
        public delegate void MidiInLongMessageHandler(byte[] data);
        public delegate void MidiInShortMessageHandler(uint data);

        IntPtr deviceHandle = IntPtr.Zero;
        public IntPtr Handle { get { return deviceHandle; } }

        MessageThread messageProc;
        volatile bool recording = false;
       
        /// <summary>
        /// On long message arrival.
        /// 長いメッセージが来たときに呼ばれます。
        /// </summary>
        /// <remarks>
        /// The event will be called from another thread.
        /// イベントは別のスレッドから呼ばれることがあります。
        /// </remarks>
        public event MidiInLongMessageHandler OnLongMsg;

        /// <summary>
        /// On short message arrival.
        /// 短いメッセージが来たときに呼ばれます。
        /// </summary>
        /// <remarks>
        /// The event will be called from another thread.
        /// イベントは別のスレッドから呼ばれることがあります。
        /// </remarks>
        public event MidiInShortMessageHandler OnShortMsg;

        int enqueuedBufferCount = 0;
        
        /// <summary>
        /// Open MidiIn.
        /// MidiInを開きます。
        /// </summary>
        /// <param name="deviceId">index of GetDeviceNames(). GetDeviceNames()のindex</param>
        public MidiIn(uint deviceId)
        {
            messageProc = new MessageThread();
            messageProc.MessageHandlers[Win32.MM_MIM_LONGDATA] = delegate(Message m)
            {
                Win32.MidiHeader hdr = Win32.MidiHeader.FromIntPtr(m.LParam);
                MidiBuffer buf = MidiBuffer.FromMidiHeader(hdr);
                if (OnLongMsg != null && hdr.dwBytesRecorded != 0)
                {
                    byte[] data = new byte[hdr.dwBytesRecorded];
                    Array.Copy(buf.Data, data, hdr.dwBytesRecorded);
                    OnLongMsg(data);
                }

                if (recording)
                {
                    Win32.midiInAddBuffer(deviceHandle, m.LParam, Win32.MidiHeader.SizeOfMidiHeader);
                }
                else
                {
                    Win32.midiInUnprepareHeader(deviceHandle, m.LParam, Win32.MidiHeader.SizeOfMidiHeader);
                    buf.Dispose();
                    Interlocked.Decrement(ref enqueuedBufferCount);
                }
            };

            messageProc.MessageHandlers[Win32.MM_MIM_DATA] = delegate(Message m)
            {
                if (OnShortMsg != null)
                    OnShortMsg((uint)m.LParam);
            };

            int mmret = Win32.midiInOpen(out deviceHandle, deviceId, new IntPtr(messageProc.Win32ThreadID), IntPtr.Zero, Win32.CALLBACK_THREAD);
            
            if (mmret != Win32.MMSYSERR_NOERROR)
            {
                messageProc.Dispose();
                throw new Exception("デバイスが開けませんでした。(" + mmret + ")");
            }
        }

        void EnsureOpened()
        {
            if (deviceHandle == IntPtr.Zero)
                throw new InvalidOperationException("開いてないんだけど！");
        }

        /// <summary>
        /// Start recording. 録音開始
        /// </summary>
        public void Start() { Start(1024); }

        /// <summary>
        /// Start recording. 録音開始
        /// </summary>
        /// <param name="bufferSize"> ex) 1024 : バッファサイズ</param>
        public void Start(int bufferSize) { Start(256, bufferSize); }

        /// <summary>
        /// Start recording. 録音開始
        /// </summary>
        /// <param name="bufferCount"> ex) 256 : バッファ数</param>
        /// <param name="bufferSize"> ex) 1024 : バッファサイズ</param>
        public void Start(int bufferCount, int bufferSize)
        {
            EnsureOpened();            
            if (recording)
                throw new InvalidOperationException("既に録音中");

            for (int i = 0; i < bufferCount; i++)
            {
                MidiBuffer buf = new MidiBuffer(bufferSize);
                Win32.midiInPrepareHeader(deviceHandle, buf.pHeader, Win32.MidiHeader.SizeOfMidiHeader);
                Win32.midiInAddBuffer(deviceHandle, buf.pHeader, Win32.MidiHeader.SizeOfMidiHeader);
                Interlocked.Increment(ref enqueuedBufferCount);
            }
            int mmret = Win32.midiInStart(deviceHandle);
            if (mmret != Win32.MMSYSERR_NOERROR)
            {
                throw new Exception("録音開始に失敗……？ (" + mmret + ")");
            }

            recording = true;
        }

        /// <summary>
        /// Stop recording. 録音停止
        /// </summary>
        public void Stop()
        {
            EnsureOpened();
            recording = false;
            Win32.midiInReset(deviceHandle);
            while (enqueuedBufferCount != 0)
                Thread.Sleep(0);
        }

        /// <summary>
        /// Close MidiIn and release all resources.
        /// MidiInを閉じ、すべてのリソースを解放します。
        /// </summary>
        public void Close()
        {
            if (deviceHandle != IntPtr.Zero)
            {
                OnLongMsg = null;
                OnShortMsg = null;
                Stop();
                messageProc.Dispose();
                Win32.midiInClose(deviceHandle);
                deviceHandle = IntPtr.Zero;
                GC.SuppressFinalize(this);
            }
        }

        void IDisposable.Dispose()
        {
            Close();
        }

        /// <summary>
        /// Get names of installed devices.
        /// インストール済みのデバイス名を得ます。
        /// </summary>
        /// <returns></returns>
        public static string[] GetDeviceNames()
        {
            uint devs = Win32.midiInGetNumDevs();
            string[] devNames = new string[devs];
            for (uint i = 0; i < devs; i++)
            {
                Win32.MidiInCaps caps = new Win32.MidiInCaps();
                Win32.midiInGetDevCaps(i, out caps, Win32.SizeOfMidiInCaps);
                devNames[i] = caps.szPname;
            }
            return devNames;
        }
    }

    [System.Security.SuppressUnmanagedCodeSecurity]
    class MidiBuffer : IDisposable
    {
        public IntPtr pHeader { get; private set; }
        public byte[] Data { get; private set; }
        GCHandle dataHandle;
        GCHandle bufferHandle;

        public MidiBuffer(int dwSize)
        {
            Data = new byte[dwSize];
            dataHandle = GCHandle.Alloc(Data, GCHandleType.Pinned);
            bufferHandle = GCHandle.Alloc(this);

            Win32.MidiHeader header = new Win32.MidiHeader();
            header.lpData = dataHandle.AddrOfPinnedObject();
            header.dwBufferLength = (uint)Data.Length;
            header.dwUser = GCHandle.ToIntPtr(bufferHandle);

            pHeader = Marshal.AllocHGlobal(Win32.MidiHeader.SizeOfMidiHeader);
            Marshal.StructureToPtr(header, pHeader, true);
        }

        public static MidiBuffer FromMidiHeader(Win32.MidiHeader header)
        {
            return (MidiBuffer)GCHandle.FromIntPtr(header.dwUser).Target;
        }

        public void Dispose()
        {
            if (pHeader == IntPtr.Zero)
                return;

            bufferHandle.Free();
            dataHandle.Free();
            Marshal.FreeHGlobal(pHeader);
            pHeader = IntPtr.Zero;
            GC.SuppressFinalize(this);
        }


        //~MidiBuffer()
        //{
        //    /* don't free buffer */
        //}
    }

    [System.Security.SuppressUnmanagedCodeSecurity]
    static class Win32
    {
        public const int MMSYSERR_NOERROR = 0;
        public const int CALLBACK_WINDOW = 0x00010000;
        public const int CALLBACK_THREAD = 0x00020000;
        public const int CALLBACK_FUNCTION = 0x00030000;

        public const int MM_MOM_DONE = 0x3C9;
        public const int MM_MIM_DATA = 0x3C3;
        public const int MM_MIM_LONGDATA = 0x3C4;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct MidiHeader
        {
            public IntPtr lpData;
            public uint dwBufferLength;
            public uint dwBytesRecorded;
            public IntPtr dwUser;
            public uint dwFlags;
            public IntPtr lpNext;
            public IntPtr reserved;
            public uint dwOffset;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public IntPtr[] dwReserved;
            public static int SizeOfMidiHeader { get { return Marshal.SizeOf(typeof(MidiHeader)); } }
            public static MidiHeader FromIntPtr(IntPtr p) { return (MidiHeader)Marshal.PtrToStructure(p, typeof(MidiHeader)); }
        }

        public static readonly int SizeOfMidiOutCaps = Marshal.SizeOf(typeof(MidiOutCaps));
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct MidiOutCaps
        {
            public ushort wMid;
            public ushort wPid;
            public uint vDriverVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szPname;
            public ushort wTechnology;
            public ushort wVoices;
            public ushort wNotes;
            public ushort wChannelMask;
            public uint dwSupport;
        }

        public static readonly int SizeOfMidiInCaps = Marshal.SizeOf(typeof(MidiInCaps));
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct MidiInCaps
        {
            public ushort wMid;
            public ushort wPid;
            public uint vDriverVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szPname;
            public uint dwSupport;
        }

        [DllImport("winmm.dll", CharSet = CharSet.Ansi)]
        public static extern uint midiOutGetNumDevs();
        [DllImport("winmm.dll", CharSet = CharSet.Ansi)]
        public static extern int midiOutGetDevCaps(uint uDeviceID, out MidiOutCaps lpMidiOutCaps, int cbMidiOutCaps);
        [DllImport("winmm.dll", CharSet = CharSet.Ansi)]
        public static extern int midiOutOpen(out IntPtr lphmo, uint uDeviceID, IntPtr dwCallback, IntPtr dwCallbackInstance, uint dwFlags);
        [DllImport("winmm.dll", CharSet = CharSet.Ansi)]
        public static extern int midiOutShortMsg(IntPtr hmo, uint dwMsg);
        [DllImport("winmm.dll", CharSet = CharSet.Ansi)]
        public static extern int midiOutPrepareHeader(IntPtr hmo, ref MidiHeader lpMidiOutHdr, int cbMidiOutHdr);
        [DllImport("winmm.dll", CharSet = CharSet.Ansi)]
        public static extern int midiOutPrepareHeader(IntPtr hmo, IntPtr lpMidiOutHdr, int cbMidiOutHdr);
        [DllImport("winmm.dll", CharSet = CharSet.Ansi)]
        public static extern int midiOutLongMsg(IntPtr hmo, ref MidiHeader lpMidiOutHdr, int cbMidiOutHdr);
        [DllImport("winmm.dll", CharSet = CharSet.Ansi)]
        public static extern int midiOutLongMsg(IntPtr hmo, IntPtr lpMidiOutHdr, int cbMidiOutHdr);
        [DllImport("winmm.dll", CharSet = CharSet.Ansi)]
        public static extern int midiOutUnprepareHeader(IntPtr hmo, ref MidiHeader lpMidiOutHdr, int cbMidiOutHdr);
        [DllImport("winmm.dll", CharSet = CharSet.Ansi)]
        public static extern int midiOutUnprepareHeader(IntPtr hmo, IntPtr lpMidiOutHdr, int cbMidiOutHdr);
        [DllImport("winmm.dll", CharSet = CharSet.Ansi)]
        public static extern int midiOutReset(IntPtr hmo);
        [DllImport("winmm.dll", CharSet = CharSet.Ansi)]
        public static extern int midiOutClose(IntPtr hmo);

        [DllImport("winmm.dll", CharSet = CharSet.Ansi)]
        public static extern uint midiInGetNumDevs();
        [DllImport("winmm.dll", CharSet = CharSet.Ansi)]
        public static extern int midiInGetDevCaps(uint uDeviceID, out MidiInCaps lpMidiInCaps, int cbMidiInCaps);
        [DllImport("winmm.dll", CharSet = CharSet.Ansi)]
        public static extern int midiInOpen(out IntPtr lphmi, uint uDeviceID, IntPtr dwCallback, IntPtr dwCallbackInstance, uint dwFlags);
        [DllImport("winmm.dll", CharSet = CharSet.Ansi)]
        public static extern int midiInPrepareHeader(IntPtr hmi, ref MidiHeader lpMidiInHdr, int cbMidiInHdr);
        [DllImport("winmm.dll", CharSet = CharSet.Ansi)]
        public static extern int midiInPrepareHeader(IntPtr hmi, IntPtr lpMidiInHdr, int cbMidiInHdr);
        [DllImport("winmm.dll", CharSet = CharSet.Ansi)]
        public static extern int midiInAddBuffer(IntPtr hmi, ref MidiHeader lpMidiInHdr, int cbMidiInHdr);
        [DllImport("winmm.dll", CharSet = CharSet.Ansi)]
        public static extern int midiInAddBuffer(IntPtr hmi, IntPtr lpMidiInHdr, int cbMidiInHdr);
        [DllImport("winmm.dll", CharSet = CharSet.Ansi)]
        public static extern int midiInUnprepareHeader(IntPtr hmi, ref MidiHeader lpMidiInHdr, int cbMidiInHdr);
        [DllImport("winmm.dll", CharSet = CharSet.Ansi)]
        public static extern int midiInUnprepareHeader(IntPtr hmi, IntPtr lpMidiInHdr, int cbMidiInHdr);
        [DllImport("winmm.dll", CharSet = CharSet.Ansi)]
        public static extern int midiInStart(IntPtr hmi);
        [DllImport("winmm.dll", CharSet = CharSet.Ansi)]
        public static extern int midiInReset(IntPtr hmi);
        [DllImport("winmm.dll", CharSet = CharSet.Ansi)]
        public static extern int midiInClose(IntPtr hmi);
    }
}