using System;
using System.IO;
using System.Windows.Forms;

namespace SmartGooseAI
{
    public class TrayContext : ApplicationContext
    {
        private NotifyIcon trayIcon;
        private ChatForm chatForm;
        private Form hiddenHost; // Невидимая форма-хост для цикла сообщений

        public TrayContext()
        {
            ConfigManager.Load();
            Log("TrayContext создан.");

            // 1. Создаём невидимую форму-хост (страховка для Message Pump)
            hiddenHost = new Form
            {
                WindowState = FormWindowState.Minimized,
                ShowInTaskbar = false,
                Opacity = 0,
                FormBorderStyle = FormBorderStyle.None,
                Size = new System.Drawing.Size(1, 1)
            };

            // 2. Создаём NotifyIcon
            trayIcon = new NotifyIcon
            {
                Icon = System.Drawing.SystemIcons.Information,
                Text = "Nexus AI\nДважды кликни, чтобы открыть чат",
                Visible = true
            };

            trayIcon.DoubleClick += (s, e) =>
            {
                Log("DoubleClick зарегистрирован!");
                ShowChat();
            };

            // 3. Показываем хост, чтобы привязать его к циклу сообщений Application.Run
            hiddenHost.Show();
        }

        private void ShowChat()
        {
            Log("Вызов ShowChat()");
            try
            {
                if (chatForm == null || chatForm.IsDisposed)
                {
                    chatForm = new ChatForm();
                }

                // Показываем окно поверх Unity
                chatForm.TopMost = true;
                chatForm.StartPosition = FormStartPosition.CenterScreen;
                chatForm.Show();
                chatForm.BringToFront();
                chatForm.Activate();

                Log($"Форма создана. Handle: {chatForm.Handle}, Visible: {chatForm.Visible}");
            }
            catch (Exception ex)
            {
                Log($"Ошибка ShowChat: {ex.Message}");
            }
        }

        private void Log(string msg)
        {
            try
            {
                string logPath = Path.Combine("Assets", "Mods", "SmartGooseAI", "error.log");
                File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] {msg}\n");
            }
            catch { }
        }

        public void Exit()
        {
            trayIcon.Visible = false;
            trayIcon.Dispose();
            hiddenHost?.Close(); // Закрываем хост-форму
            Application.ExitThread();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (trayIcon != null) { trayIcon.Dispose(); }
                if (hiddenHost != null) { hiddenHost.Dispose(); }
            }
            base.Dispose(disposing);
        }
    }
}
