using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ScheduleSystem.Data;
using ScheduleSystem.Models;
using ScheduleSystem.Logic;

namespace ScheduleSystem
{
    public partial class ScheduleMatrixForm : Form
    {
        private List<Group> _sortedGroups;
        private ScheduleRepository _repo = new ScheduleRepository();
        private AnalyticsService _analytics = new AnalyticsService();
        private ToolTip _infoTooltip;

        // --- Error Handling State ---
        private ToolStripButton _btnShowErrors;
        private List<string> _lastErrors = new List<string>();
        private OptimizationResultForm _openedErrorForm = null;
        // ----------------------------

        private object _currentTooltipData = null;
        private int _lastHoverRow = -1;
        private int _lastHoverCol = -1;

        // Zooming
        private float _zoomFactor = 1.0f;
        private const float MIN_ZOOM = 0.5f;
        private const float MAX_ZOOM = 2.0f;

        // Fonts
        private readonly Font _baseFontHeader = new Font("Times New Roman", 10, FontStyle.Bold);
        private readonly Font _baseFontDay = new Font("Times New Roman", 12, FontStyle.Bold);
        private readonly Font _baseFontPair = new Font("Times New Roman", 14, FontStyle.Bold);
        private readonly Font _baseFontContent = new Font("Times New Roman", 9, FontStyle.Regular);
        private readonly Font _tooltipFont = new Font("Consolas", 9, FontStyle.Regular);

        private Font _currFontHeader, _currFontDay, _currFontPair, _currFontContent;
        private Pen _thinPen = new Pen(Color.Gray, 1);
        private Pen _thickPen = new Pen(Color.Black, 3);
        private Pen _gridPen = new Pen(Color.Gray, 1);

        public ScheduleMatrixForm()
        {
            InitializeComponent();
            UpdateZoomFonts();

            this.Load += ScheduleMatrixForm_Load;

            _infoTooltip = new ToolTip();
            _infoTooltip.OwnerDraw = true;
            _infoTooltip.Popup += _infoTooltip_Popup;
            _infoTooltip.Draw += _infoTooltip_Draw;
            _infoTooltip.AutoPopDelay = 30000;
            _infoTooltip.InitialDelay = 100;
            _infoTooltip.ReshowDelay = 100;
            _infoTooltip.UseAnimation = false;
            _infoTooltip.UseFading = false;

            // Double buffering for smooth rendering
            typeof(DataGridView).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.SetProperty,
                null, dataGridView1, new object[] { true });

            dataGridView1.MouseWheel += DataGridView1_MouseWheel;
            dataGridView1.CellMouseEnter += DataGridView1_CellMouseEnter;
            dataGridView1.CellMouseLeave += DataGridView1_CellMouseLeave;

            // Add Painting events
            dataGridView1.CellPainting += DataGridView1_CellPainting;
            dataGridView1.Paint += DataGridView1_Paint;
            dataGridView1.Scroll += (s, ev) => dataGridView1.Invalidate();

            // Manual Edit Event
            dataGridView1.CellDoubleClick += DataGridView1_CellDoubleClick;
        }

        private void ScheduleMatrixForm_Load(object sender, EventArgs e)
        {
            SetupUI();
            LoadAndSortGroups();
            BuildColumns();
            BuildRows();
            ConfigureGrid();

            try { _analytics.ReloadStats(); } catch { }

            LoadScheduleFromDb();
        }

        private void SetupUI()
        {
            foreach (Control c in this.Controls.OfType<ToolStrip>().ToList()) this.Controls.Remove(c);
            var toolStrip = new ToolStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden };

            var btnGen = new ToolStripButton("⚙️ Генерировать");
            btnGen.Click += (s, e) =>
            {
                var form = new GeneratorSettingsForm(new GeneratorSettings());
                form.ShowDialog();
                _analytics.ReloadStats();
                LoadScheduleFromDb();

                // Clear errors on new generation attempt (assuming GeneratorSettingsForm handles logic)
                _lastErrors.Clear();
                UpdateErrorButtonState();
            };

