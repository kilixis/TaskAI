using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using DotNetEnv;

namespace TaskAI
{
    public class MainForm : Form
    {
        private readonly HttpClient httpClient = new HttpClient();
        private readonly string ApiKey;
        private TextBox inputBox;
        private Button searchButton;
        private Button powerButton;
        private readonly int borderRadius = 20;

        private const string Endpoint = "https://openrouter.ai/api/v1/chat/completions";

        public MainForm()
        {
            DotNetEnv.Env.Load();
            ApiKey = Environment.GetEnvironmentVariable("ApiKey");

            this.FormBorderStyle = FormBorderStyle.None;
            this.DoubleBuffered = true;
            this.BackColor = Color.Black;
            this.TopMost = true;
            this.StartPosition = FormStartPosition.Manual;
            this.Size = new Size(500, 80);

            Rectangle screen = Screen.PrimaryScreen.WorkingArea;
            int x = (screen.Width - this.Width) / 2;
            int y = screen.Height - this.Height - 30;
            this.Location = new Point(x, y);

            CreateRoundedRegion();
            InitializeUI();
        }

        private void CreateRoundedRegion()
        {
            GraphicsPath path = new GraphicsPath();
            int r = borderRadius;
            path.StartFigure();
            path.AddArc(new Rectangle(0, 0, r, r), 180, 90);
            path.AddArc(new Rectangle(this.Width - r, 0, r, r), 270, 90);
            path.AddArc(new Rectangle(this.Width - r, this.Height - r, r, r), 0, 90);
            path.AddArc(new Rectangle(0, this.Height - r, r, r), 90, 90);
            path.CloseFigure();
            this.Region = new Region(path);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            int thickness = 4;
            var rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);

            using (var brush = new LinearGradientBrush(rect, Color.Red, Color.Violet, LinearGradientMode.Horizontal))
            using (var pen = new Pen(brush, thickness))
            {
                pen.Alignment = PenAlignment.Inset;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                using (GraphicsPath path = GetRoundedPath(rect, borderRadius))
                {
                    e.Graphics.DrawPath(pen, path);
                }
            }
        }

        private GraphicsPath GetRoundedPath(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            var path = new GraphicsPath();

            path.StartFigure();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();

            return path;
        }

        private void InitializeUI()
        {
            inputBox = new TextBox
            {
                Width = 300,
                Height = 30,
                Location = new Point(20, 25),
                Font = new Font("Segoe UI", 10),
                BackColor = Color.Black,
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            inputBox.KeyDown += InputBox_KeyDown;

            searchButton = new Button
            {
                Text = "🔍",
                Width = 40,
                Height = 30,
                Location = new Point(330, 25),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Black,
                ForeColor = Color.White
            };
            searchButton.FlatAppearance.BorderSize = 0;
            searchButton.Click += async (s, e) => await ProcessInput();

            powerButton = new Button
            {
                Text = "⏻",
                Width = 40,
                Height = 30,
                Location = new Point(380, 25),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Black,
                ForeColor = Color.Red
            };
            powerButton.FlatAppearance.BorderSize = 0;
            powerButton.Click += (s, e) => this.Close();

            this.Controls.Add(inputBox);
            this.Controls.Add(searchButton);
            this.Controls.Add(powerButton);
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
                    string response = await AskMercury(userInput);
                    string message = ParseMercuryResponse(response);
                    MessageBox.Show(message, "Mercury AI Response");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: " + ex.Message);
                }
            }
        }

        private async Task<string> AskMercury(string prompt)
        {
            var payload = new
            {
                model = "inception/mercury",
                messages = new[]
                {
                    new { role = "system", content = "You are a helpful assistant." },
                    new { role = "user", content = prompt }
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
            httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://yourapp.com");
            httpClient.DefaultRequestHeaders.Add("X-Title", "TaskAI");

            var response = await httpClient.PostAsync(Endpoint, content);
            string raw = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"API Error {response.StatusCode}: {raw}");
            }

            return raw;
        }

        private string ParseMercuryResponse(string json)
        {
            try
            {
                var doc = JsonDocument.Parse(json);
                return doc.RootElement
                          .GetProperty("choices")[0]
                          .GetProperty("message")
                          .GetProperty("content")
                          .GetString() ?? "No response text found.";
            }
            catch
            {
                return "Error parsing Mercury response.";
            }
        }
    }
}
