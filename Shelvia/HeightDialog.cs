using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Shelvia
{
    public partial class HeightDialog : Form
    {
        public int TitleHeight => trackBarTitleHeight.Value;
        public event Action<int> PreviewTitleHeightChanged;

        public HeightDialog(int val)
        {
            InitializeComponent();
            ApplyDarkTheme();
            trackBarTitleHeight.Value = val;
            UpdateText();
        }

        private void ApplyDarkTheme()
        {
            BackColor = Color.FromArgb(36, 36, 36);
            ForeColor = Color.White;

            labelTitleHeight.ForeColor = Color.White;

            btnOk.FlatStyle = FlatStyle.Flat;
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.BackColor = Color.FromArgb(0, 120, 215);
            btnOk.ForeColor = Color.White;

            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.BackColor = Color.FromArgb(70, 70, 70);
            btnCancel.ForeColor = Color.White;

            btnRestore.FlatStyle = FlatStyle.Flat;
            btnRestore.FlatAppearance.BorderSize = 0;
            btnRestore.BackColor = Color.FromArgb(70, 70, 70);
            btnRestore.ForeColor = Color.White;

            trackBarTitleHeight.BackColor = BackColor;
        }

        private void UpdateText()
        {
            labelTitleHeight.Text = trackBarTitleHeight.Value + "px";
        }

        private void trackBarTitleHeight_Scroll(object sender, EventArgs e)
        {
            UpdateText();
            PreviewTitleHeightChanged?.Invoke(trackBarTitleHeight.Value);
        }

        private void btnRestore_Click(object sender, EventArgs e)
        {
            trackBarTitleHeight.Value = 35;
            UpdateText();
            PreviewTitleHeightChanged?.Invoke(trackBarTitleHeight.Value);
        }

        private void HeightDialog_Load(object sender, EventArgs e)
        {

        }

        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int useImmersiveDarkMode = 1;
            try { DwmSetWindowAttribute(this.Handle, 20, ref useImmersiveDarkMode, sizeof(int)); } catch { } // Windows 11
            try { DwmSetWindowAttribute(this.Handle, 19, ref useImmersiveDarkMode, sizeof(int)); } catch { } // Windows 10
        }
    }
}
