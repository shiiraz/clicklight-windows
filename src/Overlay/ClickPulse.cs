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
}
