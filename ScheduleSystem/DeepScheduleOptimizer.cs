using System;
using System.Collections.Generic;
using System.Linq;
using ScheduleSystem.Data;
using ScheduleSystem.Models;

namespace ScheduleSystem.Logic
{
    public class DeepScheduleOptimizer
    {
        private readonly GeneratorSettings _settings;
        private List<string> _errors = new List<string>();
        private const int MAX_PAIRS = 6;

        private List<RoomInfo> _rooms;
        private Dictionary<int, TeacherProfile> _teachersCache = new Dictionary<int, TeacherProfile>();
        private Dictionary<string, GroupProfile> _groupsCache = new Dictionary<string, GroupProfile>();
        private Dictionary<int, bool[,,]> _roomSchedule = new Dictionary<int, bool[,,]>();
        private List<PlacedLesson> _placedLessons = new List<PlacedLesson>();

        public List<string> Errors => _errors;

        public DeepScheduleOptimizer(GeneratorSettings settings)
        {
            _settings = settings;
        }

        public List<ScheduleItem> Generate(List<AcademicPlanItem> planItems, List<RoomInfo> rooms)
        {
            _errors.Clear();
            _placedLessons.Clear();
            _rooms = rooms;

            InitializeData();

            var seasonItems = planItems.Where(p =>
                (_settings.SemesterFilter == 1 && p.Semester % 2 != 0) ||
                (_settings.SemesterFilter == 2 && p.Semester % 2 == 0)
            ).ToList();

            var queue = PrepareQueueAndStreams(seasonItems);

            queue = queue
                .OrderByDescending(t => t.TeacherTimeScarcity)
                .ThenByDescending(t => t.HasAbsolutePriority)
                .ThenByDescending(t => t.GroupNames.Count)
                .ThenBy(t => t.CourseLevel)
                .ToList();

            foreach (var task in queue)
            {
                var bestSlot = FindBestSlot(task);
                if (bestSlot != null) ApplySlot(task, bestSlot);
                else _errors.Add($"Не влезает: {task.SubjectName} ({task.TeacherName})");
            }

            return ConvertToResult();
        }

        private List<LessonTask> PrepareQueueAndStreams(List<AcademicPlanItem> items)
        {
            var rawTasks = new List<LessonTask>();
            foreach (var item in items)
            {
                CreateTasksFromHours(rawTasks, item, LessonType.Lecture, item.LectureInWeek, item.TeacherLecture);
                CreateTasksFromHours(rawTasks, item, LessonType.Practice, item.PracticeInWeek, item.TeacherPractice);
                CreateTasksFromHours(rawTasks, item, LessonType.Lab, item.LabsInWeek, item.TeacherPractice);
            }

            if (_settings.MergeStreams) return MergeIntoStreams(rawTasks);
            return rawTasks;
        }

        // Найдите метод CreateTasksFromHours и замените его на этот:
        private void CreateTasksFromHours(List<LessonTask> list, AcademicPlanItem item, LessonType type, int hours, string teacherName)
        {
            if (hours <= 0) return;
            int tId = GetTeacherId(teacherName);
            int course = CalculateCourseLevel(item.GroupName);

            // === ИСПРАВЛЕНИЕ: Получаем данные для сортировки ===
            int scarcity = _teachersCache.ContainsKey(tId) ? _teachersCache[tId].TimeScarcity : 0;
            bool isVip = false;
            if (_teachersCache.ContainsKey(tId))
            {
                // Если есть хоть одна комната с приоритетом 4
                isVip = _teachersCache[tId].RoomPriorities.Values.Any(p => p == 4);
            }
            // ==================================================

            int fullPairs = hours / 2;
            bool hasHalf = (hours % 2) != 0;

            for (int i = 0; i < fullPairs; i++)
                list.Add(new LessonTask(item, type, 0, teacherName, tId, course, scarcity, isVip)); // <-- Передаем параметры

            if (hasHalf)
                list.Add(new LessonTask(item, type, 1, teacherName, tId, course, scarcity, isVip));
        }
        private List<LessonTask> MergeIntoStreams(List<LessonTask> tasks)
        {
            var result = new List<LessonTask>();
            var grouped = tasks.Where(t => t.Type == LessonType.Lecture).GroupBy(t => new { t.SubjectName, t.TeacherID, t.WeekType });

            foreach (var g in grouped)
            {
                var candidates = g.ToList();
                int maxGroups = _teachersCache.ContainsKey(g.Key.TeacherID) ? _teachersCache[g.Key.TeacherID].MaxLectureGroups : 3;
                if (maxGroups <= 0) maxGroups = 3;

                for (int i = 0; i < candidates.Count; i += maxGroups)
                {
                    var chunk = candidates.Skip(i).Take(maxGroups).ToList();
                    var streamTask = chunk[0];
                    streamTask.GroupNames = chunk.SelectMany(x => x.GroupNames).Distinct().ToList();
                    streamTask.TotalStudents = chunk.Sum(x => x.TotalStudents);
                    result.Add(streamTask);
                }
            }
            result.AddRange(tasks.Where(t => t.Type != LessonType.Lecture));
            return result;
        }

