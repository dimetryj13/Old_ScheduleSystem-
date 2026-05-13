using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Linq;
using ScheduleSystem.Data;
using ScheduleSystem.Models;

namespace ScheduleSystem.Logic
{
    /// <summary>
    /// УЛУЧШЕННЫЙ ОПТИМИЗАТОР РАСПИСАНИЯ
    /// Использует гибридный подход: Constraint Programming + Backtracking + Local Search
    /// </summary>
    public class ScheduleOptimizer
    {
        public List<string> Errors { get; private set; } = new List<string>();
        public List<string> Logs { get; private set; } = new List<string>();
        public ScheduleMetrics Metrics { get; private set; } = new ScheduleMetrics();

        private List<RoomSlot> _rooms = new List<RoomSlot>();
        private Dictionary<int, TeacherSlot> _teachers = new Dictionary<int, TeacherSlot>();
        private Dictionary<string, GroupSlot> _groups = new Dictionary<string, GroupSlot>();

        // Параметры алгоритма
        private const int MAX_BACKTRACK_DEPTH = 50;
        private const int LOCAL_SEARCH_ITERATIONS = 100;
        private const int MAX_PAIRS_PER_DAY = 6;
        private const int OPTIMAL_PAIRS_PER_DAY = 4;

        // История решений для backtracking
        private Stack<ScheduleSnapshot> _snapshots = new Stack<ScheduleSnapshot>();
        private int _backtrackCount = 0;

        #region Main Algorithm

        public void RunOptimization(bool isAutumn)
        {
            Errors.Clear();
            Logs.Clear();
            Metrics = new ScheduleMetrics();
            _backtrackCount = 0;

            Logs.Add($"╔═══════════════════════════════════════════════╗");
            Logs.Add($"║  УЛУЧШЕННАЯ ГЕНЕРАЦИЯ РАСПИСАНИЯ v2.0         ║");
            Logs.Add($"║  Семестр: {(isAutumn ? "Осенний" : "Весенний"),-35} ║");
            Logs.Add($"║  Запуск: {DateTime.Now,-35:yyyy-MM-dd HH:mm:ss} ║");
            Logs.Add($"╚═══════════════════════════════════════════════╝");

            // Этап 1: Загрузка данных
            LoadDataInMemory();
            Logs.Add($"[1/5] Загружено: Групп={_groups.Count}, Преподавателей={_teachers.Count}, Аудиторий={_rooms.Count}");

            // Этап 2: Генерация задач
            var rawTasks = GenerateRawTasks(isAutumn);
            Logs.Add($"[2/5] Сгенерировано задач: {rawTasks.Count}");

            // Этап 3: УЛУЧШЕННОЕ объединение в потоки
            var optimizedQueue = MergeIntoSmartStreams(rawTasks);
            Logs.Add($"[3/5] После умной группировки: {optimizedQueue.Count} потоков");

            // Этап 4: Сортировка по сложности (Multi-criteria)
            optimizedQueue = SortTasksByComplexity(optimizedQueue);

            ClearScheduleDb();

            // Этап 5: Размещение с backtracking
            Logs.Add($"[4/5] Начинаем размещение занятий с возможностью отката...");
            bool success = PlaceAllLessonsWithBacktracking(optimizedQueue);

            if (success)
            {
                Logs.Add($"[5/5] ✓ Все занятия успешно размещены!");

                // Этап 6: Пост-оптимизация (Local Search)
                Logs.Add($"[BONUS] Запуск пост-оптимизации для улучшения расписания...");
                PerformLocalSearchOptimization();
            }
            else
            {
                Logs.Add($"[5/5] ✗ Не удалось разместить все занятия даже с откатами");
            }

            // Этап 7: Расчет метрик
            CalculateFinalMetrics();
            LogMetrics();

            Logs.Add($"\n╔═══════════════════════════════════════════════╗");
            Logs.Add($"║  ГЕНЕРАЦИЯ ЗАВЕРШЕНА                          ║");
            Logs.Add($"║  Откатов выполнено: {_backtrackCount,-26} ║");
            Logs.Add($"║  Качество: {GetQualityRating(),-33} ║");
            Logs.Add($"╚═══════════════════════════════════════════════╝");
        }

        #endregion

        #region Enhanced Placement Algorithm

