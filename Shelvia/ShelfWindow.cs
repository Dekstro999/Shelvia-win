using Shelvia.Model;
using Shelvia.Util;
using Shelvia.Win32;
using Peter;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using static Shelvia.Win32.WindowUtil;

namespace Shelvia
{
    public partial class ShelfWindow : Form
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;

        private int logicalTitleHeight;
        private int titleHeight;
        private const int titleOffset = 3;
        private const int itemWidth = 72;
        private const int iconSize = 40;
        private const int textHeight = 24;
        private const int itemPadding = 8;
        private const int itemHeight = iconSize + itemPadding + textHeight;
        private const float shadowDist = 1.5f;

        private readonly ShelfInfo shelfInfo;
        private Color shelfColor = Color.Black;

        private Font titleFont;
        private Font iconFont;

        private string selectedItem;
        private string hoveringItem;
        private bool shouldUpdateSelection;
        private bool hasSelectionUpdated;
        private bool hasHoverUpdated;
        private bool isMinified;
        private bool isMinifyPreviewExpanded;
        private int prevHeight;

        private int scrollHeight;
        private int scrollOffset;
        private float scrollOffsetAnimated;
        private float scrollTargetOffset;
        private readonly Timer scrollAnimationTimer = new Timer { Interval = 8 };
        private readonly Timer minifyAnimationTimer = new Timer { Interval = 15 };
        private readonly Timer minifyPreviewCollapseTimer = new Timer { Interval = 700 };
        private DateTime minifyAnimationStartTime;
        private int minifyAnimationStartHeight;
        private int minifyAnimationTargetHeight;
        private const double minifyAnimationDurationMs = 180.0;
        private readonly List<ShelfEntry> cachedEntries = new List<ShelfEntry>();
        private readonly List<Point> cachedItemPositions = new List<Point>();
        private bool entriesCacheDirty = true;
        private int cachedLayoutWidth = -1;
        private int cachedLayoutEntryCount = -1;

        private readonly ThrottledExecution throttledMove = new ThrottledExecution(TimeSpan.FromSeconds(4));
        private readonly ThrottledExecution throttledResize = new ThrottledExecution(TimeSpan.FromSeconds(4));

        private readonly ShellContextMenu shellContextMenu = new ShellContextMenu();
        private FileSystemWatcher fileWatcher;

