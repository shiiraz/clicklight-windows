using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ClickLight.Windows
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            NativeMethods.SetProcessDPIAwareSafe();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using (ClickLightApplicationContext context = new ClickLightApplicationContext())
            {
                Application.Run(context);
            }
        }
    }

    internal sealed class ClickLightApplicationContext : ApplicationContext
    {
        private readonly SettingsStore settingsStore;
        private readonly LaunchAtLoginController launchAtLogin;
        private readonly ClickOverlayWindow overlayWindow;
        private readonly OverlayCoordinator overlayCoordinator;
        private readonly MouseHook mouseHook;
        private readonly TrayController trayController;
        private readonly Control dispatcher;

        public ClickLightApplicationContext()
        {
            settingsStore = new SettingsStore();
            launchAtLogin = new LaunchAtLoginController();
            overlayWindow = new ClickOverlayWindow(settingsStore.Settings);
            overlayCoordinator = new OverlayCoordinator(settingsStore, overlayWindow);
            dispatcher = new Control();
            dispatcher.CreateControl();

            mouseHook = new MouseHook(delegate(ClickEvent clickEvent)
            {
                if (!dispatcher.IsDisposed && dispatcher.IsHandleCreated)
                {
                    dispatcher.BeginInvoke(new Action(delegate
                    {
                        overlayCoordinator.Show(clickEvent);
                    }));
                }
            });

            trayController = new TrayController(
                settingsStore,
                launchAtLogin,
                delegate { return mouseHook.StatusLabel; },
                OpenSettings,
                ShowTestPulse,
                Quit);

            settingsStore.SettingsChanged += delegate
            {
                overlayWindow.Apply(settingsStore.Settings);
                RefreshCapture();
                trayController.RebuildMenu();
            };

            overlayWindow.ShowInactive();
            RefreshCapture();
            trayController.Start();
        }

        private void RefreshCapture()
        {
            ClickSettings settings = settingsStore.Settings;
            if (settings.isEnabled)
            {
                mouseHook.Start(settings.showLaserPointer);
            }
            else
            {
                mouseHook.Stop();
            }
        }

        private void OpenSettings()
        {
            MessageBox.Show(
                "The full Windows settings window is planned for Phase 3. Phase 1 settings are available from the tray menu and are persisted to %AppData%\\ClickLight\\settings.json.",
                "ClickLight Settings",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void ShowTestPulse()
        {
            Point p = Cursor.Position;
            overlayCoordinator.Show(new ClickEvent(ClickKind.LeftDown, p.X, p.Y, Clock.NowSeconds()));
        }

        private void Quit()
        {
            ExitThread();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                mouseHook.Dispose();
                trayController.Dispose();
                overlayWindow.Dispose();
                dispatcher.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    internal static class Clock
    {
        public static double NowSeconds()
        {
            return (double)Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        }
    }

    internal enum ClickKind
    {
        LeftDown,
        LeftUp,
        RightDown,
        RightUp,
        Drag,
        Move
    }

    internal struct ClickEvent
    {
        public readonly ClickKind Kind;
        public readonly double X;
        public readonly double Y;
        public readonly double Timestamp;

        public ClickEvent(ClickKind kind, double x, double y, double timestamp)
        {
            Kind = kind;
            X = x;
            Y = y;
            Timestamp = timestamp;
        }
    }

    [DataContract]
    internal sealed class ClickSettings
    {
        [DataMember(Order = 1)] public bool isEnabled;
        [DataMember(Order = 2)] public bool showPress;
        [DataMember(Order = 3)] public bool showRelease;
        [DataMember(Order = 4)] public bool showRightClick;
        [DataMember(Order = 5)] public bool showDrag;
        [DataMember(Order = 6)] public bool showLaserPointer;
        [DataMember(Order = 7)] public bool showMenuBarText;
        [DataMember(Order = 8)] public double size;
        [DataMember(Order = 9)] public double intensity;
        [DataMember(Order = 10)] public double duration;
        [DataMember(Order = 11)] public string colorPreset;
        [DataMember(Order = 12)] public double customColorRed;
        [DataMember(Order = 13)] public double customColorGreen;
        [DataMember(Order = 14)] public double customColorBlue;

        public static ClickSettings Defaults()
        {
            return new ClickSettings
            {
                isEnabled = true,
                showPress = true,
                showRelease = true,
                showRightClick = true,
                showDrag = true,
                showLaserPointer = false,
                showMenuBarText = false,
                size = 64.0,
                intensity = 0.7,
                duration = 0.48,
                colorPreset = "default",
                customColorRed = 0.0,
                customColorGreen = 0.74,
                customColorBlue = 1.0
            };
        }

        public ClickSettings Clone()
        {
            return new ClickSettings
            {
                isEnabled = isEnabled,
                showPress = showPress,
                showRelease = showRelease,
                showRightClick = showRightClick,
                showDrag = showDrag,
                showLaserPointer = showLaserPointer,
                showMenuBarText = showMenuBarText,
                size = size,
                intensity = intensity,
                duration = duration,
                colorPreset = colorPreset,
                customColorRed = customColorRed,
                customColorGreen = customColorGreen,
                customColorBlue = customColorBlue
            };
        }

        public void Sanitize()
        {
            if (String.IsNullOrEmpty(colorPreset) || !ClickColorPreset.IsKnown(colorPreset))
            {
                colorPreset = "default";
            }

            if (Double.IsNaN(size) || Double.IsInfinity(size) || size <= 0.0) size = 64.0;
            if (Double.IsNaN(intensity) || Double.IsInfinity(intensity) || intensity <= 0.0) intensity = 0.7;
            if (Double.IsNaN(duration) || Double.IsInfinity(duration) || duration <= 0.0) duration = 0.48;

            customColorRed = Clamp01(customColorRed);
            customColorGreen = Clamp01(customColorGreen);
            customColorBlue = Clamp01(customColorBlue);
        }

        private static double Clamp01(double value)
        {
            if (Double.IsNaN(value) || Double.IsInfinity(value)) return 0.0;
            return Math.Max(0.0, Math.Min(1.0, value));
        }
    }

    internal sealed class SettingsStore
    {
        private readonly string settingsPath;
        private ClickSettings current;

        public event EventHandler SettingsChanged;

        public SettingsStore()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ClickLight");
            settingsPath = Path.Combine(dir, "settings.json");
            current = LoadFromDisk();
        }

        public ClickSettings Settings
        {
            get { return current.Clone(); }
        }

        public void Update(Action<ClickSettings> mutate)
        {
            ClickSettings next = current.Clone();
            mutate(next);
            Save(next);
        }

        public void ResetToDefaults()
        {
            Save(ClickSettings.Defaults());
        }

        private ClickSettings LoadFromDisk()
        {
            try
            {
                if (!File.Exists(settingsPath))
                {
                    ClickSettings defaults = ClickSettings.Defaults();
                    WriteToDisk(defaults);
                    return defaults;
                }

                using (FileStream stream = File.OpenRead(settingsPath))
                {
                    DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(ClickSettings));
                    ClickSettings loaded = (ClickSettings)serializer.ReadObject(stream);
                    if (loaded == null)
                    {
                        return ClickSettings.Defaults();
                    }
                    loaded.Sanitize();
                    return loaded;
                }
            }
            catch
            {
                return ClickSettings.Defaults();
            }
        }

        private void Save(ClickSettings settings)
        {
            settings.Sanitize();
            current = settings.Clone();
            WriteToDisk(current);
            EventHandler changed = SettingsChanged;
            if (changed != null)
            {
                changed(this, EventArgs.Empty);
            }
        }

        private void WriteToDisk(ClickSettings settings)
        {
            string dir = Path.GetDirectoryName(settingsPath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using (FileStream stream = File.Create(settingsPath))
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(ClickSettings));
                serializer.WriteObject(stream, settings);
            }
        }
    }

    internal struct ClickNumericPreset
    {
        public readonly string Title;
        public readonly double Value;

        public ClickNumericPreset(string title, double value)
        {
            Title = title;
            Value = value;
        }
    }

    internal static class ClickSettingOptions
    {
        public static readonly ClickNumericPreset[] SizePresets =
        {
            new ClickNumericPreset("Small", 44.0),
            new ClickNumericPreset("Medium", 64.0),
            new ClickNumericPreset("Large", 88.0),
            new ClickNumericPreset("Huge", 116.0)
        };

        public static readonly ClickNumericPreset[] IntensityPresets =
        {
            new ClickNumericPreset("Subtle", 0.28),
            new ClickNumericPreset("Normal", 0.7),
            new ClickNumericPreset("Bright", 1.0),
            new ClickNumericPreset("Beacon", 1.35)
        };

        public static readonly ClickNumericPreset[] DurationPresets =
        {
            new ClickNumericPreset("Snappy", 0.28),
            new ClickNumericPreset("Normal", 0.48),
            new ClickNumericPreset("Slow", 0.72),
            new ClickNumericPreset("Very Slow", 1.0)
        };
    }

    internal static class ClickColorPreset
    {
        public static readonly string[] All =
        {
            "default",
            "custom",
            "blue",
            "green",
            "purple",
            "pink",
            "orange",
            "white"
        };

        public static bool IsKnown(string preset)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (String.Equals(All[i], preset, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        public static string Title(string preset)
        {
            if (preset == "default") return "Default";
            if (preset == "custom") return "Custom";
            if (preset == "blue") return "Blue";
            if (preset == "green") return "Green";
            if (preset == "purple") return "Purple";
            if (preset == "pink") return "Pink";
            if (preset == "orange") return "Orange";
            if (preset == "white") return "White";
            return "Default";
        }

        public static Color ColorForKind(ClickSettings settings, ClickKind kind)
        {
            if (settings.colorPreset == "custom")
            {
                return FromUnit(settings.customColorRed, settings.customColorGreen, settings.customColorBlue);
            }

            Color? preset = PresetColor(settings.colorPreset);
            if (preset.HasValue)
            {
                return preset.Value;
            }

            if (kind == ClickKind.LeftDown)
            {
                return FromUnit(0.0, 0.74, 1.0);
            }
            if (kind == ClickKind.LeftUp)
            {
                return FromUnit(0.4, 0.88, 1.0);
            }
            if (kind == ClickKind.RightDown || kind == ClickKind.RightUp)
            {
                return FromUnit(1.0, 0.46, 0.19);
            }
            if (kind == ClickKind.Drag)
            {
                return FromUnit(0.92, 0.84, 0.22);
            }
            return Color.Transparent;
        }

        private static Color? PresetColor(string preset)
        {
            if (preset == "blue") return FromUnit(0.0, 0.74, 1.0);
            if (preset == "green") return FromUnit(0.2, 0.9, 0.42);
            if (preset == "purple") return FromUnit(0.58, 0.36, 1.0);
            if (preset == "pink") return FromUnit(1.0, 0.32, 0.72);
            if (preset == "orange") return FromUnit(1.0, 0.46, 0.19);
            if (preset == "white") return Color.White;
            return null;
        }

        private static Color FromUnit(double r, double g, double b)
        {
            return Color.FromArgb(255, UnitToByte(r), UnitToByte(g), UnitToByte(b));
        }

        private static int UnitToByte(double value)
        {
            if (Double.IsNaN(value) || Double.IsInfinity(value)) value = 0.0;
            return (int)Math.Round(Math.Max(0.0, Math.Min(1.0, value)) * 255.0);
        }
    }

    internal sealed class OverlayCoordinator
    {
        private readonly SettingsStore settingsStore;
        private readonly ClickOverlayWindow overlayWindow;
        private readonly List<ClickEvent> recentEvents;

        public OverlayCoordinator(SettingsStore settingsStore, ClickOverlayWindow overlayWindow)
        {
            this.settingsStore = settingsStore;
            this.overlayWindow = overlayWindow;
            recentEvents = new List<ClickEvent>();
        }

        public void Show(ClickEvent clickEvent)
        {
            ClickSettings settings = settingsStore.Settings;
            if (!settings.isEnabled) return;
            if (!settings.showLaserPointer && !ShouldShow(settings, clickEvent.Kind)) return;
            if (!ShouldAccept(clickEvent)) return;

            overlayWindow.ShowEvent(clickEvent, settings);
        }

        private static bool ShouldShow(ClickSettings settings, ClickKind kind)
        {
            if (kind == ClickKind.LeftDown) return settings.showPress;
            if (kind == ClickKind.LeftUp) return settings.showRelease;
            if (kind == ClickKind.RightDown || kind == ClickKind.RightUp) return settings.showRightClick;
            if (kind == ClickKind.Drag) return settings.showDrag;
            return false;
        }

        private bool ShouldAccept(ClickEvent clickEvent)
        {
            double now = Clock.NowSeconds();
            recentEvents.RemoveAll(delegate(ClickEvent existing)
            {
                return now - existing.Timestamp >= 0.1;
            });

            for (int i = 0; i < recentEvents.Count; i++)
            {
                ClickEvent existing = recentEvents[i];
                if (existing.Kind == clickEvent.Kind &&
                    Math.Abs(existing.X - clickEvent.X) < 3.0 &&
                    Math.Abs(existing.Y - clickEvent.Y) < 3.0)
                {
                    return false;
                }
            }

            recentEvents.Add(clickEvent);
            return true;
        }
    }

    internal sealed class ClickOverlayWindow : Form
    {
        private readonly Timer displayTimer;
        private readonly List<ClickPulse> pulses;
        private readonly List<LaserStroke> completedLaserStrokes;
        private ClickSettings settings;
        private Rectangle screenFrame;
        private LaserCursor laserCursor;
        private LaserStroke activeLaserStroke;

        public ClickOverlayWindow(ClickSettings settings)
        {
            this.settings = settings.Clone();
            pulses = new List<ClickPulse>();
            completedLaserStrokes = new List<LaserStroke>();
            displayTimer = new Timer();
            displayTimer.Interval = 16;
            displayTimer.Tick += delegate { RenderFrame(); };

            screenFrame = SystemInformation.VirtualScreen;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = screenFrame;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.Black;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= NativeMethods.WS_EX_LAYERED;
                cp.ExStyle |= NativeMethods.WS_EX_TRANSPARENT;
                cp.ExStyle |= NativeMethods.WS_EX_TOOLWINDOW;
                cp.ExStyle |= NativeMethods.WS_EX_NOACTIVATE;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        public void ShowInactive()
        {
            if (!Visible)
            {
                Show();
                RenderBlankFrame();
            }
        }

        public void Apply(ClickSettings newSettings)
        {
            settings = newSettings.Clone();
            if (!settings.showLaserPointer)
            {
                laserCursor = null;
                activeLaserStroke = null;
                completedLaserStrokes.Clear();
                RenderFrame();
            }
        }

        public void ShowEvent(ClickEvent clickEvent, ClickSettings newSettings)
        {
            settings = newSettings.Clone();
            RefreshScreenFrameIfNeeded();
            if (!Visible)
            {
                ShowInactive();
            }

            PointF localPoint = new PointF(
                (float)(clickEvent.X - screenFrame.Left),
                (float)(clickEvent.Y - screenFrame.Top));

            if (settings.showLaserPointer)
            {
                if (clickEvent.Kind == ClickKind.Move)
                {
                    ShowLaserCursor(localPoint);
                    return;
                }
                if (clickEvent.Kind == ClickKind.Drag)
                {
                    AppendLaserPoint(localPoint);
                    return;
                }
                if (clickEvent.Kind == ClickKind.LeftUp || clickEvent.Kind == ClickKind.RightUp)
                {
                    CompleteLaserStroke();
                }
            }

            if (!ShouldShowPulse(settings, clickEvent.Kind))
            {
                return;
            }

            pulses.Add(new ClickPulse(
                clickEvent.Kind,
                localPoint,
                Clock.NowSeconds(),
                DurationFor(settings, clickEvent.Kind),
                SizeFor(settings, clickEvent.Kind),
                settings.intensity,
                ClickColorPreset.ColorForKind(settings, clickEvent.Kind)));

            StartDisplayTimer();
            RenderFrame();
        }

        private void RefreshScreenFrameIfNeeded()
        {
            Rectangle latest = SystemInformation.VirtualScreen;
            if (latest != screenFrame)
            {
                screenFrame = latest;
                Bounds = screenFrame;
            }
        }

        private void ShowLaserCursor(PointF point)
        {
            laserCursor = new LaserCursor(point, Clock.NowSeconds());
            StartDisplayTimer();
            RenderFrame();
        }

        private void AppendLaserPoint(PointF point)
        {
            double now = Clock.NowSeconds();
            ShowLaserCursor(point);

            if (activeLaserStroke == null)
            {
                activeLaserStroke = new LaserStroke();
                activeLaserStroke.Points.Add(point);
            }
            else if (activeLaserStroke.ShouldAppend(point))
            {
                activeLaserStroke.Points.Add(point);
            }

            if (activeLaserStroke.Points.Count == 1)
            {
                activeLaserStroke.Points.Add(point);
            }

            laserCursor = new LaserCursor(point, now);
            StartDisplayTimer();
            RenderFrame();
        }

        private void CompleteLaserStroke()
        {
            if (activeLaserStroke == null) return;
            activeLaserStroke.CompletedAt = Clock.NowSeconds();
            completedLaserStrokes.Add(activeLaserStroke);
            activeLaserStroke = null;
            StartDisplayTimer();
            RenderFrame();
        }

        private void StartDisplayTimer()
        {
            if (!displayTimer.Enabled)
            {
                displayTimer.Start();
            }
        }

        private void StopDisplayTimer()
        {
            if (displayTimer.Enabled)
            {
                displayTimer.Stop();
            }
        }

        private void RenderFrame()
        {
            if (!IsHandleCreated) return;

            double now = Clock.NowSeconds();
            pulses.RemoveAll(delegate(ClickPulse pulse) { return pulse.IsExpired(now); });
            completedLaserStrokes.RemoveAll(delegate(LaserStroke stroke) { return stroke.IsExpired(now); });

            bool hasLaserCursor = laserCursor != null && !laserCursor.IsExpired(now);
            bool hasContent = pulses.Count > 0 || hasLaserCursor || activeLaserStroke != null || completedLaserStrokes.Count > 0;

            using (Bitmap bitmap = new Bitmap(Math.Max(1, Width), Math.Max(1, Height), PixelFormat.Format32bppPArgb))
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;

                DrawLaser(g, now);
                for (int i = 0; i < pulses.Count; i++)
                {
                    DrawPulse(g, pulses[i], now);
                }

                UpdateLayeredBitmap(bitmap);
            }

            if (!hasContent)
            {
                StopDisplayTimer();
            }
        }

        private void RenderBlankFrame()
        {
            if (!IsHandleCreated) return;
            using (Bitmap bitmap = new Bitmap(Math.Max(1, Width), Math.Max(1, Height), PixelFormat.Format32bppPArgb))
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                UpdateLayeredBitmap(bitmap);
            }
        }

        private void DrawLaser(Graphics g, double now)
        {
            if (!settings.showLaserPointer) return;

            for (int i = 0; i < completedLaserStrokes.Count; i++)
            {
                LaserStroke stroke = completedLaserStrokes[i];
                DrawLaserStroke(g, stroke, stroke.Alpha(now));
            }

            if (activeLaserStroke != null)
            {
                DrawLaserStroke(g, activeLaserStroke, 0.95);
            }

            if (laserCursor == null || laserCursor.IsExpired(now)) return;

            double alpha = laserCursor.Alpha(now);
            Color laserColor = UnitColor(1.0, 0.16, 0.24);
            FillEllipse(g, laserCursor.Point, 14.0, WithAlpha(laserColor, alpha * 0.18));
            FillEllipse(g, laserCursor.Point, 6.0, WithAlpha(laserColor, alpha));
        }

        private void DrawLaserStroke(Graphics g, LaserStroke stroke, double alpha)
        {
            if (stroke.Points.Count < 2 || alpha <= 0.0) return;

            Color laserColor = UnitColor(1.0, 0.16, 0.24);
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddLines(stroke.Points.ToArray());
                using (Pen glow = new Pen(WithAlpha(laserColor, alpha * 0.2), 14.0f))
                using (Pen core = new Pen(WithAlpha(laserColor, alpha), 5.0f))
                {
                    glow.StartCap = LineCap.Round;
                    glow.EndCap = LineCap.Round;
                    glow.LineJoin = LineJoin.Round;
                    core.StartCap = LineCap.Round;
                    core.EndCap = LineCap.Round;
                    core.LineJoin = LineJoin.Round;
                    g.DrawPath(glow, path);
                    g.DrawPath(core, path);
                }
            }
        }

        private void DrawPulse(Graphics g, ClickPulse pulse, double now)
        {
            double progress = pulse.Progress(now);
            double eased = 1.0 - Math.Pow(1.0 - progress, 3.0);
            double fade = 1.0 - eased;
            double visualIntensity = Math.Max(0.15, Math.Min(1.35, pulse.Intensity));
            double alpha = Clamp01(fade * (0.18 + visualIntensity * 0.78));
            double lineWidth = Math.Max(2.25, pulse.BaseSize * (0.035 + visualIntensity * 0.045));

            if (pulse.Kind == ClickKind.LeftDown)
            {
                DrawGlowIfNeeded(g, pulse.Point, pulse.BaseSize * (0.28 + 0.78 * eased), pulse.Color, fade * visualIntensity);
                DrawRing(g, pulse.Point, pulse.BaseSize * (0.18 + 0.62 * eased), lineWidth, pulse.Color, alpha);
                DrawDot(g, pulse.Point, pulse.BaseSize * 0.085, pulse.Color, alpha * 0.75);
            }
            else if (pulse.Kind == ClickKind.LeftUp)
            {
                double releaseRadius = pulse.BaseSize * (0.76 - 0.42 * eased);
                double releaseAlpha = alpha * 0.55;
                DrawGlowIfNeeded(g, pulse.Point, releaseRadius * 1.25, pulse.Color, fade * visualIntensity * 0.45);
                DrawRing(g, pulse.Point, releaseRadius, lineWidth * 0.55, pulse.Color, releaseAlpha);
                DrawDot(g, pulse.Point, pulse.BaseSize * 0.055, pulse.Color, releaseAlpha * 0.6);
            }
            else if (pulse.Kind == ClickKind.RightDown)
            {
                DrawGlowIfNeeded(g, pulse.Point, pulse.BaseSize * (0.28 + 0.7 * eased), pulse.Color, fade * visualIntensity);
                DrawRing(g, pulse.Point, pulse.BaseSize * (0.18 + 0.54 * eased), lineWidth, pulse.Color, alpha);
                DrawCrosshair(g, pulse.Point, pulse.BaseSize * 0.28, pulse.Color, alpha * 0.85);
            }
            else if (pulse.Kind == ClickKind.RightUp)
            {
                double releaseRadius = pulse.BaseSize * (0.68 - 0.36 * eased);
                double releaseAlpha = alpha * 0.5;
                DrawGlowIfNeeded(g, pulse.Point, releaseRadius * 1.22, pulse.Color, fade * visualIntensity * 0.4);
                DrawRing(g, pulse.Point, releaseRadius, lineWidth * 0.55, pulse.Color, releaseAlpha);
                DrawCrosshair(g, pulse.Point, pulse.BaseSize * (0.16 + 0.08 * fade), pulse.Color, releaseAlpha * 0.7);
            }
            else if (pulse.Kind == ClickKind.Drag)
            {
                DrawDot(g, pulse.Point, pulse.BaseSize * (0.08 + 0.065 * visualIntensity), pulse.Color, alpha * 0.78);
            }
        }

        private void DrawGlowIfNeeded(Graphics g, PointF point, double radius, Color color, double alpha)
        {
            if (settings.intensity < 0.7) return;
            double glowAlpha = Clamp01(alpha * (settings.intensity >= 1.2 ? 0.18 : 0.08));
            FillEllipse(g, point, radius, WithAlpha(color, glowAlpha));
        }

        private static void DrawRing(Graphics g, PointF point, double radius, double lineWidth, Color color, double alpha)
        {
            using (Pen pen = new Pen(WithAlpha(color, alpha), (float)lineWidth))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                g.DrawEllipse(pen, (float)(point.X - radius), (float)(point.Y - radius), (float)(radius * 2.0), (float)(radius * 2.0));
            }
        }

        private static void DrawDot(Graphics g, PointF point, double radius, Color color, double alpha)
        {
            FillEllipse(g, point, radius, WithAlpha(color, alpha));
        }

        private static void DrawCrosshair(Graphics g, PointF point, double size, Color color, double alpha)
        {
            using (Pen pen = new Pen(WithAlpha(color, alpha), (float)Math.Max(2.0, size * 0.12)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawLine(pen, (float)(point.X - size), point.Y, (float)(point.X + size), point.Y);
                g.DrawLine(pen, point.X, (float)(point.Y - size), point.X, (float)(point.Y + size));
            }
        }

        private static void FillEllipse(Graphics g, PointF point, double radius, Color color)
        {
            using (SolidBrush brush = new SolidBrush(color))
            {
                g.FillEllipse(brush, (float)(point.X - radius), (float)(point.Y - radius), (float)(radius * 2.0), (float)(radius * 2.0));
            }
        }

        private static bool ShouldShowPulse(ClickSettings settings, ClickKind kind)
        {
            if (kind == ClickKind.LeftDown) return settings.showPress;
            if (kind == ClickKind.LeftUp) return settings.showRelease;
            if (kind == ClickKind.RightDown || kind == ClickKind.RightUp) return settings.showRightClick;
            if (kind == ClickKind.Drag) return settings.showDrag && !settings.showLaserPointer;
            return false;
        }

        private static double DurationFor(ClickSettings settings, ClickKind kind)
        {
            if (kind == ClickKind.Drag) return Math.Min(0.38, settings.duration * 0.82);
            if (kind == ClickKind.LeftUp || kind == ClickKind.RightUp) return settings.duration * 0.78;
            if (kind == ClickKind.LeftDown || kind == ClickKind.RightDown) return settings.duration;
            return 0.0;
        }

        private static double SizeFor(ClickSettings settings, ClickKind kind)
        {
            if (kind == ClickKind.Drag) return settings.size * 0.6;
            if (kind == ClickKind.LeftUp || kind == ClickKind.RightUp) return settings.size * 0.82;
            if (kind == ClickKind.LeftDown || kind == ClickKind.RightDown) return settings.size;
            return 0.0;
        }

        private void UpdateLayeredBitmap(Bitmap bitmap)
        {
            IntPtr screenDc = IntPtr.Zero;
            IntPtr memDc = IntPtr.Zero;
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr oldBitmap = IntPtr.Zero;

            try
            {
                screenDc = NativeMethods.GetDC(IntPtr.Zero);
                memDc = NativeMethods.CreateCompatibleDC(screenDc);
                hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
                oldBitmap = NativeMethods.SelectObject(memDc, hBitmap);

                NativeMethods.POINT top = new NativeMethods.POINT(Left, Top);
                NativeMethods.SIZE size = new NativeMethods.SIZE(Width, Height);
                NativeMethods.POINT source = new NativeMethods.POINT(0, 0);
                NativeMethods.BLENDFUNCTION blend = new NativeMethods.BLENDFUNCTION();
                blend.BlendOp = NativeMethods.AC_SRC_OVER;
                blend.BlendFlags = 0;
                blend.SourceConstantAlpha = 255;
                blend.AlphaFormat = NativeMethods.AC_SRC_ALPHA;

                NativeMethods.UpdateLayeredWindow(
                    Handle,
                    screenDc,
                    ref top,
                    ref size,
                    memDc,
                    ref source,
                    0,
                    ref blend,
                    NativeMethods.ULW_ALPHA);
            }
            finally
            {
                if (oldBitmap != IntPtr.Zero) NativeMethods.SelectObject(memDc, oldBitmap);
                if (hBitmap != IntPtr.Zero) NativeMethods.DeleteObject(hBitmap);
                if (memDc != IntPtr.Zero) NativeMethods.DeleteDC(memDc);
                if (screenDc != IntPtr.Zero) NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        private static Color UnitColor(double r, double g, double b)
        {
            return Color.FromArgb(255, ToByte(r), ToByte(g), ToByte(b));
        }

        private static Color WithAlpha(Color color, double alpha)
        {
            return Color.FromArgb(ToByte(Clamp01(alpha)), color.R, color.G, color.B);
        }

        private static int ToByte(double value)
        {
            if (Double.IsNaN(value) || Double.IsInfinity(value)) value = 0.0;
            return (int)Math.Round(Math.Max(0.0, Math.Min(1.0, value)) * 255.0);
        }

        private static double Clamp01(double value)
        {
            if (Double.IsNaN(value) || Double.IsInfinity(value)) return 0.0;
            return Math.Max(0.0, Math.Min(1.0, value));
        }
    }

    internal sealed class ClickPulse
    {
        public readonly ClickKind Kind;
        public readonly PointF Point;
        public readonly double StartTime;
        public readonly double Duration;
        public readonly double BaseSize;
        public readonly double Intensity;
        public readonly Color Color;

        public ClickPulse(ClickKind kind, PointF point, double startTime, double duration, double baseSize, double intensity, Color color)
        {
            Kind = kind;
            Point = point;
            StartTime = startTime;
            Duration = duration;
            BaseSize = baseSize;
            Intensity = intensity;
            Color = color;
        }

        public double Progress(double time)
        {
            if (Duration <= 0.0) return 1.0;
            return Math.Max(0.0, Math.Min(1.0, (time - StartTime) / Duration));
        }

        public bool IsExpired(double time)
        {
            return Progress(time) >= 1.0;
        }
    }

    internal sealed class LaserCursor
    {
        public const double FadeDuration = 0.42;
        public readonly PointF Point;
        public readonly double UpdatedAt;

        public LaserCursor(PointF point, double updatedAt)
        {
            Point = point;
            UpdatedAt = updatedAt;
        }

        public double Alpha(double time)
        {
            double progress = Math.Max(0.0, Math.Min(1.0, (time - UpdatedAt) / FadeDuration));
            return 1.0 - progress;
        }

        public bool IsExpired(double time)
        {
            return time - UpdatedAt >= FadeDuration;
        }
    }

    internal sealed class LaserStroke
    {
        public const double FadeDuration = 0.9;
        public readonly List<PointF> Points = new List<PointF>();
        public double? CompletedAt;

        public bool ShouldAppend(PointF point)
        {
            if (Points.Count == 0) return true;
            PointF last = Points[Points.Count - 1];
            double dx = last.X - point.X;
            double dy = last.Y - point.Y;
            return Math.Sqrt(dx * dx + dy * dy) >= 2.5;
        }

        public double Alpha(double time)
        {
            if (!CompletedAt.HasValue) return 1.0;
            double progress = Math.Max(0.0, Math.Min(1.0, (time - CompletedAt.Value) / FadeDuration));
            return 1.0 - progress;
        }

        public bool IsExpired(double time)
        {
            return CompletedAt.HasValue && time - CompletedAt.Value >= FadeDuration;
        }
    }

    internal sealed class MouseHook : IDisposable
    {
        private readonly NativeMethods.LowLevelMouseProc hookProc;
        private readonly Action<ClickEvent> onEvent;
        private IntPtr hookHandle;
        private bool laserPointerEnabled;
        private bool leftButtonDown;
        private bool rightButtonDown;

        public MouseHook(Action<ClickEvent> onEvent)
        {
            this.onEvent = onEvent;
            hookProc = HookCallback;
        }

        public string StatusLabel
        {
            get { return hookHandle != IntPtr.Zero ? "Hook active" : "Stopped"; }
        }

        public void Start(bool laserPointerEnabled)
        {
            this.laserPointerEnabled = laserPointerEnabled;
            if (hookHandle != IntPtr.Zero) return;

            IntPtr module = NativeMethods.GetModuleHandle(null);
            hookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, hookProc, module, 0);
        }

        public void Stop()
        {
            if (hookHandle != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(hookHandle);
                hookHandle = IntPtr.Zero;
            }
            leftButtonDown = false;
            rightButtonDown = false;
        }

        public void Dispose()
        {
            Stop();
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int message = wParam.ToInt32();
                NativeMethods.MSLLHOOKSTRUCT data = (NativeMethods.MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(NativeMethods.MSLLHOOKSTRUCT));
                ClickKind? kind = KindForMessage(message);
                if (kind.HasValue)
                {
                    onEvent(new ClickEvent(kind.Value, data.pt.X, data.pt.Y, Clock.NowSeconds()));
                }
            }

            return NativeMethods.CallNextHookEx(hookHandle, nCode, wParam, lParam);
        }

        private ClickKind? KindForMessage(int message)
        {
            if (message == NativeMethods.WM_LBUTTONDOWN)
            {
                leftButtonDown = true;
                return ClickKind.LeftDown;
            }
            if (message == NativeMethods.WM_LBUTTONUP)
            {
                leftButtonDown = false;
                return ClickKind.LeftUp;
            }
            if (message == NativeMethods.WM_RBUTTONDOWN)
            {
                rightButtonDown = true;
                return ClickKind.RightDown;
            }
            if (message == NativeMethods.WM_RBUTTONUP)
            {
                rightButtonDown = false;
                return ClickKind.RightUp;
            }
            if (message == NativeMethods.WM_MOUSEMOVE)
            {
                if (leftButtonDown || rightButtonDown)
                {
                    return ClickKind.Drag;
                }
                if (laserPointerEnabled)
                {
                    return ClickKind.Move;
                }
            }
            return null;
        }
    }

    internal sealed class TrayController : IDisposable
    {
        private readonly SettingsStore settingsStore;
        private readonly LaunchAtLoginController launchAtLogin;
        private readonly Func<string> captureStatus;
        private readonly Action openSettings;
        private readonly Action testPulse;
        private readonly Action quit;
        private readonly NotifyIcon notifyIcon;
        private ContextMenuStrip menu;

        public TrayController(
            SettingsStore settingsStore,
            LaunchAtLoginController launchAtLogin,
            Func<string> captureStatus,
            Action openSettings,
            Action testPulse,
            Action quit)
        {
            this.settingsStore = settingsStore;
            this.launchAtLogin = launchAtLogin;
            this.captureStatus = captureStatus;
            this.openSettings = openSettings;
            this.testPulse = testPulse;
            this.quit = quit;
            notifyIcon = new NotifyIcon();
            notifyIcon.Icon = TrayIconFactory.CreateIcon();
            notifyIcon.Text = "ClickLight";
            notifyIcon.Visible = false;
        }

        public void Start()
        {
            RebuildMenu();
            notifyIcon.Visible = true;
        }

        public void RebuildMenu()
        {
            ClickSettings settings = settingsStore.Settings;
            ContextMenuStrip oldMenu = menu;
            menu = new ContextMenuStrip();

            menu.Items.Add(ToggleItem("Enabled", settings.isEnabled, delegate
            {
                settingsStore.Update(delegate(ClickSettings s) { s.isEnabled = !s.isEnabled; });
            }));

            menu.Items.Add(CommandItem("Open Settings...", openSettings));
            menu.Items.Add(new ToolStripSeparator());

            menu.Items.Add(ToggleItem("Laser Pointer Mode", settings.showLaserPointer, delegate
            {
                settingsStore.Update(delegate(ClickSettings s) { s.showLaserPointer = !s.showLaserPointer; });
            }));
            menu.Items.Add(new ToolStripSeparator());

            menu.Items.Add(ToggleItem("Show Press", settings.showPress, delegate
            {
                settingsStore.Update(delegate(ClickSettings s) { s.showPress = !s.showPress; });
            }));
            menu.Items.Add(ToggleItem("Show Release", settings.showRelease, delegate
            {
                settingsStore.Update(delegate(ClickSettings s) { s.showRelease = !s.showRelease; });
            }));
            menu.Items.Add(ToggleItem("Show Right Click", settings.showRightClick, delegate
            {
                settingsStore.Update(delegate(ClickSettings s) { s.showRightClick = !s.showRightClick; });
            }));

            ToolStripMenuItem showDrag = ToggleItem("Show Drag", settings.showDrag, delegate
            {
                settingsStore.Update(delegate(ClickSettings s) { s.showDrag = !s.showDrag; });
            });
            showDrag.Enabled = !settings.showLaserPointer;
            menu.Items.Add(showDrag);

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(ToggleItem("Show Tray Label", settings.showMenuBarText, delegate
            {
                settingsStore.Update(delegate(ClickSettings s) { s.showMenuBarText = !s.showMenuBarText; });
            }));
            menu.Items.Add(ToggleItem("Launch at Login", launchAtLogin.IsEnabled, delegate
            {
                launchAtLogin.SetEnabled(!launchAtLogin.IsEnabled);
                RebuildMenu();
            }));
            menu.Items.Add(new ToolStripSeparator());

            menu.Items.Add(NumericSubmenu("Size", ClickSettingOptions.SizePresets, settings.size, delegate(double value)
            {
                settingsStore.Update(delegate(ClickSettings s) { s.size = value; });
            }));
            menu.Items.Add(NumericSubmenu("Intensity", ClickSettingOptions.IntensityPresets, settings.intensity, delegate(double value)
            {
                settingsStore.Update(delegate(ClickSettings s) { s.intensity = value; });
            }));
            menu.Items.Add(NumericSubmenu("Duration", ClickSettingOptions.DurationPresets, settings.duration, delegate(double value)
            {
                settingsStore.Update(delegate(ClickSettings s) { s.duration = value; });
            }));
            menu.Items.Add(ColorSubmenu(settings.colorPreset));
            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem capture = new ToolStripMenuItem("Click Capture: " + captureStatus());
            capture.Enabled = false;
            menu.Items.Add(capture);

            ToolStripMenuItem test = CommandItem("Test Pulse at Pointer", testPulse);
            test.Enabled = settings.isEnabled;
            menu.Items.Add(test);
            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem note = new ToolStripMenuItem("Capture note: elevated apps may require elevated ClickLight");
            note.Enabled = false;
            menu.Items.Add(note);

            ToolStripMenuItem updates = new ToolStripMenuItem("Updates: Not Configured");
            updates.Enabled = false;
            menu.Items.Add(updates);

            menu.Items.Add(CommandItem("Quit ClickLight", quit));

            notifyIcon.ContextMenuStrip = menu;
            notifyIcon.Text = settings.showMenuBarText ? "ClickLight - " + captureStatus() : "ClickLight";

            if (oldMenu != null)
            {
                oldMenu.Dispose();
            }
        }

        public void Dispose()
        {
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
            if (menu != null) menu.Dispose();
        }

        private static ToolStripMenuItem CommandItem(string title, Action action)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(title);
            item.Click += delegate { action(); };
            return item;
        }

        private static ToolStripMenuItem ToggleItem(string title, bool isOn, Action action)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(title);
            item.Checked = isOn;
            item.Click += delegate { action(); };
            return item;
        }

        private static ToolStripMenuItem NumericSubmenu(string title, ClickNumericPreset[] presets, double selected, Action<double> select)
        {
            ToolStripMenuItem parent = new ToolStripMenuItem(title);
            bool matched = false;

            for (int i = 0; i < presets.Length; i++)
            {
                ClickNumericPreset preset = presets[i];
                ToolStripMenuItem item = new ToolStripMenuItem(preset.Title);
                item.Checked = Math.Abs(preset.Value - selected) < 0.01;
                if (item.Checked) matched = true;
                double value = preset.Value;
                item.Click += delegate { select(value); };
                parent.DropDownItems.Add(item);
            }

            if (!matched)
            {
                parent.DropDownItems.Add(new ToolStripSeparator());
                ToolStripMenuItem custom = new ToolStripMenuItem("Custom");
                custom.Checked = true;
                custom.Enabled = false;
                parent.DropDownItems.Add(custom);
            }

            return parent;
        }

        private ToolStripMenuItem ColorSubmenu(string selected)
        {
            ToolStripMenuItem parent = new ToolStripMenuItem("Colors");
            for (int i = 0; i < ClickColorPreset.All.Length; i++)
            {
                string preset = ClickColorPreset.All[i];
                ToolStripMenuItem item = new ToolStripMenuItem(ClickColorPreset.Title(preset));
                item.Checked = preset == selected;
                string selectedPreset = preset;
                item.Click += delegate
                {
                    settingsStore.Update(delegate(ClickSettings s) { s.colorPreset = selectedPreset; });
                };
                parent.DropDownItems.Add(item);
            }
            return parent;
        }
    }

    internal static class TrayIconFactory
    {
        public static Icon CreateIcon()
        {
            Bitmap bitmap = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (Pen ring = new Pen(Color.FromArgb(230, 0, 189, 255), 3.0f))
                using (SolidBrush dot = new SolidBrush(Color.FromArgb(255, 0, 189, 255)))
                using (Pen cursor = new Pen(Color.White, 2.0f))
                using (SolidBrush shadow = new SolidBrush(Color.FromArgb(90, 0, 0, 0)))
                {
                    g.FillEllipse(shadow, 5, 5, 22, 22);
                    g.DrawEllipse(ring, 6, 6, 18, 18);
                    g.FillEllipse(dot, 13, 13, 6, 6);
                    PointF[] points =
                    {
                        new PointF(18, 18),
                        new PointF(27, 25),
                        new PointF(22, 26),
                        new PointF(24, 31),
                        new PointF(21, 32),
                        new PointF(19, 27),
                        new PointF(15, 30)
                    };
                    g.DrawPolygon(cursor, points);
                }
            }

            IntPtr handle = bitmap.GetHicon();
            Icon icon = (Icon)Icon.FromHandle(handle).Clone();
            NativeMethods.DestroyIcon(handle);
            bitmap.Dispose();
            return icon;
        }
    }

    internal sealed class LaunchAtLoginController
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "ClickLight";

        public bool IsEnabled
        {
            get
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, false))
                {
                    return key != null && key.GetValue(ValueName) != null;
                }
            }
        }

        public void SetEnabled(bool enabled)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (key == null) return;
                if (enabled)
                {
                    key.SetValue(ValueName, "\"" + Application.ExecutablePath + "\"");
                }
                else
                {
                    key.DeleteValue(ValueName, false);
                }
            }
        }
    }

    internal static class NativeMethods
    {
        public const int WH_MOUSE_LL = 14;
        public const int WM_MOUSEMOVE = 0x0200;
        public const int WM_LBUTTONDOWN = 0x0201;
        public const int WM_LBUTTONUP = 0x0202;
        public const int WM_RBUTTONDOWN = 0x0204;
        public const int WM_RBUTTONUP = 0x0205;

        public const int WS_EX_LAYERED = 0x00080000;
        public const int WS_EX_TRANSPARENT = 0x00000020;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_NOACTIVATE = 0x08000000;

        public const int SW_SHOWNOACTIVATE = 4;
        public const int ULW_ALPHA = 0x00000002;
        public const byte AC_SRC_OVER = 0x00;
        public const byte AC_SRC_ALPHA = 0x01;

        public delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;

            public POINT(int x, int y)
            {
                X = x;
                Y = y;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SIZE
        {
            public int X;
            public int Y;

            public SIZE(int x, int y)
            {
                X = x;
                Y = y;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateCompatibleDC(IntPtr hDC);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UpdateLayeredWindow(
            IntPtr hwnd,
            IntPtr hdcDst,
            ref POINT pptDst,
            ref SIZE psize,
            IntPtr hdcSrc,
            ref POINT pptSrc,
            int crKey,
            ref BLENDFUNCTION pblend,
            int dwFlags);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("user32.dll", EntryPoint = "DestroyIcon", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIconNative(IntPtr hIcon);

        public static void SetProcessDPIAwareSafe()
        {
            try
            {
                SetProcessDPIAware();
            }
            catch
            {
            }
        }

        public static void DestroyIconSafe(IntPtr hIcon)
        {
            try
            {
                DestroyIconNative(hIcon);
            }
            catch
            {
            }
        }

        public static void DestroyIcon(IntPtr hIcon)
        {
            DestroyIconSafe(hIcon);
        }
    }
}
