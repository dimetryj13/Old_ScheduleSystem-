using System;
using System.Collections.Generic;
using ScheduleSystem.Data;
using ScheduleSystem.Models;

namespace ScheduleSystem.Logic
{
    public class ScheduleGeneratorService
    {
        private readonly GeneratorSettings _settings;
        public List<string> GenerationErrors { get; private set; } = new List<string>();

        public ScheduleGeneratorService(GeneratorSettings settings)
        {
            _settings = settings;
        }

        public void GenerateAndSave()
        {
            GenerationErrors.Clear();

            // 1. Проверяем соединение с БД
            if (!DbHelper.TestConnection())
            {
                GenerationErrors.Add("Нет соединения с базой данных.");
                return;
            }

            // 2. Загружаем данные
            var planItems = DbHelper.GetFullAcademicPlan();
            var rooms = DbHelper.GetAllRooms();

            if (planItems.Count == 0)
            {
                GenerationErrors.Add("Учебный план пуст! Нечего генерировать.");
                return;
            }

            if (rooms.Count == 0)
            {
                GenerationErrors.Add("Список аудиторий пуст!");
                return;
            }

            // 3. Запускаем DeepAI алгоритм
            // Мы используем ТОЛЬКО новый алгоритм, так как он заменяет все старые режимы
            var optimizer = new DeepScheduleOptimizer(_settings);

            // Генерация расписания
            var schedule = optimizer.Generate(planItems, rooms);

            // Собираем ошибки, которые возникли в процессе (например, "не влезло")
            GenerationErrors.AddRange(optimizer.Errors);

            // 4. Сохраняем результат в БД
            try
            {
                DbHelper.SaveScheduleToDb(schedule);
            }
            catch (Exception ex)
            {
                GenerationErrors.Add($"Ошибка при сохранении в БД: {ex.Message}");
            }

            // 5. Опционально: Запускаем оценщик (Evaluator)
            // Он сейчас работает как заглушка, но может пригодиться в будущем для статистики
            var evaluator = new ScheduleEvaluator();
            evaluator.Evaluate(schedule);
        }
    }
}