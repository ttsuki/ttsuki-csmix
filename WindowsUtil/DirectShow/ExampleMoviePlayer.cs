using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace Tsukikage.DirectShow
{
    /// <summary>
    /// DirectShowのSampleGrabberGraph を使ってメディアファイルを再生するサンプル。
    /// SampleGrabberを使うメリットは、ビデオもオーディオもデコード後のデータのポインタが得られることで、
    /// Marshal.Copyなどでbyte[]にコピーするなり、unsafeコンテキストを使うなりで手軽に加工することができる。
    /// ゲームアプリなどではDynamicTextureに転送することで簡単にムービーテクスチャを作ることもできる。
    /// VMR9を使った方がパフォーマンスが出るが、Device LostがOSのバージョンごとに挙動が違ったりで対応しきれない。
    /// カスタムレンダラを作るのが最良の方法なのだろうけれど、C#のみでカスタムレンダラを作るのは至難の業。
    /// Enter:開く, Space:再生/一時停止, Escape:停止 ←/→:5秒シーク
    /// </summary>
    public class ExampleMoviePlayer : Form
    {
        SampleGrabberGraph graph;
        Bitmap videoBuffer;
        Tsukikage.WinMM.WaveIO.WaveOut waveOut;

        public ExampleMoviePlayer()
        {
            // construct
            InitializeComponent();
        }

        /// <summary>
        /// ウィンドウが表示された。開くダイアログを開く。
        /// </summary>
        private void ExampleMoviePlayer_Shown(object sender, EventArgs e)
        {
            openMediaFileDialog.ShowDialog();
        }

        /// <summary>
        /// 開くダイアログでOKが押された。ファイルを開いて再生する。
        /// </summary>
        private void openMediaFileDialog_FileOk(object sender, CancelEventArgs e)
        {
            // 前に再生していたものを閉じる
            CloseMedia();

            // ファイル名
            string path = System.IO.Path.GetFullPath(openMediaFileDialog.FileName);

            try
            {
                // 読み込む
                graph = new SampleGrabberGraph(path);
                //graph.RegisterToROT = true; // trueにするとgraphedit.exeから見えるようになる。
                graph.Load();
            }
            catch
            {
                // error...
                graph.Dispose();
                graph = null;
                MessageBox.Show("Error occured.");
                return;
            }

            // メディアと同じ形式でオーディオデバイスを開く
            if (graph.HasAudio)
            {
                waveOut = new WinMM.WaveIO.WaveOut(WinMM.WaveIO.WaveOut.WaveMapper, graph.AudioSamplePerSec, graph.AudioBitsPerSample, graph.AudioChannels);
                graph.AudioFrame += graph_AudioFrame;
            }

            // メディアと同じサイズでビデオバッファを作る
            if (graph.HasVideo)
            {
                this.ClientSize = graph.VideoSize;
                videoBuffer = new Bitmap(graph.VideoSize.Width, graph.VideoSize.Height, PixelFormat.Format32bppArgb);
                graph.VideoFrame += graph_VideoFrame;
            }

            // 再生。
            if (graph != null) graph.Play();
        }

        /// <summary>
        /// オーディオデータが到着。waveOutに書き込む。
        /// </summary>
        private void graph_AudioFrame(double sampleTime, IntPtr pFrame, int bufferSize)
        {
            if (graph.IsPlaying)
                waveOut.Write(pFrame, bufferSize);
        }

        /// <summary>
        /// ビデオフレームが到着。videoBufferをロックして転送する。
        /// </summary>
        private void graph_VideoFrame(double sampleTime, IntPtr pFrame, int bufferSize)
        {
            lock (videoBuffer)
            {
                BitmapData data = videoBuffer.LockBits(new Rectangle(Point.Empty, videoBuffer.Size), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
                graph.RenderVideoFrameToTexture(pFrame, data.Scan0, videoBuffer.Width, videoBuffer.Height);
                videoBuffer.UnlockBits(data);
            }

            // ビデオバッファを更新したので画面も更新する。
            BeginInvoke(new MethodInvoker(Invalidate));
        }

        /// <summary>
        /// 一定時間毎に画面更新しておく。(オーディオファイルや、一時的に0FPSになるビデオファイルも存在する)
        /// </summary>
        private void timer1_Tick(object sender, EventArgs e)
        {
            BeginInvoke(new MethodInvoker(Invalidate));
        }

        /// <summary>
        /// 画面を更新すべき。
        /// </summary>
        private void ExampleMoviePlayer_Paint(object sender, PaintEventArgs e)
        {
            // ビデオバッファの中身を描く
            if (videoBuffer != null)
                lock (videoBuffer)
                {
                    float mag = Math.Min((float)ClientSize.Width / videoBuffer.Width, (float)ClientSize.Height / videoBuffer.Height);
                    e.Graphics.DrawImage(videoBuffer,
                        ClientSize.Width / 2 - videoBuffer.Width * mag / 2,
                        ClientSize.Height / 2 - videoBuffer.Height * mag / 2,
                        videoBuffer.Width * mag,
                        videoBuffer.Height * mag);
                }

            // タイムスタンプを描く
            double timestamp = graph != null ? graph.CurrentPosition : 0;
            string position = DateTime.MinValue.AddMilliseconds(timestamp).ToString("HH:mm:ss.fff");
            e.Graphics.DrawString(position, Font, Brushes.Blue, new PointF(2, 2));
            e.Graphics.DrawString(position, Font, Brushes.White, new PointF(0, 0));
        }

        /// <summary>
        /// キーが押された。各種操作。
        /// </summary>
        private void ExampleMoviePlayer_KeyDown(object sender, KeyEventArgs e)
        {
            int seek = 0;
            switch (e.KeyCode)
            {
                case Keys.Enter: openMediaFileDialog.ShowDialog(); break; // 開く
                case Keys.Space: if (graph != null && !graph.IsPlaying) graph.Play(); else if (graph != null) graph.Pause(); break; // 再生/一時停止
                case Keys.Escape: if (graph != null) graph.Stop(); break; // 停止
                case Keys.Left: seek = -5000; break; // -5秒
                case Keys.Right: seek = +5000; break; // +5秒
            }

            if (seek != 0)
            {
                if (graph != null) graph.CurrentPosition = Math.Max(0, graph.CurrentPosition + seek);
                if (waveOut != null) waveOut.Stop();
            }
        }

        /// <summary>
        /// 閉じられる。ファイルを閉じる。
        /// </summary>
        private void ExampleMoviePlayer_FormClosing(object sender, FormClosingEventArgs e)
        {
            CloseMedia();
        }

        /// <summary>
        /// グラフとビデオバッファとオーディオデバイスを解放する。
        /// </summary>
        private void CloseMedia()
        {
            if (graph != null)
            {
                graph.Dispose();
                graph = null;
            }

            if (videoBuffer != null)
            {
                videoBuffer.Dispose();
                videoBuffer = null;
            }

            if (waveOut != null)
            {
                waveOut.Close();
                waveOut = null;
            }
        }

        #region Windows Form Designer generated code

        private OpenFileDialog openMediaFileDialog;
        private Timer timer1;

        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.openMediaFileDialog = new System.Windows.Forms.OpenFileDialog();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.SuspendLayout();
            // 
            // openMediaFileDialog
            // 
            this.openMediaFileDialog.Filter = "Media files|*.avi;*.wav;*.asf;*.wmv;*.wma;*.mp3|All files (*.*)|*.*";
            this.openMediaFileDialog.FileOk += new System.ComponentModel.CancelEventHandler(this.openMediaFileDialog_FileOk);
            // 
            // timer1
            // 
            this.timer1.Enabled = true;
            this.timer1.Interval = 33;
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick);
            // 
            // ExampleMoviePlayer
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.ClientSize = new System.Drawing.Size(640, 480);
            this.DoubleBuffered = true;
            this.Font = new System.Drawing.Font("Verdana", 24F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Margin = new System.Windows.Forms.Padding(4);
            this.Name = "ExampleMoviePlayer";
            this.Text = "ExampleMoviePlayer";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.ExampleMoviePlayer_FormClosing);
            this.Shown += new System.EventHandler(this.ExampleMoviePlayer_Shown);
            this.Paint += new System.Windows.Forms.PaintEventHandler(this.ExampleMoviePlayer_Paint);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.ExampleMoviePlayer_KeyDown);
            this.ResumeLayout(false);

        }

        #endregion

        [STAThread]
        static void Main_()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Tsukikage.DirectShow.ExampleMoviePlayer());
        }

        public static void SampleMain()
        {
            // ファイルを開くダイアログを使ってるため、STAThreadである必要がある。
            if (System.Threading.Thread.CurrentThread.GetApartmentState() == System.Threading.ApartmentState.STA)
            {
                // STAThreadならそのまま実行。
                Main_();
            }
            else
            {
                // 違ったらスレッドを起こしてそっちで実行させて自分は終了待機。
                System.Threading.ThreadStart main = new System.Threading.ThreadStart(Main_);
                System.Threading.Thread staThread = new System.Threading.Thread(main);
                staThread.SetApartmentState(System.Threading.ApartmentState.STA);
                staThread.Start();
                staThread.Join();
            }
        }
    }
}
