using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CrosshairOverlay
{
    internal class HotkeyCaptureForm : Form
    {
        public uint ResultMod { get; private set; }
        public uint ResultVk { get; private set; }
        public bool Captured { get; private set; }

        private readonly string _hotkeyName;
        private string _displayText = Lang.HotkeyCapPrompt;
        private bool _waiting = true;

        private static readonly Color BgColor = Color.FromArgb(18, 10, 30);
        private static readonly Color BorderColor = Color.FromArgb(130, 80, 220);
        private static readonly Color TextColor = Color.FromArgb(235, 228, 245);
        private static readonly Color DimColor = Color.FromArgb(130, 120, 155);

        public HotkeyCaptureForm(string hotkeyName, uint currentMod, uint currentVk)
        {
            _hotkeyName = hotkeyName;
            ResultMod = currentMod;
            ResultVk = currentVk;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(340, 160);
            BackColor = BgColor;
            TopMost = true;
            ShowInTaskbar = false;
            KeyPreview = true;
            DoubleBuffered = true;

            KeyDown += OnKeyDown;
            Paint += OnPaint;
            MouseDown += (_, e) =>
            {
                if (e.Button == MouseButtons.Right)
                {
                    // Right-click = clear hotkey
                    ResultMod = 0;
                    ResultVk = 0;
                    Captured = true;
                    DialogResult = DialogResult.OK;
                    Close();
                }
            };
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            // Ensure the form receives keyboard focus so KeyDown fires.
            try { Activate(); Focus(); } catch { }
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;

            // Skip modifier-only presses
            if (e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.ShiftKey ||
                e.KeyCode == Keys.Menu || e.KeyCode == Keys.LWin || e.KeyCode == Keys.RWin)
            {
                // Show current modifiers while held
                uint mod = 0;
                if (e.Control) mod |= 0x0002;
                if (e.Shift) mod |= 0x0004;
                if (e.Alt) mod |= 0x0001;
                _displayText = BuildModString(mod) + "...";
                Invalidate();
                return;
            }

            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
                return;
            }

            uint m = 0;
            if (e.Control) m |= 0x0002;
            if (e.Shift) m |= 0x0004;
            if (e.Alt) m |= 0x0001;

            ResultMod = m;
            ResultVk = (uint)e.KeyCode;
            Captured = true;

            _displayText = OverlayForm.HotkeyToString(ResultMod, ResultVk);
            _waiting = false;
            Invalidate();

            // Auto-close after brief delay
            var t = new System.Windows.Forms.Timer { Interval = 400 };
            t.Tick += (_, _) => { t.Stop(); t.Dispose(); DialogResult = DialogResult.OK; Close(); };
            t.Start();
        }

        private static string BuildModString(uint mod)
        {
            string s = "";
            if ((mod & 0x0002) != 0) s += "Ctrl+";
            if ((mod & 0x0004) != 0) s += "Shift+";
            if ((mod & 0x0001) != 0) s += "Alt+";
            return s;
        }

        private void OnPaint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // Border
            using var pen = new Pen(BorderColor, 2f);
            g.DrawRoundedRectangle(pen, new Rectangle(1, 1, Width - 3, Height - 3), 12);

            // Title
            using var titleFont = new Font("Segoe UI", 11f, FontStyle.Bold);
            using var titleBrush = new SolidBrush(TextColor);
            string title = Lang.HotkeyCapTitle(_hotkeyName);
            var titleSize = g.MeasureString(title, titleFont);
            g.DrawString(title, titleFont, titleBrush, (Width - titleSize.Width) / 2, 18);

            // Display text
            using var mainFont = new Font("Segoe UI", 16f, FontStyle.Bold);
            using var mainBrush = new SolidBrush(_waiting ? DimColor : BorderColor);
            var mainSize = g.MeasureString(_displayText, mainFont);
            g.DrawString(_displayText, mainFont, mainBrush, (Width - mainSize.Width) / 2, 58);

            // Hint
            using var hintFont = new Font("Segoe UI", 8f);
            using var hintBrush = new SolidBrush(DimColor);
            string hint = Lang.HotkeyCapHint;
            var hintSize = g.MeasureString(hint, hintFont);
            g.DrawString(hint, hintFont, hintBrush, (Width - hintSize.Width) / 2, Height - 30);
        }
    }

    internal static class GraphicsRoundedRectExtensions
    {
        public static void DrawRoundedRectangle(this Graphics g, Pen pen, Rectangle rect, int radius)
        {
            using var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            g.DrawPath(pen, path);
        }
    }
}