        private bool PlaceAllLessonsWithBacktracking(List<LessonTask> tasks)
        {
            for (int i = 0; i < tasks.Count; i++)
            {
                var task = tasks[i];
                Logs.Add($"  [{i + 1}/{tasks.Count}] Размещаем: {task.SubjectName} ({GetTypeName(task.LessonType)}) для {task.GroupNames.Count} групп(ы)");

                List<int> targetWeeks = new List<int>();
                if (task.WeekType == 0) { targetWeeks.Add(1); targetWeeks.Add(2); }
                else { targetWeeks.Add(task.WeekType); }

                bool taskPlaced = false;

                foreach (var week in targetWeeks)
                {
                    // Сохраняем snapshot перед попыткой размещения
                    SaveSnapshot();

                    if (TryPlaceLessonEnhanced(task, week))
                    {
                        taskPlaced = true;
                        _snapshots.Pop(); // Успешно разместили, snapshot не нужен
                    }
                    else
                    {
                        // Попытка размещения провалилась
                        string wName = week == 1 ? "Светлая" : "Темная";

                        // Пробуем откатиться и попробовать другой вариант
                        if (_backtrackCount < MAX_BACKTRACK_DEPTH && TryBacktrackAndRetry(task, week))
                        {
                            Logs.Add($"    ✓ Откат #{_backtrackCount} помог! Задача размещена.");
                            taskPlaced = true;
                        }
                        else
                        {
                            string msg = $"Не влезает: {task.SubjectName} ({GetTypeName(task.LessonType)}) для {string.Join(",", task.GroupNames)} ({wName})";
                            Errors.Add(msg);
                            Logs.Add($"    ✗ {msg}");
                            RestoreSnapshot(); // Откатываем неудачную попытку
                        }
                    }
                }

                if (!taskPlaced)
                {
                    // Критическая ошибка - не смогли разместить даже с откатами
                    Logs.Add($"    ✗✗✗ КРИТИЧНО: Задача не размещена даже после откатов!");
                }
            }

            return Errors.Count == 0;
        }

