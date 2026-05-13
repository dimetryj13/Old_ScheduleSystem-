using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Drawing;
using ScheduleSystem.Models;
using Excel = Microsoft.Office.Interop.Excel;

namespace ScheduleSystem.Logic
{
    public class ExcelExporter
    {
        private const int DATA_START_ROW = 12;

        // Цвет заливки (Светло-серый)
        private readonly System.Drawing.Color CellBackColor = System.Drawing.Color.FromArgb(242, 242, 242);

        public void ExportScheduleToExcel(
            List<ScheduleItem> scheduleData,
            List<string> groupNamesOrdered,
            string savePath,
            IProgress<(int percent, string message)> progress)
        {
            Excel.Application excelApp = null;
            Excel.Workbook workbook = null;
            Excel.Worksheet sheet = null;

            try
            {
                progress?.Report((0, "Запуск Excel..."));

                excelApp = new Excel.Application();
                excelApp.Visible = false;
                excelApp.ScreenUpdating = false;
                excelApp.DisplayAlerts = false;

                workbook = excelApp.Workbooks.Add();
                sheet = (Excel.Worksheet)workbook.Sheets[1];
                sheet.Name = "Расписание";

                // Настройки шрифта
                sheet.Cells.Font.Name = "Times New Roman";
                sheet.Cells.Font.Size = 9;
                sheet.Cells.VerticalAlignment = Excel.XlVAlign.xlVAlignCenter;
                sheet.Cells.HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;
                sheet.Cells.WrapText = true;

                // Словарь данных
                var scheduleMap = new Dictionary<string, ScheduleItem>();
                foreach (var item in scheduleData)
                {
                    string key = $"{item.GroupName}_{item.DayOfWeek}_{item.PairNumber}_{item.WeekType}";
                    if (!scheduleMap.ContainsKey(key)) scheduleMap.Add(key, item);
                }

                // 1. ШАПКА
                progress?.Report((5, "Создание шапки..."));
                DrawOfficialHeader(sheet, groupNamesOrdered.Count * 2 + 2);

                // 2. ЗАГОЛОВКИ ГРУПП
                int headerRow = DATA_START_ROW;
                sheet.Cells[headerRow, 1] = "День";
                sheet.Cells[headerRow, 2] = "Пара";
                ((Excel.Range)sheet.Columns[1]).ColumnWidth = 4;
                ((Excel.Range)sheet.Columns[2]).ColumnWidth = 3;

                int colIndex = 3;
                foreach (var group in groupNamesOrdered)
                {
                    Excel.Range groupHeader = sheet.Range[sheet.Cells[headerRow, colIndex], sheet.Cells[headerRow, colIndex + 1]];
                    groupHeader.Merge();
                    groupHeader.Value = group;
                    groupHeader.Font.Bold = true;
                    groupHeader.Font.Size = 10;
                    groupHeader.Borders.Weight = Excel.XlBorderWeight.xlThin;

                    ((Excel.Range)sheet.Columns[colIndex]).ColumnWidth = 25;
                    ((Excel.Range)sheet.Columns[colIndex + 1]).ColumnWidth = 5;
                    colIndex += 2;
                }
                ((Excel.Range)sheet.Rows[headerRow]).RowHeight = 30;
                sheet.Application.ActiveWindow.SplitRow = headerRow;
                sheet.Application.ActiveWindow.SplitColumn = 2;
                sheet.Application.ActiveWindow.FreezePanes = true;

                // 3. СЕТКА
                int currentRow = headerRow + 1;
                string[] dayNames = { "ПОНЕДЕЛЬНИК", "ВТОРНИК", "СРЕДА", "ЧЕТВЕРГ", "ПЯТНИЦА", "СУББОТА" };

                for (int d = 1; d <= 6; d++)
                {
                    progress?.Report((20 + (d * 10), $"День {d}: {dayNames[d - 1]}..."));
                    int dayStartRow = currentRow;

                    for (int p = 1; p <= 6; p++)
                    {
                        Excel.Range pairCell = sheet.Range[sheet.Cells[currentRow, 2], sheet.Cells[currentRow + 1, 2]];
                        pairCell.Merge();
                        pairCell.Value = p;
                        pairCell.Font.Bold = true;

                        // Высокие строки (45)
                        ((Excel.Range)sheet.Rows[currentRow]).RowHeight = 45;
                        ((Excel.Range)sheet.Rows[currentRow + 1]).RowHeight = 45;

                        int gCol = 3;
                        foreach (var group in groupNamesOrdered)
                        {
                            string keyAlways = $"{group}_{d}_{p}_0";
                            string keyUp = $"{group}_{d}_{p}_1";
                            string keyDown = $"{group}_{d}_{p}_2";

                            Excel.Range cellTopSubj = (Excel.Range)sheet.Cells[currentRow, gCol];
                            Excel.Range cellTopRoom = (Excel.Range)sheet.Cells[currentRow, gCol + 1];

                            Excel.Range cellBotSubj = (Excel.Range)sheet.Cells[currentRow + 1, gCol];
                            Excel.Range cellBotRoom = (Excel.Range)sheet.Cells[currentRow + 1, gCol + 1];

                            // === 1. СТИЛИЗАЦИЯ (ЗЕБРА) ===
                            // Верхняя (светлая) - всегда белая
                            cellTopSubj.Interior.ColorIndex = Excel.XlColorIndex.xlColorIndexNone;
                            cellTopRoom.Interior.ColorIndex = Excel.XlColorIndex.xlColorIndexNone;

                            // Нижняя (темная) - ВСЕГДА серая (даже если пустая)
                            cellBotSubj.Interior.Color = CellBackColor;
                            cellBotRoom.Interior.Color = CellBackColor;

                            // === 2. ДАННЫЕ ===
                            if (scheduleMap.ContainsKey(keyAlways))
                            {
                                var item = scheduleMap[keyAlways];
                                WriteCellData(cellTopSubj, cellTopRoom, item);
                                WriteCellData(cellBotSubj, cellBotRoom, item);
                            }
                            else
                            {
                                if (scheduleMap.ContainsKey(keyUp))
                                    WriteCellData(cellTopSubj, cellTopRoom, scheduleMap[keyUp]);

                                if (scheduleMap.ContainsKey(keyDown))
                                    WriteCellData(cellBotSubj, cellBotRoom, scheduleMap[keyDown]);
                            }

                            // === 3. ГРАНИЦЫ ===
                            DrawThinBorder(cellTopSubj); DrawThinBorder(cellTopRoom);
                            DrawThinBorder(cellBotSubj); DrawThinBorder(cellBotRoom);

                            gCol += 2;
                        }
                        currentRow += 2;
                    }

                    // Оформление Дня
                    Excel.Range dayCell = sheet.Range[sheet.Cells[dayStartRow, 1], sheet.Cells[currentRow - 1, 1]];
                    dayCell.Merge();
                    dayCell.Value = dayNames[d - 1];
                    dayCell.Orientation = 90;
                    dayCell.Font.Bold = true;
                    dayCell.Font.Size = 10;
                    dayCell.BorderAround2(Excel.XlLineStyle.xlContinuous, Excel.XlBorderWeight.xlMedium);

                    Excel.Range pairBlock = sheet.Range[sheet.Cells[dayStartRow, 2], sheet.Cells[currentRow - 1, 2]];
                    pairBlock.BorderAround2(Excel.XlLineStyle.xlContinuous, Excel.XlBorderWeight.xlMedium);

                    Excel.Range fullRowLine = sheet.Range[sheet.Cells[currentRow - 1, 1], sheet.Cells[currentRow - 1, (groupNamesOrdered.Count * 2) + 2]];
                    fullRowLine.Borders[Excel.XlBordersIndex.xlEdgeBottom].Weight = Excel.XlBorderWeight.xlMedium;
                }

                progress?.Report((95, "Сохранение..."));
                workbook.SaveAs(savePath);
                progress?.Report((100, "Готово!"));
            }
            catch
            {
                throw;
            }
            finally
            {
                if (workbook != null) { workbook.Close(false); Marshal.ReleaseComObject(workbook); }
                if (excelApp != null) { excelApp.Quit(); Marshal.ReleaseComObject(excelApp); }
                if (sheet != null) Marshal.ReleaseComObject(sheet);
            }
        }

