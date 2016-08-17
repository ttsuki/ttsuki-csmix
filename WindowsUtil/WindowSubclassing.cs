using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Tsukikage.WindowsUtil
{
    /// <summary>
    /// 昔懐かしい方式でのウィンドウサブクラス化。
    /// </summary>
    public class WindowSubclassingMessageFilter : IDisposable
    {
        public delegate void MessageProc(ref Message m);

        IntPtr hWnd;
        IntPtr oldWndProc;
        IntPtr newWndProc;
        WndProcDelegate newWndProcDelegate;
        Dictionary<int, MessageProc> procs;

        public WindowSubclassingMessageFilter(IntPtr hWnd)
        {
            if (!Win32.IsWindow(hWnd)) { throw new ArgumentException(); }
            this.procs = new Dictionary<int, MessageProc>();
            this.hWnd = hWnd;
            this.newWndProcDelegate = this.WndProc;
            this.newWndProc = Marshal.GetFunctionPointerForDelegate(newWndProcDelegate);
            this.oldWndProc = Win32.GetWindowLong(hWnd, -4);
            Win32.SetWindowLong(hWnd, -4, newWndProc);
        }

        public void RegisterProc(int msg, MessageProc proc)
        {
            if (procs.ContainsKey(msg))
            {
                procs[msg] += proc;
            }
            else
            {
                procs[msg] = proc;
            }
        }

        public void UnregisterProc(int msg, MessageProc proc)
        {
            if (procs.ContainsKey(msg))
            {
                procs[msg] -= proc;
            }
        }

        delegate IntPtr WndProcDelegate(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        IntPtr WndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam)
        {
            Message m = new Message()
            {
                HWnd = hWnd,
                Msg = msg,
                WParam = wParam,
                LParam = lParam,
            };

            MessageProc proc;
            if (procs.TryGetValue(msg, out proc) && proc != null)
            {
                proc(ref m);
            }
            else
            {
                CallBaseWindowProc(ref m);
            }
            return m.Result;
        }

        public void CallBaseWindowProc(ref Message m)
        {
            m.Result = Win32.CallWindowProc(oldWndProc, m.HWnd, m.Msg, m.WParam, m.LParam);
        }

        public void Dispose()
        {
            if (Win32.IsWindow(hWnd)) 
            {
                Win32.SetWindowLong(hWnd, -4, oldWndProc);
            }
            GC.SuppressFinalize(this);
        }

        ~WindowSubclassingMessageFilter()
        {
            Dispose();
        }

        class Win32
        {
            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool IsWindow(IntPtr hWnd);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern IntPtr GetWindowLong(IntPtr hWnd, int nIndex);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr newWndProc);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);
        }
    }
}
