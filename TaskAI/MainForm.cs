using System;
using System.Drawing;
using System.Windows.Forms;

namespace TaskAI
{
    public class MainForm : Form
    {
        TextBox inputBox;
        Button sendButton;

        public MainForm()
        {
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
                Location = new Point(10, 15),
                Font = new Font("Segoe UI", 10),
            };
            inputBox.KeyDown += InputBox_KeyDown;

            sendButton = new Button
            {
                Text = "Send",
                Location = new Point(420, 13),
                Width = 60,
                Height = 30
            };
            sendButton.Click += SendButton_Click;

            this.Controls.Add(inputBox);
            this.Controls.Add(sendButton);
        }

        private void SendButton_Click(object sender, EventArgs e)
        {
            ProcessInput();
        }

        private void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                ProcessInput();
            }
        }

        private void ProcessInput()
        {
            string userInput = inputBox.Text.Trim();
            if (!string.IsNullOrEmpty(userInput))
            {
                MessageBox.Show("You entered: " + userInput, "AI Command");
                inputBox.Clear();
            }
        }
    }
}
