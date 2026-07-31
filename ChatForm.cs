using System;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace SmartGooseAI
{
    public class ChatForm : Form
    {
        private FlowLayoutPanel flowMessages;
        private TextBox txtInput;
        private Button btnSend;
        private Label lblStatus;
        private Timer topMostTimer;
        private static readonly HttpClient client = new HttpClient();
        private Config config;

        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        const uint SWP_NOMOVE = 0x0002, SWP_NOSIZE = 0x0001, SWP_SHOWWINDOW = 0x0040;

        static readonly Color BG_DARK = Color.FromArgb(25, 25, 30);
        static readonly Color BG_PANEL = Color.FromArgb(35, 35, 42);
        static readonly Color BG_USER = Color.FromArgb(60, 100, 180);
        static readonly Color BG_BOT = Color.FromArgb(50, 50, 58);
        static readonly Color TEXT_LIGHT = Color.FromArgb(230, 230, 235);
        static readonly Color ACCENT = Color.FromArgb(100, 180, 255);

        public ChatForm()
        {
            config = ConfigManager.Load();
            KnowledgeBase.Load(Path.Combine("Assets", "Mods", "SmartGooseAI", "knowledge"));
            InitializeUI();
        }

        private void InitializeUI()
        {
            this.Text = "🦆 Nexus AI";
            this.Size = new Size(500, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ShowInTaskbar = false;
            this.BackColor = BG_DARK;
            this.Font = new Font("Segoe UI", 10f);

            var header = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = BG_PANEL };
            var lblTitle = new Label
            {
                Text = config.EnableAI ? "🔵 Nexus AI (с ИИ)" : "🟢 Nexus AI",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = ACCENT,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0)
            };
            header.Controls.Add(lblTitle);

            var panelMessages = new Panel { Dock = DockStyle.Fill, BackColor = BG_DARK, AutoScroll = true };
            flowMessages = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = BG_DARK,
                Padding = new Padding(10)
            };
            panelMessages.Controls.Add(flowMessages);

            var panelInput = new Panel { Dock = DockStyle.Bottom, Height = 80, BackColor = BG_PANEL, Padding = new Padding(8) };

            txtInput = new TextBox
            {
                Multiline = true,
                Dock = DockStyle.Fill,
                BackColor = BG_DARK,
                ForeColor = TEXT_LIGHT,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10f),
                Padding = new Padding(8, 6, 8, 6)
            };
            txtInput.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && !e.Shift) { e.SuppressKeyPress = true; BtnSend_Click(s, e); }
            };

            var btnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 36,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(4, 2, 4, 2),
                BackColor = BG_PANEL
            };

            btnSend = new Button
            {
                Text = config.EnableAI ? "🦆 Ответить" : "🔍 Найти",
                Size = new Size(100, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = ACCENT,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            btnSend.Click += BtnSend_Click;

            lblStatus = new Label
            {
                Text = $"📚 {KnowledgeBase.GetChunkCount()} фактов",
                Dock = DockStyle.Fill,
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 8f),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0)
            };

            btnPanel.Controls.Add(btnSend);
            btnPanel.Controls.Add(lblStatus);
            panelInput.Controls.Add(txtInput);
            panelInput.Controls.Add(btnPanel);

            this.Controls.Add(panelMessages);
            this.Controls.Add(panelInput);
            this.Controls.Add(header);

            topMostTimer = new Timer { Interval = 500 };
            topMostTimer.Tick += (s, e) =>
            {
                this.TopMost = true;
                SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
            };

            AddWelcomeMessage();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            this.TopMost = true;
            SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
            this.Activate();
            this.Focus();
            topMostTimer.Start();
            txtInput.Focus();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            topMostTimer?.Stop();
            topMostTimer?.Dispose();
            base.OnFormClosed(e);
        }

        private void AddWelcomeMessage()
        {
            var welcome = new Panel { AutoSize = true, MaximumSize = new Size(420, 0), Margin = new Padding(4, 4, 4, 12), Padding = new Padding(12, 10, 12, 10), BackColor = BG_BOT };
            var lbl = new Label
            {
                Text = "👋 Привет! Я Nexus.\n\nЗадай вопрос по Roblox, Minecraft или играм — я найду ответ в своей базе!",
                ForeColor = TEXT_LIGHT,
                Font = new Font("Segoe UI", 10f),
                AutoSize = true,
                MaximumSize = new Size(390, 0)
            };
            welcome.Controls.Add(lbl);
            flowMessages.Controls.Add(welcome);
        }

        private async void BtnSend_Click(object sender, EventArgs e)
        {
            var userMsg = txtInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(userMsg)) return;

            AddUserBubble(userMsg);
            txtInput.Clear();
            btnSend.Enabled = false;

            try
            {
                if (config.EnableAI)
                {
                    lblStatus.Text = "🦆 Думаю...";
                    var response = await SendToOllama(userMsg);
                    AddBotBubble(response);
                    lblStatus.Text = "✅";
                }
                else
                {
                    lblStatus.Text = "🔍 Ищу...";
                    var response = KnowledgeBase.ProcessQuery(userMsg, topK: 1);

                    if (response.IsGrounded && !string.IsNullOrEmpty(response.Context))
                    {
                        AddBotBubble($"🦆 {response.Context}");
                        lblStatus.Text = "✅ Найдено";
                    }
                    else
                    {
                        AddBotBubble("😕 Не нашёл ответа. Попробуй переформулировать вопрос!");
                        lblStatus.Text = "⚠️";
                    }
                }
            }
            catch (Exception ex)
            {
                AddBotBubble($"❌ Ошибка: {ex.Message}");
                lblStatus.Text = "❌";
            }
            finally
            {
                btnSend.Enabled = true;
                txtInput.Focus();
            }
        }

        private async Task<string> SendToOllama(string prompt)
        {
            var ragResponse = KnowledgeBase.ProcessQuery(prompt, topK: 1);
            string systemPrompt = config.SystemPrompt;
            if (ragResponse.IsGrounded && !string.IsNullOrEmpty(ragResponse.Context))
                systemPrompt += $"\n\nКонтекст: {ragResponse.Context}";

            var payload = new { model = config.Model, prompt = prompt, stream = false, system = systemPrompt };
            var json = JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(config.OllamaUrl, content);
            response.EnsureSuccessStatusCode();
            var responseBody = await response.Content.ReadAsStringAsync();
            dynamic result = JsonConvert.DeserializeObject(responseBody);
            return result.response != null ? (string)result.response : "Пустой ответ.";
        }

        private void AddUserBubble(string text)
        {
            var bubble = CreateBubble(BG_USER, $"👤 {text}");
            flowMessages.Controls.Add(bubble);
            flowMessages.ScrollControlIntoView(bubble);
        }

        private void AddBotBubble(string text)
        {
            var bubble = CreateBubble(BG_BOT, text);
            flowMessages.Controls.Add(bubble);
            flowMessages.ScrollControlIntoView(bubble);
        }

        private Panel CreateBubble(Color bgColor, string text)
        {
            var bubble = new Panel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MaximumSize = new Size(420, 0),
                Margin = new Padding(4, 4, 4, 6),
                Padding = new Padding(10, 8, 10, 8),
                BackColor = bgColor
            };
            var lbl = new Label { Text = text, ForeColor = TEXT_LIGHT, Font = new Font("Segoe UI", 10f), AutoSize = true, MaximumSize = new Size(390, 0) };
            bubble.Controls.Add(lbl);
            return bubble;
        }
    }
}
