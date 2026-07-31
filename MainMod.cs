using GooseShared;
using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace SmartGooseAI
{
    public class MainMod : IMod
    {
        private Thread uiThread;
        private TrayContext uiContext;

        public void Init()
        {
            string logPath = Path.Combine("Assets", "Mods", "SmartGooseAI", "error.log");
            File.AppendAllText(logPath, $"[{DateTime.Now}] Init() вызван. Поток STA: {Thread.CurrentThread.GetApartmentState()}\n");

            uiThread = new Thread(() =>
            {
                try
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);

                    // Создаем невидимую форму-хост для полноценного Message Pump WinForms
                    var hostForm = new Form
                    {
                        WindowState = FormWindowState.Minimized,
                        ShowInTaskbar = false,
                        Opacity = 0,
                        FormBorderStyle = FormBorderStyle.None,
                        Size = new System.Drawing.Size(1, 1)
                    };

                    uiContext = new TrayContext();

                    // Запускаем цикл сообщений WinForms на этой невидимой форме
                    Application.Run(hostForm);
                }
                catch (Exception ex)
                {
                    try { File.AppendAllText(logPath, $"UI Exception: {ex}\n"); } catch { }
                }
            });

            uiThread.SetApartmentState(ApartmentState.STA);
            uiThread.IsBackground = true;
            uiThread.Start();
        }
    }
}
