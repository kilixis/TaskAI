using System;
using System.Drawing;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using DotNetEnv;

namespace TaskAI
{
    public class MainForm : Form
    {
        private TextBox inputBox;
        private Button sendButton;
        private readonly HttpClient httpClient = new HttpClient();
        private readonly string ApiKey;
        private const string Endpoint = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";

        public MainForm()
        {
            DotNetEnv.Env.Load();
            ApiKey = Environment.GetEnvironmentVariable("ApiKey");
            InitializeUI();
        }

        private void InitializeUI()
        {
            this.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            this.TopMost = true;
            this.StartPosition = FormStartPosition.Manual;
            int formWidth = 500;
            int formHeight = 100;
            Rectangle screen = Screen.PrimaryScreen.WorkingArea;
            int x = (screen.Width - formWidth) / 2;
            int y = screen.Height - formHeight - 30;
            this.Size = new Size(formWidth, formHeight);
            this.Location = new Point(x, y);

            inputBox = new TextBox
            {
                Width = 400,
                Location = new Point(10, 35),
                Font = new Font("Segoe UI", 10),
            };
            inputBox.KeyDown += InputBox_KeyDown;

            sendButton = new Button
            {
                Text = "Send",
                Location = new Point(420, 33),
                Width = 60,
                Height = 30
            };
            sendButton.Click += async (s, e) => await ProcessInput();

            this.Controls.Add(inputBox);
            this.Controls.Add(sendButton);
        }

        private void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                _ = ProcessInput();
            }
        }

        private async Task ProcessInput()
        {
            string userInput = inputBox.Text.Trim();
            if (!string.IsNullOrEmpty(userInput))
            {
                inputBox.Clear();

                try
                {
                    string response = await AskGemini(userInput);
                    string message = ParseGeminiResponse(response);
                    MessageBox.Show(message, "Gemini AI Response");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: " + ex.Message);
                }
            }
        }

        private async Task<string> AskGemini(string prompt)
        {
            var payload = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                }
            };

            var requestUri = $"{Endpoint}?key={ApiKey}";
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(requestUri, content);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }

        private string ParseGeminiResponse(string json)
        {
            try
            {
                var doc = JsonDocument.Parse(json);
                var text = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                return text ?? "No response text found.";
            }
            catch
            {
                return "Error parsing Gemini response.";
            }
        }
    }
}
