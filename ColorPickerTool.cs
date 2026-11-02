// Language: C#
// Filename: ColorPickerTool.cs
// Runs on: Windows 10 / Windows 11 (.NET 9.0-windows10.0.19041.0)
// Targets: Dynamic Island Atoll Color Picker Tool

using System;
using System.Drawing;
using System.Windows;
using System.Windows.Media;

namespace DynamicIsland
{
    public class ColorPickedEventArgs : EventArgs
    {
        public System.Windows.Media.Color Color { get; set; }
        public string HexString { get; set; } = string.Empty;
        public string RgbString { get; set; } = string.Empty;
    }

    public static class ColorPickerTool
    {
        public static event EventHandler<ColorPickedEventArgs>? ColorPicked;

        public static ColorPickedEventArgs? PickColorAtCursor()
        {
            try
            {
                if (!NativeMethods.GetCursorPos(out var pt)) return null;

                IntPtr hdc = NativeMethods.GetDC(IntPtr.Zero);
                if (hdc == IntPtr.Zero) return null;

                uint pixel = NativeMethods.GetPixel(hdc, pt.X, pt.Y);
                NativeMethods.ReleaseDC(IntPtr.Zero, hdc);

                byte r = (byte)(pixel & 0x000000FF);
                byte g = (byte)((pixel & 0x0000FF00) >> 8);
                byte b = (byte)((pixel & 0x00FF0000) >> 16);

                var color = System.Windows.Media.Color.FromRgb(r, g, b);
                string hex = $"#{r:X2}{g:X2}{b:X2}";
                string rgb = $"rgb({r}, {g}, {b})";

                System.Windows.Clipboard.SetText(hex);
                SoundManager.Play(AtollSound.Feedback);

                var args = new ColorPickedEventArgs
                {
                    Color = color,
                    HexString = hex,
                    RgbString = rgb
                };

                ColorPicked?.Invoke(null, args);
                return args;
            }
            catch
            {
                return null;
            }
        }
    }
}
