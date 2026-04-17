using System;
using System.Collections.Generic;
using System.Drawing;

namespace CrosshairOverlay
{
    /// <summary>
    /// Data-driven crosshair presets. Each preset is a full bundle of settings
    /// that can be applied to OverlayForm in one click.
    /// </summary>
    internal static class CrosshairPresets
    {
        public sealed class Preset
        {
            public string Name = "";
            public OverlayForm.CrosshairStyle Style;
            public int Size = 20;
            public int Thickness = 2;
            public int Gap = 4;
            public int DotSize = 2;
            public bool ShowDot = true;
            public bool ShowOutline = true;
            public bool UseGradient;
            public bool DotPulse;
            public bool Rainbow;
            public bool Spin;
            public bool GlowEnabled;
            public bool ShowShadow;
            public Color Color = Color.FromArgb(0, 255, 0);
            public Color Color2 = Color.FromArgb(0, 200, 255);
            public Color OutlineColor = Color.Black;
            public float OutlineWidth = 1f;
            public float Rotation = 0f;
            public float SpinSpeed = 2f;
            public int GlowSize = 6;
            public int GlowAlpha = 80;
        }

        // ── 25 curated base templates × 8 color variants = 200 presets ──
        private static readonly (string name, Action<Preset> cfg)[] _templates =
        {
            ("Classic Cross",    p => { p.Style = OverlayForm.CrosshairStyle.Cross; p.Size = 20; p.Thickness = 2; p.Gap = 4; }),
            ("Mini Cross",       p => { p.Style = OverlayForm.CrosshairStyle.Cross; p.Size = 8;  p.Thickness = 1; p.Gap = 1; p.ShowDot = false; }),
            ("Giant Cross",      p => { p.Style = OverlayForm.CrosshairStyle.Cross; p.Size = 40; p.Thickness = 4; p.Gap = 8; }),
            ("CS:GO Default",    p => { p.Style = OverlayForm.CrosshairStyle.Cross; p.Size = 14; p.Thickness = 1; p.Gap = 3; p.ShowDot = false; }),
            ("Valorant Small",   p => { p.Style = OverlayForm.CrosshairStyle.Cross; p.Size = 10; p.Thickness = 2; p.Gap = 2; p.ShowDot = false; }),
            ("Valorant Big",     p => { p.Style = OverlayForm.CrosshairStyle.Cross; p.Size = 24; p.Thickness = 3; p.Gap = 6; p.DotSize = 2; }),
            ("Pro Dot",          p => { p.Style = OverlayForm.CrosshairStyle.Dot;   p.Size = 8;  p.ShowDot = false; }),
            ("Pulsing Dot",      p => { p.Style = OverlayForm.CrosshairStyle.Dot;   p.Size = 12; p.DotPulse = true; }),
            ("Sniper Ring",      p => { p.Style = OverlayForm.CrosshairStyle.Crosshairs; p.Size = 28; p.Thickness = 1; p.Gap = 8; }),
            ("T-Bar",            p => { p.Style = OverlayForm.CrosshairStyle.TShape; p.Size = 18; p.Thickness = 2; p.Gap = 5; }),
            ("X-Hairs",          p => { p.Style = OverlayForm.CrosshairStyle.XShape; p.Size = 16; p.Thickness = 2; p.Gap = 3; }),
            ("Spinning X",       p => { p.Style = OverlayForm.CrosshairStyle.XShape; p.Size = 18; p.Thickness = 2; p.Gap = 3; p.Spin = true; p.SpinSpeed = 3f; }),
            ("Diamond Lock",     p => { p.Style = OverlayForm.CrosshairStyle.Diamond; p.Size = 18; p.Thickness = 2; }),
            ("Apex Triangle",    p => { p.Style = OverlayForm.CrosshairStyle.TriangleDown; p.Size = 22; p.Thickness = 2; }),
            ("Arrow Up",         p => { p.Style = OverlayForm.CrosshairStyle.Arrow; p.Size = 18; p.Thickness = 2; }),
            ("Wings",            p => { p.Style = OverlayForm.CrosshairStyle.Wings; p.Size = 18; p.Thickness = 2; p.Gap = 5; }),
            ("Plus Thin",        p => { p.Style = OverlayForm.CrosshairStyle.Plus;  p.Size = 14; p.Thickness = 1; p.Gap = 2; }),
            ("Plus Bold",        p => { p.Style = OverlayForm.CrosshairStyle.Plus;  p.Size = 22; p.Thickness = 4; p.Gap = 5; }),
            ("Brackets",         p => { p.Style = OverlayForm.CrosshairStyle.SquareBrackets; p.Size = 16; p.Thickness = 2; }),
            ("Chevron",          p => { p.Style = OverlayForm.CrosshairStyle.Chevron; p.Size = 20; p.Thickness = 2; p.Gap = 4; }),
            ("Cross + Circle",   p => { p.Style = OverlayForm.CrosshairStyle.CrossWithCircle; p.Size = 20; p.Thickness = 2; p.Gap = 4; }),
            ("Glow Cross",       p => { p.Style = OverlayForm.CrosshairStyle.Cross; p.Size = 18; p.Thickness = 2; p.Gap = 3; p.GlowEnabled = true; p.GlowSize = 8; p.GlowAlpha = 100; }),
            ("Rainbow Cross",    p => { p.Style = OverlayForm.CrosshairStyle.Cross; p.Size = 20; p.Thickness = 2; p.Gap = 4; p.Rainbow = true; }),
            ("Gradient Thick",   p => { p.Style = OverlayForm.CrosshairStyle.Cross; p.Size = 22; p.Thickness = 3; p.Gap = 5; p.UseGradient = true; }),
            ("Shadow Cross",     p => { p.Style = OverlayForm.CrosshairStyle.Cross; p.Size = 22; p.Thickness = 3; p.Gap = 5; p.ShowShadow = true; }),
        };

