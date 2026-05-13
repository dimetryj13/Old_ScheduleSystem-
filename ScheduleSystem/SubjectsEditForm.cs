using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ScheduleSystem.Data;

namespace ScheduleSystem
{
    public partial class SubjectsEditForm : Form
    {
        // Простой класс для хранения в списке
        private class SubjectDTO
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public override string ToString() => Name;
        }

        private List<SubjectDTO> _subjects = new List<SubjectDTO>();
        private SubjectDTO _selectedSubject = null;

        // Флаги
        private bool _isDirty = false;
        private bool _isLoading = false;

        // UI Элементы
        private ListBox lstSubjects;
        private TextBox txtName;
        private CheckBox chkComputers;
        private ComboBox cmbFixedRoom;
        private ComboBox cmbForbiddenRoom;

        private Button btnSave, btnAdd, btnDelete;
        private Label lblStatus;

        public SubjectsEditForm()
        {
            this.Text = "Редактор дисциплин";
            this.Size = new Size(800, 500);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            SetupUI();
            LoadSubjectsList();
        }

        // Для дизайнера
        private void SubjectsEditForm_Load(object sender, EventArgs e) { }

        private void SetupUI()
        {
            // === ЛЕВАЯ КОЛОНКА ===
            this.Controls.Add(new Label() { Text = "Дисциплины:", Location = new Point(10, 10), AutoSize = true, Font = new Font(Font, FontStyle.Bold) });

            lstSubjects = new ListBox();
            lstSubjects.Location = new Point(10, 35);
            lstSubjects.Size = new Size(250, 360);
            lstSubjects.SelectedIndexChanged += LstSubjects_SelectedIndexChanged;
            this.Controls.Add(lstSubjects);

            btnAdd = new Button() { Text = "+ Добавить", Location = new Point(10, 405), Size = new Size(120, 35) };
            btnAdd.Click += (s, e) => PrepareNewSubject();
            this.Controls.Add(btnAdd);

            btnDelete = new Button() { Text = "Удалить", Location = new Point(140, 405), Size = new Size(120, 35), ForeColor = Color.DarkRed };
            btnDelete.Click += BtnDelete_Click;
            this.Controls.Add(btnDelete);

            // === ПРАВАЯ ЧАСТЬ ===
            int x = 280;
            int y = 35;
            int w = 480;

            // 1. Название
            this.Controls.Add(new Label() { Text = "Название дисциплины:", Location = new Point(x, y - 20), AutoSize = true });
            txtName = new TextBox() { Location = new Point(x, y), Size = new Size(w, 25) };
            txtName.TextChanged += (s, e) => SetDirty();
            this.Controls.Add(txtName);

            y += 60;

            // 2. Настройки аудитории (Группировка)
            var grpRoom = new GroupBox() { Text = "Требования к помещению", Location = new Point(x, y), Size = new Size(w, 180) };
            this.Controls.Add(grpRoom);

            // Галочка компьютеров
            chkComputers = new CheckBox() { Text = "🖥 Требуются компьютеры (Лабораторная)", Location = new Point(20, 30), AutoSize = true, Parent = grpRoom };
            chkComputers.CheckedChanged += (s, e) => SetDirty();

            // Жесткая привязка
            new Label() { Text = "Жесткая привязка (только эта ауд.):", Location = new Point(20, 70), AutoSize = true, Parent = grpRoom };
            cmbFixedRoom = new ComboBox() { Location = new Point(250, 67), Size = new Size(150, 25), DropDownStyle = ComboBoxStyle.DropDownList, Parent = grpRoom };
            cmbFixedRoom.SelectedIndexChanged += (s, e) => SetDirty();

            // Запрет
            new Label() { Text = "Запрещенная аудитория (кроме этой):", Location = new Point(20, 110), AutoSize = true, Parent = grpRoom };
            cmbForbiddenRoom = new ComboBox() { Location = new Point(250, 107), Size = new Size(150, 25), DropDownStyle = ComboBoxStyle.DropDownList, Parent = grpRoom };
            cmbForbiddenRoom.SelectedIndexChanged += (s, e) => SetDirty();

            // Загружаем список комнат в выпадающие списки
            LoadRoomsToCombos();

            y += 200;

            // Кнопка сохранения
            btnSave = new Button()
            {
                Text = "💾 СОХРАНИТЬ ИЗМЕНЕНИЯ",
                Location = new Point(x, y),
                Size = new Size(w, 50),
                BackColor = Color.LightGreen,
                Font = new Font(Font.FontFamily, 12, FontStyle.Bold)
            };
            btnSave.Click += BtnSave_Click;
            this.Controls.Add(btnSave);

            lblStatus = new Label() { Location = new Point(x, y + 60), AutoSize = true, ForeColor = Color.Gray };
            this.Controls.Add(lblStatus);
        }

        // --- ЛОГИКА ---

        private void LoadRoomsToCombos()
        {
            try
            {
                var rooms = DataLoader.LoadRooms(); // Получаем список строк ["101", "102"...]

                cmbFixedRoom.Items.Clear();
                cmbFixedRoom.Items.Add(""); // Пустой вариант (нет привязки)
                foreach (var r in rooms) cmbFixedRoom.Items.Add(r);

                cmbForbiddenRoom.Items.Clear();
                cmbForbiddenRoom.Items.Add("");
                foreach (var r in rooms) cmbForbiddenRoom.Items.Add(r);
            }
            catch { }
        }

