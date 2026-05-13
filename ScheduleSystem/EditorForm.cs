using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using ScheduleSystem.Data;
using ScheduleSystem.Models;

namespace ScheduleSystem
{
    public partial class EditorForm : Form
    {
        private List<Group> _groups;

        // Массив таблиц (по одной на каждый день недели: 0=Пн, 5=Сб)
        private DataGridView[] _dailyGrids = new DataGridView[6];

        public EditorForm()
        {
            InitializeComponent();

            // Настройка вкладок дней недели
            SetupTabs();

            // Загрузка групп при старте
            Load += EditorForm_Load;

            // При смене группы будем очищать/загружать расписание
            comboGroupEdit.SelectedIndexChanged += ComboGroupEdit_SelectedIndexChanged;
        }

        private void EditorForm_Load(object sender, EventArgs e)
        {
            if (DbHelper.TestConnection())
            {
                // Грузим группы так же, как в Form1
                _groups = DataLoader.LoadGroups(true);
                comboGroupEdit.DataSource = _groups;
                comboGroupEdit.DisplayMember = "Name";
            }
        }

        private void SetupTabs()
        {
            // Очищаем стандартные вкладки, если они есть
            tabControl1.TabPages.Clear();

            string[] days = { "Понедельник", "Вторник", "Среда", "Четверг", "Пятница", "Суббота" };

            for (int i = 0; i < 6; i++)
            {
                // 1. Создаем вкладку
                TabPage page = new TabPage(days[i]);

                // 2. Создаем таблицу для этого дня
                DataGridView grid = new DataGridView();
                grid.Dock = DockStyle.Fill;
                grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                grid.AllowUserToAddRows = false; // Строки (пары) добавим сами фиксированно
                grid.RowHeadersVisible = false;

                // Настраиваем колонки
                grid.Columns.Add("colPair", "Пара");
                grid.Columns.Add("colTime", "Время");

                // КОЛОНКА ТИПА НЕДЕЛИ (ComboBox внутри таблицы!)
                DataGridViewComboBoxColumn colWeek = new DataGridViewComboBoxColumn();
                colWeek.HeaderText = "Неделя";
                colWeek.Name = "colWeek";
                colWeek.Items.AddRange("Общая", "Светлая", "Темная");
                colWeek.FlatStyle = FlatStyle.Flat;
                grid.Columns.Add(colWeek);

                grid.Columns.Add("colSubject", "Дисциплина");
                grid.Columns.Add("colTeacher", "Преподаватель");
                grid.Columns.Add("colRoom", "Аудитория");

                // Настраиваем ширину
                grid.Columns["colPair"].Width = 40;
                grid.Columns["colTime"].Width = 100;
                grid.Columns["colWeek"].Width = 80;
                grid.Columns["colRoom"].Width = 70;

                // Добавляем 8 пар (пустых)
                string[] times = { "08:00 - 09:35", "09:45 - 11:20", "11:30 - 13:05", "13:25 - 15:00", "15:10 - 16:45", "16:55 - 18:30", "18:40 - 20:00", "20:10 - 21:30" };
                for (int pair = 1; pair <= 8; pair++)
                {
                    grid.Rows.Add(pair, times[pair - 1], "Общая", "", "", "");
                }

                // Сохраняем таблицу в массив и добавляем на вкладку
                _dailyGrids[i] = grid;
                page.Controls.Add(grid);
                tabControl1.TabPages.Add(page);
            }
        }

        private void ComboGroupEdit_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Здесь позже добавим загрузку расписания из базы
            // Пока просто меняем заголовок
            var grp = comboGroupEdit.SelectedItem as Group;
            if (grp != null)
                this.Text = $"Редактирование расписания: {grp.Name}";
        }

        private void EditorForm_Load_1(object sender, EventArgs e)
        {

        }
    }
}