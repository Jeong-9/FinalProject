using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;

namespace LabSafetyManager
{
    /// <summary>
    /// 보안실 마이크 → Pi(실험실) UDP 10000 송신
    /// 비상 시 자동 시작, 해제 시 자동 종료
    /// </summary>
    public class IntercomService : IDisposable
    {
        private const int VOICE_PORT  = 10000;
        private const int RATE        = 16000;
        private const int CHANNELS    = 1;
        private const int BITS        = 16;
        private const int BLOCK_MS    = 40;   // 40ms 단위 전송

        private readonly string _piIp;
        private WaveInEvent?   _waveIn;
        private UdpClient?     _udp;
        private bool           _running = false;

        public bool IsRunning => _running;

        public IntercomService(string piIp)
        {
            _piIp = piIp;
        }

        // ── 인터컴 시작 (비상 발생 시 호출) ──────────────────
        public void Start()
        {
            if (_running) return;
            _running = true;

            try
            {
                _udp = new UdpClient();
                _udp.Connect(_piIp, VOICE_PORT);

                _waveIn = new WaveInEvent
                {
                    WaveFormat    = new WaveFormat(RATE, BITS, CHANNELS),
                    BufferMilliseconds = BLOCK_MS,
                };
                _waveIn.DataAvailable += OnDataAvailable;
                _waveIn.StartRecording();

                System.Diagnostics.Debug.WriteLine($"[인터컴] 시작 → {_piIp}:{VOICE_PORT}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[인터컴] 시작 실패: {ex.Message}");
                _running = false;
            }
        }

        // ── 인터컴 종료 (비상 해제 시 호출) ─────────────────
        public void Stop()
        {
            if (!_running) return;
            _running = false;

            try { _waveIn?.StopRecording(); } catch { }
            try { _waveIn?.Dispose(); }       catch { }
            try { _udp?.Close(); }            catch { }
            _waveIn = null;
            _udp    = null;

            System.Diagnostics.Debug.WriteLine("[인터컴] 종료");
        }

        // ── 마이크 데이터 → UDP 전송 ─────────────────────────
        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (!_running || _udp == null) return;
            try
            {
                var buf = new byte[e.BytesRecorded];
                Buffer.BlockCopy(e.Buffer, 0, buf, 0, e.BytesRecorded);
                _udp.Send(buf, buf.Length);
            }
            catch { }
        }

        public void Dispose() => Stop();
    }
}
