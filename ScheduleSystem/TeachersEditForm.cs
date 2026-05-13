using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Drawing;
using System.Windows.Forms;
using ScheduleSystem.Data;

namespace ScheduleSystem
{
    public partial class TeachersEditForm : Form
    {
        // Класс для хранения данных в списке
        private class TeacherDTO
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public override string ToString() => Name;
        }

        private class RoomPrefDTO
        {
            public string Room { get; set; }
            public string Priority { get; set; }
            public override string ToString() => $"{Room} ({Priority})";
        }

        private List<TeacherDTO> _teachers = new List<TeacherDTO>();
        private TeacherDTO _selectedTeacher = null;
        private List<RoomPrefDTO> _currentPrefs = new List<RoomPrefDTO>();

        // Флаги состояния
        private bool _isDirty = false;
        private bool _isLoading = false; // Блокирует события при загрузке

        // Элементы интерфейса
        private ListBox lstTeachers;
        private TextBox txtName;
        private NumericUpDown numMaxLec, numMaxPrac;
        private DataGridView gridAvail;
        private ListBox lstPrefs;
        private ComboBox cmbRooms, cmbPriority;
        private Button btnAddPref, btnDelPref, btnSave, btnAddTeacher, btnDeleteTeacher;
        private Label lblStatus;

        public TeachersEditForm()
        {
            this.Text = "Редактор преподавателей";
            this.Size = new Size(950, 600);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            SetupUI();
            LoadTeachersList();
        }

        // Пустышка для дизайнера
        private void TeachersEditForm_Load(object sender, EventArgs e) { }

