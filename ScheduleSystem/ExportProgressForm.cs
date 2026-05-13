using System;
using System.Windows.Forms;
using System.Drawing;

namespace ScheduleSystem
{
    public partial class ExportProgressForm : Form
    {
        private ProgressBar _progressBar;
        private Label _lblStatus;
        private Label _lblTitle;

        public ExportProgressForm()
        {
            InitializeComponent(); // Обязательный метод для работы дизайнера VS
            SetupManualUI();       // Наша ручная настройка интерфейса
        }

        // --- ТОТ САМЫЙ МЕТОД, КОТОРОГО НЕ ХВАТАЛО ---
        private void ExportProgressForm_Load(object sender, EventArgs e)
        {
            // Оставляем пустым. Он нужен, чтобы Visual Studio не выдавала ошибку.
        }
        // ---------------------------------------------

        private void SetupManualUI()
        {
            // Настройка свойств формы
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.ControlBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Size = new Size(400, 150);
            this.Text = "Экспорт в Excel";
            this.BackColor = Color.White;

            // Проверяем, не созданы ли элементы дизайнером, чтобы не дублировать
            if (_lblTitle == null)
            {
                _lblTitle = new Label
                {
                    Text = "Экспорт расписания...",
                    Location = new Point(20, 20),
                    AutoSize = true,
                    Font = new Font("Segoe UI", 12, FontStyle.Bold)
                };
                this.Controls.Add(_lblTitle);
            }

            if (_lblStatus == null)
            {
                _lblStatus = new Label
                {
                    Text = "Подготовка...",
                    Location = new Point(20, 55),
                    Size = new Size(360, 20),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Font = new Font("Segoe UI", 9, FontStyle.Regular),
                    ForeColor = Color.Gray
                };
                this.Controls.Add(_lblStatus);
            }

            if (_progressBar == null)
            {
                _progressBar = new ProgressBar
                {
                    Location = new Point(20, 80),
                    Size = new Size(345, 25),
                    Style = ProgressBarStyle.Continuous,
                    Maximum = 100
                };
                this.Controls.Add(_progressBar);
            }
        }

        public void UpdateProgress(int percent, string message)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => UpdateProgress(percent, message)));
            }
            else
            {
                // Проверки на null нужны на случай, если форма закрылась раньше времени
                if (_progressBar != null && percent >= 0)
                    _progressBar.Value = Math.Min(100, percent);

                if (_lblStatus != null && !string.IsNullOrEmpty(message))
                    _lblStatus.Text = message;
            }
        }
    }
}