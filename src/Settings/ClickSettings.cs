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

            size = ClampRange(size, 16.0, 240.0, 64.0);
            intensity = ClampRange(intensity, 0.05, 2.0, 0.7);
            duration = ClampRange(duration, 0.1, 2.0, 0.48);

            customColorRed = Clamp01(customColorRed);
            customColorGreen = Clamp01(customColorGreen);
            customColorBlue = Clamp01(customColorBlue);
        }

        private static double ClampRange(double value, double min, double max, double fallback)
        {
            if (Double.IsNaN(value) || Double.IsInfinity(value)) return fallback;
            return Math.Max(min, Math.Min(max, value));
        }

        private static double Clamp01(double value)
        {
            if (Double.IsNaN(value) || Double.IsInfinity(value)) return 0.0;
            return Math.Max(0.0, Math.Min(1.0, value));
        }
    }
}
