using System.Collections.Generic;

namespace ScheduleSystem.Models
{
    public class Teacher
    {
        public int TeacherID { get; set; } // В базе это просто TeacherID (или Код)
        public string FullName { get; set; }
        public string Department { get; set; }

        // Лимиты потоков (из скриншота Teachers_1)
        public int MaxLectureGroups { get; set; }
        public int MaxPracticeGroups { get; set; }

        // Списки для детального редактора (загружаются отдельно при необходимости)
        public List<TeacherAvailability> Availability { get; set; } = new List<TeacherAvailability>();
        public List<TeacherRoomPref> RoomPrefs { get; set; } = new List<TeacherRoomPref>();

        public override string ToString() => FullName;
    }

    // Вспомогательный класс для доступности (из скриншота TeacherAvailability)
    public class TeacherAvailability
    {
        public string DayIdx { get; set; }   // В базе "Короткий текст"
        public string PairIdx { get; set; }  // В базе "Короткий текст"
        public bool IsAvailable { get; set; }
    }

    // Вспомогательный класс для аудиторий (из скриншота TeacherRoomPrefs)
    public class TeacherRoomPref
    {
        public string RoomNumber { get; set; } // В базе числовой, но лучше хранить строкой как имя ауд.
        public string Priority { get; set; }   // "Высокий", "Средний"...
    }
}