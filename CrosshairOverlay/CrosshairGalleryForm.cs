using System;
using System.Collections.Generic;
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
        private const int PadTop = 44;   // content starts below the title row

        // v2.5 — window dragging via native caption-move (smooth, no manual tracking)
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private static readonly IntPtr HTCAPTION = (IntPtr)0x2;
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        private const int SectionH = 36;

        // Hit-test index ranges (so sections can grow without collisions)
        private const int ArtIndexBase = 1000;    // v2.4: 50 hand-designed crosshairs
        private const int PresetIndexBase = 2000;
        private const int RandomButtonId = -998;

        // v2.4: community image previews used to be decoded from disk on every
        // paint (each hover repaint = full PNG decode per card). Cache the scaled
        // thumbnails instead.
        private readonly Dictionary<string, Bitmap> _thumbCache = new();

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

        // Scrollbar state
        private bool _draggingSb;
        private int _sbGrabOffset;
        private const int SbWidth = 10;
        private const int SbRightPad = 4;
        private const int SbHitPad = 18;

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
            TopMost = true;

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

        // ── v2.4 unified section model ─────────────────────────────────
        // Paint and hit-testing used to duplicate the same layout math per
        // section (an easy way to desync them). Both now walk this single list.
        // Card ids: 0..13 standard · 14+ community (last = add button) ·
        // 1000+ art · 2000+ presets. Favorites reuse the original ids.

        private List<(string Title, List<int> Ids)> BuildSections()
        {
            var sections = new List<(string, List<int>)>();

            var favs = new List<int>();
            foreach (var key in _overlay._galleryFavorites)
            {
                int id = FavKeyToId(key);
                if (id >= 0) favs.Add(id);
            }
            if (favs.Count > 0)
                sections.Add((Lang.GalleryFavorites, favs));

            var std = new List<int>();
            for (int i = 0; i < StandardStyles.Length; i++) std.Add(i);
            sections.Add((Lang.GalleryStandard, std));

            var art = new List<int>();
            for (int i = 0; i < ArtCrosshairs.Count; i++) art.Add(ArtIndexBase + i);
            sections.Add((Lang.GalleryArt, art));

            var com = new List<int>();
            for (int i = 0; i <= _communityImages.Count; i++) com.Add(StandardStyles.Length + i);
            sections.Add((Lang.GalleryCommunity, com));

            var pre = new List<int>();
            for (int i = 0; i < CrosshairPresets.All.Count; i++) pre.Add(PresetIndexBase + i);
            sections.Add((Lang.GalleryPresets, pre));

            return sections;
        }

        private string? IdToFavKey(int id)
        {
            if (id >= PresetIndexBase) return "preset:" + (id - PresetIndexBase);
            if (id >= ArtIndexBase) return "art:" + (id - ArtIndexBase);
            if (id >= 0 && id < StandardStyles.Length) return "std:" + id;
            return null; // community images / add button are not favoritable
        }

        private int FavKeyToId(string key)
        {
            int sep = key.IndexOf(':');
            if (sep <= 0 || !int.TryParse(key[(sep + 1)..], out int i) || i < 0) return -1;
            return key[..sep] switch
            {
                "std" when i < StandardStyles.Length => i,
                "art" when i < ArtCrosshairs.Count => ArtIndexBase + i,
                "preset" when i < CrosshairPresets.All.Count => PresetIndexBase + i,
                _ => -1
            };
        }

        private Rectangle RandomButtonRect => new(Width - 36 - 30, 6, 26, 24);

        private int GetCardIndex(int x, int y)
        {
            int ly = y + _scrollY;
            int curY = PadTop;

            foreach (var (_, ids) in BuildSections())
            {
                curY += SectionH;
                int rows = (ids.Count + Cols - 1) / Cols;
                int endY = curY + rows * (CardSize + CardGap);

                if (ly >= curY && ly < endY)
                {
                    int row = (ly - curY) / (CardSize + CardGap);
                    int col = (x - PadX) / (CardSize + CardGap);
                    if (col >= 0 && col < Cols && x >= PadX && x < PadX + Cols * (CardSize + CardGap))
                    {
                        int idx = row * Cols + col;
                        if (idx < ids.Count) return ids[idx];
                    }
                    return -1;
                }
                curY = endY;
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

            // Random crosshair button (dice), pinned next to the close button.
            var rndRect = RandomButtonRect;
            using (var rndBg = new SolidBrush(_hoverIndex == RandomButtonId
                ? Color.FromArgb(90, SettingsForm.GetAccent())
                : Color.FromArgb(40, 80, 60, 140)))
            using (var rndPath = RoundRect(rndRect, 6))
                g.FillPath(rndBg, rndPath);
            using (var rndBrush = new SolidBrush(_hoverIndex == RandomButtonId
                ? Color.White : Color.FromArgb(200, 200, 190, 230)))
            {
                var sfRnd = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("⚄", _fontSection, rndBrush, rndRect, sfRnd);
            }

            g.TranslateTransform(0, -_scrollY);
            int curY = PadTop;
            int viewTop = _scrollY;
            int viewBottom = _scrollY + ClientSize.Height;

            using var secBrush = new SolidBrush(SettingsForm.GetAccent());
            using var lblBrush = new SolidBrush(Color.FromArgb(160, 180, 170, 210));
            using var lblSelBrush = new SolidBrush(Color.White);
            using var starBrush = new SolidBrush(Color.FromArgb(230, 255, 200, 60));
            var sfCenter = new StringFormat { Alignment = StringAlignment.Center };
            var sfLabel = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };

            foreach (var (title, ids) in BuildSections())
            {
                if (curY + SectionH >= viewTop && curY <= viewBottom)
                    g.DrawString(title, _fontSection, secBrush, PadX, curY + 4);
                curY += SectionH;

                int rows = (ids.Count + Cols - 1) / Cols;
                int endY = curY + rows * (CardSize + CardGap);

                // v2.4 viewport culling: with ~290 cards (presets + art) painting
                // everything on each hover repaint is wasteful — draw only rows
                // that intersect the visible area.
                if (endY < viewTop || curY > viewBottom)
                {
                    curY = endY;
                    continue;
                }

                for (int idx = 0; idx < ids.Count; idx++)
                {
                    int row = idx / Cols, col = idx % Cols;
                    int cx = PadX + col * (CardSize + CardGap);
                    int cy = curY + row * (CardSize + CardGap);
                    if (cy + CardSize < viewTop || cy > viewBottom) continue;

                    int id = ids[idx];
                    bool hover = _hoverIndex == id;

                    if (id >= PresetIndexBase)
                    {
                        var p = CrosshairPresets.All[id - PresetIndexBase];
                        bool selected = CrosshairPresets.Matches(_overlay, p);
                        DrawCard(g, cx, cy, CardSize, CardSize, hover, selected);
                        DrawPresetPreview(g, cx, cy, CardSize, p);
                        var labelRect = new RectangleF(cx + 2, cy + CardSize - 14, CardSize - 4, 12);
                        g.DrawString(p.Name, _fontLabel, selected ? lblSelBrush : lblBrush, labelRect, sfLabel);
                    }
                    else if (id >= ArtIndexBase)
                    {
                        int artIdx = id - ArtIndexBase;
                        bool selected = _overlay._style == OverlayForm.CrosshairStyle.Art && _overlay._artIndex == artIdx;
                        DrawCard(g, cx, cy, CardSize, CardSize, hover, selected);
                        ArtCrosshairs.Draw(g, artIdx, cx + CardSize / 2f, cy + CardSize / 2f - 4f, 24f, 255, true);
                        var labelRect = new RectangleF(cx + 2, cy + CardSize - 14, CardSize - 4, 12);
                        g.DrawString(ArtCrosshairs.GetName(artIdx), _fontLabel, selected ? lblSelBrush : lblBrush, labelRect, sfLabel);
                    }
                    else if (id < StandardStyles.Length)
                    {
                        bool selected = _overlay._style == StandardStyles[id];
                        DrawCard(g, cx, cy, CardSize, CardSize, hover, selected);
                        DrawCrosshairPreview(g, cx, cy, CardSize, StandardStyles[id]);
                        g.DrawString(StyleLabels[id], _fontLabel, selected ? lblSelBrush : lblBrush, cx + CardSize / 2, cy + CardSize - 14, sfCenter);
                    }
                    else
                    {
                        int comIdx = id - StandardStyles.Length;
                        if (comIdx < _communityImages.Count)
                        {
                            bool selected = _overlay._style == OverlayForm.CrosshairStyle.CustomImage
                                && _overlay._customImagePath == _communityImages[comIdx];
                            DrawCard(g, cx, cy, CardSize, CardSize, hover, selected);
                            DrawImagePreview(g, cx, cy, CardSize, _communityImages[comIdx]);
                            string name = Path.GetFileNameWithoutExtension(_communityImages[comIdx]);
                            if (name.Length > 10) name = name[..9] + "…";
                            g.DrawString(name, _fontLabel, lblBrush, cx + CardSize / 2, cy + CardSize - 14, sfCenter);
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
                            g.DrawString(Lang.GalleryAdd, _fontLabel, lblBrush3, cx + CardSize / 2, cy + CardSize - 14, sfCenter);
                        }
                    }

                    // Favorite star badge (right-click toggles)
                    var favKey = IdToFavKey(id);
                    if (favKey != null && _overlay._galleryFavorites.Contains(favKey))
                        g.DrawString("★", _fontLabel, starBrush, cx + CardSize - 16, cy + 4);
                }

                curY = endY;
            }

            _contentHeight = curY + PadTop;

            g.ResetTransform();

            // Scrollbar
            int viewH = ClientSize.Height;
            if (_contentHeight > viewH)
            {
                // Track
                var trackRect = new Rectangle(Width - SbRightPad - SbWidth, 4, SbWidth, viewH - 8);
                using (var trackBrush = new SolidBrush(Color.FromArgb(35, 255, 255, 255)))
                using (var tpath = RoundRect(trackRect, SbWidth / 2))
                    g.FillPath(trackBrush, tpath);

                float ratio = (float)viewH / _contentHeight;
                int barH = Math.Max(40, (int)(trackRect.Height * ratio));
                int maxScroll = _contentHeight - viewH;
                float scrollRatio = maxScroll > 0 ? (float)_scrollY / maxScroll : 0;
                int barY = trackRect.Y + (int)((trackRect.Height - barH) * scrollRatio);
                int alpha = _draggingSb ? 220 : 140;
                using var sbBrush = new SolidBrush(Color.FromArgb(alpha, SettingsForm.GetAccent()));
                using var sbPath = RoundRect(new Rectangle(trackRect.X, barY, SbWidth, barH), SbWidth / 2);
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
                case OverlayForm.CrosshairStyle.DoubleCircle:
                    DrawPreviewCircle(g, cx, cy, s, t, ow, brush, outBrush);
                    DrawPreviewCircle(g, cx, cy, s * 0.55f, t, ow, brush, outBrush);
                    break;
                case OverlayForm.CrosshairStyle.DashedCross:
                    DrawPreviewDashedCross(g, cx, cy, s, gap, t, ow, brush, outBrush);
                    break;
                case OverlayForm.CrosshairStyle.TriangleUp:
                    DrawPreviewTriangleUp(g, cx, cy, s, t, ow, brush, outBrush);
                    break;
                case OverlayForm.CrosshairStyle.SerifCross:
                    DrawPreviewSerifCross(g, cx, cy, s, gap, t, ow, brush, outBrush);
                    break;
            }
        }

        private void DrawImagePreview(Graphics g, int x, int y, int cardSize, string path)
        {
            try
            {
                int maxDim = cardSize - 24;
                if (!_thumbCache.TryGetValue(path, out var thumb))
                {
                    // Decode once, keep only the pre-scaled thumbnail.
                    using var img = new Bitmap(path);
                    float scale = Math.Min((float)maxDim / img.Width, (float)maxDim / img.Height);
                    int tw = Math.Max(1, (int)(img.Width * scale));
                    int th = Math.Max(1, (int)(img.Height * scale));
                    thumb = new Bitmap(img, tw, th);
                    _thumbCache[path] = thumb;
                }
                int px = x + (cardSize - thumb.Width) / 2;
                int py = y + (cardSize - thumb.Height) / 2 - 4;
                g.DrawImage(thumb, px, py, thumb.Width, thumb.Height);
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

        private void DrawPreviewDashedCross(Graphics g, int cx, int cy, float s, float gap, float t, float ow, Brush brush, SolidBrush outBrush)
        {
            float dash = Math.Max(2f, s / 4f);
            float step = dash * 1.8f;
            var segs = new (PointF a, PointF b)[]
            {
                // vertical top
                (new(cx, cy - gap - dash), new(cx, cy - gap - dash - dash)),
                (new(cx, cy - gap - dash - step), new(cx, cy - gap - dash - step - dash)),
                // vertical bottom
                (new(cx, cy + gap), new(cx, cy + gap + dash)),
                (new(cx, cy + gap + step), new(cx, cy + gap + step + dash)),
                // horizontal left
                (new(cx - gap - dash, cy), new(cx - gap - dash - dash, cy)),
                (new(cx - gap - dash - step, cy), new(cx - gap - dash - step - dash, cy)),
                // horizontal right
                (new(cx + gap, cy), new(cx + gap + dash, cy)),
                (new(cx + gap + step, cy), new(cx + gap + step + dash, cy)),
            };
            foreach (var (a, b) in segs)
            {
                using var op = new Pen(outBrush, t + ow * 2) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLine(op, a, b);
                using var p = new Pen(brush, t) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLine(p, a, b);
            }
        }

        private void DrawPreviewTriangleUp(Graphics g, int cx, int cy, float s, float t, float ow, Brush brush, SolidBrush outBrush)
        {
            float h = s * 0.9f;
            var pts = new PointF[] { new(cx - h, cy + h * 0.5f), new(cx, cy - h * 0.7f), new(cx + h, cy + h * 0.5f), new(cx - h, cy + h * 0.5f) };
            using var op = new Pen(outBrush, t + ow * 2) { LineJoin = LineJoin.Round };
            g.DrawLines(op, pts);
            using var p = new Pen(brush, t) { LineJoin = LineJoin.Round };
            g.DrawLines(p, pts);
        }

        private void DrawPreviewSerifCross(Graphics g, int cx, int cy, float s, float gap, float t, float ow, Brush brush, SolidBrush outBrush)
        {
            // Plain cross…
            DrawPreviewCross(g, cx, cy, s, gap, t, ow, brush, outBrush, false);
            // …with small serif ticks at each arm end.
            float tick = Math.Max(2f, s * 0.22f);
            var serifs = new (PointF a, PointF b)[]
            {
                // top arm
                (new(cx - tick, cy - gap - s), new(cx + tick, cy - gap - s)),
                // bottom arm
                (new(cx - tick, cy + gap + s), new(cx + tick, cy + gap + s)),
                // left arm
                (new(cx - gap - s, cy - tick), new(cx - gap - s, cy + tick)),
                // right arm
                (new(cx + gap + s, cy - tick), new(cx + gap + s, cy + tick)),
            };
            foreach (var (a, b) in serifs)
            {
                using var op = new Pen(outBrush, t + ow * 2) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLine(op, a, b);
                using var p = new Pen(brush, t) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLine(p, a, b);
            }
        }

        private void DrawPresetPreview(Graphics g, int x, int y, int cardSize, CrosshairPresets.Preset p)
        {
            int cx = x + cardSize / 2;
            int cy = y + cardSize / 2 - 4;
            // Scale preset size into ~16-22px visible range
            float s = Math.Clamp(p.Size * 0.75f, 8f, 22f);
            float gap = Math.Max(1f, p.Gap * 0.7f);
            float t = Math.Clamp(p.Thickness, 1, 4);
            float ow = p.ShowOutline ? p.OutlineWidth : 0f;

            using var brush = new SolidBrush(Color.FromArgb(235, p.Color));
            using var outBrush = new SolidBrush(Color.FromArgb(200, p.OutlineColor));

            // Optional glow halo
            if (p.GlowEnabled)
            {
                using var glow = new SolidBrush(Color.FromArgb(Math.Min(100, p.GlowAlpha), p.Color));
                int gs = (int)(s + p.GlowSize * 0.7f);
                g.FillEllipse(glow, cx - gs, cy - gs, gs * 2, gs * 2);
            }

            switch (p.Style)
            {
                case OverlayForm.CrosshairStyle.Cross:
                case OverlayForm.CrosshairStyle.Plus:
                    DrawPreviewCross(g, cx, cy, s, gap, t, ow, brush, outBrush, false);
                    break;
                case OverlayForm.CrosshairStyle.TShape:
                    DrawPreviewCross(g, cx, cy, s, gap, t, ow, brush, outBrush, true);
                    break;
                case OverlayForm.CrosshairStyle.Circle:
                    DrawPreviewCircle(g, cx, cy, s * 0.85f, t, ow, brush, outBrush);
                    break;
                case OverlayForm.CrosshairStyle.CrossWithCircle:
                    DrawPreviewCross(g, cx, cy, s, gap, t, ow, brush, outBrush, false);
                    DrawPreviewCircle(g, cx, cy, s * 0.9f, t, ow, brush, outBrush);
                    break;
                case OverlayForm.CrosshairStyle.Dot:
                    {
                        float r = Math.Max(3f, p.DotSize + s * 0.1f);
                        g.FillEllipse(outBrush, cx - r - 1, cy - r - 1, (r + 1) * 2, (r + 1) * 2);
                        g.FillEllipse(brush, cx - r, cy - r, r * 2, r * 2);
                    }
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
                    DrawPreviewCircle(g, cx, cy, s * 0.9f, t, ow, brush, outBrush);
                    g.FillEllipse(brush, cx - 2, cy - 2, 4, 4);
                    break;
                case OverlayForm.CrosshairStyle.SquareBrackets:
                    DrawPreviewBrackets(g, cx, cy, s * 0.85f, t, ow, brush, outBrush);
                    break;
                case OverlayForm.CrosshairStyle.Wings:
                    DrawPreviewWings(g, cx, cy, s, gap, t, ow, brush, outBrush);
                    break;
                case OverlayForm.CrosshairStyle.DoubleCircle:
                    DrawPreviewCircle(g, cx, cy, s * 0.85f, t, ow, brush, outBrush);
                    DrawPreviewCircle(g, cx, cy, s * 0.47f, t, ow, brush, outBrush);
                    break;
                case OverlayForm.CrosshairStyle.DashedCross:
                    DrawPreviewDashedCross(g, cx, cy, s, gap, t, ow, brush, outBrush);
                    break;
                case OverlayForm.CrosshairStyle.TriangleUp:
                    DrawPreviewTriangleUp(g, cx, cy, s, t, ow, brush, outBrush);
                    break;
                case OverlayForm.CrosshairStyle.SerifCross:
                    DrawPreviewSerifCross(g, cx, cy, s, gap, t, ow, brush, outBrush);
                    break;
            }

            if (p.ShowDot && p.Style != OverlayForm.CrosshairStyle.Dot)
            {
                float r = Math.Max(1.5f, p.DotSize);
                g.FillEllipse(brush, cx - r, cy - r, r * 2, r * 2);
            }
        }

        #endregion

        #region Mouse Handling

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_draggingSb)
            {
                HandleSbDrag(e.Y);
                return;
            }

            int oldHover = _hoverIndex;

            // Close button hit test
            if (e.X >= Width - 36 && e.X <= Width - 8 && e.Y >= 4 && e.Y <= 30)
                _hoverIndex = -999;
            else if (RandomButtonRect.Contains(e.X, e.Y))
                _hoverIndex = RandomButtonId;
            else
                _hoverIndex = GetCardIndex(e.X, e.Y);

            if (_hoverIndex != oldHover)
                Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_draggingSb)
            {
                _draggingSb = false;
                Invalidate();
            }
        }

        private void HandleSbDrag(int mouseY)
        {
            int viewH = ClientSize.Height;
            int trackTop = 4;
            int trackH = viewH - 8;
            int maxScroll = Math.Max(1, _contentHeight - viewH);
            float ratio = (float)viewH / _contentHeight;
            int barH = Math.Max(40, (int)(trackH * ratio));
            int thumbTop = mouseY - trackTop - _sbGrabOffset;
            int thumbTravel = Math.Max(1, trackH - barH);
            _scrollY = Math.Clamp((int)((float)thumbTop / thumbTravel * maxScroll), 0, maxScroll);
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            // v2.4: right-click toggles favorite on standard/art/preset cards.
            if (e.Button == MouseButtons.Right)
            {
                int id = GetCardIndex(e.X, e.Y);
                var favKey = IdToFavKey(id);
                if (favKey != null)
                {
                    if (!_overlay._galleryFavorites.Remove(favKey))
                        _overlay._galleryFavorites.Add(favKey);
                    _overlay.SaveSettings();
                    Invalidate();
                }
                return;
            }

            if (e.Button != MouseButtons.Left) return;

            // Scrollbar hit test (takes priority over cards on its strip)
            if (_contentHeight > ClientSize.Height && e.X >= Width - SbHitPad)
            {
                int viewH = ClientSize.Height;
                int trackTop = 4;
                int trackH = viewH - 8;
                float ratio = (float)viewH / _contentHeight;
                int barH = Math.Max(40, (int)(trackH * ratio));
                int maxScroll = _contentHeight - viewH;
                float scrollRatio = maxScroll > 0 ? (float)_scrollY / maxScroll : 0;
                int barY = trackTop + (int)((trackH - barH) * scrollRatio);

                _draggingSb = true;
                if (e.Y >= barY && e.Y < barY + barH)
                    _sbGrabOffset = e.Y - barY;
                else
                    _sbGrabOffset = barH / 2;
                HandleSbDrag(e.Y);
                Invalidate();
                return;
            }

            // Close
            if (_hoverIndex == -999)
            {
                Close();
                return;
            }

            // Random crosshair
            if (_hoverIndex == RandomButtonId)
            {
                ApplyRandom();
                return;
            }

            // v2.5: drag the window by its title strip (everything above the cards
            // that isn't a button). Standard Win32 trick: pretend the click hit the
            // caption — Windows then runs its native, perfectly smooth move loop.
            if (e.Y < PadTop)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, IntPtr.Zero);
                return;
            }

            if (_hoverIndex < 0) return;

            // v2.4: art designs
            if (_hoverIndex >= ArtIndexBase && _hoverIndex < PresetIndexBase)
            {
                int artIdx = _hoverIndex - ArtIndexBase;
                if (artIdx < ArtCrosshairs.Count)
                {
                    _overlay._style = OverlayForm.CrosshairStyle.Art;
                    _overlay._artIndex = artIdx;
                    _overlay._needsStaticRender = true;
                    _overlay.SaveSettings();
                    Invalidate();
                }
                return;
            }

            if (_hoverIndex >= PresetIndexBase)
            {
                int presetIdx = _hoverIndex - PresetIndexBase;
                var presets = CrosshairPresets.All;
                if (presetIdx >= 0 && presetIdx < presets.Count)
                {
                    CrosshairPresets.Apply(_overlay, presets[presetIdx]);
                    Invalidate();
                }
                return;
            }

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

        private void ApplyRandom()
        {
            var rnd = new Random();
            int total = StandardStyles.Length + ArtCrosshairs.Count + CrosshairPresets.All.Count;
            int pick = rnd.Next(total);

            if (pick < StandardStyles.Length)
            {
                _overlay._style = StandardStyles[pick];
            }
            else if (pick < StandardStyles.Length + ArtCrosshairs.Count)
            {
                _overlay._style = OverlayForm.CrosshairStyle.Art;
                _overlay._artIndex = pick - StandardStyles.Length;
            }
            else
            {
                CrosshairPresets.Apply(_overlay, CrosshairPresets.All[pick - StandardStyles.Length - ArtCrosshairs.Count]);
                Invalidate();
                return; // Apply() already saves + rerenders
            }

            _overlay._needsStaticRender = true;
            _overlay.SaveSettings();
            Invalidate();
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
                foreach (var thumb in _thumbCache.Values) thumb.Dispose();
                _thumbCache.Clear();
            }
            base.Dispose(disposing);
        }
    }
}
