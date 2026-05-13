using System;
using System.Collections.Generic;
using System.Data.OleDb;
using ScheduleSystem.Data;

namespace ScheduleSystem.Models
{
    public class ScheduleRepository
    {
        // ИСПРАВЛЕНО: Метод теперь корректно чистит старые записи перед вставкой
        public void SaveLesson(string groupName, int dayIndex, int pairNumber, int weekType, string subject, string teacher, string room, int lessonType = 1)
        {
            using (var conn = DbHelper.GetConnection())
            {
                conn.Open();

                string deleteSql;
                // Если ставим пару "Всегда" (0) -> удаляем всё из этого слота (и 0, и 1, и 2)
                if (weekType == 0)
                {
                    deleteSql = "DELETE FROM Schedule WHERE GroupName = ? AND DayOfWeek = ? AND PairNumber = ?";
                    using (var cmdDel = new OleDbCommand(deleteSql, conn))
                    {
                        cmdDel.Parameters.AddWithValue("?", groupName);
                        cmdDel.Parameters.AddWithValue("?", dayIndex);
                        cmdDel.Parameters.AddWithValue("?", pairNumber);
                        cmdDel.ExecuteNonQuery();
                    }
                }
                // Если ставим "Мигалку" -> удаляем именно её + удаляем "Всегда" (т.к. она конфликтует)
                else
                {
                    deleteSql = "DELETE FROM Schedule WHERE GroupName = ? AND DayOfWeek = ? AND PairNumber = ? AND (WeekType = ? OR WeekType = 0)";
                    using (var cmdDel = new OleDbCommand(deleteSql, conn))
                    {
                        cmdDel.Parameters.AddWithValue("?", groupName);
                        cmdDel.Parameters.AddWithValue("?", dayIndex);
                        cmdDel.Parameters.AddWithValue("?", pairNumber);
                        cmdDel.Parameters.AddWithValue("?", weekType);
                        cmdDel.ExecuteNonQuery();
                    }
                }

                // Вставка новой записи (если предмет не пустой)
                if (!string.IsNullOrWhiteSpace(subject))
                {
                    string insertSql = @"INSERT INTO Schedule 
                                         (GroupName, DayOfWeek, PairNumber, WeekType, SubjectName, TeacherName, RoomNumber, LessonType)
                                         VALUES (?, ?, ?, ?, ?, ?, ?, ?)";

                    using (var cmdIn = new OleDbCommand(insertSql, conn))
                    {
                        cmdIn.Parameters.AddWithValue("?", groupName);
                        cmdIn.Parameters.AddWithValue("?", dayIndex);
                        cmdIn.Parameters.AddWithValue("?", pairNumber);
                        cmdIn.Parameters.AddWithValue("?", weekType);
                        cmdIn.Parameters.AddWithValue("?", subject);
                        cmdIn.Parameters.AddWithValue("?", teacher);
                        cmdIn.Parameters.AddWithValue("?", room);
                        cmdIn.Parameters.AddWithValue("?", lessonType);
                        cmdIn.ExecuteNonQuery();
                    }
                }
            }
        }

        public List<ScheduleItem> GetAllSchedule()
        {
            var list = new List<ScheduleItem>();
            using (var conn = DbHelper.GetConnection())
            {
                conn.Open();
                string sql = "SELECT * FROM Schedule";
                using (var cmd = new OleDbCommand(sql, conn))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new ScheduleItem
                            {
                                GroupName = reader["GroupName"].ToString(),
                                DayOfWeek = Convert.ToInt32(reader["DayOfWeek"]),
                                PairNumber = Convert.ToInt32(reader["PairNumber"]),
                                WeekType = Convert.ToInt32(reader["WeekType"]),
                                SubjectName = reader["SubjectName"].ToString(),
                                TeacherName = reader["TeacherName"].ToString(),
                                RoomNumber = reader["RoomNumber"].ToString(),
                                LessonType = reader["LessonType"] != DBNull.Value ? Convert.ToInt32(reader["LessonType"]) : 1
                            });
                        }
                    }
                }
            }
            return list;
        }
    }
}