using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

internal static class GlassVariantPostProcess
{
    private static bool IsBackdrop(byte a, byte r, byte g, byte b, int threshold)
    {
        if (a == 0) return false;
        if (r <= threshold && g <= threshold && b <= threshold) return true;
        int max = Math.Max(r, Math.Max(g, b));
        int min = Math.Min(r, Math.Min(g, b));
        int spread = max - min;
        int avg = (r + g + b) / 3;
        if (spread <= 18 && avg >= 95 && avg <= 175) return true;
        return spread <= 10 && avg >= 175;
    }

    private static void Enqueue(Queue<int> queue, int width, int height, int x, int y)
    {
        if (x < 0 || y < 0 || x >= width || y >= height) return;
        queue.Enqueue(y * width + x);
    }

    private static int RemoveEdgeBackdrop(Bitmap bitmap, int threshold)
    {
        int width = bitmap.Width;
        int height = bitmap.Height;
        var data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        int stride = data.Stride;
        int bytes = Math.Abs(stride) * height;
        byte[] pixels = new byte[bytes];
        System.Runtime.InteropServices.Marshal.Copy(data.Scan0, pixels, 0, bytes);

        bool[] visited = new bool[width * height];
        var queue = new Queue<int>();
        for (int x = 0; x < width; x++) { Enqueue(queue, width, height, x, 0); Enqueue(queue, width, height, x, height - 1); }
        for (int y = 0; y < height; y++) { Enqueue(queue, width, height, 0, y); Enqueue(queue, width, height, width - 1, y); }

        int removed = 0;
        while (queue.Count > 0)
        {
            int index = queue.Dequeue();
            if (visited[index]) continue;
            visited[index] = true;

            int x = index % width;
            int y = index / width;
            int offset = y * stride + x * 4;
            byte b = pixels[offset];
            byte g = pixels[offset + 1];
            byte r = pixels[offset + 2];
            byte a = pixels[offset + 3];

            if (a == 0)
            {
                Enqueue(queue, width, height, x - 1, y);
                Enqueue(queue, width, height, x + 1, y);
                Enqueue(queue, width, height, x, y - 1);
                Enqueue(queue, width, height, x, y + 1);
                continue;
            }

            if (!IsBackdrop(a, r, g, b, threshold)) continue;

            pixels[offset + 3] = 0;
            removed++;
            Enqueue(queue, width, height, x - 1, y);
            Enqueue(queue, width, height, x + 1, y);
            Enqueue(queue, width, height, x, y - 1);
            Enqueue(queue, width, height, x, y + 1);
        }

        System.Runtime.InteropServices.Marshal.Copy(pixels, 0, data.Scan0, bytes);
        bitmap.UnlockBits(data);
        return removed;
    }

    private static void GetAlphaBounds(Bitmap bitmap, out int minX, out int minY, out int maxX, out int maxY)
    {
        int width = bitmap.Width;
        int height = bitmap.Height;
        minX = width; minY = height; maxX = -1; maxY = -1;
        var data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        int stride = data.Stride;
        int bytes = Math.Abs(stride) * height;
        byte[] pixels = new byte[bytes];
        System.Runtime.InteropServices.Marshal.Copy(data.Scan0, pixels, 0, bytes);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (pixels[y * stride + x * 4 + 3] > 0)
                {
                    if (x < minX) minX = x;
                    if (y < minY) minY = y;
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;
                }
            }
        }
        bitmap.UnlockBits(data);
        if (maxX < 0) throw new InvalidOperationException("No opaque pixels found.");
    }

    private static Bitmap Process(string inputPath, int threshold, int padding, out int removed, out string cornerAlpha)
    {
        using (var source = new Bitmap(inputPath))
        {
            var working = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(working)) g.DrawImage(source, 0, 0);

            removed = RemoveEdgeBackdrop(working, threshold);
            int minX, minY, maxX, maxY;
            GetAlphaBounds(working, out minX, out minY, out maxX, out maxY);

            int cropX = Math.Max(0, minX - padding);
            int cropY = Math.Max(0, minY - padding);
            int cropW = Math.Min(working.Width - cropX, (maxX - minX + 1) + (padding * 2));
            int cropH = Math.Min(working.Height - cropY, (maxY - minY + 1) + (padding * 2));

            var cropped = new Bitmap(cropW, cropH, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(cropped))
            {
                g.Clear(Color.Transparent);
                g.DrawImage(working, new Rectangle(0, 0, cropW, cropH), new Rectangle(cropX, cropY, cropW, cropH), GraphicsUnit.Pixel);
            }
            working.Dispose();

            cornerAlpha = string.Format("{0},{1},{2},{3}",
                cropped.GetPixel(0, 0).A,
                cropped.GetPixel(cropW - 1, 0).A,
                cropped.GetPixel(0, cropH - 1).A,
                cropped.GetPixel(cropW - 1, cropH - 1).A);
            return cropped;
        }
    }

    private static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: GlassVariantPostProcess.exe <input.png> <output.png> [threshold] [padding]");
            return 1;
        }

        string input = Path.GetFullPath(args[0]);
        string output = Path.GetFullPath(args[1]);
        int threshold = args.Length > 2 ? int.Parse(args[2]) : 12;
        int padding = args.Length > 3 ? int.Parse(args[3]) : 12;

        if (!File.Exists(input)) throw new FileNotFoundException(input);

        int removed;
        string corners;
        var result = Process(input, threshold, padding, out removed, out corners);
        string temp = output + ".tmp";
        result.Save(temp, ImageFormat.Png);
        result.Dispose();
        if (File.Exists(output)) File.Delete(output);
        File.Move(temp, output);

        Console.WriteLine("Input=" + input);
        Console.WriteLine("Output=" + output);
        Console.WriteLine("RemovedBackdropPixels=" + removed);
        Console.WriteLine("Width=" + new Bitmap(output).Width);
        Console.WriteLine("Height=" + new Bitmap(output).Height);
        Console.WriteLine("CornerAlpha=" + corners);
        return 0;
    }
}
