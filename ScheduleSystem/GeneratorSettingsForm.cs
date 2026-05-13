using System;
using System.Drawing;
using System.Windows.Forms;
using ScheduleSystem.Logic;
using ScheduleSystem.Models;

namespace ScheduleSystem
{
    public partial class GeneratorSettingsForm : Form
    {
        private GeneratorSettings _settings;

        private ComboBox cmbMode;
        private ComboBox cmbSeason;
        private CheckBox chkSaturday;
        private CheckBox chkMergeStreams;

        private CheckBox chkAllowWindows;
        private CheckBox chkStrictCapacity;
        private CheckBox chkIgnoreStickiness;

        private TrackBar tbPriority;
        private TrackBar tbTeachWindow;
        private TrackBar tbStudWindow;
        private TrackBar tbLatePair;

        private Button btnStart;
        private ProgressBar progressBar;
        private Label lblStatus;

        public GeneratorSettingsForm(GeneratorSettings settings)
        {
            InitializeComponent();
            _settings = settings;
            SetupManualUI();
            LoadValues();
        }

        private void SetupManualUI()
        {
            this.Text = "Центр управления генерацией";
            this.Size = new Size(600, 750);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.Controls.Clear();

            int y = 10; int x = 20; int w = 540;

            AddHeader("Базовые параметры", x, y); y += 30;

            AddLabel("Режим:", x, y);
            cmbMode = new ComboBox { Location = new Point(x + 100, y - 3), Width = w - 100, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbMode.Items.AddRange(new object[] { "⚡ Быстрый", "⚖️ Сбалансированный", "🧠 Deep AI (Рекомендуется)" });
            this.Controls.Add(cmbMode);
            y += 40;

            AddLabel("Семестр:", x, y);
            cmbSeason = new ComboBox { Location = new Point(x + 100, y - 3), Width = w - 100, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbSeason.Items.AddRange(new object[] { "🍂 ОСЕНЬ (Нечетные)", "🌱 ВЕСНА (Четные)" });
            this.Controls.Add(cmbSeason);
            y += 40;

            chkSaturday = new CheckBox { Text = "Учебная суббота", Location = new Point(x, y), AutoSize = true };
            this.Controls.Add(chkSaturday);
            chkMergeStreams = new CheckBox { Text = "Объединять в потоки", Location = new Point(x + 200, y), AutoSize = true };
            this.Controls.Add(chkMergeStreams);
            y += 40;

            AddHeader("Правила и Допущения", x, y); y += 30;

            chkAllowWindows = new CheckBox { Text = "Разрешить окна студентам", Location = new Point(x, y), AutoSize = true, ForeColor = Color.DarkRed };
            this.Controls.Add(chkAllowWindows);
            y += 30;

            chkStrictCapacity = new CheckBox { Text = "Строгая проверка вместимости", Location = new Point(x, y), AutoSize = true };
            this.Controls.Add(chkStrictCapacity);
            y += 30;

            chkIgnoreStickiness = new CheckBox { Text = "Игнорировать 'липкость'", Location = new Point(x, y), AutoSize = true };
            this.Controls.Add(chkIgnoreStickiness);
            y += 40;

            AddHeader("Тонкая настройка весов", x, y); y += 30;

            tbPriority = AddSlider("Важность VIP-аудиторий:", x, ref y, w);
            tbTeachWindow = AddSlider("Нетерпимость преподов к окнам:", x, ref y, w);
            tbStudWindow = AddSlider("Нетерпимость студентов к окнам:", x, ref y, w);
            tbLatePair = AddSlider("Штраф за поздние пары:", x, ref y, w);

            y += 20;
            btnStart = new Button { Text = "🚀 ЗАПУСТИТЬ", Location = new Point(x, y), Size = new Size(w, 50), BackColor = Color.LightGreen, Font = new Font("Segoe UI", 11, FontStyle.Bold) };
            btnStart.Click += BtnStart_Click;
            this.Controls.Add(btnStart);
            y += 60;

            lblStatus = new Label { Text = "Готов к работе", Location = new Point(x, y), AutoSize = true };
            this.Controls.Add(lblStatus);
            progressBar = new ProgressBar { Location = new Point(x, y + 25), Size = new Size(w, 5), Visible = false };
            this.Controls.Add(progressBar);
        }

        private TrackBar AddSlider(string title, int x, ref int y, int w)
        {
            var lbl = new Label { Text = title, Location = new Point(x, y), AutoSize = true };
            this.Controls.Add(lbl);
            var tb = new TrackBar { Location = new Point(x + 250, y), Width = w - 250, Maximum = 100, TickFrequency = 10 };
            this.Controls.Add(tb);
            y += 40;
            return tb;
        }

        private void AddHeader(string text, int x, int y) { this.Controls.Add(new Label { Text = text, Location = new Point(x, y), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold | FontStyle.Underline) }); }
        private void AddLabel(string text, int x, int y) { this.Controls.Add(new Label { Text = text, Location = new Point(x, y), AutoSize = true }); }

        private void LoadValues()
        {
            cmbMode.SelectedIndex = (int)_settings.Mode;
            cmbSeason.SelectedIndex = _settings.SemesterFilter == 1 ? 0 : 1;
            chkSaturday.Checked = _settings.UseSaturday;
            chkMergeStreams.Checked = _settings.MergeStreams;
            chkAllowWindows.Checked = _settings.AllowStudentWindows;
            chkStrictCapacity.Checked = _settings.StrictRoomCapacity;
            chkIgnoreStickiness.Checked = _settings.IgnoreRoomStickiness;
            tbPriority.Value = _settings.WeightRoomPriority;
            tbTeachWindow.Value = _settings.WeightTeacherWindow;
            tbStudWindow.Value = _settings.WeightStudentWindow;
            tbLatePair.Value = _settings.LatePairPenalty;
        }

        private async void BtnStart_Click(object sender, EventArgs e)
        {
            _settings.Mode = (GenerationMode)cmbMode.SelectedIndex;
            _settings.SemesterFilter = cmbSeason.SelectedIndex == 0 ? 1 : 2;
            _settings.UseSaturday = chkSaturday.Checked;
            _settings.MergeStreams = chkMergeStreams.Checked;
            _settings.AllowStudentWindows = chkAllowWindows.Checked;
            _settings.StrictRoomCapacity = chkStrictCapacity.Checked;
            _settings.IgnoreRoomStickiness = chkIgnoreStickiness.Checked;
            _settings.WeightRoomPriority = tbPriority.Value;
            _settings.WeightTeacherWindow = tbTeachWindow.Value;
            _settings.WeightStudentWindow = tbStudWindow.Value;
            _settings.LatePairPenalty = tbLatePair.Value;

            btnStart.Enabled = false;
            progressBar.Visible = true;
            lblStatus.Text = "Работаю...";
            lblStatus.ForeColor = Color.Blue;

            try
            {
                var service = new ScheduleGeneratorService(_settings);
                await System.Threading.Tasks.Task.Run(() => service.GenerateAndSave());

                if (service.GenerationErrors.Count > 0)
                {
                    lblStatus.Text = $"Есть замечания: {service.GenerationErrors.Count}";
                    lblStatus.ForeColor = Color.OrangeRed;
                    MessageBox.Show("Готово, но есть замечания.", "Внимание", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    lblStatus.Text = "Идеально!";
                    lblStatus.ForeColor = Color.Green;
                    MessageBox.Show("Успех!", "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                this.Close();
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Ошибка";
                MessageBox.Show(ex.ToString());
            }
            finally { btnStart.Enabled = true; progressBar.Visible = false; }
        }

        private void GeneratorSettingsForm_Load(object sender, EventArgs e) { }
    }
}