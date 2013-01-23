using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Tsukikage.Windows.Messaging
{
    /// <summary>
    /// Win32 Message Loop Thread for win32 multimedia API callbacks
    /// Win32 メッセージループスレッドを作って、マルチメディア系 Win32 APIからのコールバックを受け取る
    /// </summary>
    [SuppressUnmanagedCodeSecurity]
    public sealed class MessageThread : IDisposable, IMessageFilter
    {
        Thread thread;

        /// <summary>
        /// Native Thread ID
        /// スレッドID
        /// </summary>
        public int Win32ThreadID { get; private set; }

        /// <summary>
        /// Install your message handler to this.
        /// メッセージハンドラをこれに組み込む
        /// </summary>
        public Dictionary<int, CallbackDelegate> MessageHandlers { get; private set; }

        public delegate void CallbackDelegate(Message m);

        /// <summary>
        /// Create new message loop thread.
        /// 新しいメッセージループスレッドを作る
        /// </summary>
        public MessageThread()
            : this(false)
        {

        }

        /// <summary>
        /// Create new message loop thread.
        /// 新しいメッセージループスレッドを作る
        /// </summary>
        /// <param name="isBackground">バックグラウンドスレッドにする場合はtrue</param>
        public MessageThread(bool isBackground)
            : this(isBackground, ThreadPriority.Normal)
        {
        }

        /// <summary>
        /// Create new message loop thread.
        /// 新しいメッセージループスレッドを作る
        /// </summary>
        /// <param name="isBackground">バックグラウンドスレッドにする場合はtrue</param>
        /// <param name="threadPriority">スレッドの実行優先度</param>
        public MessageThread(bool isBackground, ThreadPriority threadPriority)
        {
            MessageHandlers = new Dictionary<int, CallbackDelegate>();
            using (ManualResetEvent initialized = new ManualResetEvent(false))
            {
                thread = new Thread(delegate()
                {
                    Application.AddMessageFilter(this);
#pragma warning disable
                    Win32ThreadID = AppDomain.GetCurrentThreadId();
#pragma warning enable
                    Application.DoEvents(); // メッセージループを作る
                    initialized.Set();
                    Application.Run();
                });
                thread.IsBackground = isBackground;
                thread.Priority = threadPriority;
                thread.Start();
                initialized.WaitOne();
            }
        }
        int nextMessage = 0;
        public void Invoke(CallbackDelegate action)
        {
            using (ManualResetEvent ok = new ManualResetEvent(false))
            {
                int msg = Interlocked.Increment(ref nextMessage) % 0x1000 + 0x9000;
                MessageHandlers[msg] = action + delegate { ok.Set(); };
                PostMessage(msg, IntPtr.Zero, IntPtr.Zero);
                ok.WaitOne();
            }
        }

        /// <summary>
        /// Post message to the thread.
        /// スレッドにメッセージを送る
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="wParam"></param>
        /// <param name="lParam"></param>
        public void PostMessage(int msg, IntPtr wParam, IntPtr lParam)
        {
            PostThreadMessage(Win32ThreadID, msg, wParam, lParam);
        }

        /// <summary>
        /// Exit thread by posting WM_QUIT and release all resources.
        /// WM_QUITを送ってスレッドを終了し、リソースを解放する。
        /// </summary>
        public void Dispose()
        {
            if (thread != null)
            {
                const int WM_QUIT = 0x0012;
                PostMessage(WM_QUIT, IntPtr.Zero, IntPtr.Zero);
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

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool PostThreadMessage(int idThread, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern int GetCurrentThreadId();
    }

#if EXAMPLE_CODE
    class TEST
    {
        static void Main()
        {
            using (MessageThread thread = new MessageThread())
            {
                const int WM_COMMAND = 0x111;
                thread.MessageHandlers[WM_COMMAND] = (m) => { MessageBox.Show(m.Msg.ToString()); };
                thread.PostMessage(WM_COMMAND, IntPtr.Zero, IntPtr.Zero);
                Console.ReadLine();
            }
        }
    }
#endif
}
