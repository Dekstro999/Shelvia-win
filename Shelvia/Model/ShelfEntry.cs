using System.Drawing;
using System.Threading.Tasks;
using System.Diagnostics;
using System;
using System.IO;
using Shelvia.Win32;
using Shelvia.Util;

namespace Shelvia.Model
{
    public class ShelfEntry
    {
        public string Path { get; }

        public EntryType Type { get; }

        public string Name => System.IO.Path.GetFileNameWithoutExtension(Path);

        private ShelfEntry(string path, EntryType type)
        {
            Path = path;
            Type = type;
        }

        public static ShelfEntry FromPath(string path)
        {
            if (File.Exists(path))
                return new ShelfEntry(path, EntryType.File);
            else if (Directory.Exists(path))
                return new ShelfEntry(path, EntryType.Folder);
            else return null;
        }

        public Icon ExtractIcon(ThumbnailProvider thumbnailProvider)
        {
            if (Type == EntryType.File)
            {
                return thumbnailProvider.GenerateThumbnail(Path);
            }
            else
            {
                return IconUtil.FolderLarge;
            }
        }

        public void Open()
        {
            Task.Run(() =>
            {
                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = Path,
                        UseShellExecute = true
                    };

                    Process.Start(startInfo);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Failed to open item '{Path}': {e}");
                }
            });
        }
    }
}
