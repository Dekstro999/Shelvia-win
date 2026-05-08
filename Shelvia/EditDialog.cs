using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace Shelvia
{
    public partial class EditDialog : Form
    {
        public EditDialog(string oldName)
        {
            InitializeComponent();
            ApplyDarkTheme();
            tbName.Text = oldName;
            tbName.SelectAll();
        }

        public string NewName => tbName.Text;

        private void btnOk_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }

        private void ApplyDarkTheme()
        {
            BackColor = Color.FromArgb(36, 36, 36);
            ForeColor = Color.White;

            lbName.ForeColor = Color.White;

            tbName.BackColor = Color.FromArgb(56, 56, 56);
            tbName.ForeColor = Color.White;
            tbName.BorderStyle = BorderStyle.FixedSingle;

            btnOk.DialogResult = DialogResult.OK;
            btnOk.FlatStyle = FlatStyle.Flat;
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.BackColor = Color.FromArgb(0, 120, 215);
            btnOk.ForeColor = Color.White;

            btnCancel.DialogResult = DialogResult.Cancel;
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.BackColor = Color.FromArgb(70, 70, 70);
            btnCancel.ForeColor = Color.White;
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int useImmersiveDarkMode = 1;
            try { DwmSetWindowAttribute(this.Handle, 20, ref useImmersiveDarkMode, sizeof(int)); } catch { }
            try { DwmSetWindowAttribute(this.Handle, 19, ref useImmersiveDarkMode, sizeof(int)); } catch { }
        }
    }
}
