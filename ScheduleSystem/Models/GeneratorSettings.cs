using System;

namespace ScheduleSystem.Models
{
    public enum GenerationMode
    {
        Fast = 0,
        Balanced = 1,
        DeepAI = 2
    }

    public class GeneratorSettings
    {
        public int AlgorithmMode
        {
            get => (int)Mode;
            set => Mode = (GenerationMode)value;
        }

        public GenerationMode Mode { get; set; } = GenerationMode.DeepAI;

        public bool UseSaturday { get; set; } = false;
        public int MaxPairsPerDay { get; set; } = 4;
        public bool MergeStreams { get; set; } = true;
        public int SemesterFilter { get; set; } = 1;

        public bool AllowStudentWindows { get; set; } = false;
        public bool StrictRoomCapacity { get; set; } = false;
        public bool IgnoreRoomStickiness { get; set; } = false;

        public int WeightRoomPriority { get; set; } = 90;
        public int WeightTeacherWindow { get; set; } = 80;
        public int WeightStudentWindow { get; set; } = 70;
        public int LatePairPenalty { get; set; } = 40;
        public int WeightRoomSwitch { get; set; } = 30;
    }
}