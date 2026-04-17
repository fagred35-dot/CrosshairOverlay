using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CrosshairOverlay
{
    public class OverlayForm : Form
    {
        #region Win32 Constants & Imports
        private const int WS_EX_LAYERED = 0x80000;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int WS_EX_TOPMOST = 0x8;
        private const int WS_EX_TOOLWINDOW = 0x80;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int ULW_ALPHA = 0x02;
        private const byte AC_SRC_OVER = 0x00;
        private const byte AC_SRC_ALPHA = 0x01;
        private const int HWND_TOPMOST = -1;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const int WM_HOTKEY = 0x0312;

        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;

        private const int INPUT_MOUSE = 0;
        private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx, dy;
            public uint mouseData, dwFlags, time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public int type;
            public MOUSEINPUT mi;
        }

        [StructLayout(LayoutKind.Sequential)] private struct POINT { public int x, y; }
        [StructLayout(LayoutKind.Sequential)] private struct SIZE { public int cx, cy; }
        [StructLayout(LayoutKind.Sequential)] private struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hDC);
        [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr hObject);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        // Low-level mouse hook for physical LMB tracking
        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll")] private static extern IntPtr GetModuleHandle(string? lpModuleName);
        [DllImport("winmm.dll")] private static extern uint timeBeginPeriod(uint uPeriod);
        [DllImport("winmm.dll")] private static extern uint timeEndPeriod(uint uPeriod);
        [DllImport("user32.dll")] private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, IntPtr dwExtraInfo);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool DestroyIcon(IntPtr hIcon);
        [DllImport("user32.dll")] private static extern int GetSystemMetrics(int nIndex);
        [DllImport("gdi32.dll")] private static extern bool BitBlt(IntPtr hdcDest, int x, int y, int cx, int cy, IntPtr hdcSrc, int x1, int y1, int rop);
        private const int SM_XVIRTUALSCREEN = 76;
        private const int SM_YVIRTUALSCREEN = 77;
        private const int SM_CXVIRTUALSCREEN = 78;
        private const int SM_CYVIRTUALSCREEN = 79;
        private const int SRCCOPY = 0x00CC0020;

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData, flags, time;
            public IntPtr dwExtraInfo;
        }

        private static readonly IntPtr SYNTHETIC_EXTRA_INFO = new IntPtr(0xDEAD);
        private IntPtr _mouseHook = IntPtr.Zero;
        private LowLevelMouseProc? _mouseHookDelegate;
        internal volatile bool _physicalLmbDown = false;
        #endregion

        #region Enums
        public enum CrosshairStyle
        {
            Cross, Circle, Dot, CrossWithCircle, Chevron, TShape, Diamond, Arrow, Plus,
            XShape, TriangleDown, Crosshairs, SquareBrackets, Wings,
            CustomImage
        }
        #endregion

        // Auto-update: change these to your GitHub repo
        internal const string APP_VERSION = "2.1.0";
        private const string GITHUB_REPO = "fagred35-dot/CrosshairOverlay";

        #region Crosshair Settings
        internal CrosshairStyle _style = CrosshairStyle.Cross;
        internal int _size = 20;
        internal int _thickness = 2;
        internal int _gap = 4;
        internal int _opacity = 255;
        internal int _offsetX = 0;
        internal int _offsetY = 0;

        internal bool _showDot = true;
        internal int _dotSize = 2;
        internal bool _dotPulse = false;

        internal Color _crossColor = Color.FromArgb(0, 255, 0);
        internal Color _crossColor2 = Color.FromArgb(0, 200, 255);
        internal bool _useGradient = false;
        internal bool _rainbowMode = false;

        internal bool _showOutline = true;
        internal Color _outlineColor = Color.Black;
        internal float _outlineWidth = 1f;

        internal bool _showShadow = false;
        internal Color _shadowColor = Color.FromArgb(80, 0, 0, 0);
        internal int _shadowOffsetX = 2;
        internal int _shadowOffsetY = 2;

        internal float _rotation = 0f;
        internal bool _spin = false;
        internal float _spinSpeed = 2f;
        internal bool _antiAlias = true;

        internal string _customImagePath = "";
        internal Bitmap? _customImageCache = null;
        #endregion

        #region AutoClicker Settings
        internal bool _autoClickerEnabled = false;
        internal bool _clickOnHold = true;  // true = click while LMB held, false = toggle mode
        internal volatile int _clicksPerSecond = 30;
        internal volatile bool _clickerRunning = false;
        private readonly List<Thread> _clickThreads = new();
        private volatile int _threadCount = 1;
        private readonly object _clickLock = new();
        #endregion

        #region Dynamic Crosshair
        internal bool _dynamicCrosshair = false;
        internal float _dynamicMaxSpread = 8f;
        internal float _dynamicRecovery = 0.15f;
        private int _lastMouseX, _lastMouseY;
        private float _mouseVelocity = 0f;
        #endregion

        #region Autoclicker Extended
        internal bool _rightClickMode = false;
        internal bool _randomDelay = false;
        internal int _randomDelayPercent = 20;
        internal long _clickCounter = 0;
        // Burst mode: when enabled and _clickOnHold, fires exactly _burstCount clicks per LMB press then waits for release.
        internal bool _burstMode = false;
        internal int _burstCount = 3;
        private int _burstRemaining = 0;
        private bool _burstLastLmbState = false;
        // Accessor avoids CS1690 when callers read field via the Form reference.
        internal long GetClickCounter() => Interlocked.Read(ref _clickCounter);
        internal void ResetClickCounter() => Interlocked.Exchange(ref _clickCounter, 0);
        #endregion

        #region Visual Effects
        internal bool _glowEnabled = false;
        internal int _glowSize = 6;
        internal int _glowAlpha = 80;
        internal bool _hitMarkerEnabled = false;
        internal volatile float _hitMarkerProgress = 0f;
        internal float _hitMarkerSize = 12f;
        #endregion

        #region Animation State
        internal float _pulseScale = 1.0f;
        private float _dotPulseSine = 0f;
        private float _hue = 0f;
        internal float _dynamicSpread = 0f;
        internal float _currentOpacity = 255f;
        internal int _targetOpacity = 255;
        internal bool _isVisible = true;
        internal bool _needsStaticRender = true;
        private bool _lastAutoClickerState = false;
        #endregion

        #region Recording
        internal RecorderEngine _recorder = new();
        internal VideoGalleryForm? _galleryForm;
        internal int _notifPosition = 1; // 0=TL, 1=TR, 2=BL, 3=BR
        internal List<string> _audioApps = new();
        #endregion

        #region Hotkey Bindings
        // Each hotkey: [mod, vk]
        internal static readonly string[] HotkeyNames = {
            "", "Видимость", "Стиль", "Размер+", "Размер−",
            "Пульсация", "Прозрач.+", "Прозрач.−", "Цвет", "Сброс",
            "Автокликер", "Настройки", "Запись", "Повтор", "Галерея",
            "Аварийный стоп", "Скриншот", "Burst"
        };
        internal uint[] _hkMods = new uint[HOTKEY_COUNT + 1];
        internal uint[] _hkKeys = new uint[HOTKEY_COUNT + 1];

        private void InitDefaultHotkeys()
        {
            uint CS = 0x0002 | 0x0004; // Ctrl+Shift
            _hkMods[HK_TOGGLE] = CS;       _hkKeys[HK_TOGGLE] = 0x58;       // X
            _hkMods[HK_STYLE] = CS;        _hkKeys[HK_STYLE] = 0x5A;        // Z
            _hkMods[HK_SIZE_UP] = CS;      _hkKeys[HK_SIZE_UP] = 0xBB;      // =
            _hkMods[HK_SIZE_DOWN] = CS;    _hkKeys[HK_SIZE_DOWN] = 0xBD;    // -
            _hkMods[HK_PULSE] = CS;        _hkKeys[HK_PULSE] = 0x50;        // P
            _hkMods[HK_OPACITY_UP] = CS;   _hkKeys[HK_OPACITY_UP] = 0x26;   // Up
            _hkMods[HK_OPACITY_DOWN] = CS; _hkKeys[HK_OPACITY_DOWN] = 0x28; // Down
            _hkMods[HK_COLOR] = CS;        _hkKeys[HK_COLOR] = 0x43;        // C
            _hkMods[HK_RESET] = CS;        _hkKeys[HK_RESET] = 0x52;        // R
            _hkMods[HK_CLICKER_TOGGLE] = CS; _hkKeys[HK_CLICKER_TOGGLE] = 0x41; // A
            _hkMods[HK_SETTINGS] = 0;      _hkKeys[HK_SETTINGS] = 0x2D;     // INS
            _hkMods[HK_RECORD] = CS;       _hkKeys[HK_RECORD] = 0x78;       // F9
            _hkMods[HK_REPLAY_SAVE] = CS;  _hkKeys[HK_REPLAY_SAVE] = 0x79;  // F10
            _hkMods[HK_GALLERY] = CS;      _hkKeys[HK_GALLERY] = 0x47;      // G
            _hkMods[HK_SCREENSHOT] = CS;    _hkKeys[HK_SCREENSHOT] = 0x7B;    // F12
            _hkMods[HK_BURST_TOGGLE] = CS;  _hkKeys[HK_BURST_TOGGLE] = 0x42;  // B
            _hkMods[HK_EMERGENCY_STOP] = 0; _hkKeys[HK_EMERGENCY_STOP] = 0x1B; // Escape
        }

        internal void ReRegisterHotkeys()
        {
            for (int i = 1; i <= HOTKEY_COUNT; i++) UnregisterHotKey(Handle, i);
            for (int i = 1; i <= HOTKEY_COUNT; i++)
            {
                if (_hkKeys[i] != 0)
                    RegisterHotKey(Handle, i, _hkMods[i], _hkKeys[i]);
            }
        }

        internal static string HotkeyToString(uint mod, uint vk)
        {
            if (vk == 0) return "—";
            string s = "";
            if ((mod & 0x0002) != 0) s += "Ctrl+";
            if ((mod & 0x0004) != 0) s += "Shift+";
            if ((mod & 0x0001) != 0) s += "Alt+";
            s += VkToName(vk);
            return s;
        }

        private static string VkToName(uint vk) => vk switch
        {
            0x08 => "Backspace", 0x09 => "Tab", 0x0D => "Enter", 0x1B => "Esc",
            0x20 => "Space", 0x21 => "PgUp", 0x22 => "PgDn", 0x23 => "End",
            0x24 => "Home", 0x25 => "Left", 0x26 => "Up", 0x27 => "Right",
            0x28 => "Down", 0x2D => "INS", 0x2E => "DEL",
            >= 0x30 and <= 0x39 => ((char)vk).ToString(),
            >= 0x41 and <= 0x5A => ((char)vk).ToString(),
            >= 0x60 and <= 0x69 => "Num" + (vk - 0x60),
            >= 0x70 and <= 0x87 => "F" + (vk - 0x6F),
            0xBA => ";", 0xBB => "=", 0xBC => ",", 0xBD => "-",
            0xBE => ".", 0xBF => "/", 0xC0 => "`",
            0xDB => "[", 0xDC => "\\", 0xDD => "]", 0xDE => "'",
            _ => $"0x{vk:X2}"
        };
        #endregion

        #region System Objects
        private System.Windows.Forms.Timer _animTimer = null!;
        private System.Windows.Forms.Timer _topmostTimer = null!;
        internal NotifyIcon _trayIcon = null!;
        private ContextMenuStrip _trayMenu = null!;
        internal SettingsForm? _settingsForm;
        private ToolStripMenuItem _clickerStatusItem = null!;
        #endregion

        #region Hotkey IDs
        private const int HK_TOGGLE = 1;
        private const int HK_STYLE = 2;
        private const int HK_SIZE_UP = 3;
        private const int HK_SIZE_DOWN = 4;
        private const int HK_PULSE = 5;
        private const int HK_OPACITY_UP = 6;
        private const int HK_OPACITY_DOWN = 7;
        private const int HK_COLOR = 8;
        private const int HK_RESET = 9;
        private const int HK_CLICKER_TOGGLE = 10;
        private const int HK_SETTINGS = 11;
        private const int HK_RECORD = 12;
        private const int HK_REPLAY_SAVE = 13;
        private const int HK_GALLERY = 14;
        private const int HK_EMERGENCY_STOP = 15;
        private const int HK_SCREENSHOT = 16;
        private const int HK_BURST_TOGGLE = 17;
        private const int HOTKEY_COUNT = 17;
        private const int MOD_NOREPEAT = 0x4000; // Prevent key repeat
        #endregion

        internal static readonly string _settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CrosshairOverlay", "settings_v3.json");

        private static readonly Color[] _colorPresets = {
            Color.FromArgb(0, 255, 0), Color.FromArgb(255, 0, 0), Color.FromArgb(0, 255, 255),
            Color.FromArgb(255, 255, 255), Color.FromArgb(255, 255, 0), Color.FromArgb(255, 0, 255),
            Color.FromArgb(255, 128, 0)
        };
        private int _colorIndex = 0;

        public OverlayForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.StartPosition = FormStartPosition.Manual;
            this.Bounds = Screen.PrimaryScreen!.Bounds;

            InitDefaultHotkeys();
            LoadSettings();
            _currentOpacity = _opacity;
            _targetOpacity = _opacity;

            var initPos = Cursor.Position;
            _lastMouseX = initPos.X;
            _lastMouseY = initPos.Y;

            SetupTrayIcon();

            _animTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _animTimer.Tick += AnimTimer_Tick;
            _animTimer.Start();

            _topmostTimer = new System.Windows.Forms.Timer { Interval = 2000 };
            _topmostTimer.Tick += (s, e) =>
            {
                if (_isVisible && IsHandleCreated)
                    SetWindowPos(Handle, (IntPtr)HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            };
            _topmostTimer.Start();

            // Init recorder
            _recorder.Init();

            // Start clicker if it was saved as enabled
            UpdateClickerState();

            // Install low-level mouse hook
            InstallMouseHook();
        }

        internal void PauseTopmost() => _topmostTimer.Stop();
        internal void ResumeTopmost() => _topmostTimer.Start();

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
                return cp;
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            for (int i = 1; i <= HOTKEY_COUNT; i++)
            {
                if (_hkKeys[i] != 0)
                    RegisterHotKey(Handle, i, _hkMods[i], _hkKeys[i]);
            }
            RenderOverlay();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _animTimer.Stop(); _animTimer.Dispose();
            _topmostTimer.Stop(); _topmostTimer.Dispose();
            StopClicker();
            UninstallMouseHook();
            _recorder.Dispose();
            _galleryForm?.Close();
            for (int i = 1; i <= HOTKEY_COUNT; i++) UnregisterHotKey(Handle, i);
            SaveSettings();
            _trayIcon.Visible = false; _trayIcon.Dispose();
            _customImageCache?.Dispose();
            base.OnFormClosed(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                switch (m.WParam.ToInt32())
                {
                    case HK_TOGGLE:
                        _isVisible = !_isVisible;
                        _targetOpacity = _isVisible ? _opacity : 0;
                        if (_isVisible) Show();
                        break;
                    case HK_STYLE:
                        int next = ((int)_style + 1);
                        int styleCount = Enum.GetValues(typeof(CrosshairStyle)).Length;
                        if (next >= styleCount - 1 && _customImageCache == null) next = 0;
                        _style = (CrosshairStyle)(next % styleCount);
                        _needsStaticRender = true;
                        break;
                    case HK_SIZE_UP:
                        _size = Math.Min(100, _size + 2);
                        _needsStaticRender = true;
                        break;
                    case HK_SIZE_DOWN:
                        _size = Math.Max(4, _size - 2);
                        _needsStaticRender = true;
                        break;
                    case HK_PULSE:
                        _pulseScale = 1.6f;
                        break;
                    case HK_OPACITY_UP:
                        _opacity = Math.Min(255, _opacity + 25);
                        _targetOpacity = _opacity;
                        _needsStaticRender = true;
                        break;
                    case HK_OPACITY_DOWN:
                        _opacity = Math.Max(25, _opacity - 25);
                        _targetOpacity = _opacity;
                        _needsStaticRender = true;
                        break;
                    case HK_COLOR:
                        _colorIndex = (_colorIndex + 1) % _colorPresets.Length;
                        _crossColor = _colorPresets[_colorIndex];
                        _needsStaticRender = true;
                        break;
                    case HK_RESET:
                        ResetToDefaults();
                        break;
                    case HK_CLICKER_TOGGLE:
                        _autoClickerEnabled = !_autoClickerEnabled;
                        UpdateClickerState();
                        SaveSettings();
                        if (_settingsForm != null && !_settingsForm.IsDisposed)
                            _settingsForm.UpdateClickerStatus();
                        break;
                    case HK_SETTINGS:
                        OpenSettings();
                        break;
                    case HK_EMERGENCY_STOP:
                        if (_clickerRunning)
                        {
                            _autoClickerEnabled = false;
                            StopClicker();
                            SaveSettings();
                            if (_settingsForm != null && !_settingsForm.IsDisposed)
                                _settingsForm.UpdateClickerStatus();
                        }
                        break;
                    case HK_SCREENSHOT:
                        TakeScreenshot();
                        break;
                    case HK_BURST_TOGGLE:
                        _burstMode = !_burstMode;
                        SaveSettings();
                        _trayIcon?.ShowBalloonTip(1500, "Crosshair Overlay",
                            (Lang.IsRussian ? "Burst-режим: " : "Burst mode: ") + (_burstMode ? "ON" : "OFF"),
                            ToolTipIcon.Info);
                        break;
                }
            }
            else if (m.Msg == 0x007E)
            {
                this.Bounds = Screen.PrimaryScreen!.Bounds;
                _needsStaticRender = true;
            }
            base.WndProc(ref m);
        }

        internal void ResetToDefaults()
        {
            _style = CrosshairStyle.Cross;
            _crossColor = Color.FromArgb(0, 255, 0);
            _size = 20; _thickness = 2; _gap = 4;
            _opacity = 255; _targetOpacity = 255; _currentOpacity = 255;
            _showDot = true; _dotSize = 2; _dotPulse = false;
            _showOutline = true; _outlineWidth = 1f;
            _showShadow = false; _useGradient = false;
            _rainbowMode = false; _spin = false; _rotation = 0f;
            _offsetX = 0; _offsetY = 0; _antiAlias = true;
            _dynamicCrosshair = false; _dynamicSpread = 0f;
            _glowEnabled = false; _hitMarkerEnabled = false;
            _rightClickMode = false; _randomDelay = false;
            _needsStaticRender = true;
        }

        #region AutoClicker

        private void InstallMouseHook()
        {
            _mouseHookDelegate = MouseHookCallback;
            _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseHookDelegate, GetModuleHandle(null), 0);
        }

        private void UninstallMouseHook()
        {
            if (_mouseHook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_mouseHook);
                _mouseHook = IntPtr.Zero;
            }
        }

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var hookData = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                // Only track physical events (not our synthetic ones)
                if (hookData.dwExtraInfo != SYNTHETIC_EXTRA_INFO)
                {
                    int msg = wParam.ToInt32();
                    if (msg == WM_LBUTTONDOWN)
                        _physicalLmbDown = true;
                    else if (msg == WM_LBUTTONUP)
                        _physicalLmbDown = false;
                }
            }
            return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }

        internal void UpdateClickerState()
        {
            if (_autoClickerEnabled && !_clickerRunning)
                StartClicker();
            else if (!_autoClickerEnabled && _clickerRunning)
                StopClicker();

            // Update tray
            if (_clickerStatusItem != null)
                _clickerStatusItem.Text = _autoClickerEnabled ? Lang.ClickerActive : Lang.ClickerOff;
        }

        private void StartClicker()
        {
            lock (_clickLock)
            {
                if (_clickerRunning) return;
                _clickerRunning = true;
                timeBeginPeriod(1);

                int cps = _clicksPerSecond;
                // Each thread can do ~60-100 CPS with Thread.Sleep(1)
                int threadCount = Math.Clamp((int)Math.Ceiling(cps / 60.0), 1, 16);
                _threadCount = threadCount;

                _clickThreads.Clear();
                for (int i = 0; i < threadCount; i++)
                {
                    var t = new Thread(ClickLoop)
                    {
                        IsBackground = true,
                        Priority = ThreadPriority.Highest
                    };
                    _clickThreads.Add(t);
                    t.Start();
                    Thread.Sleep(1);
                }
            }
        }

        private void ClickLoop()
        {
            var sw = new Stopwatch();

            while (_clickerRunning)
            {
                if (_clickOnHold && !_physicalLmbDown)
                {
                    // Reset burst armer so the next press starts fresh
                    if (_burstMode) { _burstLastLmbState = false; _burstRemaining = 0; }
                    Thread.Sleep(1);
                    continue;
                }

                // Burst mode: fire exactly _burstCount clicks per LMB press, then wait for release.
                if (_clickOnHold && _burstMode)
                {
                    if (!_burstLastLmbState)
                    {
                        _burstLastLmbState = true;
                        Interlocked.Exchange(ref _burstRemaining, Math.Max(1, _burstCount));
                    }
                    if (Interlocked.Decrement(ref _burstRemaining) < 0)
                    {
                        // Already fired the requested amount — idle until button released.
                        Thread.Sleep(1);
                        continue;
                    }
                }

                int cps = _clicksPerSecond;
                int threads = _threadCount;
                int myCps = Math.Max(1, cps / threads);

                uint downFlag = _rightClickMode ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_LEFTDOWN;
                uint upFlag = _rightClickMode ? MOUSEEVENTF_RIGHTUP : MOUSEEVENTF_LEFTUP;

                sw.Restart();

                mouse_event(downFlag | upFlag, 0, 0, 0, SYNTHETIC_EXTRA_INFO);

                Interlocked.Add(ref _clickCounter, 1);
                if (_hitMarkerEnabled) _hitMarkerProgress = 1f;

                // Throttle: wait for interval based on this thread's share of CPS
                long intervalTicks = Stopwatch.Frequency / myCps;
                if (_randomDelay)
                {
                    double jitter = 1.0 + (Random.Shared.NextDouble() * 2 - 1) * (_randomDelayPercent / 100.0);
                    intervalTicks = (long)(intervalTicks * Math.Max(0.2, jitter));
                }

                long msToWait = intervalTicks * 1000 / Stopwatch.Frequency;
                if (msToWait > 2)
                    Thread.Sleep((int)(msToWait - 1));
                while (sw.ElapsedTicks < intervalTicks)
                {
                    if (!_clickerRunning) return;
                    Thread.SpinWait(10);
                }
            }
        }

        private void StopClicker()
        {
            lock (_clickLock)
            {
                _clickerRunning = false;

                foreach (var t in _clickThreads)
                    t.Join(500);
                _clickThreads.Clear();

                timeEndPeriod(1);
            }
        }



        #endregion

        #region Screenshot

        internal void TakeScreenshot()
        {
            try
            {
                int x = GetSystemMetrics(SM_XVIRTUALSCREEN);
                int y = GetSystemMetrics(SM_YVIRTUALSCREEN);
                int w = GetSystemMetrics(SM_CXVIRTUALSCREEN);
                int h = GetSystemMetrics(SM_CYVIRTUALSCREEN);
                if (w <= 0 || h <= 0) { w = Screen.PrimaryScreen!.Bounds.Width; h = Screen.PrimaryScreen!.Bounds.Height; x = 0; y = 0; }

                using var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmp))
                {
                    IntPtr dst = g.GetHdc();
                    IntPtr src = GetDC(IntPtr.Zero);
                    try { BitBlt(dst, 0, 0, w, h, src, x, y, SRCCOPY); }
                    finally { ReleaseDC(IntPtr.Zero, src); g.ReleaseHdc(dst); }
                }

                string baseDir = !string.IsNullOrWhiteSpace(_recorder.OutputDir)
                    ? _recorder.OutputDir!
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "CrosshairOverlay");
                string dir = Path.Combine(baseDir, "screenshots");
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, $"shot_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png");
                bmp.Save(file, ImageFormat.Png);

                _trayIcon?.ShowBalloonTip(2500, "Crosshair Overlay",
                    (Lang.IsRussian ? "Скриншот сохранён: " : "Screenshot saved: ") + Path.GetFileName(file),
                    ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                _trayIcon?.ShowBalloonTip(3000, "Crosshair Overlay",
                    (Lang.IsRussian ? "Ошибка скриншота: " : "Screenshot error: ") + ex.Message,
                    ToolTipIcon.Error);
            }
        }

        #endregion

        #region Animation Loop

        private void AnimTimer_Tick(object? sender, EventArgs e)
        {
            bool needsRender = _needsStaticRender;
            _needsStaticRender = false;

            if (Math.Abs(_currentOpacity - _targetOpacity) > 0.5f)
            {
                _currentOpacity += (_targetOpacity - _currentOpacity) * 0.18f;
                needsRender = true;
                if (_currentOpacity < 2f && !_isVisible) { Hide(); return; }
            }
            else if (Math.Abs(_currentOpacity - _targetOpacity) > 0.01f)
            {
                _currentOpacity = _targetOpacity;
                needsRender = true;
            }

            if (_rainbowMode && _isVisible)
            {
                _hue = (_hue + 1.5f) % 360f;
                _crossColor = HslToColor(_hue, 1.0, 0.5);
                needsRender = true;
            }

            if (_spin && _isVisible)
            {
                _rotation = (_rotation + _spinSpeed) % 360f;
                needsRender = true;
            }

            if (_pulseScale > 1.005f)
            {
                _pulseScale += (1.0f - _pulseScale) * 0.12f;
                if (_pulseScale <= 1.005f) _pulseScale = 1.0f;
                needsRender = true;
            }

            if (_dotPulse && _showDot && _isVisible)
            {
                _dotPulseSine += 0.12f;
                if (_dotPulseSine > MathF.PI * 2) _dotPulseSine -= MathF.PI * 2;
                needsRender = true;
            }

            if (_autoClickerEnabled != _lastAutoClickerState)
            {
                _lastAutoClickerState = _autoClickerEnabled;
                needsRender = true;
            }

            // Dynamic crosshair: track mouse movement
            if (_dynamicCrosshair && _isVisible)
            {
                var curPos = Cursor.Position;
                float dx = curPos.X - _lastMouseX;
                float dy = curPos.Y - _lastMouseY;
                _lastMouseX = curPos.X;
                _lastMouseY = curPos.Y;
                _mouseVelocity = MathF.Sqrt(dx * dx + dy * dy);
                float targetSpread = Math.Min(_mouseVelocity * 0.5f, _dynamicMaxSpread);
                _dynamicSpread += (targetSpread - _dynamicSpread) * _dynamicRecovery;
                if (MathF.Abs(_dynamicSpread) > 0.1f) needsRender = true;
            }
            else if (!_dynamicCrosshair && _dynamicSpread > 0.1f)
            {
                _dynamicSpread *= 0.85f;
                if (_dynamicSpread < 0.1f) _dynamicSpread = 0f;
                needsRender = true;
            }

            // Hit marker fade
            if (_hitMarkerProgress > 0.01f)
            {
                _hitMarkerProgress *= 0.88f;
                if (_hitMarkerProgress < 0.01f) _hitMarkerProgress = 0f;
                needsRender = true;
            }

            if (needsRender && _currentOpacity > 0.5f)
                RenderOverlay();
        }

        #endregion

        #region Rendering

        internal void RenderOverlay()
        {
            if (!IsHandleCreated) return;

            var screen = Screen.FromControl(this);
            int w = screen.Bounds.Width, h = screen.Bounds.Height;
            int cx = w / 2 + _offsetX, cy = h / 2 + _offsetY;

            using var bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            using var g = Graphics.FromImage(bmp);

            g.SmoothingMode = _antiAlias ? SmoothingMode.AntiAlias : SmoothingMode.None;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighSpeed;

            if (_rotation != 0f || _pulseScale != 1.0f)
            {
                g.TranslateTransform(cx, cy);
                if (_rotation != 0f) g.RotateTransform(_rotation);
                if (_pulseScale != 1.0f) g.ScaleTransform(_pulseScale, _pulseScale);
                g.TranslateTransform(-cx, -cy);
            }

            int alpha = (int)Math.Clamp((_currentOpacity / 255f) * 255f, 0, 255);
            DrawCrosshairFull(g, cx, cy, alpha);

            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memDc = CreateCompatibleDC(screenDc);
            IntPtr hBitmap = bmp.GetHbitmap(Color.FromArgb(0));
            IntPtr oldBitmap = SelectObject(memDc, hBitmap);
            try
            {
                var pptDst = new POINT { x = screen.Bounds.Left, y = screen.Bounds.Top };
                var psize = new SIZE { cx = w, cy = h };
                var pptSrc = new POINT { x = 0, y = 0 };
                var blend = new BLENDFUNCTION { BlendOp = AC_SRC_OVER, SourceConstantAlpha = 255, AlphaFormat = AC_SRC_ALPHA };
                UpdateLayeredWindow(Handle, screenDc, ref pptDst, ref psize, memDc, ref pptSrc, 0, ref blend, ULW_ALPHA);
            }
            finally
            {
                SelectObject(memDc, oldBitmap);
                DeleteObject(hBitmap);
                DeleteDC(memDc);
                ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        internal void DrawCrosshairFull(Graphics g, int cx, int cy, int alpha)
        {
            if (_showShadow && _style != CrosshairStyle.CustomImage)
            {
                var state = g.Save();
                g.TranslateTransform(_shadowOffsetX, _shadowOffsetY);
                using var shadowBrush = new SolidBrush(Color.FromArgb(Math.Min(alpha, _shadowColor.A), _shadowColor));
                DrawStyle(g, cx, cy, shadowBrush, null, alpha / 2);
                g.Restore(state);
            }

            // Glow effect
            if (_glowEnabled && _style != CrosshairStyle.CustomImage)
            {
                int gs = _size + _glowSize;
                using var glowBrush = new SolidBrush(Color.FromArgb(
                    Math.Min(alpha, _glowAlpha), _crossColor));
                g.FillEllipse(glowBrush, cx - gs, cy - gs, gs * 2, gs * 2);
            }

            Brush mainBrush;
            if (_useGradient)
            {
                var rect = new Rectangle(cx - _size - 2, cy - _size - 2, _size * 2 + 4, _size * 2 + 4);
                mainBrush = new LinearGradientBrush(rect,
                    Color.FromArgb(alpha, _crossColor),
                    Color.FromArgb(alpha, _crossColor2), 45f);
            }
            else
            {
                mainBrush = new SolidBrush(Color.FromArgb(alpha, _crossColor));
            }

            SolidBrush? outlineBrush = null;
            if (_showOutline)
                outlineBrush = new SolidBrush(Color.FromArgb(Math.Min(alpha, 200), _outlineColor));

            DrawStyle(g, cx, cy, mainBrush, outlineBrush, alpha);

            if (_showDot && _style != CrosshairStyle.Dot && _style != CrosshairStyle.CustomImage)
            {
                float dSize = _dotSize + (_dotPulse ? MathF.Sin(_dotPulseSine) * 1.5f : 0f);
                dSize = Math.Max(0.5f, dSize);
                if (outlineBrush != null)
                    g.FillEllipse(outlineBrush, cx - dSize - _outlineWidth, cy - dSize - _outlineWidth,
                        (dSize + _outlineWidth) * 2, (dSize + _outlineWidth) * 2);
                g.FillEllipse(mainBrush, cx - dSize, cy - dSize, dSize * 2, dSize * 2);
            }

            mainBrush.Dispose();
            outlineBrush?.Dispose();

            // Hit marker animation
            if (_hitMarkerEnabled && _hitMarkerProgress > 0.01f)
            {
                float hms = _hitMarkerSize * (1f + (1f - _hitMarkerProgress) * 0.5f);
                int hma = (int)(alpha * _hitMarkerProgress);
                using var hmPen = new Pen(Color.FromArgb(hma, 255, 255, 255), 2f);
                float d = hms * 0.7f;
                g.DrawLine(hmPen, cx - d, cy - d, cx - d * 0.4f, cy - d * 0.4f);
                g.DrawLine(hmPen, cx + d, cy - d, cx + d * 0.4f, cy - d * 0.4f);
                g.DrawLine(hmPen, cx - d, cy + d, cx - d * 0.4f, cy + d * 0.4f);
                g.DrawLine(hmPen, cx + d, cy + d, cx + d * 0.4f, cy + d * 0.4f);
            }

            // Macro indicator dot (bottom-right corner)
            int dotRadius = 4;
            var screen2 = Screen.FromControl(this);
            int dotX = screen2.Bounds.Width - 20;
            int dotY2 = screen2.Bounds.Height - 20;
            Color dotColor = _autoClickerEnabled
                ? Color.FromArgb(alpha, 0, 220, 60)
                : Color.FromArgb(alpha, 220, 40, 40);
            using var indicatorBrush = new SolidBrush(dotColor);
            g.FillEllipse(indicatorBrush, dotX - dotRadius, dotY2 - dotRadius, dotRadius * 2, dotRadius * 2);
        }

        private void DrawStyle(Graphics g, int cx, int cy, Brush brush, SolidBrush? outlineBrush, int alpha)
        {
            float s = _size;
            float gap = _gap + _dynamicSpread;
            float t = _thickness;
            float ow = _outlineWidth;

            switch (_style)
            {
                case CrosshairStyle.Cross:
                case CrosshairStyle.Plus:
                    DrawCrossLines(g, cx, cy, s, gap, t, ow, brush, outlineBrush, false);
                    break;
                case CrosshairStyle.TShape:
                    DrawCrossLines(g, cx, cy, s, gap, t, ow, brush, outlineBrush, true);
                    break;
                case CrosshairStyle.Circle:
                    DrawCircleStyle(g, cx, cy, s, t, ow, brush, outlineBrush);
                    break;
                case CrosshairStyle.CrossWithCircle:
                    DrawCrossLines(g, cx, cy, s, gap, t, ow, brush, outlineBrush, false);
                    DrawCircleStyle(g, cx, cy, s, t, ow, brush, outlineBrush);
                    break;
                case CrosshairStyle.Dot:
                    DrawDotStyle(g, cx, cy, s, brush, outlineBrush);
                    break;
                case CrosshairStyle.Chevron:
                    DrawChevron(g, cx, cy, s, gap, t, ow, brush, outlineBrush);
                    break;
                case CrosshairStyle.Diamond:
                    DrawDiamond(g, cx, cy, s, t, ow, brush, outlineBrush);
                    break;
                case CrosshairStyle.Arrow:
                    DrawArrow(g, cx, cy, s, t, ow, brush, outlineBrush);
                    break;
                case CrosshairStyle.XShape:
                    DrawXShape(g, cx, cy, s, gap, t, ow, brush, outlineBrush);
                    break;
                case CrosshairStyle.TriangleDown:
                    DrawTriangleDown(g, cx, cy, s, t, ow, brush, outlineBrush);
                    break;
                case CrosshairStyle.Crosshairs:
                    DrawCrossLines(g, cx, cy, s, gap, t, ow, brush, outlineBrush, false);
                    DrawCircleStyle(g, cx, cy, s, t, ow, brush, outlineBrush);
                    DrawDotStyle(g, cx, cy, s * 0.4f, brush, outlineBrush);
                    break;
                case CrosshairStyle.SquareBrackets:
                    DrawSquareBrackets(g, cx, cy, s, t, ow, brush, outlineBrush);
                    break;
                case CrosshairStyle.Wings:
                    DrawWings(g, cx, cy, s, gap, t, ow, brush, outlineBrush);
                    break;
                case CrosshairStyle.CustomImage:
                    if (_customImageCache != null)
                    {
                        float imgScale = _size / (float)Math.Max(_customImageCache.Width, _customImageCache.Height) * 2f;
                        int dw = (int)(_customImageCache.Width * imgScale);
                        int dh = (int)(_customImageCache.Height * imgScale);
                        var destRect = new Rectangle(cx - dw / 2, cy - dh / 2, dw, dh);
                        float a = alpha / 255f;
                        var cm = new System.Drawing.Imaging.ColorMatrix(new float[][]
                        {
                            new float[] {1,0,0,0,0},
                            new float[] {0,1,0,0,0},
                            new float[] {0,0,1,0,0},
                            new float[] {0,0,0,a,0},
                            new float[] {0,0,0,0,1}
                        });
                        using var ia = new System.Drawing.Imaging.ImageAttributes();
                        ia.SetColorMatrix(cm);
                        g.DrawImage(_customImageCache, destRect, 0, 0,
                            _customImageCache.Width, _customImageCache.Height, GraphicsUnit.Pixel, ia);
                    }
                    break;
            }
        }

        #region Shape Drawing

        private void DrawCrossLines(Graphics g, int cx, int cy, float s, float gap, float t, float ow,
            Brush brush, SolidBrush? outlineBrush, bool tStyle)
        {
            var lines = new (float x, float y, float w, float h)[]
            {
                (cx - t / 2f, cy - s,     t, s - gap),
                (cx - t / 2f, cy + gap,    t, s - gap),
                (cx - s,      cy - t / 2f, s - gap, t),
                (cx + gap,    cy - t / 2f, s - gap, t),
            };
            int start = tStyle ? 1 : 0;
            for (int i = start; i < lines.Length; i++)
            {
                var (x, y, w, h) = lines[i];
                if (outlineBrush != null)
                    g.FillRectangle(outlineBrush, x - ow, y - ow, w + ow * 2, h + ow * 2);
                g.FillRectangle(brush, x, y, w, h);
            }
        }

        private void DrawCircleStyle(Graphics g, int cx, int cy, float s, float t, float ow,
            Brush brush, SolidBrush? outlineBrush)
        {
            float r = s * 0.7f;
            if (outlineBrush != null)
            {
                using var oPen = new Pen(outlineBrush, t + ow * 2);
                g.DrawEllipse(oPen, cx - r, cy - r, r * 2, r * 2);
            }
            using var pen = new Pen(brush, t);
            g.DrawEllipse(pen, cx - r, cy - r, r * 2, r * 2);
        }

        private void DrawDotStyle(Graphics g, int cx, int cy, float s, Brush brush, SolidBrush? outlineBrush)
        {
            float r = s * 0.3f + (_dotPulse ? MathF.Sin(_dotPulseSine) * 2f : 0f);
            r = Math.Max(1f, r);
            if (outlineBrush != null)
                g.FillEllipse(outlineBrush, cx - r - _outlineWidth, cy - r - _outlineWidth,
                    (r + _outlineWidth) * 2, (r + _outlineWidth) * 2);
            g.FillEllipse(brush, cx - r, cy - r, r * 2, r * 2);
        }

        private void DrawChevron(Graphics g, int cx, int cy, float s, float gap, float t, float ow,
            Brush brush, SolidBrush? outlineBrush)
        {
            var pts = new PointF[] { new(cx - s, cy + gap), new(cx, cy + gap + s * 0.6f), new(cx + s, cy + gap) };
            if (outlineBrush != null)
            {
                using var oPen = new Pen(outlineBrush, t + ow * 2) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLines(oPen, pts);
            }
            using var pen = new Pen(brush, t) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLines(pen, pts);
        }

        private void DrawDiamond(Graphics g, int cx, int cy, float s, float t, float ow,
            Brush brush, SolidBrush? outlineBrush)
        {
            float h = s * 0.6f;
            var pts = new PointF[] { new(cx, cy - h), new(cx + h, cy), new(cx, cy + h), new(cx - h, cy) };
            if (outlineBrush != null)
            {
                using var oPen = new Pen(outlineBrush, t + ow * 2) { LineJoin = LineJoin.Miter };
                g.DrawPolygon(oPen, pts);
            }
            using var pen = new Pen(brush, t) { LineJoin = LineJoin.Miter };
            g.DrawPolygon(pen, pts);
        }

        private void DrawArrow(Graphics g, int cx, int cy, float s, float t, float ow,
            Brush brush, SolidBrush? outlineBrush)
        {
            float half = s * 0.8f;
            var pts = new PointF[] { new(cx - half, cy - half * 0.4f), new(cx, cy + half * 0.4f), new(cx + half, cy - half * 0.4f) };
            if (outlineBrush != null)
            {
                using var oPen = new Pen(outlineBrush, t + ow * 2) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLines(oPen, pts);
            }
            using var pen = new Pen(brush, t) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLines(pen, pts);
        }

        private void DrawXShape(Graphics g, int cx, int cy, float s, float gap, float t, float ow,
            Brush brush, SolidBrush? outlineBrush)
        {
            float half = s;
            float gapD = gap * 0.7f;
            var lines = new (PointF a, PointF b)[]
            {
                (new(cx - half, cy - half), new(cx - gapD, cy - gapD)),
                (new(cx + half, cy - half), new(cx + gapD, cy - gapD)),
                (new(cx - half, cy + half), new(cx - gapD, cy + gapD)),
                (new(cx + half, cy + half), new(cx + gapD, cy + gapD)),
            };
            foreach (var (a, b) in lines)
            {
                if (outlineBrush != null)
                {
                    using var op = new Pen(outlineBrush, t + ow * 2) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                    g.DrawLine(op, a, b);
                }
                using var p = new Pen(brush, t) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLine(p, a, b);
            }
        }

        private void DrawTriangleDown(Graphics g, int cx, int cy, float s, float t, float ow,
            Brush brush, SolidBrush? outlineBrush)
        {
            float half = s * 0.9f;
            var pts = new PointF[] { new(cx - half, cy - half * 0.5f), new(cx, cy + half * 0.7f), new(cx + half, cy - half * 0.5f), new(cx - half, cy - half * 0.5f) };
            if (outlineBrush != null)
            {
                using var op = new Pen(outlineBrush, t + ow * 2) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLines(op, pts);
            }
            using var pen = new Pen(brush, t) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLines(pen, pts);
        }

        private void DrawSquareBrackets(Graphics g, int cx, int cy, float s, float t, float ow,
            Brush brush, SolidBrush? outlineBrush)
        {
            float half = s;
            float tick = half * 0.4f;
            var brackets = new PointF[][]
            {
                new[] { new PointF(cx - half + tick, cy - half), new PointF(cx - half, cy - half), new PointF(cx - half, cy + half), new PointF(cx - half + tick, cy + half) },
                new[] { new PointF(cx + half - tick, cy - half), new PointF(cx + half, cy - half), new PointF(cx + half, cy + half), new PointF(cx + half - tick, cy + half) },
            };
            foreach (var pts2 in brackets)
            {
                if (outlineBrush != null)
                {
                    using var op = new Pen(outlineBrush, t + ow * 2) { LineJoin = LineJoin.Miter, StartCap = LineCap.Round, EndCap = LineCap.Round };
                    g.DrawLines(op, pts2);
                }
                using var p = new Pen(brush, t) { LineJoin = LineJoin.Miter, StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLines(p, pts2);
            }
        }

        private void DrawWings(Graphics g, int cx, int cy, float s, float gap, float t, float ow,
            Brush brush, SolidBrush? outlineBrush)
        {
            float half = s;
            float gapH = gap * 0.5f;
            var leftWing = new PointF[] { new(cx - half, cy - half * 0.6f), new(cx - gapH, cy), new(cx - half, cy + half * 0.6f) };
            var rightWing = new PointF[] { new(cx + half, cy - half * 0.6f), new(cx + gapH, cy), new(cx + half, cy + half * 0.6f) };
            foreach (var pts2 in new[] { leftWing, rightWing })
            {
                if (outlineBrush != null)
                {
                    using var op = new Pen(outlineBrush, t + ow * 2) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
                    g.DrawLines(op, pts2);
                }
                using var p = new Pen(brush, t) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLines(p, pts2);
            }
        }

        #endregion

        #endregion

        #region Tray Icon

        private void SetupTrayIcon()
        {
            _trayMenu = new ContextMenuStrip();
            _clickerStatusItem = new ToolStripMenuItem(Lang.ClickerOff) { Enabled = false };

            _trayMenu.Items.Add(_clickerStatusItem);
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add(Lang.TraySettings, null, (s, e) => OpenSettings());
            _trayMenu.Items.Add(Lang.TrayReset, null, (s, e) => ResetToDefaults());
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add(Lang.TrayExit, null, (s, e) => { SaveSettings(); Application.Exit(); });

            _trayIcon = new NotifyIcon
            {
                Text = "Crosshair + AutoClicker",
                Icon = CreateTrayIcon(),
                ContextMenuStrip = _trayMenu,
                Visible = true
            };
            _trayIcon.DoubleClick += (s, e) => OpenSettings();
        }

        internal Icon CreateTrayIcon()
        {
            using var bmp = new Bitmap(16, 16, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var pen = new Pen(_crossColor, 2);
            g.DrawLine(pen, 8, 2, 8, 14);
            g.DrawLine(pen, 2, 8, 14, 8);
            IntPtr h = bmp.GetHicon();
            // Clone into a managed Icon so we can destroy the native handle immediately (avoids GDI leak)
            using var tmp = Icon.FromHandle(h);
            try { return (Icon)tmp.Clone(); }
            finally { DestroyIcon(h); }
        }

        internal void OpenSettings()
        {
            if (_settingsForm != null && !_settingsForm.IsDisposed)
            {
                // Toggle: if visible, slide out; otherwise slide in
                if (_settingsForm.Visible)
                    _settingsForm.SlideOut();
                else
                    _settingsForm.SlideIn();
                return;
            }
            _settingsForm = new SettingsForm(this);
            _settingsForm.Show();
            _settingsForm.SlideIn();
        }

        internal void RefreshTray()
        {
            if (_trayMenu == null) return;
            _trayMenu.Items.Clear();
            _clickerStatusItem = new ToolStripMenuItem(_autoClickerEnabled ? Lang.ClickerActive : Lang.ClickerOff) { Enabled = false };
            _trayMenu.Items.Add(_clickerStatusItem);
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add(Lang.TraySettings, null, (s, e) => OpenSettings());
            _trayMenu.Items.Add(Lang.TrayReset, null, (s, e) => ResetToDefaults());
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add(Lang.TrayExit, null, (s, e) => { SaveSettings(); Application.Exit(); });
        }

        internal async void CheckForUpdateAsync(Form? owner)
        {
            try
            {
                using var http = new HttpClient();
                http.DefaultRequestHeaders.UserAgent.ParseAdd("CrosshairOverlay/" + APP_VERSION);
                var url = $"https://api.github.com/repos/{GITHUB_REPO}/releases/latest";
                var json = await http.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var tag = root.GetProperty("tag_name").GetString()?.TrimStart('v', 'V') ?? "";
                if (string.Compare(tag, APP_VERSION, StringComparison.OrdinalIgnoreCase) <= 0)
                {
                    MessageBox.Show(owner, Lang.IsRussian ? $"У вас актуальная версия ({APP_VERSION})." 
                        : $"You are up to date ({APP_VERSION}).", "Crosshair Overlay", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                var assets = root.GetProperty("assets");
                string? downloadUrl = null;
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString() ?? "";
                    if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = asset.GetProperty("browser_download_url").GetString();
                        break;
                    }
                }
                if (downloadUrl == null)
                {
                    MessageBox.Show(owner, Lang.IsRussian ? "Не найден .exe в релизе." : "No .exe asset found in release.",
                        "Update", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                var msg = Lang.IsRussian
                    ? $"Доступна новая версия {tag}!\nТекущая: {APP_VERSION}\n\nОбновить сейчас?"
                    : $"New version {tag} available!\nCurrent: {APP_VERSION}\n\nUpdate now?";
                if (MessageBox.Show(owner, msg, "Update", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;

                var exePath = Application.ExecutablePath;
                var tempPath = exePath + ".update";
                var bytes = await http.GetByteArrayAsync(downloadUrl);
                File.WriteAllBytes(tempPath, bytes);
                // Replace on next launch via a short batch script
                var batPath = Path.Combine(Path.GetTempPath(), "crosshair_update.bat");
                File.WriteAllText(batPath,
                    $"@echo off\r\n" +
                    $"timeout /t 2 /nobreak >nul\r\n" +
                    $"move /Y \"{tempPath}\" \"{exePath}\"\r\n" +
                    $"start \"\" \"{exePath}\"\r\n" +
                    $"del \"%~f0\"\r\n");
                Process.Start(new ProcessStartInfo(batPath) { CreateNoWindow = true, UseShellExecute = false });
                Application.Exit();
            }
            catch (Exception ex)
            {
                MessageBox.Show(owner, (Lang.IsRussian ? "Ошибка обновления: " : "Update error: ") + ex.Message,
                    "Update", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        internal void RequestRender() { _needsStaticRender = true; }

        internal void ToggleRecording()
        {
            if (_recorder.IsRecording)
            {
                var path = _recorder.StopRecord();
                if (path != null) ShowRecordNotification(path);
                _needsStaticRender = true;
                if (_settingsForm != null && !_settingsForm.IsDisposed)
                    _settingsForm.Invalidate();
            }
            else
            {
                if (!_recorder.HasFFmpeg())
                {
                    _trayIcon.ShowBalloonTip(3000, "Crosshair Overlay",
                        Lang.FfmpegNotFound,
                        ToolTipIcon.Warning);
                    return;
                }
                _recorder.StartRecord();
                _needsStaticRender = true;
                if (_settingsForm != null && !_settingsForm.IsDisposed)
                    _settingsForm.Invalidate();
            }
        }

        internal void SaveReplayClip()
        {
            if (!_recorder.IsReplayActive) return;
            var path = _recorder.SaveReplay();
            if (path != null) ShowRecordNotification(path);
        }

        internal void ShowRecordNotification(string filePath)
        {
            if (InvokeRequired) { BeginInvoke(new Action(() => ShowRecordNotification(filePath))); return; }
            var notif = new RecordNotification(filePath, _notifPosition);
            notif.Show();
        }

        internal void OpenGallery()
        {
            if (_galleryForm != null && !_galleryForm.IsDisposed)
            {
                if (_galleryForm.Visible) _galleryForm.SlideOut();
                else _galleryForm.SlideIn();
                return;
            }
            _galleryForm = new VideoGalleryForm(_recorder.OutputDir, _recorder);
            _galleryForm.Show();
            _galleryForm.SlideIn();
        }

        #endregion

        #region Settings Persistence

        internal void SaveSettings()
        {
            try
            {
                var dir = Path.GetDirectoryName(_settingsPath)!;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var data = new SettingsData
                {
                    Style = (int)_style, Size = _size, Thickness = _thickness, Gap = _gap,
                    Opacity = _opacity, OffsetX = _offsetX, OffsetY = _offsetY,
                    ColorR = _crossColor.R, ColorG = _crossColor.G, ColorB = _crossColor.B,
                    Color2R = _crossColor2.R, Color2G = _crossColor2.G, Color2B = _crossColor2.B,
                    UseGradient = _useGradient, RainbowMode = _rainbowMode,
                    ShowDot = _showDot, DotSize = _dotSize, DotPulse = _dotPulse,
                    ShowOutline = _showOutline,
                    OutColorR = _outlineColor.R, OutColorG = _outlineColor.G, OutColorB = _outlineColor.B,
                    OutlineWidth = _outlineWidth,
                    ShowShadow = _showShadow, ShadowOX = _shadowOffsetX, ShadowOY = _shadowOffsetY,
                    Rotation = _rotation, Spin = _spin, SpinSpeed = _spinSpeed, AntiAlias = _antiAlias,
                    CustomImagePath = _customImagePath ?? "",
                    AutoClickerEnabled = _autoClickerEnabled,
                    ClicksPerSecond = _clicksPerSecond,
                    ClickOnHold = _clickOnHold,
                    RightClickMode = _rightClickMode,
                    RandomDelay = _randomDelay,
                    RandomDelayPercent = _randomDelayPercent,
                    BurstMode = _burstMode,
                    BurstCount = _burstCount,

                    DynamicCrosshair = _dynamicCrosshair,
                    DynamicMaxSpread = _dynamicMaxSpread,
                    DynamicRecovery = _dynamicRecovery,
                    GlowEnabled = _glowEnabled,
                    GlowSize = _glowSize,
                    GlowAlpha = _glowAlpha,
                    HitMarkerEnabled = _hitMarkerEnabled,
                    HitMarkerSize = _hitMarkerSize,
                    // Recording
                    FfmpegPath = _recorder.FfmpegPath ?? "",
                    RecOutputDir = _recorder.OutputDir ?? "",
                    RecFps = _recorder.Fps,
                    RecCrf = _recorder.Crf,
                    RecPreset = _recorder.Preset ?? "fast",
                    RecAudioDev = _recorder.AudioDev ?? "",
                    RecMicDev = _recorder.MicDev ?? "",
                    RecUseMic = _recorder.UseMic,
                    RecReplaySec = _recorder.ReplaySec,
                    NotifPosition = _notifPosition,
                    AudioApps = _audioApps,
                    // Hotkeys
                    HkMods = _hkMods.Skip(1).Select(v => (int)v).ToArray(),
                    HkKeys = _hkKeys.Skip(1).Select(v => (int)v).ToArray(),
                    LanguageRu = Lang.IsRussian
                };
                File.WriteAllText(_settingsPath, JsonSerializer.Serialize(data));
            }
            catch { }
        }

        private void LoadSettings()
        {
            try
            {
                if (!File.Exists(_settingsPath)) return;
                var data = JsonSerializer.Deserialize<SettingsData>(File.ReadAllText(_settingsPath));
                if (data == null) return;

                int _styleMax = Enum.GetValues(typeof(CrosshairStyle)).Length - 1;
                _style = (CrosshairStyle)Math.Clamp(data.Style, 0, _styleMax);
                _size = Math.Clamp(data.Size, 4, 100);
                _thickness = Math.Clamp(data.Thickness, 1, 10);
                _gap = Math.Clamp(data.Gap, 0, 30);
                _opacity = Math.Clamp(data.Opacity, 10, 255);
                _offsetX = Math.Clamp(data.OffsetX, -500, 500);
                _offsetY = Math.Clamp(data.OffsetY, -500, 500);
                _crossColor = Color.FromArgb(data.ColorR, data.ColorG, data.ColorB);
                _crossColor2 = Color.FromArgb(data.Color2R, data.Color2G, data.Color2B);
                _useGradient = data.UseGradient;
                _rainbowMode = data.RainbowMode;
                _showDot = data.ShowDot;
                _dotSize = Math.Clamp(data.DotSize, 1, 10);
                _dotPulse = data.DotPulse;
                _showOutline = data.ShowOutline;
                _outlineColor = Color.FromArgb(data.OutColorR, data.OutColorG, data.OutColorB);
                _outlineWidth = Math.Clamp(data.OutlineWidth, 0.5f, 5f);
                _showShadow = data.ShowShadow;
                _shadowOffsetX = data.ShadowOX; _shadowOffsetY = data.ShadowOY;
                _rotation = data.Rotation;
                _spin = data.Spin; _spinSpeed = data.SpinSpeed;
                _antiAlias = data.AntiAlias;
                _customImagePath = data.CustomImagePath ?? "";
                if (!string.IsNullOrEmpty(_customImagePath) && File.Exists(_customImagePath))
                    _customImageCache = new Bitmap(_customImagePath);
                _autoClickerEnabled = data.AutoClickerEnabled;
                _clicksPerSecond = Math.Clamp(data.ClicksPerSecond, 5, 1000);
                _clickOnHold = data.ClickOnHold;
                _rightClickMode = data.RightClickMode;
                _randomDelay = data.RandomDelay;
                _randomDelayPercent = Math.Clamp(data.RandomDelayPercent, 5, 50);
                _burstMode = data.BurstMode;
                _burstCount = Math.Clamp(data.BurstCount, 1, 50);

                _dynamicCrosshair = data.DynamicCrosshair;
                _dynamicMaxSpread = Math.Clamp(data.DynamicMaxSpread, 1f, 30f);
                _dynamicRecovery = Math.Clamp(data.DynamicRecovery, 0.05f, 0.5f);
                _glowEnabled = data.GlowEnabled;
                _glowSize = Math.Clamp(data.GlowSize, 2, 20);
                _glowAlpha = Math.Clamp(data.GlowAlpha, 20, 150);
                _hitMarkerEnabled = data.HitMarkerEnabled;
                _hitMarkerSize = Math.Clamp(data.HitMarkerSize, 4f, 30f);
                // Recording
                if (!string.IsNullOrEmpty(data.FfmpegPath)) _recorder.FfmpegPath = data.FfmpegPath;
                if (!string.IsNullOrEmpty(data.RecOutputDir)) _recorder.OutputDir = data.RecOutputDir;
                _recorder.Fps = Math.Clamp(data.RecFps, 15, 60);
                _recorder.Crf = Math.Clamp(data.RecCrf, 0, 51);
                if (!string.IsNullOrEmpty(data.RecPreset)) _recorder.Preset = data.RecPreset;
                if (!string.IsNullOrEmpty(data.RecAudioDev)) _recorder.AudioDev = data.RecAudioDev;
                if (!string.IsNullOrEmpty(data.RecMicDev)) _recorder.MicDev = data.RecMicDev;
                _recorder.UseMic = data.RecUseMic;
                _recorder.ReplaySec = Math.Clamp(data.RecReplaySec, 30, 600);
                _notifPosition = Math.Clamp(data.NotifPosition, 0, 3);
                _audioApps = data.AudioApps ?? new();
                // Hotkeys
                if (data.HkMods != null && data.HkKeys != null)
                {
                    // Copy as many saved hotkeys as we can; keep defaults for any new hotkeys
                    int n = Math.Min(HOTKEY_COUNT, Math.Min(data.HkMods.Length, data.HkKeys.Length));
                    for (int i = 0; i < n; i++)
                    {
                        _hkMods[i + 1] = (uint)data.HkMods[i];
                        _hkKeys[i + 1] = (uint)data.HkKeys[i];
                    }
                }
                Lang.IsRussian = data.LanguageRu;
            }
            catch { }
        }

        private class SettingsData
        {
            public int Style { get; set; }
            public int Size { get; set; } = 20;
            public int Thickness { get; set; } = 2;
            public int Gap { get; set; } = 4;
            public int Opacity { get; set; } = 255;
            public int OffsetX { get; set; }
            public int OffsetY { get; set; }
            public int ColorR { get; set; }
            public int ColorG { get; set; } = 255;
            public int ColorB { get; set; }
            public int Color2R { get; set; }
            public int Color2G { get; set; } = 200;
            public int Color2B { get; set; } = 255;
            public bool UseGradient { get; set; }
            public bool RainbowMode { get; set; }
            public bool ShowDot { get; set; } = true;
            public int DotSize { get; set; } = 2;
            public bool DotPulse { get; set; }
            public bool ShowOutline { get; set; } = true;
            public int OutColorR { get; set; }
            public int OutColorG { get; set; }
            public int OutColorB { get; set; }
            public float OutlineWidth { get; set; } = 1f;
            public bool ShowShadow { get; set; }
            public int ShadowOX { get; set; } = 2;
            public int ShadowOY { get; set; } = 2;
            public float Rotation { get; set; }
            public bool Spin { get; set; }
            public float SpinSpeed { get; set; } = 2f;
            public bool AntiAlias { get; set; } = true;
            public string CustomImagePath { get; set; } = "";
            public bool AutoClickerEnabled { get; set; } = false;
            public int ClicksPerSecond { get; set; } = 30;
            public bool ClickOnHold { get; set; } = true;
            public bool RightClickMode { get; set; }
            public bool BurstMode { get; set; }
            public int BurstCount { get; set; } = 3;
            public bool RandomDelay { get; set; }
            public int RandomDelayPercent { get; set; } = 20;
            public bool UseSendInput { get; set; } = true;
            public bool UseMultithreading { get; set; } = true;
            public bool UseHighPrecision { get; set; } = true;
            public bool DynamicCrosshair { get; set; }
            public float DynamicMaxSpread { get; set; } = 8f;
            public float DynamicRecovery { get; set; } = 0.15f;
            public bool GlowEnabled { get; set; }
            public int GlowSize { get; set; } = 6;
            public int GlowAlpha { get; set; } = 80;
            public bool HitMarkerEnabled { get; set; }
            public float HitMarkerSize { get; set; } = 12f;
            // Recording
            public string FfmpegPath { get; set; } = "ffmpeg";
            public string RecOutputDir { get; set; } = "";
            public int RecFps { get; set; } = 30;
            public int RecCrf { get; set; } = 23;
            public string RecPreset { get; set; } = "fast";
            public string RecAudioDev { get; set; } = "";
            public string RecMicDev { get; set; } = "";
            public bool RecUseMic { get; set; }
            public int RecReplaySec { get; set; } = 120;
            public int NotifPosition { get; set; } = 1;
            public List<string> AudioApps { get; set; } = new();
            // Hotkeys
            public int[]? HkMods { get; set; }
            public int[]? HkKeys { get; set; }
            public bool LanguageRu { get; set; } = true;
        }

        #endregion

        internal static Color HslToColor(double h, double s, double l)
        {
            double c = (1.0 - Math.Abs(2.0 * l - 1.0)) * s;
            double x = c * (1.0 - Math.Abs((h / 60.0) % 2.0 - 1.0));
            double m = l - c / 2.0;
            double r = 0, g2 = 0, b = 0;
            if (h < 60) { r = c; g2 = x; } else if (h < 120) { r = x; g2 = c; }
            else if (h < 180) { g2 = c; b = x; } else if (h < 240) { g2 = x; b = c; }
            else if (h < 300) { r = x; b = c; } else { r = c; b = x; }
            return Color.FromArgb(
                Math.Clamp((int)((r + m) * 255), 0, 255),
                Math.Clamp((int)((g2 + m) * 255), 0, 255),
                Math.Clamp((int)((b + m) * 255), 0, 255));
        }
    }
}
