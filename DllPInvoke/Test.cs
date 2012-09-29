using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Tsukikage.DllPInvoke
{
    public class Test
    {
        /// <summary>
        /// Raise DllNotFoundException
        /// </summary>
        [DllImport("hogehogefugafugaasdfasdfpiyopiyo.dll")]
        public static extern void RaiseDllNotFoundException();
    }
}
