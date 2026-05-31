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
}
