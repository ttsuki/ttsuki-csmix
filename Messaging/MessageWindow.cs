using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Windows.Forms;

namespace Tsukikage.Windows.Messaging
{
    /// <summary>
    /// Win32 native message window.
    /// Win32 メッセージを受け取るためだけのウィンドウ
    /// </summary>
    [SuppressUnmanagedCodeSecurity]
    public class MessageWindow : NativeWindow, IDisposable
    {
        public delegate void MessageHandler(Message m);

        const int WM_CLOSE = 0x10;

        /// <summary>
        /// Install your message handler to this.
        /// メッセージハンドラをこれに組み込む
        /// </summary>
        public Dictionary<int, MessageHandler> MessageHandlers { get; private set; }

        /// <summary>
        /// Create new message window thread.
        /// 新しいメッセージウィンドウを作る
        /// </summary>
        public MessageWindow()
        {
            MessageHandlers = new Dictionary<int, MessageHandler>();
            System.Windows.Forms.CreateParams cp = new System.Windows.Forms.CreateParams();
            cp.ClassName = "Message";
            CreateHandle(cp);
        }

        /// <summary>
        /// Close window by posting WM_CLOSE and release all resouces.
        /// WM_CLOSEを送ってウィンドウを閉じ、すべてのリソースを解放する。
        /// </summary>
        public void Dispose()
        {
            PostMessage(WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Post message to the window.
        /// ウィンドウにメッセージを送る
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="wParam"></param>
        /// <param name="lParam"></param>
        public void PostMessage(int msg, IntPtr wParam, IntPtr lParam)
        {
            PostMessage(Handle, msg, wParam, lParam);
        }

        protected override void WndProc(ref Message m)
        {
            MessageHandler handler;
            if (MessageHandlers.TryGetValue(m.Msg, out handler))
                handler(m);
            else
                base.WndProc(ref m);

            if (m.Msg == WM_CLOSE)
                DestroyHandle();
        }
        
        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool PostMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);
    }


    class TEST
    {
        static void Main()
        {
            using (MessageWindow window = new MessageWindow())
            {
                const int WM_COMMAND = 0x111;
                window.MessageHandlers[WM_COMMAND] = (m) => { MessageBox.Show(m.Msg.ToString()); };
                window.PostMessage(WM_COMMAND, IntPtr.Zero, IntPtr.Zero);
                Application.Run(new Form());
            }
        }
    }
}
