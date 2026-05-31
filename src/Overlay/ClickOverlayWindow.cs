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
internal sealed class ClickOverlayWindow : Form
    {
        private readonly Timer displayTimer;
        private readonly List<ClickPulse> pulses;
        private readonly List<LaserStroke> completedLaserStrokes;
        private ClickSettings settings;
        private Rectangle screenFrame;
        private LaserCursor laserCursor;
        private LaserStroke activeLaserStroke;

        public ClickOverlayWindow(Rectangle screenFrame, ClickSettings settings)
        {
            this.settings = settings.Clone();
            pulses = new List<ClickPulse>();
            completedLaserStrokes = new List<LaserStroke>();
            displayTimer = new Timer();
            displayTimer.Interval = 16;
            displayTimer.Tick += delegate { RenderFrame(); };

            this.screenFrame = screenFrame;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = new Rectangle(screenFrame.Left, screenFrame.Top, 1, 1);
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.Black;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= NativeMethods.WS_EX_LAYERED;
                cp.ExStyle |= NativeMethods.WS_EX_TRANSPARENT;
                cp.ExStyle |= NativeMethods.WS_EX_TOOLWINDOW;
                cp.ExStyle |= NativeMethods.WS_EX_NOACTIVATE;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        public void ShowInactive()
        {
            if (!Visible)
            {
                Show();
                RenderBlankFrame();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                displayTimer.Dispose();
                DisposeLaserContent();
            }

            base.Dispose(disposing);
        }

        public void Apply(ClickSettings newSettings)
        {
            settings = newSettings.Clone();
            if (!settings.showLaserPointer)
            {
                laserCursor = null;
                DisposeLaserContent();
                RenderFrame();
            }
        }

        public void ShowEvent(ClickEvent clickEvent, ClickSettings newSettings)
        {
            settings = newSettings.Clone();

            PointF localPoint = new PointF(
                (float)(clickEvent.X - screenFrame.Left),
                (float)(clickEvent.Y - screenFrame.Top));

            if (settings.showLaserPointer)
            {
                if (clickEvent.Kind == ClickKind.Move)
                {
                    ShowLaserCursor(localPoint);
                    return;
                }
                if (clickEvent.Kind == ClickKind.Drag)
                {
                    AppendLaserPoint(localPoint);
                    return;
                }
                if (clickEvent.Kind == ClickKind.LeftUp || clickEvent.Kind == ClickKind.RightUp)
                {
                    CompleteLaserStroke();
                }
            }

            if (!ShouldShowPulse(settings, clickEvent.Kind))
            {
                return;
            }

            pulses.Add(new ClickPulse(
                clickEvent.Kind,
                localPoint,
                Clock.NowSeconds(),
                DurationFor(settings, clickEvent.Kind),
                SizeFor(settings, clickEvent.Kind),
                settings.intensity,
                ClickColorPreset.ColorForKind(settings, clickEvent.Kind)));

            StartDisplayTimer();
            RenderFrame();
        }

        private void ShowLaserCursor(PointF point)
        {
            laserCursor = new LaserCursor(point, Clock.NowSeconds());
            StartDisplayTimer();
        }

        private void AppendLaserPoint(PointF point)
        {
            double now = Clock.NowSeconds();

            if (activeLaserStroke == null)
            {
                activeLaserStroke = new LaserStroke();
                activeLaserStroke.AddPoint(point);
            }
            else if (activeLaserStroke.ShouldAppend(point))
            {
                activeLaserStroke.AddPoint(point);
            }

            if (activeLaserStroke.Points.Count == 1)
            {
                activeLaserStroke.AddPoint(point);
            }

            laserCursor = new LaserCursor(point, now);
            StartDisplayTimer();
        }

        private void CompleteLaserStroke()
        {
            if (activeLaserStroke == null) return;
            activeLaserStroke.CompletedAt = Clock.NowSeconds();
            completedLaserStrokes.Add(activeLaserStroke);
            activeLaserStroke = null;
            StartDisplayTimer();
        }

        private void StartDisplayTimer()
        {
            if (!displayTimer.Enabled)
            {
                displayTimer.Start();
            }
        }

        private void StopDisplayTimer()
        {
            if (displayTimer.Enabled)
            {
                displayTimer.Stop();
            }
        }

        private void RenderFrame()
        {
            double now = Clock.NowSeconds();
            pulses.RemoveAll(delegate(ClickPulse pulse) { return pulse.IsExpired(now); });
            RemoveExpiredLaserStrokes(now);

            bool hasLaserCursor = laserCursor != null && !laserCursor.IsExpired(now);
            bool hasContent = pulses.Count > 0 || hasLaserCursor || activeLaserStroke != null || completedLaserStrokes.Count > 0;

            if (!hasContent)
            {
                if (Visible && IsHandleCreated)
                {
                    RenderBlankFrame();
                }
                StopDisplayTimer();
                Hide();
                return;
            }

            bool hasLaserStrokeContent = settings.showLaserPointer &&
                (activeLaserStroke != null || completedLaserStrokes.Count > 0);

            // Laser strokes keep a fixed monitor-sized surface while active. Moving
            // a layered HWND mid-stroke can produce a one-frame offset on Windows.
            // Keep it one pixel short of fullscreen so Focus Assist does not treat
            // the click-through overlay as a fullscreen app.
            Rectangle localRenderBounds = hasLaserStrokeContent
                ? LaserStrokeRenderBounds()
                : ContentBounds(now);
            Rectangle targetBounds = new Rectangle(
                screenFrame.Left + localRenderBounds.Left,
                screenFrame.Top + localRenderBounds.Top,
                localRenderBounds.Width,
                localRenderBounds.Height);
            Bounds = targetBounds;

            if (!Visible)
            {
                Show();
            }

            if (!IsHandleCreated) return;

            using (Bitmap bitmap = new Bitmap(Math.Max(1, targetBounds.Width), Math.Max(1, targetBounds.Height), PixelFormat.Format32bppPArgb))
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.TranslateTransform(-localRenderBounds.Left, -localRenderBounds.Top);

                DrawLaser(g, now);
                for (int i = 0; i < pulses.Count; i++)
                {
                    DrawPulse(g, pulses[i], now);
                }

                UpdateLayeredBitmap(bitmap, targetBounds);
            }
        }

        private Rectangle ContentBounds(double now)
        {
            RectangleF? content = null;

            for (int i = 0; i < pulses.Count; i++)
            {
                AddBounds(ref content, PulseBounds(pulses[i]));
            }

            if (settings.showLaserPointer)
            {
                if (laserCursor != null && !laserCursor.IsExpired(now))
                {
                    AddBounds(ref content, BoundsAround(laserCursor.Point, 24.0f));
                }

                if (activeLaserStroke != null)
                {
                    AddBounds(ref content, StrokeBounds(activeLaserStroke, 24.0f));
                }

                for (int i = 0; i < completedLaserStrokes.Count; i++)
                {
                    AddBounds(ref content, StrokeBounds(completedLaserStrokes[i], 24.0f));
                }
            }

            RectangleF contentBounds = content.GetValueOrDefault(new RectangleF(0.0f, 0.0f, 1.0f, 1.0f));
            int left = Math.Max(0, (int)Math.Floor(contentBounds.Left));
            int top = Math.Max(0, (int)Math.Floor(contentBounds.Top));
            int right = Math.Min(screenFrame.Width, (int)Math.Ceiling(contentBounds.Right));
            int bottom = Math.Min(screenFrame.Height, (int)Math.Ceiling(contentBounds.Bottom));

            return new Rectangle(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
        }

        private Rectangle LaserStrokeRenderBounds()
        {
            int width = Math.Max(1, screenFrame.Width);
            int height = Math.Max(1, screenFrame.Height);

            if (height > 1)
            {
                height -= 1;
            }
            else if (width > 1)
            {
                width -= 1;
            }

            return new Rectangle(0, 0, width, height);
        }

        private static RectangleF PulseBounds(ClickPulse pulse)
        {
            float radius = (float)(pulse.BaseSize * 1.35 + 48.0);
            return BoundsAround(pulse.Point, radius);
        }

        private static RectangleF StrokeBounds(LaserStroke stroke, float padding)
        {
            return stroke.Bounds(padding);
        }

        private static RectangleF BoundsAround(PointF point, float radius)
        {
            return new RectangleF(point.X - radius, point.Y - radius, radius * 2.0f, radius * 2.0f);
        }

        private static void AddBounds(ref RectangleF? target, RectangleF addition)
        {
            target = target.HasValue ? RectangleF.Union(target.Value, addition) : addition;
        }

        private void RenderBlankFrame()
        {
            if (!IsHandleCreated) return;
            Rectangle targetBounds = new Rectangle(Left, Top, Math.Max(1, Width), Math.Max(1, Height));
            using (Bitmap bitmap = new Bitmap(targetBounds.Width, targetBounds.Height, PixelFormat.Format32bppPArgb))
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                UpdateLayeredBitmap(bitmap, targetBounds);
            }
        }

        private void DrawLaser(Graphics g, double now)
        {
            if (!settings.showLaserPointer) return;

            for (int i = 0; i < completedLaserStrokes.Count; i++)
            {
                LaserStroke stroke = completedLaserStrokes[i];
                DrawLaserStroke(g, stroke, stroke.Alpha(now));
            }

            if (activeLaserStroke != null)
            {
                DrawLaserStroke(g, activeLaserStroke, 0.95);
            }

            if (laserCursor == null || laserCursor.IsExpired(now)) return;

            double alpha = laserCursor.Alpha(now);
            Color laserColor = UnitColor(1.0, 0.16, 0.24);
            FillEllipse(g, laserCursor.Point, 14.0, WithAlpha(laserColor, alpha * 0.18));
            FillEllipse(g, laserCursor.Point, 6.0, WithAlpha(laserColor, alpha));
        }

        private void DrawLaserStroke(Graphics g, LaserStroke stroke, double alpha)
        {
            if (stroke.Points.Count < 2 || alpha <= 0.0) return;

            Color laserColor = UnitColor(1.0, 0.16, 0.24);
            using (Pen glow = new Pen(WithAlpha(laserColor, alpha * 0.2), 14.0f))
            using (Pen core = new Pen(WithAlpha(laserColor, alpha), 5.0f))
            {
                GraphicsPath path = stroke.Path();
                glow.StartCap = LineCap.Round;
                glow.EndCap = LineCap.Round;
                glow.LineJoin = LineJoin.Round;
                core.StartCap = LineCap.Round;
                core.EndCap = LineCap.Round;
                core.LineJoin = LineJoin.Round;
                g.DrawPath(glow, path);
                g.DrawPath(core, path);
            }
        }

        private void DrawPulse(Graphics g, ClickPulse pulse, double now)
        {
            double progress = pulse.Progress(now);
            double eased = 1.0 - Math.Pow(1.0 - progress, 3.0);
            double fade = 1.0 - eased;
            double visualIntensity = Math.Max(0.15, Math.Min(1.35, pulse.Intensity));
            double alpha = Clamp01(fade * (0.18 + visualIntensity * 0.78));
            double lineWidth = Math.Max(2.25, pulse.BaseSize * (0.035 + visualIntensity * 0.045));

            if (pulse.Kind == ClickKind.LeftDown)
            {
                DrawGlowIfNeeded(g, pulse.Point, pulse.BaseSize * (0.28 + 0.78 * eased), pulse.Color, fade * visualIntensity);
                DrawRing(g, pulse.Point, pulse.BaseSize * (0.18 + 0.62 * eased), lineWidth, pulse.Color, alpha);
                DrawDot(g, pulse.Point, pulse.BaseSize * 0.085, pulse.Color, alpha * 0.75);
            }
            else if (pulse.Kind == ClickKind.LeftUp)
            {
                double releaseRadius = pulse.BaseSize * (0.76 - 0.42 * eased);
                double releaseAlpha = alpha * 0.55;
                DrawGlowIfNeeded(g, pulse.Point, releaseRadius * 1.25, pulse.Color, fade * visualIntensity * 0.45);
                DrawRing(g, pulse.Point, releaseRadius, lineWidth * 0.55, pulse.Color, releaseAlpha);
                DrawDot(g, pulse.Point, pulse.BaseSize * 0.055, pulse.Color, releaseAlpha * 0.6);
            }
            else if (pulse.Kind == ClickKind.RightDown)
            {
                DrawGlowIfNeeded(g, pulse.Point, pulse.BaseSize * (0.28 + 0.7 * eased), pulse.Color, fade * visualIntensity);
                DrawRing(g, pulse.Point, pulse.BaseSize * (0.18 + 0.54 * eased), lineWidth, pulse.Color, alpha);
                DrawCrosshair(g, pulse.Point, pulse.BaseSize * 0.28, pulse.Color, alpha * 0.85);
            }
            else if (pulse.Kind == ClickKind.RightUp)
            {
                double releaseRadius = pulse.BaseSize * (0.68 - 0.36 * eased);
                double releaseAlpha = alpha * 0.5;
                DrawGlowIfNeeded(g, pulse.Point, releaseRadius * 1.22, pulse.Color, fade * visualIntensity * 0.4);
                DrawRing(g, pulse.Point, releaseRadius, lineWidth * 0.55, pulse.Color, releaseAlpha);
                DrawCrosshair(g, pulse.Point, pulse.BaseSize * (0.16 + 0.08 * fade), pulse.Color, releaseAlpha * 0.7);
            }
            else if (pulse.Kind == ClickKind.Drag)
            {
                DrawDot(g, pulse.Point, pulse.BaseSize * (0.08 + 0.065 * visualIntensity), pulse.Color, alpha * 0.78);
            }
        }

        private void DrawGlowIfNeeded(Graphics g, PointF point, double radius, Color color, double alpha)
        {
            if (settings.intensity < 0.7) return;
            double glowAlpha = Clamp01(alpha * (settings.intensity >= 1.2 ? 0.18 : 0.08));
            FillEllipse(g, point, radius, WithAlpha(color, glowAlpha));
        }

        private static void DrawRing(Graphics g, PointF point, double radius, double lineWidth, Color color, double alpha)
        {
            using (Pen pen = new Pen(WithAlpha(color, alpha), (float)lineWidth))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                g.DrawEllipse(pen, (float)(point.X - radius), (float)(point.Y - radius), (float)(radius * 2.0), (float)(radius * 2.0));
            }
        }

        private static void DrawDot(Graphics g, PointF point, double radius, Color color, double alpha)
        {
            FillEllipse(g, point, radius, WithAlpha(color, alpha));
        }

        private static void DrawCrosshair(Graphics g, PointF point, double size, Color color, double alpha)
        {
            using (Pen pen = new Pen(WithAlpha(color, alpha), (float)Math.Max(2.0, size * 0.12)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawLine(pen, (float)(point.X - size), point.Y, (float)(point.X + size), point.Y);
                g.DrawLine(pen, point.X, (float)(point.Y - size), point.X, (float)(point.Y + size));
            }
        }

        private static void FillEllipse(Graphics g, PointF point, double radius, Color color)
        {
            using (SolidBrush brush = new SolidBrush(color))
            {
                g.FillEllipse(brush, (float)(point.X - radius), (float)(point.Y - radius), (float)(radius * 2.0), (float)(radius * 2.0));
            }
        }

        private static bool ShouldShowPulse(ClickSettings settings, ClickKind kind)
        {
            if (kind == ClickKind.LeftDown) return settings.showPress;
            if (kind == ClickKind.LeftUp) return settings.showRelease;
            if (kind == ClickKind.RightDown || kind == ClickKind.RightUp) return settings.showRightClick;
            if (kind == ClickKind.Drag) return settings.showDrag && !settings.showLaserPointer;
            return false;
        }

        private static double DurationFor(ClickSettings settings, ClickKind kind)
        {
            if (kind == ClickKind.Drag) return Math.Min(0.38, settings.duration * 0.82);
            if (kind == ClickKind.LeftUp || kind == ClickKind.RightUp) return settings.duration * 0.78;
            if (kind == ClickKind.LeftDown || kind == ClickKind.RightDown) return settings.duration;
            return 0.0;
        }

        private static double SizeFor(ClickSettings settings, ClickKind kind)
        {
            if (kind == ClickKind.Drag) return settings.size * 0.6;
            if (kind == ClickKind.LeftUp || kind == ClickKind.RightUp) return settings.size * 0.82;
            if (kind == ClickKind.LeftDown || kind == ClickKind.RightDown) return settings.size;
            return 0.0;
        }

        private void UpdateLayeredBitmap(Bitmap bitmap, Rectangle targetBounds)
        {
            IntPtr screenDc = IntPtr.Zero;
            IntPtr memDc = IntPtr.Zero;
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr oldBitmap = IntPtr.Zero;

            try
            {
                screenDc = NativeMethods.GetDC(IntPtr.Zero);
                memDc = NativeMethods.CreateCompatibleDC(screenDc);
                hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
                oldBitmap = NativeMethods.SelectObject(memDc, hBitmap);

                NativeMethods.POINT top = new NativeMethods.POINT(targetBounds.Left, targetBounds.Top);
                NativeMethods.SIZE size = new NativeMethods.SIZE(targetBounds.Width, targetBounds.Height);
                NativeMethods.POINT source = new NativeMethods.POINT(0, 0);
                NativeMethods.BLENDFUNCTION blend = new NativeMethods.BLENDFUNCTION();
                blend.BlendOp = NativeMethods.AC_SRC_OVER;
                blend.BlendFlags = 0;
                blend.SourceConstantAlpha = 255;
                blend.AlphaFormat = NativeMethods.AC_SRC_ALPHA;

                NativeMethods.UpdateLayeredWindow(
                    Handle,
                    screenDc,
                    ref top,
                    ref size,
                    memDc,
                    ref source,
                    0,
                    ref blend,
                    NativeMethods.ULW_ALPHA);
            }
            finally
            {
                if (oldBitmap != IntPtr.Zero) NativeMethods.SelectObject(memDc, oldBitmap);
                if (hBitmap != IntPtr.Zero) NativeMethods.DeleteObject(hBitmap);
                if (memDc != IntPtr.Zero) NativeMethods.DeleteDC(memDc);
                if (screenDc != IntPtr.Zero) NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        private void RemoveExpiredLaserStrokes(double now)
        {
            for (int i = completedLaserStrokes.Count - 1; i >= 0; i--)
            {
                LaserStroke stroke = completedLaserStrokes[i];
                if (stroke.IsExpired(now))
                {
                    stroke.Dispose();
                    completedLaserStrokes.RemoveAt(i);
                }
            }
        }

        private void DisposeLaserContent()
        {
            if (activeLaserStroke != null)
            {
                activeLaserStroke.Dispose();
                activeLaserStroke = null;
            }

            for (int i = 0; i < completedLaserStrokes.Count; i++)
            {
                completedLaserStrokes[i].Dispose();
            }
            completedLaserStrokes.Clear();
        }

        private static Color UnitColor(double r, double g, double b)
        {
            return Color.FromArgb(255, ToByte(r), ToByte(g), ToByte(b));
        }

        private static Color WithAlpha(Color color, double alpha)
        {
            return Color.FromArgb(ToByte(Clamp01(alpha)), color.R, color.G, color.B);
        }

        private static int ToByte(double value)
        {
            if (Double.IsNaN(value) || Double.IsInfinity(value)) value = 0.0;
            return (int)Math.Round(Math.Max(0.0, Math.Min(1.0, value)) * 255.0);
        }

        private static double Clamp01(double value)
        {
            if (Double.IsNaN(value) || Double.IsInfinity(value)) return 0.0;
            return Math.Max(0.0, Math.Min(1.0, value));
        }
    }
}
