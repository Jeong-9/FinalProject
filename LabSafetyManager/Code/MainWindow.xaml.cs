using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace LabSafetyManager
{
    public partial class MainWindow : Window
    {
        // 실제 장치 IP에 맞게 수정하세요.
        private const string CONTROL_PC_IP = "192.168.0.38"; // 통제실 PC
        private const string PI_IP = "192.168.0.32";         // 실험실 라즈베리파이
        private const int FALL_TCP_PORT = 9999;
        private const int EMERGENCY_UDP_PORT = 9998;

        private readonly FallTcpClient _fallClient;
        private readonly IntercomService _intercom;
        private readonly Storyboard _flash;
        private readonly Storyboard _beacon;
        private readonly DispatcherTimer _clock = new();

        private DispatcherTimer? _cooldownTimer;
        private MediaPlayer? _siren;

        private bool _emergencyActive;
        private bool _armed = true;
        private bool _cameraConnected;
        private int _frameCount;
        private int _cooldownRemaining;

        private string _lastEmergencySource = "UNKNOWN";
        private string _lastEmergencyTimestamp = string.Empty;

        public ObservableCollection<LogEntry> Logs { get; } = new();

        public MainWindow()
        {
            InitializeComponent();
            LogListView.ItemsSource = Logs;

            _flash = (Storyboard)Resources["FlashStoryboard"];
            _beacon = (Storyboard)Resources["BeaconStoryboard"];

            ClockText.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            _clock.Interval = TimeSpan.FromSeconds(1);
            _clock.Tick += (_, _) =>
                ClockText.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            _clock.Start();

            TrySetupSiren();

            // TCP 9999 영상 / UDP 9998 비상 신호
            _fallClient = new FallTcpClient(
                CONTROL_PC_IP,
                FALL_TCP_PORT,
                EMERGENCY_UDP_PORT);

            _fallClient.EmergencyReceived += OnEmergencyReceived;
            _fallClient.FrameReceived += OnFallFrame;
            _fallClient.ConnectionChanged += OnFallConnectionChanged;

            // UDP 10000 음성 송신
            _intercom = new IntercomService(PI_IP);

            CameraCombo.IsEnabled = false;
            CameraCombo.Visibility = Visibility.Collapsed;

            Loaded += OnWindowLoaded;
            Closed += OnWindowClosed;
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            AddLog(LogLevel.Info, "보안실 시스템 시작");
            AddLog(
                LogLevel.Info,
                $"통제실 연결 시도 중 ({CONTROL_PC_IP}:{FALL_TCP_PORT})");

            ShowStandby();
            _fallClient.StartUdpListener();
            _fallClient.StartTcp();
        }

        private void OnWindowClosed(object? sender, EventArgs e)
        {
            _cooldownTimer?.Stop();
            _intercom.Dispose();
            _fallClient.StopAll();
            _clock.Stop();

            try
            {
                _siren?.Stop();
                _siren?.Close();
            }
            catch
            {
                // 종료 중 예외는 무시한다.
            }
        }

        // ================================================================
        // 대기 화면
        // ================================================================

        private void ShowStandby()
        {
            CameraOverlay.Visibility =
                _cameraConnected ? Visibility.Collapsed : Visibility.Visible;
            CameraBadge.Opacity = _cameraConnected ? 1.0 : 0.3;

            SetCameraStatus(
                _cameraConnected,
                _cameraConnected ? "통제실 연결됨" : "대기");

            PersonStatusText.Text = "—";
            FallStatusText.Text = "—";
            GasStatusText.Text = "—";
            ButtonStatusText.Text = "—";

            SetLevel("정상", LogLevel.Info);
        }

        // ================================================================
        // FallTcpClient 이벤트
        // ================================================================

        private void OnEmergencyReceived(object? sender, string payload)
        {
            Dispatcher.InvokeAsync(() =>
            {
                ParseEmergencyPayload(
                    payload,
                    out string source,
                    out string timestamp);

                string sourceName = GetSourceDisplayName(source);

                AddLog(
                    LogLevel.Danger,
                    $"{sourceName} 신호 수신: {timestamp}");

                UpdateEmergencyStatus(source);
                SetLevel("비상", LogLevel.Danger);

                if (_armed && !_emergencyActive)
                {
                    EnterEmergency(
                        timestamp,
                        source,
                        addDetectionHistory: true);
                }
            });
        }

        private void OnFallFrame(object? sender, byte[] jpg)
        {
            ShowFrame(jpg);
        }

        private void OnFallConnectionChanged(object? sender, bool connected)
        {
            Dispatcher.InvokeAsync(() =>
            {
                _cameraConnected = connected;

                if (connected)
                {
                    SetCameraStatus(true, "통제실 연결됨");
                    CameraOverlay.Visibility = Visibility.Collapsed;
                    AddLog(LogLevel.Info, "통제실 TCP 연결 성공");
                }
                else
                {
                    SetCameraStatus(false, "연결 끊김");
                    AddLog(LogLevel.Warning, "통제실 TCP 연결 끊김");

                    if (!_emergencyActive)
                    {
                        CameraOverlay.Visibility = Visibility.Visible;
                    }
                }
            });
        }

        // ================================================================
        // 영상 표시
        // ================================================================

        private void ShowFrame(byte[] jpg)
        {
            BitmapImage bmp;

            try
            {
                bmp = new BitmapImage();
                using var ms = new MemoryStream(jpg);

                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = ms;
                bmp.EndInit();
                bmp.Freeze();
            }
            catch
            {
                return;
            }

            Dispatcher.InvokeAsync(() =>
            {
                VideoImage.Source = bmp;
                CameraOverlay.Visibility = Visibility.Collapsed;

                _frameCount++;
                CameraBadge.Opacity =
                    (_frameCount / 15 % 2 == 0) ? 1.0 : 0.4;
            });
        }

        // ================================================================
        // 상태 표시
        // ================================================================

        private void SetCameraStatus(bool connected, string text)
        {
            CamDot.Fill = new SolidColorBrush(
                connected
                    ? Color.FromRgb(0x2E, 0xCC, 0x71)
                    : Color.FromRgb(0xE7, 0x4C, 0x3C));

            CamStatusText.Text = text;
        }

        private void SetLevel(string text, LogLevel level)
        {
            LevelText.Text = text;

            var (badge, dot) = level switch
            {
                LogLevel.Danger => ("#5A1A1A", "#E74C3C"),
                LogLevel.Warning => ("#5A4A0F", "#F1C40F"),
                _ => ("#1F4D2E", "#2ECC71"),
            };

            LevelBadge.Background =
                (Brush)new BrushConverter().ConvertFromString(badge)!;
            OverallBadge.Background =
                (Brush)new BrushConverter().ConvertFromString(badge)!;
            OverallDot.Fill =
                (Brush)new BrushConverter().ConvertFromString(dot)!;
            OverallText.Text = text;
        }

        private void UpdateEmergencyStatus(string source)
        {
            switch (source)
            {
                case "GAS_SENSOR":
                    GasStatusText.Text = "감지됨";
                    break;

                case "EME_BUTTON":
                    ButtonStatusText.Text = "감지됨";
                    break;

                case "FALL_DOWN":
                    PersonStatusText.Text = "감지됨";
                    FallStatusText.Text = "감지됨";
                    break;

                case "WPF_BUTTON":
                    // 관리자 수동 호출은 별도의 센서 상태를 변경하지 않는다.
                    break;

                default:
                    AddLog(
                        LogLevel.Warning,
                        $"알 수 없는 비상 원인: {source}");
                    break;
            }
        }

        // ================================================================
        // 비상 상태
        // ================================================================

        private void EnterEmergency(
            string timestamp,
            string source,
            bool addDetectionHistory)
        {
            _emergencyActive = true;
            _lastEmergencySource = NormalizeSource(source);
            _lastEmergencyTimestamp = string.IsNullOrWhiteSpace(timestamp)
                ? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                : timestamp;

            EmergencyTime.Text = $"감지 시각  {_lastEmergencyTimestamp}";
            EmergencySourceText.Text =
                GetEmergencyMessage(_lastEmergencySource);

            if (addDetectionHistory)
            {
                AddDetectionHistory(
                    _lastEmergencyTimestamp,
                    _lastEmergencySource);
            }

            EmergencyOverlay.Visibility = Visibility.Visible;
            _flash.Begin(this, true);
            _beacon.Begin(this, true);
            SetLevel("비상", LogLevel.Danger);

            StartIntercom();

            try
            {
                _siren?.Play();
            }
            catch (Exception ex)
            {
                AddLog(
                    LogLevel.Warning,
                    $"사이렌 재생 실패: {ex.Message}");
            }
        }

        private void StartIntercom()
        {
            if (_intercom.IsRunning)
            {
                return;
            }

            _intercom.Start();

            if (_intercom.IsRunning)
            {
                AddLog(
                    LogLevel.Info,
                    $"인터컴 음성 송신 시작 ({PI_IP}:10000)");
            }
            else
            {
                AddLog(
                    LogLevel.Warning,
                    "인터컴 시작 실패 — 보안실 마이크 장치를 확인하세요");
            }
        }

        /// <summary>
        /// 비상 화면 확인: 화면과 사이렌만 닫고 인터컴은 유지한다.
        /// 30초 안에 경고를 완전히 해제하지 않으면 같은 원인으로 재경고한다.
        /// </summary>
        private void AckEmergency()
        {
            if (!_emergencyActive)
            {
                return;
            }

            StopEmergencyVisuals();

            _emergencyActive = false;
            _armed = false;
            _cooldownRemaining = 30;

            AddLog(
                LogLevel.Warning,
                "비상 확인 — 30초 내 경고 해제 필요");

            _cooldownTimer?.Stop();
            _cooldownTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };

            _cooldownTimer.Tick += (_, _) =>
            {
                _cooldownRemaining--;

                if (_cooldownRemaining > 0)
                {
                    return;
                }

                _cooldownTimer?.Stop();
                _armed = true;

                AddLog(
                    LogLevel.Danger,
                    "쿨다운 만료 → 비상 상황 재표시");

                EnterEmergency(
                    _lastEmergencyTimestamp,
                    _lastEmergencySource,
                    addDetectionHistory: false);
            };

            _cooldownTimer.Start();
        }

        /// <summary>
        /// 경고 완전 해제: 화면, 사이렌, 인터컴을 종료하고 감지를 재활성화한다.
        /// </summary>
        private void ClearEmergency()
        {
            StopEmergencyVisuals();
            _emergencyActive = false;

            _cooldownTimer?.Stop();
            _cooldownTimer = null;

            _fallClient.SendClearSignal();

            if (_intercom.IsRunning)
            {
                _intercom.Stop();
                AddLog(LogLevel.Info, "인터컴 음성 송신 종료");
            }

            _armed = false;
            _fallClient.SetCooldown(1);

            ShowStandby();
            AddLog(
                LogLevel.Info,
                "경고 해제 — 통제실 자동 리셋 요청");

            var resumeTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };

            resumeTimer.Tick += (_, _) =>
            {
                resumeTimer.Stop();
                _armed = true;
                AddLog(LogLevel.Info, "감지 재활성화 완료");
            };

            resumeTimer.Start();
        }

        private void StopEmergencyVisuals()
        {
            try
            {
                _flash.Stop(this);
                _beacon.Stop(this);
            }
            catch
            {
                // 애니메이션이 시작되지 않은 경우 무시한다.
            }

            EmergencyOverlay.Visibility = Visibility.Collapsed;

            try
            {
                _siren?.Stop();
            }
            catch
            {
                // 사이렌 종료 오류는 무시한다.
            }
        }

        private void AddDetectionHistory(string timestamp, string source)
        {
            NoDetectionText.Visibility = Visibility.Collapsed;

            var entry = new TextBlock
            {
                Text = $"[{timestamp}] {GetSourceDisplayName(source)}",
                Foreground = new SolidColorBrush(
                    Color.FromRgb(0xE8, 0xEA, 0xED)),
                FontSize = 13,
                Margin = new Thickness(0, 1, 0, 1),
                TextWrapping = TextWrapping.Wrap
            };

            DetectionLogPanel.Children.Insert(0, entry);

            while (DetectionLogPanel.Children.Count > 20)
            {
                DetectionLogPanel.Children.RemoveAt(
                    DetectionLogPanel.Children.Count - 1);
            }

            DetectionScroller.ScrollToTop();
        }

        // ================================================================
        // 버튼
        // ================================================================

        private void TestButton_Click(object sender, RoutedEventArgs e)
        {
            AddLog(LogLevel.Warning, "테스트: 쓰러짐 감지 강제 실행");

            if (!_emergencyActive)
            {
                UpdateEmergencyStatus("FALL_DOWN");
                EnterEmergency(
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    "FALL_DOWN",
                    addDetectionHistory: true);
            }
        }

        private void CallEmergencyButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            AddLog(LogLevel.Danger, "관리자 수동 비상 호출");

            if (!_emergencyActive)
            {
                EnterEmergency(
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    "WPF_BUTTON",
                    addDetectionHistory: true);
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            ClearEmergency();
        }

        private void AckButton_Click(object sender, RoutedEventArgs e)
        {
            AckEmergency();
        }

        private async void RestartCameraButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            AddLog(LogLevel.Info, "TCP 재연결 시도");

            _fallClient.StopTcp();
            await Task.Delay(300);
            _fallClient.StartTcp();
        }

        // 현재 화면에서는 로컬 USB 카메라를 사용하지 않는다.
        private void CameraCombo_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
        }

        private void RefreshCamerasButton_Click(
            object sender,
            RoutedEventArgs e)
        {
        }

        private void SaveLogButton_Click(object sender, RoutedEventArgs e)
        {
            if (Logs.Count == 0)
            {
                MessageBox.Show(
                    "저장할 로그가 없습니다.",
                    "로그 저장",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var dialog = new SaveFileDialog
            {
                FileName = $"lab_log_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
                Filter = "텍스트 파일 (*.txt)|*.txt|모든 파일 (*.*)|*.*"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                var lines = Logs
                    .Reverse()
                    .Select(log => log.ToString());

                File.WriteAllLines(dialog.FileName, lines);
                AddLog(
                    LogLevel.Info,
                    $"로그 저장 완료 ({Logs.Count}건)");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"저장 실패: {ex.Message}",
                    "오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ================================================================
        // 이벤트 로그
        // ================================================================

        private void AddLog(LogLevel level, string message)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.InvokeAsync(() => AddLog(level, message));
                return;
            }

            Logs.Insert(0, new LogEntry(level, message));

            while (Logs.Count > 500)
            {
                Logs.RemoveAt(Logs.Count - 1);
            }

            LogCountText.Text = $"{Logs.Count}건";
        }

        // ================================================================
        // 비상 원인 처리
        // ================================================================

        private static void ParseEmergencyPayload(
            string payload,
            out string source,
            out string timestamp)
        {
            var parts = payload.Split('|', 2);

            source = NormalizeSource(
                parts.Length > 0 ? parts[0] : "UNKNOWN");

            timestamp = parts.Length > 1 &&
                        !string.IsNullOrWhiteSpace(parts[1])
                ? parts[1]
                : DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        private static string NormalizeSource(string? source)
        {
            return source?.Trim().ToUpperInvariant() switch
            {
                "GAS" or "GAS_SENSOR" => "GAS_SENSOR",
                "BUTTON" or "EMERGENCY_BUTTON" or "EME_BUTTON" =>
                    "EME_BUTTON",
                "FALL" or "FALL_DOWN" => "FALL_DOWN",
                "WPF" or "WPF_BUTTON" => "WPF_BUTTON",
                _ => "UNKNOWN"
            };
        }

        private static string GetSourceDisplayName(string source)
        {
            return NormalizeSource(source) switch
            {
                "GAS_SENSOR" => "가스 누출",
                "EME_BUTTON" => "비상 버튼",
                "FALL_DOWN" => "쓰러짐 감지",
                "WPF_BUTTON" => "관리자 비상 호출",
                _ => "알 수 없는 비상"
            };
        }

        private static string GetEmergencyMessage(string source)
        {
            return NormalizeSource(source) switch
            {
                "GAS_SENSOR" =>
                    "⚠ 가스 누출에 의한 비상상황이 감지되었습니다",
                "EME_BUTTON" =>
                    "⚠ 비상 버튼에 의한 비상상황이 감지되었습니다",
                "FALL_DOWN" =>
                    "⚠ 쓰러짐 감지에 의한 비상상황이 감지되었습니다",
                "WPF_BUTTON" =>
                    "⚠ 관리자에 의해 비상상황이 호출되었습니다",
                _ =>
                    "⚠ 비상상황이 감지되었습니다"
            };
        }

        // ================================================================
        // 사이렌
        // ================================================================

        private void TrySetupSiren()
        {
            try
            {
                string path = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "siren.wav");

                if (!File.Exists(path))
                {
                    return;
                }

                _siren = new MediaPlayer();
                _siren.Open(new Uri(path));
                _siren.MediaEnded += (_, _) =>
                {
                    if (_siren == null)
                    {
                        return;
                    }

                    _siren.Position = TimeSpan.Zero;
                    _siren.Play();
                };
            }
            catch
            {
                _siren = null;
            }
        }
    }
}