        private readonly ThumbnailProvider thumbnailProvider = new ThumbnailProvider();
        private readonly ToolStripMenuItem addFolderToolStripMenuItem = new ToolStripMenuItem("Add folder...");
        private readonly ToolStripMenuItem showWindowsMenuToolStripMenuItem = new ToolStripMenuItem("Show Windows menu");
        private readonly ToolStripMenuItem renameItemToolStripMenuItem = new ToolStripMenuItem("Rename item");
        private readonly TextBox inlineRenameTextBox = new TextBox();
        private string inlineRenamePath;
        private RectangleF inlineRenameBaseRect;
        private string inlineRenameOriginalName;
        private bool inlineRenameIsFile;
        private string inlineRenameExtension;
        private bool inlineRenameRenderedThisFrame;
        private bool suppressInlineRenameCommitOnHide;
        private readonly Dictionary<string, RectangleF> inlineRenameTextRectsByPath = new Dictionary<string, RectangleF>(StringComparer.OrdinalIgnoreCase);
        private static readonly StringFormat EntryTextFormat = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };

        private int GetColumnCount()
        {
            return Math.Max(1, (Width - itemPadding) / (itemWidth + itemPadding));
        }

        private void MarkEntriesDirty()
        {
            entriesCacheDirty = true;
            cachedLayoutEntryCount = -1;
        }

        private void EnsureEntriesCache()
        {
            if (!entriesCacheDirty)
                return;

            cachedEntries.Clear();

            foreach (var file in shelfInfo.Files)
            {
                var entry = ShelfEntry.FromPath(file);
                if (entry != null)
                    cachedEntries.Add(entry);
            }

            entriesCacheDirty = false;
        }

        private void EnsureLayoutCache()
        {
            EnsureEntriesCache();

            if (cachedLayoutWidth == Width && cachedLayoutEntryCount == cachedEntries.Count)
                return;

            cachedItemPositions.Clear();
            var columns = GetColumnCount();

            for (var i = 0; i < cachedEntries.Count; i++)
            {
                var row = i / columns;
                var col = i % columns;

                var x = itemPadding + (col * (itemWidth + itemPadding));
                var y = itemPadding + (row * (itemHeight + itemPadding));
                cachedItemPositions.Add(new Point(x, y));
            }

            cachedLayoutWidth = Width;
            cachedLayoutEntryCount = cachedEntries.Count;
        }

        private void ReloadFonts()
        {
            var family = new FontFamily("Segoe UI");
            titleFont = new Font(family, (int)Math.Floor(logicalTitleHeight / 2.0));
            iconFont = new Font(family, 9);
        }

        public Guid ShelfId => shelfInfo.Id;

        public void SetShelfColor(Color color)
        {
            shelfColor = color;
            shelfInfo.ShelfColorArgb = color.ToArgb();
            BackColor = color;
            Refresh();
            Save();
        }

        private Point? dragStartPoint;
        private string dragStartItem;
        private string dragHoverTargetFolder;

        public ShelfWindow(ShelfInfo shelfInfo)
        {
            InitializeComponent();
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            UpdateStyles();

            appContextMenu.Renderer = new DarkMenuRenderer();
            appContextMenu.ForeColor = Color.White;

            inlineRenameTextBox.Visible = false;
            inlineRenameTextBox.BorderStyle = BorderStyle.None;
            inlineRenameTextBox.Multiline = false;
            inlineRenameTextBox.WordWrap = false;
            inlineRenameTextBox.AcceptsReturn = false;
            inlineRenameTextBox.BackColor = Color.FromArgb(28, 28, 28);
            inlineRenameTextBox.ForeColor = Color.White;
            inlineRenameTextBox.KeyDown += InlineRenameTextBox_KeyDown;
            inlineRenameTextBox.LostFocus += InlineRenameTextBox_LostFocus;
            inlineRenameTextBox.TextChanged += InlineRenameTextBox_TextChanged;
            Controls.Add(inlineRenameTextBox);

            KeyPreview = true;
            KeyDown += ShelfWindow_KeyDown;

            addFolderToolStripMenuItem.Click += addFolderToolStripMenuItem_Click;
            showWindowsMenuToolStripMenuItem.Click += showWindowsMenuToolStripMenuItem_Click;
            renameItemToolStripMenuItem.Click += renameItemToolStripMenuItem_Click;

            var deleteItemIndex = appContextMenu.Items.IndexOf(deleteItemToolStripMenuItem);
            if (deleteItemIndex >= 0)
            {
                appContextMenu.Items.Insert(deleteItemIndex + 1, renameItemToolStripMenuItem);
            }

            var separatorIndex = appContextMenu.Items.IndexOf(toolStripSeparator1);
            if (separatorIndex >= 0)
            {
                appContextMenu.Items.Insert(separatorIndex, addFolderToolStripMenuItem);
                appContextMenu.Items.Insert(separatorIndex + 1, showWindowsMenuToolStripMenuItem);
            }

            openFolderToolStripMenuItem.Enabled = !string.IsNullOrEmpty(shelfInfo.TargetFolder) && Directory.Exists(shelfInfo.TargetFolder);
            openFolderToolStripMenuItem.Visible = openFolderToolStripMenuItem.Enabled;

            var opacityMenuItem = new ToolStripMenuItem("Opacity");
            foreach (var val in new[] { 20, 40, 60, 80, 100 })
            {
                var item = new ToolStripMenuItem($"{val}%");
                item.Click += (s, e) => {
                    shelfInfo.Opacity = val / 100.0;
                    this.Opacity = shelfInfo.Opacity;
                    Save();
                };
                opacityMenuItem.DropDownItems.Add(item);
            }
            appContextMenu.Items.Insert(appContextMenu.Items.IndexOf(titleSizeToolStripMenuItem) + 1, opacityMenuItem);

            DropShadow.ApplyShadows(this);
            BlurUtil.EnableBlur(Handle);
            WindowUtil.HideFromAltTab(Handle);
            DesktopUtil.GlueToDesktop(Handle);
            //DesktopUtil.PreventMinimize(Handle);
            logicalTitleHeight = (shelfInfo.TitleHeight < 16 || shelfInfo.TitleHeight > 100) ? 35 : shelfInfo.TitleHeight;
            titleHeight = LogicalToDeviceUnits(logicalTitleHeight);
            
            this.MouseWheel += ShelfWindow_MouseWheel;
            this.MouseDown += ShelfWindow_MouseDown;
            this.MouseUp += ShelfWindow_MouseUp;
            this.DragOver += ShelfWindow_DragOver;
            this.DragLeave += ShelfWindow_DragLeave;
            thumbnailProvider.IconThumbnailLoaded += ThumbnailProvider_IconThumbnailLoaded;
            scrollAnimationTimer.Tick += ScrollAnimationTimer_Tick;
            minifyAnimationTimer.Tick += MinifyAnimationTimer_Tick;
            minifyPreviewCollapseTimer.Tick += MinifyPreviewCollapseTimer_Tick;

            ReloadFonts();

            AllowDrop = true;


            this.shelfInfo = shelfInfo;
            shelfColor = shelfInfo.ShelfColorArgb != 0 ? Color.FromArgb(shelfInfo.ShelfColorArgb) : Color.Black;
            this.BackColor = shelfColor;
            this.Opacity = shelfInfo.Opacity > 0 ? shelfInfo.Opacity : 0.8;
            if (shelfInfo.Opacity == 0) shelfInfo.Opacity = 0.8;

            Text = shelfInfo.Name;
            Location = new Point(shelfInfo.PosX, shelfInfo.PosY);

            Width = shelfInfo.Width;
            Height = shelfInfo.Height;

            prevHeight = Height;
            lockedToolStripMenuItem.Checked = shelfInfo.Locked;
            minifyToolStripMenuItem.Visible = false; // We use the manual arrow now

            ConfigureFileWatcher(shelfInfo.TargetFolder);

            isMinified = shelfInfo.CanMinify;
            if (isMinified)
            {
                Height = titleHeight;
            }

            scrollOffsetAnimated = scrollOffset;
            scrollTargetOffset = scrollOffset;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            WindowUtil.HideFromAltTab(Handle);
            DesktopUtil.GlueToDesktop(Handle);
        }

        private void ConfigureFileWatcher(string targetFolder)
        {
            if (fileWatcher != null)
            {
                fileWatcher.EnableRaisingEvents = false;
                fileWatcher.Created -= FileWatcher_Event;
                fileWatcher.Deleted -= FileWatcher_Event;
                fileWatcher.Renamed -= FileWatcher_Event;
                fileWatcher.Dispose();
                fileWatcher = null;
            }

            if (!string.IsNullOrEmpty(targetFolder) && Directory.Exists(targetFolder))
            {
                fileWatcher = new FileSystemWatcher(targetFolder);
                fileWatcher.NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName | NotifyFilters.LastWrite;
                fileWatcher.Created += FileWatcher_Event;
                fileWatcher.Deleted += FileWatcher_Event;
                fileWatcher.Renamed += FileWatcher_Event;
                fileWatcher.EnableRaisingEvents = true;
                ReloadFolderFiles();
            }
        }

        private void FileWatcher_Event(object sender, FileSystemEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => { ReloadFolderFiles(); Refresh(); }));
            }
            else
            {
                ReloadFolderFiles();
                Invalidate();
            }
        }

        private void ReloadFolderFiles()
        {
            if (string.IsNullOrEmpty(shelfInfo.TargetFolder) || !Directory.Exists(shelfInfo.TargetFolder))
                return;
            
            var files = new System.Collections.Generic.List<string>();
            try
            {
                foreach (var d in Directory.GetDirectories(shelfInfo.TargetFolder))
                    files.Add(d);
                foreach (var f in Directory.GetFiles(shelfInfo.TargetFolder))
                    files.Add(f);
                
                shelfInfo.Files = files;
                MarkEntriesDirty();
            }
            catch (Exception) { }
        }

        protected override void WndProc(ref Message m)
        {
            //Console.WriteLine(m.Msg.ToString("X4"));

            // Right click on non-client area (e.g. title hit-test region): show app menu
            if (m.Msg == 0x00A5) // WM_NCRBUTTONUP
            {
                var clientPos = PointToClient(MousePosition);
                appContextMenu.Show(this, clientPos);
                return;
            }

            // Remove border
            if (m.Msg == 0x0083)
            {
                m.Result = IntPtr.Zero;
                return;
            }

            // Mouse leave
            var myrect = new Rectangle(Location, Size);
            if (m.Msg == 0x02a2 && !myrect.IntersectsWith(new Rectangle(MousePosition, new Size(1, 1))))
            {
                // Minify();
            }

            // Prevent maximize
            if ((m.Msg == WM_SYSCOMMAND) && m.WParam.ToInt32() == 0xF032)
            {
                m.Result = IntPtr.Zero;
                return;
            }

            // Prevent foreground
            if (m.Msg == WM_SETFOCUS)
            {
                SetWindowPos(Handle, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
                return;
            }

            // Other messages
            base.WndProc(ref m);

            // If not locked and using the left mouse button
            if (MouseButtons == MouseButtons.Right || lockedToolStripMenuItem.Checked)
                return;

            // Then, allow dragging and resizing
            if (m.Msg == WM_NCHITTEST)
            {
                var pt = PointToClient(new Point(m.LParam.ToInt32()));

                var arrowRect = new Rectangle(Width - 30, 0, 30, titleHeight);
                if (arrowRect.Contains(pt))
                {
                    m.Result = (IntPtr)HTCLIENT;
                    return;
                }

                // Keep title area as client so right-click context menu works reliably.

                if (pt.X < 10 && pt.Y < 10)
                    m.Result = new IntPtr(HTTOPLEFT);
                else if (pt.X > (Width - 10) && pt.Y < 10)
                    m.Result = new IntPtr(HTTOPRIGHT);
                else if (pt.X < 10 && pt.Y > (Height - 10))
                    m.Result = new IntPtr(HTBOTTOMLEFT);
                else if (pt.X > (Width - 10) && pt.Y > (Height - 10))
                    m.Result = new IntPtr(HTBOTTOMRIGHT);
                else if (pt.Y > (Height - 10))
                    m.Result = new IntPtr(HTBOTTOM);
                else if (pt.X < 10)
                    m.Result = new IntPtr(HTLEFT);
                else if (pt.X > (Width - 10))
                    m.Result = new IntPtr(HTRIGHT);
            }
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show(this, "Really remove this shelf?", "Remove", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                ShelfManager.Instance.RemoveShelf(shelfInfo);
                Close();
            }
        }

        private void deleteItemToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(shelfInfo.TargetFolder) && Directory.Exists(shelfInfo.TargetFolder))
            {
                try
                {
                    if (File.Exists(hoveringItem))
                        File.Delete(hoveringItem);
                    else if (Directory.Exists(hoveringItem))
                        Directory.Delete(hoveringItem, true);
                }
                catch { } // ignore
            }
            else
            {
                shelfInfo.Files.Remove(hoveringItem);
                MarkEntriesDirty();
            }
            hoveringItem = null;
            Save();
            Refresh();
        }

        private void contextMenuStrip1_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            deleteItemToolStripMenuItem.Visible = hoveringItem != null;
            renameItemToolStripMenuItem.Visible = hoveringItem != null;

            var hasTargetFolder = !string.IsNullOrEmpty(shelfInfo.TargetFolder) && Directory.Exists(shelfInfo.TargetFolder);
            openFolderToolStripMenuItem.Visible = hasTargetFolder;
            openFolderToolStripMenuItem.Enabled = hasTargetFolder;
            addFolderToolStripMenuItem.Enabled = hasTargetFolder;
            showWindowsMenuToolStripMenuItem.Enabled = hasTargetFolder;
        }

        private void ShelfWindow_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) && !lockedToolStripMenuItem.Checked)
            {
                if ((e.KeyState & 8) == 8) // Ctrl key handles copy
                    e.Effect = DragDropEffects.Copy;
                else
                    e.Effect = DragDropEffects.Move;
            }
        }

        private void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(sourceDir))
                File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), true);
            foreach (var dir in Directory.GetDirectories(sourceDir))
                CopyDirectory(dir, Path.Combine(destDir, Path.GetFileName(dir)));
        }

        private void ShelfWindow_DragDrop(object sender, DragEventArgs e)
        {
            var dropped = (string[])e.Data.GetData(DataFormats.FileDrop);

            if (!string.IsNullOrEmpty(shelfInfo.TargetFolder) && Directory.Exists(shelfInfo.TargetFolder))
            {
                var dropPointClient = PointToClient(new Point(e.X, e.Y));
                var dropTargetFolder = GetDropTargetFolderAtClientPoint(dropPointClient);
                var destinationRoot = string.IsNullOrEmpty(dropTargetFolder) ? shelfInfo.TargetFolder : dropTargetFolder;

                foreach (var file in dropped)
                {
                    try
                    {
                        var dest = Path.Combine(destinationRoot, Path.GetFileName(file));
                        bool isDir = Directory.Exists(file);

                        if (file.Equals(dest, StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (e.Effect == DragDropEffects.Move)
                        {
                            if (isDir) Directory.Move(file, dest);
                            else File.Move(file, dest);
                        }
                        else if (e.Effect == DragDropEffects.Copy)
                        {
                            if (!isDir) File.Copy(file, dest, true);
                            else CopyDirectory(file, dest);
                        }
                    }
                    catch { }
                }
            }
            else
            {
                var addedAny = false;
                foreach (var file in dropped)
                    if (!shelfInfo.Files.Contains(file) && ItemExists(file))
                    {
                        shelfInfo.Files.Add(file);
                        addedAny = true;
                    }

                if (addedAny)
                    MarkEntriesDirty();
            }

            dragHoverTargetFolder = null;
            Save();
            Refresh();
        }

        private void ShelfWindow_MouseMove(object sender, MouseEventArgs e)
        {
            var shouldInvalidate = false;

            if (e.Button == MouseButtons.Left && dragStartPoint.HasValue && !string.IsNullOrEmpty(dragStartItem) && ItemExists(dragStartItem))
            {
                var dragRect = new Rectangle(
                    dragStartPoint.Value.X - SystemInformation.DragSize.Width / 2,
                    dragStartPoint.Value.Y - SystemInformation.DragSize.Height / 2,
                    SystemInformation.DragSize.Width,
                    SystemInformation.DragSize.Height);

                if (!dragRect.Contains(e.Location))
                {
                    var data = new DataObject(DataFormats.FileDrop, new[] { dragStartItem });
                    DoDragDrop(data, DragDropEffects.Copy | DragDropEffects.Move);
                    dragStartPoint = null;
                    dragStartItem = null;
                    shouldInvalidate = true;
                }
            }

            if (!shouldInvalidate)
            {
                var hoveredPath = GetItemPathAtClientPoint(e.Location);
                if (!string.Equals(hoveringItem, hoveredPath, StringComparison.OrdinalIgnoreCase))
                    shouldInvalidate = true;
            }

            if (shouldInvalidate)
                Invalidate();
        }

        private void ShelfWindow_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            var arrowRect = new Rectangle(Width - 30, 0, 30, titleHeight);
            if (!lockedToolStripMenuItem.Checked && e.Y < titleHeight && !arrowRect.Contains(e.Location))
            {
                if (e.Clicks >= 2)
                {
                    openFolderToolStripMenuItem_Click(sender, EventArgs.Empty);
                    return;
                }

                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
                return;
            }

            if (inlineRenameTextBox.Visible && !inlineRenameTextBox.Bounds.Contains(e.Location))
            {
                CommitInlineRename();
            }

            dragStartPoint = e.Location;
            dragStartItem = GetItemPathAtClientPoint(e.Location);
        }

        private void ShelfWindow_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                dragStartPoint = null;
                dragStartItem = null;
            }
        }

        private void ShelfWindow_DragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop) || lockedToolStripMenuItem.Checked)
            {
                if (!string.IsNullOrEmpty(dragHoverTargetFolder))
                {
                    dragHoverTargetFolder = null;
                    Invalidate();
                }
                return;
            }

            if ((e.KeyState & 8) == 8)
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.Move;

            var clientPoint = PointToClient(new Point(e.X, e.Y));
            var targetFolder = GetDropTargetFolderAtClientPoint(clientPoint);

            if (!string.Equals(dragHoverTargetFolder, targetFolder, StringComparison.OrdinalIgnoreCase))
            {
                dragHoverTargetFolder = targetFolder;
                Invalidate();
            }
        }

        private void ShelfWindow_DragLeave(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(dragHoverTargetFolder))
            {
                dragHoverTargetFolder = null;
                Invalidate();
            }
        }

        private string GetItemPathAtClientPoint(Point clientPoint)
        {
            EnsureLayoutCache();

            for (var i = 0; i < cachedEntries.Count; i++)
            {
                var point = cachedItemPositions[i];
                var x = point.X;
                var y = point.Y;

                var itemRect = new Rectangle(x - 2, y + titleHeight - scrollOffset - 2, itemWidth + 4, itemHeight + 4);
                if (itemRect.Contains(clientPoint))
                    return cachedEntries[i].Path;
            }

            return null;
        }

        private string GetDropTargetFolderAtClientPoint(Point clientPoint)
        {
            var path = GetItemPathAtClientPoint(clientPoint);
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                return path;

            return null;
        }

        private void ShelfWindow_Resize(object sender, EventArgs e)
        {
            throttledResize.Run(() =>
            {
                shelfInfo.Width = Width;
                if (!isMinified && !isMinifyPreviewExpanded)
                {
                    prevHeight = Height;
                    shelfInfo.Height = Height;
                }
                else
                {
                    shelfInfo.Height = prevHeight;
                }
                Save();
            });

            Refresh();
        }

        private void ShelfWindow_MouseEnter(object sender, EventArgs e)
        {
            if (minifyPreviewCollapseTimer.Enabled)
                minifyPreviewCollapseTimer.Stop();

            if (isMinified && !isMinifyPreviewExpanded)
            {
                var previewHeight = prevHeight > titleHeight ? prevHeight : Math.Max(titleHeight + itemHeight, Height);
                StartMinifyAnimation(previewHeight);
                isMinifyPreviewExpanded = true;
            }
        }

        private void ShelfWindow_MouseLeave(object sender, EventArgs e)
        {
            if (isMinified && isMinifyPreviewExpanded)
            {
                if (MouseButtons != MouseButtons.Left)
                {
                    minifyPreviewCollapseTimer.Stop();
                    minifyPreviewCollapseTimer.Start();
                }
            }

            selectedItem = null;
            Refresh();
        }

        private void ToggleMinify()
        {
            isMinified = !isMinified;
            if (isMinified)
            {
                prevHeight = Height;
                StartMinifyAnimation(titleHeight);
                isMinifyPreviewExpanded = false;
            }
            else
            {
                var restoreHeight = prevHeight > titleHeight ? prevHeight : Math.Max(titleHeight + itemHeight, Height);
                StartMinifyAnimation(restoreHeight);
                isMinifyPreviewExpanded = false;
            }

            minifyToolStripMenuItem.Checked = isMinified;
            shelfInfo.CanMinify = isMinified;
            Save();
            Invalidate();
        }

        private void StartMinifyAnimation(int targetHeight)
        {
            if (Height == targetHeight)
            {
                if (minifyAnimationTimer.Enabled)
                    minifyAnimationTimer.Stop();

                Invalidate();
                return;
            }

            minifyAnimationStartHeight = Height;
            minifyAnimationTargetHeight = targetHeight;
            minifyAnimationStartTime = DateTime.UtcNow;

            if (!minifyAnimationTimer.Enabled)
                minifyAnimationTimer.Start();
        }

        private void MinifyPreviewCollapseTimer_Tick(object sender, EventArgs e)
        {
            if (!isMinified || !isMinifyPreviewExpanded)
            {
                minifyPreviewCollapseTimer.Stop();
                return;
            }

            if (MouseButtons == MouseButtons.Left)
                return;

            if (Cursor == Cursors.SizeWE || Cursor == Cursors.SizeNS || Cursor == Cursors.SizeNWSE || Cursor == Cursors.SizeNESW)
                return;

            var clientPos = PointToClient(MousePosition);
            var relaxedBounds = ClientRectangle;
            relaxedBounds.Inflate(14, 14);
            if (relaxedBounds.Contains(clientPos))
                return;

            StartMinifyAnimation(titleHeight);
            isMinifyPreviewExpanded = false;
            minifyPreviewCollapseTimer.Stop();
        }

        private void MinifyAnimationTimer_Tick(object sender, EventArgs e)
        {
            var elapsed = (DateTime.UtcNow - minifyAnimationStartTime).TotalMilliseconds;
            var t = Math.Min(1.0, elapsed / minifyAnimationDurationMs);

            // Cubic ease-out
            var ease = 1.0 - Math.Pow(1.0 - t, 3.0);
            var newHeight = (int)Math.Round(minifyAnimationStartHeight + ((minifyAnimationTargetHeight - minifyAnimationStartHeight) * ease));

            if (newHeight != Height)
                Height = newHeight;

            if (t >= 1.0)
            {
                Height = minifyAnimationTargetHeight;
                minifyAnimationTimer.Stop();
            }
        }

        private void minifyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (minifyToolStripMenuItem.Checked != isMinified)
                ToggleMinify();
        }

        private void ShelfWindow_Click(object sender, EventArgs e)
        {
            if (inlineRenameTextBox.Visible)
            {
                CommitInlineRename();
            }

            shouldUpdateSelection = true;
            Refresh();
        }

        private void ShelfWindow_DoubleClick(object sender, EventArgs e)
        {
            var clientPoint = PointToClient(MousePosition);
            var arrowRect = new Rectangle(Width - 30, 0, 30, titleHeight);
            if (clientPoint.Y < titleHeight && !arrowRect.Contains(clientPoint))
            {
                return;
            }

            var hoveredPath = GetItemPathAtClientPoint(PointToClient(MousePosition));
            if (!string.IsNullOrEmpty(hoveredPath))
            {
                var entry = ShelfEntry.FromPath(hoveredPath);
                entry?.Open();
                return;
            }

            if (!string.IsNullOrEmpty(shelfInfo.TargetFolder) && Directory.Exists(shelfInfo.TargetFolder))
            {
                openFolderToolStripMenuItem_Click(sender, e);
            }
        }

        private void ShelfWindow_Paint(object sender, PaintEventArgs e)
        {
            inlineRenameRenderedThisFrame = false;
            inlineRenameTextRectsByPath.Clear();
            e.Graphics.Clip = new Region(ClientRectangle);
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            e.Graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

            // Background
            e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(100, shelfColor)), ClientRectangle);

            // Title
            e.Graphics.DrawString(Text, titleFont, Brushes.White, new PointF(Width / 2, titleOffset), new StringFormat { Alignment = StringAlignment.Center });
            e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(50, shelfColor)), new RectangleF(0, 0, Width, titleHeight));

            var arrowColor = Color.FromArgb(200, 255, 255, 255);
            using (var pen = new Pen(arrowColor, 2f))
            {
                int arrowSize = 10;
                int arrowX = Width - 20;
                int arrowY = titleHeight / 2;
                if (isMinified)
                {
                    // Down arrow
                    e.Graphics.DrawLine(pen, arrowX - arrowSize / 2, arrowY - arrowSize / 4, arrowX, arrowY + arrowSize / 4);
                    e.Graphics.DrawLine(pen, arrowX, arrowY + arrowSize / 4, arrowX + arrowSize / 2, arrowY - arrowSize / 4);
                }
                else
                {
                    // Up arrow
                    e.Graphics.DrawLine(pen, arrowX - arrowSize / 2, arrowY + arrowSize / 4, arrowX, arrowY - arrowSize / 4);
                    e.Graphics.DrawLine(pen, arrowX, arrowY - arrowSize / 4, arrowX + arrowSize / 2, arrowY + arrowSize / 4);
                }
            }

            // Items
            EnsureLayoutCache();
            var viewportTop = scrollOffset;
            var viewportBottom = scrollOffset + (Height - titleHeight);
            scrollHeight = 0;
            e.Graphics.Clip = new Region(new Rectangle(0, titleHeight, Width, Height - titleHeight));
            var mousePos = PointToClient(MousePosition);

            for (var i = 0; i < cachedEntries.Count; i++)
            {
                var entry = cachedEntries[i];
                var point = cachedItemPositions[i];
                var x = point.X;
                var y = point.Y;
                var itemBottom = y + itemHeight;

                if (itemBottom > scrollHeight)
                    scrollHeight = itemBottom;

                if (itemBottom < viewportTop || y > viewportBottom)
                    continue;

                RenderEntry(e.Graphics, entry, x, y + titleHeight - scrollOffset, mousePos);
            }

            if (!inlineRenameRenderedThisFrame && !string.IsNullOrEmpty(inlineRenamePath) && inlineRenameTextBox.Visible)
            {
                suppressInlineRenameCommitOnHide = true;
                inlineRenameTextBox.Visible = false;
            }
            else if (inlineRenameRenderedThisFrame && !inlineRenameTextBox.Visible && !string.IsNullOrEmpty(inlineRenamePath))
            {
                inlineRenameTextBox.Visible = true;
                UpdateInlineRenameBounds();
            }

            scrollHeight -= (ClientRectangle.Height - titleHeight);
            if (scrollHeight < 0)
                scrollHeight = 0;

            scrollTargetOffset = Math.Min(scrollTargetOffset, scrollHeight);
            scrollOffsetAnimated = Math.Min(scrollOffsetAnimated, scrollHeight);
            scrollOffset = Math.Min(scrollOffset, scrollHeight);

            // Scroll bars
            if (scrollHeight > 0)
            {
                var contentHeight = Height - titleHeight;
                var scrollbarHeight = contentHeight - scrollHeight;
                e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(150, Color.Black)), new Rectangle(Width - 5, titleHeight + scrollOffset, 5, scrollbarHeight));
            }



            // Click handlers
            if (shouldUpdateSelection && !hasSelectionUpdated)
                selectedItem = null;

            if (!hasHoverUpdated)
                hoveringItem = null;

            shouldUpdateSelection = false;
            hasSelectionUpdated = false;
            hasHoverUpdated = false;
        }

        private void RenderEntry(Graphics g, ShelfEntry entry, int x, int y, Point mousePos)
        {
            var icon = entry.ExtractIcon(thumbnailProvider);
            var name = entry.Name;

            var textPosition = new PointF(x, y + iconSize + 5);
            var textMaxSize = new SizeF(itemWidth, textHeight);
            var textRect = new RectangleF(textPosition, textMaxSize);

            var stringFormat = EntryTextFormat;

            var textSize = g.MeasureString(name, iconFont, textMaxSize, stringFormat);
            var outlineRect = new Rectangle(x - 2, y - 2, itemWidth + 2, iconSize + (int)textSize.Height + 5 + 2);
            var outlineRectInner = outlineRect.Shrink(1);

            var mouseOver = mousePos.X >= x && mousePos.Y >= y && mousePos.X < x + outlineRect.Width && mousePos.Y < y + outlineRect.Height;
            var mouseOverText = textRect.Contains(mousePos);

            var isInlineRenamingCurrentItem = inlineRenameTextBox.Visible && string.Equals(inlineRenamePath, entry.Path, StringComparison.OrdinalIgnoreCase);
            if (!inlineRenameTextBox.Visible && !string.IsNullOrEmpty(inlineRenamePath) && string.Equals(inlineRenamePath, entry.Path, StringComparison.OrdinalIgnoreCase))
            {
                isInlineRenamingCurrentItem = true;
            }
            inlineRenameTextRectsByPath[entry.Path] = textRect;
            if (isInlineRenamingCurrentItem)
            {
                inlineRenameRenderedThisFrame = true;
                inlineRenameBaseRect = textRect;
                UpdateInlineRenameBounds();
            }

            var isDragHoverTarget = !string.IsNullOrEmpty(dragHoverTargetFolder) && string.Equals(dragHoverTargetFolder, entry.Path, StringComparison.OrdinalIgnoreCase);

            if (mouseOver)
            {
                hoveringItem = entry.Path;
                hasHoverUpdated = true;
            }

            if (mouseOver && shouldUpdateSelection)
            {
                if (selectedItem == entry.Path && mouseOverText)
                {
                    BeginInlineRename(entry, textRect);
                    shouldUpdateSelection = false;
                    hasSelectionUpdated = true;
                }
                else
                {
                    selectedItem = entry.Path;
                    shouldUpdateSelection = false;
                    hasSelectionUpdated = true;
                }
            }

            if (selectedItem == entry.Path)
            {
                if (mouseOver)
                {
                    g.DrawRectangle(new Pen(Color.FromArgb(120, SystemColors.ActiveBorder)), outlineRectInner);
                    g.FillRectangle(new SolidBrush(Color.FromArgb(100, SystemColors.GradientActiveCaption)), outlineRect);
                }
                else
                {
                    g.DrawRectangle(new Pen(Color.FromArgb(120, SystemColors.ActiveBorder)), outlineRectInner);
                    g.FillRectangle(new SolidBrush(Color.FromArgb(80, SystemColors.GradientInactiveCaption)), outlineRect);
                }
            }
            else
            {
                if (mouseOver)
                {
                    g.DrawRectangle(new Pen(Color.FromArgb(120, SystemColors.ActiveBorder)), outlineRectInner);
                    g.FillRectangle(new SolidBrush(Color.FromArgb(80, SystemColors.ActiveCaption)), outlineRect);
                }
            }

            if (isDragHoverTarget)
            {
                g.DrawRectangle(new Pen(Color.FromArgb(220, 120, 200, 255), 2f), outlineRectInner);
                g.FillRectangle(new SolidBrush(Color.FromArgb(45, 120, 200, 255)), outlineRect);
            }

            var iconX = x + itemWidth / 2 - iconSize / 2;
            g.DrawIcon(icon, new Rectangle(iconX, y, iconSize, iconSize));

            if (!isInlineRenamingCurrentItem)
            {
                g.DrawString(name, iconFont, new SolidBrush(Color.FromArgb(180, 15, 15, 15)), new RectangleF(textPosition.Move(shadowDist, shadowDist), textMaxSize), stringFormat);
                g.DrawString(name, iconFont, Brushes.White, new RectangleF(textPosition, textMaxSize), stringFormat);
            }
        }

        private void InlineRenameTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                CommitInlineRename();
            }
            else if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                CancelInlineRename();
            }
        }

        private void InlineRenameTextBox_LostFocus(object sender, EventArgs e)
        {
            if (suppressInlineRenameCommitOnHide)
            {
                suppressInlineRenameCommitOnHide = false;
                return;
            }

            CommitInlineRename();
        }

        private void InlineRenameTextBox_TextChanged(object sender, EventArgs e)
        {
            UpdateInlineRenameBounds();
        }

        private void UpdateInlineRenameBounds()
        {
            if (!inlineRenameTextBox.Visible)
                return;

            var width = Math.Max(40, (int)Math.Ceiling(inlineRenameBaseRect.Width));
            var measured = TextRenderer.MeasureText(
                inlineRenameTextBox.Text + " ",
                inlineRenameTextBox.Font,
                new Size(width, 1000),
                TextFormatFlags.WordBreak);

            var maxHeight = textHeight * 4;
            var height = Math.Max(22, Math.Min(measured.Height + 6, maxHeight));

            var x = (int)Math.Round(inlineRenameBaseRect.X);
            var y = (int)Math.Round(inlineRenameBaseRect.Y);

            // Keep editor strictly inside content area (never over title bar)
            var minY = titleHeight + 1;
            var maxY = Math.Max(minY, Height - height - 1);
            if (y < minY) y = minY;
            if (y > maxY) y = maxY;

            var minX = 0;
            var maxX = Math.Max(minX, Width - width);
            if (x < minX) x = minX;
            if (x > maxX) x = maxX;

            inlineRenameTextBox.Bounds = new Rectangle(x, y, width, height);
        }

        private void BeginInlineRename(ShelfEntry entry, RectangleF textRect)
        {
            if (entry == null || string.IsNullOrEmpty(entry.Path))
                return;

            inlineRenamePath = entry.Path;
            inlineRenameBaseRect = textRect;
            inlineRenameTextBox.Font = iconFont;
            inlineRenameIsFile = File.Exists(entry.Path);
            inlineRenameOriginalName = Path.GetFileName(entry.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            inlineRenameExtension = inlineRenameIsFile ? Path.GetExtension(inlineRenameOriginalName) : string.Empty;

            var initialText = inlineRenameIsFile
                ? Path.GetFileNameWithoutExtension(inlineRenameOriginalName)
                : inlineRenameOriginalName;

            inlineRenameTextBox.Text = initialText;
            inlineRenameTextBox.Visible = true;
            UpdateInlineRenameBounds();
            inlineRenameTextBox.BringToFront();
            inlineRenameTextBox.Focus();
            inlineRenameTextBox.SelectAll();
        }

        private void CommitInlineRename()
        {
            if (!inlineRenameTextBox.Visible)
                return;

            var oldPath = inlineRenamePath;
            var newNameInput = inlineRenameTextBox.Text?.Trim();

            inlineRenameTextBox.Visible = false;
            inlineRenamePath = null;

            if (string.IsNullOrEmpty(oldPath))
                return;

            var newName = newNameInput;
            if (inlineRenameIsFile && !string.IsNullOrEmpty(inlineRenameExtension))
            {
                var inputExt = Path.GetExtension(newNameInput ?? string.Empty);
                if (string.IsNullOrEmpty(inputExt))
                {
                    newName = (newNameInput ?? string.Empty) + inlineRenameExtension;
                }
            }

            TryRenameItem(oldPath, newName, true);
        }

        private void CancelInlineRename()
        {
            inlineRenameTextBox.Visible = false;
            inlineRenamePath = null;
            inlineRenameBaseRect = RectangleF.Empty;
            inlineRenameOriginalName = null;
            inlineRenameExtension = null;
            inlineRenameIsFile = false;
        }

        private void ShelfWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.F2 || string.IsNullOrEmpty(selectedItem) || inlineRenameTextBox.Visible)
                return;

            if (!ItemExists(selectedItem))
                return;

            if (!inlineRenameTextRectsByPath.TryGetValue(selectedItem, out var rect))
                return;

            var entry = ShelfEntry.FromPath(selectedItem);
            if (entry == null)
                return;

            BeginInlineRename(entry, rect);
            e.SuppressKeyPress = true;
            e.Handled = true;
        }

        private bool TryRenameItem(string currentPath, string newName, bool showErrors)
        {
            if (string.IsNullOrEmpty(currentPath))
                return false;

            if (!File.Exists(currentPath) && !Directory.Exists(currentPath))
                return false;

            var currentName = Path.GetFileName(currentPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var parentDir = Path.GetDirectoryName(currentPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            if (string.IsNullOrEmpty(parentDir))
                return false;

            if (string.IsNullOrWhiteSpace(newName) || string.Equals(newName, currentName, StringComparison.OrdinalIgnoreCase))
                return false;

            foreach (var invalidChar in Path.GetInvalidFileNameChars())
            {
                if (newName.IndexOf(invalidChar) >= 0)
                {
                    if (showErrors)
                        MessageBox.Show(this, "The name contains invalid characters.", "Rename", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
            }

            var newPath = Path.Combine(parentDir, newName);
            if (Directory.Exists(newPath) || File.Exists(newPath))
            {
                if (showErrors)
                    MessageBox.Show(this, "A file or folder with that name already exists.", "Rename", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            try
            {
                if (File.Exists(currentPath))
                    File.Move(currentPath, newPath);
                else if (Directory.Exists(currentPath))
                    Directory.Move(currentPath, newPath);

                if (string.IsNullOrEmpty(shelfInfo.TargetFolder) || !Directory.Exists(shelfInfo.TargetFolder))
                {
                    var index = shelfInfo.Files.IndexOf(currentPath);
                    if (index >= 0)
                        shelfInfo.Files[index] = newPath;

                    MarkEntriesDirty();
                }
                else
                {
                    ReloadFolderFiles();
                }

                hoveringItem = newPath;
                if (selectedItem == currentPath)
                    selectedItem = newPath;

                Save();
                Refresh();
                return true;
            }
            catch
            {
                if (showErrors)
                    MessageBox.Show(this, "Could not rename the selected item.", "Rename", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private void renameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var form = new Form
            {
                Text = "Rename Shelf",
                Size = new Size(320, 150),
                StartPosition = FormStartPosition.CenterParent,
                Font = this.Font,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White
            };

            var tbName = new TextBox
            {
                Location = new Point(20, 30),
                Size = new Size(260, 25),
                Text = Text,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            var btnOk = new Button
            {
                Text = "OK",
                Location = new Point(190, 70),
                Size = new Size(90, 30),
                DialogResult = DialogResult.OK,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 120, 215)
            };

            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Paint += (s, ev) =>
            {
                var btn = (Button)s;
                using (var path = new System.Drawing.Drawing2D.GraphicsPath())
                {
                    int radius = 10;
                    var rect = new Rectangle(0, 0, btn.Width - 1, btn.Height - 1);
                    path.AddArc(rect.X, rect.Y, radius, radius, 180, 90);
                    path.AddArc(rect.X + rect.Width - radius, rect.Y, radius, radius, 270, 90);
                    path.AddArc(rect.X + rect.Width - radius, rect.Y + rect.Height - radius, radius, radius, 0, 90);
                    path.AddArc(rect.X, rect.Y + rect.Height - radius, radius, radius, 90, 90);
                    path.CloseFigure();
                    btn.Region = new Region(path);
                }
            };

            form.Controls.Add(tbName);
            form.Controls.Add(btnOk);
            form.AcceptButton = btnOk;

            if (form.ShowDialog(this) == DialogResult.OK)
            {
                Text = string.IsNullOrWhiteSpace(tbName.Text) ? "New Shelf" : tbName.Text;
                shelfInfo.Name = Text;
                Refresh();
                Save();
            }
        }

        private void addFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(shelfInfo.TargetFolder) || !Directory.Exists(shelfInfo.TargetFolder))
            {
                MessageBox.Show(this, "This shelf does not have a target folder.", "Add folder", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var dialog = new EditDialog("New Folder");
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            var folderName = dialog.NewName?.Trim();
            if (string.IsNullOrWhiteSpace(folderName))
                return;

            foreach (var invalidChar in Path.GetInvalidFileNameChars())
            {
                if (folderName.IndexOf(invalidChar) >= 0)
                {
                    MessageBox.Show(this, "The folder name contains invalid characters.", "Add folder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            var newFolderPath = Path.Combine(shelfInfo.TargetFolder, folderName);
            if (Directory.Exists(newFolderPath) || File.Exists(newFolderPath))
            {
                MessageBox.Show(this, "A file or folder with that name already exists.", "Add folder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                Directory.CreateDirectory(newFolderPath);
                ReloadFolderFiles();
                Save();
                Refresh();
            }
            catch
            {
                MessageBox.Show(this, "Could not create the folder.", "Add folder", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void renameItemToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(hoveringItem))
                return;

            if (!File.Exists(hoveringItem) && !Directory.Exists(hoveringItem))
                return;

            var currentPath = hoveringItem;
            var currentName = Path.GetFileName(currentPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            var dialog = new EditDialog(currentName);
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            var newName = dialog.NewName?.Trim();
            TryRenameItem(currentPath, newName, true);
        }

        private void ShelfWindow_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (Application.OpenForms.Count == 0)
                Application.Exit();
        }

        private readonly object saveLock = new object();
        private void Save()
        {
            lock (saveLock)
            {
                ShelfManager.Instance.UpdateShelf(shelfInfo);
            }
        }

        private void ShelfWindow_LocationChanged(object sender, EventArgs e)
        {
            throttledMove.Run(() =>
            {
                shelfInfo.PosX = Location.X;
                shelfInfo.PosY = Location.Y;
                Save();
            });
        }

        private void lockedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            shelfInfo.Locked = lockedToolStripMenuItem.Checked;
            Save();
        }

        private void ShelfWindow_Load(object sender, EventArgs e)
        {

        }

        private void titleSizeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var originalTitleHeight = shelfInfo.TitleHeight;
            var originalLogicalTitleHeight = logicalTitleHeight;
            var originalDeviceTitleHeight = titleHeight;
            var originalHeight = Height;

            Action<int> applyPreview = val =>
            {
                logicalTitleHeight = val;
                titleHeight = LogicalToDeviceUnits(logicalTitleHeight);
                ReloadFonts();

                if (isMinified && !isMinifyPreviewExpanded)
                {
                    Height = titleHeight;
                }

                Invalidate();
            };

            var dialog = new HeightDialog(shelfInfo.TitleHeight);
            dialog.PreviewTitleHeightChanged += applyPreview;
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                shelfInfo.TitleHeight = dialog.TitleHeight;
                applyPreview(dialog.TitleHeight);
                Save();
            }
            else
            {
                shelfInfo.TitleHeight = originalTitleHeight;
                logicalTitleHeight = originalLogicalTitleHeight;
                titleHeight = originalDeviceTitleHeight;
                ReloadFonts();

                if (isMinified && !isMinifyPreviewExpanded)
                    Height = originalDeviceTitleHeight;
                else
                    Height = originalHeight;

                Invalidate();
            }

            dialog.PreviewTitleHeightChanged -= applyPreview;
        }

        private void ShelfWindow_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                var arrowRect = new Rectangle(Width - 30, 0, 30, titleHeight);
                if (arrowRect.Contains(e.Location))
                {
                    ToggleMinify();
                    return;
                }
            }

            if (e.Button != MouseButtons.Right)
                return;

            if (hoveringItem != null && !ModifierKeys.HasFlag(Keys.Shift))
            {
                shellContextMenu.ShowContextMenu(new[] { new FileInfo(hoveringItem) }, MousePosition);
            }
            else
            {
                appContextMenu.Show(this, e.Location);
            }
        }

        private void ShelfWindow_MouseWheel(object sender, MouseEventArgs e)
        {
            if (scrollHeight < 1)
                return;

            var notchCount = Math.Max(1, Math.Abs(e.Delta) / 120);
            var scrollStep = Math.Max(24, itemHeight / 2);
            scrollTargetOffset -= Math.Sign(e.Delta) * scrollStep * notchCount;
            if (scrollTargetOffset < 0)
                scrollTargetOffset = 0;
            if (scrollTargetOffset > scrollHeight)
                scrollTargetOffset = scrollHeight;

            if (!scrollAnimationTimer.Enabled)
                scrollAnimationTimer.Start();

            Invalidate();
        }

        private void ScrollAnimationTimer_Tick(object sender, EventArgs e)
        {
            var diff = scrollTargetOffset - scrollOffsetAnimated;
            if (Math.Abs(diff) <= 0.2f)
            {
                SetScrollOffset(scrollTargetOffset);
                scrollAnimationTimer.Stop();
                return;
            }

            var step = Math.Max(1.5f, (Math.Abs(diff) * 0.6f) + 0.5f);
            SetScrollOffset(scrollOffsetAnimated + (Math.Sign(diff) * Math.Min(step, Math.Abs(diff))));
        }

        private void SetScrollOffset(float newOffset)
        {
            var clamped = Math.Max(0f, Math.Min(scrollHeight, newOffset));

            scrollOffsetAnimated = clamped;
            scrollOffset = (int)Math.Round(scrollOffsetAnimated);

            Invalidate();
        }

        private void ThumbnailProvider_IconThumbnailLoaded(object sender, EventArgs e)
        {
            Invalidate();
        }

        private bool ItemExists(string path)
        {
            return File.Exists(path) || Directory.Exists(path);
        }

        private void showWindowsMenuToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(shelfInfo.TargetFolder) || !Directory.Exists(shelfInfo.TargetFolder))
                return;

            shellContextMenu.ShowContextMenu(new[] { new DirectoryInfo(shelfInfo.TargetFolder) }, MousePosition);
        }

        private void openFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(shelfInfo.TargetFolder) || !Directory.Exists(shelfInfo.TargetFolder))
                return;

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = shelfInfo.TargetFolder,
                    UseShellExecute = true
                };

                Process.Start(startInfo);
            }
            catch
            {
                // ignore
            }
        }

        private void newShelfToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var targetPath = ModernFolderBrowser.ShowDialog(this.Handle, "Select a folder to link this Shelf to");
            if (string.IsNullOrEmpty(targetPath))
                return;

            var defaultName = Path.GetFileName(targetPath);
            if (string.IsNullOrWhiteSpace(defaultName))
                defaultName = "New Shelf";

            var dialog = new EditDialog(defaultName);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                var shelfName = string.IsNullOrWhiteSpace(dialog.NewName) ? "New Shelf" : dialog.NewName;
                ShelfManager.Instance.CreateShelf(shelfName, targetPath);
            }
        }
    }

}

