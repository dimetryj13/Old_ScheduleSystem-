namespace ScheduleSystem.Models
{
    public class RoomInfo
    {
        public int RoomID { get; set; }
        public string Number { get; set; }
        public int Capacity { get; set; }       // Вместимость
        public bool HasComputers { get; set; }  // Есть ли ПК

        public override string ToString() => Number;
    }
}