using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Tsukikage.WinMM.AcmMP3Decoder;
using Tsukikage.WinMM.WaveIO;
using System.IO;
using System.Diagnostics;
using Tsukikage.SharpJson;

namespace Tsukikage
{
    public class TTsuki
    {
        static void Main()
        {
            //WaveInToWaveOutSample();
            //Mp3ToWaveOutSample(@"E:\My Documents\20nov.mp3");

            //StableSorter.Benchmark.DoIt();
            //Tsukikage.Net.UPnPWanService.Test();
        }

        public static void VarVarVar()
        {
            string s = @"{ a:123, b:456, c:""789"", list: [1, 2, ""!Xyz<> \u0030 \u3042"", true, null, ], }";

            // From Stream
            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(s)))
            {
                Var importedFromStream = Var.FromFormattedStream(ms);
                Console.WriteLine("parsed: \n" + importedFromStream.ToFormattedString());
            }

            // From String
            Var imported = Var.FromFormattedString(s);
            Console.WriteLine("parsed: " + imported.ToCompressedFormattedString());

            // test
            Console.WriteLine("a+b = " + (string)(imported["a"] + imported["b"])); // 579
            Console.WriteLine("a+c = " + (string)(imported["a"] + imported["c"])); // "123789"

            // "123789" is true like JavaScript.
            bool result = imported["a"] + imported["c"];
            Console.WriteLine(@"(bool)(imported[""a""] + imported[""c""]) = " + result);

            // list is VarList. VarList is true like JavaScript. 
            Console.WriteLine(@"imported[""list""] ? true : false = " + (imported["list"] ? true : false));

            if (imported["list"].IsList)
            {
                Console.WriteLine("in list... : ");
                foreach (var v in imported["list"].AsList)
                {
                    Console.WriteLine("\t" + v.ToString());
                }
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
