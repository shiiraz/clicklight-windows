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
internal sealed class ClickLightApplicationContext : ApplicationContext
    {
        private readonly SettingsStore settingsStore;
        private readonly LaunchAtLoginController launchAtLogin;
        private readonly OverlayCoordinator overlayCoordinator;
        private readonly MouseHook mouseHook;
        private readonly TrayController trayController;
        private readonly Control dispatcher;
        private SettingsWindow settingsWindow;

        public ClickLightApplicationContext()
        {
            settingsStore = new SettingsStore();
            launchAtLogin = new LaunchAtLoginController();
            overlayCoordinator = new OverlayCoordinator(settingsStore);
            dispatcher = new Control();
            dispatcher.CreateControl();

            mouseHook = new MouseHook(delegate(ClickEvent clickEvent)
            {
                if (!dispatcher.IsDisposed && dispatcher.IsHandleCreated)
                {
                    dispatcher.BeginInvoke(new Action(delegate
                    {
                        if (settingsWindow != null && settingsWindow.ContainsScreenPoint(clickEvent.X, clickEvent.Y))
                        {
                            return;
                        }
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
                overlayCoordinator.RefreshSettings();
                RefreshCapture();
                trayController.RebuildMenu();
            };

            overlayCoordinator.Start();
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
            if (settingsWindow == null || settingsWindow.IsDisposed)
            {
                settingsWindow = new SettingsWindow(
                    settingsStore,
                    launchAtLogin,
                    delegate { return mouseHook.StatusLabel; },
                    ShowTestPulse);
            }

            settingsWindow.ShowSettings();
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
                if (settingsWindow != null)
                {
                    settingsWindow.Dispose();
                }
                overlayCoordinator.Dispose();
                dispatcher.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
