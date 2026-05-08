namespace Shelvia
{
    partial class ShelfWindow
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ShelfWindow));
            this.appContextMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.deleteItemToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.lockedToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.minifyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openFolderToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.renameToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.titleSizeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.newShelfToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.appContextMenu.SuspendLayout();
            this.SuspendLayout();
            // 
            // appContextMenu
            // 
            resources.ApplyResources(this.appContextMenu, "appContextMenu");
            this.appContextMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.deleteItemToolStripMenuItem,
            this.lockedToolStripMenuItem,
            this.minifyToolStripMenuItem,
            this.openFolderToolStripMenuItem,
            this.renameToolStripMenuItem,
            this.titleSizeToolStripMenuItem,
            this.toolStripSeparator1,
            this.newShelfToolStripMenuItem,
            this.exitToolStripMenuItem});
            this.appContextMenu.Name = "contextMenuStrip1";
            this.appContextMenu.RenderMode = System.Windows.Forms.ToolStripRenderMode.Professional;
            this.appContextMenu.Opening += new System.ComponentModel.CancelEventHandler(this.contextMenuStrip1_Opening);
            // 
            // deleteItemToolStripMenuItem
            // 
            resources.ApplyResources(this.deleteItemToolStripMenuItem, "deleteItemToolStripMenuItem");
            this.deleteItemToolStripMenuItem.Name = "deleteItemToolStripMenuItem";
            this.deleteItemToolStripMenuItem.Click += new System.EventHandler(this.deleteItemToolStripMenuItem_Click);
            // 
            // lockedToolStripMenuItem
            // 
            resources.ApplyResources(this.lockedToolStripMenuItem, "lockedToolStripMenuItem");
            this.lockedToolStripMenuItem.CheckOnClick = true;
            this.lockedToolStripMenuItem.Name = "lockedToolStripMenuItem";
            this.lockedToolStripMenuItem.Click += new System.EventHandler(this.lockedToolStripMenuItem_Click);
            // 
            // minifyToolStripMenuItem
            // 
            resources.ApplyResources(this.minifyToolStripMenuItem, "minifyToolStripMenuItem");
            this.minifyToolStripMenuItem.CheckOnClick = true;
            this.minifyToolStripMenuItem.Name = "minifyToolStripMenuItem";
            this.minifyToolStripMenuItem.Click += new System.EventHandler(this.minifyToolStripMenuItem_Click);
            // 
            // openFolderToolStripMenuItem
            // 
            resources.ApplyResources(this.openFolderToolStripMenuItem, "openFolderToolStripMenuItem");
            this.openFolderToolStripMenuItem.Text = "Open folder";
            this.openFolderToolStripMenuItem.Name = "openFolderToolStripMenuItem";
            this.openFolderToolStripMenuItem.Click += new System.EventHandler(this.openFolderToolStripMenuItem_Click);
            // 
            // renameToolStripMenuItem
            // 
            resources.ApplyResources(this.renameToolStripMenuItem, "renameToolStripMenuItem");
            this.renameToolStripMenuItem.Name = "renameToolStripMenuItem";
            this.renameToolStripMenuItem.Click += new System.EventHandler(this.renameToolStripMenuItem_Click);
            // 
            // titleSizeToolStripMenuItem
            // 
            resources.ApplyResources(this.titleSizeToolStripMenuItem, "titleSizeToolStripMenuItem");
            this.titleSizeToolStripMenuItem.Name = "titleSizeToolStripMenuItem";
            this.titleSizeToolStripMenuItem.Click += new System.EventHandler(this.titleSizeToolStripMenuItem_Click);
            // 
            // toolStripSeparator1
            // 
            resources.ApplyResources(this.toolStripSeparator1, "toolStripSeparator1");
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            // 
            // newShelfToolStripMenuItem
            // 
            resources.ApplyResources(this.newShelfToolStripMenuItem, "newShelfToolStripMenuItem");
            this.newShelfToolStripMenuItem.Name = "newShelfToolStripMenuItem";
            this.newShelfToolStripMenuItem.Click += new System.EventHandler(this.newShelfToolStripMenuItem_Click);
            // 
            // exitToolStripMenuItem
            // 
            resources.ApplyResources(this.exitToolStripMenuItem, "exitToolStripMenuItem");
            this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            this.exitToolStripMenuItem.Click += new System.EventHandler(this.exitToolStripMenuItem_Click);
            // 
            // ShelfWindow
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Black;
            this.DoubleBuffered = true;
            this.ForeColor = System.Drawing.SystemColors.ControlText;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.MinimizeBox = false;
            this.Name = "ShelfWindow";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.ShelfWindow_FormClosed);
            this.Load += new System.EventHandler(this.ShelfWindow_Load);
            this.LocationChanged += new System.EventHandler(this.ShelfWindow_LocationChanged);
            this.Click += new System.EventHandler(this.ShelfWindow_Click);
            this.DragDrop += new System.Windows.Forms.DragEventHandler(this.ShelfWindow_DragDrop);
            this.DragEnter += new System.Windows.Forms.DragEventHandler(this.ShelfWindow_DragEnter);
            this.Paint += new System.Windows.Forms.PaintEventHandler(this.ShelfWindow_Paint);
            this.DoubleClick += new System.EventHandler(this.ShelfWindow_DoubleClick);
            this.MouseClick += new System.Windows.Forms.MouseEventHandler(this.ShelfWindow_MouseClick);
            this.MouseEnter += new System.EventHandler(this.ShelfWindow_MouseEnter);
            this.MouseLeave += new System.EventHandler(this.ShelfWindow_MouseLeave);
            this.MouseMove += new System.Windows.Forms.MouseEventHandler(this.ShelfWindow_MouseMove);
            this.Resize += new System.EventHandler(this.ShelfWindow_Resize);
            this.appContextMenu.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ContextMenuStrip appContextMenu;
        private System.Windows.Forms.ToolStripMenuItem lockedToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem minifyToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openFolderToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem renameToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem deleteItemToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem newShelfToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem titleSizeToolStripMenuItem;
    }
}