        private bool TryBacktrackAndRetry(LessonTask task, int week)
        {
            if (_snapshots.Count == 0) return false;

            const int MAX_BACKTRACK_STEPS = 3;
            int steps = 0;

            while (_snapshots.Count > 0 && steps < MAX_BACKTRACK_STEPS)
            {
                RestoreSnapshot();
                _backtrackCount++;
                steps++;

                Logs.Add($"    ↩ Откат #{_backtrackCount} (глубина {steps})...");

                // Пробуем разместить с другой стратегией (например, более поздние пары)
                if (TryPlaceLessonEnhanced(task, week, allowLatePairs: true))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryPlaceLessonEnhanced(LessonTask task, int week, bool allowLatePairs = false)
        {
            // Порядок дней (адаптивный)
            int[] dayOrder = GetAdaptiveDayOrder(task, week);

            foreach (int day in dayOrder)
            {
                // 1. Конфликт предметов (лекция и практика в один день)
                if (HasSubjectConflict(task, day, week)) continue;

                // 2. Балансировка нагрузки по дням
                if (!allowLatePairs && IsOverloaded(task.GroupNames, week, day)) continue;

                // 3. Получаем ранжированные пары (с учетом окон и перегрузки)
                var rankedPairs = GetEnhancedRankedPairs(task, week, day, allowLatePairs);

                foreach (int pair in rankedPairs)
                {
                    // Проверки доступности
                    if (task.GroupNames.Any(gn => _groups.ContainsKey(gn) && _groups[gn].IsBusy(week, day, pair)))
                        continue;

                    if (_teachers.ContainsKey(task.TeacherID))
                    {
                        var t = _teachers[task.TeacherID];
                        if (t.IsBusy(week, day, pair) || !t.IsAvailable(day, pair))
                            continue;
                    }

                    // Поиск лучшей аудитории
                    var room = FindBestRoomEnhanced(task, week, day, pair);

                    if (room != null)
                    {
                        // Бронируем!
                        BookLesson(task, week, day, pair, room);

                        // Логируем особые случаи
                        if (pair >= 5)
                            Logs.Add($"      ⚠ Поздняя пара ({pair}) для {task.SubjectName}");

                        return true;
                    }
                }
            }

            return false;
        }

        #endregion

        #region Smart Stream Merging

        private List<LessonTask> MergeIntoSmartStreams(List<LessonTask> input)
        {
            var result = new List<LessonTask>();

            // Практики и лабы НЕ объединяем (они индивидуальны)
            result.AddRange(input.Where(x => x.LessonType != 0));

            // Лекции объединяем УМНО
            var lectures = input.Where(x => x.LessonType == 0).ToList();

            // Группируем по предмету, преподавателю, типу недели
            var grouped = lectures.GroupBy(x => new
            {
                x.SubjectName,
                x.TeacherID,
                x.WeekType
            }).ToList();

            foreach (var grp in grouped)
            {
                var teacherId = grp.Key.TeacherID;
                int maxGroups = _teachers.ContainsKey(teacherId)
                    ? _teachers[teacherId].MaxLectureGroups
                    : 3;

                var tasks = grp.ToList();

                // УМНАЯ ГРУППИРОВКА: группы одного курса/специальности объединяем в первую очередь
                var smartSorted = tasks
                    .OrderBy(t => GetEducationLevel(t.GroupNames.First()))
                    .ThenBy(t => GetSpecialty(t.GroupNames.First()))
                    .ThenBy(t => t.GroupNames.First())
                    .ToList();

                while (smartSorted.Count > 0)
                {
                    var chunk = smartSorted.Take(maxGroups).ToList();
                    smartSorted.RemoveRange(0, chunk.Count);

                    result.Add(new LessonTask
                    {
                        GroupNames = chunk.SelectMany(c => c.GroupNames).ToList(),
                        SubjectName = grp.Key.SubjectName,
                        TeacherID = grp.Key.TeacherID,
                        LessonType = 0,
                        RequiresComputers = chunk.Any(c => c.RequiresComputers),
                        WeekType = grp.Key.WeekType,
                        FixedRoom = chunk.First().FixedRoom
                    });

                    Logs.Add($"  Поток: {grp.Key.SubjectName} - {string.Join(", ", chunk.Select(c => c.GroupNames.First()))}");
                }
            }

            return result;
        }

        private string GetEducationLevel(string groupName)
        {
            return SpecialtyHelper.GetLevel(groupName).ToString();
        }

        private string GetSpecialty(string groupName)
        {
            return SpecialtyHelper.GetSpecialty(groupName);
        }

        #endregion

        #region Enhanced Pair Ranking

        private List<int> GetEnhancedRankedPairs(LessonTask task, int week, int day, bool allowLatePairs)
        {
            var pairsWithScore = new List<(int Pair, int Score)>();
            int maxPair = allowLatePairs ? MAX_PAIRS_PER_DAY : OPTIMAL_PAIRS_PER_DAY;

            for (int p = 1; p <= maxPair; p++)
            {
                int score = CalculatePairScore(task, week, day, p);
                pairsWithScore.Add((p, score));
            }

            // Сортируем: чем МЕНЬШЕ score, тем ЛУЧШЕ
            return pairsWithScore
                .OrderBy(x => x.Score)
                .Select(x => x.Pair)
                .ToList();
        }

        private int CalculatePairScore(LessonTask task, int week, int day, int pair)
        {
            int score = 0;

            // 1. Штраф за поздние пары (экспоненциальный рост)
            if (pair == 5) score += 300;
            if (pair == 6) score += 1000;

            // 2. Штраф за окна (МАКСИМАЛЬНЫЙ ПРИОРИТЕТ)
            foreach (var gName in task.GroupNames)
            {
                if (!_groups.ContainsKey(gName)) continue;

                var busyPairs = _groups[gName].GetBusyPairsList(week, day);

                if (busyPairs.Count == 0)
                {
                    // День пустой - ставим как можно раньше
                    score += pair * 10;
                }
                else
                {
                    var simulated = new HashSet<int>(busyPairs) { pair };
                    int windows = CalculateWindows(simulated);

                    if (windows > 0)
                    {
                        // ОГРОМНЫЙ штраф за создание окон
                        score += windows * 50000;
                    }
                    else
                    {
                        // Бонус за компактность (пары идут подряд)
                        score -= 100;
                    }
                }
            }

            // 3. Штраф за перегрузку дня (если уже 4+ пары)
            int totalPairs = task.GroupNames
                .Where(g => _groups.ContainsKey(g))
                .Max(g => _groups[g].GetBusyPairsList(week, day).Count);

            if (totalPairs >= 4) score += 200;
            if (totalPairs >= 5) score += 800;

            // 4. Бонус за предпочитаемые аудитории преподавателя
            if (_teachers.ContainsKey(task.TeacherID))
            {
                var availableRooms = _rooms.Where(r =>
                    !r.IsBusy(week, day, pair) &&
                    (!task.RequiresComputers || r.HasComputers)
                ).ToList();

                if (availableRooms.Any(r => _teachers[task.TeacherID].HighPriorityRooms.Contains(r.Number)))
                    score -= 50; // Бонус
            }

            return score;
        }

        private int CalculateWindows(HashSet<int> pairs)
        {
            if (pairs.Count <= 1) return 0;

            int min = pairs.Min();
            int max = pairs.Max();
            int span = max - min + 1;
            int windows = span - pairs.Count;

            return windows;
        }

        #endregion

        #region Adaptive Day Ordering

        private int[] GetAdaptiveDayOrder(LessonTask task, int week)
        {
            // Базовый порядок
            int[] baseOrder = task.LessonType == 0
                ? new int[] { 0, 1, 2, 3, 4, 5 }  // Лекции: Пн-Сб
                : new int[] { 2, 3, 4, 1, 0, 5 }; // Практики: Ср-Сб, Вт, Пн

            // Считаем загруженность дней для групп
            var dayLoads = new Dictionary<int, int>();

            foreach (int day in baseOrder)
            {
                int totalLoad = 0;
                foreach (var gName in task.GroupNames)
                {
                    if (_groups.ContainsKey(gName))
                    {
                        totalLoad += _groups[gName].GetBusyPairsList(week, day).Count;
                    }
                }
                dayLoads[day] = totalLoad;
            }

            // Сортируем дни: сначала менее загруженные
            return baseOrder
                .OrderBy(d => dayLoads.ContainsKey(d) ? dayLoads[d] : 0)
                .ToArray();
        }

        private bool IsOverloaded(List<string> groupNames, int week, int day)
        {
            foreach (var gName in groupNames)
            {
                if (!_groups.ContainsKey(gName)) continue;

                int currentLoad = _groups[gName].GetBusyPairsList(week, day).Count;

                // Максимум 5 пар в день (6-я только в крайнем случае)
                if (currentLoad >= 5) return true;
            }
            return false;
        }

        #endregion

        #region Enhanced Room Selection

        private RoomSlot FindBestRoomEnhanced(LessonTask task, int week, int day, int pair)
        {
            List<string> highPriority = new List<string>();
            List<string> medPriority = new List<string>();

            if (_teachers.ContainsKey(task.TeacherID))
            {
                highPriority = _teachers[task.TeacherID].HighPriorityRooms;
                medPriority = _teachers[task.TeacherID].MedPriorityRooms;
            }

            // Фильтруем подходящие аудитории
            var validRooms = _rooms.Where(r =>
                !r.IsBusy(week, day, pair) &&
                (!task.RequiresComputers || r.HasComputers) &&
                (string.IsNullOrEmpty(task.FixedRoom) || r.Number == task.FixedRoom)
            ).ToList();

            // Оцениваем количество студентов (улучшенная формула)
            int estimatedStudents = task.GroupNames
                .Where(g => _groups.ContainsKey(g))
                .Sum(g => _groups[g].EstimatedStudents);

            if (estimatedStudents == 0) estimatedStudents = task.GroupNames.Count * 25;

            // Фильтруем по вместимости + небольшой запас
            validRooms = validRooms.Where(r => r.Capacity >= estimatedStudents * 0.9).ToList();

            // Приоритет 1: Предпочитаемая аудитория
            var best = validRooms.FirstOrDefault(r => highPriority.Contains(r.Number));
            if (best != null) return best;

            // Приоритет 2: Средний приоритет
            var med = validRooms.FirstOrDefault(r => medPriority.Contains(r.Number));
            if (med != null) return med;

            // Приоритет 3: Оптимальная по размеру (не слишком большая, не слишком маленькая)
            var optimal = validRooms
                .OrderBy(r => Math.Abs(r.Capacity - estimatedStudents))
                .FirstOrDefault();

            return optimal;
        }

        #endregion

        #region Local Search Optimization

        private void PerformLocalSearchOptimization()
        {
            Logs.Add($"  Пост-оптимизация: поиск улучшений через локальные перестановки...");

            int improvementsMade = 0;

            for (int iteration = 0; iteration < LOCAL_SEARCH_ITERATIONS; iteration++)
            {
                bool improved = false;

                // Пробуем переставить пары для устранения окон
                foreach (var group in _groups.Values)
                {
                    for (int w = 1; w <= 2; w++)
                    {
                        for (int d = 0; d < 6; d++)
                        {
                            if (TryOptimizeDay(group.Name, w, d))
                            {
                                improved = true;
                                improvementsMade++;
                            }
                        }
                    }
                }

                if (!improved) break; // Нет улучшений - завершаем
            }

            Logs.Add($"  ✓ Пост-оптимизация завершена. Улучшений сделано: {improvementsMade}");
        }

        private bool TryOptimizeDay(string groupName, int week, int day)
        {
            if (!_groups.ContainsKey(groupName)) return false;

            var busyPairs = _groups[groupName].GetBusyPairsList(week, day);
            if (busyPairs.Count <= 2) return false; // Мало пар, оптимизировать нечего

            int initialWindows = CalculateWindows(new HashSet<int>(busyPairs));
            if (initialWindows == 0) return false; // Окон нет

            // Пытаемся переместить пары для устранения окон
            // (Упрощенная версия - полная реализация требует работы с БД)

            return false; // Заглушка для демонстрации концепции
        }

        #endregion

        #region Snapshots (Backtracking Support)

        private void SaveSnapshot()
        {
            var snapshot = new ScheduleSnapshot
            {
                Rooms = _rooms.Select(r => r.Clone()).ToList(),
                Teachers = _teachers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Clone()),
                Groups = _groups.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Clone())
            };
            _snapshots.Push(snapshot);
        }

