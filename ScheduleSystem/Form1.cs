using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ScheduleSystem.Data;
using ScheduleSystem.Models;
using System.Data.OleDb;

namespace ScheduleSystem
{
    public partial class Form1 : Form
    {
        private List<AcademicPlanItem> _fullPlan;
        private List<Group> _allGroups;
        private Dictionary<string, List<int>> _groupSemestersCache;

        // Флаги защиты
        private bool _isLoaded = false;
        private bool _isLocked = false;

        public Form1()
        {
            InitializeComponent();

            // --- СОБЫТИЯ ---

            // 1. Глобальные фильтры (Уровень, Форма, Специальность)
            // При их смене мы полностью перестраиваем список групп
            comboLevel.SelectedIndexChanged += (s, e) => FullRebuild(s);
            comboForm.SelectedIndexChanged += (s, e) => FullRebuild(s);
            comboSpecialty.SelectedIndexChanged += (s, e) => FullRebuild(s);

            // 2. Выбор КУРСА (Умный поиск группы)
            comboCourse.SelectedIndexChanged += (s, e) => OnCourseChanged();

            // 3. Выбор СЕЗОНА (Просто обновление)
            comboSeason.SelectedIndexChanged += (s, e) => UpdateGrid();

            // 4. Выбор ГРУППЫ (Главный мастер)
            comboGroups.SelectedIndexChanged += (s, e) => OnGroupChanged();

            // 5. Визуал
            dataGridView1.CellFormatting += DataGridView1_CellFormatting;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            if (DbHelper.TestConnection())
            {
                DbHelper.CheckAndCreateScheduleTable();
                _isLocked = true;
                _fullPlan = DataLoader.LoadAcademicPlan();
                _allGroups = DataLoader.LoadGroups(true);
                BuildGroupCache();

                // Инициализация списков
                comboLevel.Items.Clear();
                comboLevel.Items.AddRange(new object[] { "Бакалавриат", "Магистратура", "Специалитет" });
                comboForm.Items.Clear();
                comboForm.Items.AddRange(new object[] { "Очная", "Заочная" });
                comboSeason.Items.Clear();
                comboSeason.Items.AddRange(new object[] { "Осень", "Весна" });

                // Безопасный выбор первых элементов
                if (comboLevel.Items.Count > 0) comboLevel.SelectedIndex = 0;
                if (comboForm.Items.Count > 0) comboForm.SelectedIndex = 0;
                if (comboSeason.Items.Count > 0) comboSeason.SelectedIndex = 0;

                UpdateSpecialtiesList();
                UpdateCoursesList();

                _isLocked = false;
                _isLoaded = true;

                // Запускаем полную перестройку под дефолтные значения
                FullRebuild(null);
            }
        }

        // --- ЛОГИКА 1: ПОЛНАЯ ПЕРЕСТРОЙКА (Смена Уровня/Спец/Формы) ---
        private void FullRebuild(object sender)
        {
            if (!_isLoaded || _isLocked) return;
            _isLocked = true;
            try
            {
                // Если сменили уровень - обновляем зависимые списки
                if (sender == comboLevel)
                {
                    UpdateSpecialtiesList();
                    UpdateCoursesList();
                }

                // 1. Фильтруем список групп
                FilterGroupsList();

                // 2. Если список пуст - ничего не поделаешь
                if (comboGroups.Items.Count == 0)
                {
                    dataGridView1.DataSource = null;
                    return;
                }

                // 3. Выбираем первую группу (пока просто так)
                if (comboGroups.SelectedIndex == -1)
                    comboGroups.SelectedIndex = 0;

                // 4. ЗАПУСКАЕМ АВТО-ПОЧИНКУ
                // Это гарантирует, что курс и сезон подстроятся под эту первую группу
                AutoRepairState();
            }
            finally { _isLocked = false; }

            // В конце обновляем таблицу
            UpdateGrid();
        }

