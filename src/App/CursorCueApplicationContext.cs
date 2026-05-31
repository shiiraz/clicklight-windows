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

namespace CursorCue.Windows
{
internal sealed class CursorCueApplicationContext : ApplicationContext
    {
        private readonly SettingsStore settingsStore;
        private readonly LaunchAtLoginController launchAtLogin;
        private readonly OverlayCoordinator overlayCoordinator;
        private readonly MouseHook mouseHook;
        private readonly TrayController trayController;
        private readonly Control dispatcher;
        private readonly ActivationWindow activationWindow;
        private SettingsWindow settingsWindow;
        private bool trayMenuOpen;

        public CursorCueApplicationContext()
        {
            settingsStore = new SettingsStore();
            launchAtLogin = new LaunchAtLoginController();
            launchAtLogin.RepairIfEnabled();
            overlayCoordinator = new OverlayCoordinator(settingsStore);
            dispatcher = new Control();
            IntPtr dispatcherHandle = dispatcher.Handle;
            activationWindow = new ActivationWindow(OpenSettings);

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
                        if (trayMenuOpen)
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
                Quit,
                SetTrayMenuOpen);

            settingsStore.SettingsChanged += delegate
            {
                overlayCoordinator.RefreshSettings();
                RefreshCapture();
                trayController.RebuildMenu();
            };

            overlayCoordinator.Start();
            RefreshCapture();
            trayController.Start();

            if (settingsStore.IsFirstRun)
            {
                OpenSettings();
            }
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

        private void SetTrayMenuOpen(bool isOpen)
        {
            trayMenuOpen = isOpen;
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
                activationWindow.Dispose();
                dispatcher.Dispose();
            }
            base.Dispose(disposing);
        }

        private sealed class ActivationWindow : NativeWindow, IDisposable
        {
            private readonly Action activate;

            public ActivationWindow(Action activate)
            {
                this.activate = activate;
                CreateParams cp = new CreateParams();
                cp.Caption = "CursorCue Activation";
                cp.X = -32000;
                cp.Y = -32000;
                cp.Width = 1;
                cp.Height = 1;
                cp.Style = NativeMethods.WS_POPUP;
                cp.ExStyle = NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE;
                CreateHandle(cp);
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == SingleInstanceActivation.ActivationMessage)
                {
                    activate();
                    return;
                }

                base.WndProc(ref m);
            }

            public void Dispose()
            {
                DestroyHandle();
            }
        }
    }
}
