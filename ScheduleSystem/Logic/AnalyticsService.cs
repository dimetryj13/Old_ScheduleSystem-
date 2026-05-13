using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Linq;
using System.Text; // Для StringBuilder
using ScheduleSystem.Data;

namespace ScheduleSystem.Logic
{
    public class AnalyticsService
    {
        // Кэш для планов по предметам
        private Dictionary<string, SubjectStats> _statsCache = new Dictionary<string, SubjectStats>();

        // Кэш занятости аудиторий (оставляем как было, это работает отлично)
        private HashSet<string> _busySlotsCache = new HashSet<string>();
        private List<string> _allRooms = new List<string>();

        // Класс для хранения статистики по типам занятий (на 2 недели)
        private class SubjectStats
        {
            public int LecPlan, LecFact;
            public int PracPlan, PracFact;
            public int LabPlan, LabFact;
        }

        public void ReloadStats()
        {
            _statsCache.Clear();
            _busySlotsCache.Clear();
            _allRooms.Clear();

            using (var conn = DbHelper.GetConnection())
            {
                conn.Open();

                // 1. Аудитории (для матрицы)
                var cmdR = new OleDbCommand("SELECT RoomNumber FROM Classrooms ORDER BY RoomNumber", conn);
                using (var r = cmdR.ExecuteReader())
                {
                    while (r.Read()) _allRooms.Add(r["RoomNumber"].ToString());
                }

                // 2. Считываем ПЛАН (AcademicPlan)
                // LectureInWeek считаем как "Пар в неделю". Значит в цикл (2 недели) входит LectureInWeek * 2.
                string sqlPlan = @"SELECT ap.GroupID, s.SubjectName, ap.LectureInWeek, ap.PracticeInWeek, ap.LabsInWeek 
                                   FROM AcademicPlan ap LEFT JOIN Subjects s ON ap.SubjectID = s.SubjectID";

                using (var cmd = new OleDbCommand(sqlPlan, conn))
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        string grp = r["GroupID"]?.ToString() ?? "";
                        string subj = r["SubjectName"]?.ToString() ?? "";
                        string key = $"{grp}_{subj}";

                        var stats = new SubjectStats();

                        // План на 2 недели = (Пар/нед) * 2
                        stats.LecPlan = SafeInt(r["LectureInWeek"]) * 2;
                        stats.PracPlan = SafeInt(r["PracticeInWeek"]) * 2;
                        stats.LabPlan = SafeInt(r["LabsInWeek"]) * 2;

                        _statsCache[key] = stats;
                    }
                }

                // 3. Считаем ФАКТ (Schedule)
                // Нам нужно пройтись по всему расписанию и посчитать пары.
                // WeekType: 0 (Общая) = 2 пары в цикл. 1 или 2 = 1 пара в цикл.
                string sqlFact = "SELECT GroupName, SubjectName, LessonType, WeekType FROM Schedule";
                using (var cmd = new OleDbCommand(sqlFact, conn))
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        string grp = r["GroupName"]?.ToString() ?? "";
                        string subj = r["SubjectName"]?.ToString() ?? "";
                        int type = SafeInt(r["LessonType"]); // 0=Лек, 1=Прак, 2=Лаб
                        int week = SafeInt(r["WeekType"]);   // 0=Общ, 1=Свет, 2=Темн

                        string key = $"{grp}_{subj}";

                        // Если такого предмета нет в плане (странно, но бывает), создаем запись
                        if (!_statsCache.ContainsKey(key)) _statsCache[key] = new SubjectStats();

                        var s = _statsCache[key];
                        int weight = (week == 0) ? 2 : 1; // Вес пары в цикле

                        if (type == 0) s.LecFact += weight;
                        else if (type == 2) s.LabFact += weight; // Лабы (обычно type 2)
                        else s.PracFact += weight; // Практики (type 1)
                    }
                }

                // 4. Загружаем занятость аудиторий (для левой колонки)
                string sqlBusy = "SELECT WeekType, DayOfWeek, PairNumber, RoomNumber FROM Schedule";
                using (var cmd = new OleDbCommand(sqlBusy, conn))
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        int w = SafeInt(r["WeekType"]);
                        int d = SafeInt(r["DayOfWeek"]) - 1;
                        int p = SafeInt(r["PairNumber"]);
                        string room = r["RoomNumber"]?.ToString() ?? "";

                        if (w == 0 || w == 1) _busySlotsCache.Add($"1_{d}_{p}_{room}");
                        if (w == 0 || w == 2) _busySlotsCache.Add($"2_{d}_{p}_{room}");
                    }
                }
            }
        }

        private int SafeInt(object value)
        {
            if (value == null || value == DBNull.Value) return 0;
            try { return Convert.ToInt32(value); } catch { return 0; }
        }

        // --- Метод для тултипа предмета (НОВЫЙ ФОРМАТ) ---
        public string GetPlanTooltip(string group, string subject)
        {
            string key = $"{group}_{subject}";
            if (!_statsCache.ContainsKey(key)) return "Нет данных в учебном плане";

            var s = _statsCache[key];
            var sb = new StringBuilder();

            sb.AppendLine($"Дисциплина: {subject}");
            sb.AppendLine("-----------------------");

            // Формируем строки только если есть план или факт
            if (s.LecPlan > 0 || s.LecFact > 0)
                sb.AppendLine($"Лекции:   {s.LecFact} / {s.LecPlan}  {GetStatusIcon(s.LecFact, s.LecPlan)}");

            if (s.PracPlan > 0 || s.PracFact > 0)
                sb.AppendLine($"Практики: {s.PracFact} / {s.PracPlan}  {GetStatusIcon(s.PracFact, s.PracPlan)}");

            if (s.LabPlan > 0 || s.LabFact > 0)
                sb.AppendLine($"Лабы:     {s.LabFact} / {s.LabPlan}  {GetStatusIcon(s.LabFact, s.LabPlan)}");

            sb.Append("(пар в 2 недели)");
            return sb.ToString();
        }

        private string GetStatusIcon(int fact, int plan)
        {
            if (fact == plan) return "✔";
            if (fact > plan) return "⚠"; // Перебор
            return ""; // Недобор
        }

        // --- Метод для матрицы аудиторий (без изменений) ---
        public RoomMatrixData GetRoomMatrix(int dayIndex, int pairNum)
        {
            var data = new RoomMatrixData();
            data.Rooms = _allRooms;
            data.LightWeekStatus = new List<bool>();
            data.DarkWeekStatus = new List<bool>();

            foreach (var room in _allRooms)
            {
                data.LightWeekStatus.Add(_busySlotsCache.Contains($"1_{dayIndex}_{pairNum}_{room}"));
                data.DarkWeekStatus.Add(_busySlotsCache.Contains($"2_{dayIndex}_{pairNum}_{room}"));
            }
            return data;
        }

        public class RoomMatrixData
        {
            public List<string> Rooms;
            public List<bool> LightWeekStatus;
            public List<bool> DarkWeekStatus;
        }
    }
}