        private void LoadSubjectsList()
        {
            _isLoading = true;
            lstSubjects.Items.Clear();
            _subjects.Clear();
            _selectedSubject = null;

            using (var conn = DbHelper.GetConnection())
            {
                try
                {
                    conn.Open();
                    var cmd = new OleDbCommand("SELECT SubjectID, SubjectName FROM Subjects ORDER BY SubjectName", conn);
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            _subjects.Add(new SubjectDTO
                            {
                                Id = (int)r["SubjectID"],
                                Name = r["SubjectName"].ToString()
                            });
                        }
                    }
                }
                catch (Exception ex) { MessageBox.Show("Ошибка: " + ex.Message); }
            }

            foreach (var s in _subjects) lstSubjects.Items.Add(s);
            _isLoading = false;
            ResetDirty();
        }

        private void LstSubjects_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isLoading) return;

            // Если есть несохраненные изменения в старом
            if (_selectedSubject != null && _isDirty)
            {
                var newSel = lstSubjects.SelectedItem as SubjectDTO;
                if (newSel != _selectedSubject) // Если реально переключились
                {
                    var res = MessageBox.Show($"Сохранить изменения для '{_selectedSubject.Name}'?", "Вопрос", MessageBoxButtons.YesNoCancel);
                    if (res == DialogResult.Cancel)
                    {
                        _isLoading = true;
                        lstSubjects.SelectedItem = _selectedSubject; // Вернуть назад
                        _isLoading = false;
                        return;
                    }
                    if (res == DialogResult.Yes) SaveData();
                }
            }

            if (lstSubjects.SelectedIndex == -1) return;

            _selectedSubject = lstSubjects.SelectedItem as SubjectDTO;
            LoadSubjectDetails(_selectedSubject.Id);
        }

        private void LoadSubjectDetails(int id)
        {
            _isLoading = true;
            using (var conn = DbHelper.GetConnection())
            {
                try
                {
                    conn.Open();
                    var cmd = new OleDbCommand("SELECT * FROM Subjects WHERE SubjectID = ?", conn);
                    cmd.Parameters.AddWithValue("?", id);
                    using (var r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            txtName.Text = r["SubjectName"].ToString();
                            chkComputers.Checked = r["RequiresComputers"] != DBNull.Value && Convert.ToBoolean(r["RequiresComputers"]);

                            // Установка комбобоксов (если значения нет в списке, будет пусто)
                            string fix = r["FixedRoom"]?.ToString();
                            string forbid = r["ForbiddenRoom"]?.ToString();

                            cmbFixedRoom.SelectedItem = string.IsNullOrEmpty(fix) ? "" : fix;
                            cmbForbiddenRoom.SelectedItem = string.IsNullOrEmpty(forbid) ? "" : forbid;
                        }
                    }
                }
                catch { }
            }
            lblStatus.Text = $"Редактирование: {_selectedSubject.Name}";
            _isLoading = false;
            ResetDirty();
        }

        private void PrepareNewSubject()
        {
            if (_isDirty && MessageBox.Show("Сохранить изменения?", "Внимание", MessageBoxButtons.YesNo) == DialogResult.Yes)
                SaveData();

            lstSubjects.SelectedIndex = -1;
            _selectedSubject = null;

            _isLoading = true;
            txtName.Text = "";
            chkComputers.Checked = false;
            cmbFixedRoom.SelectedIndex = 0; // Пусто
            cmbForbiddenRoom.SelectedIndex = 0;
            _isLoading = false;

            ResetDirty();
            lblStatus.Text = "Новая дисциплина";
            txtName.Focus();
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            SaveData();
            LoadSubjectsList(); // Перезагрузить список, чтобы обновилось имя
        }

        private void SaveData()
        {
            string name = txtName.Text.Trim();
            if (string.IsNullOrEmpty(name)) { MessageBox.Show("Введите название!"); return; }

            string fix = cmbFixedRoom.SelectedItem?.ToString() ?? "";
            string forbid = cmbForbiddenRoom.SelectedItem?.ToString() ?? "";
            bool comp = chkComputers.Checked;

            using (var conn = DbHelper.GetConnection())
            {
                conn.Open();
                if (_selectedSubject == null)
                {
                    // INSERT
                    string sql = "INSERT INTO Subjects (SubjectName, RequiresComputers, FixedRoom, ForbiddenRoom) VALUES (?, ?, ?, ?)";
                    using (var cmd = new OleDbCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("?", name);
                        cmd.Parameters.AddWithValue("?", comp);
                        cmd.Parameters.AddWithValue("?", fix);
                        cmd.Parameters.AddWithValue("?", forbid);
                        cmd.ExecuteNonQuery();
                    }
                }
                else
                {
                    // UPDATE
                    string sql = "UPDATE Subjects SET SubjectName=?, RequiresComputers=?, FixedRoom=?, ForbiddenRoom=? WHERE SubjectID=?";
                    using (var cmd = new OleDbCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("?", name);
                        cmd.Parameters.AddWithValue("?", comp);
                        cmd.Parameters.AddWithValue("?", fix);
                        cmd.Parameters.AddWithValue("?", forbid);
                        cmd.Parameters.AddWithValue("?", _selectedSubject.Id);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            ResetDirty();
            MessageBox.Show("Сохранено!");
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            if (_selectedSubject == null) return;
            if (MessageBox.Show($"Удалить дисциплину '{_selectedSubject.Name}'?", "Удаление", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                using (var conn = DbHelper.GetConnection())
                {
                    conn.Open();
                    // ВАЖНО: Удаление предмета может сломать план, если нет каскада.
                    // Пока удаляем только сам предмет.
                    new OleDbCommand($"DELETE FROM Subjects WHERE SubjectID={_selectedSubject.Id}", conn).ExecuteNonQuery();
                }
                LoadSubjectsList();
                PrepareNewSubject();
            }
        }

        private void SetDirty()
        {
            if (_isLoading) return;
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
    }
}