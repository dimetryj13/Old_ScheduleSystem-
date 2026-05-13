namespace ScheduleSystem.Models
{
    public class Group
    {
        // ID группы из базы (поле "Код")
        public int Id { get; set; }

        // Название (поле "GroupName")
        public string Name { get; set; }

        // Количество студентов
        public int StudentCount { get; set; }

        // ID Куратора (поле "MainTeacher")
        public int CuratorId { get; set; }

        // Актуальна ли группа (поле "Actually")
        public bool IsActual { get; set; }

        // Важно для отображения в ComboBox: возвращаем имя
        public override string ToString()
        {
            return Name;
        }
    }
}