namespace ScheduleSystem.Models
{
    public class Subject
    {
        public int SubjectID { get; set; }
        public string Name { get; set; }

        // Поля из скриншота Subjects
        public bool RequiresComputers { get; set; }
        public string FixedRoom { get; set; }      // Жесткая привязка
        public string ForbiddenRoom { get; set; }  // Где нельзя проводить

        public override string ToString() => Name;
    }
}