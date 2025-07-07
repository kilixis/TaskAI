using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
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
                    string parsed = ParseGeminiResponse(response);

                    if (parsed.StartsWith("KILL:"))
                    {
                        HandleKillCommand(parsed.Substring(5));
                    }
                    else
                    {
                        MessageBox.Show(parsed, "Gemini AI Response");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: " + ex.Message);
                }
            }
        }

        private async Task<string> AskGemini(string prompt)
        {
            // get list of rprocesses
            var runningProcesses = GetRunningProcessesList();
            
            var payload = new
            {
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new[]
                        {
                            new { text = "You are a Windows task assistant with access to the following running processes:\n\n" + runningProcesses + "\n\nWhen users ask about specific processes or want to close/kill applications, ONLY include processes from the above list in your response. Return a JSON object using this format:\n\njson\n{\n  \"kill\": [\"ProcessName1\", \"ProcessName2\"]\n}\n\n\nIf the user specifically asks for a process by name, ONLY include that process if it's in the list. Don't suggest killing processes the user is actively using (like browsers) unless specifically requested. For questions completely unrelated to processes, provide a helpful response as normal." }
                        }
                    },
                    new
                    {
                        role = "user",
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

        private string GetRunningProcessesList()
        {
            try
            {
                var processes = Process.GetProcesses()
                    .Where(p => {
                        try { return !string.IsNullOrEmpty(p.ProcessName); }
                        catch { return false; }
                    })
                    .GroupBy(p => p.ProcessName)
                    .Select(g => new {
                        Name = g.Key,
                        Count = g.Count(),
                        Memory = g.Sum(p => {
                            try { return p.WorkingSet64; }
                            catch { return 0L; }
                        })
                    })
                    .OrderByDescending(p => p.Memory)
                    .Take(50) // Limit rn=50 by memory usage
                    .ToList();

                var sb = new StringBuilder();
                foreach (var proc in processes)
                {
                    string memoryUsage = FormatBytes(proc.Memory);
                    sb.AppendLine($"{proc.Name} ({proc.Count} instances, {memoryUsage})");
                }
                
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return "Error getting process list: " + ex.Message;
            }
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

                if (string.IsNullOrWhiteSpace(text))
                    return "No response text found.";

                string cleaned = text.Trim()
                    .Replace("```json", "")
                    .Replace("```", "")
                    .Trim();

                if (cleaned.StartsWith("{") && cleaned.Contains("\"kill\""))
                    return "KILL:" + cleaned;

                return text;
            }
            catch
            {
                return "Error parsing Gemini response.";
            }
        }

        private void HandleKillCommand(string json)
        {
            try
            {
                var doc = JsonDocument.Parse(json);
                var killArray = doc.RootElement.GetProperty("kill");
                var requested = killArray.EnumerateArray().Select(p => p.GetString()).Where(p => !string.IsNullOrEmpty(p)).ToList();

                var protectedKeywords = new[]
                {
                    "system", "winlogon", "csrss", "dwm", "taskmgr", "explorer",
                    "svchost", "spoolsv", "lsass", "services", "wininit", "smss",
                    "conhost", "msmpeng", "securityhealthservice", "applicationframehost",
                    "runtimebroker", "searchui", "startmenuexperiencehost", "sihost", "ctfmon",
                    "windows", "microsoft", "dllhost", "fontdrvhost",
                    "hp", "dell", "lenovo", "intel", "OneDrive"
                };

                var approved = requested
                    .Where(procName =>
                        !protectedKeywords.Any(protectedName =>
                            procName.IndexOf(protectedName, StringComparison.OrdinalIgnoreCase) >= 0))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (approved.Count == 0)
                {
                    MessageBox.Show("No safe processes found to kill.", "Protected Process Filter");
                    return;
                }

                // match name
                var runningProcesses = Process.GetProcesses()
                    .Where(p => {
                        try { return !string.IsNullOrEmpty(p.ProcessName); }
                        catch { return false; }
                    })
                    .ToList();
                
                // find match which is running
                var matchingProcesses = new List<Process>();
                foreach (var procName in approved)
                {
                    var matches = runningProcesses.Where(p => 
                        p.ProcessName.Equals(procName, StringComparison.OrdinalIgnoreCase) ||
                        p.ProcessName.Contains(procName, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    
                    matchingProcesses.AddRange(matches);
                }
                
                if (matchingProcesses.Count == 0)
                {
                    MessageBox.Show("No matching processes found running.", "No Processes Found");
                    return;
                }

                // get resource info before maut
                var processInfo = new Dictionary<string, (long memory, TimeSpan cpu)>();
                foreach (var proc in matchingProcesses)
                {
                    try
                    {
                        string procName = proc.ProcessName;
                        long memoryUsage = proc.WorkingSet64;
                        TimeSpan cpuTime = proc.TotalProcessorTime;
                        
                        processInfo[proc.Id.ToString()] = (memoryUsage, cpuTime);
                    }
                    catch { /* ss*/ }
                }

                string preview = "Gemini suggests killing the following processes:\n\n" + 
                    string.Join("\n", matchingProcesses.Select(p => p.ProcessName).Distinct());
                var confirm = MessageBox.Show(preview, "Confirm Kill", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                if (confirm == DialogResult.Yes)
                {
                    int killed = 0;
                    long totalMemoryFreed = 0;
                    
                    foreach (var proc in matchingProcesses)
                    {
                        try
                        {
                            string procId = proc.Id.ToString();
                            if (processInfo.ContainsKey(procId))
                            {
                                totalMemoryFreed += processInfo[procId].memory;
                            }
                            
                            proc.Kill();
                            killed++;
                        }
                        catch { /* skip ts */ }
                    }

                    string memoryMessage = totalMemoryFreed > 0 
                        ? $"\nMemory freed: {FormatBytes(totalMemoryFreed)}"
                        : "";
                        
                    MessageBox.Show($"Killed {killed} process(es).{memoryMessage}", "Done");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error handling kill command: " + ex.Message);
            }
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double formattedSize = bytes;
            int order = 0;
            
            while (formattedSize >= 1024 && order < sizes.Length - 1)
            {
                order++;
                formattedSize /= 1024;
            }

            return $"{formattedSize:0.##} {sizes[order]}";
        }
    }
}