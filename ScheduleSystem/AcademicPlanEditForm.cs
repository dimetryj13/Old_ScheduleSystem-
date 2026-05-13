using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ScheduleSystem.Data;
using ScheduleSystem.Models;

namespace ScheduleSystem
{
    public partial class AcademicPlanEditForm : Form
    {
        // Данные
        private DataTable _dtSubjects;
        private DataTable _dtTeachers;
        private DataTable _dtCurators;

        // UI Элементы
        private DataGridView gridPlan;
        private Panel pnlTools;  // Боковая панель (Справа)
        private Panel pnlHeader; // Информационная панель (Теперь СНИЗУ)

        // Элементы информации
        private Label lblTitle;
        private Label lblGroupInfo;

        // Элементы управления (Боковая панель)
        private NumericUpDown numYear;
        private ComboBox cmbSemester;
        private NumericUpDown numCourse;
        private Button btnActualGroups;
        private Button btnArchiveGroups;
        private ComboBox cmbGroups;
        private ComboBox cmbCurators;
        private Label lblToolStudentCount;
        private Button btnManualCount;

        // Кнопки действий
        private Button btnAddRow;
        private Button btnDelRow;
        private Button btnReload;
        private Button btnSave;

        // Шрифты
        private Font _docFont = new Font("Times New Roman", 14, FontStyle.Regular);

        public AcademicPlanEditForm()
        {
            this.Text = "Редактор учебного плана";
            this.Size = new Size(1400, 850);
            this.WindowState = FormWindowState.Maximized;

            LoadReferenceData();
            SetupUI();
            LoadGroups(true);
        }

        private void AcademicPlanEditForm_Load(object sender, EventArgs e) { }

        // --- 1. ЗАГРУЗКА СПРАВОЧНИКОВ ---
        private void LoadReferenceData()
        {
            using (var conn = DbHelper.GetConnection())
            {
                conn.Open();
                _dtSubjects = new DataTable();
                new OleDbDataAdapter("SELECT SubjectID, SubjectName FROM Subjects ORDER BY SubjectName", conn).Fill(_dtSubjects);

                _dtTeachers = new DataTable();
                new OleDbDataAdapter("SELECT TeacherID, FullName FROM Teachers ORDER BY FullName", conn).Fill(_dtTeachers);

                DataRow emptyRow = _dtTeachers.NewRow();
                emptyRow["TeacherID"] = 0;
                emptyRow["FullName"] = "- Нет -";
                _dtTeachers.Rows.InsertAt(emptyRow, 0);

                _dtCurators = _dtTeachers.Copy();
            }
        }

        // --- 2. ЗАГРУЗКА ГРУПП ---
        private void LoadGroups(bool isActual)
        {
            cmbGroups.Items.Clear();
            var groups = DataLoader.LoadGroups(isActual);
            foreach (var g in groups) cmbGroups.Items.Add(g);

            if (isActual) { btnActualGroups.BackColor = Color.LightGreen; btnArchiveGroups.BackColor = SystemColors.Control; }
            else { btnActualGroups.BackColor = SystemColors.Control; btnArchiveGroups.BackColor = Color.LightCoral; }

            if (cmbGroups.Items.Count > 0) cmbGroups.SelectedIndex = 0;
        }