        private void SetupUI()
        {
            // Увеличиваем высоту формы, чтобы всё влезло
            this.Size = new Size(950, 750);

            // === ЛЕВАЯ КОЛОНКА (СПИСОК) ===
            this.Controls.Add(new Label() { Text = "Преподаватели:", Location = new Point(10, 10), AutoSize = true, Font = new Font(Font, FontStyle.Bold) });

            lstTeachers = new ListBox();
            lstTeachers.Location = new Point(10, 35);
            lstTeachers.Size = new Size(200, 550); // Высота списка под форму
            lstTeachers.SelectedIndexChanged += LstTeachers_SelectedIndexChanged;
            this.Controls.Add(lstTeachers);

            btnAddTeacher = new Button() { Text = "+ Добавить", Location = new Point(10, 600), Size = new Size(95, 30) };
            btnAddTeacher.Click += (s, e) => PrepareNewTeacher();
            this.Controls.Add(btnAddTeacher);

            btnDeleteTeacher = new Button() { Text = "Удалить", Location = new Point(115, 600), Size = new Size(95, 30), ForeColor = Color.DarkRed };
            btnDeleteTeacher.Click += BtnDeleteTeacher_Click;
            this.Controls.Add(btnDeleteTeacher);

            // === ПРАВАЯ ЧАСТЬ (НАСТРОЙКИ) ===
            int xStart = 230;
            int y = 10;
            int rightW = 680;

            // 1. ФИО
            this.Controls.Add(new Label() { Text = "ФИО Преподавателя:", Location = new Point(xStart, y), AutoSize = true });
            txtName = new TextBox() { Location = new Point(xStart + 130, y - 3), Size = new Size(550, 25) };
            txtName.TextChanged += (s, e) => SetDirty();
            this.Controls.Add(txtName);

            y += 40;

            // 2. НАГРУЗКА
            var grpLoad = new GroupBox() { Text = "Ограничение нагрузки (групп в потоке)", Location = new Point(xStart, y), Size = new Size(rightW, 60) };
            this.Controls.Add(grpLoad);

            new Label() { Text = "Лекции:", Location = new Point(20, 25), AutoSize = true, Parent = grpLoad };
            numMaxLec = new NumericUpDown() { Location = new Point(80, 23), Size = new Size(50, 25), Minimum = 1, Maximum = 10, Value = 1, Parent = grpLoad };
            numMaxLec.ValueChanged += (s, e) => SetDirty();

            new Label() { Text = "Практики:", Location = new Point(160, 25), AutoSize = true, Parent = grpLoad };
            numMaxPrac = new NumericUpDown() { Location = new Point(230, 23), Size = new Size(50, 25), Minimum = 1, Maximum = 10, Value = 1, Parent = grpLoad };
            numMaxPrac.ValueChanged += (s, e) => SetDirty();

            y += 70;

            // 3. ГРАФИК (ВО ВСЮ ШИРИНУ)
            var grpGraph = new GroupBox()
            {
                Text = "График доступности (Клик по заголовкам меняет всю строку/столбец)",
                Location = new Point(xStart, y),
                Size = new Size(rightW, 280) // Высота 280
            };
            this.Controls.Add(grpGraph);

            gridAvail = new DataGridView();
            gridAvail.Parent = grpGraph;
            gridAvail.Dock = DockStyle.Fill;
            gridAvail.BackgroundColor = SystemColors.Control;
            gridAvail.BorderStyle = BorderStyle.None;
            gridAvail.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            // ОТКЛЮЧАЕМ СКРОЛЛ И РЕДАКТИРОВАНИЕ СТРУКТУРЫ
            gridAvail.ScrollBars = ScrollBars.None;
            gridAvail.AllowUserToAddRows = false;
            gridAvail.AllowUserToDeleteRows = false;
            gridAvail.AllowUserToResizeColumns = false;
            gridAvail.AllowUserToResizeRows = false;

            gridAvail.RowHeadersWidth = 60;
            gridAvail.ColumnHeadersHeight = 35;
            gridAvail.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            // Колонки
            string[] days = { "ПН", "ВТ", "СР", "ЧТ", "ПТ", "СБ" };
            foreach (var d in days)
            {
                var col = new DataGridViewCheckBoxColumn { HeaderText = d, SortMode = DataGridViewColumnSortMode.NotSortable };
                gridAvail.Columns.Add(col);
            }

            // РАСЧЕТ ВЫСОТЫ СТРОК (Чтобы влезло ровно 6 штук без прокрутки)
            // (Высота контейнера - Шапка - Нижний бордюр) / 6
            // (Высота контейнера - Шапка - Нижний бордюр) / 6
            // ИСПРАВЛЕНИЕ: Увеличиваем отступ, чтобы учесть заголовок рамки GroupBox (~20px)
            // Было -10, ставим -40. Теперь таблица будет чуть короче и не будет прокручиваться.
            int availableHeight = grpGraph.Height - 35 - 40;
            int rowH = availableHeight / 6;
        
            for (int i = 1; i <= 6; i++)
            {
                gridAvail.Rows.Add(true, true, true, true, true, true);
                gridAvail.Rows[i - 1].HeaderCell.Value = i.ToString();
                gridAvail.Rows[i - 1].Height = rowH;
            }

            gridAvail.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            gridAvail.DefaultCellStyle.Font = new Font(Font.FontFamily, 12, FontStyle.Regular); // Галочки покрупнее

            // События
            // События кликов
            gridAvail.ColumnHeaderMouseClick += (s, e) => {
                gridAvail.EndEdit(); // <--- ВАЖНО: Завершаем редактирование активной ячейки

                // Определяем новое значение (инверсия первой ячейки)
                bool newVal = !(bool)gridAvail.Rows[0].Cells[e.ColumnIndex].Value;

                foreach (DataGridViewRow r in gridAvail.Rows)
                    r.Cells[e.ColumnIndex].Value = newVal;

                SetDirty();
            };

            gridAvail.RowHeaderMouseClick += (s, e) => {
                gridAvail.EndEdit(); // <--- ВАЖНО: Завершаем редактирование активной ячейки

                // Определяем новое значение
                bool newVal = !(bool)gridAvail.Rows[e.RowIndex].Cells[0].Value;

                for (int j = 0; j < 6; j++)
                    gridAvail.Rows[e.RowIndex].Cells[j].Value = newVal;

                SetDirty();
            };

            gridAvail.CellValueChanged += (s, e) => SetDirty();
            gridAvail.CurrentCellDirtyStateChanged += (s, e) => { if (gridAvail.IsCurrentCellDirty) gridAvail.CommitEdit(DataGridViewDataErrorContexts.Commit); };

            y += 290; // Сдвигаем вниз (280 + 10 отступ)

            // 4. АУДИТОРИИ (СНИЗУ)
            var grpRooms = new GroupBox()
            {
                Text = "Предпочтительные аудитории",
                Location = new Point(xStart, y),
                Size = new Size(rightW, 160)
            };
            this.Controls.Add(grpRooms);

            new Label() { Text = "Ауд:", Location = new Point(15, 25), AutoSize = true, Parent = grpRooms };
            cmbRooms = new ComboBox() { Location = new Point(50, 22), Size = new Size(100, 25), DropDownStyle = ComboBoxStyle.DropDownList, Parent = grpRooms };
            try { foreach (var r in DataLoader.LoadRooms()) cmbRooms.Items.Add(r); } catch { }

            new Label() { Text = "Важность:", Location = new Point(160, 25), AutoSize = true, Parent = grpRooms };
            cmbPriority = new ComboBox() { Location = new Point(230, 22), Size = new Size(100, 25), DropDownStyle = ComboBoxStyle.DropDownList, Parent = grpRooms };
            cmbPriority.Items.AddRange(new string[] { "Высокий", "Средний" });
            cmbPriority.SelectedIndex = 0;

            btnAddPref = new Button() { Text = "Добавить", Location = new Point(340, 21), Size = new Size(100, 27), Parent = grpRooms };
            btnAddPref.Click += (s, e) => {
                if (cmbRooms.SelectedItem == null) return;
                _currentPrefs.Add(new RoomPrefDTO { Room = cmbRooms.SelectedItem.ToString(), Priority = cmbPriority.SelectedItem.ToString() });
                RefreshPrefsList();
                SetDirty();
            };

            lstPrefs = new ListBox() { Location = new Point(15, 60), Size = new Size(rightW - 140, 90), Parent = grpRooms };

            btnDelPref = new Button() { Text = "Убрать", Location = new Point(rightW - 115, 60), Size = new Size(100, 30), Parent = grpRooms };
            btnDelPref.Click += (s, e) => {
                if (lstPrefs.SelectedIndex != -1) { _currentPrefs.RemoveAt(lstPrefs.SelectedIndex); RefreshPrefsList(); SetDirty(); }
            };

            y += 170; // Отступ после блока аудиторий

            // 5. КНОПКА СОХРАНЕНИЯ
            btnSave = new Button()
            {
                Text = "💾 СОХРАНИТЬ ИЗМЕНЕНИЯ",
                Location = new Point(xStart, y),
                Size = new Size(rightW, 50),
                BackColor = Color.LightGreen,
                Font = new Font(Font.FontFamily, 12, FontStyle.Bold)
            };
            btnSave.Click += BtnSave_Click;
            this.Controls.Add(btnSave); // <-- ДОБАВЛЯЕМ ЕЁ НА ФОРМУ

            lblStatus = new Label() { Location = new Point(xStart, y + 60), AutoSize = true, ForeColor = Color.Gray };
            this.Controls.Add(lblStatus);
        }
        private void SetDirty()
        {
            if (_isLoading) return; // Если идет загрузка - игнорируем изменения!
            _isDirty = true;
            btnSave.BackColor = Color.Orange;
            btnSave.Text = "💾 СОХРАНИТЬ ИЗМЕНЕНИЯ *";
        }

