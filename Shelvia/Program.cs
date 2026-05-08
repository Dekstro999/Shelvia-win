using Shelvia.Model;
using System;
using System.Threading;
using System.Windows.Forms;
using Shelvia.Win32;

namespace Shelvia
{
    static class Program
    {
       
        [STAThread]
        static void Main()
        {
            //allows the context menu to be in dark mode
            //inherits from the system settings
            WindowUtil.SetPreferredAppMode(1);

            using (var mutex = new Mutex(true, "Shelvia", out var createdNew))
            {
                if (createdNew)
                {
                    Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);

                    ShelfManager.Instance.LoadShelves();
                    
                    var adminForm = new AdminForm();
                    adminForm.Show();

                    Application.Run();
                }
            }
        }

    }
}