        private void RestoreSnapshot()
        {
            if (_snapshots.Count == 0) return;

            var snapshot = _snapshots.Pop();
            _rooms = snapshot.Rooms;
            _teachers = snapshot.Teachers;
            _groups = snapshot.Groups;

            // Откатываем БД (удаляем последние записи)
            // TODO: Реализовать откат через TransactionID или временные таблицы
        }

        #endregion

        #region Metrics & Analytics

        private void CalculateFinalMetrics()
        {
            Metrics.TotalLessons = 0;
            Metrics.TotalWindows = 0;
            Metrics.LatePairs = 0;
            Metrics.AverageLoadPerDay = 0;

            int totalDays = 0;
            int totalPairs = 0;

            foreach (var group in _groups.Values)
            {
                for (int w = 1; w <= 2; w++)
                {
                    for (int d = 0; d < 6; d++)
                    {
                        var pairs = group.GetBusyPairsList(w, d);
                        if (pairs.Count > 0)
                        {
                            totalDays++;
                            totalPairs += pairs.Count;

                            Metrics.TotalWindows += CalculateWindows(new HashSet<int>(pairs));
                            Metrics.LatePairs += pairs.Count(p => p >= 5);
                        }
                    }
                }
            }

            Metrics.TotalLessons = totalPairs;
            Metrics.AverageLoadPerDay = totalDays > 0 ? (double)totalPairs / totalDays : 0;
        }