        private void ResetDirty()
        {
            _isDirty = false;
            btnSave.BackColor = Color.LightGreen;
            btnSave.Text = "💾 СОХРАНИТЬ ИЗМЕНЕНИЯ";
        }

        // --- ЛОГИКА ВЫБОРА (С ВОЗВРАТОМ) ---
        private TeacherDTO _prevSelection = null; // Помним, кто был выбран

        private void LstTeachers_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Если мы программно возвращаем выбор назад, игнорируем повторный вызов
            if (_isLoading) return;

            var newSelection = lstTeachers.SelectedItem as TeacherDTO;

            // Если есть изменения в текущем (предыдущем) преподавателе
            if (_selectedTeacher != null && _isDirty && newSelection != _selectedTeacher)
            {
                var res = MessageBox.Show($"Сохранить изменения для {_selectedTeacher.Name}?", "Сохранение", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

                if (res == DialogResult.Cancel)
                {
                    // Отмена: возвращаем выделение назад и выходим
                    _isLoading = true; // Блокируем события, чтобы не зациклиться
                    lstTeachers.SelectedItem = _selectedTeacher;
                    _isLoading = false;
                    return;
                }
                else if (res == DialogResult.Yes)
                {
                    SaveDataInternal(); // Сохраняем и идем дальше
                }
                // Если No - просто идем дальше, изменения теряются
            }

            // Загружаем нового
            _selectedTeacher = newSelection;
            if (_selectedTeacher != null)
            {
                LoadFullTeacherInfo(_selectedTeacher.Id);
                lblStatus.Text = $"Редактирование: {_selectedTeacher.Name}";
            }
        }