            var btnClear = new ToolStripButton("🗑 Очистить");
            btnClear.Click += (s, e) => {
                if (MessageBox.Show("Удалить всё?", "Очистка", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    using (var conn = DbHelper.GetConnection())
                    {
                        conn.Open();
                        new System.Data.OleDb.OleDbCommand("DELETE FROM Schedule", conn).ExecuteNonQuery();
                    }
                    try { _analytics.ReloadStats(); } catch { }
                    _lastErrors.Clear();
                    UpdateErrorButtonState();
                    LoadScheduleFromDb();
                }
            };
            // --- ВСТАВКА НАЧАЛО ---
            var btnExport = new ToolStripButton("📊 Экспорт в Excel");
            btnExport.Click += BtnExport_Click; // Метод напишем ниже
            // --- ВСТАВКА КОНЕЦ ---

            _btnShowErrors = new ToolStripButton("⚠️ Ошибки");
            _btnShowErrors.ForeColor = Color.Red;
            _btnShowErrors.Visible = false;
            _btnShowErrors.Click += (s, e) => ShowErrorWindow();

            var btnTeachers = new ToolStripButton("👥 Преподаватели"); btnTeachers.Click += (s, e) => new TeachersEditForm().ShowDialog();
            var btnSubjects = new ToolStripButton("📚 Дисциплины"); btnSubjects.Click += (s, e) => new SubjectsEditForm().ShowDialog();
            var btnPlan = new ToolStripButton("📅 Учебный план"); btnPlan.Click += (s, e) => new AcademicPlanEditForm().Show();
            var btnRooms = new ToolStripButton("🚪 Аудитории"); btnRooms.Click += (s, e) => new RoomsEditForm().ShowDialog();
            var btnRefresh = new ToolStripButton("🔄 Обновить"); btnRefresh.Click += (s, e) => LoadScheduleFromDb();

            toolStrip.Items.Add(btnGen);
            toolStrip.Items.Add(_btnShowErrors);
            toolStrip.Items.Add(new ToolStripSeparator());
            toolStrip.Items.Add(btnClear);
            toolStrip.Items.Add(btnExport);
            toolStrip.Items.Add(new ToolStripSeparator());
            toolStrip.Items.Add(btnRefresh);
            toolStrip.Items.Add(new ToolStripSeparator());
            toolStrip.Items.Add(btnTeachers);
            toolStrip.Items.Add(btnSubjects);
            toolStrip.Items.Add(btnPlan);
            toolStrip.Items.Add(btnRooms);

            this.Controls.Add(toolStrip);

            dataGridView1.Parent = this;
            dataGridView1.Dock = DockStyle.Fill;
            dataGridView1.BringToFront();
        }

        private void UpdateErrorButtonState()
        {
            if (_lastErrors != null && _lastErrors.Count > 0)
            {
                _btnShowErrors.Visible = true;
                _btnShowErrors.Text = $"⚠️ Ошибки ({_lastErrors.Count})";
            }
            else
            {
                _btnShowErrors.Visible = false;
                if (_openedErrorForm != null && !_openedErrorForm.IsDisposed)
                    _openedErrorForm.Close();
            }
        }

        private void ShowErrorWindow()
        {
            if (_lastErrors == null || _lastErrors.Count == 0) return;
            if (_openedErrorForm != null && !_openedErrorForm.IsDisposed)
            {
                _openedErrorForm.BringToFront();
                return;
            }
            _openedErrorForm = new OptimizationResultForm(_lastErrors);
            _openedErrorForm.Show(this);
        }

