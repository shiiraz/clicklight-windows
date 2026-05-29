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
internal sealed class OverlayCoordinator : IDisposable
    {
        private readonly SettingsStore settingsStore;
        private readonly Dictionary<string, ClickOverlayWindow> overlaysByScreen;
        private readonly List<ClickEvent> recentEvents;
        private ClickSettings settings;

        public OverlayCoordinator(SettingsStore settingsStore)
        {
            this.settingsStore = settingsStore;
            settings = settingsStore.Settings;
            overlaysByScreen = new Dictionary<string, ClickOverlayWindow>();
            recentEvents = new List<ClickEvent>();
        }

        public void Start()
        {
            RebuildOverlays();
            SystemEvents.DisplaySettingsChanged += DisplaySettingsDidChange;
        }

        public void RefreshSettings()
        {
            settings = settingsStore.Settings;
            foreach (ClickOverlayWindow overlay in overlaysByScreen.Values)
            {
                overlay.Apply(settings);
            }
        }

        public void Show(ClickEvent clickEvent)
        {
            if (!settings.isEnabled) return;
            if (!settings.showLaserPointer && !ShouldShow(settings, clickEvent.Kind)) return;
            if (!ShouldAccept(clickEvent)) return;

            Screen screen = Screen.FromPoint(new Point((int)Math.Round(clickEvent.X), (int)Math.Round(clickEvent.Y)));
            ClickOverlayWindow overlayWindow = OverlayForScreen(screen);
            overlayWindow.ShowEvent(clickEvent, settings);
        }

        public void Dispose()
        {
            SystemEvents.DisplaySettingsChanged -= DisplaySettingsDidChange;
            foreach (ClickOverlayWindow overlay in overlaysByScreen.Values)
            {
                overlay.Dispose();
            }
            overlaysByScreen.Clear();
        }

        private void DisplaySettingsDidChange(object sender, EventArgs e)
        {
            RebuildOverlays();
        }

        private void RebuildOverlays()
        {
            foreach (ClickOverlayWindow overlay in overlaysByScreen.Values)
            {
                overlay.Dispose();
            }
            overlaysByScreen.Clear();

            Screen[] screens = Screen.AllScreens;
            for (int i = 0; i < screens.Length; i++)
            {
                OverlayForScreen(screens[i]);
            }
        }

        private ClickOverlayWindow OverlayForScreen(Screen screen)
        {
            string key = screen.DeviceName;
            ClickOverlayWindow overlayWindow;
            if (!overlaysByScreen.TryGetValue(key, out overlayWindow))
            {
                overlayWindow = new ClickOverlayWindow(screen.Bounds, settings);
                overlayWindow.ShowInactive();
                overlaysByScreen[key] = overlayWindow;
            }
            return overlayWindow;
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
}