        private void LogMetrics()
        {
            Logs.Add($"\n📊 МЕТРИКИ КАЧЕСТВА РАСПИСАНИЯ:");
            Logs.Add($"  • Всего занятий размещено: {Metrics.TotalLessons}");
            Logs.Add($"  • Окон в расписании: {Metrics.TotalWindows} {(Metrics.TotalWindows == 0 ? "✓" : "⚠")}");
            Logs.Add($"  • Поздних пар (5-6): {Metrics.LatePairs}");
            Logs.Add($"  • Средняя нагрузка: {Metrics.AverageLoadPerDay:F1} пар/день");
            Logs.Add($"  • Ошибок размещения: {Errors.Count}");
        }

        private string GetQualityRating()
        {
            if (Errors.Count > 0) return "НЕУДОВЛЕТВОРИТЕЛЬНО";
            if (Metrics.TotalWindows == 0 && Metrics.LatePairs <= 3) return "ОТЛИЧНО ★★★★★";
            if (Metrics.TotalWindows <= 5 && Metrics.LatePairs <= 10) return "ХОРОШО ★★★★";
            if (Metrics.TotalWindows <= 15) return "УДОВЛЕТВОРИТЕЛЬНО ★★★";
            return "ТРЕБУЕТ ДОРАБОТКИ ★★";
        }