        // --- ЛОГИКА 2: СМЕНА КУРСА ---
        private void OnCourseChanged()
        {
            if (!_isLoaded || _isLocked) return;
            _isLocked = true; // Блокируем, мы сами всё решим
            try
            {
                // Пользователь хочет этот курс.
                int desiredCourse = int.Parse(comboCourse.SelectedItem.ToString());
                var currentGroup = comboGroups.SelectedItem as Group;

                if (currentGroup == null) return;

                // Проверяем: А текущая группа учится на этом курсе?
                if (IsGroupValidForCourse(currentGroup.Name, desiredCourse))
                {
                    // Всё ок, просто обновляем таблицу
                }
                else
                {
                    // НЕТ! Текущая группа не подходит.
                    // Ищем в списке ДРУГУЮ группу, которая подходит под этот курс.
                    bool found = false;
                    foreach (Group g in comboGroups.Items)
                    {
                        if (IsGroupValidForCourse(g.Name, desiredCourse))
                        {
                            comboGroups.SelectedItem = g; // Переключаем группу
                            // (Сезон подстроится сам в конце метода при UpdateGrid, если надо допилим)
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        // ВООБЩЕ НИКТО в этой специальности не учится на этом курсе.
                        // (Например, выбрали 5 курс для Магистров).
                        // ОТКАТЫВАЕМ КУРС НАЗАД (к курсу текущей группы)
                        AutoRepairState();
                    }
                }
            }
            finally { _isLocked = false; }
            UpdateGrid();
        }

        // --- ЛОГИКА 3: СМЕНА ГРУППЫ ---
        private void OnGroupChanged()
        {
            if (!_isLoaded || _isLocked) return;
            _isLocked = true;
            try
            {
                // Когда выбрали группу, мы жестко подгоняем всё под неё
                AutoRepairState();
            }
            finally { _isLocked = false; }
            UpdateGrid();
        }

        // --- ГЛАВНЫЙ МЕХАНИЗМ "АВТО-ПОЧИНКА" ---
        // Этот метод берет текущую ГРУППУ и заставляет все остальные списки
        // принять правильные значения.
        private void AutoRepairState()
        {
            var selectedGroup = comboGroups.SelectedItem as Group;
            if (selectedGroup == null) return;

            string gName = selectedGroup.Name;

            // 1. ЧИНИМ СПЕЦИАЛЬНОСТЬ/УРОВЕНЬ/ФОРМУ (Если вдруг они не совпадают)
            var level = SpecialtyHelper.GetLevel(gName);
            string levelStr = level == EducationLevel.Master ? "Магистратура" :
                              level == EducationLevel.Specialist ? "Специалитет" : "Бакалавриат";
            if (comboLevel.SelectedItem?.ToString() != levelStr)
            {
                comboLevel.SelectedItem = levelStr;
                UpdateSpecialtiesList();
                UpdateCoursesList();
            }

            var form = SpecialtyHelper.GetForm(gName);
            string formStr = form == EducationForm.Correspondence ? "Заочная" : "Очная";
            if (comboForm.SelectedItem?.ToString() != formStr) comboForm.SelectedItem = formStr;

            string spec = SpecialtyHelper.GetSpecialty(gName);
            if (comboSpecialty.Items.Contains(spec) && comboSpecialty.SelectedItem?.ToString() != spec)
                comboSpecialty.SelectedItem = spec;

            // 2. ЧИНИМ КУРС И СЕЗОН
            // Проверяем, подходит ли текущий выбранный курс этой группе
            int currentCourse = int.Parse(comboCourse.SelectedItem.ToString());

            if (!IsGroupValidForCourse(gName, currentCourse))
            {
                // Не подходит! (Например, группа 1 курса, а выбран 3).
                // Ищем минимальный (родной) курс группы.
                if (_groupSemestersCache.ContainsKey(gName) && _groupSemestersCache[gName].Count > 0)
                {
                    int minSem = _groupSemestersCache[gName].Min();
                    int nativeCourse = (minSem + 1) / 2;

                    // Ставим правильный курс
                    string courseStr = nativeCourse.ToString();
                    if (comboCourse.Items.Contains(courseStr))
                        comboCourse.SelectedItem = courseStr;

                    // Ставим правильный сезон
                    comboSeason.SelectedIndex = (minSem % 2 != 0) ? 0 : 1;
                }
            }
            // Если курс подходит, сезон не трогаем (пользователь мог сам выбрать Весну)
        }

        private void UpdateGrid()
        {
            if (comboGroups.SelectedItem == null || comboCourse.SelectedItem == null)
            {
                dataGridView1.DataSource = null;
                return;
            }

            var selectedGroup = comboGroups.SelectedItem as Group;
            int course = int.Parse(comboCourse.SelectedItem.ToString());
            int seasonAdd = comboSeason.SelectedItem.ToString() == "Осень" ? 1 : 2;
            int targetSemester = (course - 1) * 2 + seasonAdd;

            var filteredList = _fullPlan.Where(item =>
                item.GroupName == selectedGroup.Name &&
                item.Semester == targetSemester
            ).ToList();

            dataGridView1.DataSource = filteredList;
            SetupGrid(); // Применяем красоту

            this.Text = $"Группа: {selectedGroup.Name} | {course}-й курс | Сем: {targetSemester}";
        }

        // --- ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ---

        private void FilterGroupsList()
        {
            if (comboSpecialty.SelectedItem == null || comboLevel.SelectedItem == null) return;

            string selectedSpec = comboSpecialty.SelectedItem.ToString();
            string levelName = comboLevel.SelectedItem.ToString();
            string formName = comboForm.SelectedItem.ToString();

            var targetLevel = levelName == "Магистратура" ? EducationLevel.Master :
                              levelName == "Специалитет" ? EducationLevel.Specialist : EducationLevel.Bachelor;
            var targetForm = formName == "Заочная" ? EducationForm.Correspondence : EducationForm.FullTime;

            // Берем ВСЕ группы этой специальности (без фильтра по курсу)
            var validGroups = _allGroups.Where(g =>
            {
                return SpecialtyHelper.GetLevel(g.Name) == targetLevel &&
                       SpecialtyHelper.GetForm(g.Name) == targetForm &&
                       SpecialtyHelper.GetSpecialty(g.Name) == selectedSpec;
            }).ToList();

            comboGroups.DataSource = validGroups;
            comboGroups.DisplayMember = "Name";

            // Если старая группа осталась в списке - выберем её, иначе сброс
            var currentGroup = comboGroups.SelectedItem as Group;
            if (validGroups.Count > 0)
            {
                // ВАЖНО: Не выбираем сразу 0, пытаемся сохранить
                if (currentGroup != null && validGroups.Any(g => g.Name == currentGroup.Name))
                    comboGroups.SelectedIndex = validGroups.FindIndex(g => g.Name == currentGroup.Name);
                else
                    comboGroups.SelectedIndex = -1; // Пока ничего, FullRebuild выберет 0 если надо
            }
        }

        private bool IsGroupValidForCourse(string groupName, int course)
        {
            if (!_groupSemestersCache.ContainsKey(groupName)) return false;
            var semesters = _groupSemestersCache[groupName];
            int sem1 = (course * 2) - 1;
            int sem2 = course * 2;
            return semesters.Contains(sem1) || semesters.Contains(sem2);
        }

        // --- ДИЗАЙН ---
        private void SetupGrid()
        {
            dataGridView1.RowHeadersVisible = false;
            var mainFont = new Font("Times New Roman", 11, FontStyle.Regular);
            var headerFont = new Font("Times New Roman", 11, FontStyle.Bold);

            dataGridView1.DefaultCellStyle.Font = mainFont;
            dataGridView1.ColumnHeadersDefaultCellStyle.Font = headerFont;
            dataGridView1.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dataGridView1.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;

            if (!dataGridView1.Columns.Contains("colNo"))
            {
                DataGridViewTextBoxColumn colNo = new DataGridViewTextBoxColumn();
                colNo.Name = "colNo";
                colNo.HeaderText = "№";
                colNo.ReadOnly = true;
                dataGridView1.Columns.Insert(0, colNo);
            }
            dataGridView1.Columns["colNo"].DisplayIndex = 0;
            dataGridView1.Columns["colNo"].Width = 40;

            if (dataGridView1.Columns["PlanId"] != null) dataGridView1.Columns["PlanId"].Visible = false;
            if (dataGridView1.Columns["GroupName"] != null) dataGridView1.Columns["GroupName"].Visible = false;

            if (dataGridView1.Columns["SubjectName"] != null) dataGridView1.Columns["SubjectName"].HeaderText = "Дисциплина";
            if (dataGridView1.Columns["TeacherLecture"] != null) dataGridView1.Columns["TeacherLecture"].HeaderText = "Лектор";
            if (dataGridView1.Columns["TeacherPractice"] != null) dataGridView1.Columns["TeacherPractice"].HeaderText = "Практик";
            if (dataGridView1.Columns["TotalHours"] != null) dataGridView1.Columns["TotalHours"].HeaderText = "Всего часов";
            if (dataGridView1.Columns["LectureHours"] != null) dataGridView1.Columns["LectureHours"].HeaderText = "Лек.";
            if (dataGridView1.Columns["LabHours"] != null) dataGridView1.Columns["LabHours"].HeaderText = "Лаб.";
            if (dataGridView1.Columns["PracticeHours"] != null) dataGridView1.Columns["PracticeHours"].HeaderText = "Прак.";
            if (dataGridView1.Columns["Semester"] != null) dataGridView1.Columns["Semester"].HeaderText = "Сем.";
        }

        private void DataGridView1_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (dataGridView1.Columns[e.ColumnIndex].Name == "colNo")
            {
                e.Value = (e.RowIndex + 1).ToString();
                e.CellStyle.Font = new Font("Times New Roman", 11, FontStyle.Bold);
            }
        }

