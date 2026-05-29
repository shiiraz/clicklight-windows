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
internal sealed class LaserCursor
    {
        public const double FadeDuration = 0.42;
        public readonly PointF Point;
        public readonly double UpdatedAt;

        public LaserCursor(PointF point, double updatedAt)
        {
            Point = point;
            UpdatedAt = updatedAt;
        }

        public double Alpha(double time)
        {
            double progress = Math.Max(0.0, Math.Min(1.0, (time - UpdatedAt) / FadeDuration));
            return 1.0 - progress;
        }

        public bool IsExpired(double time)
        {
            return time - UpdatedAt >= FadeDuration;
        }
    }
}
