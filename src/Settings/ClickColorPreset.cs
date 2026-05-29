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
}
