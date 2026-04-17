using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CrosshairOverlay
{
    /// <summary>
    /// Themed achievements dialog — matches the app's glass/gradient style.
    /// </summary>
    internal class AchievementsForm : Form
    {
        internal struct Achievement
        {
            public string Name;
            public string Desc;
            public bool Unlocked;
            public double Progress; // 0..1
            public string Icon;     // emoji / glyph
        }

        private readonly List<Achievement> _list;
        private int _scrollY = 0;
        private int _contentHeight;

        private static readonly Color BgTop     = Color.FromArgb(245, 18, 10, 30);
        private static readonly Color BgBottom  = Color.FromArgb(245, 36, 20, 60);
        private static readonly Color BorderCol = Color.FromArgb(130, 80, 220);
        private static readonly Color Accent    = Color.FromArgb(175, 130, 255);
        private static readonly Color AccentGlow= Color.FromArgb(220, 170, 255);
        private static readonly Color TextMain  = Color.FromArgb(235, 228, 245);
        private static readonly Color TextDim   = Color.FromArgb(160, 150, 185);
        private static readonly Color CardBg    = Color.FromArgb(80, 50, 30, 110);
        private static readonly Color CardBgDone= Color.FromArgb(130, 60, 40, 160);

        private readonly Font _fontTitle = new("Segoe UI", 16f, FontStyle.Bold);
        private readonly Font _fontName  = new("Segoe UI Semibold", 10f);
        private readonly Font _fontDesc  = new("Segoe UI", 8.5f);
        private readonly Font _fontIcon  = new("Segoe UI Emoji", 18f);
        private readonly Font _fontClose = new("Segoe UI", 13f, FontStyle.Bold);

        private const int Width_ = 440;
        private const int Height_ = 520;
        private const int HeaderH = 60;
        private const int CardH = 72;
        private const int Gap = 8;
        private const int PadX = 16;

        private Rectangle _closeRect;

        public AchievementsForm(List<Achievement> list)
        {
            _list = list;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(Width_, Height_);
            BackColor = Color.FromArgb(18, 10, 30);
            DoubleBuffered = true;
            KeyPreview = true;
            ShowInTaskbar = false;

            MouseWheel += (_, e) =>
            {
                int maxScroll = Math.Max(0, _contentHeight - (Height - HeaderH));
                _scrollY = Math.Clamp(_scrollY - e.Delta / 2, 0, maxScroll);
                Invalidate();
            };
            KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };
            MouseDown += OnMouseDown;
            MouseMove += (_, _) => Invalidate();

            _contentHeight = PadX + list.Count * (CardH + Gap);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var full = new Rectangle(0, 0, Width, Height);
            using (var bg = new LinearGradientBrush(full, BgTop, BgBottom, 90f))
                g.FillRectangle(bg, full);

            // Header
            var headerRect = new Rectangle(0, 0, Width, HeaderH);
            using (var hbg = new LinearGradientBrush(headerRect,
                Color.FromArgb(200, 60, 30, 140), Color.FromArgb(200, 40, 20, 90), 90f))
                g.FillRectangle(hbg, headerRect);

            using (var border = new Pen(BorderCol, 1.3f))
                g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);

            using (var tb = new SolidBrush(TextMain))
                g.DrawString(Lang.IsRussian ? "🏆  Достижения" : "🏆  Achievements",
                    _fontTitle, tb, PadX, 14);

            // Close button
            _closeRect = new Rectangle(Width - 42, 14, 28, 28);
            bool closeHover = _closeRect.Contains(PointToClient(Cursor.Position));
            using (var cbg = new SolidBrush(closeHover ? Color.FromArgb(220, 180, 60, 60) : Color.FromArgb(80, 100, 60, 130)))
            using (var cpath = RoundRect(_closeRect, 8))
                g.FillPath(cbg, cpath);
            using (var ctb = new SolidBrush(TextMain))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("✕", _fontClose, ctb, _closeRect, sf);
            }

            // Cards
            g.SetClip(new Rectangle(0, HeaderH, Width, Height - HeaderH));
            int unlocked = 0;
            foreach (var a in _list) if (a.Unlocked) unlocked++;

            int y = HeaderH + Gap - _scrollY;
            int x = PadX;
            int w = Width - PadX * 2;
            foreach (var a in _list)
            {
                if (y + CardH >= HeaderH && y <= Height)
                    DrawCard(g, a, new Rectangle(x, y, w, CardH));
                y += CardH + Gap;
            }

            // Progress header — draw on top of clip reset
            g.ResetClip();
            using (var prBrush = new SolidBrush(TextDim))
                g.DrawString($"{unlocked} / {_list.Count}", _fontName, prBrush, Width - 90, 38);
        }

        private void DrawCard(Graphics g, Achievement a, Rectangle r)
        {
            using var path = RoundRect(r, 12);
            using (var bg = new LinearGradientBrush(r,
                a.Unlocked ? Color.FromArgb(180, 80, 40, 180) : Color.FromArgb(80, 40, 30, 70),
                a.Unlocked ? Color.FromArgb(180, 120, 60, 220) : Color.FromArgb(80, 50, 35, 85),
                30f))
                g.FillPath(bg, path);
            using (var border = new Pen(a.Unlocked ? AccentGlow : Color.FromArgb(60, 180, 140, 255), 1f))
                g.DrawPath(border, path);

            // Icon circle
            int iconD = CardH - 20;
            var iconRect = new Rectangle(r.X + 10, r.Y + 10, iconD, iconD);
            using (var ibg = new LinearGradientBrush(iconRect,
                a.Unlocked ? Accent : Color.FromArgb(80, 80, 60, 130),
                a.Unlocked ? AccentGlow : Color.FromArgb(60, 40, 30, 90), 45f))
            using (var ipath = RoundRect(iconRect, iconD / 2))
                g.FillPath(ibg, ipath);
            using (var ib = new SolidBrush(a.Unlocked ? Color.White : TextDim))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(a.Icon, _fontIcon, ib, iconRect, sf);
            }

            // Text
            int tx = r.X + 10 + iconD + 12;
            int tw = r.Width - (tx - r.X) - 12;
            using (var tn = new SolidBrush(a.Unlocked ? TextMain : TextDim))
                g.DrawString(a.Name, _fontName, tn, tx, r.Y + 8);
            using (var td = new SolidBrush(TextDim))
                g.DrawString(a.Desc, _fontDesc, td, new RectangleF(tx, r.Y + 28, tw, 20));

            // Progress bar
            var barRect = new Rectangle(tx, r.Y + CardH - 18, tw, 6);
            using (var bbg = new SolidBrush(Color.FromArgb(80, 40, 30, 80)))
            using (var bpath = RoundRect(barRect, 3))
                g.FillPath(bbg, bpath);
            double prog = Math.Clamp(a.Progress, 0, 1);
            if (prog > 0)
            {
                var fill = new Rectangle(barRect.X, barRect.Y, (int)(barRect.Width * prog), barRect.Height);
                if (fill.Width > 3)
                {
                    using var fbg = new LinearGradientBrush(fill, Accent, AccentGlow, 0f);
                    using var fpath = RoundRect(fill, 3);
                    g.FillPath(fbg, fpath);
                }
            }
        }

        private void OnMouseDown(object? sender, MouseEventArgs e)
        {
            if (_closeRect.Contains(e.Location)) { Close(); return; }
            if (e.Y < HeaderH && e.Button == MouseButtons.Left)
            {
                // drag window by header
                if (e.X < Width - 50)
                {
                    NativeMethods.ReleaseCapture();
                    NativeMethods.SendMessage(Handle, 0xA1, 0x2, 0);
                }
            }
        }

        private static GraphicsPath RoundRect(Rectangle r, int radius)
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

        private static class NativeMethods
        {
            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern bool ReleaseCapture();
            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
        }
    }
}