        private void DrawOfficialHeader(Excel.Worksheet sheet, int totalCols)
        {
            Excel.Range titleRange = sheet.Range[sheet.Cells[4, 2], sheet.Cells[8, totalCols - 5]];
            titleRange.Merge();
            titleRange.Value = "РАСПИСАНИЕ\nзанятий дневного отделения\nна текущий семестр\n2025/2026 учебного года";
            titleRange.Font.Size = 16;
            titleRange.Font.Bold = true;
            titleRange.Font.Name = "Times New Roman";
            titleRange.HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;
            titleRange.VerticalAlignment = Excel.XlVAlign.xlVAlignCenter;

            int startApproveCol = Math.Max(3, totalCols - 6);
            Excel.Range approveRange = sheet.Range[sheet.Cells[2, startApproveCol], sheet.Cells[5, totalCols]];
            approveRange.Merge();
            approveRange.Value = "УТВЕРЖДАЮ:\nДиректор института\n____________ Ф.И.О.\n\"___\" _________ 2026 г.";
            approveRange.HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;
            approveRange.VerticalAlignment = Excel.XlVAlign.xlVAlignCenter;
            approveRange.Font.Size = 11;
            approveRange.Font.Bold = true;
        }

        // Метод теперь только пишет данные (цвета уже настроены в цикле)
        private void WriteCellData(Excel.Range subjRange, Excel.Range roomRange, ScheduleItem item)
        {
            string fullText = item.SubjectName;
            if (!string.IsNullOrEmpty(item.TeacherName)) fullText += "\n" + item.TeacherName;

            subjRange.Value = fullText;

            // Адаптивный шрифт
            if (fullText.Length > 50) subjRange.Font.Size = 7;
            else if (fullText.Length > 30) subjRange.Font.Size = 8;
            else subjRange.Font.Size = 9;

            string r = item.RoomNumber;
            if (r.Trim().ToLower() == "с" || r.Trim().ToLower() == "c") r = "";
            roomRange.Value = r;
        }

        private void DrawThinBorder(Excel.Range range)
        {
            range.Borders.LineStyle = Excel.XlLineStyle.xlContinuous;
            range.Borders.Weight = Excel.XlBorderWeight.xlThin;
            range.Borders.Color = System.Drawing.Color.Black;
        }
    }
}