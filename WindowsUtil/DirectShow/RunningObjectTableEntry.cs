using System;
using System.Runtime.InteropServices;

namespace Tsukikage.DirectShow
{
    /// <summary>
    /// ROTにオブジェクトを登録する。
    /// </summary>
    public class RunningObjectTableEntry : IDisposable
    {
        int? register = null;

        [DllImport("ole32.dll")]
        static extern int GetRunningObjectTable(uint reserved, out System.Runtime.InteropServices.ComTypes.IRunningObjectTable pprot);

        [DllImport("ole32.dll")]
        static extern int CreateItemMoniker(
            [MarshalAs(UnmanagedType.LPWStr)] string lpszDelim,
            [MarshalAs(UnmanagedType.LPWStr)] string lpszItem,
            out System.Runtime.InteropServices.ComTypes.IMoniker ppmk);

        /// <summary>
        /// ROTにオブジェクトを登録する。
        /// </summary>
        /// <param name="iUnknownObject">オブジェクト</param>
        /// <param name="name">名前</param>
        public RunningObjectTableEntry(object iUnknownObject, string name)
        {
            int hresult;
            const int ROTFLAGS_REGISTRATIONKEEPSALIVE = 1;
            System.Runtime.InteropServices.ComTypes.IRunningObjectTable rot = null;
            System.Runtime.InteropServices.ComTypes.IMoniker moniker = null;

            try
            {
                hresult = GetRunningObjectTable(0, out rot);
                if (hresult < 0) throw new COMException("GetRunningObjectTable failed.", hresult);

                string wsz = string.Format("{0} {1:x16} pid {2:x8}",
                    name,
                    Marshal.GetIUnknownForObject(iUnknownObject).ToInt64(),
                    System.Diagnostics.Process.GetCurrentProcess().Id);

                hresult = CreateItemMoniker("!", wsz, out moniker);
                if (hresult < 0) throw new COMException("CreateItemMoniker failed.", hresult);

                int register = rot.Register(ROTFLAGS_REGISTRATIONKEEPSALIVE, iUnknownObject, moniker);
                this.register = register;

            }
            finally
            {
                if (moniker != null) Marshal.ReleaseComObject(moniker);
                if (rot != null) Marshal.ReleaseComObject(rot);
            }

        }

        /// <summary>
        /// ROTからオブジェクトを削除する。
        /// </summary>
        public void Revoke()
        {
            if (register != null)
            {
                int hresult;
                System.Runtime.InteropServices.ComTypes.IRunningObjectTable rot = null;

                try
                {
                    hresult = GetRunningObjectTable(0, out rot);
                    if (hresult < 0) throw new COMException("GetRunningObjectTable failed.", hresult);
                    rot.Revoke((int)register);
                    register = null;
                }
                finally
                {
                    if (rot != null) Marshal.ReleaseComObject(rot);
                }
            }
        }

        void IDisposable.Dispose() { Revoke(); GC.SuppressFinalize(this); }
        ~RunningObjectTableEntry() { Revoke(); }
    }
}
