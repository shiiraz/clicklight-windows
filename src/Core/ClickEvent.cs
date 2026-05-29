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
internal enum ClickKind
    {
        LeftDown,
        LeftUp,
        RightDown,
        RightUp,
        Drag,
        Move
    }

internal struct ClickEvent
    {
        public readonly ClickKind Kind;
        public readonly double X;
        public readonly double Y;
        public readonly double Timestamp;

        public ClickEvent(ClickKind kind, double x, double y, double timestamp)
        {
            Kind = kind;
            X = x;
            Y = y;
            Timestamp = timestamp;
        }
    }
}
