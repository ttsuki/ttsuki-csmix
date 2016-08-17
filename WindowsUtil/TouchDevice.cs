using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Tsukikage.WindowsUtil
{
    public class Touch
    {
        public int ID { get; set; }
        public bool Primary { get; set; }
        public IntPtr Src { get; set; }
        public int Duration { get; set; } // タッチ時間
        public bool Pressed { get { return Current.Touched; } }
        public bool Touched { get { return Current.Touched && !Previous.Touched; } }
        public bool Released { get { return !Current.Touched && Previous.Touched; } }
        public int X { get { return Current.X; } }
        public int Y { get { return Current.Y; } }
        public int Width { get { return Current.Width; } }
        public int Height { get { return Current.Height; } }
        public bool InRange { get { return Current.InRange; } }
        public int LifeTime { get; set; } // 離されてから減算

        public TouchStatus Current, Previous;

        public struct TouchStatus
        {
            public bool Touched;
            public bool InRange;
            public int X, Y, Width, Height;
        }
    }

    public class TouchDevice : IDisposable
    {
        List<Touch> activeItems;
        List<Touch> touchedItems;
        List<Touch> releasedItems;
        List<TouchEvent> eventQueue;
        TouchMessageDecoder adapter = null;
        
        Control control;
        Touch mouseInput;
        bool lastLButtonStatus;

        public ReadOnlyCollection<Touch> Touched { get; private set; }
        public ReadOnlyCollection<Touch> Released { get; private set; }
        public ReadOnlyCollection<Touch> Active { get; private set; }

        public TouchDevice()
        {
            activeItems = new List<Touch>(256);
            touchedItems = new List<Touch>(16);
            releasedItems = new List<Touch>(16);
            eventQueue = new List<TouchEvent>(1024);

            Touched = touchedItems.AsReadOnly();
            Released = releasedItems.AsReadOnly();
            Active = activeItems.AsReadOnly();
        }

        public void Initialize(Control control)
        {
            this.control = control;
            this.adapter = new TouchMessageDecoder(control);
            this.adapter.RegisterWindow(true);
            adapter.TouchEvent += this.NotifyTouchEvent;
        }

        public void Dispose()
        {
            if (adapter != null)
            {
                adapter.UnregisterTouchWindow();
                adapter = null;
            }
            GC.SuppressFinalize(this);
        }

        public void Update()
        {
            // 現在の状態を過去へ
            touchedItems.Clear();
            releasedItems.Clear();
            for (int i = 0; i < activeItems.Count; i++)
            {
                activeItems[i].Previous = activeItems[i].Current;
            }

            // たまってるイベントの処理
            ProcessAllQueuedEvents();
            MouseToTouch();

            // 状態から Touched, Released を生成。
            for (int i = 0; i < activeItems.Count; i++)
            {
                Touch t = activeItems[i];
                if (t.Touched) { touchedItems.Add(t); }
                if (t.Released) { releasedItems.Add(t); }

                if (t.Pressed)
                {
                    t.Duration++;
                }
                else if (t.LifeTime-- <= 0)
                {
                    activeItems.RemoveAt(i);
                    i--;
                }
            }

            for (int i = 0; i < activeItems.Count; i++)
            {
                Touch t = activeItems[i];
                //Debug.Print("ID{0} P{1}", t.ID, t.Pressed);
            }
        }

        /// <summary>
        /// 指定した領域内をタッチされたか？
        /// </summary>
        /// <param name="r"></param>
        /// <returns></returns>
        public Touch FindTouch(Rectangle r)
        {
            for (int i = 0; i < touchedItems.Count; i++)
            {
                if (r.Contains(touchedItems[i].X, touchedItems[i].Y))
                    return touchedItems[i];
            }
            return null;
        }

        /// <summary>
        /// キューにたまっているイベントを処理する。
        /// </summary>
        void ProcessAllQueuedEvents()
        {
            // EventQueueの中身をCurrent反映
            for (int i = 0; i < eventQueue.Count; i++)
            {
                TouchEvent e = eventQueue[i];

                Touch t = null;
                if (e.Down)
                {
                    activeItems.Add(t = new Touch()
                    {
                        ID = e.ID,
                        Primary = e.Primary,
                        Src = e.SourceHandle,
                        LifeTime = 30,
                        Duration = 0,
                    });
                }
                else
                {
                    for (int j = 0; j < activeItems.Count; j++)
                    {
                        if (activeItems[j].ID == e.ID)
                        {
                            t = activeItems[j];
                            break;
                        }
                    }
                }

                // こうしん
                t.Current = new Touch.TouchStatus()
                {
                    X = e.X,
                    Y = e.Y,
                    Width = e.Width,
                    Height = e.Height,
                    InRange = e.InRange,
                    Touched = (t.Current.Touched | e.Down) & !e.Up,
                };
            }
            eventQueue.Clear();
        }

        public void NotifyTouchEvent(object sender, TouchEvent e)
        {
            eventQueue.Add(e);
        }

        /// <summary>
        /// マウスをタッチイベントとしてシミュレートする
        /// </summary>
        void MouseToTouch()
        {
            if (!control.Created) { return; }

            // マウス座標と左ボタンの状態を得る
            Point p = System.Windows.Forms.Cursor.Position; // スクリーン座標系
            bool lButton = (System.Windows.Forms.Form.MouseButtons & MouseButtons.Left) != 0;
            bool clickDown = lButton & !lastLButtonStatus;
            lastLButtonStatus = lButton;

            // クライアント座標系に変換。
            IntPtr controlHanle = control.Handle;
            Win32.POINT pt = new Win32.POINT() { X = p.X, Y = p.Y };
            Win32.ScreenToClient(controlHanle, ref pt);
            p = new Point(pt.X, pt.Y);

            // 
            if (clickDown && mouseInput == null)
            {
                if (control.ClientRectangle.Contains(p))
                {
                    mouseInput = new Touch()
                    {
                        ID = -1,
                        Duration = 0,
                        Src = IntPtr.Zero,
                        LifeTime = 30,
                        Primary = true,
                        Current = new Touch.TouchStatus() { Touched = true, InRange = true, X = p.X, Y = p.Y, Width = 10, Height = 10, },
                        Previous = new Touch.TouchStatus(),
                    };
                    activeItems.Add(mouseInput);
                }
            }

            if (mouseInput != null)
            {
                mouseInput.Current.X = p.X;
                mouseInput.Current.Y = p.Y;
            }

            if (mouseInput != null && !lButton)
            {
                mouseInput.Current.Touched = false;
                mouseInput = null;
            }
        }

        public static class Win32
        {
            [StructLayout(LayoutKind.Sequential)]
            public struct POINT
            {
                public int X;
                public int Y;
            }

            [DllImport("user32")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);
        }
    }

    /// <summary>
    /// WM_TOUCHをイベント化するためのクラス
    /// </summary>
    public class TouchMessageDecoder : IDisposable
    {
        public delegate void TouchEventHandler(object sender, TouchEvent e);
        public event TouchEventHandler TouchEvent;

        Control control = null;
        WindowSubclassingMessageFilter filter = null;
        TouchEvent[] inputBuffer = new TouchEvent[64];

        public TouchMessageDecoder(Control control)
        {
            this.control = control;
        }

        /// <summary>
        /// WM_TOUCH メッセージから TouchEvent を発火します。
        /// </summary>
        /// <param name="m"></param>
        public void DecodeTouch(ref Message m)
        {
            if (m.Msg != Win32.WM_TOUCH)
            {
                // WM_TOUCHではない。
                return;
            }

            int touchInputSize = Marshal.SizeOf(inputBuffer[0]);
            int inputCount = (ushort)m.WParam.ToInt32();
            if (inputCount > inputBuffer.Length)
            {
                Win32.CloseTouchInputHandle(m.LParam);
                m.Result = IntPtr.Zero;
                return;
            }

            if (!Win32.GetTouchInputInfo(m.LParam, inputCount, inputBuffer, touchInputSize))
            {
                Win32.CloseTouchInputHandle(m.LParam);
                m.Result = IntPtr.Zero;
                Debug.Print("Error on GetTouchInputInfo.");
                return;
            }

            for (int i = 0; i < inputCount; i++)
            {
                TouchEvent e = inputBuffer[i];
                
                Win32.POINT p = new Win32.POINT() { X = e.X / 100, Y = e.Y / 100 };
                Win32.ScreenToClient(m.HWnd, ref p);
                e.X = p.X;
                e.Y = p.Y;
                e.Width = e.HasContact ? e.Width / 100 : 0;
                e.Height = e.HasContact ? e.Height / 100 : 0;

                //Debug.Print("ID{4}-{7} x{0} y{1} w{2} h{3} P{5} R{6}",
                //    e.X, e.Y, e.Width, e.Height, e.ID,
                //    e.Primary, e.InRange,
                //    (e.Down ? "D" : e.Up ? "U" : "M")
                //    );

                if (TouchEvent != null)
                {
                    TouchEvent(this, e);
                }
            }

            Win32.CloseTouchInputHandle(m.LParam);
            return;
        }

        public void RegisterWindow()
        {
            RegisterWindow(false);
        }

        public void RegisterWindow(bool subclassing)
        {
            try
            {
                if (!Win32.RegisterTouchWindow(control.Handle, 0))
                {
                    Debug.WriteLine("ERROR: Could not register window for multi-touch");
                }
                if (subclassing)
                {
                    if (filter == null)
                    {
                        filter = new WindowSubclassingMessageFilter(control.Handle);
                        filter.RegisterProc(Win32.WM_TOUCH, this.DecodeTouch);
                    }
                }
            }
            catch (Exception)
            {
                Debug.WriteLine("ERROR: Touch APIs aren't supported.");
            }
        }

        public void UnregisterTouchWindow()
        {
            try
            {
                if (filter != null)
                {
                    filter.Dispose();
                    filter = null;
                }

                if (!control.IsDisposed && !Win32.UnregisterTouchWindow(control.Handle))
                {
                    Debug.WriteLine("ERROR: Could not unregister window for multi-touch");
                }
            }
            catch (Exception)
            {
                Debug.WriteLine("ERROR: Touch APIs aren't supported.");
            }
        }

        public void Dispose()
        {
            UnregisterTouchWindow();
            GC.SuppressFinalize(this);
        }

        public static class Win32
        {
            public const int WM_TOUCH = 0x0240;

            public const int TOUCHEVENTF_MOVE = 0x0001;
            public const int TOUCHEVENTF_DOWN = 0x0002;
            public const int TOUCHEVENTF_UP = 0x0004;
            public const int TOUCHEVENTF_INRANGE = 0x0008;
            public const int TOUCHEVENTF_PRIMARY = 0x0010;
            public const int TOUCHEVENTF_NOCOALESCE = 0x0020;
            public const int TOUCHEVENTF_PEN = 0x0040;

            public const int TOUCHINPUTMASKF_TIMEFROMSYSTEM = 0x0001;
            public const int TOUCHINPUTMASKF_EXTRAINFO = 0x0002;
            public const int TOUCHINPUTMASKF_CONTACTAREA = 0x0004;

            [StructLayout(LayoutKind.Sequential)]
            public struct POINT
            {
                public int X;
                public int Y;
            }

            [DllImport("user32")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool RegisterTouchWindow(System.IntPtr hWnd, uint ulFlags);

            [DllImport("user32")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool UnregisterTouchWindow(System.IntPtr hWnd);

            [DllImport("user32")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool GetTouchInputInfo(System.IntPtr hTouchInput, int cInputs, [In, Out] TouchEvent[] pInputs, int cbSize);

            [DllImport("user32")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern void CloseTouchInputHandle(System.IntPtr lParam);

            [DllImport("user32")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TouchEvent // TOUCHINPUT structure
    {
        public int X; // x
        public int Y; // y
        public System.IntPtr SourceHandle; // hSource
        public int ID; // dwID
        public int Flags; // dwFlags
        public int Mask; // dwMask
        public int Timestamp; // dwTime
        public System.IntPtr ExtraInfo; // dwExtraInfo
        public int Width; // cxContact
        public int Height; // cyContact

        public bool Move { get { return (Flags & TouchMessageDecoder.Win32.TOUCHEVENTF_MOVE) != 0; } }
        public bool Up { get { return (Flags & TouchMessageDecoder.Win32.TOUCHEVENTF_UP) != 0; } }
        public bool Down { get { return (Flags & TouchMessageDecoder.Win32.TOUCHEVENTF_DOWN) != 0; } }
        public bool Primary { get { return (Flags & TouchMessageDecoder.Win32.TOUCHEVENTF_PRIMARY) != 0; } }
        public bool InRange { get { return (Flags & TouchMessageDecoder.Win32.TOUCHEVENTF_INRANGE) != 0; } }
        public bool HasContact { get { return (Mask & TouchMessageDecoder.Win32.TOUCHINPUTMASKF_CONTACTAREA) != 0; } }
        public bool HasExtraInfo { get { return (Mask & TouchMessageDecoder.Win32.TOUCHINPUTMASKF_EXTRAINFO) != 0; } }
        public bool TimestampFromSystem { get { return (Mask & TouchMessageDecoder.Win32.TOUCHINPUTMASKF_TIMEFROMSYSTEM) != 0; } }
    }
}

