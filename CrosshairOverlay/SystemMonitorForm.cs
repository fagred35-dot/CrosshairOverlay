using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace CrosshairOverlay
{
    /// <summary>
    /// Draggable always-on-top system monitor: CPU, RAM, GPU (where available), disk, network.
    /// Sampling happens on a dedicated background thread to avoid blocking UI.
    /// The form itself is a small rounded panel with live text; drag anywhere on it to move.
    /// </summary>
    public class SystemMonitorForm : Form
    {
        private readonly OverlayForm _overlay;
        private readonly System.Windows.Forms.Timer _uiTimer;
        private readonly Thread _samplerThread;
        private volatile bool _running = true;

        // Sampled values (written by sampler thread, read by UI thread)
        private volatile float _cpuPct;
        private volatile float _ramUsedMB;
        private volatile float _ramTotalMB;
        private volatile float _gpuPct;
        private volatile float _diskPct;
        private volatile float _netMBs;

        // Counters
        private PerformanceCounter? _cpuCounter;
        private PerformanceCounter? _diskCounter;
        private PerformanceCounter[]? _gpuCounters;
        private PerformanceCounter[]? _netCounters;

        // Dragging
        private bool _dragging;
        private Point _dragStart;
        private Point _formStart;

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        public SystemMonitorForm(OverlayForm overlay)
        {
            _overlay = overlay;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            DoubleBuffered = true;
            BackColor = Color.FromArgb(16, 18, 22);
            Opacity = 0.92;
            Size = new Size(220, 150);
            Location = new Point(Math.Max(0, overlay._sysMonX), Math.Max(0, overlay._sysMonY));

            // Init counters lazily on the sampler thread to avoid blocking UI on startup.
            _samplerThread = new Thread(SamplerLoop)
            {
                IsBackground = true,
                Priority = ThreadPriority.BelowNormal
            };
            _samplerThread.Start();

            _uiTimer = new System.Windows.Forms.Timer { Interval = 500 };
            _uiTimer.Tick += (_, _) => Invalidate();
            _uiTimer.Start();

            MouseDown += OnFormMouseDown;
            MouseMove += OnFormMouseMove;
            MouseUp += OnFormMouseUp;
            FormClosing += (_, _) => { _running = false; _uiTimer.Stop(); };
        }

        private void OnFormMouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            _dragging = true;
            _dragStart = Cursor.Position;
            _formStart = Location;
        }

        private void OnFormMouseMove(object? sender, MouseEventArgs e)
        {
            if (!_dragging) return;
            var cur = Cursor.Position;
            Location = new Point(_formStart.X + (cur.X - _dragStart.X),
                                 _formStart.Y + (cur.Y - _dragStart.Y));
        }

        private void OnFormMouseUp(object? sender, MouseEventArgs e)
        {
            if (!_dragging) return;
            _dragging = false;
            _overlay._sysMonX = Location.X;
            _overlay._sysMonY = Location.Y;
            _overlay.SaveSettings();
        }

        private void SamplerLoop()
        {
            // First-call init: PerformanceCounter needs ~1s warm-up before returning non-zero.
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
                _cpuCounter.NextValue();
            }
            catch { _cpuCounter = null; }

            try
            {
                _diskCounter = new PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total", true);
                _diskCounter.NextValue();
            }
            catch { _diskCounter = null; }

            try
            {
                var cat = new PerformanceCounterCategory("GPU Engine");
                var instances = cat.GetInstanceNames();
                var list = new System.Collections.Generic.List<PerformanceCounter>();
                foreach (var inst in instances)
                {
                    if (inst.Contains("engtype_3D"))
                    {
                        try
                        {
                            var pc = new PerformanceCounter("GPU Engine", "Utilization Percentage", inst, true);
                            pc.NextValue();
                            list.Add(pc);
                        }
                        catch { }
                    }
                }
                _gpuCounters = list.ToArray();
            }
            catch { _gpuCounters = null; }

            try
            {
                var cat = new PerformanceCounterCategory("Network Interface");
                var instances = cat.GetInstanceNames();
                var list = new System.Collections.Generic.List<PerformanceCounter>();
                foreach (var inst in instances)
                {
                    // Skip loopback / virtual adapters
                    if (inst.IndexOf("Loopback", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    if (inst.IndexOf("isatap",   StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    if (inst.IndexOf("Teredo",   StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    try
                    {
                        var pc = new PerformanceCounter("Network Interface", "Bytes Total/sec", inst, true);
                        pc.NextValue();
                        list.Add(pc);
                    }
                    catch { }
                }
                _netCounters = list.ToArray();
            }
            catch { _netCounters = null; }

            Thread.Sleep(500); // warm-up

            while (_running)
            {
                try
                {
                    if (_cpuCounter != null) _cpuPct = _cpuCounter.NextValue();

                    var mem = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
                    if (GlobalMemoryStatusEx(ref mem))
                    {
                        _ramTotalMB = mem.ullTotalPhys / 1024f / 1024f;
                        _ramUsedMB  = (mem.ullTotalPhys - mem.ullAvailPhys) / 1024f / 1024f;
                    }

                    if (_gpuCounters != null && _gpuCounters.Length > 0)
                    {
                        float sum = 0;
                        foreach (var c in _gpuCounters)
                        {
                            try { sum += c.NextValue(); } catch { }
                        }
                        _gpuPct = Math.Min(100f, sum);
                    }

                    if (_diskCounter != null)
                    {
                        try { _diskPct = Math.Min(100f, _diskCounter.NextValue()); } catch { }
                    }

                    if (_netCounters != null && _netCounters.Length > 0)
                    {
                        float sum = 0;
                        foreach (var c in _netCounters)
                        {
                            try { sum += c.NextValue(); } catch { }
                        }
                        _netMBs = sum / 1024f / 1024f;
                    }
                }
                catch { }

                Thread.Sleep(500);
            }

            try { _cpuCounter?.Dispose(); } catch { }
            try { _diskCounter?.Dispose(); } catch { }
            if (_gpuCounters != null) foreach (var c in _gpuCounters) { try { c.Dispose(); } catch { } }
            if (_netCounters != null) foreach (var c in _netCounters) { try { c.Dispose(); } catch { } }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // Rounded background
            using (var path = RoundedRect(new Rectangle(0, 0, Width, Height), 10))
            using (var bg = new SolidBrush(Color.FromArgb(240, 18, 20, 26)))
            using (var border = new Pen(Color.FromArgb(80, 90, 100, 120), 1))
            {
                g.FillPath(bg, path);
                g.DrawPath(border, path);
            }

            // Title
            using (var titleFont = new Font("Segoe UI Semibold", 9f))
            using (var titleBrush = new SolidBrush(Color.FromArgb(220, 230, 240, 255)))
            {
                g.DrawString(Lang.IsRussian ? "МОНИТОР СИСТЕМЫ" : "SYSTEM MONITOR",
                    titleFont, titleBrush, new PointF(10, 6));
            }

            // Rows
            using var rowFont = new Font("Consolas", 9.5f);
            using var labelBrush = new SolidBrush(Color.FromArgb(200, 180, 195, 220));
            using var valBrush   = new SolidBrush(Color.FromArgb(255, 240, 245, 255));

            int y = 28;
            DrawRow(g, rowFont, labelBrush, valBrush, y, "CPU",  $"{_cpuPct,5:F1} %",
                _cpuPct / 100f, ColorForPct(_cpuPct)); y += 20;

            float ramPct = _ramTotalMB > 0 ? _ramUsedMB / _ramTotalMB * 100f : 0f;
            DrawRow(g, rowFont, labelBrush, valBrush, y, "RAM",
                $"{_ramUsedMB/1024f,4:F1} / {_ramTotalMB/1024f,4:F1} GB",
                ramPct / 100f, ColorForPct(ramPct)); y += 20;

            DrawRow(g, rowFont, labelBrush, valBrush, y, "GPU",  $"{_gpuPct,5:F1} %",
                _gpuPct / 100f, ColorForPct(_gpuPct)); y += 20;

            DrawRow(g, rowFont, labelBrush, valBrush, y, "DISK", $"{_diskPct,5:F1} %",
                _diskPct / 100f, ColorForPct(_diskPct)); y += 20;

            string netTxt = _netMBs >= 1f
                ? $"{_netMBs,5:F2} MB/s"
                : $"{_netMBs * 1024f,5:F0} KB/s";
            float netBar = Math.Min(1f, _netMBs / 10f); // 10 MB/s = full bar
            DrawRow(g, rowFont, labelBrush, valBrush, y, "NET",  netTxt, netBar,
                Color.FromArgb(255, 120, 200, 255));
        }

        private static Color ColorForPct(float pct)
        {
            if (pct < 40f) return Color.FromArgb(255,  80, 220, 120);
            if (pct < 75f) return Color.FromArgb(255, 235, 200,  80);
            return               Color.FromArgb(255, 240, 100,  95);
        }

        private void DrawRow(Graphics g, Font font, Brush labelBrush, Brush valBrush,
            int y, string label, string value, float bar01, Color barColor)
        {
            g.DrawString(label, font, labelBrush, new PointF(10, y));
            var valSize = g.MeasureString(value, font);
            g.DrawString(value, font, valBrush,
                new PointF(Width - 10 - valSize.Width, y));

            // Thin bar under the row
            int barY = y + 16;
            int barW = Width - 20;
            using var track = new SolidBrush(Color.FromArgb(60, 60, 70, 85));
            g.FillRectangle(track, 10, barY, barW, 2);
            int w = (int)(barW * Math.Clamp(bar01, 0f, 1f));
            if (w > 0)
            {
                using var fg = new SolidBrush(barColor);
                g.FillRectangle(fg, 10, barY, w, 2);
            }
        }

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            var p = new GraphicsPath();
            int d = radius * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        // Don't steal focus when shown
        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                const int WS_EX_NOACTIVATE = 0x08000000;
                const int WS_EX_TOOLWINDOW = 0x00000080;
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
                return cp;
            }
        }
    }
}
