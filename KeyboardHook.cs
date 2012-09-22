using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Tsukikage.WindowsUtil
{
    /// <summary>
    /// Keyboard Hook :) 
    /// If any event handler returns true, the input will be canceled ;) 
    /// - 簡単キーボードフック。いずれかのイベントハンドラがtrueを返すと、そのキーボードイベントは握りつぶされる。
    /// </summary>
    public sealed class KeyboardHook : IDisposable
    {
        IntPtr hHook = IntPtr.Zero;
        LowLevelKeyboardProc proc;

        public delegate bool KeyboardEvent(Keys code, int scanCode, int flags);
        public event KeyboardEvent OnKeyDown;
        public event KeyboardEvent OnKeyUp;
        public event KeyboardEvent OnSystemKeyDown;
        public event KeyboardEvent OnSystemKeyUp;

        public KeyboardHook()
            :this(false)
        {
        }

        public KeyboardHook(bool subthreaded)
        {
            proc = new LowLevelKeyboardProc(ProcessKeyboardEvent);
            if (subthreaded) InstallHookThreaded();
            else InstallHook();
        }

        void InstallHook()
        {
            if (hHook != IntPtr.Zero) return; // throw new InvalidOperationException("already installed.");
            IntPtr module = Marshal.GetHINSTANCE(MethodBase.GetCurrentMethod().Module);
            hHook = SetWindowsHookEx(0x000D, proc, module, 0); // 0x000D = WH_KEYBOARD_LL
        }

        void UninstallHook()
        {
            if (hHook != IntPtr.Zero)
                UnhookWindowsHookEx(hHook);
            hHook = IntPtr.Zero;
        }

        int ProcessKeyboardEvent(int code, IntPtr wParam, ref KBDLLHOOKSTRUCT lParam)
        {
            bool cancel = false;
            if (code >= 0 && lParam.scanCode != 0)
            {
                KeyboardEvent ev = null;
                switch (wParam.ToInt32())
                {
                    case 0x0100: ev = OnKeyDown; break; // 0x0100 = WM_KEYDOWN
                    case 0x0101: ev = OnKeyUp; break; // 0x0101 = WM_KEYUP
                    case 0x0104: ev = OnSystemKeyDown; break; // 0x0104 = WM_SYSKEYDOWN
                    case 0x0105: ev = OnSystemKeyUp; break; // 0x0105 = WM_SYSKEYUP
                }

                // if any handler returns true, input will be canceled.
                if (ev != null)
                    foreach (var d in ev.GetInvocationList())
                        cancel |= (bool)d.DynamicInvoke((Keys)lParam.vkCode, lParam.scanCode, lParam.flags);
            }

            int result = CallNextHookEx(hHook, code, wParam, ref lParam);
            return (result != 0 || cancel) ? 1 : 0;
        }

        public void Dispose()
        {
            if (subThread != null) { UninstallHookThreaded(); }
            else UninstallHook();
            GC.SuppressFinalize(this);
        }

        ~KeyboardHook()
        {
            Dispose();
        }


        #region omake
        public static void GenerateKeyboardEvent(Keys keyCode, bool keyDown)
        {
            keybd_event((byte)keyCode, 0, keyDown ? 0 : 2, IntPtr.Zero);
        }
        #endregion

        #region Win32
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct KBDLLHOOKSTRUCT
        {
            public int vkCode;
            public int scanCode;
            public int flags;
            public int time;
            public IntPtr dwExtraInfo;
        }

        delegate int LowLevelKeyboardProc(int code, IntPtr wParam, ref KBDLLHOOKSTRUCT lParam);
        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr SetWindowsHookEx(int hookType, LowLevelKeyboardProc hookDelegate, IntPtr hInstance, uint threadId);
        [DllImport("user32.dll", SetLastError = true)]
        static extern int CallNextHookEx(IntPtr hook, int code, IntPtr wParam, ref KBDLLHOOKSTRUCT lParam);
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll", SetLastError = true)]
        static extern void keybd_event(byte bVk, byte bScan, int dwFlags, IntPtr dwExtraInfo);
        #endregion

        #region subThreaded
        Thread subThread;
        ManualResetEvent exited;
        void InstallHookThreaded()
        {
            exited = new ManualResetEvent(false);
            subThread = new Thread(delegate()
            {
                InstallHook();
                while (!exited.WaitOne(1))
                    Application.DoEvents();
                UninstallHook();
            });
            subThread.Start();
        }

        void UninstallHookThreaded()
        {
            if (subThread != null)
            {
                exited.Set();
                subThread.Join();
            }
            subThread = null;
            exited = null;
        }
        #endregion
    }

#if BUILD_EXAMPLE
    #region EXAMPLE CODE
    static class Program
    {
        [STAThread]
        static void Main()
        {
            using (var HookLL = new KeyboardHook(true))
            {
                HookLL.OnKeyDown += HookLL_OnKeyDown;
                HookLL.OnKeyUp += HookLL_OnKeyUp;
                Console.ReadLine();
            }
        }

        static bool HookLL_OnKeyDown(Keys code, int scanCode, int flags)
        {
            Console.WriteLine("Down " + code + " / " + scanCode + " / " + flags.ToString("X"));
            if (Form.ModifierKeys == (Keys.Control | Keys.Shift))
            {
                Keys key = Keys.None;
                switch (code)
                {
                    case Keys.J: key = Keys.Down; break;
                    case Keys.K: key = Keys.Up; break;
                    case Keys.H: key = Keys.Left; break;
                    case Keys.L: key = Keys.Right; break;
                    case Keys.D: key = Keys.PageDown; break;
                    case Keys.U: key = Keys.PageUp; break;
                }

                if (key != Keys.None)
                {
                    KeyboardHook.GenerateKeyboardEvent(Keys.ShiftKey, false);
                    KeyboardHook.GenerateKeyboardEvent(Keys.ControlKey, false);
                    KeyboardHook.GenerateKeyboardEvent(key, true);
                    KeyboardHook.GenerateKeyboardEvent(key, false);
                    KeyboardHook.GenerateKeyboardEvent(Keys.ControlKey, true);
                    KeyboardHook.GenerateKeyboardEvent(Keys.ShiftKey, true);
                    return true;
                }
            }
            return false;
        }

        static bool HookLL_OnKeyUp(Keys code, int scanCode, int flags)
        {
            Console.WriteLine("Up " + code + " / " + scanCode + " / " + flags.ToString("X"));
            return false;
        }
    }
    #endregion
#endif
}
