using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace CrosshairOverlay
{
    public class CrosshairGalleryForm : Form
    {
        private readonly OverlayForm _overlay;
        private int _hoverIndex = -1;
        private int _scrollY = 0;
        private int _contentHeight = 0;
        private bool _draggingScrollbar = false;

        // Community images
        private readonly System.Collections.Generic.List<string> _communityImages = new();
        private static readonly string CommunityFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CrosshairOverlay", "community");

        // Layout
        private const int Cols = 4;
        private const int CardSize = 90;
        private const int CardGap = 10;
        private const int PadX = 20;
        private const int PadTop = 20;
        private const int SectionH = 36;

        // Standard styles (all except CustomImage)
        private static readonly OverlayForm.CrosshairStyle[] StandardStyles =
        {
            OverlayForm.CrosshairStyle.Cross,
            OverlayForm.CrosshairStyle.Circle,
            OverlayForm.CrosshairStyle.Dot,
            OverlayForm.CrosshairStyle.CrossWithCircle,
            OverlayForm.CrosshairStyle.Chevron,
            OverlayForm.CrosshairStyle.TShape,
            OverlayForm.CrosshairStyle.Diamond,
            OverlayForm.CrosshairStyle.Arrow,
            OverlayForm.CrosshairStyle.Plus,
            OverlayForm.CrosshairStyle.XShape,
            OverlayForm.CrosshairStyle.TriangleDown,
            OverlayForm.CrosshairStyle.Crosshairs,
            OverlayForm.CrosshairStyle.SquareBrackets,
            OverlayForm.CrosshairStyle.Wings,
        };

        private static readonly string[] StyleLabels =
        {
            "Cross", "Circle", "Dot", "Cross+Circle", "Chevron", "T-Shape",
            "Diamond", "Arrow", "Plus", "X-Shape", "Triangle", "Crosshairs",
            "Brackets", "Wings",
        };

        // Fonts
        private readonly Font _fontTitle = new("Segoe UI", 14f, FontStyle.Bold);
        private readonly Font _fontSection = new("Segoe UI", 10f, FontStyle.Bold);
        private readonly Font _fontLabel = new("Segoe UI", 7f);
        private readonly Font _fontClose = new("Segoe UI", 14f, FontStyle.Bold);

        public CrosshairGalleryForm(OverlayForm overlay)
        {
            _overlay = overlay;

            Text = "Crosshair Gallery";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            int totalW = PadX * 2 + Cols * (CardSize + CardGap) - CardGap;
            ClientSize = new Size(totalW, 520);
            DoubleBuffered = true;
            BackColor = Color.FromArgb(12, 6, 24);
            ShowInTaskbar = false;

            MouseWheel += (s, e) =>
            {
                _scrollY = Math.Max(0, Math.Min(_scrollY - e.Delta / 3, Math.Max(0, _contentHeight - ClientSize.Height + 40)));
                Invalidate();
            };

            LoadCommunityImages();
        }

        private void LoadCommunityImages()
        {
            _communityImages.Clear();
            if (!Directory.Exists(CommunityFolder))
            {
                try { Directory.CreateDirectory(CommunityFolder); } catch { }
            }
            if (Directory.Exists(CommunityFolder))
            {
                foreach (var f in Directory.GetFiles(CommunityFolder, "*.*"))
                {
                    var ext = Path.GetExtension(f).ToLowerInvariant();
                    if (ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif")
                        _communityImages.Add(f);
                }
            }
        }

        private int GetCardIndex(int x, int y)
        {
            int ly = y + _scrollY;
            int totalItems = StandardStyles.Length + 1 + _communityImages.Count + 1; // +1 add button per section? nah

            // Standard section
            int curY = PadTop + SectionH;
            int stdRows = (StandardStyles.Length + Cols - 1) / Cols;
            int stdEndY = curY + stdRows * (CardSize + CardGap);

            if (ly >= curY && ly < stdEndY)
            {
                int row = (ly - curY) / (CardSize + CardGap);
                int col = (x - PadX) / (CardSize + CardGap);
                if (col >= 0 && col < Cols && x >= PadX && x < PadX + Cols * (CardSize + CardGap))
                {
                    int idx = row * Cols + col;
                    if (idx < StandardStyles.Length)
                        return idx; // 0..13 = standard
                }
            }

            // Community section
            curY = stdEndY + SectionH;
            int comCount = _communityImages.Count + 1; // +1 for "add" button
            int comRows = (comCount + Cols - 1) / Cols;
            int comEndY = curY + comRows * (CardSize + CardGap);

            if (ly >= curY && ly < comEndY)
            {
                int row = (ly - curY) / (CardSize + CardGap);
                int col = (x - PadX) / (CardSize + CardGap);
                if (col >= 0 && col < Cols && x >= PadX && x < PadX + Cols * (CardSize + CardGap))
                {
                    int idx = row * Cols + col;
                    if (idx < comCount)
                        return StandardStyles.Length + idx; // 14+ = community, last = add button
                }
            }

            return -1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // Background
            g.Clear(Color.FromArgb(12, 6, 24));

            // Border glow
            using var borderPen = new Pen(Color.FromArgb(50, SettingsForm.GetAccent()), 2f);
            g.DrawRectangle(borderPen, 1, 1, Width - 3, Height - 3);

            // Close button (top-right)
            using var closeBrush = new SolidBrush(_hoverIndex == -999 ? Color.FromArgb(220, 255, 80, 80) : Color.FromArgb(180, 200, 200, 220));
            g.DrawString("×", _fontClose, closeBrush, Width - 32, 4);

            // Title
            using var titleBrush = new SolidBrush(Color.FromArgb(235, 228, 245));
            g.DrawString(Lang.CrosshairGalleryTitle, _fontTitle, titleBrush, PadX, 8);

            g.TranslateTransform(0, -_scrollY);
            int curY = PadTop;

            // ── STANDARD SECTION ──
            using var secBrush = new SolidBrush(SettingsForm.GetAccent());
            g.DrawString(Lang.GalleryStandard, _fontSection, secBrush, PadX, curY + 4);
            curY += SectionH;

            for (int i = 0; i < StandardStyles.Length; i++)
            {
                int row = i / Cols, col = i % Cols;
                int cx = PadX + col * (CardSize + CardGap);
                int cy = curY + row * (CardSize + CardGap);
                bool hover = _hoverIndex == i;
                bool selected = _overlay._style == StandardStyles[i];
                DrawCard(g, cx, cy, CardSize, CardSize, hover, selected);
                DrawCrosshairPreview(g, cx, cy, CardSize, StandardStyles[i]);
                // Label
                using var lblBrush = new SolidBrush(selected ? Color.White : Color.FromArgb(160, 180, 170, 210));
                var sf = new StringFormat { Alignment = StringAlignment.Center };
                g.DrawString(StyleLabels[i], _fontLabel, lblBrush, cx + CardSize / 2, cy + CardSize - 14, sf);
            }

            int stdRows = (StandardStyles.Length + Cols - 1) / Cols;
            curY += stdRows * (CardSize + CardGap);

            // ── COMMUNITY SECTION ──
            g.DrawString(Lang.GalleryCommunity, _fontSection, secBrush, PadX, curY + 4);
            curY += SectionH;

            int comCount = _communityImages.Count + 1;
            for (int i = 0; i < comCount; i++)
            {
                int row = i / Cols, col = i % Cols;
                int cx = PadX + col * (CardSize + CardGap);
                int cy = curY + row * (CardSize + CardGap);
                int globalIdx = StandardStyles.Length + i;
                bool hover = _hoverIndex == globalIdx;

                if (i < _communityImages.Count)
                {
                    bool selected = _overlay._style == OverlayForm.CrosshairStyle.CustomImage
                        && _overlay._customImagePath == _communityImages[i];
                    DrawCard(g, cx, cy, CardSize, CardSize, hover, selected);
                    DrawImagePreview(g, cx, cy, CardSize, _communityImages[i]);
                    // Filename label
                    string name = Path.GetFileNameWithoutExtension(_communityImages[i]);
                    if (name.Length > 10) name = name[..9] + "…";
                    using var lblBrush2 = new SolidBrush(Color.FromArgb(160, 180, 170, 210));
                    var sf2 = new StringFormat { Alignment = StringAlignment.Center };
                    g.DrawString(name, _fontLabel, lblBrush2, cx + CardSize / 2, cy + CardSize - 14, sf2);
                }
                else
                {
                    // "Add" button
                    DrawCard(g, cx, cy, CardSize, CardSize, hover, false);
                    using var addBrush = new SolidBrush(hover ? Color.FromArgb(200, 180, 140, 255) : Color.FromArgb(100, 130, 100, 180));
                    var sf3 = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    using var addFont = new Font("Segoe UI", 28f, FontStyle.Bold);
                    g.DrawString("+", addFont, addBrush, new RectangleF(cx, cy, CardSize, CardSize - 8), sf3);
                    using var lblBrush3 = new SolidBrush(Color.FromArgb(120, 130, 100, 180));
                    var sf4 = new StringFormat { Alignment = StringAlignment.Center };
                    g.DrawString(Lang.GalleryAdd, _fontLabel, lblBrush3, cx + CardSize / 2, cy + CardSize - 14, sf4);
                }
            }

            int comRows = (comCount + Cols - 1) / Cols;
            curY += comRows * (CardSize + CardGap);
            _contentHeight = curY + PadTop;

            g.ResetTransform();

            // Scrollbar
            if (_contentHeight > ClientSize.Height)
            {
                float ratio = (float)ClientSize.Height / _contentHeight;
                int barH = Math.Max(30, (int)(ClientSize.Height * ratio));
                int barY = (int)((float)_scrollY / (_contentHeight - ClientSize.Height) * (ClientSize.Height - barH));
                using var sbBrush = new SolidBrush(Color.FromArgb(60, 255, 255, 255));
                using var sbPath = RoundRect(new Rectangle(Width - 8, barY, 5, barH), 3);
                g.FillPath(sbBrush, sbPath);
            }
        }

        private void DrawCard(Graphics g, int x, int y, int w, int h, bool hover, bool selected)
        {
            var rect = new Rectangle(x, y, w, h);
            using var path = RoundRect(rect, 12);

            // Background
            Color bg = hover ? Color.FromArgb(60, 70, 40, 120) : Color.FromArgb(40, 50, 30, 90);
            using var bgBrush = new SolidBrush(bg);
            g.FillPath(bgBrush, path);

            // Border
            if (selected)
            {
                using var selPen = new Pen(Color.FromArgb(200, SettingsForm.GetAccent()), 2.5f);
                g.DrawPath(selPen, path);
                // Glow
                using var glowPen = new Pen(Color.FromArgb(40, SettingsForm.GetAccent()), 6f);
                g.DrawPath(glowPen, path);
            }
            else
            {
                using var brdPen = new Pen(Color.FromArgb(hover ? 60 : 35, 180, 140, 255), 1f);
                g.DrawPath(brdPen, path);
            }
        }

        private void DrawCrosshairPreview(Graphics g, int x, int y, int cardSize, OverlayForm.CrosshairStyle style)
        {
            int cx = x + cardSize / 2;
            int cy = y + cardSize / 2 - 4;
            float s = 16f;
            float gap = 3f;
            float t = 2f;
            float ow = 1f;

            using var brush = new SolidBrush(Color.FromArgb(220, 0, 255, 80));
            using var outBrush = new SolidBrush(Color.FromArgb(180, 0, 0, 0));

            switch (style)
            {
                case OverlayForm.CrosshairStyle.Cross:
                case OverlayForm.CrosshairStyle.Plus:
                    DrawPreviewCross(g, cx, cy, s, gap, t, ow, brush, outBrush, false);
                    break;
                case OverlayForm.CrosshairStyle.TShape:
                    DrawPreviewCross(g, cx, cy, s, gap, t, ow, brush, outBrush, true);
                    break;
                case OverlayForm.CrosshairStyle.Circle:
                    DrawPreviewCircle(g, cx, cy, s, t, ow, brush, outBrush);
                    break;
                case OverlayForm.CrosshairStyle.CrossWithCircle:
                    DrawPreviewCross(g, cx, cy, s, gap, t, ow, brush, outBrush, false);
                    DrawPreviewCircle(g, cx, cy, s, t, ow, brush, outBrush);
                    break;
                case OverlayForm.CrosshairStyle.Dot:
                    g.FillEllipse(outBrush, cx - 5, cy - 5, 10, 10);
                    g.FillEllipse(brush, cx - 4, cy - 4, 8, 8);
                    break;
                case OverlayForm.CrosshairStyle.Chevron:
                    DrawPreviewChevron(g, cx, cy, s, gap, t, ow, brush, outBrush);
                    break;
                case OverlayForm.CrosshairStyle.Diamond:
                    DrawPreviewDiamond(g, cx, cy, s, t, ow, brush, outBrush);
                    break;
                case OverlayForm.CrosshairStyle.Arrow:
                    DrawPreviewArrow(g, cx, cy, s, t, ow, brush, outBrush);
                    break;
                case OverlayForm.CrosshairStyle.XShape:
                    DrawPreviewX(g, cx, cy, s, gap, t, ow, brush, outBrush);
                    break;
                case OverlayForm.CrosshairStyle.TriangleDown:
                    DrawPreviewTriangleDown(g, cx, cy, s, t, ow, brush, outBrush);
                    break;
                case OverlayForm.CrosshairStyle.Crosshairs:
                    DrawPreviewCross(g, cx, cy, s, gap, t, ow, brush, outBrush, false);
                    DrawPreviewCircle(g, cx, cy, s, t, ow, brush, outBrush);
                    g.FillEllipse(brush, cx - 3, cy - 3, 6, 6);
                    break;
                case OverlayForm.CrosshairStyle.SquareBrackets:
                    DrawPreviewBrackets(g, cx, cy, s, t, ow, brush, outBrush);
                    break;
                case OverlayForm.CrosshairStyle.Wings:
                    DrawPreviewWings(g, cx, cy, s, gap, t, ow, brush, outBrush);
                    break;
            }
        }

        private void DrawImagePreview(Graphics g, int x, int y, int cardSize, string path)
        {
            try
            {
                using var img = new Bitmap(path);
                int maxDim = cardSize - 24;
                float scale = Math.Min((float)maxDim / img.Width, (float)maxDim / img.Height);
                int w = (int)(img.Width * scale);
                int h = (int)(img.Height * scale);
                int px = x + (cardSize - w) / 2;
                int py = y + (cardSize - h) / 2 - 4;
                g.DrawImage(img, px, py, w, h);
            }
            catch { }
        }

        #region Preview Drawing Helpers

        private void DrawPreviewCross(Graphics g, int cx, int cy, float s, float gap, float t, float ow,
            Brush brush, SolidBrush outBrush, bool tStyle)
        {
            var lines = new (PointF a, PointF b)[]
            {
                (new(cx, cy - gap - s), new(cx, cy - gap)),       // top
                (new(cx + gap, cy), new(cx + gap + s, cy)),       // right
                (new(cx, cy + gap), new(cx, cy + gap + s)),       // bottom
                (new(cx - gap - s, cy), new(cx - gap, cy)),       // left
            };
            int start = tStyle ? 0 : 0;
            int end = tStyle ? 3 : 4;
            for (int i = start; i < end; i++)
            {
                using var op = new Pen(outBrush, t + ow * 2) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLine(op, lines[i].a, lines[i].b);
                using var p = new Pen(brush, t) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLine(p, lines[i].a, lines[i].b);
            }
            if (tStyle)
            {
                // Left and right only, no bottom
                using var op = new Pen(outBrush, t + ow * 2) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLine(op, lines[3].a, lines[3].b);
                using var p = new Pen(brush, t) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLine(p, lines[3].a, lines[3].b);
            }
        }

        private void DrawPreviewCircle(Graphics g, int cx, int cy, float s, float t, float ow, Brush brush, SolidBrush outBrush)
        {
            float r = s;
            using var op = new Pen(outBrush, t + ow * 2);
            g.DrawEllipse(op, cx - r, cy - r, r * 2, r * 2);
            using var p = new Pen(brush, t);
            g.DrawEllipse(p, cx - r, cy - r, r * 2, r * 2);
        }

        private void DrawPreviewChevron(Graphics g, int cx, int cy, float s, float gap, float t, float ow, Brush brush, SolidBrush outBrush)
        {
            float h = s * 0.7f;
            var pts = new PointF[] { new(cx - h, cy - h * 0.5f), new(cx, cy + h * 0.3f), new(cx + h, cy - h * 0.5f) };
            using var op = new Pen(outBrush, t + ow * 2) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLines(op, pts);
            using var p = new Pen(brush, t) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLines(p, pts);
        }

        private void DrawPreviewDiamond(Graphics g, int cx, int cy, float s, float t, float ow, Brush brush, SolidBrush outBrush)
        {
            float h = s * 0.8f;
            var pts = new PointF[] { new(cx, cy - h), new(cx + h, cy), new(cx, cy + h), new(cx - h, cy), new(cx, cy - h) };
            using var op = new Pen(outBrush, t + ow * 2) { LineJoin = LineJoin.Round };
            g.DrawLines(op, pts);
            using var p = new Pen(brush, t) { LineJoin = LineJoin.Round };
            g.DrawLines(p, pts);
        }

        private void DrawPreviewArrow(Graphics g, int cx, int cy, float s, float t, float ow, Brush brush, SolidBrush outBrush)
        {
            float h = s * 0.8f;
            var pts = new PointF[] { new(cx - h, cy - h * 0.4f), new(cx, cy + h * 0.4f), new(cx + h, cy - h * 0.4f) };
            using var op = new Pen(outBrush, t + ow * 2) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLines(op, pts);
            using var p = new Pen(brush, t) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLines(p, pts);
        }

        private void DrawPreviewX(Graphics g, int cx, int cy, float s, float gap, float t, float ow, Brush brush, SolidBrush outBrush)
        {
            float h = s;
            float gd = gap * 0.7f;
            var lines = new (PointF a, PointF b)[]
            {
                (new(cx - h, cy - h), new(cx - gd, cy - gd)),
                (new(cx + h, cy - h), new(cx + gd, cy - gd)),
                (new(cx - h, cy + h), new(cx - gd, cy + gd)),
                (new(cx + h, cy + h), new(cx + gd, cy + gd)),
            };
            foreach (var (a, b) in lines)
            {
                using var op = new Pen(outBrush, t + ow * 2) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLine(op, a, b);
                using var p = new Pen(brush, t) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLine(p, a, b);
            }
        }

        private void DrawPreviewTriangleDown(Graphics g, int cx, int cy, float s, float t, float ow, Brush brush, SolidBrush outBrush)
        {
            float h = s * 0.9f;
            var pts = new PointF[] { new(cx - h, cy - h * 0.5f), new(cx, cy + h * 0.7f), new(cx + h, cy - h * 0.5f), new(cx - h, cy - h * 0.5f) };
            using var op = new Pen(outBrush, t + ow * 2) { LineJoin = LineJoin.Round };
            g.DrawLines(op, pts);
            using var p = new Pen(brush, t) { LineJoin = LineJoin.Round };
            g.DrawLines(p, pts);
        }

        private void DrawPreviewBrackets(Graphics g, int cx, int cy, float s, float t, float ow, Brush brush, SolidBrush outBrush)
        {
            float h = s;
            float tick = h * 0.4f;
            var brackets = new PointF[][]
            {
                new[] { new PointF(cx - h + tick, cy - h), new(cx - h, cy - h), new(cx - h, cy + h), new(cx - h + tick, cy + h) },
                new[] { new PointF(cx + h - tick, cy - h), new(cx + h, cy - h), new(cx + h, cy + h), new(cx + h - tick, cy + h) },
            };
            foreach (var pts in brackets)
            {
                using var op = new Pen(outBrush, t + ow * 2) { LineJoin = LineJoin.Miter, StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLines(op, pts);
                using var p = new Pen(brush, t) { LineJoin = LineJoin.Miter, StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLines(p, pts);
            }
        }

        private void DrawPreviewWings(Graphics g, int cx, int cy, float s, float gap, float t, float ow, Brush brush, SolidBrush outBrush)
        {
            float h = s;
            float gH = gap * 0.5f;
            var left = new PointF[] { new(cx - h, cy - h * 0.6f), new(cx - gH, cy), new(cx - h, cy + h * 0.6f) };
            var right = new PointF[] { new(cx + h, cy - h * 0.6f), new(cx + gH, cy), new(cx + h, cy + h * 0.6f) };
            foreach (var pts in new[] { left, right })
            {
                using var op = new Pen(outBrush, t + ow * 2) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLines(op, pts);
                using var p = new Pen(brush, t) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLines(p, pts);
            }
        }

        #endregion

        #region Mouse Handling

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int oldHover = _hoverIndex;

            // Close button hit test
            if (e.X >= Width - 36 && e.X <= Width - 8 && e.Y >= 4 && e.Y <= 30)
                _hoverIndex = -999;
            else
                _hoverIndex = GetCardIndex(e.X, e.Y);

            if (_hoverIndex != oldHover)
                Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;

            // Close
            if (_hoverIndex == -999)
            {
                Close();
                return;
            }

            if (_hoverIndex < 0) return;

            if (_hoverIndex < StandardStyles.Length)
            {
                // Select standard style
                _overlay._style = StandardStyles[_hoverIndex];
                _overlay._needsStaticRender = true;
                _overlay.SaveSettings();
                Invalidate();
            }
            else
            {
                int comIdx = _hoverIndex - StandardStyles.Length;
                if (comIdx < _communityImages.Count)
                {
                    // Select community image
                    _overlay._style = OverlayForm.CrosshairStyle.CustomImage;
                    _overlay._customImagePath = _communityImages[comIdx];
                    _overlay._customImageCache?.Dispose();
                    try { _overlay._customImageCache = new Bitmap(_communityImages[comIdx]); }
                    catch { _overlay._customImageCache = null; }
                    _overlay._needsStaticRender = true;
                    _overlay.SaveSettings();
                    Invalidate();
                }
                else
                {
                    // Add button clicked
                    AddCommunityImage();
                }
            }
        }

        private void AddCommunityImage()
        {
            using var ofd = new OpenFileDialog
            {
                Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files|*.*",
                Title = Lang.ChooseImage
            };
            if (ofd.ShowDialog() != DialogResult.OK) return;

            try
            {
                string dest = Path.Combine(CommunityFolder, Path.GetFileName(ofd.FileName));
                // Avoid overwrite — add number
                int n = 1;
                while (File.Exists(dest))
                {
                    dest = Path.Combine(CommunityFolder,
                        Path.GetFileNameWithoutExtension(ofd.FileName) + $"_{n++}" + Path.GetExtension(ofd.FileName));
                }
                File.Copy(ofd.FileName, dest);
                LoadCommunityImages();

                // Auto-select the new image
                _overlay._style = OverlayForm.CrosshairStyle.CustomImage;
                _overlay._customImagePath = dest;
                _overlay._customImageCache?.Dispose();
                try { _overlay._customImageCache = new Bitmap(dest); }
                catch { _overlay._customImageCache = null; }
                _overlay._needsStaticRender = true;
                _overlay.SaveSettings();
                Invalidate();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoverIndex = -1;
            Invalidate();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) Close();
            base.OnKeyDown(e);
        }

        #endregion

        private static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _fontTitle.Dispose();
                _fontSection.Dispose();
                _fontLabel.Dispose();
                _fontClose.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
