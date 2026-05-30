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
    internal sealed class SettingsStore
    {
        private readonly string settingsPath;
        private readonly bool firstRun;
        private ClickSettings current;

        public event EventHandler SettingsChanged;

        public SettingsStore()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ClickLight");
            settingsPath = Path.Combine(dir, "settings.json");
            current = LoadFromDisk(out firstRun);
        }

        public ClickSettings Settings
        {
            get { return current.Clone(); }
        }

        public bool IsFirstRun
        {
            get { return firstRun; }
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

        private ClickSettings LoadFromDisk(out bool createdDefaults)
        {
            createdDefaults = false;

            try
            {
                if (!File.Exists(settingsPath))
                {
                    ClickSettings defaults = ClickSettings.Defaults();
                    WriteToDisk(defaults);
                    createdDefaults = true;
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
}
