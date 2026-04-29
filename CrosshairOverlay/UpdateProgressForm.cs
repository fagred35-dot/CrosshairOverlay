using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Windows.Forms;

namespace CrosshairOverlay
{
    /// <summary>
    /// Small modal dialog showing download progress + a Cancel button.
    /// Visual style matches the rest of the dark/violet UI.
    /// </summary>
    internal sealed class UpdateProgressForm : Form
    {
        private readonly CancellationTokenSource _cts = new();
        private long _received;
        private long _total = -1;
        private string _status = "";
        private bool _done;

        public CancellationToken CancellationToken => _cts.Token;

        public UpdateProgressForm(string title)
        {
            Text = title;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(420, 140);
            BackColor = Color.FromArgb(12, 6, 24);
            DoubleBuffered = true;
            ShowInTaskbar = false;
            TopMost = true;
            KeyPreview = true;
        }

        public void SetStatus(string status)
        {
            _status = status;
            if (IsHandleCreated) BeginInvoke(new Action(Invalidate));
        }

        public void ReportProgress(long received, long total)
        {
            _received = received;
            _total = total;
            if (IsHandleCreated) BeginInvoke(new Action(Invalidate));
        }

        public void MarkDone()
        {
            _done = true;
            if (IsHandleCreated) BeginInvoke(new Action(Close));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            g.Clear(Color.FromArgb(12, 6, 24));

            using var border = new Pen(Color.FromArgb(60, SettingsForm.GetAccent()), 2f);
            g.DrawRectangle(border, 1, 1, Width - 3, Height - 3);

            using var titleFont = new Font("Segoe UI", 11f, FontStyle.Bold);
            using var bodyFont = new Font("Segoe UI", 9f);
            using var titleBrush = new SolidBrush(Color.FromArgb(235, 228, 245));
            using var bodyBrush = new SolidBrush(Color.FromArgb(190, 200, 220));

            g.DrawString(Text, titleFont, titleBrush, 18, 14);
            g.DrawString(_status, bodyFont, bodyBrush, 18, 38);

            // Progress bar
            int barX = 18, barY = 70, barW = ClientSize.Width - 36, barH = 12;
            using (var trackBrush = new SolidBrush(Color.FromArgb(35, 255, 255, 255)))
            using (var trackPath = RoundRect(new Rectangle(barX, barY, barW, barH), barH / 2))
                g.FillPath(trackBrush, trackPath);

            float progress = _total > 0 ? (float)_received / _total : 0f;
            progress = Math.Clamp(progress, 0f, 1f);
            int fillW = (int)(barW * progress);
            if (fillW > 4)
            {
                var fillRect = new Rectangle(barX, barY, fillW, barH);
                using var fillPath = RoundRect(fillRect, barH / 2);
                using var fill = new LinearGradientBrush(fillRect,
                    SettingsForm.GetAccent(),
                    Color.FromArgb(180, 120, 255), 0f);
                g.FillPath(fill, fillPath);
            }

            // Bytes label
            using var smallFont = new Font("Segoe UI", 8f);
            using var dimBrush = new SolidBrush(Color.FromArgb(160, 180, 170, 210));
            string bytes = _total > 0
                ? $"{FormatBytes(_received)} / {FormatBytes(_total)}  ({progress * 100f:F1}%)"
                : FormatBytes(_received);
            g.DrawString(bytes, smallFont, dimBrush, 18, 88);

            // Cancel button
            var btnRect = new Rectangle(ClientSize.Width - 110, ClientSize.Height - 38, 90, 26);
            using var btnPath = RoundRect(btnRect, 6);
            using var btnBrush = new SolidBrush(_cancelHover
                ? Color.FromArgb(220, 100, 60, 60)
                : Color.FromArgb(180, 60, 40, 80));
            g.FillPath(btnBrush, btnPath);
            using var btnPen = new Pen(Color.FromArgb(120, 200, 200, 220), 1f);
            g.DrawPath(btnPen, btnPath);
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using var btnTextBrush = new SolidBrush(Color.FromArgb(235, 235, 245));
            g.DrawString(Lang.IsRussian ? "Отмена" : "Cancel", bodyFont, btnTextBrush, btnRect, sf);
        }

        private bool _cancelHover;

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool hover = HitCancel(e.X, e.Y);
            if (hover != _cancelHover) { _cancelHover = hover; Invalidate(); }
            Cursor = hover ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && HitCancel(e.X, e.Y))
                Cancel();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) Cancel();
        }

        private void Cancel()
        {
            if (_done) return;
            try { _cts.Cancel(); } catch { }
            Close();
        }

        private bool HitCancel(int x, int y)
        {
            var btn = new Rectangle(ClientSize.Width - 110, ClientSize.Height - 38, 90, 26);
            return btn.Contains(x, y);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _cts.Dispose();
            base.Dispose(disposing);
        }

        private static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            int d = Math.Max(2, radius * 2);
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static string FormatBytes(long n)
        {
            if (n < 0) return "?";
            string[] u = { "B", "KB", "MB", "GB" };
            double v = n;
            int i = 0;
            while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
            return v.ToString("0.##") + " " + u[i];
        }
    }
}
