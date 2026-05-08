using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Reflection;
using Shelvia.Model;
using Shelvia.Util;

namespace Shelvia
{
    public class AdminForm : Form
    {
        private class HiddenScrollFlowLayoutPanel : FlowLayoutPanel
        {
            private const int SB_BOTH = 3;

            [DllImport("user32.dll")]
            private static extern bool ShowScrollBar(IntPtr hWnd, int wBar, bool bShow);

            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);

                if (!IsHandleCreated)
                    return;

                ShowScrollBar(Handle, SB_BOTH, false);
            }
        }

        private DataGridView dgvShelves;
        private Label lblTitle;
        private FlowLayoutPanel pnlShelves;
        private ContextMenuStrip trayMenu;
        private NotifyIcon trayIcon;
        private Panel pnlEmpty;
        private Form colorPalettePopup;
        private Icon appIcon;
        private bool _allowShowDisplay = false;

        private static readonly Color[] ShelfColorPalette =
        {
            Color.Black,
            Color.FromArgb(45, 45, 45),
            Color.FromArgb(70, 70, 70),
            Color.FromArgb(0, 120, 215),
            Color.SteelBlue,
            Color.Teal,
            Color.SeaGreen,
            Color.ForestGreen,
            Color.OliveDrab,
            Color.Goldenrod,
            Color.DarkOrange,
            Color.IndianRed,
            Color.Firebrick,
            Color.MediumPurple,
            Color.MediumSlateBlue,
            Color.DimGray
        };

        private const int ShelfCardHeight = 156;
        private const int ShelfCardMaxWidth = 238;
        private const int ShelfCardMinWidth = 190;
        private const int ShelfCardGap = 12;

        public AdminForm()
        {
            InitializeComponent();
            ShelfManager.Instance.ShelvesChanged += ShelfManager_ShelvesChanged;
            Shown += (s, e) => LoadShelvesList();
        }

        private Icon LoadAppIcon()
        {
            try
            {
                // 1) Prefer executable icon (works great for single-file/self-contained publish)
                var exePath = Application.ExecutablePath;
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    var exeIcon = Icon.ExtractAssociatedIcon(exePath);
                    if (exeIcon != null)
                        return exeIcon;
                }

                // 2) Development-time icon file candidates
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var candidates = new[]
                {
                    Path.Combine(baseDir, "icons", "Shelvia.ico"),
                    Path.GetFullPath(Path.Combine(baseDir, "..", "icons", "Shelvia.ico")),
                    Path.GetFullPath(Path.Combine(baseDir, "..", "..", "icons", "Shelvia.ico")),
                    Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "icons", "Shelvia.ico"))
                };

                foreach (var iconPath in candidates)
                {
                    if (File.Exists(iconPath))
                        return new Icon(iconPath);
                }
            }
            catch { }

            return SystemIcons.Application;
        }

        private void InitializeComponent()
        {
            Text = "Shelvia - Admin Panel";
            Size = new Size(808, 600);
            MinimumSize = new Size(760, 520);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 10F, FontStyle.Regular);
            BackColor = Color.FromArgb(26, 26, 26);
            ForeColor = Color.White;
            DoubleBuffered = true;

            appIcon = LoadAppIcon();
            Icon = appIcon;

            lblTitle = new Label { 
                Text = "Manage your Shelves", 
                Location = new Point(20, 20), 
                AutoSize = true,
                Font = new Font("Segoe UI", 16F, FontStyle.Bold)
            };
            Controls.Add(lblTitle);

            //var lblSubtitle = new Label
            //{
            //    Text = "Organiza, renombra y personaliza tus Shelves",
            //    Location = new Point(22, 50),
            //    AutoSize = true,
            //    ForeColor = Color.FromArgb(170, 170, 170),
            //    Font = new Font("Segoe UI", 9.5F, FontStyle.Regular)
            //};
            //Controls.Add(lblSubtitle);

            pnlShelves = new HiddenScrollFlowLayoutPanel
            {
                Location = new Point(20, 86),
                Size = new Size(ClientSize.Width - 40, ClientSize.Height - 106),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                AutoScroll = true,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Color.FromArgb(26, 26, 26),
                Padding = Padding.Empty
            };
            Controls.Add(pnlShelves);

            pnlEmpty = new Panel {
                Location = new Point(20, 86),
                Size = new Size(ClientSize.Width - 40, ClientSize.Height - 106),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.FromArgb(34, 34, 34),
                Cursor = Cursors.Hand,
                Visible = false
            };
            pnlEmpty.Paint += (s, e) => {
                using (var p = new Pen(Color.FromArgb(100, 100, 100), 2) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash })
                {
                    e.Graphics.DrawRectangle(p, new Rectangle(0, 0, pnlEmpty.Width - 1, pnlEmpty.Height - 1));
                }
            };
            pnlEmpty.Click += BtnAdd_Click;

            var lblEmpty = new Label {
                Text = "No Shelves found.\nClick here to add your first shelf.",
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 16F, FontStyle.Regular),
                ForeColor = Color.FromArgb(150, 150, 150)
            };
            lblEmpty.Click += BtnAdd_Click;
            pnlEmpty.Controls.Add(lblEmpty);
            Controls.Add(pnlEmpty);

            trayMenu = new ContextMenuStrip();
            trayMenu.Renderer = new DarkMenuRenderer();
            trayMenu.ForeColor = Color.White;
            trayMenu.Items.Add("Admin Panel", null, (s, e) => { _allowShowDisplay = true; this.Show(); this.WindowState = FormWindowState.Normal; });
            trayMenu.Items.Add("Exit", null, (s, e) => Application.Exit());

            trayIcon = new NotifyIcon();
            trayIcon.Text = "Shelvia";
            trayIcon.ContextMenuStrip = trayMenu;
            trayIcon.Visible = true;
            trayIcon.Icon = appIcon;
            trayIcon.DoubleClick += (s, e) => { _allowShowDisplay = true; this.Show(); this.WindowState = FormWindowState.Normal; };

            this.FormClosing += AdminForm_FormClosing;
        }

        protected override void SetVisibleCore(bool value)
        {
            if (!_allowShowDisplay)
            {
                value = false;
                if (!IsHandleCreated) CreateHandle();
            }
            base.SetVisibleCore(value);
        }

        private void AdminForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
                trayIcon.ShowBalloonTip(2000, "Shelvia", "Shelvia is still running in the tray.", ToolTipIcon.Info);
            }
        }

        private void EnableDoubleBuffering(DataGridView grid)
        {
            try
            {
                typeof(DataGridView).InvokeMember(
                    "DoubleBuffered",
                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.SetProperty,
                    null,
                    grid,
                    new object[] { true });
            }
            catch
            {
                // Ignore if reflection fails in future framework versions
            }
        }

        private void LoadShelvesList()
        {
            pnlShelves.SuspendLayout();
            pnlShelves.Controls.Clear();
            var basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Shelvia");
            if (Directory.Exists(basePath))
            {
                foreach (var dir in Directory.GetDirectories(basePath))
                {
                    var metaFile = Path.Combine(dir, "__shelf_metadata.xml");
                    if (File.Exists(metaFile))
                    {
                        try
                        {
                            var serializer = new System.Xml.Serialization.XmlSerializer(typeof(ShelfInfo));
                            using (var reader = new StreamReader(metaFile))
                            {
                                var shelf = (ShelfInfo)serializer.Deserialize(reader);
                                if (shelf != null)
                                {
                                    pnlShelves.Controls.Add(CreateShelfCard(shelf));
                                }
                            }
                        }
                        catch { }
                    }
                }
            }

            pnlShelves.Controls.Add(CreateAddShelfCard());

            pnlShelves.Visible = true;
            pnlEmpty.Visible = false;
            pnlShelves.ResumeLayout();
        }

        private Panel CreateAddShelfCard()
        {
            var cardWidth = GetShelfCardWidth();
            var card = new Panel
            {
                Size = new Size(cardWidth, ShelfCardHeight),
                Margin = new Padding(0, 0, ShelfCardGap, ShelfCardGap),
                BackColor = pnlShelves.BackColor,
                Cursor = Cursors.Hand,
                Padding = new Padding(1)
            };

            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                using (var path = CreateRoundedRectPath(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 18))
                using (var border = new Pen(Color.FromArgb(125, 170, 170, 170), 1.5f)
                {
                    DashStyle = System.Drawing.Drawing2D.DashStyle.Dash,
                    Alignment = System.Drawing.Drawing2D.PenAlignment.Inset
                })
                {
                    e.Graphics.DrawPath(border, path);
                }
            };

            var lblPlus = new Label
            {
                Text = "+",
                AutoSize = false,
                Size = new Size(cardWidth - 32, 48),
                Location = new Point(16, 42),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 28F, FontStyle.Regular),
                ForeColor = Color.FromArgb(180, 180, 180),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };

            var lblText = new Label
            {
                Text = "Add Shelf",
                AutoSize = false,
                Size = new Size(cardWidth - 32, 24),
                Location = new Point(16, 100),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(165, 165, 165),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };

            card.Click += BtnAdd_Click;
            lblPlus.Click += BtnAdd_Click;
            lblText.Click += BtnAdd_Click;

            card.Controls.Add(lblPlus);
            card.Controls.Add(lblText);

            return card;
        }

        private Panel CreateShelfCard(ShelfInfo shelf)
        {
            var shelfColor = shelf.ShelfColorArgb != 0 ? Color.FromArgb(shelf.ShelfColorArgb) : Color.Black;
            var textColor = GetReadableTextColor(shelfColor);
            var cardWidth = GetShelfCardWidth();

            var card = new Panel
            {
                Size = new Size(cardWidth, ShelfCardHeight),
                Margin = new Padding(0, 0, ShelfCardGap, ShelfCardGap),
                BackColor = pnlShelves.BackColor,
                Cursor = Cursors.Hand,
                Padding = new Padding(1)
            };

            var borderColor = Color.FromArgb(95, 255, 255, 255);
            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                using (var path = CreateRoundedRectPath(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 18))
                using (var border = new Pen(borderColor, 1.1f) { Alignment = System.Drawing.Drawing2D.PenAlignment.Inset })
                using (var fill = new SolidBrush(shelfColor))
                {
                    e.Graphics.FillPath(fill, path);
                    e.Graphics.DrawPath(border, path);
                }
            };

            card.DoubleClick += (s, e) => OpenShelfFolder(shelf);

            var lblName = new Label
            {
                Text = shelf.Name,
                AutoSize = false,
                Location = new Point(16, 14),
                Size = new Size(cardWidth - 36, 24),
                BackColor = shelfColor,
                ForeColor = textColor,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            lblName.DoubleClick += (s, e) => OpenShelfFolder(shelf);

            var lblTarget = new Label
            {
                Text = string.IsNullOrEmpty(shelf.TargetFolder) ? "(None)" : shelf.TargetFolder,
                AutoSize = false,
                Location = new Point(16, 40),
                Size = new Size(cardWidth - 36, 40),
                BackColor = shelfColor,
                ForeColor = Color.FromArgb(Math.Min(255, textColor.R + 35), Math.Min(255, textColor.G + 35), Math.Min(255, textColor.B + 35)),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                Cursor = Cursors.Default
            };
            lblTarget.DoubleClick += (s, e) => OpenShelfFolder(shelf);

            var actionsPanel = new FlowLayoutPanel
            {
                Location = new Point(16, 102),
                Size = new Size(cardWidth - 32, 32),
                BackColor = Color.Transparent,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            var colorButton = CreateRainbowColorButton();
            colorButton.Click += (s, e) => ShowShelfColorPalette(shelf, colorButton);

            var btnRename = CreateCardIconButton("✎");
            btnRename.Click += (s, e) => EditShelfName(shelf);

            var btnDelete = CreateCardIconButton("🗑");
            btnDelete.ForeColor = Color.FromArgb(255, 170, 170);
            btnDelete.Click += (s, e) => DeleteShelf(shelf);

            card.Controls.Add(lblName);
            card.Controls.Add(lblTarget);
            actionsPanel.Controls.Add(colorButton);
            actionsPanel.Controls.Add(btnRename);
            actionsPanel.Controls.Add(btnDelete);
            card.Controls.Add(actionsPanel);

            return card;
        }

        private int GetShelfCardWidth()
        {
            var availableWidth = pnlShelves.ClientSize.Width;
            if (availableWidth <= 0)
                return ShelfCardMaxWidth;

            var columns = Math.Max(1, (availableWidth + ShelfCardGap) / (ShelfCardMaxWidth + ShelfCardGap));
            var computedWidth = (availableWidth - ((columns - 1) * ShelfCardGap)) / columns;

            return Math.Max(ShelfCardMinWidth, Math.Min(ShelfCardMaxWidth, computedWidth));
        }

        private Button CreateCardIconButton(string text)
        {
            var button = new Button
            {
                Size = new Size(30, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = Color.White,
                Text = text,
                Font = new Font("Segoe UI Emoji", 9.5F, FontStyle.Regular),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 6, 0),
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = Padding.Empty
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseDownBackColor = Color.Transparent;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(32, 255, 255, 255);
            button.Paint += (s, e) =>
            {
                var btn = (Button)s;
                using (var path = new System.Drawing.Drawing2D.GraphicsPath())
                {
                    int radius = 8;
                    var rect = new Rectangle(0, 0, btn.Width - 1, btn.Height - 1);
                    path.AddArc(rect.X, rect.Y, radius, radius, 180, 90);
                    path.AddArc(rect.X + rect.Width - radius, rect.Y, radius, radius, 270, 90);
                    path.AddArc(rect.X + rect.Width - radius, rect.Y + rect.Height - radius, radius, radius, 0, 90);
                    path.AddArc(rect.X, rect.Y + rect.Height - radius, radius, radius, 90, 90);
                    path.CloseFigure();
                    btn.Region = new Region(path);
                }
            };
            return button;
        }

        private Button CreateRainbowColorButton()
        {
            var button = new Button
            {
                Size = new Size(30, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = Color.White,
                Text = string.Empty,
                Cursor = Cursors.Hand,
                TabStop = false,
                Margin = new Padding(0, 0, 6, 0),
                Padding = Padding.Empty
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseDownBackColor = Color.Transparent;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(32, 255, 255, 255);
            button.Paint += (s, e) =>
            {
                var btn = (Button)s;
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                var rect = new Rectangle(2, 2, btn.Width - 5, btn.Height - 5);
                var colors = new[]
                {
                    Color.Red,
                    Color.Orange,
                    Color.Gold,
                    Color.LimeGreen,
                    Color.DodgerBlue,
                    Color.MediumPurple
                };

                float sweep = 360f / colors.Length;
                for (int i = 0; i < colors.Length; i++)
                {
                    using (var brush = new SolidBrush(colors[i]))
                    {
                        e.Graphics.FillPie(brush, rect, i * sweep, sweep);
                    }
                }

                using (var whiteRing = new Pen(Color.White, 1.6f))
                    e.Graphics.DrawEllipse(whiteRing, rect);

                using (var inner = new SolidBrush(Color.FromArgb(80, 30, 30, 30)))
                {
                    e.Graphics.FillEllipse(inner, rect.X + 6, rect.Y + 6, rect.Width - 12, rect.Height - 12);
                }
            };
            return button;
        }

        private static System.Drawing.Drawing2D.GraphicsPath CreateRoundedRectPath(Rectangle rect, int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            var diameter = radius * 2;
            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static void UpdateCardRegion(Panel card)
        {
            using (var path = CreateRoundedRectPath(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 18))
            {
                card.Region = new Region(path);
            }
        }

        private static Color LightenColor(Color color, float amount)
        {
            amount = Math.Max(0f, Math.Min(1f, amount));
            var r = (int)(color.R + ((255 - color.R) * amount));
            var g = (int)(color.G + ((255 - color.G) * amount));
            var b = (int)(color.B + ((255 - color.B) * amount));
            return Color.FromArgb(color.A, r, g, b);
        }

        private static Color GetReadableTextColor(Color background)
        {
            var luminance = (0.299 * background.R) + (0.587 * background.G) + (0.114 * background.B);
            return luminance > 160 ? Color.Black : Color.White;
        }

        private void OpenShelfFolder(ShelfInfo shelf)
        {
            if (shelf == null || string.IsNullOrEmpty(shelf.TargetFolder) || !Directory.Exists(shelf.TargetFolder))
                return;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = shelf.TargetFolder,
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private void EditShelfName(ShelfInfo shelf)
        {
            if (shelf == null)
                return;

            var form = new Form { Text = "Rename Shelf", Size = new Size(320, 150), StartPosition = FormStartPosition.CenterParent, Font = this.Font, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, BackColor = Color.FromArgb(40, 40, 40), ForeColor = Color.White };
            StyleFormDarkTitleBar(form);
            var tbName = new TextBox { Location = new Point(20, 30), Size = new Size(260, 25), Text = shelf.Name, BackColor = Color.FromArgb(60, 60, 60), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            var btnOk = new Button { Text = "OK", Location = new Point(190, 70), Size = new Size(90, 30), DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0, 120, 215) };
            btnOk.FlatAppearance.BorderSize = 0;
            form.Controls.Add(tbName);
            form.Controls.Add(btnOk);
            form.AcceptButton = btnOk;

            if (form.ShowDialog() == DialogResult.OK)
            {
                var newName = string.IsNullOrWhiteSpace(tbName.Text) ? "New Shelf" : tbName.Text;
                shelf.Name = newName;
                ShelfManager.Instance.UpdateShelf(shelf);
                LoadShelvesList();
            }
        }

        private void DeleteShelf(ShelfInfo shelf)
        {
            if (shelf == null)
                return;

            if (MessageBox.Show($"Delete the Shelf '{shelf.Name}'?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            ShelfManager.Instance.RemoveShelf(shelf);

            Form toClose = null;
            foreach (Form openForm in Application.OpenForms)
            {
                if (openForm is ShelfWindow fw && fw.ShelfId == shelf.Id)
                {
                    toClose = fw;
                    break;
                }
            }
            toClose?.Close();

            LoadShelvesList();
        }

        private void StyleFormDarkTitleBar(Form form)
        {
            _ = form.Handle; // Force handle creation
            int useImmersiveDarkMode = 1;
            try { DwmSetWindowAttribute(form.Handle, 20, ref useImmersiveDarkMode, sizeof(int)); } catch { } // Windows 11
            try { DwmSetWindowAttribute(form.Handle, 19, ref useImmersiveDarkMode, sizeof(int)); } catch { } // Windows 10
        }

        private void DgvShelves_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            var shelf = dgvShelves.Rows[e.RowIndex].Tag as ShelfInfo;
            if (shelf == null) return;

            if (e.ColumnIndex == dgvShelves.Columns["ColColor"].Index)
            {
                ShowShelfColorPalette(shelf, dgvShelves.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, true));
            }
            else if (e.ColumnIndex == dgvShelves.Columns["ColRename"].Index)
            {
                var form = new Form { Text = "Rename Shelf", Size = new Size(320, 150), StartPosition = FormStartPosition.CenterParent, Font = this.Font, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, BackColor = Color.FromArgb(40, 40, 40), ForeColor = Color.White };
                StyleFormDarkTitleBar(form);
                var tbName = new TextBox { Location = new Point(20, 30), Size = new Size(260, 25), Text = shelf.Name, BackColor = Color.FromArgb(60, 60, 60), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
                var btnOk = new Button { Text = "OK", Location = new Point(190, 70), Size = new Size(90, 30), DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0, 120, 215) };
                btnOk.FlatAppearance.BorderSize = 0;
                btnOk.Paint += (s, ev) => {
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

                if (form.ShowDialog() == DialogResult.OK)
                {
                    var newName = string.IsNullOrWhiteSpace(tbName.Text) ? "New Shelf" : tbName.Text;
                    shelf.Name = newName;
                    ShelfManager.Instance.UpdateShelf(shelf);

                    foreach (Form openForm in Application.OpenForms)
                    {
                        if (openForm is ShelfWindow fw && fw.ShelfId == shelf.Id)
                        {
                            fw.Text = newName;
                            fw.Refresh();
                            break;
                        }
                    }
                    LoadShelvesList();
                }
            }
            else if (e.ColumnIndex == dgvShelves.Columns["ColRemove"].Index)
            {
                if (MessageBox.Show($"Delete the Shelf '{shelf.Name}'?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    ShelfManager.Instance.RemoveShelf(shelf);

                    Form toClose = null;
                    foreach (Form openForm in Application.OpenForms)
                    {
                        if (openForm is ShelfWindow fw && fw.ShelfId == shelf.Id)
                        {
                            toClose = fw;
                            break;
                        }
                    }
                    toClose?.Close();

                    LoadShelvesList();
                }
            }
        }

        private void ShowShelfColorPalette(ShelfInfo shelf, Control anchorControl)
        {
            if (anchorControl == null)
                return;

            var screenPoint = anchorControl.PointToScreen(new Point(0, anchorControl.Height));
            ShowShelfColorPalette(shelf, screenPoint);
        }

        private void ShowShelfColorPalette(ShelfInfo shelf, Rectangle cellRect)
        {
            var screenPoint = dgvShelves.PointToScreen(new Point(cellRect.Left, cellRect.Bottom));
            ShowShelfColorPalette(shelf, screenPoint);
        }

        private void ShowShelfColorPalette(ShelfInfo shelf, Point screenPoint)
        {
            if (colorPalettePopup != null)
            {
                colorPalettePopup.Close();
                colorPalettePopup.Dispose();
                colorPalettePopup = null;
            }

            var columns = 4;
            var rows = (int)Math.Ceiling(ShelfColorPalette.Length / (double)columns);
            var swatchSize = 20;
            var swatchGap = 6;
            var padding = 8;

            var contentWidth = (columns * swatchSize) + ((columns - 1) * swatchGap);
            var contentHeight = (rows * swatchSize) + ((rows - 1) * swatchGap);

            var popup = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                ShowInTaskbar = false,
                TopMost = true,
                AutoScaleMode = AutoScaleMode.None,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = Padding.Empty,
                Margin = Padding.Empty,
                BackColor = Color.FromArgb(35, 35, 35)
            };

            var panel = new Panel
            {
                BackColor = Color.FromArgb(35, 35, 35),
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                Size = new Size(contentWidth + (padding * 2), contentHeight + (padding * 2))
            };

            for (int i = 0; i < ShelfColorPalette.Length; i++)
            {
                var color = ShelfColorPalette[i];
                var col = i % columns;
                var row = i / columns;

                var swatch = new Panel
                {
                    BackColor = color,
                    Cursor = Cursors.Hand,
                    BorderStyle = BorderStyle.FixedSingle,
                    Size = new Size(swatchSize, swatchSize),
                    Location = new Point(
                        padding + (col * (swatchSize + swatchGap)),
                        padding + (row * (swatchSize + swatchGap)))
                };

                swatch.Click += (s, e) =>
                {
                    ApplyShelfColor(shelf, color);
                    popup.Close();
                };

                panel.Controls.Add(swatch);
            }

            popup.Controls.Add(panel);
            popup.Deactivate += (s, e) => popup.Close();
            popup.FormClosed += (s, e) =>
            {
                if (ReferenceEquals(colorPalettePopup, popup))
                {
                    colorPalettePopup.Dispose();
                    colorPalettePopup = null;
                }
            };

            popup.Location = screenPoint;

            colorPalettePopup = popup;
            popup.Show(this);
            popup.BringToFront();
        }

        private void ApplyShelfColor(ShelfInfo shelf, Color color)
        {
            shelf.ShelfColorArgb = color.ToArgb();
            ShelfManager.Instance.UpdateShelf(shelf);

            foreach (Form openForm in Application.OpenForms)
            {
                if (openForm is ShelfWindow fw && fw.ShelfId == shelf.Id)
                {
                    fw.SetShelfColor(color);
                    break;
                }
            }

            LoadShelvesList();
        }

        private Bitmap CreateColorPreview(Color color)
        {
            var bmp = new Bitmap(18, 18);
            using (var g = Graphics.FromImage(bmp))
            {
                using (var brush = new SolidBrush(color))
                using (var pen = new Pen(Color.FromArgb(180, 255, 255, 255)))
                {
                    g.FillRectangle(brush, 1, 1, 16, 16);
                    g.DrawRectangle(pen, 1, 1, 16, 16);
                }
            }

            return bmp;
        }

        private void BtnAdd_Click(object sender, EventArgs e)
        {
            string targetPath = ModernFolderBrowser.ShowDialog(this.Handle, "Select a folder to link this Shelf to");
            if (!string.IsNullOrEmpty(targetPath))
            {
                var form = new Form { Text = "Shelf Name", Size = new Size(320, 150), StartPosition = FormStartPosition.CenterParent, Font = this.Font, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, BackColor = Color.FromArgb(40, 40, 40), ForeColor = Color.White };
                StyleFormDarkTitleBar(form);
                var tbName = new TextBox { Location = new Point(20, 30), Size = new Size(260, 25), Text = Path.GetFileName(targetPath), BackColor = Color.FromArgb(60, 60, 60), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
                var btnOk = new Button { Text = "OK", Location = new Point(190, 70), Size = new Size(90, 30), DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0, 120, 215) };
                btnOk.FlatAppearance.BorderSize = 0;
                btnOk.Paint += (s, ev) => {
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

                if (form.ShowDialog() == DialogResult.OK)
                {
                    var shelfName = string.IsNullOrWhiteSpace(tbName.Text) ? "New Shelf" : tbName.Text;
                    ShelfManager.Instance.CreateShelf(shelfName, targetPath);
                    LoadShelvesList();
                }
            }
        }

        private void ShelfManager_ShelvesChanged(object sender, EventArgs e)
        {
            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(LoadShelvesList));
                return;
            }

            LoadShelvesList();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (colorPalettePopup != null)
                {
                    colorPalettePopup.Dispose();
                    colorPalettePopup = null;
                }

                ShelfManager.Instance.ShelvesChanged -= ShelfManager_ShelvesChanged;
                trayIcon?.Dispose();
                appIcon?.Dispose();
            }
            base.Dispose(disposing);
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int useImmersiveDarkMode = 1;
            try { DwmSetWindowAttribute(this.Handle, 20, ref useImmersiveDarkMode, sizeof(int)); } catch { } // Windows 11
            try { DwmSetWindowAttribute(this.Handle, 19, ref useImmersiveDarkMode, sizeof(int)); } catch { } // Windows 10
        }
    }

    public static class ModernFolderBrowser
    {
        public static string ShowDialog(IntPtr ownerHostWindow, string title)
        {
            Type type = Type.GetTypeFromCLSID(new Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7"));
            if (type == null) return null;
            object dialog = Activator.CreateInstance(type);
            try
            {
                if (dialog is IFileOpenDialog fileOpenDialog)
                {
                    fileOpenDialog.SetOptions(32); // FOS_PICKFOLDERS
                    if (!string.IsNullOrEmpty(title))
                        fileOpenDialog.SetTitle(title);

                    if (fileOpenDialog.Show(ownerHostWindow) == 0) // S_OK
                    {
                        fileOpenDialog.GetResult(out IShellItem shellItem);
                        shellItem.GetDisplayName(0x80058000, out string path); // SIGDN_FILESYSPATH
                        return path;
                    }
                }
            }
            finally
            {
                Marshal.ReleaseComObject(dialog);
            }
            return null;
        }

        [ComImport, Guid("42f85136-db7e-439c-85f1-e4075d135fc8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileOpenDialog
        {
            [PreserveSig] int Show(IntPtr parent);
            void SetFileTypes([In] uint cFileTypes, [In] IntPtr rgFilterSpec);
            void SetFileTypeIndex([In] uint iFileType);
            void GetFileTypeIndex(out uint piFileType);
            void Advise([In] IntPtr pfde, out uint pdwCookie);
            void Unadvise([In] uint dwCookie);
            void SetOptions([In] uint fos);
            void GetOptions(out uint pfos);
            void SetDefaultFolder([In] IShellItem psi);
            void SetFolder([In] IShellItem psi);
            void GetFolder(out IShellItem ppsi);
            void GetCurrentSelection(out IShellItem ppsi);
            void SetFileName([In, MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
            void SetTitle([In, MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
            void SetOkButtonLabel([In, MarshalAs(UnmanagedType.LPWStr)] string pszText);
            void SetFileNameLabel([In, MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
            void GetResult(out IShellItem ppsi);
            void AddPlace([In] IShellItem psi, uint fdap);
            void SetDefaultExtension([In, MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
            void Close([MarshalAs(UnmanagedType.Error)] int hr);
            void SetClientGuid([In] ref Guid guid);
            void ClearClientData();
            void SetFilter([MarshalAs(UnmanagedType.Interface)] IntPtr pFilter);
            void GetResults([MarshalAs(UnmanagedType.Interface)] out IntPtr ppenum);
            void GetSelectedItems([MarshalAs(UnmanagedType.Interface)] out IntPtr ppsai);
        }

        [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
            void GetParent(out IShellItem ppsi);
            void GetDisplayName(uint sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
            void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
            void Compare(IShellItem psi, uint hint, out int piOrder);
        }
    }
}