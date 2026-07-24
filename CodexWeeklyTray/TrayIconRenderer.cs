using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace CodexWeeklyTray;

internal static class TrayIconRenderer
{
    public static Icon CreatePercentIcon(double remainingPercent)
    {
        int rounded = (int)Math.Round(Math.Clamp(remainingPercent, 0d, 100d));
        string text = rounded >= 100 ? "99+" : rounded.ToString();

        using var bitmap = new Bitmap(32, 32);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        graphics.Clear(Color.Transparent);

        using var background = new SolidBrush(SystemColors.Highlight);
        graphics.FillEllipse(background, 1, 1, 30, 30);

        float fontSize = text.Length >= 3 ? 10f : 13f;
        using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
        using var foreground = new SolidBrush(SystemColors.HighlightText);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        graphics.DrawString(text, font, foreground, new RectangleF(0, 0, 32, 31), format);

        IntPtr hIcon = bitmap.GetHicon();
        try
        {
            using Icon temporary = Icon.FromHandle(hIcon);
            return (Icon)temporary.Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    public static Icon CreateErrorIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        using var background = new SolidBrush(SystemColors.ControlDarkDark);
        graphics.FillEllipse(background, 1, 1, 30, 30);
        using var font = new Font("Segoe UI", 18f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var foreground = new SolidBrush(Color.White);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        graphics.DrawString("?", font, foreground, new RectangleF(0, 0, 32, 31), format);

        IntPtr hIcon = bitmap.GetHicon();
        try
        {
            using Icon temporary = Icon.FromHandle(hIcon);
            return (Icon)temporary.Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
