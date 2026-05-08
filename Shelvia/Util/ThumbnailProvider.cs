using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Shelvia.Util
{
    public class ThumbnailProvider
    {
        // Supported .NET images as per https://docs.microsoft.com/en-us/dotnet/api/system.drawing.image.fromfile
        private static readonly string[] SupportedExtensions =
        {
            ".bmp",
            ".gif",
            ".jpg",
            ".jpeg",
            ".png",
            ".tiff",
            ".tif"
        };

        private class ThumbnailState
        {
            public Icon icon;
            public bool isThumbnailLoaded;
        }

        // Only allow 4 concurrent images to be decoded to try and prevent OOM errors
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(4);
        private readonly IDictionary<string, ThumbnailState> iconCache = new Dictionary<string, ThumbnailState>(StringComparer.OrdinalIgnoreCase);
        private readonly object cacheLock = new object();
        public event EventHandler IconThumbnailLoaded;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        public bool IsSupported(string path)
        {
            return SupportedExtensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
        }

        public Icon GenerateThumbnail(string path)
        {
            var isImage = IsSupported(path);
            var cacheKey = GetCacheKey(path, isImage);

            ThumbnailState state;
            lock (cacheLock)
            {
                if (!iconCache.TryGetValue(cacheKey, out state))
                {
                    state = SubmitGeneratorTask(path, cacheKey, isImage);
                }
            }

            return state.icon;
        }

        private string GetCacheKey(string path, bool isImage)
        {
            if (isImage)
                return "img:" + path;

            var ext = Path.GetExtension(path) ?? string.Empty;
            if (ext.Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".ico", StringComparison.OrdinalIgnoreCase))
            {
                return "path:" + path;
            }

            return "ext:" + ext;
        }

        private ThumbnailState SubmitGeneratorTask(string path, string cacheKey, bool isImage)
        {
            var state = new ThumbnailState();

            try
            {
                state.icon = Icon.ExtractAssociatedIcon(path);
            }
            catch
            {
                state.icon = SystemIcons.WinLogo;
            }

            iconCache[cacheKey] = state;

            if (!isImage)
                return state;

            Task.Run(() =>
            {
                semaphore.Wait();
                try
                {
                    if (!File.Exists(path))
                        return;

                    using (MemoryStream ms = new MemoryStream(File.ReadAllBytes(path)))
                    {
                        using (var img = Image.FromStream(ms))
                        {
                            using (var thumb = new Bitmap(32, 32))
                            {
                                using (var g = Graphics.FromImage(thumb))
                                {
                                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                                    g.DrawImage(img, new Rectangle(0, 0, 32, 32));
                                }

                                var hIcon = thumb.GetHicon();
                                var icon = (Icon)Icon.FromHandle(hIcon).Clone();
                                DestroyIcon(hIcon);
                                state.icon = icon;
                                state.isThumbnailLoaded = true;
                            }
                        }
                    }

                    IconThumbnailLoaded?.Invoke(this, EventArgs.Empty);
                }
                catch
                {
                    // Ignore thumbnail generation errors and keep associated icon
                }
                finally
                {
                    semaphore.Release();
                }
            });

            return state;
        }

    }
}