        // --- 3. ЗАГРУЗКА ПЛАНА ---
        private void LoadPlanData()
        {
            if (cmbGroups.SelectedItem == null) return;
            var group = (Group)cmbGroups.SelectedItem;

            // Фильтры
            int year = (int)numYear.Value;
            int course = (int)numCourse.Value;
            bool isAutumn = cmbSemester.SelectedIndex == 0;
            int targetSemester = (course - 1) * 2 + (isAutumn ? 1 : 2);

            UpdateHeaderTitle(year, isAutumn ? "осенний" : "весенний");

            string curatorName = "Не назначен";
            if (group.CuratorId > 0)
            {
                DataRow[] rows = _dtTeachers.Select($"TeacherID = {group.CuratorId}");
                if (rows.Length > 0) curatorName = rows[0]["FullName"].ToString();
            }

            lblGroupInfo.Text = $"Группа: {group.Name}   Курс: {course}   Семестр: {targetSemester}\n" +
                                $"Куратор: {curatorName}\n" +
                                $"Количество студентов: {group.StudentCount}";

            lblToolStudentCount.Text = group.StudentCount.ToString();

            // Сбрасываем подписку перед установкой значения, чтобы не триггерить сохранение
            cmbCurators.SelectedIndexChanged -= CmbCurators_SelectedIndexChanged;
            cmbCurators.SelectedValue = group.CuratorId;
            cmbCurators.SelectedIndexChanged += CmbCurators_SelectedIndexChanged;

            // Загружаем таблицу
            gridPlan.Rows.Clear();

            using (var conn = DbHelper.GetConnection())
            {
                conn.Open();
                string sql = @"SELECT * FROM AcademicPlan WHERE Trim(GroupID) = ? AND Semester = ?";

                using (var cmd = new OleDbCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("?", group.Name.Trim());
                    cmd.Parameters.AddWithValue("?", targetSemester);

                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            int idx = gridPlan.Rows.Add();
                            DataGridViewRow row = gridPlan.Rows[idx];

                            row.Cells["colSubject"].Value = r["SubjectID"];
                            row.Cells["colLecHours"].Value = r["LectureInWeek"];
                            row.Cells["colPracHours"].Value = r["PracticeInWeek"];
                            row.Cells["colLabHours"].Value = r["LabsInWeek"];
                            row.Cells["colTotal"].Value = r["Hours"];

                            row.Cells["colLecTeacher"].Value = r["LectureTeacher"] == DBNull.Value ? 0 : r["LectureTeacher"];
                            row.Cells["colPracTeacher"].Value = r["PracticeTeacher"] == DBNull.Value ? 0 : r["PracticeTeacher"];
                        }
                    }
                }
            }
        }

        // --- ЛОГИКА СОХРАНЕНИЯ И УПРАВЛЕНИЯ (Без изменений) ---
        private void CmbCurators_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbGroups.SelectedItem == null) return;
            var group = (Group)cmbGroups.SelectedItem;

            if (cmbCurators.SelectedValue != null && int.TryParse(cmbCurators.SelectedValue.ToString(), out int newCuratorId))
            {
                DataLoader.UpdateGroupInfo(group.Id, group.StudentCount, newCuratorId);
                group.CuratorId = newCuratorId;
                LoadPlanData();
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (cmbGroups.SelectedItem == null) return;
            var group = (Group)cmbGroups.SelectedItem;

            int course = (int)numCourse.Value;
            bool isAutumn = cmbSemester.SelectedIndex == 0;
            int targetSemester = (course - 1) * 2 + (isAutumn ? 1 : 2);

            try
            {
                using (var conn = DbHelper.GetConnection())
                {
                    conn.Open();
                    var trans = conn.BeginTransaction();

                    try
                    {
                        string delSql = "DELETE FROM AcademicPlan WHERE GroupID = ? AND Semester = ?";
                        using (var cmdDel = new OleDbCommand(delSql, conn, trans))
                        {
                            cmdDel.Parameters.AddWithValue("?", group.Name);
                            cmdDel.Parameters.AddWithValue("?", targetSemester);
                            cmdDel.ExecuteNonQuery();
                        }

                        string insSql = @"INSERT INTO AcademicPlan 
                                          (GroupID, SubjectID, Hours, LectureInWeek, PracticeInWeek, LabsInWeek, 
                                           LectureTeacher, PracticeTeacher, Semester) 
                                          VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)";

                        foreach (DataGridViewRow row in gridPlan.Rows)
                        {
                            if (row.IsNewRow) continue;
                            if (row.Cells["colSubject"].Value == null) continue;

                            using (var cmdIns = new OleDbCommand(insSql, conn, trans))
                            {
                                cmdIns.Parameters.AddWithValue("?", group.Name);
                                cmdIns.Parameters.AddWithValue("?", row.Cells["colSubject"].Value);
                                cmdIns.Parameters.AddWithValue("?", ToInt(row.Cells["colTotal"].Value));
                                cmdIns.Parameters.AddWithValue("?", ToInt(row.Cells["colLecHours"].Value));
                                cmdIns.Parameters.AddWithValue("?", ToInt(row.Cells["colPracHours"].Value));
                                cmdIns.Parameters.AddWithValue("?", ToInt(row.Cells["colLabHours"].Value));
                                cmdIns.Parameters.AddWithValue("?", ToInt(row.Cells["colLecTeacher"].Value));
                                cmdIns.Parameters.AddWithValue("?", ToInt(row.Cells["colPracTeacher"].Value));
                                cmdIns.Parameters.AddWithValue("?", targetSemester);
                                cmdIns.ExecuteNonQuery();
                            }
                        }
                        trans.Commit();
                        MessageBox.Show("План успешно сохранен!", "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        trans.Rollback();
                        throw ex;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при сохранении: " + ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnAddRow_Click(object sender, EventArgs e)
        {
            gridPlan.Rows.Add();
        }

        private void BtnDelRow_Click(object sender, EventArgs e)
        {
            if (gridPlan.SelectedRows.Count > 0)
            {
                if (gridPlan.SelectedRows[0].IsNewRow) return;
                if (MessageBox.Show("Удалить выбранную строку?", "Удаление", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    gridPlan.Rows.RemoveAt(gridPlan.SelectedRows[0].Index);
                }
            }
            else
            {
                MessageBox.Show("Выберите строку для удаления (кликните на стрелочку слева).");
            }
        }

        private void BtnManualCount_Click(object sender, EventArgs e)
        {
            if (cmbGroups.SelectedItem == null) return;
            var group = (Group)cmbGroups.SelectedItem;

            string input = ShowInputDialog("Введите количество студентов:", "Изменение", group.StudentCount.ToString());

            if (int.TryParse(input, out int newCount))
            {
                DataLoader.UpdateGroupInfo(group.Id, newCount, group.CuratorId);
                group.StudentCount = newCount;
                lblToolStudentCount.Text = newCount.ToString();
                LoadPlanData();
            }
        }

        private string ShowInputDialog(string text, string caption, string defaultValue)
        {
            Form prompt = new Form()
            {
                Width = 350,
                Height = 160,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = caption,
                StartPosition = FormStartPosition.CenterScreen,
                MinimizeBox = false,
                MaximizeBox = false
            };
            Label textLabel = new Label() { Left = 20, Top = 20, Text = text, AutoSize = true };
            TextBox textBox = new TextBox() { Left = 20, Top = 45, Width = 290, Text = defaultValue };
            Button confirmation = new Button() { Text = "ОК", Left = 210, Width = 100, Top = 80, DialogResult = DialogResult.OK };
            prompt.Controls.Add(textLabel); prompt.Controls.Add(textBox); prompt.Controls.Add(confirmation);
            prompt.AcceptButton = confirmation;
            return prompt.ShowDialog() == DialogResult.OK ? textBox.Text : "";
        }

        private int ToInt(object val)
        {
            if (val == null || val == DBNull.Value) return 0;
            if (int.TryParse(val.ToString(), out int res)) return res;
            return 0;
        }

        private void UpdateHeaderTitle(int year, string season)
        {
            lblTitle.Text = $"Перечень учебных дисциплин на {season} семестр {year}-{year + 1} учебный год";
        }

        // ==========================================
        // UI НАСТРОЙКА (ПАНЕЛЬ ПЕРЕНЕСЕНА ВНИЗ)
        // ==========================================
        private void SetupUI()
        {
            // 1. Боковая панель (Справа)
            pnlTools = new Panel();
            pnlTools.Dock = DockStyle.Right;
            pnlTools.Width = 300;
            pnlTools.BackColor = Color.FromArgb(230, 230, 230);
            pnlTools.Padding = new Padding(10);
            this.Controls.Add(pnlTools);

            // 2. Информационная панель (ТЕПЕРЬ СНИЗУ)
            pnlHeader = new Panel();
            pnlHeader.Dock = DockStyle.Bottom; // <--- ИЗМЕНЕНИЕ: Было Top, стало Bottom
            pnlHeader.Height = 130;
            pnlHeader.BackColor = Color.WhiteSmoke; // Светлый фон, чтобы отличался от таблицы
            pnlHeader.BorderStyle = BorderStyle.FixedSingle;
            this.Controls.Add(pnlHeader);

            // 3. Таблица (Заполняет все оставшееся место)
            gridPlan = new DataGridView();
            gridPlan.Dock = DockStyle.Fill;
            gridPlan.BackgroundColor = Color.White;
            gridPlan.AllowUserToAddRows = false;
            gridPlan.AllowUserToDeleteRows = false;
            gridPlan.RowHeadersVisible = true;
            gridPlan.AutoGenerateColumns = false;
            gridPlan.EditMode = DataGridViewEditMode.EditOnEnter;
            this.Controls.Add(gridPlan);

            // Вывод на передний план (важно для корректной стыковки)
            pnlTools.BringToFront();
            pnlHeader.BringToFront();

            // --- КОЛОНКИ ТАБЛИЦЫ ---
            var colSubj = new DataGridViewComboBoxColumn();
            colSubj.Name = "colSubject";
            colSubj.HeaderText = "Дисциплина";
            colSubj.DataSource = _dtSubjects;
            colSubj.DisplayMember = "SubjectName";
            colSubj.ValueMember = "SubjectID";
            colSubj.Width = 300;
            colSubj.FlatStyle = FlatStyle.Flat;
            gridPlan.Columns.Add(colSubj);

            AddTextCol("colLecHours", "Лек (пар)", 70);
            AddTextCol("colPracHours", "Прак (пар)", 70);
            AddTextCol("colLabHours", "Лаб (пар)", 70);
            AddTextCol("colTotal", "Всего ч.", 70);

            var colLecT = new DataGridViewComboBoxColumn();
            colLecT.Name = "colLecTeacher";
            colLecT.HeaderText = "Лектор";
            colLecT.DataSource = _dtTeachers;
            colLecT.DisplayMember = "FullName";
            colLecT.ValueMember = "TeacherID";
            colLecT.Width = 200;
            colLecT.FlatStyle = FlatStyle.Flat;
            gridPlan.Columns.Add(colLecT);

            var colPracT = new DataGridViewComboBoxColumn();
            colPracT.Name = "colPracTeacher";
            colPracT.HeaderText = "Практик";
            colPracT.DataSource = _dtTeachers.Copy();
            colPracT.DisplayMember = "FullName";
            colPracT.ValueMember = "TeacherID";
            colPracT.Width = 200;
            colPracT.FlatStyle = FlatStyle.Flat;
            gridPlan.Columns.Add(colPracT);

            // --- ЗАПОЛНЕНИЕ БОКОВОЙ ПАНЕЛИ ---
            int y = 10; int w = 270;

            pnlTools.Controls.Add(new Label { Text = "Учебный год:", Location = new Point(10, y), AutoSize = true });
            y += 20;
            numYear = new NumericUpDown { Location = new Point(10, y), Size = new Size(w, 25), Minimum = 2020, Maximum = 2035, Value = 2026 };
            numYear.ValueChanged += (s, e) => LoadPlanData();
            pnlTools.Controls.Add(numYear);

            y += 45;
            pnlTools.Controls.Add(new Label { Text = "Семестр:", Location = new Point(10, y), AutoSize = true });
            y += 20;
            cmbSemester = new ComboBox { Location = new Point(10, y), Size = new Size(w, 25), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbSemester.Items.AddRange(new string[] { "Осенний", "Весенний" });
            cmbSemester.SelectedIndex = 0;
            cmbSemester.SelectedIndexChanged += (s, e) => LoadPlanData();
            pnlTools.Controls.Add(cmbSemester);

            y += 45;
            pnlTools.Controls.Add(new Label { Text = "Курс:", Location = new Point(10, y), AutoSize = true });
            y += 20;
            numCourse = new NumericUpDown { Location = new Point(10, y), Size = new Size(w, 25), Minimum = 1, Maximum = 6, Value = 1 };
            numCourse.ValueChanged += (s, e) => LoadPlanData();
            pnlTools.Controls.Add(numCourse);

            y += 45;
            btnActualGroups = new Button { Text = "Актуальные", Location = new Point(10, y), Size = new Size(130, 30), BackColor = Color.LightGreen };
            btnActualGroups.Click += (s, e) => LoadGroups(true);
            pnlTools.Controls.Add(btnActualGroups);

            btnArchiveGroups = new Button { Text = "Архив", Location = new Point(150, y), Size = new Size(130, 30) };
            btnArchiveGroups.Click += (s, e) => LoadGroups(false);
            pnlTools.Controls.Add(btnArchiveGroups);

            y += 40;
            pnlTools.Controls.Add(new Label { Text = "---------------------------------------------", Location = new Point(10, y), ForeColor = Color.Gray });

            y += 20;
            pnlTools.Controls.Add(new Label { Text = "Группа:", Location = new Point(10, y), AutoSize = true });
            y += 20;
            cmbGroups = new ComboBox { Location = new Point(10, y), Size = new Size(w, 25), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbGroups.SelectedIndexChanged += (s, e) => LoadPlanData();
            pnlTools.Controls.Add(cmbGroups);

            y += 45;
            pnlTools.Controls.Add(new Label { Text = "Куратор (выберите для сохранения):", Location = new Point(10, y), AutoSize = true });
            y += 20;
            cmbCurators = new ComboBox { Location = new Point(10, y), Size = new Size(w, 25), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbCurators.DataSource = _dtCurators;
            cmbCurators.DisplayMember = "FullName";
            cmbCurators.ValueMember = "TeacherID";
            pnlTools.Controls.Add(cmbCurators);

            y += 45;
            pnlTools.Controls.Add(new Label { Text = "Студентов (БД):", Location = new Point(10, y), AutoSize = true });
            y += 20;
            lblToolStudentCount = new Label { Text = "0", Location = new Point(10, y + 5), Font = new Font("Arial", 10, FontStyle.Bold), AutoSize = true };
            pnlTools.Controls.Add(lblToolStudentCount);

            btnManualCount = new Button { Text = "Вручную", Location = new Point(150, y), Size = new Size(130, 30) };
            btnManualCount.Click += BtnManualCount_Click;
            pnlTools.Controls.Add(btnManualCount);

            y += 60;
            btnAddRow = new Button { Text = "➕ Добавить строку", Location = new Point(10, y), Size = new Size(w, 35), BackColor = Color.AliceBlue };
            btnAddRow.Click += BtnAddRow_Click;
            pnlTools.Controls.Add(btnAddRow);

            y += 40;
            btnDelRow = new Button { Text = "➖ Удалить строку", Location = new Point(10, y), Size = new Size(w, 35), BackColor = Color.MistyRose };
            btnDelRow.Click += BtnDelRow_Click;
            pnlTools.Controls.Add(btnDelRow);

            // Кнопки внизу панели
            y = pnlTools.Height - 120;
            btnReload = new Button { Text = "🔄 Обновить", Location = new Point(10, y), Size = new Size(130, 40) };
            btnReload.Click += (s, e) => LoadPlanData();
            btnReload.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            pnlTools.Controls.Add(btnReload);

            btnSave = new Button { Text = "💾 Сохранить план", Location = new Point(150, y), Size = new Size(130, 40), BackColor = Color.LightGreen };
            btnSave.Click += BtnSave_Click;
            btnSave.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            pnlTools.Controls.Add(btnSave);

            // --- ЗАПОЛНЕНИЕ НИЖНЕГО ЗАГОЛОВКА (HEADER ТЕПЕРЬ FOOTER) ---
            lblTitle = new Label { Location = new Point(15, 10), AutoSize = true, Font = _docFont, BackColor = Color.Yellow, Padding = new Padding(3) };
            pnlHeader.Controls.Add(lblTitle);

            lblGroupInfo = new Label { Location = new Point(15, 45), AutoSize = true, Font = new Font("Arial", 11, FontStyle.Regular), Height = 80 };
            pnlHeader.Controls.Add(lblGroupInfo);
        }

        private void AddTextCol(string name, string header, int w)
        {
            var col = new DataGridViewTextBoxColumn();
            col.Name = name;
            col.HeaderText = header;
            col.Width = w;
            gridPlan.Columns.Add(col);
        }
    }
}