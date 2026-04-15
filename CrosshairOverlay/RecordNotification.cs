using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Windows.Forms;

namespace CrosshairOverlay
{
    internal class RecordNotification : Form
    {
        private readonly string _filePath;
        private readonly string _fileName;
        private readonly System.Windows.Forms.Timer _fadeTimer;
        private float _opacity;
        private bool _fadingIn = true;
        private int _holdElapsed;

        private static readonly Color BgColor = Color.FromArgb(240, 16, 10, 30);
        private static readonly Color Accent = Color.FromArgb(130, 80, 220);
        private static readonly Color AccentGlow = Color.FromArgb(175, 130, 255);
        private static readonly Color TextColor = Color.FromArgb(235, 228, 245);
        private static readonly Color DimColor = Color.FromArgb(130, 120, 155);
        private static readonly Color GlassBorder = Color.FromArgb(60, 180, 140, 255);
        private const int W = 340, H = 80;

        private readonly Font _fontTitle = new("Segoe UI Semibold", 10f);
        private readonly Font _fontFile = new("Segoe UI", 8.5f);
        private readonly Font _fontHint = new("Segoe UI", 7.5f);

        /// <summary>Position: 0=TopLeft, 1=TopRight, 2=BottomLeft, 3=BottomRight</summary>
        public int NotifPosition { get; set; } = 1;

        public RecordNotification(string filePath, int position = 1)
        {
            _filePath = filePath;
            _fileName = Path.GetFileName(filePath);
            NotifPosition = position;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(W, H);
            BackColor = Color.Black;
            Opacity = 0;

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);

            PositionOnScreen();
            Cursor = Cursors.Hand;

            _fadeTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _fadeTimer.Tick += FadeTick;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x08000000 | 0x8; // WS_EX_NOACTIVATE | WS_EX_TOPMOST
                return cp;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _fadeTimer.Start();
        }

        private void PositionOnScreen()
        {
            var screen = Screen.PrimaryScreen!.WorkingArea;
            int m = 16;
            Location = NotifPosition switch
            {
                0 => new Point(screen.Left + m, screen.Top + m),
                1 => new Point(screen.Right - W - m, screen.Top + m),
                2 => new Point(screen.Left + m, screen.Bottom - H - m),
                3 => new Point(screen.Right - W - m, screen.Bottom - H - m),
                _ => new Point(screen.Right - W - m, screen.Top + m)
            };
        }

        private void FadeTick(object? sender, EventArgs e)
        {
            if (_fadingIn)
            {
                _opacity += 0.08f;
                if (_opacity >= 1f) { _opacity = 1f; _fadingIn = false; }
            }
            else
            {
                _holdElapsed += _fadeTimer.Interval;
                if (_holdElapsed >= 5000)
                {
                    _opacity -= 0.04f;
                    if (_opacity <= 0f) { _fadeTimer.Stop(); Close(); return; }
                }
            }
            Opacity = _opacity;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var rect = new Rectangle(0, 0, W - 1, H - 1);
            using var path = RoundRect(rect, 14);
            using (var fill = new SolidBrush(BgColor))
                g.FillPath(fill, path);
            using (var border = new Pen(GlassBorder, 1f))
                g.DrawPath(border, path);

            // Left accent bar
            using (var accentBrush = new SolidBrush(Accent))
                g.FillRectangle(accentBrush, 0, 16, 3, H - 32);

            // Record saved icon — green circle with check
            using (var circBrush = new SolidBrush(Color.FromArgb(60, 200, 80)))
                g.FillEllipse(circBrush, 16, (H - 24) / 2, 24, 24);
            using var checkPen = new Pen(Color.White, 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(checkPen, 23, H / 2 + 1, 27, H / 2 + 5);
            g.DrawLine(checkPen, 27, H / 2 + 5, 35, H / 2 - 4);

            // Text
            using var titleBrush = new SolidBrush(TextColor);
            g.DrawString(Lang.RecordSaved, _fontTitle, titleBrush, 48, 12);
            using var fileBrush = new SolidBrush(AccentGlow);
            var fileRect = new Rectangle(48, 34, W - 60, 16);
            g.DrawString(_fileName, _fontFile, fileBrush, fileRect,
                new StringFormat { Trimming = StringTrimming.EllipsisPath });
            using var hintBrush = new SolidBrush(DimColor);
            g.DrawString(Lang.ClickToOpen, _fontHint, hintBrush, 48, 54);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            try
            {
                if (File.Exists(_filePath))
                    Process.Start(new ProcessStartInfo(_filePath) { UseShellExecute = true });
            }
            catch { }
            Close();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _fadeTimer.Stop(); _fadeTimer.Dispose();
            _fontTitle.Dispose(); _fontFile.Dispose(); _fontHint.Dispose();
            base.OnFormClosed(e);
        }

        private static GraphicsPath RoundRect(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
