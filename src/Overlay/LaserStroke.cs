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
internal sealed class LaserStroke : IDisposable
    {
        public const double FadeDuration = 0.9;
        public readonly List<PointF> Points = new List<PointF>();
        public double? CompletedAt;
        private RectangleF bounds;
        private bool hasBounds;
        private PointF[] pointSnapshot;
        private GraphicsPath path;

        public bool ShouldAppend(PointF point)
        {
            if (Points.Count == 0) return true;
            PointF last = Points[Points.Count - 1];
            double dx = last.X - point.X;
            double dy = last.Y - point.Y;
            return Math.Sqrt(dx * dx + dy * dy) >= 2.5;
        }

        public void AddPoint(PointF point)
        {
            Points.Add(point);
            pointSnapshot = null;
            if (path != null)
            {
                path.Dispose();
                path = null;
            }

            if (!hasBounds)
            {
                bounds = new RectangleF(point.X, point.Y, 0.0f, 0.0f);
                hasBounds = true;
                return;
            }

            bounds = RectangleF.FromLTRB(
                Math.Min(bounds.Left, point.X),
                Math.Min(bounds.Top, point.Y),
                Math.Max(bounds.Right, point.X),
                Math.Max(bounds.Bottom, point.Y));
        }

        public RectangleF Bounds(float padding)
        {
            if (!hasBounds)
            {
                RebuildBounds();
            }

            if (!hasBounds)
            {
                return new RectangleF(0.0f, 0.0f, 1.0f, 1.0f);
            }

            return RectangleF.FromLTRB(
                bounds.Left - padding,
                bounds.Top - padding,
                bounds.Right + padding,
                bounds.Bottom + padding);
        }

        public PointF[] Snapshot()
        {
            if (pointSnapshot == null)
            {
                pointSnapshot = Points.ToArray();
            }

            return pointSnapshot;
        }

        public GraphicsPath Path()
        {
            if (path == null)
            {
                path = new GraphicsPath();
                if (Points.Count >= 2)
                {
                    path.AddLines(Snapshot());
                }
            }

            return path;
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

        private void RebuildBounds()
        {
            if (Points.Count == 0)
            {
                hasBounds = false;
                return;
            }

            PointF first = Points[0];
            bounds = new RectangleF(first.X, first.Y, 0.0f, 0.0f);
            hasBounds = true;

            for (int i = 1; i < Points.Count; i++)
            {
                PointF point = Points[i];
                bounds = RectangleF.FromLTRB(
                    Math.Min(bounds.Left, point.X),
                    Math.Min(bounds.Top, point.Y),
                    Math.Max(bounds.Right, point.X),
                    Math.Max(bounds.Bottom, point.Y));
            }
        }

        public void Dispose()
        {
            if (path != null)
            {
                path.Dispose();
                path = null;
            }
        }
    }
}