        // --- FIX: MANUAL EDIT MODE ---
        private void DataGridView1_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 2) return;

            int groupIdx = (e.ColumnIndex - 2) / 2;
            if (groupIdx < 0 || groupIdx >= _sortedGroups.Count) return;
            var group = _sortedGroups[groupIdx];

            // Calculate Day (0-5)
            int dayIndex = e.RowIndex / 12;
            string[] days = { "Понедельник", "Вторник", "Среда", "Четверг", "Пятница", "Суббота" };
            string dayName = days[dayIndex];

            // Calculate Pair (1-6)
            int rowInDay = e.RowIndex % 12;
            int pairNum = (rowInDay / 2) + 1;

            // Calculate Week (1=Light, 2=Dark)
            int weekType = (e.RowIndex % 2 == 0) ? 1 : 2;
            string weekName = weekType == 1 ? "Светлая неделя" : "Темная неделя";

            // Get Current Values
            string currentSubj = "", currentTeach = "", currentRoom = "";
            int colSubj = 2 + (groupIdx * 2);
            var valSubj = dataGridView1.Rows[e.RowIndex].Cells[colSubj].Value;
            if (valSubj != null)
            {
                var parts = valSubj.ToString().Split('\n');
                currentSubj = parts[0].Trim();
                if (parts.Length > 1) currentTeach = parts[1].Trim(' ', '(', ')');
            }
            int colRoom = colSubj + 1;
            var valRoom = dataGridView1.Rows[e.RowIndex].Cells[colRoom].Value;
            if (valRoom != null) currentRoom = valRoom.ToString();

            // Open Form
            using (AddLessonForm form = new AddLessonForm(group.Name, dayName, pairNum, weekName, currentSubj, currentTeach, currentRoom))
            {
                if (form.ShowDialog() == DialogResult.OK && form.IsSaved)
                {
                    // FIX: Save with correct WeekType
                    _repo.SaveLesson(group.Name, dayIndex + 1, pairNum, weekType, form.SelectedSubject, form.SelectedTeacher, form.SelectedRoom, form.SelectedType);

                    try { _analytics.ReloadStats(); } catch { }

                    // FIX: Immediate Refresh
                    LoadScheduleFromDb();
                }
            }
        }

        private void LoadAndSortGroups()
        {
            var allGroups = DataLoader.LoadGroups(true);
            _sortedGroups = allGroups.Where(g => SpecialtyHelper.GetForm(g.Name) == EducationForm.FullTime)
                .OrderBy(g => SpecialtyHelper.GetLevel(g.Name) == EducationLevel.Master ? 1 : 0)
                .ThenByDescending(g => GetYearDigit(g.Name))
                .ThenBy(g => g.Name).ToList();
        }

        private int GetYearDigit(string g) { try { return int.Parse(g.Split('-')[1].Substring(1, 1)); } catch { return 0; } }

        private void BuildColumns()
        {
            dataGridView1.Columns.Clear();

            // 1. Создаем фиксированные колонки
            int idxDay = dataGridView1.Columns.Add("colDay", "День");
            int idxPair = dataGridView1.Columns.Add("colPair", "Пара");

            // 2. Настраиваем ширину и заморозку
            dataGridView1.Columns[idxDay].Width = (int)(40 * _zoomFactor);
            dataGridView1.Columns[idxDay].Frozen = true;

            dataGridView1.Columns[idxPair].Width = (int)(50 * _zoomFactor);
            dataGridView1.Columns[idxPair].Frozen = true;

            // 3. ГЛАВНОЕ ИСПРАВЛЕНИЕ: Блокируем сортировку для первых двух колонок
            dataGridView1.Columns[idxDay].SortMode = DataGridViewColumnSortMode.NotSortable;
            dataGridView1.Columns[idxPair].SortMode = DataGridViewColumnSortMode.NotSortable;

            // 4. Создаем колонки групп
            foreach (var g in _sortedGroups)
            {
                int i = dataGridView1.Columns.Add($"g_{g.Name}", g.Name);
                dataGridView1.Columns[i].Width = (int)(170 * _zoomFactor);
                // Для групп сортировка уже была отключена, оставляем как есть
                dataGridView1.Columns[i].SortMode = DataGridViewColumnSortMode.NotSortable;

                int j = dataGridView1.Columns.Add($"r_{g.Name}", "Ауд.");
                dataGridView1.Columns[j].Width = (int)(45 * _zoomFactor);
                dataGridView1.Columns[j].SortMode = DataGridViewColumnSortMode.NotSortable;
            }

            dataGridView1.ColumnHeadersDefaultCellStyle.Font = _currFontHeader;
            dataGridView1.ColumnHeadersHeight = (int)(50 * _zoomFactor);
        }
        private void BuildRows()
        {
            dataGridView1.Rows.Clear();
            string[] days = { "ПОНЕДЕЛЬНИК", "ВТОРНИК", "СРЕДА", "ЧЕТВЕРГ", "ПЯТНИЦА", "СУББОТА" };
            int h = (int)(50 * _zoomFactor);
            foreach (var d in days)
                for (int p = 1; p <= 6; p++)
                {
                    int r1 = dataGridView1.Rows.Add(d, p); dataGridView1.Rows[r1].Height = h;
                    int r2 = dataGridView1.Rows.Add(d, p); dataGridView1.Rows[r2].Height = h;
                    dataGridView1.Rows[r2].DefaultCellStyle.BackColor = Color.FromArgb(245, 245, 245);
                }
        }

        private void LoadScheduleFromDb()
        {
            // Очистка ячеек
            foreach (DataGridViewRow row in dataGridView1.Rows)
            {
                for (int i = 2; i < row.Cells.Count; i++)
                {
                    row.Cells[i].Value = null;
                    row.Cells[i].Style.BackColor = (row.Index % 2 == 0) ? Color.White : Color.FromArgb(245, 245, 245);
                }
            }

            // Читаем из базы напрямую (так как Repository удален)
            var schedule = new List<ScheduleItem>();
            using (var conn = DbHelper.GetConnection())
            {
                try
                {
                    conn.Open();
                    var cmd = new System.Data.OleDb.OleDbCommand("SELECT * FROM Schedule", conn);
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            schedule.Add(new ScheduleItem
                            {
                                GroupName = r["GroupName"].ToString(),
                                DayOfWeek = Convert.ToInt32(r["DayOfWeek"]),
                                PairNumber = Convert.ToInt32(r["PairNumber"]),
                                WeekType = Convert.ToInt32(r["WeekType"]),
                                SubjectName = r["SubjectName"].ToString(),
                                TeacherName = r["TeacherName"].ToString(),
                                RoomNumber = r["RoomNumber"].ToString(),
                                LessonType = Convert.ToInt32(r["LessonType"])
                            });
                        }
                    }
                }
                catch { }
            }

            // Заполняем сетку (без изменений)
            foreach (var lesson in schedule)
            {
                int groupIndex = _sortedGroups.FindIndex(g => g.Name.Trim() == lesson.GroupName.Trim());
                if (groupIndex == -1) continue;

                int baseRow = (lesson.DayOfWeek - 1) * 12 + (lesson.PairNumber - 1) * 2;
                List<int> targetRows = new List<int>();

                if (lesson.WeekType == 0) { targetRows.Add(baseRow); targetRows.Add(baseRow + 1); }
                else if (lesson.WeekType == 1) targetRows.Add(baseRow);
                else if (lesson.WeekType == 2) targetRows.Add(baseRow + 1);

                foreach (int rowIndex in targetRows)
                {
                    if (rowIndex >= 0 && rowIndex < dataGridView1.Rows.Count)
                    {
                        int colSubj = 2 + (groupIndex * 2);
                        int colRoom = colSubj + 1;
                        var cell = dataGridView1.Rows[rowIndex].Cells[colSubj];
                        cell.Value = $"{lesson.SubjectName}\n({lesson.TeacherName})";

                        if (lesson.LessonType == 0) cell.Style.BackColor = Color.LightYellow;
                        else if (lesson.LessonType == 2) cell.Style.BackColor = Color.LightCyan;
                        else cell.Style.BackColor = Color.Honeydew;

                        dataGridView1.Rows[rowIndex].Cells[colRoom].Value = lesson.RoomNumber;
                    }
                }
            }
        }
        private void ConfigureGrid()
        {
            dataGridView1.RowHeadersVisible = false;
            dataGridView1.AllowUserToResizeRows = false;
            dataGridView1.AllowUserToResizeColumns = false;
            dataGridView1.CellBorderStyle = DataGridViewCellBorderStyle.None;
            dataGridView1.BackgroundColor = Color.White;
            dataGridView1.DefaultCellStyle.SelectionBackColor = Color.FromArgb(220, 230, 240);
            dataGridView1.DefaultCellStyle.SelectionForeColor = Color.Black;
            dataGridView1.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            dataGridView1.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dataGridView1.DefaultCellStyle.Font = _currFontContent;
            dataGridView1.ShowCellToolTips = false;
        }

        // --- DRAWING & SCALING ---
        private void DataGridView1_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            bool isHeader = (e.ColumnIndex < 2);
            e.PaintBackground(e.CellBounds, !isHeader);
            e.Graphics.DrawLine(_gridPen, e.CellBounds.Right - 1, e.CellBounds.Top, e.CellBounds.Right - 1, e.CellBounds.Bottom);
            e.Graphics.DrawLine(_gridPen, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1);
            if (isHeader) e.Handled = true; else { e.PaintContent(e.CellBounds); e.Handled = true; }
        }

        private void DataGridView1_Paint(object sender, PaintEventArgs e)
        {
            DrawMergedText(e.Graphics);
        }

        private void DrawMergedText(Graphics g)
        {
            int firstRow = dataGridView1.FirstDisplayedScrollingRowIndex;
            if (firstRow < 0) return;
            int lastRow = firstRow + dataGridView1.DisplayedRowCount(true);

            int startDayBlock = (firstRow / 12) * 12;
            for (int i = startDayBlock; i <= lastRow; i += 12)
            {
                if (i >= dataGridView1.Rows.Count) break;
                Rectangle rectDay = GetMergedRectangle(0, i, 12);
                if (rectDay.Height > 20 && rectDay.Bottom > 0)
                {
                    string dayText = dataGridView1.Rows[i].Cells["colDay"].Value?.ToString() ?? "";
                    DrawVerticalText(g, dayText, _currFontDay, rectDay);
                }
            }
            int startPairBlock = (firstRow / 2) * 2;
            for (int i = startPairBlock; i <= lastRow; i += 2)
            {
                if (i >= dataGridView1.Rows.Count) break;
                Rectangle rectPair = GetMergedRectangle(1, i, 2);
                if (rectPair.Height > 10)
                {
                    string pairText = dataGridView1.Rows[i].Cells["colPair"].Value?.ToString() ?? "";
                    TextRenderer.DrawText(g, pairText, _currFontPair, rectPair, Color.Black, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            }
        }

        private Rectangle GetMergedRectangle(int colIndex, int startRow, int count)
        {
            Rectangle res = Rectangle.Empty;
            for (int k = 0; k < count; k++)
            {
                int rowIndex = startRow + k;
                if (rowIndex >= dataGridView1.Rows.Count) break;
                Rectangle r = dataGridView1.GetCellDisplayRectangle(colIndex, rowIndex, false);
                if (r.Width == 0) continue;
                if (res == Rectangle.Empty) res = r; else res = Rectangle.Union(res, r);
            }
            return res;
        }

        private void DrawVerticalText(Graphics g, string text, Font font, Rectangle rect)
        {
            var state = g.Save();
            float cx = rect.X + rect.Width / 2f;
            float cy = rect.Y + rect.Height / 2f;
            g.SetClip(new Rectangle(0, 0, dataGridView1.Width, dataGridView1.Height));
            g.TranslateTransform(cx, cy);
            g.RotateTransform(-90);
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString(text, font, Brushes.Black, 0, 0, sf);
            }
            g.Restore(state);
        }

        private void DataGridView1_MouseWheel(object sender, MouseEventArgs e)
        {
            if (Control.ModifierKeys == Keys.Control)
            {
                ((HandledMouseEventArgs)e).Handled = true;
                if (e.Delta > 0) _zoomFactor += 0.1f; else _zoomFactor -= 0.1f;
                if (_zoomFactor < MIN_ZOOM) _zoomFactor = MIN_ZOOM;
                if (_zoomFactor > MAX_ZOOM) _zoomFactor = MAX_ZOOM;
                ApplyZoom();
            }
        }

        private void UpdateZoomFonts()
        {
            _currFontHeader = new Font(_baseFontHeader.FontFamily, _baseFontHeader.Size * _zoomFactor, _baseFontHeader.Style);
            _currFontDay = new Font(_baseFontDay.FontFamily, _baseFontDay.Size * _zoomFactor, _baseFontDay.Style);
            _currFontPair = new Font(_baseFontPair.FontFamily, _baseFontPair.Size * _zoomFactor, _baseFontPair.Style);
            _currFontContent = new Font(_baseFontContent.FontFamily, _baseFontContent.Size * _zoomFactor, _baseFontContent.Style);
        }

        private void ApplyZoom()
        {
            UpdateZoomFonts();
            dataGridView1.ColumnHeadersHeight = (int)(50 * _zoomFactor);
            dataGridView1.ColumnHeadersDefaultCellStyle.Font = _currFontHeader;
            dataGridView1.Columns["colDay"].Width = (int)(40 * _zoomFactor);
            dataGridView1.Columns["colPair"].Width = (int)(50 * _zoomFactor);

            for (int i = 2; i < dataGridView1.Columns.Count; i += 2)
            {
                dataGridView1.Columns[i].Width = (int)(170 * _zoomFactor);
                dataGridView1.Columns[i + 1].Width = (int)(45 * _zoomFactor);
            }

            int rowH = (int)(50 * _zoomFactor);
            foreach (DataGridViewRow row in dataGridView1.Rows) row.Height = rowH;
            dataGridView1.DefaultCellStyle.Font = _currFontContent;
            dataGridView1.Invalidate();
        }

        private void DataGridView1_CellMouseEnter(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (e.RowIndex == _lastHoverRow && e.ColumnIndex == _lastHoverCol) return;

            _lastHoverRow = e.RowIndex;
            _lastHoverCol = e.ColumnIndex;

            int dayIndex = e.RowIndex / 12;
            int rowInDay = e.RowIndex % 12;
            int pairNum = (rowInDay / 2) + 1;

            if (e.ColumnIndex == 1)
            {
                try
                {
                    var matrixData = _analytics.GetRoomMatrix(dayIndex, pairNum);
                    _currentTooltipData = matrixData;
                    Point cursorPt = dataGridView1.PointToClient(Cursor.Position);
                    cursorPt.Y += 20;
                    _infoTooltip.Show(" ", dataGridView1, cursorPt);
                }
                catch { _infoTooltip.Hide(dataGridView1); }
            }
            else if (e.ColumnIndex >= 2 && (e.ColumnIndex - 2) % 2 == 0)
            {
                var cellVal = dataGridView1.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;
                if (cellVal == null || string.IsNullOrWhiteSpace(cellVal.ToString()))
                {
                    _infoTooltip.Hide(dataGridView1);
                    return;
                }

                string subjName = cellVal.ToString().Split('\n')[0].Trim();
                int groupIdx = (e.ColumnIndex - 2) / 2;

                if (groupIdx < _sortedGroups.Count)
                {
                    string groupName = _sortedGroups[groupIdx].Name;
                    _currentTooltipData = _analytics.GetPlanTooltip(groupName, subjName);
                    var rect = dataGridView1.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, false);
                    _infoTooltip.Show(" ", dataGridView1, rect.X + rect.Width / 2, rect.Bottom + 5);
                }
            }
            else
            {
                _infoTooltip.Hide(dataGridView1);
            }
        }

        private void DataGridView1_CellMouseLeave(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex == _lastHoverRow && e.ColumnIndex == _lastHoverCol)
            {
                _lastHoverRow = -1;
                _lastHoverCol = -1;
                _infoTooltip.Hide(dataGridView1);
            }
        }

        private void _infoTooltip_Popup(object sender, PopupEventArgs e)
        {
            if (_currentTooltipData is AnalyticsService.RoomMatrixData matrix)
            {
                int cellW = 45;
                int rowH = 25;
                int cols = matrix.Rooms.Count;
                int totalW = 100 + (cols * cellW);
                int totalH = 3 * rowH + 10;
                e.ToolTipSize = new Size(totalW, totalH);
            }
            else if (_currentTooltipData is string text)
            {
                Size sz = TextRenderer.MeasureText(text, _tooltipFont);
                e.ToolTipSize = new Size(sz.Width + 20, sz.Height + 10);
            }
        }

        private void _infoTooltip_Draw(object sender, DrawToolTipEventArgs e)
        {
            e.Graphics.Clear(Color.White);
            e.Graphics.DrawRectangle(Pens.Black, 0, 0, e.Bounds.Width - 1, e.Bounds.Height - 1);

            if (_currentTooltipData is AnalyticsService.RoomMatrixData matrix)
            {
                int startX = 100;
                int cellW = 45;
                int rowH = 25;

                e.Graphics.DrawString("Аудитория:", _currFontHeader, Brushes.Black, 5, 5);
                e.Graphics.DrawString("Светлая:", _currFontHeader, Brushes.Black, 5, 5 + rowH);
                e.Graphics.DrawString("Темная:", _currFontHeader, Brushes.Black, 5, 5 + rowH * 2);

                for (int i = 0; i < matrix.Rooms.Count; i++)
                {
                    int x = startX + (i * cellW);
                    e.Graphics.DrawRectangle(Pens.LightGray, x, 0, cellW, rowH);
                    e.Graphics.DrawString(matrix.Rooms[i], _currFontContent, Brushes.Black, x + 2, 5);

                    e.Graphics.DrawRectangle(Pens.LightGray, x, rowH, cellW, rowH);
                    if (matrix.LightWeekStatus[i]) DrawCross(e.Graphics, x, rowH, cellW, rowH);

                    e.Graphics.DrawRectangle(Pens.LightGray, x, rowH * 2, cellW, rowH);
                    if (matrix.DarkWeekStatus[i]) DrawCross(e.Graphics, x, rowH * 2, cellW, rowH);
                }
            }
            else if (_currentTooltipData is string text)
            {
                e.Graphics.DrawString(text, _tooltipFont, Brushes.Black, 5, 5);
            }
        }

        private void DrawCross(Graphics g, int x, int y, int w, int h)
        {
            int pad = 6;
            using (Pen p = new Pen(Color.Red, 2))
            {
                g.DrawLine(p, x + pad, y + pad, x + w - pad, y + h - pad);
                g.DrawLine(p, x + w - pad, y + pad, x + pad, y + h - pad);
            }
        }

        // --- ВСТАВИТЬ ЭТОТ МЕТОД ВНУТРЬ КЛАССА ScheduleMatrixForm ---
        private async void BtnExport_Click(object sender, EventArgs e)
        {
            // 1. Проверки
            if (_sortedGroups == null || _sortedGroups.Count == 0) { MessageBox.Show("Нет групп!"); return; }
            var allLessons = _repo.GetAllSchedule();
            if (allLessons.Count == 0) { MessageBox.Show("Расписание пустое!"); return; }

            // 2. Выбор пути сохранения
            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = "Excel Files|*.xlsx";
                sfd.FileName = $"Расписание_{DateTime.Now:yyyy-MM-dd}.xlsx";
                sfd.Title = "Куда сохранить расписание?";

                if (sfd.ShowDialog() != DialogResult.OK) return; // Пользователь отменил

                // 3. Запуск прогресса
                var progressForm = new ExportProgressForm();
                progressForm.Show(this); // Показываем поверх главного окна

                // Создаем репортер прогресса, который сам прокинет данные в UI поток
                var progressReporter = new Progress<(int percent, string message)>(data =>
                {
                    progressForm.UpdateProgress(data.percent, data.message);
                });

                var groupNames = _sortedGroups.Select(g => g.Name).ToList();
                string path = sfd.FileName;

                try
                {
                    // Запускаем тяжелую работу в фоне
                    await System.Threading.Tasks.Task.Run(() =>
                    {
                        var exporter = new ExcelExporter();
                        exporter.ExportScheduleToExcel(allLessons, groupNames, path, progressReporter);
                    });

                    progressForm.Close();

                    if (MessageBox.Show("Расписание успешно сохранено!\nОткрыть файл сейчас?", "Успех", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = path, UseShellExecute = true });
                    }
                }
                catch (Exception ex)
                {
                    progressForm.Close();
                    MessageBox.Show("Ошибка при экспорте:\n" + ex.Message, "Сбой", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}