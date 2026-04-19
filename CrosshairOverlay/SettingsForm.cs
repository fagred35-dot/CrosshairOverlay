using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CrosshairOverlay
{
    public class SettingsForm : Form
    {
        private readonly OverlayForm _overlay;
        private readonly ToolTip _toolTip;
        private readonly System.Windows.Forms.Timer _animTimer;

        // Animation
        private double _slideProgress = 0.0;
        private bool _sliding = false;
        private bool _slidingIn = true;

        // Scroll
        private int _scrollY = 0;
        private int _contentHeight = 0;

        // Layout items
        private readonly System.Collections.Generic.List<UiItem> _items = new();
        private int _hoverIndex = -1;
        private int _activeSlider = -1;
        private bool _draggingSlider = false;

        // ═══════════════════════════════════════════════════
        //  Theme Colors (switchable)
        // ═══════════════════════════════════════════════════
        private static Color BgColor = Color.FromArgb(220, 12, 6, 24);
        private static Color GlassBg = Color.FromArgb(35, 140, 100, 220);
        private static Color GlassHighlight = Color.FromArgb(18, 255, 255, 255);
        private static Color GlassBorder = Color.FromArgb(45, 180, 140, 255);

        private static Color Accent = Color.FromArgb(130, 80, 220);
        private static Color AccentGlow = Color.FromArgb(175, 130, 255);
        internal static Color GetAccent() => Accent;
        private static Color TextMain = Color.FromArgb(235, 228, 245);
        private static Color TextDim = Color.FromArgb(130, 120, 155);
        private static Color TextMuted = Color.FromArgb(90, 85, 110);

        private static Color ControlBg = Color.FromArgb(50, 40, 20, 70);
        private static Color ControlHover = Color.FromArgb(70, 60, 30, 110);
        private static Color SliderTrack = Color.FromArgb(100, 35, 18, 60);
        private static Color ToggleOn = Color.FromArgb(130, 80, 220);
        private static Color ToggleOff = Color.FromArgb(50, 30, 16, 55);
        private static Color SectionColor = Color.FromArgb(160, 130, 255);
        private static Color BtnBorder = Color.FromArgb(70, 130, 80, 220);
        private static Color CardBg = Color.FromArgb(40, 50, 30, 90);
        private static Color CardHover = Color.FromArgb(55, 70, 40, 120);

        private const int PanelWidth = 340;
        private const int ItemPadX = 16;
        private const int HeaderTitleHeight = 68;    // title bar
        private const int HeaderHeight = 68 + 38;    // title bar + tab strip (total header region)
        private const int TabStripY = 68;             // y of tab strip inside header
        private const int ControlRadius = 10;

        // Themes
        internal static int _currentTheme = 0;
        private static readonly string[] ThemeNames = {
            "Пурпур", "Кибер", "Матрица", "Кровь", "Океан", "Закат", "Арктика", "Полночь"
        };

        // Theme gradient colors for swatches [Accent, AccentGlow]
        private static readonly Color[][] ThemeGradients = {
            new[] { Color.FromArgb(130, 80, 220),  Color.FromArgb(175, 130, 255) }, // Пурпур
            new[] { Color.FromArgb(255, 50, 150),  Color.FromArgb(255, 100, 200) }, // Кибер
            new[] { Color.FromArgb(0, 200, 40),    Color.FromArgb(50, 255, 80)   }, // Матрица
            new[] { Color.FromArgb(220, 30, 30),   Color.FromArgb(255, 80, 80)   }, // Кровь
            new[] { Color.FromArgb(30, 120, 220),  Color.FromArgb(80, 170, 255)  }, // Океан
            new[] { Color.FromArgb(230, 150, 30),  Color.FromArgb(255, 200, 80)  }, // Закат
            new[] { Color.FromArgb(100, 180, 230), Color.FromArgb(160, 215, 245) }, // Арктика
            new[] { Color.FromArgb(60, 80, 180),   Color.FromArgb(100, 130, 220) }, // Полночь
        };

        // Scrollbar drag
        private bool _draggingScrollbar = false;
        private int _scrollGrabOffset = 0; // offset from thumb top to grab point
        // Scrollbar geometry
        private const int ScrollbarWidth = 8;
        private const int ScrollbarRightPad = 3;
        private const int ScrollbarHitPad = 14; // clickable area (wider than visual bar)

        // ═══════════════════════════════════════════════════
        //  v2.1.3 — Tabs & Search
        // ═══════════════════════════════════════════════════
        internal enum Tab { Appearance = 0, Effects = 1, Autoclicker = 2, Hotkeys = 3, Advanced = 4, About = 5 }
        private Tab _currentTab = Tab.Appearance;
        private const int TabBarHeight = 38;
        private static readonly string[] TabIconsRu = { "Вид", "Эффект", "Клик", "Хоткеи", "Дополн.", "О прог." };
        private static readonly string[] TabIconsEn = { "Look", "Effects", "Click", "Hotkeys", "Advanced", "About" };

        // Search
        private TextBox? _searchBox;
        private string _searchText = "";


        // Cached fonts
        private readonly Font _fontTitle = new("Segoe UI", 16f, FontStyle.Bold);
        private readonly Font _fontSection = new("Segoe UI", 9.5f, FontStyle.Bold);
        private readonly Font _fontControl = new("Segoe UI", 9f);
        private readonly Font _fontSmall = new("Segoe UI", 7.5f);
        private readonly Font _fontClose = new("Segoe UI", 14f, FontStyle.Bold);
        private readonly Font _fontValue = new("Segoe UI Semibold", 8.5f);

        private enum UiType { Section, Slider, Toggle, ColorPicker, StyleSelector, Button, Info, Spacer, NumericInput }

        // Inline text editing state
        private TextBox? _editBox;
        private int _editingIndex = -1;
        private bool _applyingEdit;

        // Background cache for performance
        private Bitmap? _bgCache;
        private bool _bgDirty = true;

        private class UiItem
        {
            public UiType Type;
            public string Label = "";
            public string Tooltip = "";
            public int Height;
            public int Y;
            public int SliderValue, SliderMin, SliderMax, SliderStep;
            public Action<int>? OnSliderChanged;
            public bool ToggleValue;
            public Action<bool>? OnToggleChanged;
            public Color ColorValue;
            public Action<Color>? OnColorChanged;
            public int SelectedStyle;
            public string[]? StyleNames;
            public Action<int>? OnStyleChanged;
            public Action? OnClick;
            public string InfoValue = "";
            public Color InfoColor = TextMain;
            public bool IsThemeSelector; // true = draw gradient swatches instead of text
            public Color[][]? GradientPalette; // override for swatch colors (null = use ThemeGradients)
        }

        public SettingsForm(OverlayForm overlay)
        {
            _overlay = overlay;

            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.StartPosition = FormStartPosition.Manual;
            this.Width = PanelWidth;
            this.Height = Screen.PrimaryScreen!.WorkingArea.Height;
            this.Location = new Point(-PanelWidth, 0);
            this.BackColor = Color.Black;
            this.Opacity = 0.96;

            this.SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw, true);
            this.UpdateStyles();

            _toolTip = new ToolTip
            {
                InitialDelay = 400,
                ReshowDelay = 150,
                AutoPopDelay = 4000,
                BackColor = Color.FromArgb(18, 10, 35),
                ForeColor = TextMain
            };

            _animTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _animTimer.Tick += AnimTick;

            BuildItems();
            ComputeLayout();
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

        #region Build Items

        private void BuildItems()
        {
            _items.Clear();

            // Apply UI theme palette
            ApplyUiTheme(UiThemePresets.Current);

            // If searching, build all tabs then filter
            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                BuildAppearanceTab();
                BuildEffectsTab();
                BuildAutoclickerTab();
                BuildHotkeysTab();
                BuildAdvancedTab();
                BuildAboutTab();
                FilterBySearch();
                return;
            }

            switch (_currentTab)
            {
                case Tab.Appearance: BuildAppearanceTab(); break;
                case Tab.Effects: BuildEffectsTab(); break;
                case Tab.Autoclicker: BuildAutoclickerTab(); break;
                case Tab.Hotkeys: BuildHotkeysTab(); break;
                case Tab.Advanced: BuildAdvancedTab(); break;
                case Tab.About: BuildAboutTab(); break;
            }
        }

        private void FilterBySearch()
        {
            string s = _searchText.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(s)) return;
            var filtered = new System.Collections.Generic.List<UiItem>();
            UiItem? currentSection = null;
            bool sectionAdded = false;
            foreach (var it in _items)
            {
                if (it.Type == UiType.Section)
                {
                    currentSection = it;
                    sectionAdded = false;
                    continue;
                }
                if (it.Type == UiType.Spacer) continue;
                string hay = (it.Label + " " + it.Tooltip).ToLowerInvariant();
                if (hay.Contains(s))
                {
                    if (!sectionAdded && currentSection != null)
                    {
                        filtered.Add(currentSection);
                        sectionAdded = true;
                    }
                    filtered.Add(it);
                }
            }
            _items.Clear();
            _items.AddRange(filtered);
            if (_items.Count == 0)
            {
                _items.Add(new UiItem { Type = UiType.Section, Label = Lang.IsRussian ? "Ничего не найдено" : "No results", Height = 32 });
            }
        }

        private void ApplyUiTheme(UiThemePresets.Preset p)
        {
            var c = UiThemePresets.Colors(p);
            BgColor = c.bg;
            TextMain = c.text;
            TextDim = c.textDim;
            CardBg = c.card;
            // Accent + Glow kept from ThemeGradients (user-picked color theme)
            _bgDirty = true;
        }

        private void BuildAppearanceTab()
        {
            // ── LANGUAGE ──
            AddSection(Lang.SectionLanguage);
            AddToggle(Lang.LanguageLabel, !Lang.IsRussian,
                v => {
                    Lang.IsRussian = !v;
                    _overlay.SaveSettings();
                    _overlay.RefreshTray();
                    _bgDirty = true;
                    BuildItems(); ComputeLayout(); Invalidate();
                },
                Lang.LanguageTooltip);
            AddSpacer();

            // ── COLOR THEME ──
            AddSection(Lang.SectionTheme);
            _items.Add(new UiItem
            {
                Type = UiType.StyleSelector,
                Label = Lang.ThemeLabel,
                Tooltip = Lang.ThemeTooltip,
                Height = 58,
                SelectedStyle = _currentTheme,
                StyleNames = ThemeNames,
                IsThemeSelector = true,
                OnStyleChanged = v => ApplyTheme(v)
            });

            // ── UI THEME (Dark/Light/OLED/Neon) — gradient swatches ──
            _items.Add(new UiItem
            {
                Type = UiType.StyleSelector,
                Label = Lang.IsRussian ? "Тема UI" : "UI Theme",
                Tooltip = Lang.IsRussian ? "Dark / Light / OLED / Neon" : "Dark / Light / OLED / Neon",
                Height = 50,
                SelectedStyle = (int)UiThemePresets.Current,
                StyleNames = new[] { "Dark", "Light", "OLED", "Neon" },
                IsThemeSelector = true,
                GradientPalette = new[]
                {
                    new[] { Color.FromArgb(36, 30, 54),   Color.FromArgb(78, 60, 128)  }, // Dark
                    new[] { Color.FromArgb(245, 245, 250),Color.FromArgb(200, 205, 215)}, // Light
                    new[] { Color.FromArgb(0, 0, 0),      Color.FromArgb(30, 30, 38)   }, // OLED
                    new[] { Color.FromArgb(255, 0, 170),  Color.FromArgb(0, 220, 255)  }, // Neon
                },
                OnStyleChanged = v =>
                {
                    UiThemePresets.Current = (UiThemePresets.Preset)v;
                    _overlay.SaveSettings();
                    _bgDirty = true;
                    BuildItems(); ComputeLayout(); Invalidate();
                }
            });
            AddSpacer();

            // ── CROSSHAIR ──
            AddSection(Lang.SectionCrosshair);
            AddSlider(Lang.Size, _overlay._size, 4, 100, 1, v => _overlay._size = v, Lang.SizeTooltip);
            AddSlider(Lang.Thickness, _overlay._thickness, 1, 10, 1, v => _overlay._thickness = v, Lang.ThicknessTooltip);
            AddSlider(Lang.Gap, _overlay._gap, 0, 30, 1, v => _overlay._gap = v, Lang.GapTooltip);
            AddSlider(Lang.Opacity, _overlay._opacity, 10, 255, 5,
                v => { _overlay._opacity = v; _overlay._targetOpacity = v; }, Lang.OpacityTooltip);
            AddSlider(Lang.OffsetX, _overlay._offsetX, -500, 500, 1, v => _overlay._offsetX = v, Lang.OffsetXTooltip);
            AddSlider(Lang.OffsetY, _overlay._offsetY, -500, 500, 1, v => _overlay._offsetY = v, Lang.OffsetYTooltip);
            AddToggle(Lang.CenterDot, _overlay._showDot, v => _overlay._showDot = v, Lang.CenterDotTooltip);
            AddSlider(Lang.DotSize, _overlay._dotSize, 1, 10, 1, v => _overlay._dotSize = v, Lang.DotSizeTooltip);
            AddToggle(Lang.DotPulse, _overlay._dotPulse, v => _overlay._dotPulse = v, Lang.DotPulseTooltip);
            AddColor(Lang.CrossColor, _overlay._crossColor, c => _overlay._crossColor = c, Lang.CrossColorTooltip);
            AddToggle(Lang.RainbowMode, _overlay._rainbowMode, v => _overlay._rainbowMode = v, Lang.RainbowModeTooltip);
            AddToggle(Lang.Gradient, _overlay._useGradient, v => _overlay._useGradient = v, Lang.GradientTooltip);
            AddColor(Lang.SecondColor, _overlay._crossColor2, c => _overlay._crossColor2 = c, Lang.SecondColorTooltip);
            AddStyle();
            AddButton(Lang.OpenGallery, () =>
            {
                _overlay.PauseTopmost();
                using var gallery = new CrosshairGalleryForm(_overlay);
                gallery.ShowDialog(this);
                _overlay.ResumeTopmost();
                BuildItems(); ComputeLayout(); Invalidate();
            }, Lang.OpenGalleryTooltip);
            AddSpacer();
        }

        private void BuildEffectsTab()
        {
            // ── EFFECTS ──
            AddSection(Lang.SectionEffects);
            AddToggle(Lang.Rotation, _overlay._spin, v => _overlay._spin = v, Lang.RotationTooltip);
            AddSlider(Lang.RotationSpeed, (int)(_overlay._spinSpeed * 10), 1, 100, 1, v => _overlay._spinSpeed = v / 10f, Lang.RotationSpeedTooltip);
            AddToggle(Lang.Outline, _overlay._showOutline, v => _overlay._showOutline = v, Lang.OutlineTooltip);
            AddColor(Lang.OutlineColor, _overlay._outlineColor, c => _overlay._outlineColor = c, Lang.OutlineColorTooltip);
            AddSlider(Lang.OutlineWidth, (int)(_overlay._outlineWidth * 10), 5, 50, 1, v => _overlay._outlineWidth = v / 10f, Lang.OutlineWidthTooltip);
            AddToggle(Lang.Shadow, _overlay._showShadow, v => _overlay._showShadow = v, Lang.ShadowTooltip);
            AddToggle(Lang.AntiAlias, _overlay._antiAlias, v => _overlay._antiAlias = v, Lang.AntiAliasTooltip);
            AddSpacer();

            // ── DYNAMIC CROSSHAIR ──
            AddSection(Lang.SectionDynamic);
            AddToggle(Lang.DynamicCrosshair, _overlay._dynamicCrosshair, v => _overlay._dynamicCrosshair = v, Lang.DynamicCrosshairTooltip);
            AddSlider(Lang.MaxSpread, (int)_overlay._dynamicMaxSpread, 1, 30, 1, v => _overlay._dynamicMaxSpread = v, Lang.MaxSpreadTooltip);
            AddSlider(Lang.RecoverySpeed, (int)(_overlay._dynamicRecovery * 100), 5, 50, 5, v => _overlay._dynamicRecovery = v / 100f, Lang.RecoverySpeedTooltip);
            AddSpacer();

            // ── VISUAL ──
            AddSection(Lang.SectionVisual);
            AddToggle(Lang.GlowEffect, _overlay._glowEnabled, v => _overlay._glowEnabled = v, Lang.GlowEffectTooltip);
            AddSlider(Lang.GlowSize, _overlay._glowSize, 2, 20, 1, v => _overlay._glowSize = v, Lang.GlowSizeTooltip);
            AddSlider(Lang.GlowBrightness, _overlay._glowAlpha, 20, 150, 5, v => _overlay._glowAlpha = v, Lang.GlowBrightnessTooltip);
            AddToggle(Lang.HitMarker, _overlay._hitMarkerEnabled, v => _overlay._hitMarkerEnabled = v, Lang.HitMarkerTooltip);
            AddSlider(Lang.HitMarkerSize, (int)_overlay._hitMarkerSize, 4, 30, 1, v => _overlay._hitMarkerSize = v, Lang.HitMarkerSizeTooltip);
            AddSpacer();
        }

        private void BuildAutoclickerTab()
        {
            AddSection(Lang.SectionAutoclicker);
            AddToggle(Lang.EnableAutoclicker, _overlay._autoClickerEnabled,
                v => { _overlay._autoClickerEnabled = v; _overlay.UpdateClickerState(); },
                Lang.EnableAutoclickerTooltip);
            AddToggle(Lang.HoldMode, _overlay._clickOnHold, v => _overlay._clickOnHold = v, Lang.HoldModeTooltip);
            AddSlider(Lang.ClicksPerSec, _overlay._clicksPerSecond, 1, 1000, 1,
                v => { _overlay._clicksPerSecond = v; _overlay.SaveSettings(); },
                Lang.IsRussian ? "Обычный автокликер (до 60 кпс)" : "Regular autoclicker (up to 60 CPS)");
            AddToggle(Lang.RightClick, _overlay._rightClickMode, v => _overlay._rightClickMode = v, Lang.RightClickTooltip);
            AddToggle(Lang.RandomDelay, _overlay._randomDelay, v => _overlay._randomDelay = v, Lang.RandomDelayTooltip);
            AddSlider(Lang.SpreadPercent, _overlay._randomDelayPercent, 5, 50, 5, v => _overlay._randomDelayPercent = v, Lang.SpreadPercentTooltip);
            AddToggle(Lang.IsRussian ? "Без системного писка" : "Mute system beep",
                _overlay._muteBeepDuringClicks,
                v => { _overlay._muteBeepDuringClicks = v; _overlay.SaveSettings(); },
                Lang.IsRussian ? "Глушить Windows beep при большом CPS" : "Silence Windows default beep at high CPS");
            AddSpacer();

            // Burst (independent process — runs on LMB press regardless of autoclicker)
            AddSection(Lang.IsRussian ? "──  BURST (независимый процесс)  ──" : "──  BURST (independent process)  ──");
            AddToggle(Lang.IsRussian ? "Burst режим" : "Burst mode", _overlay._burstMode,
                v => { _overlay._burstMode = v; _overlay.UpdateBurstState(); },
                Lang.IsRussian ? "Отдельный процесс. Работает независимо от автокликера." : "Separate process. Works independently of autoclicker.");
            AddSlider(Lang.IsRussian ? "Кликов в Burst" : "Burst count", _overlay._burstCount, 1, 200, 1, v => _overlay._burstCount = v,
                Lang.IsRussian ? "Пачка кликов на каждое нажатие ЛКМ (одним SendInput)" : "Batched clicks per each LMB press (single SendInput)");
            AddSpacer();

            // Session limits (#40, #41)
            AddSection(Lang.IsRussian ? "──  ЛИМИТЫ СЕССИИ  ──" : "──  SESSION LIMITS  ──");
            AddSlider(Lang.IsRussian ? "Лимит кликов (0=выкл)" : "Click limit (0=off)",
                _overlay._sessionClickLimit, 0, 100000, 100,
                v => _overlay._sessionClickLimit = v,
                Lang.IsRussian ? "Авто-стоп при достижении количества" : "Auto-stop after N clicks");
            AddSlider(Lang.IsRussian ? "Таймер, мин (0=выкл)" : "Timer, min (0=off)",
                _overlay._sessionTimerMin, 0, 120, 1,
                v => _overlay._sessionTimerMin = v,
                Lang.IsRussian ? "Авто-стоп через N минут" : "Auto-stop after N minutes");
            AddSpacer();

            // Anti-AFK (#37)
            AddSection(Lang.IsRussian ? "──  АНТИ-AFK  ──" : "──  ANTI-AFK  ──");
            AddToggle(Lang.IsRussian ? "Анти-AFK" : "Anti-AFK",
                _overlay._antiAfkEnabled, v => _overlay._antiAfkEnabled = v,
                Lang.IsRussian ? "Клик при отсутствии активности" : "Click on inactivity");
            AddSlider(Lang.IsRussian ? "Интервал, сек" : "Interval, sec",
                _overlay._antiAfkSeconds, 5, 600, 5,
                v => _overlay._antiAfkSeconds = v,
                Lang.IsRussian ? "Через сколько секунд бездействия" : "Seconds of inactivity");
            AddSpacer();

            // Jitter aim (#47)
            AddToggle(Lang.IsRussian ? "Jitter Aim" : "Jitter Aim",
                _overlay._jitterAim, v => _overlay._jitterAim = v,
                Lang.IsRussian ? "Мелкие случайные смещения курсора" : "Small random mouse offsets");
            AddSlider(Lang.IsRussian ? "Jitter px" : "Jitter px",
                _overlay._jitterAimPx, 1, 20, 1, v => _overlay._jitterAimPx = v,
                Lang.IsRussian ? "Размах смещения в пикселях" : "Offset amplitude in pixels");
            AddSpacer();

            AddInfo(Lang.ClickCounter, _overlay.GetClickCounter().ToString("N0"), AccentGlow, Lang.ClickCounterTooltip);
            AddInfo(Lang.HotkeyLabel, OverlayForm.HotkeyToString(_overlay._hkMods[10], _overlay._hkKeys[10]), AccentGlow, Lang.HotkeyAutoTooltip);
            AddSpacer();
        }

        private void BuildHotkeysTab()
        {
            AddSection(Lang.SectionHotkeys);

            // Conflict detector (#55)
            var conflicts = HotkeyConflictDetector.FindConflicts(_overlay._hkMods, _overlay._hkKeys);
            if (conflicts.Count > 0)
            {
                var sb = new System.Text.StringBuilder();
                sb.Append(Lang.IsRussian ? "⚠ Конфликт: " : "⚠ Conflict: ");
                foreach (var (a, b) in conflicts)
                {
                    sb.Append($"{Lang.HotkeyNamesArr[a]} ↔ {Lang.HotkeyNamesArr[b]}; ");
                }
                AddInfo(Lang.IsRussian ? "Конфликты" : "Conflicts", sb.ToString(),
                    Color.OrangeRed, Lang.IsRussian ? "Обнаружены одинаковые биндинги" : "Duplicate bindings");
            }

            for (int i = 1; i <= OverlayForm.HOTKEY_COUNT; i++)
                AddHotkeyButton(i);
            AddSpacer();
        }

        private void BuildAdvancedTab()
        {
            // Profile import/export (#26, #27)
            AddSection(Lang.IsRussian ? "──  ПРОФИЛИ  ──" : "──  PROFILES  ──");
            AddButton(Lang.IsRussian ? "Экспорт профиля в файл…" : "Export profile to file…",
                () => ExportProfileFile(),
                Lang.IsRussian ? "Сохранить все настройки в JSON" : "Save all settings to JSON");
            AddButton(Lang.IsRussian ? "Импорт профиля из файла…" : "Import profile from file…",
                () => ImportProfileFile(),
                Lang.IsRussian ? "Загрузить настройки из JSON" : "Load settings from JSON");
            AddButton(Lang.IsRussian ? "Экспорт кода прицела" : "Export crosshair code",
                () => ExportCrosshairCode(),
                Lang.IsRussian ? "Скопировать короткий код в буфер" : "Copy short share code to clipboard");
            AddButton(Lang.IsRussian ? "Импорт кода прицела" : "Import crosshair code",
                () => ImportCrosshairCode(),
                Lang.IsRussian ? "Вставить код из буфера" : "Paste code from clipboard");
            AddSpacer();

            // System (#86, #95)
            AddSection(Lang.IsRussian ? "──  СИСТЕМА  ──" : "──  SYSTEM  ──");
            AddToggle(Lang.IsRussian ? "Автозапуск с Windows" : "Autostart with Windows",
                AutostartManager.IsEnabled,
                v => AutostartManager.Set(v),
                Lang.IsRussian ? "Добавить в реестр HKCU\\...\\Run" : "Add to HKCU\\...\\Run");
            AddToggle(Lang.IsRussian ? "Portable-режим" : "Portable mode",
                PortableMode.IsPortable,
                v =>
                {
                    PortableMode.Enable(v);
                    _overlay.SaveSettings();
                    _bgDirty = true;
                    BuildItems(); ComputeLayout(); Invalidate();
                },
                Lang.IsRussian ? "Хранить настройки рядом с exe (portable.flag)" : "Store settings next to exe");
            AddToggle(Lang.IsRussian ? "Скрыть в полноэкр. играх" : "Hide in fullscreen apps",
                _overlay._hideInFullscreen,
                v => _overlay._hideInFullscreen = v,
                Lang.IsRussian ? "Скрывать прицел когда игра в fullscreen" : "Hide overlay when a fullscreen app is foreground");
            AddToggle(Lang.IsRussian ? "Монитор системы (CPU/RAM/GPU)" : "System monitor (CPU/RAM/GPU)",
                _overlay._sysMonVisible,
                v => { _overlay._sysMonVisible = v; _overlay.UpdateSysMonState(); _overlay.SaveSettings(); },
                Lang.IsRussian
                    ? "Плавающее окно с загрузкой CPU/RAM/GPU/Disk/Net в реальном времени. Перетаскивайте мышью."
                    : "Floating window with live CPU/RAM/GPU/Disk/Net. Drag to move.");
            AddButton(Lang.IsRussian ? "Открыть папку скриншотов" : "Open screenshots folder",
                () => _overlay.OpenScreenshotsFolder(),
                "");
            AddSpacer();

            // Actions (reset, update)
            AddSection(Lang.SectionActions);
            AddButton(Lang.ResetSettings, () =>
            {
                _overlay.ResetToDefaults();
                BuildItems(); ComputeLayout(); Invalidate();
            }, Lang.ResetSettingsTooltip);
            AddButton(Lang.CloseMenu, () => SlideOut(), Lang.CloseMenuTooltip);
            _items.Add(new UiItem
            {
                Type = UiType.Button, Label = Lang.CheckUpdate, Tooltip = Lang.CheckUpdateTooltip,
                Height = 38, OnClick = () => _overlay.CheckForUpdateAsync(this)
            });
            AddSpacer();
        }

        private void BuildAboutTab()
        {
            AddSection(Lang.IsRussian ? "──  О ПРОГРАММЕ  ──" : "──  ABOUT  ──");
            AddInfo("Version", OverlayForm.APP_VERSION, AccentGlow, "");
            var u = UsageTracker.Current;
            AddInfo(Lang.IsRussian ? "Сегодня" : "Today", UsageTracker.FormatDuration(u.SecondsToday), TextMain, "");
            AddInfo(Lang.IsRussian ? "Всего" : "Total", UsageTracker.FormatDuration(u.SecondsTotal), TextMain, "");
            AddInfo(Lang.IsRussian ? "Стрик" : "Streak", u.StreakDays.ToString() + (Lang.IsRussian ? " дн." : " d"), AccentGlow, "");
            AddInfo(Lang.IsRussian ? "Всего кликов" : "Total clicks", u.TotalClicks.ToString("N0"), TextMain, "");
            AddInfo(Lang.IsRussian ? "Макс. CPS" : "Max CPS", u.MaxCps.ToString(), AccentGlow, "");
            AddSpacer();

            AddSection(Lang.IsRussian ? "──  ССЫЛКИ  ──" : "──  LINKS  ──");
            AddButton("GitHub", () => ShellHelper.OpenFolder("https://github.com/fagred35-dot/CrosshairOverlay"), "");
            AddButton(Lang.IsRussian ? "Достижения" : "Achievements",
                () => ShowAchievements(), Lang.IsRussian ? "Плашки 1M/10h/..." : "1M/10h/... badges");
            AddSpacer();
        }

        private void ExportProfileFile()
        {
            using var sfd = new SaveFileDialog { Filter = "JSON profile|*.json", FileName = "crosshair_profile.json" };
            if (sfd.ShowDialog(this) != DialogResult.OK) return;
            _overlay.SaveSettings();
            if (ProfileIO.ExportToFile(OverlayForm._settingsPath, sfd.FileName))
                MessageBox.Show(this, Lang.IsRussian ? "Профиль экспортирован" : "Profile exported", "OK");
            else
                MessageBox.Show(this, Lang.IsRussian ? "Ошибка экспорта" : "Export error", "Error");
        }

        private void ImportProfileFile()
        {
            using var ofd = new OpenFileDialog { Filter = "JSON profile|*.json" };
            if (ofd.ShowDialog(this) != DialogResult.OK) return;
            if (ProfileIO.ImportFromFile(ofd.FileName, OverlayForm._settingsPath))
            {
                MessageBox.Show(this, Lang.IsRussian ? "Импортировано. Перезапуск применит все изменения." : "Imported. Restart to apply.", "OK");
            }
            else
                MessageBox.Show(this, Lang.IsRussian ? "Ошибка импорта" : "Import error", "Error");
        }

        private void ExportCrosshairCode()
        {
            string code = ProfileIO.ExportCrosshairCode(_overlay);
            try { Clipboard.SetText(code); } catch { }
            MessageBox.Show(this, (Lang.IsRussian ? "Код скопирован в буфер:\n\n" : "Code copied to clipboard:\n\n") + code, "OK");
        }

        private void ImportCrosshairCode()
        {
            string code = "";
            try { code = Clipboard.GetText() ?? ""; } catch { }
            if (string.IsNullOrWhiteSpace(code))
            {
                MessageBox.Show(this, Lang.IsRussian ? "Буфер пуст" : "Clipboard empty", "Error");
                return;
            }
            if (ProfileIO.ImportCrosshairCode(code, _overlay))
            {
                _overlay.SaveSettings();
                _overlay._needsStaticRender = true;
                BuildItems(); ComputeLayout(); Invalidate();
                MessageBox.Show(this, Lang.IsRussian ? "Прицел импортирован" : "Crosshair imported", "OK");
            }
            else
                MessageBox.Show(this, Lang.IsRussian ? "Неверный код" : "Invalid code", "Error");
        }

        private void ShowAchievements()
        {
            var u = UsageTracker.Current;
            var list = new System.Collections.Generic.List<AchievementsForm.Achievement>();
            void A(string icon, string name, string desc, bool unlocked, double progress) =>
                list.Add(new AchievementsForm.Achievement
                    { Icon = icon, Name = name, Desc = desc, Unlocked = unlocked, Progress = Math.Clamp(progress, 0, 1) });

            bool ru = Lang.IsRussian;
            A("🎯", ru ? "Первый запуск" : "First launch",
                ru ? "Открыл программу" : "Opened the app", true, 1.0);
            A("🖱", ru ? "10 000 кликов" : "10K clicks",
                ru ? "Всего ЛКМ" : "Total LMB", u.TotalClicks >= 10000, u.TotalClicks / 10000.0);
            A("💥", ru ? "100 000 кликов" : "100K clicks",
                ru ? "Настоящий энтузиаст" : "True enthusiast", u.TotalClicks >= 100000, u.TotalClicks / 100000.0);
            A("🏅", ru ? "1 миллион кликов" : "1M clicks",
                ru ? "Легенда автокликера" : "Autoclicker legend", u.TotalClicks >= 1000000, u.TotalClicks / 1000000.0);
            A("⏱", ru ? "1 час использования" : "1 hour active",
                ru ? "60 минут с прицелом" : "60 min with crosshair", u.SecondsTotal >= 3600, u.SecondsTotal / 3600.0);
            A("🕰", ru ? "10 часов" : "10 hours",
                ru ? "Время идёт…" : "Time flies…", u.SecondsTotal >= 36000, u.SecondsTotal / 36000.0);
            A("🔥", ru ? "Стрик 7 дней" : "7-day streak",
                ru ? "Заходил 7 дней подряд" : "Entered 7 days in a row", u.StreakDays >= 7, u.StreakDays / 7.0);
            A("⚡", ru ? "500+ CPS" : "500+ CPS",
                ru ? "Пиковая скорость" : "Peak speed", u.MaxCps >= 500, u.MaxCps / 500.0);
            A("🎨", ru ? "Коллекционер" : "Collector",
                ru ? "Опробуй все стили прицела" : "Try all crosshair styles", false, 0.0);

            bool prevTop = this.TopMost;
            this.TopMost = false;
            _overlay.PauseTopmost();
            using (var dlg = new AchievementsForm(list))
            {
                dlg.ShowDialog(this);
            }
            _overlay.ResumeTopmost();
            this.TopMost = prevTop;
            this.Activate();
        }

        // Legacy builder retained for safety (unused now that we dispatch by tab).
        private void BuildCrosshairTab()
        {
            BuildAppearanceTab();
            BuildEffectsTab();
            BuildAutoclickerTab();
            BuildAdvancedTab();
            BuildHotkeysTab();
            BuildAboutTab();
        }


        private void AddSection(string label)
        {
            _items.Add(new UiItem { Type = UiType.Section, Label = label, Height = 32 });
        }

        private void AddSlider(string label, int value, int min, int max, int step,
            Action<int> onChange, string tooltip)
        {
            _items.Add(new UiItem
            {
                Type = UiType.Slider, Label = label, Tooltip = tooltip, Height = 48,
                SliderValue = value, SliderMin = min, SliderMax = max, SliderStep = step,
                OnSliderChanged = onChange
            });
        }

        private void AddToggle(string label, bool value, Action<bool> onChange, string tooltip)
        {
            _items.Add(new UiItem
            {
                Type = UiType.Toggle, Label = label, Tooltip = tooltip, Height = 36,
                ToggleValue = value, OnToggleChanged = onChange
            });
        }

        private void AddColor(string label, Color value, Action<Color> onChange, string tooltip)
        {
            _items.Add(new UiItem
            {
                Type = UiType.ColorPicker, Label = label, Tooltip = tooltip, Height = 38,
                ColorValue = value, OnColorChanged = onChange
            });
        }

        private void AddNumericInput(string label, int value, int min, int max,
            Action<int> onChange, string tooltip)
        {
            _items.Add(new UiItem
            {
                Type = UiType.NumericInput, Label = label, Tooltip = tooltip, Height = 40,
                SliderValue = value, SliderMin = min, SliderMax = max, SliderStep = 1,
                OnSliderChanged = onChange
            });
        }

        private void AddStyle()
        {
            _items.Add(new UiItem
            {
                Type = UiType.StyleSelector, Label = Lang.CrosshairStyle,
                Tooltip = Lang.CrosshairStyleTooltip, Height = 68,
                SelectedStyle = (int)_overlay._style,
                StyleNames = new[] { "✚", "○", "●", "⊕", "‹", "T", "◇", "▲", "+", "✕", "▽", "⊹", "[ ]", "∨", "🖼" },
                OnStyleChanged = v =>
                {
                    _overlay._style = (OverlayForm.CrosshairStyle)v;
                    _overlay.SaveSettings();
                    BuildItems(); ComputeLayout(); Invalidate();
                }
            });

            // Custom image controls (only visible when CustomImage style is selected)
            if (_overlay._style == OverlayForm.CrosshairStyle.CustomImage)
            {
                AddButton(Lang.ChooseImage, () =>
                {
                    using var ofd = new OpenFileDialog
                    {
                        Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files|*.*",
                        Title = Lang.ChooseImage
                    };
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        _overlay._customImagePath = ofd.FileName;
                        _overlay._customImageCache?.Dispose();
                        _overlay._customImageCache = new Bitmap(ofd.FileName);
                        _overlay.SaveSettings();
                    }
                }, Lang.ChooseImageTooltip);
            }

            // Manual rotation slider (for all styles)
            AddSlider(Lang.ManualRotation, (int)_overlay._rotation, 0, 360, 1,
                v => _overlay._rotation = v,
                Lang.ManualRotationTooltip);
        }

        private void AddButton(string label, Action onClick, string tooltip)
        {
            _items.Add(new UiItem
            {
                Type = UiType.Button, Label = label, Tooltip = tooltip, Height = 38,
                OnClick = onClick
            });
        }

        private void AddInfo(string label, string value, Color color, string tooltip)
        {
            _items.Add(new UiItem
            {
                Type = UiType.Info, Label = label, Tooltip = tooltip, Height = 38,
                InfoValue = value, InfoColor = color
            });
        }

        private void AddHotkeyButton(int hkId)
        {
            // Defensive bounds: array was extended in v2.1.3 from 18 to 22 entries.
            // If an older saved settings file yields out-of-range here, fall back gracefully.
            string name = (hkId >= 0 && hkId < Lang.HotkeyNamesArr.Length)
                ? Lang.HotkeyNamesArr[hkId] : "Hotkey " + hkId;
            uint curMod = (hkId >= 0 && hkId < _overlay._hkMods.Length) ? _overlay._hkMods[hkId] : 0u;
            uint curVk  = (hkId >= 0 && hkId < _overlay._hkKeys.Length) ? _overlay._hkKeys[hkId] : 0u;
            string combo = OverlayForm.HotkeyToString(curMod, curVk);
            AddButton($"{name}:  {combo}", () =>
            {
                try
                {
                    // Pause topmost so the capture dialog can come to front and receive focus.
                    bool prevTop = this.TopMost;
                    this.TopMost = false;
                    _overlay.PauseTopmost();
                    using (var cap = new HotkeyCaptureForm(name, curMod, curVk))
                    {
                        var dr = cap.ShowDialog(this);
                        if (dr == DialogResult.OK)
                        {
                            if (hkId >= 0 && hkId < _overlay._hkMods.Length)
                            {
                                _overlay._hkMods[hkId] = cap.ResultMod;
                                _overlay._hkKeys[hkId] = cap.ResultVk;
                                _overlay.ReRegisterHotkeys();
                                _overlay.SaveSettings();
                            }
                            BuildItems(); ComputeLayout(); Invalidate();
                        }
                    }
                    _overlay.ResumeTopmost();
                    this.TopMost = prevTop;
                    this.Activate();
                }
                catch (Exception ex)
                {
                    try { _overlay.ResumeTopmost(); } catch { }
                    MessageBox.Show(this,
                        "Не удалось открыть окно назначения хоткея:\n" + ex.GetType().Name + ": " + ex.Message + "\n\n" + ex.StackTrace,
                        "Crosshair Overlay", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }, $"Нажмите чтобы изменить хоткей «{name}»");
        }

        private void AddSpacer()
        {
            _items.Add(new UiItem { Type = UiType.Spacer, Height = 12 });
        }

        private void ComputeLayout()
        {
            int y = HeaderHeight + 4;
            foreach (var item in _items)
            {
                item.Y = y;
                y += item.Height + 2;
            }
            _contentHeight = y + 20;
        }

        #endregion

        #region Animation

        internal void SlideIn()
        {
            _slidingIn = true;
            _sliding = true;
            this.Visible = true;
            _animTimer.Start();
        }

        internal void SlideOut()
        {
            _slidingIn = false;
            _sliding = true;
            _animTimer.Start();
        }

        private void AnimTick(object? sender, EventArgs e)
        {
            if (_sliding)
            {
                double target = _slidingIn ? 1.0 : 0.0;
                _slideProgress += (target - _slideProgress) * 0.18;

                if (Math.Abs(_slideProgress - target) < 0.005)
                {
                    _slideProgress = target;
                    _sliding = false;
                    if (!_slidingIn)
                    {
                        _animTimer.Stop();
                        this.Visible = false;
                        return;
                    }
                }

                int x = (int)(-PanelWidth * (1.0 - _slideProgress));
                this.Location = new Point(x, 0);
                Invalidate();
            }
            else
            {
                _animTimer.Stop();
            }
        }

        #endregion

        #region Painting

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var clientRect = this.ClientRectangle;

            // Draw cached background (expensive gradients/glow rendered once)
            EnsureBgCache(clientRect);
            g.DrawImageUnscaled(_bgCache!, 0, 0);

            // Header
            DrawHeader(g);

            // Separator — gradient line
            int sepY = HeaderHeight;
            using (var sepBrush = new LinearGradientBrush(
                new Point(ItemPadX, sepY), new Point(PanelWidth - ItemPadX, sepY),
                Color.FromArgb(90, AccentGlow), Color.FromArgb(0, Accent)))
            {
                g.FillRectangle(sepBrush, ItemPadX, sepY, PanelWidth - ItemPadX * 2, 1);
            }

            // Clip to content area
            g.SetClip(new Rectangle(0, HeaderHeight + 4, PanelWidth, clientRect.Height - HeaderHeight - 4));
            g.TranslateTransform(0, -_scrollY);

            // Update live values
            foreach (var item in _items)
            {
                if (item.Type == UiType.Info && item.Label == Lang.ClickCounter)
                    item.InfoValue = _overlay.GetClickCounter().ToString("N0");
            }

            // Draw items
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                int drawY = item.Y;

                if (drawY + item.Height - _scrollY < HeaderHeight || drawY - _scrollY > clientRect.Height)
                    continue;

                bool hovered = (i == _hoverIndex);
                DrawItem(g, item, i, hovered);
            }

            g.ResetTransform();
            g.ResetClip();

            DrawScrollbar(g, clientRect);
        }

        private void EnsureBgCache(Rectangle clientRect)
        {
            if (_bgCache != null && !_bgDirty) return;

            _bgCache?.Dispose();
            _bgCache = new Bitmap(clientRect.Width, Math.Max(1, clientRect.Height),
                System.Drawing.Imaging.PixelFormat.Format32bppPArgb);

            using var g = Graphics.FromImage(_bgCache);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Deep background
            using (var bgBrush = new SolidBrush(BgColor))
                g.FillRectangle(bgBrush, clientRect);

            // Liquid glass radial glow
            using (var glowPath = new GraphicsPath())
            {
                glowPath.AddEllipse(-80, -80, PanelWidth + 60, 400);
                using var glowBrush = new PathGradientBrush(glowPath)
                {
                    CenterColor = Color.FromArgb(25, 140, 100, 255),
                    SurroundColors = new[] { Color.FromArgb(0, 140, 100, 255) },
                    CenterPoint = new PointF(PanelWidth * 0.3f, 60)
                };
                g.FillPath(glowBrush, glowPath);
            }

            // Right edge — glass reflection
            using (var edgeBrush = new LinearGradientBrush(
                new Point(PanelWidth - 4, 0), new Point(PanelWidth, 0),
                Color.FromArgb(0, 180, 140, 255), Color.FromArgb(40, 180, 140, 255)))
            {
                g.FillRectangle(edgeBrush, PanelWidth - 2, 0, 2, clientRect.Height);
            }

            // Left accent line
            using (var accentBrush = new LinearGradientBrush(
                new Point(0, 0), new Point(0, clientRect.Height),
                Color.FromArgb(150, AccentGlow), Color.FromArgb(10, Accent)))
            {
                g.FillRectangle(accentBrush, 0, 0, 2, clientRect.Height);
            }

            _bgDirty = false;
        }

        private void DrawHeader(Graphics g)
        {
            // Glass card behind header title
            var headerRect = new Rectangle(8, 8, PanelWidth - 16, 50);
            DrawGlassCard(g, headerRect, 16);

            using var titleBrush = new SolidBrush(Color.White);
            g.DrawString(Lang.HeaderTitle, _fontTitle, titleBrush, 20, 14);

            using var dimBrush = new SolidBrush(TextMuted);
            g.DrawString(Lang.HeaderSub + "  v" + OverlayForm.APP_VERSION, _fontSmall, dimBrush, 22, 40);

            // Close button — glass pill
            var closeBtnRect = new Rectangle(PanelWidth - 48, 14, 32, 32);
            bool closeHover = closeBtnRect.Contains(PointToClient(Cursor.Position));
            using (var closePath = RoundRect(closeBtnRect, 16))
            {
                using var closeFill = new SolidBrush(closeHover
                    ? Color.FromArgb(140, 200, 60, 90)
                    : Color.FromArgb(40, 255, 255, 255));
                g.FillPath(closeFill, closePath);
            }
            using var closeFg = new SolidBrush(closeHover ? Color.White : TextDim);
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("✕", _fontClose, closeFg, closeBtnRect, sf);

            DrawTabStrip(g);
        }

        private Rectangle GetTabRect(int tabIndex)
        {
            int count = 6;
            int tabW = (PanelWidth - 8 - 8) / count;
            int x = 8 + tabIndex * tabW;
            int y = TabStripY;
            return new Rectangle(x, y + 2, tabW - 2, 32);
        }

        private void DrawTabStrip(Graphics g)
        {
            var names = Lang.IsRussian ? TabIconsRu : TabIconsEn;
            Point mouse = PointToClient(Cursor.Position);
            bool searching = !string.IsNullOrWhiteSpace(_searchText);
            for (int i = 0; i < names.Length; i++)
            {
                var r = GetTabRect(i);
                bool active = !searching && (int)_currentTab == i;
                bool hover = r.Contains(mouse);
                using var path = RoundRect(r, 8);
                using var fill = new SolidBrush(active
                    ? Color.FromArgb(180, Accent)
                    : hover ? Color.FromArgb(60, AccentGlow)
                            : Color.FromArgb(25, 255, 255, 255));
                g.FillPath(fill, path);
                using var border = new Pen(active ? AccentGlow : Color.FromArgb(40, AccentGlow), 1f);
                g.DrawPath(border, path);
                using var textBrush = new SolidBrush(active ? Color.White : TextMain);
                var sf2 = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(names[i], _fontSmall, textBrush, r, sf2);
            }
            if (searching)
            {
                using var hint = new SolidBrush(Color.FromArgb(220, 255, 200, 80));
                g.DrawString("🔎 \"" + _searchText + "\" (Esc)", _fontSmall, hint, 12, TabStripY + 20);
            }
        }

        private void DrawGlassCard(Graphics g, Rectangle rect, int radius)
        {
            using var cardPath = RoundRect(rect, radius);

            using (var glassFill = new SolidBrush(GlassBg))
                g.FillPath(glassFill, cardPath);

            using var borderPen = new Pen(GlassBorder, 1f);
            g.DrawPath(borderPen, cardPath);
        }

        private void DrawItem(Graphics g, UiItem item, int index, bool hovered)
        {
            int x = ItemPadX;
            int y = item.Y;
            int w = PanelWidth - ItemPadX * 2;

            switch (item.Type)
            {
                case UiType.Section: DrawSection(g, item, x, y, w); break;
                case UiType.Slider: DrawSliderItem(g, item, index, x, y, w, hovered); break;
                case UiType.Toggle: DrawToggleItem(g, item, x, y, w, hovered); break;
                case UiType.ColorPicker: DrawColorItem(g, item, x, y, w, hovered); break;
                case UiType.StyleSelector: DrawStyleItem(g, item, x, y, w, hovered); break;
                case UiType.Button: DrawButtonItem(g, item, x, y, w, hovered); break;
                case UiType.Info: DrawInfoItem(g, item, x, y, w, hovered); break;
                case UiType.NumericInput: DrawNumericItem(g, item, index, x, y, w, hovered); break;
            }
        }

        private void DrawSection(Graphics g, UiItem item, int x, int y, int w)
        {
            using var brush = new SolidBrush(SectionColor);
            g.DrawString(item.Label, _fontSection, brush, x + 2, y + 10);
            int textWidth = (int)g.MeasureString(item.Label, _fontSection).Width;
            using var linePen = new Pen(Color.FromArgb(50, Accent), 1f);
            g.DrawLine(linePen, x + textWidth + 10, y + 20, x + w, y + 20);
        }

        private void DrawControlBg(Graphics g, int x, int y, int w, int h, bool hovered)
        {
            var rect = new Rectangle(x, y, w, h);
            using var path = RoundRect(rect, ControlRadius);

            using var fill = new SolidBrush(hovered ? CardHover : CardBg);
            g.FillPath(fill, path);

            using var borderPen = new Pen(hovered
                ? Color.FromArgb(50, AccentGlow)
                : Color.FromArgb(25, 180, 140, 255), 1f);
            g.DrawPath(borderPen, path);
        }

        private void DrawSliderItem(Graphics g, UiItem item, int index, int x, int y, int w, bool hovered)
        {
            DrawControlBg(g, x, y, w, item.Height, hovered);

            // Label
            using var textBrush = new SolidBrush(TextMain);
            g.DrawString(item.Label, _fontControl, textBrush, x + 12, y + 6);

            // Value badge
            string valStr = item.SliderValue.ToString();
            var valSize = g.MeasureString(valStr, _fontValue);
            int badgeX = x + w - (int)valSize.Width - 20;
            int badgeY = y + 4;
            var badgeRect = new Rectangle(badgeX, badgeY, (int)valSize.Width + 12, 18);
            using (var badgePath = RoundRect(badgeRect, 9))
            {
                using var badgeFill = new SolidBrush(Color.FromArgb(60, Accent));
                g.FillPath(badgeFill, badgePath);
            }
            using var valBrush = new SolidBrush(AccentGlow);
            g.DrawString(valStr, _fontValue, valBrush, badgeX + 6, badgeY + 1);

            int trackX = x + 14;
            int trackY = y + 30;
            int trackW = w - 28;
            int trackH = 6;

            using var trackPath = RoundRect(new Rectangle(trackX, trackY, trackW, trackH), 3);
            using var trackBrush = new SolidBrush(SliderTrack);
            g.FillPath(trackBrush, trackPath);

            float ratio = (float)(item.SliderValue - item.SliderMin) / Math.Max(1, item.SliderMax - item.SliderMin);
            int fillW = Math.Max(1, (int)(trackW * ratio));
            using var fillPath = RoundRect(new Rectangle(trackX, trackY, fillW, trackH), 3);
            using var fillBrush = new LinearGradientBrush(
                new Point(trackX, 0), new Point(trackX + trackW, 0), Accent, AccentGlow);
            g.FillPath(fillBrush, fillPath);

            // Glass thumb
            int thumbX = trackX + fillW;
            int thumbR = 8;
            bool active = hovered || _activeSlider == index;

            if (active)
            {
                using var glowBrush = new SolidBrush(Color.FromArgb(30, AccentGlow));
                g.FillEllipse(glowBrush, thumbX - thumbR - 4, trackY + trackH / 2 - thumbR - 4,
                    (thumbR + 4) * 2, (thumbR + 4) * 2);
            }

            var thumbRect = new Rectangle(thumbX - thumbR, trackY + trackH / 2 - thumbR, thumbR * 2, thumbR * 2);
            using var thumbFill = new SolidBrush(active ? AccentGlow : Color.FromArgb(200, 180, 255));
            g.FillEllipse(thumbFill, thumbRect);
        }

        private void DrawToggleItem(Graphics g, UiItem item, int x, int y, int w, bool hovered)
        {
            DrawControlBg(g, x, y, w, item.Height, hovered);

            using var textBrush = new SolidBrush(TextMain);
            g.DrawString(item.Label, _fontControl, textBrush, x + 12, y + 11);

            int swX = x + w - 56;
            int swY = y + 9;
            int swW = 44;
            int swH = 22;

            using var swPath = RoundRect(new Rectangle(swX, swY, swW, swH), swH / 2);

            if (item.ToggleValue)
            {
                using var onBrush = new LinearGradientBrush(
                    new Point(swX, swY), new Point(swX + swW, swY),
                    Accent, AccentGlow);
                g.FillPath(onBrush, swPath);
            }
            else
            {
                using var offBrush = new SolidBrush(ToggleOff);
                g.FillPath(offBrush, swPath);
            }

            using var swBorder = new Pen(Color.FromArgb(60, AccentGlow), 1f);
            g.DrawPath(swBorder, swPath);

            int knobX = item.ToggleValue ? swX + swW - swH + 2 : swX + 2;
            int knobSize = swH - 4;
            var knobRect = new Rectangle(knobX, swY + 2, knobSize, knobSize);
            using var knobBrush = new SolidBrush(item.ToggleValue
                ? Color.FromArgb(240, 230, 245, 255)
                : Color.FromArgb(200, 160, 155, 180));
            g.FillEllipse(knobBrush, knobRect);
        }

        private void DrawColorItem(Graphics g, UiItem item, int x, int y, int w, bool hovered)
        {
            DrawControlBg(g, x, y, w, item.Height, hovered);

            using var textBrush = new SolidBrush(TextMain);
            g.DrawString(item.Label, _fontControl, textBrush, x + 10, y + 10);

            int swX = x + w - 80;
            int swY = y + 8;
            var swRect = new Rectangle(swX, swY, 24, 22);
            using var swPath = RoundRect(swRect, 4);
            using var colorBrush = new SolidBrush(item.ColorValue);
            g.FillPath(colorBrush, swPath);
            using var swBorder = new Pen(BtnBorder, 1f);
            g.DrawPath(swBorder, swPath);

            string hex = $"#{item.ColorValue.R:X2}{item.ColorValue.G:X2}{item.ColorValue.B:X2}";
            using var hexBrush = new SolidBrush(TextDim);
            g.DrawString(hex, _fontSmall, hexBrush, swX + 28, swY + 4);
        }

        private void DrawStyleItem(Graphics g, UiItem item, int x, int y, int w, bool hovered)
        {
            DrawControlBg(g, x, y, w, item.Height, hovered);

            if (item.StyleNames == null) return;

            if (item.IsThemeSelector)
            {
                // Draw gradient swatches instead of text labels
                int btnW = 32, btnH = 28, gap = 4;
                int totalW = item.StyleNames.Length * (btnW + gap) - gap;
                int startX = x + (w - totalW) / 2;
                int btnY = y + (item.Height - btnH) / 2;

                for (int si = 0; si < item.StyleNames.Length; si++)
                {
                    int bx = startX + si * (btnW + gap);
                    if (bx + btnW > x + w) break;

                    bool selected = si == item.SelectedStyle;
                    var btnRect = new Rectangle(bx, btnY, btnW, btnH);
                    using var btnPath = RoundRect(btnRect, 8);

                    // Draw gradient swatch
                    var palette = item.GradientPalette ?? ThemeGradients;
                    Color c1 = si < palette.Length ? palette[si][0] : Color.Gray;
                    Color c2 = si < palette.Length ? palette[si][1] : Color.DarkGray;
                    using (var gradBrush = new LinearGradientBrush(btnRect, c1, c2, 45f))
                        g.FillPath(gradBrush, btnPath);

                    // Selected border glow
                    if (selected)
                    {
                        using var glowPen = new Pen(Color.FromArgb(200, 255, 255, 255), 2f);
                        g.DrawPath(glowPen, btnPath);
                    }
                    else
                    {
                        using var borderPen = new Pen(Color.FromArgb(40, 255, 255, 255), 1f);
                        g.DrawPath(borderPen, btnPath);
                    }
                }
            }
            else
            {
                // Original text-based style buttons
                using var textBrush = new SolidBrush(TextMain);
                g.DrawString(item.Label, _fontControl, textBrush, x + 10, y + 4);

                int btnW = 20, btnH = 22, btnY = y + 26, gap = 2, startX = x + 6;

                for (int si = 0; si < item.StyleNames.Length; si++)
                {
                    int bx = startX + si * (btnW + gap);
                    if (bx + btnW > x + w) break;

                    bool selected = si == item.SelectedStyle;
                    var btnRect = new Rectangle(bx, btnY, btnW, btnH);
                    using var btnPath = RoundRect(btnRect, 8);

                    if (selected)
                    {
                        using var selBrush = new LinearGradientBrush(btnRect, Accent, AccentGlow, 45f);
                        g.FillPath(selBrush, btnPath);
                    }
                    else
                    {
                        using var btnBrush = new SolidBrush(Color.FromArgb(40, 50, 30, 80));
                        g.FillPath(btnBrush, btnPath);
                    }

                    using var btnBorder = new Pen(selected
                        ? Color.FromArgb(120, AccentGlow)
                        : Color.FromArgb(30, 180, 140, 255), 1f);
                    g.DrawPath(btnBorder, btnPath);

                    using var sBrush = new SolidBrush(selected ? Color.White : TextMain);
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(item.StyleNames[si], _fontSmall, sBrush, btnRect, sf);
                }
            }
        }

        private void DrawButtonItem(Graphics g, UiItem item, int x, int y, int w, bool hovered)
        {
            var rect = new Rectangle(x, y, w, item.Height);
            using var path = RoundRect(rect, ControlRadius);

            if (hovered)
            {
                using var hoverBrush = new LinearGradientBrush(rect, Accent, AccentGlow, 0f);
                g.FillPath(hoverBrush, path);
            }
            else
            {
                using var fill = new SolidBrush(Color.FromArgb(50, 40, 20, 75));
                g.FillPath(fill, path);
            }

            using var border = new Pen(hovered
                ? Color.FromArgb(120, AccentGlow)
                : BtnBorder, 1f);
            g.DrawPath(border, path);

            using var textBrush = new SolidBrush(hovered ? Color.White : AccentGlow);
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(item.Label, _fontControl, textBrush, rect, sf);
        }

        private void DrawInfoItem(Graphics g, UiItem item, int x, int y, int w, bool hovered)
        {
            DrawControlBg(g, x, y, w, item.Height, hovered);

            using var labelBrush = new SolidBrush(TextDim);
            g.DrawString(item.Label, _fontSmall, labelBrush, x + 10, y + 4);

            using var valueBrush = new SolidBrush(item.InfoColor);
            g.DrawString(item.InfoValue, _fontControl, valueBrush, x + 10, y + 18);
        }

        private void DrawNumericItem(Graphics g, UiItem item, int index, int x, int y, int w, bool hovered)
        {
            DrawControlBg(g, x, y, w, item.Height, hovered);

            using var textBrush = new SolidBrush(TextMain);
            g.DrawString(item.Label, _fontControl, textBrush, x + 10, y + 11);

            // [-] button
            int btnW = 28, btnH = 24;
            int minusBtnX = x + w - 140;
            int btnY = y + 8;
            var minusRect = new Rectangle(minusBtnX, btnY, btnW, btnH);
            using (var minusPath = RoundRect(minusRect, 4))
            {
                using var minusFill = new SolidBrush(hovered ? ControlHover : ControlBg);
                g.FillPath(minusFill, minusPath);
                using var minusBorder = new Pen(BtnBorder, 1f);
                g.DrawPath(minusBorder, minusPath);
            }
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using var btnTextBrush = new SolidBrush(AccentGlow);
            g.DrawString("\u2212", _fontControl, btnTextBrush, minusRect, sf);

            // Value display (clickable)
            int valX = minusBtnX + btnW + 4;
            int valW = 50;
            var valRect = new Rectangle(valX, btnY, valW, btnH);
            using (var valPath = RoundRect(valRect, 4))
            {
                using var valFill = new SolidBrush(Color.FromArgb(200, 30, 15, 55));
                g.FillPath(valFill, valPath);
                using var valBorder = new Pen(Accent, 1f);
                g.DrawPath(valBorder, valPath);
            }
            if (_editingIndex != index) // don't draw text when editing
            {
                using var valBrush = new SolidBrush(Color.White);
                g.DrawString(item.SliderValue.ToString(), _fontControl, valBrush, valRect, sf);
            }

            // [+] button
            int plusBtnX = valX + valW + 4;
            var plusRect = new Rectangle(plusBtnX, btnY, btnW, btnH);
            using (var plusPath = RoundRect(plusRect, 4))
            {
                using var plusFill = new SolidBrush(hovered ? ControlHover : ControlBg);
                g.FillPath(plusFill, plusPath);
                using var plusBorder = new Pen(BtnBorder, 1f);
                g.DrawPath(plusBorder, plusPath);
            }
            g.DrawString("+", _fontControl, btnTextBrush, plusRect, sf);
        }

        private void DrawScrollbar(Graphics g, Rectangle clientRect)
        {
            int viewH = clientRect.Height - HeaderHeight - 4;
            if (_contentHeight <= viewH) return;

            float ratio = (float)viewH / _contentHeight;
            int barH = Math.Max(40, (int)(viewH * ratio));
            int maxScroll = _contentHeight - viewH;
            float scrollRatio = maxScroll > 0 ? (float)_scrollY / maxScroll : 0;
            int barY = HeaderHeight + 4 + (int)((viewH - barH) * scrollRatio);

            // Track (subtle)
            var trackRect = new Rectangle(PanelWidth - ScrollbarRightPad - ScrollbarWidth, HeaderHeight + 4, ScrollbarWidth, viewH);
            using (var trackBrush = new SolidBrush(Color.FromArgb(25, 255, 255, 255)))
            using (var trackPath = RoundRect(trackRect, ScrollbarWidth / 2))
                g.FillPath(trackBrush, trackPath);

            // Thumb
            int thumbAlpha = _draggingScrollbar ? 220 : 140;
            using var barBrush = new SolidBrush(Color.FromArgb(thumbAlpha, AccentGlow));
            var barRect = new Rectangle(PanelWidth - ScrollbarRightPad - ScrollbarWidth, barY, ScrollbarWidth, barH);
            using var barPath = RoundRect(barRect, ScrollbarWidth / 2);
            g.FillPath(barBrush, barPath);
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

        #endregion

        #region Input

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_draggingScrollbar)
            {
                HandleScrollbarDrag(e.Y);
                return;
            }

            if (_draggingSlider && _activeSlider >= 0 && _activeSlider < _items.Count)
            {
                HandleSliderDrag(e);
                return;
            }

            int oldHover = _hoverIndex;
            _hoverIndex = HitTest(e.Location);
            if (oldHover != _hoverIndex)
                Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            // Right-click on a slider = reset to default/mid (#77)
            if (e.Button == MouseButtons.Right)
            {
                int idxR = HitTest(e.Location);
                if (idxR >= 0 && idxR < _items.Count)
                {
                    var itR = _items[idxR];
                    if (itR.Type == UiType.Slider || itR.Type == UiType.NumericInput)
                    {
                        int def = (itR.SliderMin + itR.SliderMax) / 2;
                        if (itR.SliderMin == 0) def = 0; // for 0-based limits reset to 0
                        itR.SliderValue = def;
                        itR.OnSliderChanged?.Invoke(def);
                        _overlay._needsStaticRender = true;
                        _overlay.SaveSettings();
                        Invalidate();
                    }
                }
                return;
            }

            if (e.Button != MouseButtons.Left) return;

            // Tab strip click (#79 UI)
            if (e.Y >= TabStripY && e.Y < HeaderHeight)
            {
                for (int i = 0; i < 6; i++)
                {
                    var r = GetTabRect(i);
                    if (r.Contains(e.Location))
                    {
                        _currentTab = (Tab)i;
                        _searchText = "";
                        _scrollY = 0;
                        BuildItems(); ComputeLayout(); Invalidate();
                        return;
                    }
                }
            }

            // Close button
            var closeBtnRect = new Rectangle(PanelWidth - 48, 14, 32, 32);
            if (closeBtnRect.Contains(e.Location))
            {
                SlideOut();
                return;
            }

            // Scrollbar drag
            int viewH = this.Height - HeaderHeight - 4;
            if (_contentHeight > viewH && e.X >= PanelWidth - ScrollbarHitPad)
            {
                // Compute current thumb geometry
                float ratio = (float)viewH / _contentHeight;
                int barH = Math.Max(40, (int)(viewH * ratio));
                int maxScroll = _contentHeight - viewH;
                float scrollRatio = maxScroll > 0 ? (float)_scrollY / maxScroll : 0;
                int barY = HeaderHeight + 4 + (int)((viewH - barH) * scrollRatio);

                _draggingScrollbar = true;
                if (e.Y >= barY && e.Y < barY + barH)
                {
                    // Grabbed on thumb — remember offset so thumb doesn't jump
                    _scrollGrabOffset = e.Y - barY;
                }
                else
                {
                    // Clicked on track — center thumb under cursor
                    _scrollGrabOffset = barH / 2;
                }
                HandleScrollbarDrag(e.Y);
                return;
            }

            int idx = HitTest(e.Location);
            if (idx < 0 || idx >= _items.Count) return;

            var item = _items[idx];
            int localY = e.Y + _scrollY;

            switch (item.Type)
            {
                case UiType.Slider:
                    _activeSlider = idx;
                    _draggingSlider = true;
                    HandleSliderDrag(e);
                    break;

                case UiType.Toggle:
                    item.ToggleValue = !item.ToggleValue;
                    item.OnToggleChanged?.Invoke(item.ToggleValue);
                    _overlay._needsStaticRender = true;
                    _overlay.SaveSettings();
                    Invalidate();
                    break;

                case UiType.ColorPicker:
                    using (var dlg = new ColorDialog { Color = item.ColorValue })
                    {
                        if (dlg.ShowDialog() == DialogResult.OK)
                        {
                            item.ColorValue = dlg.Color;
                            item.OnColorChanged?.Invoke(dlg.Color);
                            _overlay._needsStaticRender = true;
                            _overlay.SaveSettings();
                            Invalidate();
                        }
                    }
                    break;

                case UiType.StyleSelector:
                    if (item.StyleNames != null)
                    {
                        int clickX = e.X;
                        int clickY = localY;

                        int btnW, btnH, gap, startX, btnY;
                        if (item.IsThemeSelector)
                        {
                            btnW = 32; btnH = 28; gap = 4;
                            int w = PanelWidth - ItemPadX * 2;
                            int totalW = item.StyleNames.Length * (btnW + gap) - gap;
                            startX = ItemPadX + (w - totalW) / 2;
                            btnY = item.Y + (item.Height - btnH) / 2;
                        }
                        else
                        {
                            btnW = 20; gap = 2; startX = ItemPadX + 6;
                            btnH = 22; btnY = item.Y + 26;
                        }

                        if (clickY >= btnY && clickY <= btnY + btnH)
                        {
                            for (int si = 0; si < item.StyleNames.Length; si++)
                            {
                                int bx = startX + si * (btnW + gap);
                                if (clickX >= bx && clickX <= bx + btnW)
                                {
                                    item.SelectedStyle = si;
                                    item.OnStyleChanged?.Invoke(si);
                                    _overlay._needsStaticRender = true;
                                    _overlay.SaveSettings();
                                    Invalidate();
                                    break;
                                }
                            }
                        }
                    }
                    break;

                case UiType.Button:
                    item.OnClick?.Invoke();
                    break;

                case UiType.NumericInput:
                    HandleNumericClick(idx, item, e.X, localY);
                    break;
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_draggingScrollbar)
            {
                _draggingScrollbar = false;
                Invalidate();
            }
            if (_draggingSlider)
            {
                _draggingSlider = false;
                _activeSlider = -1;
                Invalidate();
            }
        }

        private void HandleScrollbarDrag(int mouseY)
        {
            int viewH = this.Height - HeaderHeight - 4;
            int maxScroll = Math.Max(1, _contentHeight - viewH);
            float ratio = (float)viewH / _contentHeight;
            int barH = Math.Max(40, (int)(viewH * ratio));
            // Convert mouse Y to thumb top (subtract grab offset), then into scroll position.
            int trackTop = HeaderHeight + 4;
            int thumbTop = mouseY - trackTop - _scrollGrabOffset;
            int thumbTravel = Math.Max(1, viewH - barH);
            _scrollY = Math.Clamp((int)((float)thumbTop / thumbTravel * maxScroll), 0, maxScroll);
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);

            // Wheel scrolls the list. Sliders are changed only by click/drag
            // (avoids accidental edits when scrolling over a control).
            int viewH = this.Height - HeaderHeight - 4;
            int maxScroll = Math.Max(0, _contentHeight - viewH);
            _scrollY = Math.Clamp(_scrollY - e.Delta / 2, 0, maxScroll);
            Invalidate();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Ctrl+F → start/focus search
            if (keyData == (Keys.Control | Keys.F))
            {
                FocusSearch();
                return true;
            }
            if (keyData == Keys.Escape)
            {
                if (!string.IsNullOrEmpty(_searchText))
                {
                    _searchText = "";
                    _searchBox?.Hide();
                    BuildItems(); ComputeLayout(); Invalidate();
                    return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void FocusSearch()
        {
            if (_searchBox == null)
            {
                _searchBox = new TextBox
                {
                    Location = new Point(10, TabStripY + 2),
                    Size = new Size(PanelWidth - 60, 30),
                    BackColor = Color.FromArgb(30, 15, 55),
                    ForeColor = Color.White,
                    Font = _fontControl,
                    BorderStyle = BorderStyle.FixedSingle
                };
                _searchBox.TextChanged += (s, ev) =>
                {
                    _searchText = _searchBox!.Text;
                    BuildItems(); ComputeLayout(); Invalidate();
                };
                _searchBox.KeyDown += (s, ev) =>
                {
                    if (ev.KeyCode == Keys.Escape)
                    {
                        _searchBox!.Text = "";
                        _searchBox.Hide();
                        ev.SuppressKeyPress = true;
                    }
                };
                Controls.Add(_searchBox);
            }
            _searchBox.Show();
            _searchBox.BringToFront();
            _searchBox.Focus();
            _searchBox.SelectAll();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverIndex >= 0)
            {
                _hoverIndex = -1;
                Invalidate();
            }
        }

        private void HandleSliderDrag(MouseEventArgs e)
        {
            if (_activeSlider < 0 || _activeSlider >= _items.Count) return;
            var item = _items[_activeSlider];

            int trackX = ItemPadX + 14;
            int trackW = PanelWidth - ItemPadX * 2 - 28;

            float ratio = Math.Clamp((float)(e.X - trackX) / trackW, 0f, 1f);
            int rawValue = item.SliderMin + (int)(ratio * (item.SliderMax - item.SliderMin));

            int stepped = item.SliderMin + ((rawValue - item.SliderMin + item.SliderStep / 2) / item.SliderStep) * item.SliderStep;
            stepped = Math.Clamp(stepped, item.SliderMin, item.SliderMax);

            if (stepped != item.SliderValue)
            {
                item.SliderValue = stepped;
                item.OnSliderChanged?.Invoke(stepped);
                _overlay._needsStaticRender = true;
                _overlay.SaveSettings();
                Invalidate();
            }
        }

        private void HandleNumericClick(int idx, UiItem item, int clickX, int clickY)
        {
            int w = PanelWidth - ItemPadX * 2;
            int btnW = 28;
            int minusBtnX = ItemPadX + w - 140;
            int valX = minusBtnX + btnW + 4;
            int valW = 50;
            int plusBtnX = valX + valW + 4;
            int btnY = item.Y + 8;

            if (clickY >= btnY && clickY <= btnY + 24)
            {
                // [-] button
                if (clickX >= minusBtnX && clickX <= minusBtnX + btnW)
                {
                    int newVal = Math.Max(item.SliderMin, item.SliderValue - 1);
                    if (newVal != item.SliderValue)
                    {
                        item.SliderValue = newVal;
                        item.OnSliderChanged?.Invoke(newVal);
                        _overlay.SaveSettings();
                        Invalidate();
                    }
                }
                // [+] button
                else if (clickX >= plusBtnX && clickX <= plusBtnX + btnW)
                {
                    int newVal = Math.Min(item.SliderMax, item.SliderValue + 1);
                    if (newVal != item.SliderValue)
                    {
                        item.SliderValue = newVal;
                        item.OnSliderChanged?.Invoke(newVal);
                        _overlay.SaveSettings();
                        Invalidate();
                    }
                }
                // Value box — open inline editor
                else if (clickX >= valX && clickX <= valX + valW)
                {
                    ShowInlineEditor(idx, item, valX, btnY - _scrollY + HeaderHeight + 4);
                }
            }
        }

        private void ShowInlineEditor(int idx, UiItem item, int x, int screenY)
        {
            CloseInlineEditor();
            _editingIndex = idx;

            _editBox = new TextBox
            {
                Text = item.SliderValue.ToString(),
                Font = _fontControl,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(30, 15, 55),
                BorderStyle = BorderStyle.FixedSingle,
                TextAlign = HorizontalAlignment.Center,
                Location = new Point(x, screenY),
                Size = new Size(50, 24)
            };
            _editBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    ApplyInlineEdit(item);
                    e.SuppressKeyPress = true;
                }
                else if (e.KeyCode == Keys.Escape)
                {
                    CloseInlineEditor();
                }
            };
            _editBox.LostFocus += (s, e) => { if (_editBox != null) ApplyInlineEdit(item); };
            this.Controls.Add(_editBox);
            _editBox.BringToFront();
            _editBox.Focus();
            _editBox.SelectAll();
        }

        private void ApplyInlineEdit(UiItem item)
        {
            if (_applyingEdit || _editBox == null) return;
            _applyingEdit = true;
            try
            {
                if (int.TryParse(_editBox.Text, out int val))
                {
                    val = Math.Clamp(val, item.SliderMin, item.SliderMax);
                    item.SliderValue = val;
                    item.OnSliderChanged?.Invoke(val);
                    _overlay.SaveSettings();
                }
                CloseInlineEditor();
                Invalidate();
            }
            finally { _applyingEdit = false; }
        }

        private void CloseInlineEditor()
        {
            if (_editBox != null)
            {
                this.Controls.Remove(_editBox);
                _editBox.Dispose();
                _editBox = null;
            }
            _editingIndex = -1;
        }

        private int HitTest(Point clientPos)
        {
            int y = clientPos.Y + _scrollY;
            if (clientPos.Y <= HeaderHeight) return -1;

            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                if (item.Type == UiType.Spacer || item.Type == UiType.Section) continue;
                if (y >= item.Y && y < item.Y + item.Height)
                    return i;
            }
            return -1;
        }

        #endregion

        #region Tooltip

        private int _lastTooltipIndex = -1;

        protected override void OnMouseHover(EventArgs e)
        {
            base.OnMouseHover(e);
            if (_hoverIndex >= 0 && _hoverIndex < _items.Count && _hoverIndex != _lastTooltipIndex)
            {
                _lastTooltipIndex = _hoverIndex;
                var item = _items[_hoverIndex];
                if (!string.IsNullOrEmpty(item.Tooltip))
                {
                    var pos = PointToClient(Cursor.Position);
                    _toolTip.Show(item.Tooltip, this, pos.X + 10, pos.Y + 20, 3000);
                }
            }
        }

        #endregion

        internal void UpdateClickerStatus()
        {
            foreach (var item in _items)
            {
                if (item.Type == UiType.Toggle && item.Label == Lang.EnableAutoclicker)
                    item.ToggleValue = _overlay._autoClickerEnabled;
            }
            if (this.Visible)
                Invalidate();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _animTimer.Stop();
            _animTimer.Dispose();
            _toolTip.Dispose();
            _bgCache?.Dispose();
            _fontTitle.Dispose();
            _fontSection.Dispose();
            _fontControl.Dispose();
            _fontSmall.Dispose();
            _fontClose.Dispose();
            _fontValue.Dispose();
            base.OnFormClosed(e);
        }

        #region Themes

        internal void ApplyTheme(int index)
        {
            _currentTheme = Math.Clamp(index, 0, ThemeNames.Length - 1);
            switch (_currentTheme)
            {
                case 0: // Пурпур (по умолчанию)
                    BgColor = Color.FromArgb(220, 12, 6, 24);
                    GlassBg = Color.FromArgb(35, 140, 100, 220);
                    GlassHighlight = Color.FromArgb(18, 255, 255, 255);
                    GlassBorder = Color.FromArgb(45, 180, 140, 255);
                    Accent = Color.FromArgb(130, 80, 220);
                    AccentGlow = Color.FromArgb(175, 130, 255);
                    TextMain = Color.FromArgb(235, 228, 245);
                    TextDim = Color.FromArgb(130, 120, 155);
                    TextMuted = Color.FromArgb(90, 85, 110);
                    ControlBg = Color.FromArgb(50, 40, 20, 70);
                    ControlHover = Color.FromArgb(70, 60, 30, 110);
                    SliderTrack = Color.FromArgb(100, 35, 18, 60);
                    ToggleOn = Color.FromArgb(130, 80, 220);
                    ToggleOff = Color.FromArgb(50, 30, 16, 55);
                    SectionColor = Color.FromArgb(160, 130, 255);
                    BtnBorder = Color.FromArgb(70, 130, 80, 220);
                    CardBg = Color.FromArgb(40, 50, 30, 90);
                    CardHover = Color.FromArgb(55, 70, 40, 120);
                    break;
                case 1: // Кибер
                    BgColor = Color.FromArgb(220, 8, 4, 20);
                    GlassBg = Color.FromArgb(35, 220, 40, 140);
                    GlassHighlight = Color.FromArgb(18, 255, 255, 255);
                    GlassBorder = Color.FromArgb(45, 255, 60, 180);
                    Accent = Color.FromArgb(255, 50, 150);
                    AccentGlow = Color.FromArgb(255, 100, 200);
                    TextMain = Color.FromArgb(240, 230, 245);
                    TextDim = Color.FromArgb(140, 110, 140);
                    TextMuted = Color.FromArgb(100, 80, 100);
                    ControlBg = Color.FromArgb(50, 60, 10, 50);
                    ControlHover = Color.FromArgb(70, 100, 20, 80);
                    SliderTrack = Color.FromArgb(100, 50, 10, 40);
                    ToggleOn = Color.FromArgb(255, 50, 150);
                    ToggleOff = Color.FromArgb(50, 40, 10, 50);
                    SectionColor = Color.FromArgb(255, 100, 200);
                    BtnBorder = Color.FromArgb(70, 255, 50, 150);
                    CardBg = Color.FromArgb(40, 60, 20, 70);
                    CardHover = Color.FromArgb(55, 100, 30, 100);
                    break;
                case 2: // Матрица
                    BgColor = Color.FromArgb(220, 2, 12, 2);
                    GlassBg = Color.FromArgb(35, 20, 180, 40);
                    GlassHighlight = Color.FromArgb(18, 200, 255, 200);
                    GlassBorder = Color.FromArgb(45, 40, 200, 60);
                    Accent = Color.FromArgb(0, 200, 40);
                    AccentGlow = Color.FromArgb(50, 255, 80);
                    TextMain = Color.FromArgb(200, 240, 210);
                    TextDim = Color.FromArgb(60, 140, 70);
                    TextMuted = Color.FromArgb(40, 100, 50);
                    ControlBg = Color.FromArgb(50, 10, 40, 15);
                    ControlHover = Color.FromArgb(70, 15, 70, 20);
                    SliderTrack = Color.FromArgb(100, 10, 35, 12);
                    ToggleOn = Color.FromArgb(0, 200, 40);
                    ToggleOff = Color.FromArgb(50, 10, 30, 10);
                    SectionColor = Color.FromArgb(80, 255, 100);
                    BtnBorder = Color.FromArgb(70, 0, 200, 40);
                    CardBg = Color.FromArgb(40, 15, 50, 20);
                    CardHover = Color.FromArgb(55, 20, 80, 30);
                    break;
                case 3: // Кровь
                    BgColor = Color.FromArgb(220, 18, 4, 4);
                    GlassBg = Color.FromArgb(35, 180, 30, 30);
                    GlassHighlight = Color.FromArgb(18, 255, 200, 200);
                    GlassBorder = Color.FromArgb(45, 200, 50, 50);
                    Accent = Color.FromArgb(220, 30, 30);
                    AccentGlow = Color.FromArgb(255, 80, 80);
                    TextMain = Color.FromArgb(245, 230, 230);
                    TextDim = Color.FromArgb(150, 100, 100);
                    TextMuted = Color.FromArgb(110, 70, 70);
                    ControlBg = Color.FromArgb(50, 50, 15, 15);
                    ControlHover = Color.FromArgb(70, 80, 20, 20);
                    SliderTrack = Color.FromArgb(100, 40, 12, 12);
                    ToggleOn = Color.FromArgb(220, 30, 30);
                    ToggleOff = Color.FromArgb(50, 35, 12, 12);
                    SectionColor = Color.FromArgb(255, 100, 100);
                    BtnBorder = Color.FromArgb(70, 220, 30, 30);
                    CardBg = Color.FromArgb(40, 60, 20, 20);
                    CardHover = Color.FromArgb(55, 90, 30, 30);
                    break;
                case 4: // Океан
                    BgColor = Color.FromArgb(220, 4, 8, 22);
                    GlassBg = Color.FromArgb(35, 30, 100, 200);
                    GlassHighlight = Color.FromArgb(18, 200, 230, 255);
                    GlassBorder = Color.FromArgb(45, 50, 140, 220);
                    Accent = Color.FromArgb(30, 120, 220);
                    AccentGlow = Color.FromArgb(80, 170, 255);
                    TextMain = Color.FromArgb(225, 235, 248);
                    TextDim = Color.FromArgb(90, 130, 170);
                    TextMuted = Color.FromArgb(60, 90, 130);
                    ControlBg = Color.FromArgb(50, 15, 30, 60);
                    ControlHover = Color.FromArgb(70, 20, 50, 90);
                    SliderTrack = Color.FromArgb(100, 12, 22, 50);
                    ToggleOn = Color.FromArgb(30, 120, 220);
                    ToggleOff = Color.FromArgb(50, 12, 20, 45);
                    SectionColor = Color.FromArgb(100, 180, 255);
                    BtnBorder = Color.FromArgb(70, 30, 120, 220);
                    CardBg = Color.FromArgb(40, 20, 40, 80);
                    CardHover = Color.FromArgb(55, 30, 60, 110);
                    break;
                case 5: // Закат
                    BgColor = Color.FromArgb(220, 20, 10, 4);
                    GlassBg = Color.FromArgb(35, 200, 120, 30);
                    GlassHighlight = Color.FromArgb(18, 255, 240, 200);
                    GlassBorder = Color.FromArgb(45, 220, 150, 50);
                    Accent = Color.FromArgb(230, 150, 30);
                    AccentGlow = Color.FromArgb(255, 200, 80);
                    TextMain = Color.FromArgb(248, 240, 225);
                    TextDim = Color.FromArgb(170, 130, 80);
                    TextMuted = Color.FromArgb(130, 95, 55);
                    ControlBg = Color.FromArgb(50, 50, 25, 10);
                    ControlHover = Color.FromArgb(70, 80, 40, 15);
                    SliderTrack = Color.FromArgb(100, 40, 20, 8);
                    ToggleOn = Color.FromArgb(230, 150, 30);
                    ToggleOff = Color.FromArgb(50, 40, 20, 8);
                    SectionColor = Color.FromArgb(255, 200, 100);
                    BtnBorder = Color.FromArgb(70, 230, 150, 30);
                    CardBg = Color.FromArgb(40, 60, 30, 15);
                    CardHover = Color.FromArgb(55, 90, 50, 20);
                    break;
                case 6: // Арктика
                    BgColor = Color.FromArgb(220, 18, 22, 32);
                    GlassBg = Color.FromArgb(35, 120, 180, 220);
                    GlassHighlight = Color.FromArgb(18, 255, 255, 255);
                    GlassBorder = Color.FromArgb(45, 140, 200, 240);
                    Accent = Color.FromArgb(100, 180, 230);
                    AccentGlow = Color.FromArgb(160, 215, 245);
                    TextMain = Color.FromArgb(230, 240, 248);
                    TextDim = Color.FromArgb(120, 150, 180);
                    TextMuted = Color.FromArgb(80, 110, 140);
                    ControlBg = Color.FromArgb(50, 25, 40, 55);
                    ControlHover = Color.FromArgb(70, 35, 60, 80);
                    SliderTrack = Color.FromArgb(100, 20, 35, 50);
                    ToggleOn = Color.FromArgb(100, 180, 230);
                    ToggleOff = Color.FromArgb(50, 20, 30, 45);
                    SectionColor = Color.FromArgb(140, 200, 240);
                    BtnBorder = Color.FromArgb(70, 100, 180, 230);
                    CardBg = Color.FromArgb(40, 30, 50, 70);
                    CardHover = Color.FromArgb(55, 45, 70, 95);
                    break;
                case 7: // Полночь
                    BgColor = Color.FromArgb(220, 6, 6, 18);
                    GlassBg = Color.FromArgb(35, 40, 50, 120);
                    GlassHighlight = Color.FromArgb(18, 180, 190, 255);
                    GlassBorder = Color.FromArgb(45, 60, 70, 160);
                    Accent = Color.FromArgb(60, 80, 180);
                    AccentGlow = Color.FromArgb(100, 130, 220);
                    TextMain = Color.FromArgb(210, 215, 235);
                    TextDim = Color.FromArgb(90, 100, 140);
                    TextMuted = Color.FromArgb(60, 65, 100);
                    ControlBg = Color.FromArgb(50, 18, 20, 50);
                    ControlHover = Color.FromArgb(70, 25, 30, 75);
                    SliderTrack = Color.FromArgb(100, 15, 16, 40);
                    ToggleOn = Color.FromArgb(60, 80, 180);
                    ToggleOff = Color.FromArgb(50, 14, 15, 38);
                    SectionColor = Color.FromArgb(110, 140, 230);
                    BtnBorder = Color.FromArgb(70, 60, 80, 180);
                    CardBg = Color.FromArgb(40, 22, 25, 60);
                    CardHover = Color.FromArgb(55, 35, 40, 85);
                    break;
            }
            _bgDirty = true;
            BuildItems();
            ComputeLayout();
            Invalidate();
        }

        #endregion
    }
}
