using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ScheduleSystem.Data;
using ScheduleSystem.Logic;
using ScheduleSystem.Models;

namespace ScheduleSystem
{
    public partial class AddLessonForm : Form
    {
        // Данные для возврата
        public string SelectedSubject { get; private set; }
        public string SelectedTeacher { get; private set; }
        public string SelectedRoom { get; private set; }
        public int SelectedType { get; private set; }
        public bool IsSaved { get; private set; } = false;

        // Контекст
        private string _groupName;
        private string _dayName;
        private int _pairNumber;

        // UI Элементы
        private ComboBox cmbSubject;
        private ComboBox cmbTeacher;
        private ComboBox cmbRoom;
        private ComboBox cmbType;
        private Label lblValidationStatus;

        // Данные
        private List<AcademicPlanItem> _groupPlan;
        private List<RoomInfo> _allRooms;
        private List<ScheduleItem> _currentSchedule;

        public AddLessonForm(string groupName, string day, int pair, string weekType,
                             string currentSubj = "", string currentTeach = "", string currentRoom = "")
        {
            _groupName = groupName;
            _dayName = day;
            _pairNumber = pair;

            InitializeComponent();

            this.Text = $"Редактор: {groupName} | {day}, пара {pair}";
            this.Size = new Size(500, 450);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            LoadContextData();
            SetupUI(currentSubj, currentTeach, currentRoom);
        }

        private void LoadContextData()
        {
            _groupPlan = DbHelper.GetFullAcademicPlan().Where(p => p.GroupName == _groupName).ToList();
            _allRooms = DbHelper.GetAllRooms();
            _currentSchedule = new List<ScheduleItem>();
        }

        private void SetupUI(string curSubj, string curTeach, string curRoom)
        {
            int y = 20; int labelW = 100; int fieldW = 300; int x = 20;

            // Предмет
            AddLabel("Предмет:", x, y, labelW);
            cmbSubject = new ComboBox { Location = new Point(x + labelW, y), Width = fieldW, DropDownStyle = ComboBoxStyle.DropDownList };
            var subjects = _groupPlan.Select(p => p.SubjectName).Distinct().ToArray();
            cmbSubject.Items.AddRange(subjects);
            if (!string.IsNullOrEmpty(curSubj) && subjects.Contains(curSubj)) cmbSubject.SelectedItem = curSubj;
            cmbSubject.SelectedIndexChanged += CmbSubject_Changed;
            this.Controls.Add(cmbSubject);
            y += 40;

            // Тип
            AddLabel("Тип:", x, y, labelW);
            cmbType = new ComboBox { Location = new Point(x + labelW, y), Width = fieldW, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbType.Items.AddRange(new object[] { "Лекция", "Практика", "Лабораторная" });
            cmbType.SelectedIndex = 0;
            this.Controls.Add(cmbType);
            y += 40;

            // Преподаватель
            AddLabel("Преподаватель:", x, y, labelW);
            cmbTeacher = new ComboBox { Location = new Point(x + labelW, y), Width = fieldW, DropDownStyle = ComboBoxStyle.DropDown };
            // Загрузка учителей
            var teachers = DbHelper.GetTeachers().Select(t => t.FullName).OrderBy(t => t).ToArray();
            cmbTeacher.Items.AddRange(teachers);
            if (!string.IsNullOrEmpty(curTeach)) cmbTeacher.Text = curTeach;
            this.Controls.Add(cmbTeacher);
            y += 40;

            // Аудитория
            AddLabel("Аудитория:", x, y, labelW);
            cmbRoom = new ComboBox { Location = new Point(x + labelW, y), Width = fieldW, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbRoom.Items.AddRange(_allRooms.Select(r => r.Number).ToArray());
            if (!string.IsNullOrEmpty(curRoom)) cmbRoom.SelectedItem = curRoom;
            this.Controls.Add(cmbRoom);
            y += 50;

            // Статус
            var grpStatus = new GroupBox { Text = "Статус", Location = new Point(x, y), Size = new Size(400, 80) };
            lblValidationStatus = new Label { Text = "Ручное редактирование", Dock = DockStyle.Fill, ForeColor = Color.Gray };
            grpStatus.Controls.Add(lblValidationStatus);
            this.Controls.Add(grpStatus);
            y += 100;

            // Кнопки
            var btnSave = new Button { Text = "Сохранить", Location = new Point(x + labelW, y), Width = 100, BackColor = Color.LightGreen };
            btnSave.Click += BtnSave_Click;
            this.Controls.Add(btnSave);

            var btnCancel = new Button { Text = "Отмена", Location = new Point(x + labelW + 110, y), Width = 100 };
            btnCancel.Click += (s, e) => this.Close();
            this.Controls.Add(btnCancel);
        }

        private void AddLabel(string text, int x, int y, int w)
        {
            this.Controls.Add(new Label { Text = text, Location = new Point(x, y), Width = w });
        }

        private void CmbSubject_Changed(object sender, EventArgs e)
        {
            if (cmbSubject.SelectedItem == null) return;
            string subj = cmbSubject.SelectedItem.ToString();
            var plan = _groupPlan.FirstOrDefault(p => p.SubjectName == subj);

            if (plan != null)
            {
                if (plan.LectureInWeek > 0)
                {
                    cmbType.SelectedIndex = 0;
                    cmbTeacher.Text = plan.TeacherLecture;
                }
                else
                {
                    cmbType.SelectedIndex = 1;
                    cmbTeacher.Text = plan.TeacherPractice;
                }
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(cmbSubject.Text)) { MessageBox.Show("Выберите предмет!"); return; }

            SelectedSubject = cmbSubject.Text;
            SelectedTeacher = cmbTeacher.Text;
            SelectedRoom = cmbRoom.Text;
            SelectedType = cmbType.SelectedIndex;
            IsSaved = true;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        // === ВОТ ЭТОТ МЕТОД Я ДОБАВИЛ, ЧТОБЫ УБРАТЬ ОШИБКУ ===
        private void AddLessonForm_Load(object sender, EventArgs e)
        {
            // Метод нужен для дизайнера
        }
    }
}