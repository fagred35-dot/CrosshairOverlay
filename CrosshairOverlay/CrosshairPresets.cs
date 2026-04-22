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

        // ── Curated "Signature" presets ──
        // Each combines several effects (glow / gradient / shadow / spin / outline / dashed …)
        // into a single hand-tuned crosshair. Unlike the base templates these are NOT
        // multiplied by the palette — they ship with their own color story.
        private static readonly Action<Preset>[] _signatures =
        {
            p => { // Phantom Lock — cross+circle, soft cyan→purple glow
                p.Name = "★ Phantom Lock";
                p.Style = OverlayForm.CrosshairStyle.CrossWithCircle;
                p.Size = 22; p.Thickness = 2; p.Gap = 5; p.DotSize = 2;
                p.Color = Color.FromArgb(120, 200, 255);
                p.Color2 = Color.FromArgb(180, 120, 255);
                p.UseGradient = true; p.GlowEnabled = true; p.GlowSize = 10; p.GlowAlpha = 70;
                p.OutlineColor = Color.FromArgb(20, 10, 30); p.OutlineWidth = 1.2f;
            },
            p => { // Viper Fang — razor-thin X with neon halo
                p.Name = "★ Viper Fang";
                p.Style = OverlayForm.CrosshairStyle.XShape;
                p.Size = 20; p.Thickness = 2; p.Gap = 3; p.DotSize = 2;
                p.Color = Color.FromArgb(60, 255, 120);
                p.Color2 = Color.FromArgb(0, 180, 80);
                p.UseGradient = true; p.GlowEnabled = true; p.GlowSize = 7; p.GlowAlpha = 90;
                p.DotPulse = true;
            },
            p => { // Halo Ring — double circle with central dot, magenta accent
                p.Name = "★ Halo Ring";
                p.Style = OverlayForm.CrosshairStyle.DoubleCircle;
                p.Size = 22; p.Thickness = 2; p.DotSize = 3; p.Gap = 4;
                p.Color = Color.FromArgb(255, 60, 200);
                p.Color2 = Color.FromArgb(140, 0, 255);
                p.UseGradient = true; p.GlowEnabled = true; p.GlowSize = 6; p.GlowAlpha = 60;
                p.OutlineColor = Color.FromArgb(20, 0, 20);
            },
            p => { // Orion's Eye — sniper crosshairs, ice-blue gradient + shadow
                p.Name = "★ Orion's Eye";
                p.Style = OverlayForm.CrosshairStyle.Crosshairs;
                p.Size = 26; p.Thickness = 1; p.Gap = 7; p.DotSize = 2;
                p.Color = Color.FromArgb(180, 235, 255);
                p.Color2 = Color.FromArgb(40, 120, 220);
                p.UseGradient = true; p.ShowShadow = true;
                p.OutlineColor = Color.FromArgb(10, 20, 40); p.OutlineWidth = 1.4f;
            },
            p => { // Nebula Spin — spinning diamond, pink→violet gradient
                p.Name = "★ Nebula Spin";
                p.Style = OverlayForm.CrosshairStyle.Diamond;
                p.Size = 20; p.Thickness = 2; p.DotSize = 2;
                p.Color = Color.FromArgb(255, 110, 200);
                p.Color2 = Color.FromArgb(130, 60, 255);
                p.UseGradient = true; p.Spin = true; p.SpinSpeed = 1.5f;
                p.GlowEnabled = true; p.GlowSize = 5; p.GlowAlpha = 60;
            },
            p => { // Reaper Scythe — fast-spinning chevron, neon red
                p.Name = "★ Reaper Scythe";
                p.Style = OverlayForm.CrosshairStyle.Chevron;
                p.Size = 22; p.Thickness = 3; p.Gap = 4; p.DotSize = 0; p.ShowDot = false;
                p.Color = Color.FromArgb(255, 50, 70);
                p.Color2 = Color.FromArgb(255, 150, 40);
                p.UseGradient = true; p.Spin = true; p.SpinSpeed = 4f;
                p.OutlineColor = Color.FromArgb(30, 0, 0); p.OutlineWidth = 1.4f;
                p.ShowShadow = true;
            },
            p => { // Sigma Recoil — dashed cross, electric yellow glow
                p.Name = "★ Sigma Recoil";
                p.Style = OverlayForm.CrosshairStyle.DashedCross;
                p.Size = 24; p.Thickness = 2; p.Gap = 5; p.DotSize = 2;
                p.Color = Color.FromArgb(255, 235, 80);
                p.Color2 = Color.FromArgb(255, 140, 20);
                p.UseGradient = true; p.GlowEnabled = true; p.GlowSize = 8; p.GlowAlpha = 80;
            },
            p => { // Samurai T — T-shape with warm serif feel
                p.Name = "★ Samurai T";
                p.Style = OverlayForm.CrosshairStyle.TShape;
                p.Size = 20; p.Thickness = 3; p.Gap = 5; p.DotSize = 2;
                p.Color = Color.FromArgb(255, 180, 40);
                p.Color2 = Color.FromArgb(220, 60, 60);
                p.UseGradient = true; p.GlowEnabled = true; p.GlowSize = 6; p.GlowAlpha = 55;
                p.OutlineColor = Color.FromArgb(30, 10, 0); p.OutlineWidth = 1.5f;
            },
            p => { // Bastion Plus — heavy white plus with deep outline & shadow
                p.Name = "★ Bastion Plus";
                p.Style = OverlayForm.CrosshairStyle.Plus;
                p.Size = 18; p.Thickness = 4; p.Gap = 3; p.DotSize = 2;
                p.Color = Color.FromArgb(245, 245, 255);
                p.Color2 = Color.FromArgb(180, 180, 220);
                p.UseGradient = true; p.ShowShadow = true;
                p.OutlineColor = Color.FromArgb(20, 20, 30); p.OutlineWidth = 1.8f;
            },
            p => { // Prism Rainbow — rainbow gradient cross that spins
                p.Name = "★ Prism Rainbow";
                p.Style = OverlayForm.CrosshairStyle.Cross;
                p.Size = 18; p.Thickness = 2; p.Gap = 4; p.DotSize = 2;
                p.Rainbow = true; p.UseGradient = true; p.Spin = true; p.SpinSpeed = 1.2f;
                p.GlowEnabled = true; p.GlowSize = 6; p.GlowAlpha = 50;
                p.DotPulse = true;
            },
            p => { // Void Pulse — pulsing dot with deep purple halo
                p.Name = "★ Void Pulse";
                p.Style = OverlayForm.CrosshairStyle.Dot;
                p.Size = 12; p.DotSize = 5; p.ShowDot = false;
                p.Color = Color.FromArgb(200, 120, 255);
                p.Color2 = Color.FromArgb(60, 0, 120);
                p.UseGradient = true; p.DotPulse = true;
                p.GlowEnabled = true; p.GlowSize = 10; p.GlowAlpha = 90;
            },
            p => { // Aegis Brackets — bracket+dot with tactical blue
                p.Name = "★ Aegis Brackets";
                p.Style = OverlayForm.CrosshairStyle.SquareBrackets;
                p.Size = 18; p.Thickness = 2; p.DotSize = 2;
                p.Color = Color.FromArgb(120, 200, 255);
                p.Color2 = Color.FromArgb(40, 120, 220);
                p.UseGradient = true; p.GlowEnabled = true; p.GlowSize = 5; p.GlowAlpha = 60;
                p.OutlineColor = Color.FromArgb(15, 20, 35); p.OutlineWidth = 1.2f;
            },
            p => { // Sniper MK-VII — thin cross + dual circle + dot
                p.Name = "★ Sniper MK-VII";
                p.Style = OverlayForm.CrosshairStyle.Crosshairs;
                p.Size = 30; p.Thickness = 1; p.Gap = 9; p.DotSize = 1;
                p.Color = Color.FromArgb(255, 90, 80);
                p.Color2 = Color.FromArgb(255, 200, 60);
                p.UseGradient = true; p.ShowShadow = true;
                p.OutlineColor = Color.FromArgb(20, 0, 0); p.OutlineWidth = 1f;
            },
            p => { // Raptor Arrow — spinning arrow, ember orange
                p.Name = "★ Raptor Arrow";
                p.Style = OverlayForm.CrosshairStyle.Arrow;
                p.Size = 20; p.Thickness = 3; p.DotSize = 2;
                p.Color = Color.FromArgb(255, 150, 40);
                p.Color2 = Color.FromArgb(255, 60, 20);
                p.UseGradient = true; p.Spin = true; p.SpinSpeed = 2.5f;
                p.GlowEnabled = true; p.GlowSize = 6; p.GlowAlpha = 70;
            },
            p => { // Oracle Wings — teal wings with pulsing core
                p.Name = "★ Oracle Wings";
                p.Style = OverlayForm.CrosshairStyle.Wings;
                p.Size = 20; p.Thickness = 2; p.Gap = 5; p.DotSize = 3;
                p.Color = Color.FromArgb(80, 240, 220);
                p.Color2 = Color.FromArgb(40, 140, 200);
                p.UseGradient = true; p.DotPulse = true;
                p.GlowEnabled = true; p.GlowSize = 6; p.GlowAlpha = 60;
            },
            p => { // Zero Kelvin — ice serif cross with frost gradient
                p.Name = "★ Zero Kelvin";
                p.Style = OverlayForm.CrosshairStyle.SerifCross;
                p.Size = 22; p.Thickness = 2; p.Gap = 5; p.DotSize = 2;
                p.Color = Color.FromArgb(220, 250, 255);
                p.Color2 = Color.FromArgb(100, 180, 255);
                p.UseGradient = true; p.ShowShadow = true;
                p.OutlineColor = Color.FromArgb(15, 30, 60); p.OutlineWidth = 1.4f;
            },
            p => { // Crimson Seal — triangle-up with shadow, blood red
                p.Name = "★ Crimson Seal";
                p.Style = OverlayForm.CrosshairStyle.TriangleUp;
                p.Size = 22; p.Thickness = 2; p.DotSize = 2;
                p.Color = Color.FromArgb(220, 40, 60);
                p.Color2 = Color.FromArgb(120, 0, 30);
                p.UseGradient = true; p.ShowShadow = true;
                p.GlowEnabled = true; p.GlowSize = 5; p.GlowAlpha = 55;
                p.OutlineColor = Color.FromArgb(30, 0, 10); p.OutlineWidth = 1.3f;
            },
            p => { // Zen Dot — minimalist glowing dot with slow breath
                p.Name = "★ Zen Dot";
                p.Style = OverlayForm.CrosshairStyle.Dot;
                p.Size = 10; p.DotSize = 4; p.ShowDot = false;
                p.Color = Color.FromArgb(250, 250, 255);
                p.Color2 = Color.FromArgb(200, 220, 255);
                p.UseGradient = true; p.DotPulse = true;
                p.GlowEnabled = true; p.GlowSize = 7; p.GlowAlpha = 60;
                p.OutlineColor = Color.FromArgb(10, 10, 20); p.OutlineWidth = 0.8f;
            },
            p => { // Havoc Xross — bold X, red→black, thick outline
                p.Name = "★ Havoc Xross";
                p.Style = OverlayForm.CrosshairStyle.XShape;
                p.Size = 22; p.Thickness = 4; p.Gap = 4; p.DotSize = 0; p.ShowDot = false;
                p.Color = Color.FromArgb(255, 60, 40);
                p.Color2 = Color.FromArgb(100, 0, 0);
                p.UseGradient = true; p.ShowShadow = true;
                p.OutlineColor = Color.FromArgb(25, 0, 0); p.OutlineWidth = 2f;
            },
            p => { // Duelist — tight mint cross, classic esports feel
                p.Name = "★ Duelist";
                p.Style = OverlayForm.CrosshairStyle.Cross;
                p.Size = 12; p.Thickness = 2; p.Gap = 2; p.DotSize = 1;
                p.Color = Color.FromArgb(90, 255, 170);
                p.Color2 = Color.FromArgb(0, 200, 140);
                p.UseGradient = true;
                p.GlowEnabled = true; p.GlowSize = 5; p.GlowAlpha = 55;
                p.OutlineColor = Color.FromArgb(10, 30, 20); p.OutlineWidth = 1.2f;
            },
            p => { // Tessera — double-circle + inner plus for recon
                p.Name = "★ Tessera";
                p.Style = OverlayForm.CrosshairStyle.DoubleCircle;
                p.Size = 26; p.Thickness = 1; p.Gap = 4; p.DotSize = 2;
                p.Color = Color.FromArgb(180, 255, 220);
                p.Color2 = Color.FromArgb(80, 180, 255);
                p.UseGradient = true;
                p.OutlineColor = Color.FromArgb(10, 30, 40); p.OutlineWidth = 1.2f;
                p.GlowEnabled = true; p.GlowSize = 4; p.GlowAlpha = 40;
            },
            p => { // Synthwave — dashed cross, hot pink→cyan retro
                p.Name = "★ Synthwave";
                p.Style = OverlayForm.CrosshairStyle.DashedCross;
                p.Size = 22; p.Thickness = 2; p.Gap = 5; p.DotSize = 2;
                p.Color = Color.FromArgb(255, 90, 200);
                p.Color2 = Color.FromArgb(80, 220, 255);
                p.UseGradient = true; p.GlowEnabled = true; p.GlowSize = 9; p.GlowAlpha = 75;
                p.DotPulse = true;
            },
            p => { // Obsidian — thick shadowed plus, graphite
                p.Name = "★ Obsidian";
                p.Style = OverlayForm.CrosshairStyle.Plus;
                p.Size = 16; p.Thickness = 5; p.Gap = 3; p.DotSize = 2;
                p.Color = Color.FromArgb(30, 30, 40);
                p.Color2 = Color.FromArgb(80, 90, 110);
                p.UseGradient = true; p.ShowShadow = true;
                p.OutlineColor = Color.FromArgb(230, 240, 255); p.OutlineWidth = 1.6f;
            },
            p => { // Sakura — wings with pink pulse, aesthetic
                p.Name = "★ Sakura";
                p.Style = OverlayForm.CrosshairStyle.Wings;
                p.Size = 18; p.Thickness = 2; p.Gap = 5; p.DotSize = 4;
                p.Color = Color.FromArgb(255, 180, 220);
                p.Color2 = Color.FromArgb(220, 100, 180);
                p.UseGradient = true; p.DotPulse = true;
                p.GlowEnabled = true; p.GlowSize = 6; p.GlowAlpha = 65;
            },
            p => { // Hex Prism — chevron with gradient and serif outline
                p.Name = "★ Hex Prism";
                p.Style = OverlayForm.CrosshairStyle.SerifCross;
                p.Size = 20; p.Thickness = 2; p.Gap = 4; p.DotSize = 2;
                p.Color = Color.FromArgb(120, 255, 200);
                p.Color2 = Color.FromArgb(40, 160, 255);
                p.UseGradient = true; p.GlowEnabled = true; p.GlowSize = 6; p.GlowAlpha = 55;
                p.OutlineColor = Color.FromArgb(10, 20, 30); p.OutlineWidth = 1.4f;
            },
        };

        private static List<Preset>? _cache;
        public static List<Preset> All => _cache ??= Build();

        private static List<Preset> Build()
        {
            var list = new List<Preset>(_templates.Length * _palette.Length + _signatures.Length);

            // Curated signature presets first — they're the showcase pieces.
            foreach (var cfg in _signatures)
            {
                var p = new Preset();
                cfg(p);
                list.Add(p);
            }

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
