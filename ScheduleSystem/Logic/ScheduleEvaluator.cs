using System;
using System.Collections.Generic;
using ScheduleSystem.Models;

namespace ScheduleSystem.Logic
{
    public class ScheduleEvaluator
    {
        // 1. Пустой конструктор (для нового кода)
        public ScheduleEvaluator() { }

        // 2. Конструктор-заглушка для СТАРОГО кода (принимает всё что угодно)
        public ScheduleEvaluator(params object[] args)
        {
            // Просто игнорируем старые параметры
        }

        // 3. Метод для НОВОГО кода
        public void Evaluate(List<ScheduleItem> schedule)
        {
            // Здесь в будущем можно написать логику оценки
        }

        // 4. Метод-заглушка для СТАРОГО кода (принимает 5 аргументов)
        public void Evaluate(object arg1, object arg2, object arg3, object arg4, object arg5)
        {
            // Ничего не делаем, чтобы не ломать компиляцию старых вызовов
        }
    }
}