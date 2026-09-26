using System.Drawing;
using System.Drawing.Drawing2D;

namespace HeadphoneStatus;

internal static class IconFactory
{
    public static Icon CreateHeadphoneIcon(Color color)
    {
        const int size = 32;
        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using var pen = new Pen(color, 3.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using var earBrush = new SolidBrush(color);

            // Headband: an arc across the top.
            var bandRect = new RectangleF(5, 4, size - 10, size - 8);
            g.DrawArc(pen, bandRect, 180, 180);

            // Ear cups.
            g.FillEllipse(earBrush, 3, size / 2f, 8, 12);
            g.FillEllipse(earBrush, size - 11, size / 2f, 8, 12);
        }

        IntPtr hIcon = bmp.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(hIcon);
            return (Icon)temp.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(hIcon);
        }
    }
}

internal static class NativeMethods
{
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern bool DestroyIcon(IntPtr handle);
}