        private ScheduleSlot FindBestSlot(LessonTask task)
        {
            ScheduleSlot bestSlot = null;
            long maxScore = long.MinValue;
            int endDay = _settings.UseSaturday ? 6 : 5;
            var teacher = _teachersCache[task.TeacherID];

            for (int day = 0; day < endDay; day++)
            {
                if (!teacher.IsAvailableDay(day)) continue;

                for (int pair = 1; pair <= MAX_PAIRS; pair++)
                {
                    if (!teacher.IsAvailableSlot(day, pair)) continue;
                    if (teacher.IsBusy(task.WeekType, day, pair)) continue;
                    if (IsAnyGroupBusy(task.GroupNames, task.WeekType, day, pair)) continue;

                    foreach (var room in _rooms)
                    {
                        if (task.RequiresComputers && !room.HasComputers) continue;

                        if (_settings.StrictRoomCapacity)
                        {
                            if (room.Capacity < task.TotalStudents) continue;
                        }
                        else if (room.Capacity < task.TotalStudents * 0.9) continue;

                        if (IsRoomBusy(room.RoomID, task.WeekType, day, pair)) continue;

                        long score = CalculateScore(task, day, pair, room);
                        if (score > maxScore)
                        {
                            maxScore = score;
                            bestSlot = new ScheduleSlot { Day = day, Pair = pair, RoomNumber = room.Number, RoomID = room.RoomID, WeekType = task.WeekType };
                        }
                    }
                }
            }
            return bestSlot;
        }

        private long CalculateScore(LessonTask task, int day, int pair, RoomInfo room)
        {
            long score = 0;
            var teacher = _teachersCache[task.TeacherID];

            int priority = teacher.GetRoomPriority(room.RoomID);
            if (priority == 4) score += 1000000;
            else score += priority * _settings.WeightRoomPriority * 100;

            if (!_settings.IgnoreRoomStickiness)
            {
                if (IsTeacherInRoom(task.TeacherID, room.RoomID, day, pair - 1)) score += 5000;
                if (IsTeacherInRoom(task.TeacherID, room.RoomID, day, pair + 1)) score += 5000;
            }

            if (!HasAdjacentPairs(teacher, day, pair)) score -= _settings.WeightTeacherWindow * 50;

            string mainGroup = task.GroupNames[0];
            var grpProfile = _groupsCache.ContainsKey(mainGroup) ? _groupsCache[mainGroup] : null;
            if (grpProfile != null && !HasAdjacentPairs(grpProfile, day, pair))
            {
                if (!_settings.AllowStudentWindows) score -= 100000;
                else score -= _settings.WeightStudentWindow * 50;
            }

            if (pair >= 5)
            {
                int penalty = _settings.LatePairPenalty * 10;
                if (task.CourseLevel > 4) penalty /= 2;
                if (task.CourseLevel > 10) penalty = 0;
                score -= penalty;
            }

            int currentPairs = CountGroupPairs(mainGroup, day);
            if (currentPairs >= 4) score -= 5000;
            if (currentPairs == 0) score += 100;

            return score;
        }

        private int CalculateCourseLevel(string groupName)
        {
            try
            {
                string digits = new string(groupName.Where(char.IsDigit).ToArray());
                if (digits.Length >= 2)
                {
                    int yearDigit = int.Parse(digits.Substring(1, 1));
                    int level = 10 - yearDigit;
                    if (groupName.ToLower().Contains("м")) level += 10;
                    return level;
                }
            }
            catch { }
            return 1;
        }

        private int GetTeacherId(string name)
        {
            var t = _teachersCache.FirstOrDefault(x => x.Value.Name == name).Value;
            if (t != null) return t.ID;
            int hash = name.GetHashCode();
            if (!_teachersCache.ContainsKey(hash)) _teachersCache[hash] = new TeacherProfile(hash) { Name = name };
            return hash;
        }

