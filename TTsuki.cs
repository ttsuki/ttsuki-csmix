using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Tsukikage.WinMM.AcmMP3Decoder;
using Tsukikage.WinMM.WaveIO;
using System.IO;
using Tsukikage.Util;
using System.Diagnostics;

namespace Tsukikage
{
    public class TTsuki
    {
        static void Main()
        {
            //WaveInToWaveOutSample();
            //Mp3ToWaveOutSample(@"E:\My Documents\20nov.mp3");

            //StableSorter.Benchmark.DoIt();

            VarVarVar();
        }

        public static void VarVarVar()
        {
            Var a = "1";
            Var b = 1;
            Var c = "a";
            Var list = new Var[] { 1, 2, 3 };
            if (a + b) // "1" + 1 = "11". "11" is true like JavaScript.
            {
                long x = list ? 1 : 2; // list is VarList. VarList is true like JavaScript. 
                Console.WriteLine(x.ToString());
            }
        }

        public static void WaveInToWaveOutSample()
        {
            using (var waveOut = new WaveOut(WaveOut.WaveMapper, 44100, 16, 2))
            using (var waveIn = new WaveIn(WaveIn.WaveMapper, 44100, 16, 2))
            {
                waveIn.OnData += waveOut.Write;
                waveIn.Start(16, 44100);
                Application.Run(new Form());
            }
        }

        public static void Mp3ToWaveOutSample(string path)
        {
            byte[] mp3buf = System.IO.File.ReadAllBytes(path);

            using(var waveOut = new WaveOut(WaveOut.WaveMapper, 44100, 16, 2))
            using (var mp3 = new AcmMp3Decoder(65536))
            {
                int i = 0;
                byte[] wavebuf = mp3.Decode(mp3buf);
                Console.WriteLine(""+wavebuf.Length + "bytes decoded.");
               
                while(wavebuf.Length > 0)
                {
                    while (waveOut.EnqueuedBufferSize > 262144) Thread.Sleep(1);
                    Console.WriteLine("Writing... {0}", i++);
                    waveOut.Write(wavebuf);
                    wavebuf = mp3.Decode(new byte[0]);
                    Console.WriteLine("" + wavebuf.Length + "bytes decoded.");
                }

                Console.ReadLine();
            }
        }
    }
}