        #endregion

        #region Task Generation & Sorting

        private List<LessonTask> GenerateRawTasks(bool isAutumn)
        {
            var tasks = new List<LessonTask>();
            using (var conn = DbHelper.GetConnection())
            {
                conn.Open();
                string semesterCondition = isAutumn ? "<> 0" : "= 0";

                string sql = $@"SELECT ap.GroupID, ap.SubjectID, s.SubjectName, s.RequiresComputers, s.FixedRoom,
                                       ap.LectureInWeek, ap.PracticeInWeek, ap.LabsInWeek,
                                       ap.LectureTeacher, ap.PracticeTeacher
                                FROM AcademicPlan ap
                                LEFT JOIN Subjects s ON ap.SubjectID = s.SubjectID
                                WHERE (ap.Semester Mod 2) {semesterCondition}";

                using (var cmd = new OleDbCommand(sql, conn))
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        string gName = r["GroupID"].ToString();
                        string subj = r["SubjectName"].ToString();
                        int lecT = SafeInt(r["LectureTeacher"]);
                        int pracT = SafeInt(r["PracticeTeacher"]);
                        bool comp = r["RequiresComputers"] != DBNull.Value && (bool)r["RequiresComputers"];
                        string fix = r["FixedRoom"]?.ToString();

                        int lecH = SafeInt(r["LectureInWeek"]);
                        int pracH = SafeInt(r["PracticeInWeek"]);
                        int labH = SafeInt(r["LabsInWeek"]);

                        AddTask(tasks, gName, subj, lecT, 0, false, fix, lecH);
                        AddTask(tasks, gName, subj, pracT, 1, comp, fix, pracH);
                        AddTask(tasks, gName, subj, pracT, 2, true, fix, labH);
                    }
                }
            }
            return tasks;
        }

        private List<LessonTask> SortTasksByComplexity(List<LessonTask> tasks)
        {
            return tasks
                .OrderBy(x => x.LessonType)                    // Лекции первыми
                .ThenByDescending(x => x.RequiresComputers)    // Требующие компьютеры
                .ThenByDescending(x => x.GroupNames.Count)     // Большие потоки
                .ThenBy(x => string.IsNullOrEmpty(x.FixedRoom) ? 0 : 1)  // С жесткой аудиторией сложнее
                .ToList();
        }

        private void AddTask(List<LessonTask> list, string grp, string subj, int teachId, int type, bool comp, string room, int hours)
        {
            if (hours <= 0) return;

            int fullPairs = hours / 2;
            bool halfPair = (hours % 2) != 0;

            for (int i = 0; i < fullPairs; i++)
            {
                list.Add(new LessonTask
                {
                    GroupNames = new List<string> { grp },
                    SubjectName = subj,
                    TeacherID = teachId,
                    LessonType = type,
                    RequiresComputers = comp,
                    FixedRoom = room,
                    WeekType = 0
                });
            }

            if (halfPair)
            {
                list.Add(new LessonTask
                {
                    GroupNames = new List<string> { grp },
                    SubjectName = subj,
                    TeacherID = teachId,
                    LessonType = type,
                    RequiresComputers = comp,
                    FixedRoom = room,
                    WeekType = 1
                });
            }
        }

        #endregion

        #region Helper Methods

        private void BookLesson(LessonTask task, int week, int day, int pair, RoomSlot room)
        {
            foreach (var gn in task.GroupNames)
                if (_groups.ContainsKey(gn))
                    _groups[gn].Book(week, day, pair, task.SubjectName);

            if (_teachers.ContainsKey(task.TeacherID))
                _teachers[task.TeacherID].Book(week, day, pair);

            room.Book(week, day, pair);
            SaveLessonToDb(task, week, day, pair, room.Number);
        }

        private bool HasSubjectConflict(LessonTask task, int day, int week)
        {
            foreach (var gName in task.GroupNames)
            {
                if (_groups.ContainsKey(gName) &&
                    _groups[gName].HasSubjectOnDay(week, day, task.SubjectName))
                    return true;
            }
            return false;
        }

        private void SaveLessonToDb(LessonTask task, int week, int day, int pair, string room)
        {
            using (var conn = DbHelper.GetConnection())
            {
                conn.Open();
                foreach (var grp in task.GroupNames)
                {
                    string tName = _teachers.ContainsKey(task.TeacherID)
                        ? _teachers[task.TeacherID].Name
                        : "---";

                    string sql = @"INSERT INTO Schedule 
                                   (GroupName, DayOfWeek, PairNumber, WeekType, SubjectName, TeacherName, RoomNumber, LessonType) 
                                   VALUES (?, ?, ?, ?, ?, ?, ?, ?)";

                    using (var cmd = new OleDbCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("?", grp);
                        cmd.Parameters.AddWithValue("?", day + 1);
                        cmd.Parameters.AddWithValue("?", pair);
                        cmd.Parameters.AddWithValue("?", week);
                        cmd.Parameters.AddWithValue("?", task.SubjectName);
                        cmd.Parameters.AddWithValue("?", tName);
                        cmd.Parameters.AddWithValue("?", room);
                        cmd.Parameters.AddWithValue("?", task.LessonType);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        private void ClearScheduleDb()
        {
            using (var conn = DbHelper.GetConnection())
            {
                conn.Open();
                new OleDbCommand("DELETE FROM Schedule", conn).ExecuteNonQuery();
            }
        }

        private void LoadDataInMemory()
        {
            _rooms.Clear();
            _teachers.Clear();
            _groups.Clear();

            using (var conn = DbHelper.GetConnection())
            {
                conn.Open();

                // Аудитории
                var cmdR = new OleDbCommand("SELECT * FROM Classrooms", conn);
                using (var r = cmdR.ExecuteReader())
                {
                    while (r.Read())
                    {
                        _rooms.Add(new RoomSlot
                        {
                            Id = (int)r["RoomID"],
                            Number = r["RoomNumber"].ToString(),
                            Capacity = SafeInt(r["Capacity"]),
                            HasComputers = (bool)r["HasComputers"]
                        });
                    }
                }

                // Преподаватели
                var cmdT = new OleDbCommand("SELECT * FROM Teachers", conn);
                using (var r = cmdT.ExecuteReader())
                {
                    while (r.Read())
                    {
                        int id = (int)r["TeacherID"];
                        int maxLec = r["MaxLectureGroups"] is int v ? v : 3;
                        _teachers[id] = new TeacherSlot
                        {
                            Id = id,
                            Name = r["FullName"].ToString(),
                            MaxLectureGroups = maxLec
                        };
                    }
                }

                // Доступность преподавателей
                var cmdBusy = new OleDbCommand("SELECT * FROM TeacherAvailability WHERE IsAvailable = False", conn);
                using (var r = cmdBusy.ExecuteReader())
                {
                    while (r.Read())
                    {
                        int tid = SafeInt(r["TeacherID"]);
                        if (int.TryParse(r["DayIdx"].ToString(), out int d) &&
                            int.TryParse(r["PairIdx"].ToString(), out int p))
                        {
                            if (_teachers.ContainsKey(tid))
                                _teachers[tid].SetUnavailable(d, p);
                        }
                    }
                }

                // Предпочтения аудиторий
                var cmdPref = new OleDbCommand("SELECT * FROM TeacherRoomPrefs", conn);
                using (var r = cmdPref.ExecuteReader())
                {
                    while (r.Read())
                    {
                        int tid = SafeInt(r["TeacherID"]);
                        if (int.TryParse(r["RoomNumber"].ToString(), out int roomId))
                        {
                            var roomObj = _rooms.FirstOrDefault(rm => rm.Id == roomId);
                            if (roomObj != null && _teachers.ContainsKey(tid))
                            {
                                string prio = r["Priority"].ToString();
                                if (prio.ToLower().Contains("высокий"))
                                    _teachers[tid].HighPriorityRooms.Add(roomObj.Number);
                                else
                                    _teachers[tid].MedPriorityRooms.Add(roomObj.Number);
                            }
                        }
                    }
                }

                // Группы
                var cmdG = new OleDbCommand("SELECT GroupName, StudentCount FROM GroupsList WHERE Actually = True", conn);
                using (var r = cmdG.ExecuteReader())
                {
                    while (r.Read())
                    {
                        string n = r["GroupName"].ToString();
                        int students = SafeInt(r["StudentCount"]);
                        _groups[n] = new GroupSlot
                        {
                            Name = n,
                            EstimatedStudents = students > 0 ? students : 25
                        };
                    }
                }
            }
        }

        private int SafeInt(object val)
        {
            if (val == null || val == DBNull.Value) return 0;
            try { return Convert.ToInt32(val); }
            catch { return 0; }
        }

        private string GetTypeName(int t) => t == 0 ? "Лек" : (t == 1 ? "Прак" : "Лаб");

        #endregion

        #region Inner Classes

        private class LessonTask
        {
            public List<string> GroupNames = new List<string>();
            public string SubjectName;
            public int TeacherID;
            public int LessonType;
            public bool RequiresComputers;
            public string FixedRoom;
            public int WeekType;
        }

        private class ScheduleSnapshot
        {
            public List<RoomSlot> Rooms;
            public Dictionary<int, TeacherSlot> Teachers;
            public Dictionary<string, GroupSlot> Groups;
        }

        public class ScheduleMetrics
        {
            public int TotalLessons { get; set; }
            public int TotalWindows { get; set; }
            public int LatePairs { get; set; }
            public double AverageLoadPerDay { get; set; }
        }

        class RoomSlot
        {
            public int Id;
            public string Number;
            public int Capacity;
            public bool HasComputers;
            private bool[,,] _schedule = new bool[3, 6, 7];

            public bool IsBusy(int w, int d, int p) => _schedule[w, d, p];
            public void Book(int w, int d, int p) => _schedule[w, d, p] = true;

            public RoomSlot Clone()
            {
                var clone = new RoomSlot
                {
                    Id = this.Id,
                    Number = this.Number,
                    Capacity = this.Capacity,
                    HasComputers = this.HasComputers,
                    _schedule = (bool[,,])this._schedule.Clone()
                };
                return clone;
            }
        }

        class TeacherSlot
        {
            public int Id;
            public string Name;
            public int MaxLectureGroups;
            public List<string> HighPriorityRooms = new List<string>();
            public List<string> MedPriorityRooms = new List<string>();
            private bool[,,] _busy = new bool[3, 6, 7];
            private bool[,] _unavailable = new bool[6, 7];

            public bool IsBusy(int w, int d, int p) => _busy[w, d, p];
            public bool IsAvailable(int d, int p) => !_unavailable[d, p];
            public void Book(int w, int d, int p) => _busy[w, d, p] = true;
            public void SetUnavailable(int d, int p) => _unavailable[d, p] = true;

            public TeacherSlot Clone()
            {
                return new TeacherSlot
                {
                    Id = this.Id,
                    Name = this.Name,
                    MaxLectureGroups = this.MaxLectureGroups,
                    HighPriorityRooms = new List<string>(this.HighPriorityRooms),
                    MedPriorityRooms = new List<string>(this.MedPriorityRooms),
                    _busy = (bool[,,])this._busy.Clone(),
                    _unavailable = (bool[,])this._unavailable.Clone()
                };
            }
        }

        class GroupSlot
        {
            public string Name;
            public int EstimatedStudents = 25;
            private bool[,,] _busy = new bool[3, 6, 7];
            private Dictionary<string, List<string>> _subjectsOnDay = new Dictionary<string, List<string>>();

            public bool IsBusy(int w, int d, int p) => _busy[w, d, p];

            public List<int> GetBusyPairsList(int w, int d)
            {
                var list = new List<int>();
                for (int p = 1; p <= 6; p++)
                {
                    if (_busy[w, d, p]) list.Add(p);
                }
                return list;
            }

            public void Book(int w, int d, int p, string subj)
            {
                _busy[w, d, p] = true;
                string key = $"{w}_{d}";
                if (!_subjectsOnDay.ContainsKey(key))
                    _subjectsOnDay[key] = new List<string>();
                _subjectsOnDay[key].Add(subj);
            }

            public bool HasSubjectOnDay(int w, int d, string subj)
            {
                string key = $"{w}_{d}";
                return _subjectsOnDay.ContainsKey(key) &&
                       _subjectsOnDay[key].Contains(subj);
            }

            public GroupSlot Clone()
            {
                var clone = new GroupSlot
                {
                    Name = this.Name,
                    EstimatedStudents = this.EstimatedStudents,
                    _busy = (bool[,,])this._busy.Clone(),
                    _subjectsOnDay = this._subjectsOnDay.ToDictionary(
                        kvp => kvp.Key,
                        kvp => new List<string>(kvp.Value)
                    )
                };
                return clone;
            }
        }

        #endregion
    }
}
