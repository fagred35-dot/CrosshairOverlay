using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace CrosshairOverlay
{
    internal class VideoGalleryForm : Form
    {
        // ── Theme ──
        private static readonly Color BgColor = Color.FromArgb(245, 12, 6, 24);
        private static readonly Color Accent = Color.FromArgb(130, 80, 220);
        private static readonly Color AccentGlow = Color.FromArgb(175, 130, 255);
        private static readonly Color TextMain = Color.FromArgb(235, 228, 245);
        private static readonly Color TextDim = Color.FromArgb(130, 120, 155);
        private static readonly Color GlassBg = Color.FromArgb(35, 140, 100, 220);
        private static readonly Color GlassBorder = Color.FromArgb(45, 180, 140, 255);
        private static readonly Color CardBg = Color.FromArgb(60, 50, 30, 90);
        private static readonly Color CardHover = Color.FromArgb(80, 70, 40, 130);

        private const int PANEL_W = 400;
        private const int PANEL_H = 560;
        private const int HEADER_H = 54;
        private const int THUMB_W = 170;
        private const int THUMB_H = 96;
        private const int CARD_PAD = 8;
        private const int CARD_H = THUMB_H + 36;

        private readonly string _videoFolder;
        private readonly RecorderEngine _recorder;
        private readonly List<VideoEntry> _entries = new();

        // Animation
        private readonly System.Windows.Forms.Timer _animTimer;
        private bool _sliding, _slidingIn;
        private double _slideProgress;

        // Scroll & interaction
        private int _scrollY, _contentHeight;
        private int _hoverIndex = -1;
        private bool _dragging;
        private Point _dragStart, _formStart;
        private bool _headerRefreshHover, _headerCloseHover;

        // Fonts
        private readonly Font _fontTitle = new("Segoe UI", 11f, FontStyle.Bold);
        private readonly Font _fontFile = new("Segoe UI", 8f);
        private readonly Font _fontInfo = new("Segoe UI", 7.5f);
        private readonly Font _fontClose = new("Segoe UI", 12f, FontStyle.Bold);
        private readonly Font _fontEmpty = new("Segoe UI", 9.5f);

        // Cached GDI resources (avoid per-frame allocation)
        private readonly SolidBrush _brBg = new(BgColor);
        private readonly SolidBrush _brTextMain = new(TextMain);
        private readonly SolidBrush _brTextDim = new(TextDim);
        private readonly SolidBrush _brGlass = new(GlassBg);
        private readonly SolidBrush _brCardNorm = new(CardBg);
        private readonly SolidBrush _brCardHov = new(CardHover);
        private readonly SolidBrush _brNoThumb = new(Color.FromArgb(60, 30, 20, 50));
        private readonly SolidBrush _brPlayDim = new(Color.FromArgb(80, AccentGlow));
        private readonly SolidBrush _brPlayBright = new(Color.FromArgb(200, 255, 255, 255));
        private readonly SolidBrush _brOverlay = new(Color.FromArgb(100, 0, 0, 0));
        private readonly SolidBrush _brScrollbar = new(Color.FromArgb(50, AccentGlow));
        private readonly Pen _penBorder = new(GlassBorder, 1f);
        private readonly Pen _penCardHov = new(Color.FromArgb(80, AccentGlow), 1f);
        private readonly StringFormat _sfCenter = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        private readonly StringFormat _sfTrim = new() { Trimming = StringTrimming.EllipsisCharacter };

        // Content bitmap cache
        private Bitmap? _contentCache;
        private bool _contentDirty = true;

        private class VideoEntry
        {
            public string FilePath = "";
            public string FileName = "";
            public string Info = "";
            public Bitmap? Thumbnail;
            public Rectangle Bounds;
        }

        public VideoGalleryForm(string videoFolder, RecorderEngine recorder)
        {
            _videoFolder = videoFolder;
            _recorder = recorder;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(PANEL_W, PANEL_H);
            BackColor = Color.Black;
            Opacity = 0;

            // Initial position: off-screen right
            var screen = Screen.PrimaryScreen!.WorkingArea;
            Location = new Point(screen.Right, screen.Top + 10);

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);

            _animTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _animTimer.Tick += AnimTick;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                return cp;
            }
        }

        // ═══════════════════════════════════════
        //  Data loading
        // ═══════════════════════════════════════

        private void LoadVideos()
        {
            foreach (var e in _entries) e.Thumbnail?.Dispose();
            _entries.Clear();

            if (!Directory.Exists(_videoFolder)) { _contentHeight = HEADER_H + 60; return; }

            var files = Directory.GetFiles(_videoFolder, "*.mp4")
                .Concat(Directory.GetFiles(_videoFolder, "*.mkv"))
                .Concat(Directory.GetFiles(_videoFolder, "*.avi"))
                .OrderByDescending(File.GetCreationTime)
                .Take(50)
                .ToArray();

            int y = HEADER_H + 8;
            for (int i = 0; i < files.Length; i += 2)
            {
                for (int col = 0; col < 2 && i + col < files.Length; col++)
                {
                    string f = files[i + col];
                    var fi = new FileInfo(f);
                    long sizeMb = fi.Length / 1024 / 1024;
                    _entries.Add(new VideoEntry
                    {
                        FilePath = f,
                        FileName = fi.Name,
                        Info = $"{sizeMb} МБ · {fi.CreationTime:dd.MM HH:mm}",
                        Bounds = new Rectangle(
                            CARD_PAD + col * (THUMB_W + CARD_PAD),
                            y, THUMB_W, CARD_H)
                    });
                }
                y += CARD_H + CARD_PAD;
            }
            _contentHeight = y + 16;
        }

        private void LoadThumbnailsAsync()
        {
            // Load thumbnails on a background thread
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                foreach (var entry in _entries.ToArray())
                {
                    if (entry.Thumbnail != null) continue;
                    string? thumbPath = _recorder.GenerateThumbnail(
                        entry.FilePath, THUMB_W - 8, THUMB_H - 8);
                    if (thumbPath != null && File.Exists(thumbPath))
                    {
                        try
                        {
                            // Load without locking the file
                            using var fs = new FileStream(thumbPath, FileMode.Open, FileAccess.Read);
                            entry.Thumbnail = new Bitmap(fs);
                        }
                        catch { }
                        // Invalidate on UI thread
                        try { BeginInvoke(new Action(Invalidate)); } catch { }
                    }
                }
            });
        }

        // ═══════════════════════════════════════
        //  Animation
        // ═══════════════════════════════════════

        public void SlideIn()
        {
            LoadVideos();
            LoadThumbnailsAsync();
            _slidingIn = true;
            _sliding = true;
            _scrollY = 0;
            Visible = true;
            _animTimer.Start();
        }

        public void SlideOut()
        {
            _slidingIn = false;
            _sliding = true;
            _animTimer.Start();
        }

        private void AnimTick(object? sender, EventArgs e)
        {
            if (!_sliding) { _animTimer.Stop(); return; }

            double target = _slidingIn ? 1.0 : 0.0;
            _slideProgress += (target - _slideProgress) * 0.16;

            if (Math.Abs(_slideProgress - target) < 0.005)
            {
                _slideProgress = target;
                _sliding = false;
                if (!_slidingIn) { _animTimer.Stop(); Visible = false; return; }
            }

            var screen = Screen.PrimaryScreen!.WorkingArea;
            int targetX = screen.Right - PANEL_W - 12;
            int startX = screen.Right + 20;
            Location = new Point(
                (int)(startX + (targetX - startX) * _slideProgress),
                Location.Y);
            Opacity = _slideProgress;
            Invalidate();
        }

        // ═══════════════════════════════════════
        //  Painting
        // ═══════════════════════════════════════

        private void MarkContentDirty() { _contentDirty = true; }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            // During slide animation, draw minimal (no card content rebuild)
            bool animating = _sliding;

            // Background
            g.FillRectangle(_brBg, ClientRectangle);

            // Outer border
            using var outerPath = RoundRect(new Rectangle(0, 0, PANEL_W - 1, PANEL_H - 1), 16);
            g.DrawPath(_penBorder, outerPath);

            // Left accent (only when not animating to save time)
            if (!animating)
            {
                using var ab = new LinearGradientBrush(new Point(0, 0), new Point(0, PANEL_H),
                    Color.FromArgb(150, AccentGlow), Color.FromArgb(10, Accent));
                g.FillRectangle(ab, 0, 0, 2, PANEL_H);
            }

            DrawHeader(g);

            // Content area
            g.SetClip(new Rectangle(0, HEADER_H, PANEL_W, PANEL_H - HEADER_H));
            g.TranslateTransform(0, -_scrollY);

            for (int i = 0; i < _entries.Count; i++)
                DrawVideoCard(g, _entries[i], i == _hoverIndex);

            g.ResetTransform();
            g.ResetClip();

            // Empty state
            if (_entries.Count == 0)
            {
                g.DrawString(Lang.NoRecordings,
                    _fontEmpty, _brTextDim,
                    new Rectangle(0, HEADER_H, PANEL_W, PANEL_H - HEADER_H), _sfCenter);
            }

            // Scrollbar
            DrawScrollbar(g);
        }

        private void DrawHeader(Graphics g)
        {
            var headerRect = new Rectangle(6, 6, PANEL_W - 12, HEADER_H - 8);
            using var hdrPath = RoundRect(headerRect, 12);
            g.FillPath(_brGlass, hdrPath);
            g.DrawPath(_penBorder, hdrPath);

            g.DrawString(Lang.GalleryTitle, _fontTitle, _brTextMain, 16, 16);

            // Refresh button
            var refreshRect = new Rectangle(PANEL_W - 80, 12, 28, 28);
            bool refreshHover = _headerRefreshHover;
            using var refreshPath = RoundRect(refreshRect, 14);
            using (var refreshFill = new SolidBrush(refreshHover
                ? Color.FromArgb(60, AccentGlow) : Color.FromArgb(30, 255, 255, 255)))
                g.FillPath(refreshFill, refreshPath);
            g.DrawString("↻", _fontClose, refreshHover ? _brPlayDim : _brTextDim, refreshRect, _sfCenter);

            // Close button
            var closeRect = new Rectangle(PANEL_W - 46, 12, 28, 28);
            bool closeHover = _headerCloseHover;
            using var closePath = RoundRect(closeRect, 14);
            using (var closeFill = new SolidBrush(closeHover
                ? Color.FromArgb(140, 200, 60, 90) : Color.FromArgb(30, 255, 255, 255)))
                g.FillPath(closeFill, closePath);
            using var closeBrush = new SolidBrush(closeHover ? Color.White : TextDim);
            g.DrawString("✕", _fontClose, closeBrush, closeRect, _sfCenter);
        }

        private void DrawVideoCard(Graphics g, VideoEntry entry, bool hovered)
        {
            var b = entry.Bounds;
            using var cardPath = RoundRect(b, 10);
            g.FillPath(hovered ? _brCardHov : _brCardNorm, cardPath);
            if (hovered)
                g.DrawPath(_penCardHov, cardPath);

            // Thumbnail
            var thumbRect = new Rectangle(b.X + 4, b.Y + 4, b.Width - 8, THUMB_H - 8);
            if (entry.Thumbnail != null)
            {
                g.DrawImage(entry.Thumbnail, thumbRect);
            }
            else
            {
                using var ntPath = RoundRect(thumbRect, 6);
                g.FillPath(_brNoThumb, ntPath);
                g.DrawString("▶", _fontTitle, _brPlayDim, thumbRect, _sfCenter);
            }

            // Play icon overlay on hover
            if (hovered && entry.Thumbnail != null)
            {
                g.FillRectangle(_brOverlay, thumbRect);
                g.DrawString("▶", _fontTitle, _brPlayBright, thumbRect, _sfCenter);
            }

            // File name
            int textY = b.Y + THUMB_H - 2;
            var nameRect = new Rectangle(b.X + 6, textY, b.Width - 12, 16);
            g.DrawString(entry.FileName, _fontFile, _brTextMain, nameRect, _sfTrim);

            // Info
            g.DrawString(entry.Info, _fontInfo, _brTextDim, b.X + 6, textY + 16);
        }

        private void DrawScrollbar(Graphics g)
        {
            int viewH = PANEL_H - HEADER_H;
            if (_contentHeight <= viewH) return;
            float ratio = (float)viewH / _contentHeight;
            int barH = Math.Max(30, (int)(viewH * ratio));
            int maxScroll = _contentHeight - viewH;
            float scrollRatio = maxScroll > 0 ? (float)_scrollY / maxScroll : 0;
            int barY = HEADER_H + (int)((viewH - barH) * scrollRatio);
            var barRect = new Rectangle(PANEL_W - 7, barY, 4, barH);
            using var barPath = RoundRect(barRect, 2);
            g.FillPath(_brScrollbar, barPath);
        }

        // ═══════════════════════════════════════
        //  Input
        // ═══════════════════════════════════════

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;

            // Close button
            var closeRect = new Rectangle(PANEL_W - 46, 12, 28, 28);
            if (closeRect.Contains(e.Location)) { SlideOut(); return; }

            // Refresh button
            var refreshRect = new Rectangle(PANEL_W - 80, 12, 28, 28);
            if (refreshRect.Contains(e.Location))
            {
                LoadVideos(); LoadThumbnailsAsync(); Invalidate(); return;
            }

            // Header drag
            if (e.Y < HEADER_H)
            {
                _dragging = true;
                _dragStart = e.Location;
                _formStart = Location;
                return;
            }

            // Card click
            int idx = HitTest(e.Location);
            if (idx >= 0 && idx < _entries.Count)
            {
                try
                {
                    Process.Start(new ProcessStartInfo(_entries[idx].FilePath)
                    { UseShellExecute = true });
                }
                catch { }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragging)
            {
                Location = new Point(
                    _formStart.X + e.X - _dragStart.X,
                    _formStart.Y + e.Y - _dragStart.Y);
                return;
            }
            int oldHover = _hoverIndex;
            _hoverIndex = HitTest(e.Location);

            // Track header button hovers
            bool oldRefresh = _headerRefreshHover, oldClose = _headerCloseHover;
            _headerRefreshHover = new Rectangle(PANEL_W - 80, 12, 28, 28).Contains(e.Location);
            _headerCloseHover = new Rectangle(PANEL_W - 46, 12, 28, 28).Contains(e.Location);

            if (oldHover != _hoverIndex || oldRefresh != _headerRefreshHover || oldClose != _headerCloseHover)
                Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _dragging = false;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            int viewH = PANEL_H - HEADER_H;
            int maxScroll = Math.Max(0, _contentHeight - viewH);
            _scrollY = Math.Clamp(_scrollY - e.Delta / 2, 0, maxScroll);
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverIndex >= 0) { _hoverIndex = -1; Invalidate(); }
        }

        private int HitTest(Point clientPos)
        {
            if (clientPos.Y <= HEADER_H) return -1;
            int y = clientPos.Y + _scrollY;
            for (int i = 0; i < _entries.Count; i++)
            {
                var b = _entries[i].Bounds;
                if (y >= b.Y && y < b.Y + b.Height &&
                    clientPos.X >= b.X && clientPos.X < b.X + b.Width)
                    return i;
            }
            return -1;
        }

        // ═══════════════════════════════════════
        //  Cleanup
        // ═══════════════════════════════════════

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _animTimer.Stop(); _animTimer.Dispose();
            foreach (var entry in _entries) entry.Thumbnail?.Dispose();
            _fontTitle.Dispose(); _fontFile.Dispose();
            _fontInfo.Dispose(); _fontClose.Dispose(); _fontEmpty.Dispose();
            _brBg.Dispose(); _brTextMain.Dispose(); _brTextDim.Dispose();
            _brGlass.Dispose(); _brCardNorm.Dispose(); _brCardHov.Dispose();
            _brNoThumb.Dispose(); _brPlayDim.Dispose(); _brPlayBright.Dispose();
            _brOverlay.Dispose(); _brScrollbar.Dispose();
            _penBorder.Dispose(); _penCardHov.Dispose();
            _sfCenter.Dispose(); _sfTrim.Dispose();
            _contentCache?.Dispose();
            base.OnFormClosed(e);
        }

        private static GraphicsPath RoundRect(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            if (d > bounds.Height) d = bounds.Height;
            if (d > bounds.Width) d = bounds.Width;
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
