using System;

namespace Tsukikage.WinMM.WaveIO
{
    public class WaveDSP
    {
        /// <summary>
        /// 入力された波形データを16bit signedと仮定して音量を変更します。
        /// </summary>
        /// <param name="wave">16bit signed Waveform</param>
        /// <param name="gain">gain (dB)</param>
        /// <returns>max output(dB)</returns>
        public static double Volume(byte[] wave, double gain)
        {
            short max = 1;
            unsafe
            {
                int samples = wave.Length / 2;
                double mul = Math.Pow(10, gain / 20.0);

                fixed (byte* p_ = wave)
                {
                    short* p = (short*)p_;
                    for (int i = 0; i < samples; i++)
                    {
                        double d = p[i] * mul;
                        if (d > 32766) p[i] = 32767;
                        else if (d < -32766) p[i] = -32767;
                        else p[i] = (short)d;
                        if (p[i] < 0 && -p[i] > max) max = (short)-p[i];
                        else if (p[i] > 0 && p[i] > max) max = p[i];
                    }
                }
            }
            return Math.Log10(max / 32768.0) * 200;
        }
    }
}
