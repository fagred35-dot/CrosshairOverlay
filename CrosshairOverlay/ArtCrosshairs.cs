using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace CrosshairOverlay
{
    /// <summary>
    /// v2.4: "Авторские" — hand-designed vector crosshairs (150 since v2.6, 108 animated).
    /// Each design is a small vector program (lines / polygons / circles /
    /// arcs / ellipses) authored in unit space (~[-1.15, 1.15]) with its own
    /// palette. Rendered with an outline pass behind for visibility on any
    /// background. Data lives in ArtCrosshairs.Designs.cs (generated).
    /// </summary>
    internal static partial class ArtCrosshairs
    {
        // Op kinds
        private const byte K_LINE = 0;   // p = x1,y1,x2,y2
        private const byte K_PLINE = 1;  // p = x1,y1,...
        private const byte K_PGON = 2;   // p = x1,y1,... (closed stroke)
        private const byte K_FPGON = 3;  // p = x1,y1,... (fill)
        private const byte K_CIRC = 4;   // p = cx,cy,r
        private const byte K_FCIRC = 5;  // p = cx,cy,r (fill)
        private const byte K_ARC = 6;    // p = cx,cy,r,startDeg,sweepDeg
        private const byte K_ELL = 7;    // p = cx,cy,rx,ry,rotDeg (full ellipse stroke)
        private const byte K_FELL = 8;   // p = cx,cy,rx,ry,rotDeg (full ellipse fill)

        // Total extra stroke width of the outline pass, in pixels.
        private const float OutlineAdd = 2.6f;

        internal readonly struct Op
        {
            public readonly byte Kind;
            public readonly byte Slot;   // 0..2 -> C1..C3
            public readonly byte Grp;    // v2.6: animation group, 0 = static
            public readonly float W;     // stroke width in unit space
            public readonly float[] P;
            public Op(byte kind, byte slot, float w, float[] p, byte grp = 0)
            { Kind = kind; Slot = slot; W = w; P = p; Grp = grp; }
        }

        // v2.6: animation kinds. Each Anim drives one op group of a design.
        internal const byte A_SPIN = 1;    // A = degrees/sec (sign = direction)
        internal const byte A_PULSE = 2;   // A = cycles/sec, B = scale amplitude (e.g. 0.12)
        internal const byte A_FADE = 3;    // A = cycles/sec, B = alpha dip 0..1 (breathing)
        internal const byte A_WOBBLE = 4;  // A = cycles/sec, B = rocking amplitude in degrees

        internal readonly struct Anim
        {
            public readonly byte Grp, Kind;
            public readonly float A, B, Phase;
            public Anim(byte grp, byte kind, float a, float b = 0f, float phase = 0f)
            { Grp = grp; Kind = kind; A = a; B = b; Phase = phase; }
        }

        internal sealed class ArtDef
        {
            public readonly string Name;
            public readonly Color C1, C2, C3, Outline;
            public readonly Op[] Ops;
            public readonly Anim[] Anims;   // v2.6, empty = static design
            public ArtDef(string name, Color c1, Color c2, Color c3, Color outline, Op[] ops, Anim[]? anims = null)
            { Name = name; C1 = c1; C2 = c2; C3 = c3; Outline = outline; Ops = ops; Anims = anims ?? System.Array.Empty<Anim>(); }
        }

        private static ArtDef[]? _all;
        internal static ArtDef[] All => _all ??= BuildDesigns();
        internal static int Count => All.Length;

        /// <summary>v2.6: true if the design has at least one animation track.</summary>
        internal static bool IsAnimated(int index)
        {
            var a = All;
            return index >= 0 && index < a.Length && a[index].Anims.Length > 0;
        }

        internal static string GetName(int index)
        {
            var a = All;
            if (index < 0 || index >= a.Length) return "?";
            return a[index].Name;
        }

        /// <summary>Draw design <paramref name="index"/> centered at (cx, cy).
        /// <paramref name="scale"/> = pixels per design unit (use crosshair size).</summary>
        internal static void Draw(Graphics g, int index, float cx, float cy, float scale, int alpha, bool withOutline = true, float t = 0f)
        {
            var a = All;
            if (a.Length == 0) return;
            index = Math.Clamp(index, 0, a.Length - 1);
            var d = a[index];
            var prev = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // v2.6: evaluate animation tracks once per frame (groups 1..7).
            Span<float> rot = stackalloc float[8];
            Span<float> scl = stackalloc float[8];
            Span<float> amul = stackalloc float[8];
            for (int i = 0; i < 8; i++) { scl[i] = 1f; amul[i] = 1f; }
            foreach (var an in d.Anims)
            {
                if (an.Grp == 0 || an.Grp > 7) continue;
                float w = 2f * MathF.PI * an.A * t + an.Phase;
                switch (an.Kind)
                {
                    case A_SPIN: rot[an.Grp] += an.A * t; break;
                    case A_PULSE: scl[an.Grp] *= 1f + an.B * MathF.Sin(w); break;
                    case A_FADE: amul[an.Grp] *= 1f - an.B * (0.5f + 0.5f * MathF.Sin(w)); break;
                    case A_WOBBLE: rot[an.Grp] += an.B * MathF.Sin(w); break;
                }
            }

            if (withOutline) RenderPass(g, d, cx, cy, scale, alpha, true, rot, scl, amul);
            RenderPass(g, d, cx, cy, scale, alpha, false, rot, scl, amul);
            g.SmoothingMode = prev;
        }

        private static void RenderPass(Graphics g, ArtDef d, float cx, float cy, float scale, int alpha, bool outline,
            ReadOnlySpan<float> rot, ReadOnlySpan<float> scl, ReadOnlySpan<float> amul)
        {
            foreach (var op in d.Ops)
            {
                Color baseColor = outline
                    ? d.Outline
                    : op.Slot switch { 0 => d.C1, 1 => d.C2, _ => d.C3 };

                float ocx = cx, ocy = cy;
                GraphicsState? st = null;
                int opAlpha = alpha;
                if (op.Grp > 0 && op.Grp < 8)
                {
                    // Animated group: rotate/scale around the design center.
                    if (rot[op.Grp] != 0f || scl[op.Grp] != 1f)
                    {
                        st = g.Save();
                        g.TranslateTransform(cx, cy);
                        if (rot[op.Grp] != 0f) g.RotateTransform(rot[op.Grp]);
                        if (scl[op.Grp] != 1f) g.ScaleTransform(scl[op.Grp], scl[op.Grp]);
                        ocx = 0f; ocy = 0f;
                    }
                    opAlpha = (int)(alpha * amul[op.Grp]);
                }
                Color col = Color.FromArgb(Math.Clamp(opAlpha, 0, 255), baseColor);
                DrawOp(g, op, col, ocx, ocy, scale, outline);
                if (st != null) g.Restore(st);
            }
        }

        private static void DrawOp(Graphics g, in Op op, Color col, float cx, float cy, float scale, bool outline)
        {
            {
                float w = op.W * scale + (outline ? OutlineAdd : 0f);
                if (w < 1f) w = 1f;
                var p = op.P;

                switch (op.Kind)
                {
                    case K_LINE:
                        using (var pen = MakePen(col, w))
                            g.DrawLine(pen, cx + p[0] * scale, cy + p[1] * scale, cx + p[2] * scale, cy + p[3] * scale);
                        break;

                    case K_PLINE:
                        using (var pen = MakePen(col, w))
                            g.DrawLines(pen, ToPoints(p, cx, cy, scale));
                        break;

                    case K_PGON:
                        using (var pen = MakePen(col, w))
                            g.DrawPolygon(pen, ToPoints(p, cx, cy, scale));
                        break;

                    case K_FPGON:
                        {
                            var pts = ToPoints(p, cx, cy, scale);
                            if (outline)
                            {
                                using var pen = MakePen(col, OutlineAdd);
                                g.DrawPolygon(pen, pts);
                            }
                            else
                            {
                                using var br = new SolidBrush(col);
                                g.FillPolygon(br, pts);
                            }
                        }
                        break;

                    case K_CIRC:
                        {
                            float r = p[2] * scale;
                            using var pen = MakePen(col, w);
                            g.DrawEllipse(pen, cx + p[0] * scale - r, cy + p[1] * scale - r, r * 2, r * 2);
                        }
                        break;

                    case K_FCIRC:
                        {
                            float r = p[2] * scale + (outline ? OutlineAdd / 2f : 0f);
                            using var br = new SolidBrush(col);
                            g.FillEllipse(br, cx + p[0] * scale - r, cy + p[1] * scale - r, r * 2, r * 2);
                        }
                        break;

                    case K_ARC:
                        {
                            float r = p[2] * scale;
                            if (r > 0.1f)
                            {
                                using var pen = MakePen(col, w);
                                g.DrawArc(pen, cx + p[0] * scale - r, cy + p[1] * scale - r, r * 2, r * 2, p[3], p[4]);
                            }
                        }
                        break;

                    case K_ELL:
                    case K_FELL:
                        {
                            float rx = p[2] * scale, ry = p[3] * scale;
                            if (rx < 0.1f || ry < 0.1f) break;
                            var state = g.Save();
                            g.TranslateTransform(cx + p[0] * scale, cy + p[1] * scale);
                            if (p[4] != 0f) g.RotateTransform(p[4]);
                            if (op.Kind == K_ELL)
                            {
                                using var pen = MakePen(col, w);
                                g.DrawEllipse(pen, -rx, -ry, rx * 2, ry * 2);
                            }
                            else if (outline)
                            {
                                float ex = rx + OutlineAdd / 2f, ey = ry + OutlineAdd / 2f;
                                using var br = new SolidBrush(col);
                                g.FillEllipse(br, -ex, -ey, ex * 2, ey * 2);
                            }
                            else
                            {
                                using var br = new SolidBrush(col);
                                g.FillEllipse(br, -rx, -ry, rx * 2, ry * 2);
                            }
                            g.Restore(state);
                        }
                        break;
                }
            }
        }

        private static Pen MakePen(Color c, float w)
        {
            var pen = new Pen(c, w)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };
            return pen;
        }

        private static PointF[] ToPoints(float[] p, float cx, float cy, float scale)
        {
            var pts = new PointF[p.Length / 2];
            for (int i = 0; i < pts.Length; i++)
                pts[i] = new PointF(cx + p[i * 2] * scale, cy + p[i * 2 + 1] * scale);
            return pts;
        }
    }
}
