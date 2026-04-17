namespace CrosshairOverlay
{
    internal static class Lang
    {
        internal static bool IsRussian = true;

        internal static string Get(string ru, string en) => IsRussian ? ru : en;

        // ── HEADER ──
        internal static string HeaderTitle => Get("CODEX CROSSHAIR", "CODEX CROSSHAIR");
        internal static string HeaderSub => Get("v2.0  ·  INS чтобы скрыть", "v2.0  ·  INS to hide");

        // ── THEME ──
        internal static string SectionTheme => Get("──  ТЕМА  ──", "──  THEME  ──");
        internal static string ThemeLabel => Get("Тема интерфейса", "Interface theme");
        internal static string ThemeTooltip => Get("Выберите цветовую схему меню настроек", "Select color scheme for settings menu");

        // ── CROSSHAIR ──
        internal static string SectionCrosshair => Get("──  ПРИЦЕЛ  ──", "──  CROSSHAIR  ──");
        internal static string Size => Get("Размер", "Size");
        internal static string SizeTooltip => Get("Размер перекрестия прицела", "Crosshair size");
        internal static string Thickness => Get("Толщина", "Thickness");
        internal static string ThicknessTooltip => Get("Толщина линий перекрестия", "Crosshair line thickness");
        internal static string Gap => Get("Отступ", "Gap");
        internal static string GapTooltip => Get("Расстояние между линиями от центра", "Distance between lines from center");
        internal static string Opacity => Get("Прозрачность", "Opacity");
        internal static string OpacityTooltip => Get("Прозрачность прицела (10-255)", "Crosshair opacity (10-255)");
        internal static string OffsetX => Get("Смещение X", "Offset X");
        internal static string OffsetXTooltip => Get("Горизонтальное смещение прицела", "Horizontal crosshair offset");
        internal static string OffsetY => Get("Смещение Y", "Offset Y");
        internal static string OffsetYTooltip => Get("Вертикальное смещение прицела", "Vertical crosshair offset");
        internal static string CenterDot => Get("Точка в центре", "Center dot");
        internal static string CenterDotTooltip => Get("Показать/скрыть точку в центре прицела", "Show/hide center dot");
        internal static string DotSize => Get("Размер точки", "Dot size");
        internal static string DotSizeTooltip => Get("Размер центральной точки", "Center dot size");
        internal static string DotPulse => Get("Пульсация точки", "Dot pulse");
        internal static string DotPulseTooltip => Get("Анимация пульсации центральной точки", "Center dot pulse animation");
        internal static string CrossColor => Get("Цвет прицела", "Crosshair color");
        internal static string CrossColorTooltip => Get("Основной цвет перекрестия", "Main crosshair color");
        internal static string RainbowMode => Get("Радужный режим", "Rainbow mode");
        internal static string RainbowModeTooltip => Get("Автоматическая смена цветов (радуга)", "Automatic color cycling (rainbow)");
        internal static string Gradient => Get("Градиент", "Gradient");
        internal static string GradientTooltip => Get("Использовать градиент между двумя цветами", "Use gradient between two colors");
        internal static string SecondColor => Get("Второй цвет", "Second color");
        internal static string SecondColorTooltip => Get("Второй цвет для градиента", "Second color for gradient");
        internal static string CrosshairStyle => Get("Стиль прицела", "Crosshair style");
        internal static string CrosshairStyleTooltip => Get("Выберите стиль отображения прицела", "Select crosshair display style");
        internal static string ChooseImage => Get("Выбрать картинку", "Choose image");
        internal static string ChooseImageTooltip => Get("Выбрать PNG/JPG картинку для прицела", "Choose PNG/JPG image for crosshair");
        internal static string ManualRotation => Get("Поворот (°)", "Rotation (°)");
        internal static string ManualRotationTooltip => Get("Ручной угол поворота прицела (0-360)", "Manual crosshair rotation angle (0-360)");
        internal static string OpenGallery => Get("Галерея прицелов", "Crosshair Gallery");
        internal static string OpenGalleryTooltip => Get("Открыть галерею всех стилей прицелов", "Open gallery of all crosshair styles");
        internal static string CrosshairGalleryTitle => Get("ГАЛЕРЕЯ ПРИЦЕЛОВ", "CROSSHAIR GALLERY");
        internal static string GalleryStandard => Get("Стандарт", "Standard");
        internal static string GalleryCommunity => Get("Сообщество", "Community");
        internal static string GalleryAdd => Get("Добавить", "Add");
        internal static string GalleryPresets => Get("Пресеты (200+)", "Presets (200+)");

        // ── AUTOCLICKER ──
        internal static string SectionAutoclicker => Get("──  АВТОКЛИКЕР  ──", "──  AUTOCLICKER  ──");
        internal static string EnableAutoclicker => Get("Включить автокликер", "Enable autoclicker");
        internal static string EnableAutoclickerTooltip => Get("Вкл/выкл автоклик. Хоткей: Ctrl+Shift+A", "On/off autoclick. Hotkey: Ctrl+Shift+A");
        internal static string HoldMode => Get("Режим: зажатие ЛКМ", "Mode: hold LMB");
        internal static string HoldModeTooltip => Get("ВКЛ: кликает пока зажата ЛКМ. ВЫКЛ: кликает постоянно пока включен", "ON: clicks while LMB held. OFF: clicks constantly while enabled");
        internal static string ClicksPerSec => Get("Кликов/сек", "Clicks/sec");
        internal static string ClicksPerSecTooltip => Get("Скорость автоклика (5-500, пакетный метод)", "Autoclick speed (5-500, batch method)");
        internal static string RightClick => Get("Правая кнопка мыши", "Right mouse button");
        internal static string RightClickTooltip => Get("Кликать ПКМ вместо ЛКМ", "Click RMB instead of LMB");
        internal static string RandomDelay => Get("Рандом задержка", "Random delay");
        internal static string RandomDelayTooltip => Get("Случайная вариация задержки (антидетект)", "Random delay variation (anti-detect)");
        internal static string SpreadPercent => Get("Разброс (%)", "Spread (%)");
        internal static string SpreadPercentTooltip => Get("Процент рандомизации задержки (5-50%)", "Delay randomization percent (5-50%)");
        internal static string UseSendInput => Get("SendInput API", "SendInput API");
        internal static string UseSendInputTooltip => Get("ВКЛ: SendInput (драйверный уровень, быстрее). ВЫКЛ: mouse_event (совместимость)", "ON: SendInput (driver-level, faster). OFF: mouse_event (compatibility)");
        internal static string Multithreading => Get("Многопоточность", "Multithreading");
        internal static string MultithreadingTooltip => Get("Отдельный поток для кликов, не блокирует UI", "Separate thread for clicks, does not block UI");
        internal static string HighPrecision => Get("Высокая точность", "High precision");
        internal static string HighPrecisionTooltip => Get("QueryPerformanceCounter для минимальных задержек (макс. CPS)", "QueryPerformanceCounter for minimal delays (max CPS)");
        internal static string ClickCounter => Get("Счётчик кликов", "Click counter");
        internal static string ClickCounterTooltip => Get("Количество кликов автокликера за сессию", "Number of autoclicker clicks per session");
        internal static string HotkeyLabel => Get("Хоткей", "Hotkey");
        internal static string HotkeyAutoTooltip => Get("Нажмите Ctrl+Shift+A чтобы вкл/выкл автокликер", "Press Ctrl+Shift+A to toggle autoclicker");

        // ── EFFECTS ──
        internal static string SectionEffects => Get("──  ЭФФЕКТЫ  ──", "──  EFFECTS  ──");
        internal static string Rotation => Get("Вращение", "Rotation");
        internal static string RotationTooltip => Get("Постоянное вращение прицела", "Constant crosshair rotation");
        internal static string RotationSpeed => Get("Скорость вращения", "Rotation speed");
        internal static string RotationSpeedTooltip => Get("Скорость вращения прицела", "Crosshair rotation speed");
        internal static string Outline => Get("Контур", "Outline");
        internal static string OutlineTooltip => Get("Показать/скрыть контур линий прицела", "Show/hide crosshair outline");
        internal static string OutlineColor => Get("Цвет контура", "Outline color");
        internal static string OutlineColorTooltip => Get("Цвет контура перекрестия", "Crosshair outline color");
        internal static string OutlineWidth => Get("Ширина контура", "Outline width");
        internal static string OutlineWidthTooltip => Get("Толщина контура линий", "Outline line thickness");
        internal static string Shadow => Get("Тень", "Shadow");
        internal static string ShadowTooltip => Get("Показать/скрыть тень прицела", "Show/hide crosshair shadow");
        internal static string AntiAlias => Get("Сглаживание", "Anti-aliasing");
        internal static string AntiAliasTooltip => Get("Сглаживание линий для более мягкого вида", "Line anti-aliasing for smoother look");

        // ── DYNAMIC ──
        internal static string SectionDynamic => Get("──  ДИНАМИКА  ──", "──  DYNAMIC  ──");
        internal static string DynamicCrosshair => Get("Динамический прицел", "Dynamic crosshair");
        internal static string DynamicCrosshairTooltip => Get("Прицел расширяется при движении мыши", "Crosshair expands on mouse movement");
        internal static string MaxSpread => Get("Макс. разброс", "Max spread");
        internal static string MaxSpreadTooltip => Get("Максимальный разброс при движении (1-30)", "Maximum spread on movement (1-30)");
        internal static string RecoverySpeed => Get("Скорость возврата", "Recovery speed");
        internal static string RecoverySpeedTooltip => Get("Скорость схождения прицела (5-50)", "Crosshair convergence speed (5-50)");

        // ── VISUAL ──
        internal static string SectionVisual => Get("──  ВИЗУАЛ  ──", "──  VISUAL  ──");
        internal static string GlowEffect => Get("Свечение (Glow)", "Glow effect");
        internal static string GlowEffectTooltip => Get("Мягкое свечение вокруг прицела", "Soft glow around crosshair");
        internal static string GlowSize => Get("Размер свечения", "Glow size");
        internal static string GlowSizeTooltip => Get("Радиус свечения (2-20)", "Glow radius (2-20)");
        internal static string GlowBrightness => Get("Яркость свечения", "Glow brightness");
        internal static string GlowBrightnessTooltip => Get("Прозрачность свечения (20-150)", "Glow opacity (20-150)");
        internal static string HitMarker => Get("Хит-маркер", "Hit marker");
        internal static string HitMarkerTooltip => Get("Анимация при клике автокликера", "Animation on autoclicker click");
        internal static string HitMarkerSize => Get("Размер хит-маркера", "Hit marker size");
        internal static string HitMarkerSizeTooltip => Get("Размер анимации хит-маркера (4-30)", "Hit marker animation size (4-30)");

        // ── ACTIONS ──
        internal static string SectionActions => Get("──  ДЕЙСТВИЯ  ──", "──  ACTIONS  ──");
        internal static string ResetSettings => Get("Сбросить настройки", "Reset settings");
        internal static string ResetSettingsTooltip => Get("Сбросить все настройки к значениям по умолчанию", "Reset all settings to default values");
        internal static string CloseMenu => Get("Закрыть [INS]", "Close [INS]");
        internal static string CloseMenuTooltip => Get("Закрыть меню настроек (также работает клавиша INS)", "Close settings menu (INS key also works)");

        // ── HOTKEYS ──
        internal static string SectionHotkeys => Get("──  ГОРЯЧИЕ КЛАВИШИ  ──", "──  HOTKEYS  ──");
        internal static string HotkeyChangeTooltip(string name) => Get($"Нажмите чтобы изменить хоткей «{name}»", $"Click to change hotkey \"{name}\"");

        // ── LANGUAGE ──
        internal static string SectionLanguage => Get("──  ЯЗЫК  ──", "──  LANGUAGE  ──");
        internal static string LanguageLabel => Get("Язык / Language", "Language / Язык");
        internal static string LanguageTooltip => Get("Переключить язык интерфейса", "Switch interface language");

        // ── UPDATE ──
        internal static string SectionUpdate => Get("──  ОБНОВЛЕНИЕ  ──", "──  UPDATE  ──");
        internal static string CheckUpdate => Get("Проверить обновления", "Check for updates");
        internal static string CheckUpdateTooltip => Get("Проверить наличие новой версии на GitHub", "Check for new version on GitHub");
        internal static string Updating => Get("Обновление...", "Updating...");
        internal static string UpdateAvailable => Get("Доступно обновление!", "Update available!");
        internal static string UpToDate => Get("Актуальная версия", "Up to date");
        internal static string UpdateError => Get("Ошибка проверки", "Check failed");
        internal static string UpdateDownloading => Get("Загрузка...", "Downloading...");
        internal static string UpdateRestart => Get("Перезапуск...", "Restarting...");

        // ── OVERLAY / TRAY ──
        internal static string ClickerActive => Get("Кликер: АКТИВЕН", "Clicker: ACTIVE");
        internal static string ClickerOff => Get("Кликер: ВЫКЛ", "Clicker: OFF");
        internal static string TraySettings => Get("Настройки", "Settings");
        internal static string TrayReset => Get("Сбросить", "Reset");
        internal static string TrayExit => Get("Выход", "Exit");
        internal static string FfmpegNotFound => Get("FFmpeg не найден. Укажите путь в настройках записи.", "FFmpeg not found. Set path in recording settings.");

        // ── HOTKEY NAMES ──
        internal static string[] HotkeyNamesArr => new[] {
            "",
            Get("Видимость", "Visibility"),
            Get("Стиль", "Style"),
            Get("Размер+", "Size+"),
            Get("Размер−", "Size−"),
            Get("Пульсация", "Pulse"),
            Get("Прозрач.+", "Opacity+"),
            Get("Прозрач.−", "Opacity−"),
            Get("Цвет", "Color"),
            Get("Сброс", "Reset"),
            Get("Автокликер", "Autoclicker"),
            Get("Настройки", "Settings"),
            Get("Запись", "Record"),
            Get("Повтор", "Replay"),
            Get("Галерея", "Gallery"),
            Get("Аварийный стоп", "Emergency stop"),
            Get("Скриншот", "Screenshot"),
            Get("Burst", "Burst"),
            Get("Папка скриншотов", "Shots folder"),
            Get("Пресет +", "Preset +"),
            Get("Пресет −", "Preset −"),
            Get("Анти-AFK", "Anti-AFK"),
            Get("Выстрел Burst", "Fire burst")
        };

        // ── HOTKEY CAPTURE FORM ──
        internal static string HotkeyCapPrompt => Get("Нажмите комбинацию клавиш...", "Press key combination...");
        internal static string HotkeyCapTitle(string name) => Get($"Хоткей: {name}", $"Hotkey: {name}");
        internal static string HotkeyCapHint => Get("ESC — отмена  |  ПКМ — убрать хоткей", "ESC — cancel  |  RMB — clear hotkey");

        // ── RECORD NOTIFICATION ──
        internal static string RecordSaved => Get("Запись сохранена", "Recording saved");
        internal static string ClickToOpen => Get("Нажмите чтобы открыть", "Click to open");

        // ── VIDEO GALLERY ──
        internal static string NoRecordings => Get("Нет записей\n\nCtrl+Shift+F9 — начать запись", "No recordings\n\nCtrl+Shift+F9 — start recording");
        internal static string GalleryTitle => Get("🎬  ЗАПИСИ", "🎬  RECORDINGS");
    }
}