        private bool IsAnyGroupBusy(List<string> groups, int week, int day, int pair)
        {
            foreach (var g in groups) { if (_groupsCache.ContainsKey(g) && _groupsCache[g].IsBusy(week, day, pair)) return true; }
            return false;
        }

        private void InitializeData()
        {
            var availabilities = DbHelper.GetTeacherAvailabilities();
            var prefs = DbHelper.GetTeacherRoomPrefs();
            var teachersDB = DbHelper.GetTeachers();

            foreach (var av in availabilities)
            {
                EnsureTeacher(av.TeacherId);
                _teachersCache[av.TeacherId].SetAvailableSlot(av.DayIdx, av.PairIdx);
            }
            foreach (var pref in prefs)
            {
                EnsureTeacher(pref.TeacherID);
                _teachersCache[pref.TeacherID].RoomPriorities[pref.RoomID] = pref.Priority;
            }
            foreach (var t in teachersDB)
            {
                EnsureTeacher(t.TeacherID);
                _teachersCache[t.TeacherID].MaxLectureGroups = t.MaxLectureGroups;
                _teachersCache[t.TeacherID].Name = t.FullName;
            }
            foreach (var r in _rooms) _roomSchedule[r.RoomID] = new bool[3, 6, MAX_PAIRS + 1];
            var sizes = DbHelper.GetGroupStudentCounts();
            foreach (var kvp in sizes) _groupsCache[kvp.Key] = new GroupProfile { StudentCount = kvp.Value };
            foreach (var t in _teachersCache.Values) t.CalculateScarcity();
        }

        private void EnsureTeacher(int id) { if (!_teachersCache.ContainsKey(id)) _teachersCache[id] = new TeacherProfile(id); }

        private void ApplySlot(LessonTask task, ScheduleSlot slot)
        {
            _teachersCache[task.TeacherID].Book(slot.WeekType, slot.Day, slot.Pair);
            SetRoomBusy(slot.RoomID, slot.WeekType, slot.Day, slot.Pair, true);
            foreach (var g in task.GroupNames)
            {
                if (!_groupsCache.ContainsKey(g)) _groupsCache[g] = new GroupProfile();
                _groupsCache[g].Book(slot.WeekType, slot.Day, slot.Pair);
            }
            _placedLessons.Add(new PlacedLesson { Task = task, Day = slot.Day, Pair = slot.Pair, WeekType = slot.WeekType, RoomID = slot.RoomID, RoomNumber = slot.RoomNumber });
        }

        private void SetRoomBusy(int roomId, int week, int day, int pair, bool busy)
        {
            if (!_roomSchedule.ContainsKey(roomId)) return;
            if (week == 0) { _roomSchedule[roomId][0, day, pair] = busy; _roomSchedule[roomId][1, day, pair] = busy; _roomSchedule[roomId][2, day, pair] = busy; }
            else _roomSchedule[roomId][week, day, pair] = busy;
        }

        private bool IsRoomBusy(int roomId, int week, int day, int pair)
        {
            if (!_roomSchedule.ContainsKey(roomId)) return false;
            var sch = _roomSchedule[roomId];
            if (week == 0) return sch[1, day, pair] || sch[2, day, pair];
            return sch[week, day, pair] || sch[0, day, pair];
        }

        private bool IsTeacherInRoom(int tId, int rId, int day, int pair)
        {
            if (pair < 1 || pair > MAX_PAIRS) return false;
            return _placedLessons.Any(p => p.Task.TeacherID == tId && p.RoomID == rId && p.Day == day && p.Pair == pair);
        }

        private bool HasAdjacentPairs(TeacherProfile t, int day, int pair) => t.IsBusy(0, day, pair - 1) || t.IsBusy(0, day, pair + 1);
        private bool HasAdjacentPairs(GroupProfile g, int day, int pair) => g.IsBusy(0, day, pair - 1) || g.IsBusy(0, day, pair + 1);

        private int CountGroupPairs(string group, int day)
        {
            if (!_groupsCache.ContainsKey(group)) return 0;
            int count = 0;
            for (int p = 1; p <= MAX_PAIRS; p++) if (_groupsCache[group].IsBusy(0, day, p)) count++;
            return count;
        }

