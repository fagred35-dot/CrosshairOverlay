using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CrosshairOverlay
{
    // =============================================================
    //  Feature #86 — Autostart with Windows (Registry Run key)
    // =============================================================
    internal static class AutostartManager
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "CrosshairOverlay";

        internal static bool IsEnabled
        {
            get
            {
                try
                {
                    using var k = Registry.CurrentUser.OpenSubKey(RunKey);
                    var v = k?.GetValue(ValueName) as string;
                    return !string.IsNullOrEmpty(v);
                }
                catch { return false; }
            }
        }

        internal static void Set(bool enable)
        {
            try
            {
                using var k = Registry.CurrentUser.CreateSubKey(RunKey);
                if (k == null) return;
                if (enable)
                {
                    string exe = Environment.ProcessPath ?? "";
                    if (string.IsNullOrEmpty(exe)) return;
                    k.SetValue(ValueName, "\"" + exe + "\"");
                }
                else
                {
                    if (k.GetValue(ValueName) != null) k.DeleteValue(ValueName, false);
                }
            }
            catch { }
        }
    }

    // =============================================================
    //  Feature #95 — Portable mode (settings next to exe if portable.flag exists)
    // =============================================================
    internal static class PortableMode
    {
        internal static bool IsPortable
        {
            get
            {
                try
                {
                    string? dir = Path.GetDirectoryName(Environment.ProcessPath ?? "");
                    if (string.IsNullOrEmpty(dir)) return false;
                    return File.Exists(Path.Combine(dir, "portable.flag"));
                }
                catch { return false; }
            }
        }

        internal static string SettingsPath
        {
            get
            {
                if (IsPortable)
                {
                    string dir = Path.GetDirectoryName(Environment.ProcessPath!) ?? ".";
                    return Path.Combine(dir, "settings_v3.json");
                }
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "CrosshairOverlay", "settings_v3.json");
            }
        }

        internal static void Enable(bool enable)
        {
            try
            {
                string dir = Path.GetDirectoryName(Environment.ProcessPath ?? "") ?? ".";
                string flag = Path.Combine(dir, "portable.flag");
                if (enable && !File.Exists(flag))
                    File.WriteAllText(flag, "Portable settings will be stored in this folder.");
                else if (!enable && File.Exists(flag))
                    File.Delete(flag);
            }
            catch { }
        }
    }

    // =============================================================
    //  Features #26, #27 — Profile Import/Export + Share Codes
    // =============================================================
    internal static class ProfileIO
    {
        // Full profile export/import (entire settings_v3.json)
        internal static bool ExportToFile(string sourcePath, string destPath)
        {
            try { File.Copy(sourcePath, destPath, true); return true; }
            catch { return false; }
        }

        internal static bool ImportFromFile(string srcPath, string destPath)
        {
            try
            {
                if (!File.Exists(srcPath)) return false;
                File.Copy(srcPath, destPath, true);
                return true;
            }
            catch { return false; }
        }

        // Single-crosshair share code (base64-encoded JSON, compact)
        internal static string ExportCrosshairCode(OverlayForm form)
        {
            Color c1 = form._crossColor;
            Color c2 = form._crossColor2;
            var o = new
            {
                v = 1,
                s = (int)form._style,
                sz = form._size,
                th = form._thickness,
                g = form._gap,
                op = form._opacity,
                c = (c1.R << 16) | (c1.G << 8) | c1.B,
                c2 = (c2.R << 16) | (c2.G << 8) | c2.B,
                gr = form._useGradient ? 1 : 0,
                rb = form._rainbowMode ? 1 : 0,
                d = form._showDot ? 1 : 0,
                ds = form._dotSize,
                ol = form._showOutline ? 1 : 0,
                r = form._rotation,
                sp = form._spin ? 1 : 0,
                gl = form._glowEnabled ? 1 : 0
            };
            string json = JsonSerializer.Serialize(o);
            return "CX1:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        }

        internal static bool ImportCrosshairCode(string code, OverlayForm form)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(code)) return false;
                code = code.Trim();
                if (code.StartsWith("CX1:")) code = code.Substring(4);
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(code));
                using var doc = JsonDocument.Parse(json);
                var e = doc.RootElement;
                int styleMax = Enum.GetValues(typeof(OverlayForm.CrosshairStyle)).Length - 1;
                if (e.TryGetProperty("s", out var ps)) form._style = (OverlayForm.CrosshairStyle)Math.Clamp(ps.GetInt32(), 0, styleMax);
                if (e.TryGetProperty("sz", out var psz)) form._size = Math.Clamp(psz.GetInt32(), 4, 100);
                if (e.TryGetProperty("th", out var pt)) form._thickness = Math.Clamp(pt.GetInt32(), 1, 10);
                if (e.TryGetProperty("g", out var pg)) form._gap = Math.Clamp(pg.GetInt32(), 0, 30);
                if (e.TryGetProperty("op", out var po)) form._opacity = Math.Clamp(po.GetInt32(), 10, 255);
                if (e.TryGetProperty("c", out var pc)) form._crossColor = UnpackColor(pc.GetInt32());
                if (e.TryGetProperty("c2", out var pc2)) form._crossColor2 = UnpackColor(pc2.GetInt32());
                if (e.TryGetProperty("gr", out var pgr)) form._useGradient = pgr.GetInt32() != 0;
                if (e.TryGetProperty("rb", out var prb)) form._rainbowMode = prb.GetInt32() != 0;
                if (e.TryGetProperty("d", out var pd)) form._showDot = pd.GetInt32() != 0;
                if (e.TryGetProperty("ds", out var pds)) form._dotSize = Math.Clamp(pds.GetInt32(), 1, 10);
                if (e.TryGetProperty("ol", out var pol)) form._showOutline = pol.GetInt32() != 0;
                if (e.TryGetProperty("r", out var pr)) form._rotation = pr.GetSingle();
                if (e.TryGetProperty("sp", out var psp)) form._spin = psp.GetInt32() != 0;
                if (e.TryGetProperty("gl", out var pgl)) form._glowEnabled = pgl.GetInt32() != 0;
                return true;
            }
            catch { return false; }
        }

        private static Color UnpackColor(int packed)
            => Color.FromArgb((packed >> 16) & 0xFF, (packed >> 8) & 0xFF, packed & 0xFF);
    }

    // =============================================================
    //  Feature #98 — Usage time tracker (saved per day)
    // =============================================================
    internal static class UsageTracker
    {
        private static readonly string _path = Path.Combine(
            Path.GetDirectoryName(PortableMode.SettingsPath) ?? ".",
            "usage.json");

        internal class UsageData
        {
            public string Date { get; set; } = "";
            public long SecondsToday { get; set; }
            public long SecondsTotal { get; set; }
            public int StreakDays { get; set; }
            public string LastStreakDate { get; set; } = "";
            public long TotalClicks { get; set; }
            public int MaxCps { get; set; }
        }

        private static UsageData _cache = Load();
        private static DateTime _sessionStart = DateTime.Now;
        private static readonly object _lock = new();

        internal static UsageData Current => _cache;

        private static UsageData Load()
        {
            try
            {
                if (File.Exists(_path))
                {
                    var d = JsonSerializer.Deserialize<UsageData>(File.ReadAllText(_path));
                    if (d != null) return d;
                }
            }
            catch { }
            return new UsageData { Date = DateTime.Today.ToString("yyyy-MM-dd") };
        }

        internal static void Tick(int maxCps = 0, long totalClicks = 0)
        {
            lock (_lock)
            {
                string today = DateTime.Today.ToString("yyyy-MM-dd");
                if (_cache.Date != today)
                {
                    // Roll over to new day; count streak if consecutive
                    try
                    {
                        if (!string.IsNullOrEmpty(_cache.Date) &&
                            DateTime.TryParse(_cache.Date, out var prev) &&
                            (DateTime.Today - prev).TotalDays <= 1.5)
                            _cache.StreakDays = Math.Max(1, _cache.StreakDays + 1);
                        else
                            _cache.StreakDays = 1;
                    }
                    catch { _cache.StreakDays = 1; }
                    _cache.LastStreakDate = today;
                    _cache.Date = today;
                    _cache.SecondsToday = 0;
                }
                var now = DateTime.Now;
                long delta = (long)(now - _sessionStart).TotalSeconds;
                if (delta > 0 && delta < 60) // only count if small delta (not huge jumps)
                {
                    _cache.SecondsToday += delta;
                    _cache.SecondsTotal += delta;
                }
                _sessionStart = now;
                if (maxCps > _cache.MaxCps) _cache.MaxCps = maxCps;
                if (totalClicks > _cache.TotalClicks) _cache.TotalClicks = totalClicks;
            }
        }

        internal static void Save()
        {
            try
            {
                Tick();
                string? dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(_path, JsonSerializer.Serialize(_cache));
            }
            catch { }
        }

        internal static string FormatDuration(long seconds)
        {
            var ts = TimeSpan.FromSeconds(seconds);
            if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}ч {ts.Minutes}м";
            if (ts.TotalMinutes >= 1) return $"{ts.Minutes}м {ts.Seconds}с";
            return $"{ts.Seconds}с";
        }
    }

    // =============================================================
    //  Feature #81 — Hex color input dialog (lightweight)
    // =============================================================
    internal class HexColorForm : Form
    {
        internal Color ResultColor { get; private set; }
        private readonly TextBox _hexBox;
        private readonly Panel _preview;
        private readonly Label _status;

        public HexColorForm(Color initial)
        {
            ResultColor = initial;
            Text = "HEX цвет";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            Width = 260;
            Height = 160;
            BackColor = Color.FromArgb(28, 18, 48);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9f);

            var lbl = new Label { Text = "HEX (#RRGGBB или RRGGBB):", Location = new Point(12, 14), AutoSize = true };
            _hexBox = new TextBox
            {
                Text = $"#{initial.R:X2}{initial.G:X2}{initial.B:X2}",
                Location = new Point(12, 40),
                Width = 140,
                BackColor = Color.FromArgb(50, 30, 80),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            _preview = new Panel
            {
                Location = new Point(160, 38),
                Size = new Size(72, 24),
                BackColor = initial,
                BorderStyle = BorderStyle.FixedSingle
            };
            _status = new Label { Location = new Point(12, 70), AutoSize = true, ForeColor = Color.FromArgb(200, 200, 200) };
            var ok = new Button
            {
                Text = "OK",
                Location = new Point(80, 92),
                DialogResult = DialogResult.OK,
                BackColor = Color.FromArgb(130, 80, 220),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Width = 70
            };
            var cancel = new Button
            {
                Text = "Отмена",
                Location = new Point(160, 92),
                DialogResult = DialogResult.Cancel,
                BackColor = Color.FromArgb(60, 40, 90),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Width = 72
            };
            _hexBox.TextChanged += (s, e) => UpdatePreview();
            AcceptButton = ok;
            CancelButton = cancel;
            Controls.AddRange(new Control[] { lbl, _hexBox, _preview, _status, ok, cancel });
            _hexBox.Select(0, _hexBox.Text.Length);
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            if (TryParseHex(_hexBox.Text, out var c))
            {
                ResultColor = c;
                _preview.BackColor = c;
                _status.Text = "Ок";
                _status.ForeColor = Color.LightGreen;
            }
            else
            {
                _status.Text = "Неверный формат";
                _status.ForeColor = Color.OrangeRed;
            }
        }

        internal static bool TryParseHex(string s, out Color c)
        {
            c = Color.Black;
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim().TrimStart('#');
            if (s.Length == 3) s = $"{s[0]}{s[0]}{s[1]}{s[1]}{s[2]}{s[2]}";
            if (s.Length != 6) return false;
            try
            {
                int r = Convert.ToInt32(s.Substring(0, 2), 16);
                int g = Convert.ToInt32(s.Substring(2, 2), 16);
                int b = Convert.ToInt32(s.Substring(4, 2), 16);
                c = Color.FromArgb(r, g, b);
                return true;
            }
            catch { return false; }
        }
    }

    // =============================================================
    //  Feature #73 — UI theme presets (Light / Dark / OLED / Neon)
    // =============================================================
    internal static class UiThemePresets
    {
        internal enum Preset { Dark, Light, OledBlack, Neon }

        internal static Preset Current = Preset.Dark;

        // Returns (BgColor, TextMain, TextDim, CardBg, Accent, AccentGlow)
        internal static (Color bg, Color text, Color textDim, Color card, Color accent, Color glow) Colors(Preset p)
            => p switch
            {
                Preset.Light => (
                    Color.FromArgb(240, 240, 245, 250),
                    Color.FromArgb(30, 30, 40),
                    Color.FromArgb(100, 100, 120),
                    Color.FromArgb(40, 150, 130, 200),
                    Color.FromArgb(110, 70, 200),
                    Color.FromArgb(150, 120, 240)
                ),
                Preset.OledBlack => (
                    Color.FromArgb(255, 0, 0, 0),
                    Color.FromArgb(230, 230, 230),
                    Color.FromArgb(120, 120, 120),
                    Color.FromArgb(255, 10, 10, 12),
                    Color.FromArgb(0, 200, 120),
                    Color.FromArgb(80, 255, 160)
                ),
                Preset.Neon => (
                    Color.FromArgb(230, 5, 2, 25),
                    Color.FromArgb(255, 100, 255),
                    Color.FromArgb(180, 80, 220),
                    Color.FromArgb(80, 255, 0, 180),
                    Color.FromArgb(255, 30, 180),
                    Color.FromArgb(255, 120, 220)
                ),
                _ => ( // Dark (default)
                    Color.FromArgb(220, 12, 6, 24),
                    Color.FromArgb(235, 228, 245),
                    Color.FromArgb(130, 120, 155),
                    Color.FromArgb(40, 50, 30, 90),
                    Color.FromArgb(130, 80, 220),
                    Color.FromArgb(175, 130, 255)
                )
            };
    }

    // =============================================================
    //  Feature #64 — Open screenshots folder
    // =============================================================
    internal static class ShellHelper
    {
        internal static void OpenFolder(string path)
        {
            try
            {
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                System.Diagnostics.Process.Start("explorer.exe", $"\"{path}\"");
            }
            catch { }
        }
    }

    // =============================================================
    //  Feature #55 — Hotkey conflict detector
    // =============================================================
    internal static class HotkeyConflictDetector
    {
        // Returns list of (index1, index2) pairs that conflict.
        internal static System.Collections.Generic.List<(int a, int b)> FindConflicts(uint[] mods, uint[] keys)
        {
            var result = new System.Collections.Generic.List<(int, int)>();
            for (int i = 1; i < mods.Length; i++)
            {
                if (keys[i] == 0) continue;
                for (int j = i + 1; j < mods.Length; j++)
                {
                    if (keys[j] == 0) continue;
                    if (mods[i] == mods[j] && keys[i] == keys[j])
                        result.Add((i, j));
                }
            }
            return result;
        }
    }

    // =============================================================
    //  Feature #57 — Detect fullscreen foreground app
    // =============================================================
    internal static class FullscreenDetector
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct RECT { public int L, T, R, B; }

        internal static bool IsForegroundFullscreen()
        {
            try
            {
                var fg = GetForegroundWindow();
                if (fg == IntPtr.Zero) return false;
                if (fg == GetDesktopWindow() || fg == GetShellWindow()) return false;
                if (!GetWindowRect(fg, out var r)) return false;
                var b = Screen.FromHandle(fg).Bounds;
                return r.L <= b.X && r.T <= b.Y && r.R >= b.Right && r.B >= b.Bottom;
            }
            catch { return false; }
        }
    }
}
