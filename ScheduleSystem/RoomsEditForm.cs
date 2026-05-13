using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Drawing;
using System.Windows.Forms;
using ScheduleSystem.Data;
using ScheduleSystem.Models;

namespace ScheduleSystem
{
    public partial class RoomsEditForm : Form
    {

        // Список аудиторий в памяти
        private List<RoomInfo> _rooms = new List<RoomInfo>();
        private RoomInfo _selectedRoom = null;

        // Элементы интерфейса
        private ListBox lstRooms;
        private TextBox txtNumber;
        private NumericUpDown numCapacity;
        private CheckBox chkComputers;
        private Button btnSave, btnAdd, btnDelete;
        private Label lblStatus;

        public RoomsEditForm()
        {
            // Настройка формы
            this.Text = "Редактор аудиторий";
            this.Size = new Size(600, 400);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            SetupUI();
            LoadRooms();
        }

        private void SetupUI()
        {
            // 1. Список слева
            lstRooms = new ListBox();
            lstRooms.Location = new Point(10, 10);
            lstRooms.Size = new Size(200, 340);
            lstRooms.SelectedIndexChanged += LstRooms_SelectedIndexChanged;
            this.Controls.Add(lstRooms);

            // 2. Панель редактирования справа
            int x = 230;
            int y = 20;
            int w = 300;

            // Номер аудитории
            var lblNum = new Label() { Text = "Номер аудитории:", Location = new Point(x, y), AutoSize = true };
            this.Controls.Add(lblNum);

            txtNumber = new TextBox();
            txtNumber.Location = new Point(x, y + 20);
            txtNumber.Size = new Size(w, 25);
            this.Controls.Add(txtNumber);

            y += 60;

            // Вместимость
            var lblCap = new Label() { Text = "Вместимость (чел):", Location = new Point(x, y), AutoSize = true };
            this.Controls.Add(lblCap);

            numCapacity = new NumericUpDown();
            numCapacity.Location = new Point(x, y + 20);
            numCapacity.Size = new Size(100, 25);
            numCapacity.Minimum = 0;
            numCapacity.Maximum = 3500;
            numCapacity.Value = 30; // Стандартный класс
            this.Controls.Add(numCapacity);

            // Компьютеры (галочка)
            chkComputers = new CheckBox();
            chkComputers.Text = "🖥 Компьютерный класс";
            chkComputers.Location = new Point(x + 120, y + 20);
            chkComputers.AutoSize = true;
            this.Controls.Add(chkComputers);

            y += 60;

            // Кнопки управления
            btnSave = new Button() { Text = "💾 Сохранить", Location = new Point(x, y), Size = new Size(140, 40), BackColor = Color.LightGreen };
            btnSave.Click += BtnSave_Click;
            this.Controls.Add(btnSave);

            btnAdd = new Button() { Text = "+ Добавить новую", Location = new Point(x + 150, y), Size = new Size(150, 40) };
            btnAdd.Click += BtnAdd_Click;
            this.Controls.Add(btnAdd);

            y += 60;

            btnDelete = new Button() { Text = "🗑 Удалить", Location = new Point(x, y), Size = new Size(300, 30), ForeColor = Color.DarkRed };
            btnDelete.Click += BtnDelete_Click;
            this.Controls.Add(btnDelete);

            // Статус
            lblStatus = new Label() { Location = new Point(x, y + 40), AutoSize = true, ForeColor = Color.Gray };
            this.Controls.Add(lblStatus);
        }

        // --- ЛОГИКА ЗАГРУЗКИ ---
        private void LoadRooms()
        {
            _rooms.Clear();
            lstRooms.Items.Clear();

            // Используем наш DataLoader, который мы обновили ранее
            // Если он возвращает List<RoomInfo>, то всё отлично
            try
            {
                _rooms = DataLoader.LoadRoomInfos(); // Читаем из базы
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки: " + ex.Message);
            }

            foreach (var r in _rooms)
            {
                lstRooms.Items.Add(r.Number); // Показываем только номер
            }

            if (lstRooms.Items.Count > 0) lstRooms.SelectedIndex = 0;
            else ClearFields();
        }

        // --- СОБЫТИЯ ---
        private void LstRooms_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstRooms.SelectedIndex == -1) return;

            // Находим выбранный объект в списке
            _selectedRoom = _rooms[lstRooms.SelectedIndex];

            // Заполняем поля
            txtNumber.Text = _selectedRoom.Number;
            numCapacity.Value = _selectedRoom.Capacity;
            chkComputers.Checked = _selectedRoom.HasComputers;

            lblStatus.Text = $"Редактирование: ID {_selectedRoom.RoomID}";
            btnSave.Text = "💾 Сохранить";
        }

        private void BtnAdd_Click(object sender, EventArgs e)
        {
            // Сбрасываем выбор, чтобы создать новую
            lstRooms.SelectedIndex = -1;
            ClearFields();
            _selectedRoom = null; // Режим создания

            lblStatus.Text = "Режим: Добавление новой аудитории";
            btnSave.Text = "💾 Создать";
            txtNumber.Focus();
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            string number = txtNumber.Text.Trim();
            if (string.IsNullOrEmpty(number))
            {
                MessageBox.Show("Введите номер аудитории!");
                return;
            }

            int cap = (int)numCapacity.Value;
            bool hasPc = chkComputers.Checked;

            using (var conn = DbHelper.GetConnection())
            {
                conn.Open();

                if (_selectedRoom == null)
                {
                    // INSERT (Добавление)
                    string sql = "INSERT INTO Classrooms (RoomNumber, Capacity, HasComputers) VALUES (?, ?, ?)";
                    using (var cmd = new OleDbCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("?", number);
                        cmd.Parameters.AddWithValue("?", cap);
                        cmd.Parameters.AddWithValue("?", hasPc);
                        cmd.ExecuteNonQuery();
                    }
                }
                else
                {
                    // UPDATE (Обновление)
                    string sql = "UPDATE Classrooms SET RoomNumber=?, Capacity=?, HasComputers=? WHERE RoomID=?";
                    using (var cmd = new OleDbCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("?", number);
                        cmd.Parameters.AddWithValue("?", cap);
                        cmd.Parameters.AddWithValue("?", hasPc);
                        cmd.Parameters.AddWithValue("?", _selectedRoom.RoomID);
                        cmd.ExecuteNonQuery();
                    }
                }
            }

            // Перезагружаем список
            LoadRooms();
            MessageBox.Show("Сохранено!");
        }


        private void BtnDelete_Click(object sender, EventArgs e)
        {
            if (_selectedRoom == null) return;

            if (MessageBox.Show($"Вы точно хотите удалить аудиторию {_selectedRoom.Number}?", "Удаление", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                using (var conn = DbHelper.GetConnection())
                {
                    conn.Open();
                    var cmd = new OleDbCommand("DELETE FROM Classrooms WHERE RoomID = ?", conn);
                    cmd.Parameters.AddWithValue("?", _selectedRoom.RoomID);
                    cmd.ExecuteNonQuery();
                }
                LoadRooms();
            }
        }

        private void ClearFields()
        {
            txtNumber.Text = "";
            numCapacity.Value = 30;
            chkComputers.Checked = false;
        }

        private void RoomsEditForm_Load(object sender, EventArgs e)
        {
            // Дизайнер требует этот метод. Оставляем пустым или обновляем список.
        }
    }
}