        // --- ЗАГРУЗКА ДАННЫХ ---
        private void LoadTeachersList()
        {
            _isLoading = true;
            lstTeachers.Items.Clear();
            _selectedTeacher = null;
            _teachers.Clear();

            try
            {
                using (var conn = DbHelper.GetConnection())
                {
                    conn.Open();
                    var cmd = new OleDbCommand("SELECT TeacherID, FullName FROM Teachers ORDER BY FullName", conn);
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            _teachers.Add(new TeacherDTO { Id = (int)r["TeacherID"], Name = r["FullName"].ToString() });
                        }
                    }
                }
                foreach (var t in _teachers) lstTeachers.Items.Add(t);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки списка: " + ex.Message);
            }

            _isLoading = false;
            ResetDirty();
        }

        private void LoadFullTeacherInfo(int id)
        {
            _isLoading = true; // ВАЖНО: Блокируем Dirty, пока заполняем поля
            try
            {
                using (var conn = DbHelper.GetConnection())
                {
                    conn.Open();

                    // 1. Основные данные
                    var cmd = new OleDbCommand("SELECT * FROM Teachers WHERE TeacherID = ?", conn);
                    cmd.Parameters.AddWithValue("?", id);
                    using (var r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            txtName.Text = r["FullName"].ToString();
                            // Безопасное чтение чисел (защита от NULL)
                            numMaxLec.Value = r["MaxLectureGroups"] is int v1 ? v1 : 1;
                            numMaxPrac.Value = r["MaxPracticeGroups"] is int v2 ? v2 : 1;
                        }
                        else
                        {
                            // Если запись не найдена (странно, но бывает)
                            txtName.Text = "Ошибка чтения";
                        }
                    }

                    // 2. График (сброс + загрузка)
                    foreach (DataGridViewRow row in gridAvail.Rows)
                        for (int i = 0; i < 6; i++) row.Cells[i].Value = true;

                    var cmdAvail = new OleDbCommand("SELECT DayIdx, PairIdx, IsAvailable FROM TeacherAvailability WHERE TeacherID = ?", conn);
                    cmdAvail.Parameters.AddWithValue("?", id);
                    using (var r = cmdAvail.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            try
                            {
                                // Пытаемся прочитать как число или как строку
                                int d = Convert.ToInt32(r["DayIdx"]);
                                int p = Convert.ToInt32(r["PairIdx"]);
                                bool av = Convert.ToBoolean(r["IsAvailable"]);
                                if (d >= 0 && d <= 5 && p >= 1 && p <= 6)
                                    gridAvail.Rows[p - 1].Cells[d].Value = av;
                            }
                            catch { /* Игнорируем ошибки конвертации старых данных */ }
                        }
                    }

                    // 3. Аудитории
                    // 3. Аудитории (ИСПРАВЛЕННЫЙ БЛОК)
                    _currentPrefs.Clear();

                    // Мы делаем JOIN (объединение), чтобы по ID аудитории получить её реальный Номер (текст)
                    // T.RoomNumber - это на самом деле ID (так у вас в базе названо поле)
                    // C.RoomNumber - это реальный номер (например, "101")
                    string sql = @"
    SELECT C.RoomNumber AS RealRoomName, T.Priority 
    FROM TeacherRoomPrefs AS T
    INNER JOIN Classrooms AS C ON T.RoomNumber = C.RoomID
    WHERE T.TeacherID = ?";

                    var cmdPref = new OleDbCommand(sql, conn);
                    cmdPref.Parameters.AddWithValue("?", id);

                    using (var r = cmdPref.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            _currentPrefs.Add(new RoomPrefDTO
                            {
                                // Теперь читаем реальное название, которое мы получили через JOIN
                                Room = r["RealRoomName"].ToString(),
                                Priority = r["Priority"].ToString()
                            });
                        }
                    }
                    RefreshPrefsList();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка чтения данных преподавателя: " + ex.Message);
            }
            finally
            {
                _isLoading = false; // Разблокируем Dirty
                ResetDirty();       // Сбрасываем флаг, т.к. данные только что загружены
            }
        }

        private void PrepareNewTeacher()
        {
            // Если есть несохраненные данные
            if (_isDirty)
            {
                if (MessageBox.Show("Сохранить изменения перед созданием нового?", "Вопрос", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    SaveDataInternal();
            }

            lstTeachers.SelectedIndex = -1;
            _selectedTeacher = null;

            _isLoading = true;
            txtName.Text = "";
            numMaxLec.Value = 1;
            numMaxPrac.Value = 1;
            _currentPrefs.Clear();
            RefreshPrefsList();
            foreach (DataGridViewRow row in gridAvail.Rows)
                for (int i = 0; i < 6; i++) row.Cells[i].Value = true;
            _isLoading = false;

            ResetDirty();
            lblStatus.Text = "Режим: Новый преподаватель";
            txtName.Focus();
        }

        private void RefreshPrefsList()
        {
            lstPrefs.Items.Clear();
            foreach (var p in _currentPrefs) lstPrefs.Items.Add(p);
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            SaveDataInternal();
            MessageBox.Show("Сохранено!");
            // Перезагружаем список, чтобы обновилось имя, если его меняли
            LoadTeachersList();
        }

        private void SaveDataInternal()
        {
            string name = txtName.Text.Trim();
            if (string.IsNullOrEmpty(name)) return;

            using (var conn = DbHelper.GetConnection())
            {
                conn.Open();
                int currentId = 0;

                // 1. Сохраняем/Создаем учителя
                if (_selectedTeacher == null)
                {
                    string sql = "INSERT INTO Teachers (FullName, MaxLectureGroups, MaxPracticeGroups) VALUES (?, ?, ?)";
                    using (var cmd = new OleDbCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("?", name);
                        cmd.Parameters.AddWithValue("?", (int)numMaxLec.Value);
                        cmd.Parameters.AddWithValue("?", (int)numMaxPrac.Value);
                        cmd.ExecuteNonQuery();
                    }
                    using (var cmdLast = new OleDbCommand("SELECT @@IDENTITY", conn))
                        currentId = Convert.ToInt32(cmdLast.ExecuteScalar());
                }
                else
                {
                    currentId = _selectedTeacher.Id;
                    string sql = "UPDATE Teachers SET FullName=?, MaxLectureGroups=?, MaxPracticeGroups=? WHERE TeacherID=?";
                    using (var cmd = new OleDbCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("?", name);
                        cmd.Parameters.AddWithValue("?", (int)numMaxLec.Value);
                        cmd.Parameters.AddWithValue("?", (int)numMaxPrac.Value);
                        cmd.Parameters.AddWithValue("?", currentId);
                        cmd.ExecuteNonQuery();
                    }
                }

                // 2. График (перезапись)
                new OleDbCommand($"DELETE FROM TeacherAvailability WHERE TeacherID={currentId}", conn).ExecuteNonQuery();
                for (int d = 0; d < 6; d++)
                {
                    for (int p = 1; p <= 6; p++)
                    {
                        bool isChecked = (bool)gridAvail.Rows[p - 1].Cells[d].Value;
                        string sqlAvail = "INSERT INTO TeacherAvailability (TeacherID, DayIdx, PairIdx, IsAvailable) VALUES (?, ?, ?, ?)";
                        using (var cmd = new OleDbCommand(sqlAvail, conn))
                        {
                            cmd.Parameters.AddWithValue("?", currentId);
                            cmd.Parameters.AddWithValue("?", d.ToString()); // 0..5
                            cmd.Parameters.AddWithValue("?", p.ToString()); // 1..6
                            cmd.Parameters.AddWithValue("?", isChecked);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }

                // 3. Аудитории (перезапись) - ИСПРАВЛЕНО!
                new OleDbCommand($"DELETE FROM TeacherRoomPrefs WHERE TeacherID={currentId}", conn).ExecuteNonQuery();

                foreach (var pref in _currentPrefs)
                {
                    // А. Сначала находим ID аудитории по её названию (которое лежит в pref.Room)
                    int roomId = 0;
                    string findSql = "SELECT RoomID FROM Classrooms WHERE RoomNumber = ?";
                    using (var cmdFind = new OleDbCommand(findSql, conn))
                    {
                        cmdFind.Parameters.AddWithValue("?", pref.Room); // Передаем текст (например, "101")
                        object res = cmdFind.ExecuteScalar();
                        if (res != null) roomId = Convert.ToInt32(res);
                    }

                    // Б. Если нашли ID, сохраняем его как ЧИСЛО
                    if (roomId > 0)
                    {
                        // ВАЖНО: В поле RoomNumber (которое числовое в БД) пишем roomId
                        string sqlPref = "INSERT INTO TeacherRoomPrefs (TeacherID, RoomNumber, Priority) VALUES (?, ?, ?)";
                        using (var cmd = new OleDbCommand(sqlPref, conn))
                        {
                            cmd.Parameters.AddWithValue("?", currentId);
                            cmd.Parameters.AddWithValue("?", roomId);        // <-- ЧИСЛО!
                            cmd.Parameters.AddWithValue("?", pref.Priority);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            ResetDirty();
        }
        private void BtnDeleteTeacher_Click(object sender, EventArgs e)
        {
            if (_selectedTeacher == null) return;
            if (MessageBox.Show("Удалить?", "Подтверждение", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                using (var conn = DbHelper.GetConnection())
                {
                    conn.Open();
                    new OleDbCommand($"DELETE FROM TeacherAvailability WHERE TeacherID={_selectedTeacher.Id}", conn).ExecuteNonQuery();
                    new OleDbCommand($"DELETE FROM TeacherRoomPrefs WHERE TeacherID={_selectedTeacher.Id}", conn).ExecuteNonQuery();
                    new OleDbCommand($"DELETE FROM Teachers WHERE TeacherID={_selectedTeacher.Id}", conn).ExecuteNonQuery();
                }
                LoadTeachersList();
                PrepareNewTeacher();
            }
        }
    }
}