using System;
using System.IO;
using System.Xml.Serialization;

namespace Shelvia.Model
{
    public class ShelfManager
    {
        public static ShelfManager Instance { get; } = new ShelfManager();

        private const string MetaFileName = "__shelf_metadata.xml";

        private readonly string basePath;

        public event EventHandler ShelvesChanged;

        public ShelfManager()
        {
            basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Shelvia");
            EnsureDirectoryExists(basePath);
        }

        public void LoadShelves()
        {
            var serializer = new XmlSerializer(typeof(ShelfInfo));

            foreach (var dir in Directory.EnumerateDirectories(basePath))
            {
                var metaFile = Path.Combine(dir, MetaFileName);
                if (!File.Exists(metaFile))
                    continue;

                try
                {
                    using (var reader = new StreamReader(metaFile))
                    {
                        var shelf = serializer.Deserialize(reader) as ShelfInfo;
                        if (shelf != null)
                        {
                            new ShelfWindow(shelf).Show();
                        }
                    }
                }
                catch
                {
                    // Ignore invalid shelf metadata and continue loading the rest
                }
            }
        }

        public void CreateShelf(string name, string targetFolder = null)
        {
            var shelfInfo = new ShelfInfo(Guid.NewGuid())
            {
                Name = name,
                TargetFolder = targetFolder,
                PosX = 100,
                PosY = 250,
                Height = 300,
                Width = 300
            };

            UpdateShelf(shelfInfo);
            new ShelfWindow(shelfInfo).Show();
            OnShelvesChanged();
        }

        public void RemoveShelf(ShelfInfo info)
        {
            if (info == null) return;
            try
            {
                var path = GetFolderPath(info);
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch
            {
                // Ignore exceptions during removal
            }

            OnShelvesChanged();
        }

        public void UpdateShelf(ShelfInfo shelfInfo)
        {
            var path = GetFolderPath(shelfInfo);
            EnsureDirectoryExists(path);

            var metaFile = Path.Combine(path, MetaFileName);
            var serializer = new XmlSerializer(typeof(ShelfInfo));
            var writer = new StreamWriter(metaFile);
            serializer.Serialize(writer, shelfInfo);
            writer.Close();
        }

        private void EnsureDirectoryExists(string dir)
        {
            var di = new DirectoryInfo(dir);
            if (!di.Exists)
                di.Create();
        }

        private void OnShelvesChanged()
        {
            ShelvesChanged?.Invoke(this, EventArgs.Empty);
        }

        private string GetFolderPath(ShelfInfo shelfInfo)
        {
            return Path.Combine(basePath, shelfInfo.Id.ToString());
        }
    }
}
