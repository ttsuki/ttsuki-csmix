
# TTSUKI LIB
TTsuki's Miscellaneous C# Windows Library. 

C#でWindowsプログラムを書くときに、ちょっと便利にするライブラリ.

なるべく、プロジェクト単位でなく、ファイル単位でコピーして持っていけるようにしたい。
## Licence: NYSL
http://www.kmonos.net/nysl/

A. 本ソフトウェアは Everyone'sWare です。このソフトを手にした一人一人が、
   ご自分の作ったものを扱うのと同じように、自由に利用することが出来ます。

　A-1. フリーウェアです。作者からは使用料等を要求しません。

　A-2. 有料無料や媒体の如何を問わず、自由に転載・再配布できます。

　A-3. いかなる種類の 改変・他プログラムでの利用 を行っても構いません。

　A-4. 変更したものや部分的に使用したものは、あなたのものになります。
       公開する場合は、あなたの名前の下で行って下さい。

B. このソフトを利用することによって生じた損害等について、作者は
   責任を負わないものとします。各自の責任においてご利用下さい。

C. 著作者人格権は ttsuki に帰属します。著作権は放棄します。

D. 以上の３項は、ソース・実行バイナリの双方に適用されます。


## 今できること
- WinMM.dll P/Invoke [View Directory](https://github.com/ttsuki/ttsuki/tree/master/WinMM)
 - MidiOut / MidiIn Wrapping Class Library. MidiOutとかMidiInを簡単に使えるようにした。 [View Source](https://github.com/ttsuki/ttsuki/blob/master/WinMM/MidiIO.cs)
 - WaveOut / WaveIn Wrapping Class Library. WaveOutとかWaveInを簡単に使えるようにした。 [View Source](https://github.com/ttsuki/ttsuki/blob/master/WinMM/WaveIO.cs)
 - WaveDSP - Volume. Gain(dB) を指定して Waveform の 音量を変える。[View Source](https://github.com/ttsuki/ttsuki/blob/master/WinMM/WaveDSP.cs)
 - ACM MP3 Decoder Class. Windows標準のAudio Codec Manager を使って簡単にMP3デコードできる。[View Source](https://github.com/ttsuki/ttsuki/blob/master/WinMM/AcmMp3Decoder.cs)
- Windows Messaging [View Directory](https://github.com/ttsuki/ttsuki/tree/master/Messaging)
 - Windows Messaging Window for Multimedia callback. [View Source](https://github.com/ttsuki/ttsuki/blob/master/Messaging/MessageWindow.cs)
 - Windows Messaging Thread for Multimedia callback. [View Source](https://github.com/ttsuki/ttsuki/blob/master/Messaging/MessageThread.cs)
- Net [View Directory](https://github.com/ttsuki/ttsuki/tree/master/Net)
 - Wake On Lan. [View Source](https://github.com/ttsuki/ttsuki/blob/master/Net/WakeOnLan.cs)
- Keyboard Hook - 特定のキー入力を他のキー入力に変更したり。[View Source](https://github.com/ttsuki/ttsuki/blob/master/KeyboardHook.cs)

##できる予定のこと

- 未定