        private static readonly (string name, Color primary, Color secondary)[] _palette =
        {
            ("Green",  Color.FromArgb(0,   255, 80),  Color.FromArgb(0,   180, 255)),
            ("Cyan",   Color.FromArgb(0,   255, 255), Color.FromArgb(80,  150, 255)),
            ("Red",    Color.FromArgb(255, 40,  60),  Color.FromArgb(255, 120, 40)),
            ("Pink",   Color.FromArgb(255, 80,  180), Color.FromArgb(180, 80,  255)),
            ("Yellow", Color.FromArgb(255, 230, 20),  Color.FromArgb(255, 120, 20)),
            ("White",  Color.FromArgb(240, 240, 255), Color.FromArgb(180, 200, 255)),
            ("Purple", Color.FromArgb(180, 80,  255), Color.FromArgb(80,  120, 255)),
            ("Orange", Color.FromArgb(255, 140, 20),  Color.FromArgb(255, 50,  80)),
        };

        private static List<Preset>? _cache;
        public static List<Preset> All => _cache ??= Build();

        private static List<Preset> Build()
        {
            var list = new List<Preset>(_templates.Length * _palette.Length);
            foreach (var (tname, cfg) in _templates)
            {
                foreach (var (cname, c1, c2) in _palette)
                {
                    var p = new Preset();
                    cfg(p);
                    p.Color = c1;
                    p.Color2 = c2;
                    p.Name = $"{tname} · {cname}";
                    list.Add(p);
                }
            }
            return list;
        }

        public static void Apply(OverlayForm o, Preset p)
        {
            if (o == null || p == null) return;
            o._style = p.Style;
            o._size = p.Size;
            o._thickness = p.Thickness;
            o._gap = p.Gap;
            o._dotSize = p.DotSize;
            o._showDot = p.ShowDot;
            o._showOutline = p.ShowOutline;
            o._useGradient = p.UseGradient;
            o._dotPulse = p.DotPulse;
            o._rainbowMode = p.Rainbow;
            o._spin = p.Spin;
            o._spinSpeed = p.SpinSpeed;
            o._glowEnabled = p.GlowEnabled;
            o._glowSize = p.GlowSize;
            o._glowAlpha = p.GlowAlpha;
            o._showShadow = p.ShowShadow;
            o._crossColor = p.Color;
            o._crossColor2 = p.Color2;
            o._outlineColor = p.OutlineColor;
            o._outlineWidth = p.OutlineWidth;
            o._rotation = p.Rotation;
            o._needsStaticRender = true;
            o.SaveSettings();
        }

        /// <summary>True if overlay currently matches this preset (approximate).</summary>
        public static bool Matches(OverlayForm o, Preset p)
        {
            if (o._style != p.Style) return false;
            if (o._size != p.Size || o._thickness != p.Thickness || o._gap != p.Gap) return false;
            var oc = o._crossColor;
            if (oc.ToArgb() != p.Color.ToArgb()) return false;
            return true;
        }
    }
}
