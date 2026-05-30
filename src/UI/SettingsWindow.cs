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
internal sealed class SettingsWindow : Form
    {
        private readonly SettingsStore settingsStore;
        private readonly LaunchAtLoginController launchAtLogin;
        private readonly Func<string> captureStatus;
        private readonly Action previewPulse;
        private readonly float uiScale;
        private readonly Icon windowIcon;
        private readonly ListBox paneList;
        private readonly Panel contentPanel;
        private bool updatingControls;
        private bool applyingChange;
        private int nextY;

        public SettingsWindow(
            SettingsStore settingsStore,
            LaunchAtLoginController launchAtLogin,
            Func<string> captureStatus,
            Action previewPulse)
        {
            uiScale = DetectUiScale();
            this.settingsStore = settingsStore;
            this.launchAtLogin = launchAtLogin;
            this.captureStatus = captureStatus;
            this.previewPulse = previewPulse;

            AutoScaleMode = AutoScaleMode.None;
            Text = "ClickLight Settings";
            windowIcon = TrayIconFactory.CreateIcon();
            Icon = windowIcon;
            Size = new Size(S(760), S(520));
            MinimumSize = new Size(S(700), S(480));
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9.0f);

            paneList = new ListBox();
            paneList.Dock = DockStyle.Fill;
            paneList.BorderStyle = BorderStyle.None;
            paneList.IntegralHeight = false;
            paneList.Font = new Font("Segoe UI", 10.0f);
            paneList.Items.Add("General");
            paneList.Items.Add("Visual Style");
            paneList.Items.Add("Event Visibility");
            paneList.Items.Add("Tray");
            paneList.Items.Add("System");
            paneList.ItemHeight = TextRenderer.MeasureText("Event Visibility", paneList.Font).Height + S(8);
            paneList.SelectedIndexChanged += delegate { BuildSelectedPane(); };

            contentPanel = new Panel();
            contentPanel.Dock = DockStyle.Fill;
            contentPanel.AutoScroll = true;
            contentPanel.BackColor = SystemColors.Window;
            contentPanel.Resize += delegate
            {
                if (Visible && !updatingControls)
                {
                    BuildSelectedPane();
                }
            };

            Panel sidebarPanel = new Panel();
            sidebarPanel.Dock = DockStyle.Left;
            sidebarPanel.Width = SidebarWidth();
            sidebarPanel.BackColor = Color.FromArgb(245, 245, 245);
            sidebarPanel.Controls.Add(paneList);

            Controls.Add(contentPanel);
            Controls.Add(sidebarPanel);

            settingsStore.SettingsChanged += SettingsDidChange;
            paneList.SelectedIndex = 0;
        }

        public void ShowSettings()
        {
            if (!Visible)
            {
                Show();
            }

            if (WindowState == FormWindowState.Minimized)
            {
                WindowState = FormWindowState.Normal;
            }

            EnsureVisibleOnScreen();
            BuildSelectedPane();

            NativeMethods.ShowWindow(Handle, NativeMethods.SW_RESTORE);
            NativeMethods.BringWindowToTop(Handle);
            NativeMethods.SetForegroundWindow(Handle);
            Activate();
            BringToFront();
        }

        public bool ContainsScreenPoint(double x, double y)
        {
            return Visible && Bounds.Contains(new Point((int)Math.Round(x), (int)Math.Round(y)));
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                return;
            }

            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                settingsStore.SettingsChanged -= SettingsDidChange;
                Icon = null;
                if (windowIcon != null)
                {
                    windowIcon.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        private void SettingsDidChange(object sender, EventArgs e)
        {
            if (applyingChange) return;

            if (IsHandleCreated && InvokeRequired)
            {
                BeginInvoke(new Action(BuildSelectedPane));
            }
            else
            {
                BuildSelectedPane();
            }
        }

        private void BuildSelectedPane()
        {
            if (paneList.SelectedIndex < 0) return;

            updatingControls = true;
            contentPanel.SuspendLayout();
            contentPanel.Controls.Clear();
            nextY = S(20);

            switch (paneList.SelectedIndex)
            {
                case 0:
                    AddHeader("General", "Toggle ClickLight and restore defaults.");
                    BuildGeneralPane();
                    break;
                case 1:
                    AddHeader("Visual Style", "Size, intensity, duration, and color of click pulses.");
                    BuildStylePane();
                    break;
                case 2:
                    AddHeader("Event Visibility", "Choose which mouse interactions trigger a pulse.");
                    BuildEventsPane();
                    break;
                case 3:
                    AddHeader("Tray", "Adjust the notification-area status item appearance.");
                    BuildTrayPane();
                    break;
                case 4:
                    AddHeader("System", "Startup and Windows capture status.");
                    BuildSystemPane();
                    break;
            }

            contentPanel.ResumeLayout();
            updatingControls = false;
        }

        private void BuildGeneralPane()
        {
            ClickSettings settings = settingsStore.Settings;

            Panel enableCard = AddCard(84);
            CheckBox enabled = CreateCheckBox(settings.isEnabled);
            enabled.CheckedChanged += delegate
            {
                if (updatingControls) return;
                ApplySetting(delegate(ClickSettings s) { s.isEnabled = enabled.Checked; }, false);
            };
            AddRow(enableCard, 18, "Enable ClickLight", "Show pulse highlights on every click.", enabled);

            Panel resetCard = AddCard(92);
            Button reset = new Button();
            reset.Text = "Reset";
            reset.Width = S(92);
            reset.Height = S(30);
            reset.Click += delegate
            {
                DialogResult result = MessageBox.Show(
                    this,
                    "Restore size, intensity, duration, color, and visibility toggles to their defaults?",
                    "Reset ClickLight settings?",
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Warning);
                if (result == DialogResult.OK)
                {
                    try
                    {
                        applyingChange = true;
                        settingsStore.ResetToDefaults();
                    }
                    finally
                    {
                        applyingChange = false;
                    }
                    BuildSelectedPane();
                }
            };
            AddRow(resetCard, 18, "Reset to Defaults", "Restore size, intensity, duration, color, and toggles.", reset);
        }

        private void BuildStylePane()
        {
            ClickSettings settings = settingsStore.Settings;

            Panel previewCard = AddCard(82);
            Button preview = new Button();
            preview.Text = "Preview Pulse";
            preview.Width = S(118);
            preview.Height = S(30);
            preview.Click += delegate { previewPulse(); };
            AddRow(previewCard, 18, "Preview", "Show the current pulse style at the pointer.", preview);

            Panel sizeCard = AddCard(146);
            AddPresetRow(sizeCard, 44, "Size Preset", ClickSettingOptions.SizePresets, settings.size, delegate(double value)
            {
                ApplySetting(delegate(ClickSettings s) { s.size = value; }, true);
            });
            AddSliderRow(sizeCard, 86, "Size", 16, 240, (int)Math.Round(settings.size), "px", delegate(int value)
            {
                ApplySetting(delegate(ClickSettings s) { s.size = value; }, false);
            });

            Panel intensityCard = AddCard(146);
            AddPresetRow(intensityCard, 44, "Intensity Preset", ClickSettingOptions.IntensityPresets, settings.intensity, delegate(double value)
            {
                ApplySetting(delegate(ClickSettings s) { s.intensity = value; }, true);
            });
            AddScaledSliderRow(intensityCard, 86, "Intensity", 5, 200, (int)Math.Round(settings.intensity * 100.0), 100.0, "", delegate(double value)
            {
                ApplySetting(delegate(ClickSettings s) { s.intensity = value; }, false);
            });

            Panel durationCard = AddCard(146);
            AddPresetRow(durationCard, 44, "Duration Preset", ClickSettingOptions.DurationPresets, settings.duration, delegate(double value)
            {
                ApplySetting(delegate(ClickSettings s) { s.duration = value; }, true);
            });
            AddScaledSliderRow(durationCard, 86, "Duration", 10, 200, (int)Math.Round(settings.duration * 100.0), 100.0, " s", delegate(double value)
            {
                ApplySetting(delegate(ClickSettings s) { s.duration = value; }, false);
            });

            Panel colorCard = AddCard(132);
            ComboBox colorCombo = new ComboBox();
            colorCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            colorCombo.Width = S(150);
            for (int i = 0; i < ClickColorPreset.All.Length; i++)
            {
                string preset = ClickColorPreset.All[i];
                colorCombo.Items.Add(new ColorChoice(preset));
                if (preset == settings.colorPreset)
                {
                    colorCombo.SelectedIndex = i;
                }
            }
            colorCombo.SelectedIndexChanged += delegate
            {
                if (updatingControls || colorCombo.SelectedItem == null) return;
                ColorChoice choice = (ColorChoice)colorCombo.SelectedItem;
                ApplySetting(delegate(ClickSettings s) { s.colorPreset = choice.Preset; }, true);
            };
            AddRow(colorCard, 18, "Color", "Tint applied to every pulse.", colorCombo);

            Button customColor = new Button();
            customColor.Text = "Pick Color";
            customColor.Width = S(100);
            customColor.Height = S(30);
            customColor.Click += delegate { PickCustomColor(); };
            AddRow(colorCard, 72, "Custom Color", "Picking a color switches to Custom.", customColor);
        }

        private void BuildEventsPane()
        {
            ClickSettings settings = settingsStore.Settings;
            Panel card = AddCard(326);

            CheckBox laser = CreateCheckBox(settings.showLaserPointer);
            laser.CheckedChanged += delegate
            {
                if (updatingControls) return;
                ApplySetting(delegate(ClickSettings s) { s.showLaserPointer = laser.Checked; }, true);
            };
            AddRow(card, 18, "Laser Pointer Mode", "Show a fading red pointer and draw temporary strokes while dragging.", laser);

            CheckBox press = CreateCheckBox(settings.showPress);
            press.CheckedChanged += delegate
            {
                if (updatingControls) return;
                ApplySetting(delegate(ClickSettings s) { s.showPress = press.Checked; }, false);
            };
            AddRow(card, 76, "Show Press", "Highlight when the mouse button goes down.", press);

            CheckBox release = CreateCheckBox(settings.showRelease);
            release.CheckedChanged += delegate
            {
                if (updatingControls) return;
                ApplySetting(delegate(ClickSettings s) { s.showRelease = release.Checked; }, false);
            };
            AddRow(card, 134, "Show Release", "Highlight when the mouse button releases.", release);

            CheckBox right = CreateCheckBox(settings.showRightClick);
            right.CheckedChanged += delegate
            {
                if (updatingControls) return;
                ApplySetting(delegate(ClickSettings s) { s.showRightClick = right.Checked; }, false);
            };
            AddRow(card, 192, "Show Right Click", "Highlight secondary-button clicks.", right);

            CheckBox drag = CreateCheckBox(settings.showDrag);
            drag.Enabled = !settings.showLaserPointer;
            drag.CheckedChanged += delegate
            {
                if (updatingControls) return;
                ApplySetting(delegate(ClickSettings s) { s.showDrag = drag.Checked; }, false);
            };
            AddRow(card, 250, "Show Drag", settings.showLaserPointer ? "Laser Pointer Mode replaces the normal drag trail." : "Trail pointer movement while dragging.", drag);
        }

        private void BuildTrayPane()
        {
            ClickSettings settings = settingsStore.Settings;
            Panel card = AddCard(84);
            CheckBox showTrayLabel = CreateCheckBox(settings.showMenuBarText);
            showTrayLabel.CheckedChanged += delegate
            {
                if (updatingControls) return;
                ApplySetting(delegate(ClickSettings s) { s.showMenuBarText = showTrayLabel.Checked; }, false);
            };
            AddRow(card, 18, "Show Capture Status in Tooltip", "Include the click-capture status when hovering the tray icon.", showTrayLabel);
        }

        private void BuildSystemPane()
        {
            Panel startupCard = AddCard(84);
            bool launchEnabled = launchAtLogin.IsEnabled;
            bool manualStartupControl = !launchEnabled && launchAtLogin.RequiresManualEnable;
            if (manualStartupControl)
            {
                Button openStartup = new Button();
                openStartup.Text = "Open Startup Apps";
                openStartup.Width = S(138);
                openStartup.Height = S(30);
                openStartup.Click += delegate { LaunchAtLoginController.OpenStartupAppsSettings(); };
                AddRow(startupCard, 18, "Launch at Login", "Windows requires enabling this from Startup Apps.", openStartup);
            }
            else
            {
                CheckBox launch = CreateCheckBox(launchEnabled);
                launch.CheckedChanged += delegate
                {
                    if (updatingControls) return;
                    bool applied = launchAtLogin.SetEnabled(launch.Checked);
                    if (!applied && launch.Checked)
                    {
                        MessageBox.Show(
                            this,
                            "Windows requires this startup setting to be enabled from Startup Apps.",
                            "Open Startup Apps",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                        LaunchAtLoginController.OpenStartupAppsSettings();
                    }
                    BuildSelectedPane();
                };
                AddRow(startupCard, 18, "Launch at Login", "Open ClickLight automatically after signing in.", launch);
            }

            Panel captureCard = AddCard(156);
            Label status = new Label();
            status.Text = captureStatus();
            status.AutoSize = false;
            status.TextAlign = ContentAlignment.MiddleRight;
            status.Width = S(170);
            status.Height = S(26);
            AddRow(captureCard, 18, "Click Capture", "Current global mouse hook status.", status);

            Label note = new Label();
            note.Text = "Windows may block non-elevated hooks from seeing clicks in elevated apps. Exclusive fullscreen apps may also hide overlays.";
            note.Location = new Point(S(16), S(82));
            note.Width = captureCard.Width - S(32);
            note.Height = Math.Max(S(44), TextHeight(note.Text, note.Font, note.Width));
            note.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            note.ForeColor = SystemColors.GrayText;
            captureCard.Controls.Add(note);

            Panel updatesCard = AddCard(76);
            Label updates = new Label();
            updates.Text = "Not Configured";
            updates.AutoSize = false;
            updates.TextAlign = ContentAlignment.MiddleRight;
            updates.Width = S(170);
            updates.Height = S(26);
            AddRow(updatesCard, 18, "Updates", "Installer and update strategy are not configured yet.", updates);
        }

        private void AddHeader(string title, string subtitle)
        {
            Label titleLabel = new Label();
            titleLabel.Text = title;
            titleLabel.Font = new Font("Segoe UI", 18.0f, FontStyle.Bold);
            titleLabel.Location = new Point(S(24), nextY);
            titleLabel.AutoSize = true;
            contentPanel.Controls.Add(titleLabel);
            nextY += titleLabel.PreferredHeight + S(6);

            Label subtitleLabel = new Label();
            subtitleLabel.Text = subtitle;
            subtitleLabel.ForeColor = SystemColors.GrayText;
            subtitleLabel.Location = new Point(S(26), nextY);
            subtitleLabel.Width = ContentWidth();
            subtitleLabel.Height = TextHeight(subtitle, subtitleLabel.Font, subtitleLabel.Width);
            subtitleLabel.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            contentPanel.Controls.Add(subtitleLabel);
            nextY += subtitleLabel.Height + S(24);
        }

        private Panel AddCard(int height)
        {
            Panel card = new Panel();
            card.BorderStyle = BorderStyle.FixedSingle;
            card.BackColor = Color.FromArgb(250, 250, 250);
            card.Location = new Point(S(24), nextY);
            card.Width = ContentWidth();
            card.Height = S(height);
            card.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            contentPanel.Controls.Add(card);
            nextY += card.Height + S(14);
            return card;
        }

        private void AddRow(Panel card, int y, string title, string subtitle, Control trailing)
        {
            int rowY = S(y);
            int inset = S(16);
            int titleToSubtitle = S(24);
            int trailingRight = S(18);
            int trailingTopOffset = S(8);

            Label titleLabel = new Label();
            titleLabel.Text = title;
            titleLabel.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            titleLabel.Location = new Point(inset, rowY);
            titleLabel.AutoSize = true;
            card.Controls.Add(titleLabel);

            Label subtitleLabel = new Label();
            subtitleLabel.Text = subtitle;
            subtitleLabel.ForeColor = SystemColors.GrayText;
            subtitleLabel.Location = new Point(inset, rowY + titleToSubtitle);
            subtitleLabel.Width = Math.Max(S(200), card.Width - trailing.Width - S(58));
            subtitleLabel.Height = Math.Max(S(34), TextHeight(subtitle, subtitleLabel.Font, subtitleLabel.Width));
            subtitleLabel.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            card.Controls.Add(subtitleLabel);

            trailing.Location = new Point(card.Width - trailing.Width - trailingRight, rowY + trailingTopOffset);
            trailing.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            card.Controls.Add(trailing);
        }

        private CheckBox CreateCheckBox(bool isChecked)
        {
            CheckBox checkBox = new CheckBox();
            checkBox.Checked = isChecked;
            checkBox.AutoSize = true;
            checkBox.Width = S(22);
            checkBox.Height = S(22);
            return checkBox;
        }

        private void AddPresetRow(Panel card, int y, string title, ClickNumericPreset[] presets, double selected, Action<double> select)
        {
            ComboBox combo = new ComboBox();
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.Width = S(150);
            int selectedIndex = -1;
            for (int i = 0; i < presets.Length; i++)
            {
                combo.Items.Add(new PresetChoice(presets[i].Title, presets[i].Value));
                if (Math.Abs(presets[i].Value - selected) < 0.01)
                {
                    selectedIndex = i;
                }
            }
            if (selectedIndex >= 0)
            {
                combo.SelectedIndex = selectedIndex;
            }
            else
            {
                combo.Items.Add(new PresetChoice("Custom", selected));
                combo.SelectedIndex = combo.Items.Count - 1;
            }

            combo.SelectedIndexChanged += delegate
            {
                if (updatingControls || combo.SelectedItem == null) return;
                PresetChoice choice = (PresetChoice)combo.SelectedItem;
                select(choice.Value);
            };

            AddRow(card, y, title, "Quick presets stay synced with the tray menu.", combo);
        }

        private void AddSliderRow(Panel card, int y, string title, int minimum, int maximum, int value, string suffix, Action<int> select)
        {
            Panel trailing = CreateSliderPanel(minimum, maximum, value, suffix, delegate(int trackValue)
            {
                select(trackValue);
            });
            AddRow(card, y, title, minimum.ToString() + " to " + maximum.ToString(), trailing);
        }

        private void AddScaledSliderRow(Panel card, int y, string title, int minimum, int maximum, int value, double scale, string suffix, Action<double> select)
        {
            Panel trailing = CreateSliderPanel(minimum, maximum, value, suffix, delegate(int trackValue)
            {
                select((double)trackValue / scale);
            });
            AddRow(card, y, title, ((double)minimum / scale).ToString("0.##") + " to " + ((double)maximum / scale).ToString("0.##"), trailing);
        }

        private Panel CreateSliderPanel(int minimum, int maximum, int value, string suffix, Action<int> select)
        {
            Panel panel = new Panel();
            panel.Width = S(322);
            panel.Height = S(42);

            TrackBar track = new TrackBar();
            track.Minimum = minimum;
            track.Maximum = maximum;
            track.Value = Math.Max(minimum, Math.Min(maximum, value));
            track.TickStyle = TickStyle.None;
            track.Width = S(230);
            track.Location = new Point(0, S(4));
            panel.Controls.Add(track);

            Label readout = new Label();
            readout.Text = FormatSliderReadout(track.Value, suffix);
            readout.Location = new Point(S(236), S(10));
            readout.Width = S(82);
            readout.Height = S(22);
            readout.TextAlign = ContentAlignment.MiddleRight;
            panel.Controls.Add(readout);

            track.ValueChanged += delegate
            {
                if (updatingControls) return;
                readout.Text = FormatSliderReadout(track.Value, suffix);
                select(track.Value);
            };

            return panel;
        }

        private static string FormatSliderReadout(int value, string suffix)
        {
            if (suffix == " s")
            {
                return ((double)value / 100.0).ToString("0.00") + suffix;
            }
            if (String.IsNullOrEmpty(suffix))
            {
                return ((double)value / 100.0).ToString("0.00");
            }
            return value.ToString() + " " + suffix;
        }

        private void PickCustomColor()
        {
            ClickSettings settings = settingsStore.Settings;
            using (ColorDialog dialog = new ColorDialog())
            {
                dialog.FullOpen = true;
                dialog.Color = Color.FromArgb(
                    UnitToByte(settings.customColorRed),
                    UnitToByte(settings.customColorGreen),
                    UnitToByte(settings.customColorBlue));
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    Color color = dialog.Color;
                    ApplySetting(delegate(ClickSettings s)
                    {
                        s.customColorRed = (double)color.R / 255.0;
                        s.customColorGreen = (double)color.G / 255.0;
                        s.customColorBlue = (double)color.B / 255.0;
                        s.colorPreset = "custom";
                    }, true);
                }
            }
        }

        private void ApplySetting(Action<ClickSettings> mutate, bool rebuild)
        {
            try
            {
                applyingChange = true;
                settingsStore.Update(mutate);
            }
            finally
            {
                applyingChange = false;
            }
            if (rebuild)
            {
                BuildSelectedPane();
            }
        }

        private int ContentWidth()
        {
            return Math.Max(S(420), contentPanel.ClientSize.Width - S(48));
        }

        private void EnsureVisibleOnScreen()
        {
            Rectangle bounds = Bounds;
            for (int i = 0; i < Screen.AllScreens.Length; i++)
            {
                if (Screen.AllScreens[i].WorkingArea.IntersectsWith(bounds))
                {
                    return;
                }
            }

            Rectangle workingArea = Screen.FromPoint(Cursor.Position).WorkingArea;
            Location = new Point(
                workingArea.Left + Math.Max(0, (workingArea.Width - Width) / 2),
                workingArea.Top + Math.Max(0, (workingArea.Height - Height) / 2));
        }

        private int SidebarWidth()
        {
            return Math.Max(S(150), TextRenderer.MeasureText("Event Visibility", paneList.Font).Width + S(42));
        }

        private int S(int value)
        {
            return Math.Max(1, (int)Math.Round(value * uiScale));
        }

        private static int TextHeight(string text, Font font, int width)
        {
            Size measured = TextRenderer.MeasureText(
                text,
                font,
                new Size(Math.Max(1, width), 10000),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
            return measured.Height;
        }

        private static float DetectUiScale()
        {
            try
            {
                using (Graphics graphics = Graphics.FromHwnd(IntPtr.Zero))
                {
                    return Math.Max(1.0f, graphics.DpiX / 96.0f);
                }
            }
            catch
            {
                return 1.0f;
            }
        }

        private static int UnitToByte(double value)
        {
            if (Double.IsNaN(value) || Double.IsInfinity(value)) value = 0.0;
            return (int)Math.Round(Math.Max(0.0, Math.Min(1.0, value)) * 255.0);
        }

        private sealed class PresetChoice
        {
            public readonly string Title;
            public readonly double Value;

            public PresetChoice(string title, double value)
            {
                Title = title;
                Value = value;
            }

            public override string ToString()
            {
                return Title;
            }
        }

        private sealed class ColorChoice
        {
            public readonly string Preset;

            public ColorChoice(string preset)
            {
                Preset = preset;
            }

            public override string ToString()
            {
                return ClickColorPreset.Title(Preset);
            }
        }
    }
}
