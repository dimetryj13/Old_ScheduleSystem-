using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ScheduleSystem
{
    public class OptimizationResultForm : Form
    {
        private DataGridView _grid;
        private Button _btnClose;
        private Label _lblSummary;

        public OptimizationResultForm(List<string> errors)
        {
            // Настройка формы
            this.Text = "Результат генерации расписания";
            this.Size = new Size(700, 500);
            this.StartPosition = FormStartPosition.CenterParent;
            this.MinimizeBox = false;
            this.MaximizeBox = false;

            SetupUI();
            LoadData(errors);
        }

        private void SetupUI()
        {
            // Верхняя панель с итогом
            _lblSummary = new Label();
            _lblSummary.Dock = DockStyle.Top;
            _lblSummary.Height = 40;
            _lblSummary.TextAlign = ContentAlignment.MiddleLeft;
            _lblSummary.Padding = new Padding(10, 0, 0, 0);
            _lblSummary.Font = new Font("Segoe UI", 11, FontStyle.Bold);
            this.Controls.Add(_lblSummary);

            // Кнопка закрытия внизу
            var pnlBottom = new Panel { Dock = DockStyle.Bottom, Height = 50 };
            _btnClose = new Button { Text = "Закрыть", Width = 100, Height = 30 };
            _btnClose.Location = new Point(this.ClientSize.Width - 115, 10);
            _btnClose.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            _btnClose.Click += (s, e) => this.Close();
            pnlBottom.Controls.Add(_btnClose);
            this.Controls.Add(pnlBottom);

            // Таблица
            _grid = new DataGridView();
            _grid.Dock = DockStyle.Fill;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.ReadOnly = true;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.BackgroundColor = Color.White;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            // Колонки
            _grid.Columns.Add("Subject", "Дисциплина");
            _grid.Columns.Add("Group", "Группа");
            _grid.Columns.Add("Details", "Детали (Неделя)");
            _grid.Columns.Add("Reason", "Причина");

            // Настройка ширины
            _grid.Columns[0].FillWeight = 40; // Дисциплина
            _grid.Columns[1].FillWeight = 20; // Группа
            _grid.Columns[2].FillWeight = 20; // Неделя
            _grid.Columns[3].FillWeight = 20; // Причина

            this.Controls.Add(_grid);
            _grid.BringToFront(); // Поверх панелей
        }

        private void LoadData(List<string> errors)
        {
            if (errors == null || errors.Count == 0)
            {
                _lblSummary.Text = "✔ Успех! Все занятия успешно расставлены.";
                _lblSummary.ForeColor = Color.Green;
                return;
            }

            _lblSummary.Text = $"⚠ Внимание! Не удалось расставить {errors.Count} занятий:";
            _lblSummary.ForeColor = Color.Red;

            foreach (var errorMsg in errors)
            {
                // Парсим строку ошибки, которую выдает Optimizer
                // Формат из Optimizer: "Не влезает: {Subject} ({Type}) для {Groups} ({Week})"

                string subject = "???";
                string group = "???";
                string details = "";
                string reason = "Нет места / Конфликт";

                try
                {
                    // Попытка простого разбора строки
                    // Пример: "Не влезает: Математика (Лек) для Гр-1, Гр-2 (Светлая)"

                    string temp = errorMsg.Replace("Не влезает: ", "");
                    // temp = "Математика (Лек) для Гр-1, Гр-2 (Светлая)"

                    var parts = temp.Split(new string[] { " для " }, StringSplitOptions.None);
                    if (parts.Length == 2)
                    {
                        subject = parts[0].Trim(); // Математика (Лек)

                        string rest = parts[1]; // Гр-1, Гр-2 (Светлая)
                        int openBracket = rest.LastIndexOf('(');

                        if (openBracket > 0)
                        {
                            group = rest.Substring(0, openBracket).Trim();
                            details = rest.Substring(openBracket).Trim('(', ')');
                        }
                        else
                        {
                            group = rest;
                        }
                    }
                    else
                    {
                        subject = errorMsg; // Если формат другой, пишем всё в предмет
                    }
                }
                catch
                {
                    subject = errorMsg;
                }

                _grid.Rows.Add(subject, group, details, reason);
            }
        }
    }
}