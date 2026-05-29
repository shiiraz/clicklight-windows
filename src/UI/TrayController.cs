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
}
