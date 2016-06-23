using System;
using System.Collections.Generic;
using System.Threading;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Tsukikage.WinMM.MidiIO
{
    /// <summary>
    /// Win32 MidiOut制御クラス
    /// </summary>
    public class MidiOut : IDisposable
    {
        public delegate void MidiOutLongMessageDoneHandler();

        IntPtr deviceHandle = IntPtr.Zero;
        int enqueuedBufferSize = 0;
        MessageThread eventHandler;

        public const int MidiMapper = -1;
        
        /// <summary>
        /// Native device handle.
        /// </summary>
        public IntPtr Handle { get { return deviceHandle; } }

        /// <summary>
        /// Not played yet (contains playing data).
        /// 再生が終わってないデータの量(Write単位)を取得します。
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
        /// Open MidiOut.
        /// MidiOutを開きます。
        /// </summary>
        /// <param name="deviceId">MidiOut.MidiMapper or index of GetDeviceNames(). MidiOut.MidiMapperか、GetDeviceNames()のindex</param>
        public MidiOut(int deviceId)
        {
            eventHandler = new MessageThread(ThreadPriority.AboveNormal);
            eventHandler.MessageHandlers[Win32.MM_MOM_DONE] = delegate(Message m)
            {
                Win32.MidiHeader hdr = Win32.MidiHeader.FromIntPtr(m.LParam);
                MidiBuffer buf = MidiBuffer.FromMidiHeader(hdr);
                Win32.midiOutUnprepareHeader(deviceHandle, buf.pHeader, Win32.MidiHeader.SizeOfMidiHeader);
                buf.Dispose();
                Interlocked.Add(ref enqueuedBufferSize, -buf.BufferLength);
                if (OnDone != null) 
                    OnDone();
            };

            int mmret = Win32.midiOutOpen(out deviceHandle, (uint)deviceId, new IntPtr(eventHandler.Win32ThreadID), IntPtr.Zero, Win32.CALLBACK_THREAD);
            if (mmret != Win32.MMSYSERR_NOERROR)
            {
                eventHandler.Dispose();
                throw new Exception("Error on midiOutOpen. (ret=" + mmret + ")");
            }
        }

        void EnsureOpened()
        {
            if (deviceHandle == IntPtr.Zero)
                throw new InvalidOperationException("Device is not open.");
        }

        /// <summary>
        /// Send short midi message.
        /// 短いmidiデータを送ります。
        /// </summary>
        /// <param name="data">データ</param>
        public void ShortMessage(uint data)
        {
            EnsureOpened();
            Win32.midiOutShortMsg(deviceHandle, data);
        }

        /// <summary>
        /// Send long midi message.
        /// 長いmidiデータを送ります。
        /// </summary>
        /// <param name="data">データ</param>
        public void Write(params byte[] data)
        {
            EnsureOpened();
            Write(data, 0, data.Length);
        }

        /// <summary>
        /// Send long midi message.
        /// 長いmidiデータを送ります。
        /// </summary>
        /// <param name="data">データ</param>
        /// <param name="offset">読み出す位置</param>
        /// <param name="count">読み出すバイト数</param>
        public void Write(byte[] data, int offset, int count)
        {
            EnsureOpened();
            MidiBuffer buf = new MidiBuffer(count);
            buf.BufferLength = count;
            Array.Copy(data, offset, buf.Data, 0, buf.BufferLength);
            Write(buf);
        }

        /// <summary>
        /// Send long midi message.
        /// 長いmidiデータを送ります。
        /// </summary>
        /// <param name="src">データ</param>
        /// <param name="length">読み出すバイト数</param>
        public void Write(IntPtr src, int length)
        {
            EnsureOpened();
            MidiBuffer buf = new MidiBuffer(length);
            buf.BufferLength = length;
            Marshal.Copy(src, buf.Data, 0, buf.BufferLength);
            Write(buf);
        }

        /// <summary>
        /// Send long midi message.
        /// 長いmidiデータを送ります。
        /// </summary>
        /// <param name="buffer">メッセージが入ったバッファ</param>
        private void Write(MidiBuffer buffer)
        {
            EnsureOpened();
            Interlocked.Add(ref enqueuedBufferSize, buffer.BufferLength);
            Win32.midiOutPrepareHeader(deviceHandle, buffer.pHeader, Win32.MidiHeader.SizeOfMidiHeader);
            Win32.midiOutLongMsg(deviceHandle, buffer.pHeader, Win32.MidiHeader.SizeOfMidiHeader);
        }

        /// <summary>
        /// Stop.
        /// 再生を止め、ます。
        /// </summary>
        public void Stop()
        {
            EnsureOpened();
            Win32.midiOutReset(deviceHandle);
            while (enqueuedBufferSize != 0)
                Thread.Sleep(0);

            for (uint i = 0; i < 16; i++)
            {
                ShortMessage(0x0040B0 | i); // Pedal = 0
                ShortMessage(0x007BB0 | i); // All Sound Off
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
    public class MidiIn : IDisposable
    {
        public delegate void MidiInLongMessageHandler(byte[] data);
        public delegate void MidiInShortMessageHandler(uint data);

        IntPtr deviceHandle = IntPtr.Zero;
        int enqueuedBufferCount = 0;
        MessageThread eventHandler;
        volatile bool recording = false;

        /// <summary>
        /// Native device handle.
        /// </summary>
        public IntPtr Handle { get { return deviceHandle; } }

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

        
        /// <summary>
        /// Open MidiIn.
        /// MidiInを開きます。
        /// </summary>
        /// <param name="deviceId">index of GetDeviceNames(). GetDeviceNames()のindex</param>
        public MidiIn(uint deviceId)
        {
            eventHandler = new MessageThread(ThreadPriority.AboveNormal);
            eventHandler.MessageHandlers[Win32.MM_MIM_LONGDATA] = delegate(Message m)
            {
                Win32.MidiHeader header = Win32.MidiHeader.FromIntPtr(m.LParam);
                MidiBuffer buf = MidiBuffer.FromMidiHeader(header);
                int bytesRecorded = (int)header.dwBytesRecorded;
                if (OnLongMsg != null && bytesRecorded != 0)
                {
                    byte[] data = buf.Data;
                    if (bytesRecorded != buf.Data.Length)
                    {
                        data = new byte[bytesRecorded];
                        Array.Copy(buf.Data, data, bytesRecorded);
                    }
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

            eventHandler.MessageHandlers[Win32.MM_MIM_DATA] = delegate(Message m)
            {
                if (OnShortMsg != null)
                    OnShortMsg((uint)m.LParam);
            };

            int mmret = Win32.midiInOpen(out deviceHandle, deviceId, new IntPtr(eventHandler.Win32ThreadID), IntPtr.Zero, Win32.CALLBACK_THREAD);
            
            if (mmret != Win32.MMSYSERR_NOERROR)
            {
                eventHandler.Dispose();
                throw new Exception("Error on midiInOpen. (ret=" + mmret + ")");
            }
        }

        void EnsureOpened()
        {
            if (deviceHandle == IntPtr.Zero)
                throw new InvalidOperationException("Device is not open.");
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
                throw new InvalidOperationException("Started already.");

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
                throw new Exception("Error on midiInStart. (ret = " + mmret + ")");
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
                eventHandler.Dispose();
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

    class MidiBuffer : IDisposable
    {
        GCHandle dataHandle;
        GCHandle bufferHandle;
        int length;

        public IntPtr pHeader { get; private set; }
        public byte[] Data { get; private set; }
        
        public int BufferLength
        {
            get { return length; }
            set { SetLength(value); }
        }

        public MidiBuffer(int dwSize)
        {
            length = dwSize;
            Data = new byte[dwSize];
            dataHandle = GCHandle.Alloc(Data, GCHandleType.Pinned);
            bufferHandle = GCHandle.Alloc(this); 
            
            Win32.MidiHeader header = new Win32.MidiHeader();
            header.lpData = dataHandle.AddrOfPinnedObject();
            header.dwBufferLength = (uint)length;
            header.dwUser = GCHandle.ToIntPtr(bufferHandle);

            pHeader = Marshal.AllocHGlobal(Win32.MidiHeader.SizeOfMidiHeader);
            Marshal.StructureToPtr(header, pHeader, true);
        }

        public void SetLength(int newLength)
        {
            if (newLength < 0 || newLength > Data.Length)
                throw new ArgumentOutOfRangeException("newLength");

            if (newLength != length)
            {
                length = newLength;
                Win32.MidiHeader header = (Win32.MidiHeader)Marshal.PtrToStructure(pHeader, typeof(Win32.MidiHeader));
                header.dwBufferLength = (uint)length;
                Marshal.StructureToPtr(header, pHeader, true);
            }
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

    sealed class MessageThread : IDisposable, IMessageFilter
    {
        public delegate void CallbackDelegate(Message m);

        Thread thread;
        public int Win32ThreadID { get; private set; }
        public Dictionary<int, CallbackDelegate> MessageHandlers { get; private set; }

        public MessageThread(ThreadPriority threadPriority)
        {
            MessageHandlers = new Dictionary<int, CallbackDelegate>();
            using (ManualResetEvent initialized = new ManualResetEvent(false))
            {
                thread = new Thread(delegate ()
                {
                    Application.AddMessageFilter(this);
                    Application.DoEvents();
                    Win32ThreadID = NativeMethods.GetCurrentThreadId();
                    initialized.Set();
                    Application.Run();
                });
                thread.Priority = threadPriority;
                thread.Start();
                initialized.WaitOne();
            }
        }

        public void Dispose()
        {
            if (thread != null)
            {
                const int WM_QUIT = 0x0012;
                NativeMethods.PostThreadMessage(Win32ThreadID, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
                thread.Join();
                Win32ThreadID = 0;
                thread = null;
            }
        }

        bool IMessageFilter.PreFilterMessage(ref Message m)
        {
            CallbackDelegate handler;
            if (MessageHandlers.TryGetValue(m.Msg, out handler))
                handler(m);
            return false;
        }

        static class NativeMethods
        {

            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool PostThreadMessage(int idThread, int msg, IntPtr wParam, IntPtr lParam);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern int GetCurrentThreadId();
        }
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