        // --- КЭШ И СПИСКИ ---
        private void BuildGroupCache()
        {
            _groupSemestersCache = new Dictionary<string, List<int>>();
            foreach (var group in _allGroups)
            {
                var semesters = _fullPlan.Where(p => p.GroupName == group.Name).Select(p => p.Semester).Distinct().ToList();
                _groupSemestersCache[group.Name] = semesters;
            }
        }

        private void UpdateSpecialtiesList()
        {
            if (comboLevel.SelectedItem == null) return;
            string levelName = comboLevel.SelectedItem.ToString();
            var level = levelName == "Магистратура" ? EducationLevel.Master :
                        levelName == "Специалитет" ? EducationLevel.Specialist : EducationLevel.Bachelor;

            string currentSpec = comboSpecialty.SelectedItem?.ToString();
            var source = SpecialtyHelper.SpecialtiesByLevel.ContainsKey(level) ? SpecialtyHelper.SpecialtiesByLevel[level] : new string[] { "Нет данных" };

            comboSpecialty.DataSource = source;
            if (currentSpec != null && source.Contains(currentSpec))
                comboSpecialty.SelectedItem = currentSpec;
        }

        private void UpdateCoursesList()
        {
            if (comboLevel.SelectedItem == null) return;
            string levelName = comboLevel.SelectedItem.ToString();
            int maxCourse = (levelName == "Магистратура") ? 2 : (levelName == "Специалитет" ? 5 : 4);

            string currentCourse = comboCourse.SelectedItem?.ToString();

            comboCourse.Items.Clear();
            for (int i = 1; i <= maxCourse; i++) comboCourse.Items.Add(i.ToString());

            // Если старый курс валиден, оставляем, иначе 1
            if (currentCourse != null && int.Parse(currentCourse) <= maxCourse)
                comboCourse.SelectedItem = currentCourse;
            else
                comboCourse.SelectedIndex = 0;
        }

        private void btnOpenEditor_Click(object sender, EventArgs e)
        {
            // Создаем и открываем окно редактора
            EditorForm editor = new EditorForm();
            editor.Show(); // Show() открывает как отдельное окно, ShowDialog() блокирует старое
        }

        private void btnMatrix_Click(object sender, EventArgs e)
        {
            ScheduleMatrixForm form = new ScheduleMatrixForm();
            form.Show();
        }
    }
}