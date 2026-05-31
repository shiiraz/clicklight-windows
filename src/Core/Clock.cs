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
internal static class Clock
    {
        public static double NowSeconds()
        {
            return (double)Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        }
    }
}
