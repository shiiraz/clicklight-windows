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
internal sealed class LaserStroke
    {
        public const double FadeDuration = 0.9;
        public readonly List<PointF> Points = new List<PointF>();
        public double? CompletedAt;

        public bool ShouldAppend(PointF point)
        {
            if (Points.Count == 0) return true;
            PointF last = Points[Points.Count - 1];
            double dx = last.X - point.X;
            double dy = last.Y - point.Y;
            return Math.Sqrt(dx * dx + dy * dy) >= 2.5;
        }

        public double Alpha(double time)
        {
            if (!CompletedAt.HasValue) return 1.0;
            double progress = Math.Max(0.0, Math.Min(1.0, (time - CompletedAt.Value) / FadeDuration));
            return 1.0 - progress;
        }

        public bool IsExpired(double time)
        {
            return CompletedAt.HasValue && time - CompletedAt.Value >= FadeDuration;
        }
    }
}
