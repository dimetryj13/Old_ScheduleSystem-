using System;
using System.Collections.Generic;

namespace ScheduleSystem.Models
{
    public enum LessonType
    {
        Lecture = 0,
        Practice = 1,
        Lab = 2
    }

    // Класс для чтения строки из базы данных (Учебный план)
    public class AcademicPlanItem
    {
        public int PlanID { get; set; }
        public string GroupName { get; set; }
        public string SubjectName { get; set; }
        public int TotalHours { get; set; }

        public int LectureInWeek { get; set; }
        public int PracticeInWeek { get; set; }
        public int LabsInWeek { get; set; }

        public string TeacherLecture { get; set; }
        public string TeacherPractice { get; set; }

        // Новые поля для Ultimate-алгоритма
        public string FixedRoom { get; set; }      // Если пара жестко привязана (например, спортзал)
        public string ForbiddenRoom { get; set; }  // Аудитория-табу
        public int Semester { get; set; }          // Чтобы фильтровать осень/весна
    }

    // Класс задачи для алгоритма
    public class SchedulingTask
    {
        public List<string> GroupNames { get; set; } = new List<string>();

        public string SubjectName { get; set; }
        public string TeacherName { get; set; }

        public LessonType Type { get; set; }

        // Раздельные требования к компьютерам
        public bool RequiresComputersForPractice { get; set; }
        public bool RequiresComputersForLabs { get; set; }

        // Упрощенный доступ (для совместимости)
        public bool RequiresComputers => (Type == LessonType.Lab && RequiresComputersForLabs) ||
                                         (Type == LessonType.Practice && RequiresComputersForPractice);

        public int DurationPairs { get; set; } = 1;

        // Тип недели: 0=Всегда, 1=Светлая, 2=Темная
        public int WeekType { get; set; } = 0;

        // Жесткие ограничения
        public string FixedRoom { get; set; }
        public string ForbiddenRoom { get; set; }
    }

    // Элемент готового расписания
    public class ScheduleItem
    {
        public string GroupName { get; set; }
        public string SubjectName { get; set; }
        public string TeacherName { get; set; }
        public string RoomNumber { get; set; }
        public int DayOfWeek { get; set; }
        public int PairNumber { get; set; }
        public int WeekType { get; set; }
        public int LessonType { get; set; }
    }
}