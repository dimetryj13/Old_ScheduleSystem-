using ScheduleSystem.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.Linq;
using System.Windows.Forms;

namespace ScheduleSystem
{
    public static class DbHelper
    {
        private static string connectionString = $@"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={Application.StartupPath}\University.accdb;Persist Security Info=False;";

        public static OleDbConnection GetConnection()
        {
            return new OleDbConnection(connectionString);
        }

        public static bool TestConnection()
        {
            using (OleDbConnection conn = GetConnection())
            {
                try
                {
                    conn.Open();
                    return true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ошибка подключения:\n" + ex.Message);
                    return false;
                }
            }
        }

        // ЧТЕНИЕ УЧЕБНОГО ПЛАНА
        public static List<AcademicPlanItem> GetFullAcademicPlan()
        {
            var list = new List<AcademicPlanItem>();

            using (var conn = GetConnection())
            {
                try
                {
                    conn.Open();
                    // Используем LEFT JOIN для получения имен вместо ID
                    string sql = @"
                        SELECT 
                            ap.PlanID, ap.GroupID, s.SubjectName, 
                            ap.LectureInWeek, ap.PracticeInWeek, ap.LabsInWeek,
                            t1.FullName AS LectorName, t2.FullName AS PracticeName,
                            s.FixedRoom, s.ForbiddenRoom, ap.Semester
                        FROM (((AcademicPlan ap
                        LEFT JOIN Subjects s ON ap.SubjectID = s.SubjectID)
                        LEFT JOIN Teachers t1 ON ap.LectureTeacher = t1.TeacherID)
                        LEFT JOIN Teachers t2 ON ap.PracticeTeacher = t2.TeacherID)";

                    var cmd = new OleDbCommand(sql, conn);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var item = new AcademicPlanItem();
                            item.PlanID = Convert.ToInt32(reader["PlanID"]);
                            item.GroupName = reader["GroupID"].ToString();
                            item.SubjectName = reader["SubjectName"] != DBNull.Value ? reader["SubjectName"].ToString() : "Предмет";

                            item.LectureInWeek = GetSafeInt(reader, "LectureInWeek");
                            item.PracticeInWeek = GetSafeInt(reader, "PracticeInWeek");
                            item.LabsInWeek = GetSafeInt(reader, "LabsInWeek");

                            item.TeacherLecture = reader["LectorName"] != DBNull.Value ? reader["LectorName"].ToString() : "";
                            item.TeacherPractice = reader["PracticeName"] != DBNull.Value ? reader["PracticeName"].ToString() : "";

                            // Дополнительные поля (с защитой, если их нет в БД)
                            try { item.FixedRoom = reader["FixedRoom"]?.ToString(); } catch { }
                            try { item.ForbiddenRoom = reader["ForbiddenRoom"]?.ToString(); } catch { }
                            try { item.Semester = GetSafeInt(reader, "Semester"); } catch { }

                            if (item.LectureInWeek > 0 || item.PracticeInWeek > 0 || item.LabsInWeek > 0)
                                list.Add(item);
                        }
                    }
                }
                catch { /* Игнорируем ошибки структуры БД */ }
            }
            return list;
        }

        // ПОЛУЧЕНИЕ ЗАПРЕТОВ (ИСПРАВЛЕНО ЧТЕНИЕ ДНЕЙ)
        public class TeacherBan { public string TeacherName; public int Day; public int Pair; }

        public static List<TeacherBan> GetTeacherBans()
        {
            var list = new List<TeacherBan>();
            using (var conn = GetConnection())
            {
                try
                {
                    conn.Open();
                    string sql = @"
                        SELECT t.FullName, ta.DayIdx, ta.PairIdx 
                        FROM TeacherAvailability ta
                        INNER JOIN Teachers t ON ta.TeacherID = t.TeacherID
                        WHERE ta.IsAvailable = False";

                    var cmd = new OleDbCommand(sql, conn);
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            // Читаем "сырой" день из базы (скорее всего 0..5)
                            int rawDay = GetSafeInt(r, "DayIdx");

                            // === ИСПРАВЛЕНИЕ ===
                            // Просто всегда добавляем 1. 
                            // 0 (Пн) станет 1. 
                            // 5 (Сб) станет 6.
                            int correctedDay = rawDay + 1;

                            // На всякий случай: если вдруг в базе день был 6 (Воскресенье), он станет 7.
                            // Алгоритм работает до 6, так что 7 просто не повлияет (и это ок).

                            list.Add(new TeacherBan
                            {
                                TeacherName = r["FullName"].ToString(),
                                Day = correctedDay,
                                Pair = GetSafeInt(r, "PairIdx")
                            });
                        }
                    }
                }
                catch { }
            }
            return list;
        }
        // Вспомогательные методы
        public static List<RoomInfo> GetAllRooms()
        {
            var list = new List<RoomInfo>();
            using (var conn = GetConnection())
            {
                try
                {
                    conn.Open();
                    var cmd = new OleDbCommand("SELECT * FROM Classrooms", conn);
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            list.Add(new RoomInfo
                            {
                                RoomID = (int)r["RoomID"],
                                Number = r["RoomNumber"].ToString(),
                                Capacity = r["Capacity"] != DBNull.Value ? (int)r["Capacity"] : 30,
                                HasComputers = r["HasComputers"] != DBNull.Value && (bool)r["HasComputers"]
                            });
                        }
                    }
                }
                catch { }
            }
            return list;
        }

        public static Dictionary<string, int> GetGroupStudentCounts()
        {
            // Упрощенная версия - возвращаем дефолт, если таблицы нет
            return new Dictionary<string, int>();
        }

        private static int GetSafeInt(OleDbDataReader r, string col)
        {
            try { return r[col] != DBNull.Value ? Convert.ToInt32(r[col]) : 0; } catch { return 0; }
        }

        public static void CheckAndCreateScheduleTable() { }
        public static void CheckAndCreateClassroomsTable() { }

        // --- НОВЫЕ МЕТОДЫ ДЛЯ DeepAI ---

        // DTO для быстрой выгрузки доступности
        public class TeacherAvailabilityDto
        {
            public int TeacherId;
            public int DayIdx; // 0-5
            public int PairIdx; // 1-8
        }

        public class TeacherRoomPrefDto
        {
            public int TeacherID;
            public int RoomID;
            public int Priority; // 0-4
        }

        public static List<TeacherAvailabilityDto> GetTeacherAvailabilities()
        {
            var list = new List<TeacherAvailabilityDto>();
            using (var conn = GetConnection())
            {
                try
                {
                    conn.Open();
                    // Выбираем только тех, кто доступен (IsAvailable = True)
                    // Учтем, что в базе DayIdx может быть строкой или числом.
                    // Если в базе "Понедельник", нужно конвертировать.
                    // Предполагаем, что у тебя уже числовой индекс или строка "1"
                    string sql = "SELECT TeacherID, DayIdx, PairIdx FROM TeacherAvailability WHERE IsAvailable = True";

                    using (var cmd = new OleDbCommand(sql, conn))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            // ПАРСИНГ ДНЯ
                            var dayRaw = r["DayIdx"].ToString();
                            int day = ParseDay(dayRaw);

                            // ПАРСИНГ ПАРЫ
                            var pairRaw = r["PairIdx"].ToString();
                            int pair = ParsePair(pairRaw);

                            list.Add(new TeacherAvailabilityDto
                            {
                                TeacherId = Convert.ToInt32(r["TeacherID"]),
                                DayIdx = day,
                                PairIdx = pair
                            });
                        }
                    }
                }
                catch { }
            }
            return list;
        }

        public static List<TeacherRoomPrefDto> GetTeacherRoomPrefs()
        {
            var list = new List<TeacherRoomPrefDto>();
            using (var conn = GetConnection())
            {
                try
                {
                    conn.Open();
                    string sql = "SELECT TeacherID, RoomNumber, Priority FROM TeachersRoomPrefs"; // RoomNumber здесь хранит ID аудитории (согласно твоей структуре)
                    using (var cmd = new OleDbCommand(sql, conn))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            list.Add(new TeacherRoomPrefDto
                            {
                                TeacherID = Convert.ToInt32(r["TeacherID"]),
                                RoomID = Convert.ToInt32(r["RoomNumber"]), // Это ID аудитории
                                Priority = Convert.ToInt32(r["Priority"])
                            });
                        }
                    }
                }
                catch { }
            }
            return list;
        }

        // Хелперы для парсинга (если в базе текст)
        private static int ParseDay(string d)
        {
            if (int.TryParse(d, out int res))
            {
                // Если в базе написано "1", возвращаем 0 (ПН). 
                // Но если в базе "0", возвращаем 0, чтобы не получить -1.
                return (res > 0) ? res - 1 : 0;
            }

            d = d.ToLower().Trim();
            if (d.StartsWith("пн") || d.Contains("пон")) return 0;
            if (d.StartsWith("вт")) return 1;
            if (d.StartsWith("ср")) return 2;
            if (d.StartsWith("чт")) return 3;
            if (d.StartsWith("пт")) return 4;
            if (d.StartsWith("сб")) return 5;
            return 0; // По умолчанию ПН
        }
        private static int ParsePair(string p)
        {
            // Убираем всё, кроме цифр
            string digits = new string(p.Where(char.IsDigit).ToArray());
            if (int.TryParse(digits, out int res)) return res;
            return 1;
        }
    }
}