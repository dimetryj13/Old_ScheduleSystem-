using System;
using System.Collections.Generic;

namespace ScheduleSystem.Models
{
    public enum EducationLevel { Bachelor, Master, Specialist, Unknown }
    public enum EducationForm { FullTime, Correspondence } // Очка/Заочка

    public static class SpecialtyHelper
    {
        // Словарь: Уровень -> Список специальностей
        public static Dictionary<EducationLevel, string[]> SpecialtiesByLevel = new Dictionary<EducationLevel, string[]>
        {
            {
                EducationLevel.Bachelor, new string[]
                {
                    "38.03.05 «Бизнес-информатика»",
                    "09.03.01 «ИНФОРМАТИКА И ВЫЧИСЛИТЕЛЬНАЯ ТЕХНИКА»",
                    "20.03.01 «Техносферная безопасность»",
                    "38.03.04 «Государственное и муниципальное управление»",
                    "38.03.01 «Экономика»"
                }
            },
            {
                EducationLevel.Master, new string[]
                {
                    "38.04.05 «Бизнес-информатика»",
                    "09.04.01 «Информатика и вычислительная техника»",
                    "23.04.03 «Эксплуатация транспортно-технологических машин и комплексов»",
                    "38.04.02 «Менеджмент»"
                }
            },
            {
                EducationLevel.Specialist, new string[]
                {
                    "23.05.01 «НАЗЕМНЫЕ ТРАНСПОРТНО-ТЕХНОЛОГИЧЕСКИЕ СРЕДСТВА»"
                }
            }
        };

        // Определение уровня образования по имени группы
        public static EducationLevel GetLevel(string groupName)
        {
            var clean = Normalize(groupName);
            if (!clean.Contains("-")) return EducationLevel.Unknown;

            string suffix = clean.Split('-')[1];
            if (suffix.Length == 0) return EducationLevel.Unknown;

            char lastChar = suffix[suffix.Length - 1];
            char firstDigit = suffix[0];

            // Магистратура (есть 'м')
            if (lastChar == 'м' || lastChar == 'm') return EducationLevel.Master;

            // Специалитет (есть 'с' ИЛИ код 5xx)
            if (lastChar == 'с' || lastChar == 'c' || lastChar == 's' || firstDigit == '5')
                return EducationLevel.Specialist;

            // Бакалавриат (все остальные: 1xx, 2xx, 3xx, 6xx, 8xx без букв)
            return EducationLevel.Bachelor;
        }

        // Определение формы (очка/заочка)
        public static EducationForm GetForm(string groupName)
        {
            var clean = Normalize(groupName);
            if (!clean.Contains("-")) return EducationForm.FullTime;

            // Смотрим префикс (до дефиса): "КР" или "КРз"
            string prefix = clean.Split('-')[0];
            if (prefix.Contains("з") || prefix.Contains("z"))
                return EducationForm.Correspondence;

            return EducationForm.FullTime;
        }

        // Определение специальности (улучшенное)
        public static string GetSpecialty(string groupName)
        {
            string clean = Normalize(groupName);
            if (!clean.Contains("-")) return "Прочие";
            string suffix = clean.Split('-')[1];
            if (suffix.Length == 0) return "Прочие";

            char d = suffix[0]; // Первая цифра
            var lvl = GetLevel(groupName);

            // 1. БИЗНЕС-ИНФОРМАТИКА
            if (d == '1') return lvl == EducationLevel.Master
                ? "38.04.05 «Бизнес-информатика»"
                : "38.03.05 «Бизнес-информатика»";

            // 2. ИВТ
            if (d == '3') return lvl == EducationLevel.Master
                ? "09.04.01 «Информатика и вычислительная техника»"
                : "09.03.01 «ИНФОРМАТИКА И ВЫЧИСЛИТЕЛЬНАЯ ТЕХНИКА»";

            // 3. НАЗЕМНЫЕ (Спец) / ЭКСПЛУАТАЦИЯ (Маг)
            if (d == '5') return lvl == EducationLevel.Master
                ? "23.04.03 «Эксплуатация транспортно-технологических машин и комплексов»"
                : "23.05.01 «НАЗЕМНЫЕ ТРАНСПОРТНО-ТЕХНОЛОГИЧЕСКИЕ СРЕДСТВА»";

            // 4. ОСТАЛЬНЫЕ
            if (d == '8') return "20.03.01 «Техносферная безопасность»";
            if (d == '6') return "38.03.04 «Государственное и муниципальное управление»";
            if (d == '2') return lvl == EducationLevel.Master
                ? "38.04.02 «Менеджмент»"
                : "38.03.01 «Экономика»";

            return "Прочие";
        }

        private static string Normalize(string s) =>
            s.ToLower().Replace(" ", "").Replace("—", "-").Replace("–", "-");
    }
}