        private List<ScheduleItem> ConvertToResult()
        {
            var list = new List<ScheduleItem>();
            foreach (var p in _placedLessons)
            {
                foreach (var g in p.Task.GroupNames)
                {
                    list.Add(new ScheduleItem
                    {
                        GroupName = g,
                        SubjectName = p.Task.SubjectName,
                        TeacherName = p.Task.TeacherName,
                        RoomNumber = p.RoomNumber,
                        DayOfWeek = p.Day,
                        PairNumber = p.Pair,
                        WeekType = p.WeekType,
                        LessonType = (int)p.Task.Type
                    });
                }
            }
            return list;
        }

        private class PlacedLesson { public LessonTask Task; public int Day, Pair, WeekType, RoomID; public string RoomNumber; }
        private class ScheduleSlot { public int Day, Pair, RoomID, WeekType; public string RoomNumber; }

        private class TeacherProfile
        {
            public int ID;
            public string Name;
            public int MaxLectureGroups = 3;
            public int TimeScarcity = 0;
            public bool[,] AllowedSlots = new bool[6, 7];
            public bool[,,] Schedule = new bool[3, 6, 7];
            public Dictionary<int, int> RoomPriorities = new Dictionary<int, int>();
            public TeacherProfile(int id) { ID = id; }
            public void SetAvailableSlot(int day, int pair) { if (day >= 0 && day < 6 && pair >= 1 && pair <= 6) AllowedSlots[day, pair] = true; }
            public bool IsAvailableDay(int day) { for (int p = 1; p <= 6; p++) if (AllowedSlots[day, p]) return true; return false; }
            public bool IsAvailableSlot(int day, int pair) { if (day < 0 || day >= 6 || pair < 1 || pair > 6) return false; return AllowedSlots[day, pair]; }
            public void CalculateScarcity() { int slots = 0; for (int d = 0; d < 6; d++) for (int p = 1; p <= 6; p++) if (AllowedSlots[d, p]) slots++; TimeScarcity = 100 - slots; }
            public bool IsBusy(int week, int day, int pair) { if (day < 0 || day >= 6 || pair < 1 || pair > 6) return false; if (week == 0) return Schedule[1, day, pair] || Schedule[2, day, pair]; return Schedule[week, day, pair] || Schedule[0, day, pair]; }
            public void Book(int week, int day, int pair) { if (day < 0 || day >= 6 || pair < 1 || pair > 6) return; if (week == 0) { Schedule[0, day, pair] = true; Schedule[1, day, pair] = true; Schedule[2, day, pair] = true; } else Schedule[week, day, pair] = true; }
            public int GetRoomPriority(int roomId) => RoomPriorities.ContainsKey(roomId) ? RoomPriorities[roomId] : 0;
        }

        private class GroupProfile
        {
            public int StudentCount = 0;
            public bool[,,] Schedule = new bool[3, 6, 7];
            public bool IsBusy(int week, int day, int pair) { if (day < 0 || day >= 6 || pair < 1 || pair > 6) return false; if (week == 0) return Schedule[1, day, pair] || Schedule[2, day, pair]; return Schedule[week, day, pair] || Schedule[0, day, pair]; }
            public void Book(int week, int day, int pair) { if (day < 0 || day >= 6 || pair < 1 || pair > 6) return; if (week == 0) { Schedule[0, day, pair] = true; Schedule[1, day, pair] = true; Schedule[2, day, pair] = true; } else Schedule[week, day, pair] = true; }
        }

        private class LessonTask
        {
            public string SubjectName;
            public List<string> GroupNames = new List<string>();
            public string TeacherName;
            public int TeacherID;
            public LessonType Type;
            public int WeekType;
            public bool RequiresComputers;

            // Поля сортировки
            public bool HasAbsolutePriority;
            public int TeacherTimeScarcity;

            public int CourseLevel;
            public int TotalStudents;

            // Обновленный конструктор принимает scarcity и vip
            public LessonTask(AcademicPlanItem item, LessonType type, int week, string teacher, int tId, int course, int scarcity, bool vip)
            {
                SubjectName = item.SubjectName;
                GroupNames.Add(item.GroupName);
                TeacherName = teacher;
                TeacherID = tId;
                Type = type;
                WeekType = week;
                CourseLevel = course;

                // Заполняем новые поля
                TeacherTimeScarcity = scarcity;
                HasAbsolutePriority = vip;

                RequiresComputers = (type == LessonType.Lab) || (type == LessonType.Practice && (item.SubjectName.Contains("Информ") || item.SubjectName.Contains("ЭВМ")));
            }
        }
    }
}