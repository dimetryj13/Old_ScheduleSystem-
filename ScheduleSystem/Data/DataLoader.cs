using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Windows.Forms;
using ScheduleSystem.Models;

namespace ScheduleSystem.Data
{
    public static class DataLoader
    {
        // ----------------- УЧЕБНЫЙ ПЛАН -----------------
        // В классе DataLoader:

        public static List<AcademicPlanItem> LoadAcademicPlan()
        {
            var list = new List<AcademicPlanItem>();
            using (var conn = DbHelper.GetConnection())
            {
                conn.Open();
                // Джойним таблицы, чтобы сразу получить имена учителей вместо ID
                string sql = @"
            SELECT 
                ap.PlanID, 
                ap.GroupID, 
                s.SubjectName, 
                ap.Hours, 
                ap.LectureInWeek, 
                ap.PracticeInWeek, 
                ap.LabsInWeek, 
                ap.Semester,
                t1.FullName AS LecName,
                t2.FullName AS PracName
            FROM ((AcademicPlan ap 
            LEFT JOIN Subjects s ON ap.SubjectID = s.SubjectID)
            LEFT JOIN Teachers t1 ON ap.LectureTeacher = t1.TeacherID)
            LEFT JOIN Teachers t2 ON ap.PracticeTeacher = t2.TeacherID";

                using (var cmd = new OleDbCommand(sql, conn))
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new AcademicPlanItem
                        {
                            PlanID = SafeInt(r["PlanID"]),
                            GroupName = r["GroupID"]?.ToString() ?? "",
                            SubjectName = r["SubjectName"]?.ToString() ?? "",
                            TotalHours = SafeInt(r["Hours"]),

                            // Заполняем новые поля
                            LectureInWeek = SafeInt(r["LectureInWeek"]),
                            PracticeInWeek = SafeInt(r["PracticeInWeek"]),
                            LabsInWeek = SafeInt(r["LabsInWeek"]),
                            Semester = SafeInt(r["Semester"]),

                            TeacherLecture = r["LecName"]?.ToString() ?? "",
                            TeacherPractice = r["PracName"]?.ToString() ?? ""
                        });
                    }
                }
            }
            return list;
        }

        // Вспомогательный метод (добавьте его в DataLoader, если нет)
        private static int SafeInt(object val)
        {
            if (val == null || val == DBNull.Value) return 0;
            if (int.TryParse(val.ToString(), out int res)) return res;
            return 0;
        }
        // ВОТ ЭТОГО МЕТОДА НЕ ХВАТАЛО, ИЗ-ЗА ЭТОГО БЫЛА ОШИБКА
        public static void UpdateAcademicPlanCell(int planId, string columnName, object value)
        {
            using (var conn = DbHelper.GetConnection())
            {
                try
                {
                    conn.Open();
                    string dbField = "";
                    if (columnName == "colTotal") dbField = "Hours";
                    else if (columnName == "colLec") dbField = "LectureTeacher";
                    else if (columnName == "colPrac") dbField = "PracticeTeacher";
                    else if (columnName == "colSubj") dbField = "SubjectID";

                    if (string.IsNullOrEmpty(dbField)) return;

                    string sql = $"UPDATE AcademicPlan SET [{dbField}] = ? WHERE [PlanID] = ?";
                    using (var cmd = new OleDbCommand(sql, conn))
                    {
                        if (value == null || value.ToString() == "") cmd.Parameters.AddWithValue("?", DBNull.Value);
                        else cmd.Parameters.AddWithValue("?", value);
                        cmd.Parameters.AddWithValue("?", planId);
                        cmd.ExecuteNonQuery();
                    }
                }
                catch { }
            }
        }

        // ----------------- ГРУППЫ -----------------
        public static List<Group> LoadGroups(bool onlyActual)
        {
            var groups = new List<Group>();
            using (var conn = DbHelper.GetConnection())
            {
                try
                {
                    conn.Open();
                    // Добавлены скобки [] чтобы Access не ругался
                    string query = "SELECT [Код], [GroupName], [StudentCount], [MainTeacher], [Actually] FROM [GroupsList] WHERE [Actually] = ? ORDER BY [GroupName]";
                    using (var cmd = new OleDbCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("?", onlyActual);
                        using (var r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                groups.Add(new Group
                                {
                                    Id = GetInt(r["Код"]),
                                    Name = r["GroupName"].ToString(),
                                    StudentCount = GetInt(r["StudentCount"]),
                                    CuratorId = r["MainTeacher"] == DBNull.Value ? 0 : GetInt(r["MainTeacher"]),
                                    IsActual = r["Actually"] != DBNull.Value && Convert.ToBoolean(r["Actually"])
                                });
                            }
                        }
                    }
                }
                catch (Exception ex) { MessageBox.Show("Ошибка групп: " + ex.Message); }
            }
            return groups;
        }

        public static void UpdateGroupInfo(int groupId, int studentCount, int curatorId)
        {
            using (var conn = DbHelper.GetConnection())
            {
                try
                {
                    conn.Open();
                    string sql = "UPDATE [GroupsList] SET [StudentCount] = ?, [MainTeacher] = ? WHERE [Код] = ?";
                    using (var cmd = new OleDbCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("?", studentCount);
                        if (curatorId == 0) cmd.Parameters.AddWithValue("?", DBNull.Value);
                        else cmd.Parameters.AddWithValue("?", curatorId);
                        cmd.Parameters.AddWithValue("?", groupId);
                        cmd.ExecuteNonQuery();
                    }
                }
                catch { }
            }
        }

        // ----------------- СПРАВОЧНИКИ -----------------
        
        public static List<Subject> LoadSubjects()
        {
            var list = new List<Subject>();
            using (var conn = DbHelper.GetConnection())
            {
                try
                {
                    conn.Open();
                    string sql = "SELECT SubjectID, SubjectName FROM Subjects ORDER BY SubjectName";
                    using (var cmd = new OleDbCommand(sql, conn))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read()) list.Add(new Subject { SubjectID = GetInt(r["SubjectID"]), Name = r["SubjectName"].ToString() });
                    }
                }
                catch { }
            }
            return list;
        }

        public static List<Teacher> LoadTeachers()
        {
            var list = new List<Teacher>();
            using (var conn = DbHelper.GetConnection())
            {
                try
                {
                    conn.Open();
                    string sql = "SELECT TeacherID, FullName FROM Teachers ORDER BY FullName";
                    using (var cmd = new OleDbCommand(sql, conn))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read()) list.Add(new Teacher { TeacherID = GetInt(r["TeacherID"]), FullName = r["FullName"].ToString() });
                    }
                }
                catch { }
            }
            return list;
        }

        public static List<RoomInfo> LoadRoomInfos()
        {
            var list = new List<RoomInfo>();
            using (var conn = DbHelper.GetConnection())
            {
                try
                {
                    conn.Open();
                    string sql = "SELECT RoomID, RoomNumber, Capacity, HasComputers FROM Classrooms ORDER BY RoomNumber";
                    using (var cmd = new OleDbCommand(sql, conn))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            list.Add(new RoomInfo
                            {
                                RoomID = GetInt(r["RoomID"]),
                                Number = r["RoomNumber"].ToString(),
                                Capacity = r["Capacity"] != DBNull.Value ? GetInt(r["Capacity"]) : 0,
                                HasComputers = r["HasComputers"] != DBNull.Value && Convert.ToBoolean(r["HasComputers"])
                            });
                        }
                    }
                }
                catch { }
            }
            return list;
        }

        public static List<string> LoadRooms()
        {
            var list = new List<string>();
            var infos = LoadRoomInfos();
            foreach (var i in infos) list.Add(i.Number);
            return list;
        }

        private static int GetInt(object value)
        {
            if (value == DBNull.Value || value == null) return 0;
            try { return Convert.ToInt32(value); } catch { return 0; }
        }
    }
}