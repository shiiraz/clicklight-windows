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
internal static class TrayIconFactory
    {
        public static Icon CreateIcon()
        {
            Icon assetIcon = TryLoadIconAsset();
            if (assetIcon != null)
            {
                return assetIcon;
            }

            Bitmap bitmap = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (Pen ring = new Pen(Color.FromArgb(230, 0, 189, 255), 3.0f))
                using (SolidBrush dot = new SolidBrush(Color.FromArgb(255, 0, 189, 255)))
                using (Pen cursor = new Pen(Color.White, 2.0f))
                using (SolidBrush shadow = new SolidBrush(Color.FromArgb(90, 0, 0, 0)))
                {
                    g.FillEllipse(shadow, 5, 5, 22, 22);
                    g.DrawEllipse(ring, 6, 6, 18, 18);
                    g.FillEllipse(dot, 13, 13, 6, 6);
                    PointF[] points =
                    {
                        new PointF(18, 18),
                        new PointF(27, 25),
                        new PointF(22, 26),
                        new PointF(24, 31),
                        new PointF(21, 32),
                        new PointF(19, 27),
                        new PointF(15, 30)
                    };
                    g.DrawPolygon(cursor, points);
                }
            }

            IntPtr handle = bitmap.GetHicon();
            Icon icon = (Icon)Icon.FromHandle(handle).Clone();
            NativeMethods.DestroyIcon(handle);
            bitmap.Dispose();
            return icon;
        }

        private static Icon TryLoadIconAsset()
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string currentDirectory = Environment.CurrentDirectory;
            string[] candidates =
            {
                Path.Combine(baseDirectory, "assets", "tray", "cursorcue-tray.ico"),
                Path.Combine(baseDirectory, "..", "assets", "tray", "cursorcue-tray.ico"),
                Path.Combine(currentDirectory, "assets", "tray", "cursorcue-tray.ico")
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                string path = Path.GetFullPath(candidates[i]);
                if (!File.Exists(path))
                {
                    continue;
                }

                try
                {
                    return new Icon(path);
                }
                catch
                {
                }
            }

            return null;
        }
    }
}
