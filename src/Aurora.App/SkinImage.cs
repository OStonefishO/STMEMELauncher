using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Aurora.App;

/// <summary>把皮肤 PNG 裁成头像 / 全身预览。</summary>
internal static class SkinImage
{
    public static BitmapSource? Head(byte[] skin, int size = 64)
    {
        var frame = Decode(skin);
        if (frame == null)
        {
            return null;
        }

        var scale = size / 8.0;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            // 脸
            Draw(dc, frame, 8, 8, 8, 8, 0, 0, 8 * scale, 8 * scale);
            // 帽子层（如果有）
            if (frame.PixelHeight >= 32)
            {
                Draw(dc, frame, 40, 8, 8, 8, 0, 0, 8 * scale, 8 * scale);
            }
        }

        return Render(visual, size, size);
    }

    /// <summary>整张皮肤（给玩家看原图）。</summary>
    public static BitmapSource? Full(byte[] skin, int width = 256)
    {
        var frame = Decode(skin);
        if (frame == null)
        {
            return null;
        }

        var scale = width / (double)frame.PixelWidth;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.NearestNeighbor);
            dc.DrawImage(frame, new Rect(0, 0, frame.PixelWidth * scale, frame.PixelHeight * scale));
        }

        return Render(visual, (int)(frame.PixelWidth * scale), (int)(frame.PixelHeight * scale));
    }

    private static BitmapFrame? Decode(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes);
            var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);
            return decoder.Frames.Count > 0 ? decoder.Frames[0] : null;
        }
        catch
        {
            return null;
        }
    }

    private static void Draw(DrawingContext dc, BitmapSource source, int sx, int sy, int sw, int sh,
        double dx, double dy, double dw, double dh)
    {
        if (sx + sw > source.PixelWidth || sy + sh > source.PixelHeight)
        {
            return;
        }

        var crop = new CroppedBitmap(source, new Int32Rect(sx, sy, sw, sh));
        dc.DrawImage(crop, new Rect(dx, dy, dw, dh));
    }

    private static BitmapSource Render(DrawingVisual visual, int width, int height)
    {
        var bitmap = new RenderTargetBitmap(Math.Max(1, width), Math.Max(1, height), 96, 96,
            PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }
}
